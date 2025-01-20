using Hi3Helper;
using Libsql.Client;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

#nullable enable
namespace CollapseLauncher.Helper.Database
{
    internal static class DbHandler
    {
        #region Config Properties

        private static bool? _enabled;
        public static bool IsEnabled
        {
            get
            {
                if (_enabled != null) return (bool)_enabled;
                var c = DbConfig.DbEnabled;
                _enabled = c;
                return c;
            }
            set
            {
                _enabled           = value;
                DbConfig.DbEnabled = value;

                _isFirstInit = true; // Force first init
                if (value) _ = Init();
                else Dispose(); // Dispose instance if user disabled database function globally
            }
        }
        
        
        private static string? _uri;
        public static string Uri
        {
            get
            {
                if (!string.IsNullOrEmpty(_uri)) return _uri;
                var c = DbConfig.DbUrl;
                _uri = c;
                return c;
            }
            set
            {
                if (value != _uri) _isFirstInit = true; // Force first init if value changed
                
                _uri           = value;
                DbConfig.DbUrl = value;
                _isFirstInit   = true;
            }
        }
        
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static string? _token;
        
        [DebuggerHidden]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static string Token
        {
            get
            {
                if (!string.IsNullOrEmpty(_token)) return _token;
                var c = DbConfig.DbToken;
                _token = c;
                return c;
            }
            set
            {
                if (value != _token) _isFirstInit = true; // Force first init if value changed
                
                _token           = value;
                DbConfig.DbToken = value;
                _isFirstInit     = true;
            }
        }
        
        private static string? _userId;
        private static string? _userIdHash;
        public static string UserId
        {
            get
            {
                if (_userId != null) return _userId;
                var c = DbConfig.UserGuid; // Get or create (if not yet has one) GUIDv7
                _userId = c;
                _userIdHash = Convert.ToHexStringLower(System.IO.Hashing.XxHash64.Hash(Encoding.ASCII.GetBytes(c))); 
                // Get hash for the UserID to be used as SQL table name
                // I know that this is overkill, but I want it to be totally non-identifiable if for some reason someone
                // has access to their database. It also lowers the amount of query command length to be sent, hopefully
                // reducing access latency.
                // p.s. oh yeah, this is also why user won't be able to get their data back if they lost the GUID,
                // good luck reversing Xxhash64 back to string. Technically possible, but good luck!
                return c;
            }
            set
            {
                if (value != _userId) _isFirstInit = true; // Force first init if value changed
                
                _userId           = value;
                DbConfig.UserGuid = value;
                
                var byteUidH = System.IO.Hashing.XxHash64.Hash(Encoding.ASCII.GetBytes(value));
                _userIdHash  = Convert.ToHexStringLower(byteUidH);
                _isFirstInit = true;
            }
        }

        private static bool _isFirstInit = true;
        #endregion
        
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static IDatabaseClient? _database;

        public static async Task Init(bool redirectThrow = false, bool bypassEnableFlag = false)
        {
            DbConfig.Init();
            
            if (!bypassEnableFlag && !IsEnabled)
            {
                LogWriteLine("[DbHandler::Init] Database functionality is disabled!");
                return;
            }

            try
            {
                // Init props
                _ = Token;
                _ = Uri;
                _ = UserId;

                if (string.IsNullOrEmpty(Uri))
                    throw new NullReferenceException(Lang._SettingsPage.Database_Error_EmptyUri);
                if (string.IsNullOrEmpty(Token))
                    throw new NullReferenceException(Lang._SettingsPage.Database_Error_EmptyToken);

                // Connect to database
                // Libsql-client-dotnet technically support file based SQLite by pushing `file://` proto in the URL.
                // But what's the point?
                _database = await DatabaseClient.Create(opts =>
                                                        {
                                                            opts.Url       = Uri;
                                                            opts.AuthToken = Token;
                                                        });

                if (_isFirstInit)
                {
                    LogWriteLine("[DbHandler::Init] Initializing database system...");
                    // Ensure table exist at first initialization
                    await
                        _database
                           .Execute($"CREATE TABLE IF NOT EXISTS \"uid-{_userIdHash}\" (Id INTEGER PRIMARY KEY AUTOINCREMENT, 'key' TEXT UNIQUE NOT NULL, 'value' TEXT)");
                    _isFirstInit = false;
                }
                else LogWriteLine("[DbHandler::Init] Reinitializing database system...");
            }
            catch (DllNotFoundException e)
            {
                LogWriteLine("[DbHandler::Init] Error when connecting to database system! Probably missing Visual C/C++ redist!\r\n" + e,
                             LogType.Error, true);
                if (redirectThrow) throw;
            }
            // No need to handle all these error catcher with sentry
            // The error should be handled in the method caller instead
            catch (LibsqlException e) when (e.Message.Contains("`api error: `{\"error\":\"Unauthorized: `The JWT is invalid`\"}``",
                                                               StringComparison.InvariantCultureIgnoreCase) && !redirectThrow)
            {
                LogWriteLine($"[DBHandler::Init] Error when connecting to database system! Token invalid!\r\n{e}",
                             LogType.Error, true);
            }
            catch (Exception e) when (!redirectThrow)
            {
                LogWriteLine($"[DBHandler::Init] Error when (re)initializing database system!\r\n{e}", LogType.Error, true);
            }
            catch (LibsqlException e) when (e.Message.Contains("`api error: `{\"error\":\"Unauthorized: `The JWT is invalid`\"}``",
                                                               StringComparison.InvariantCultureIgnoreCase))
            {
                LogWriteLine($"[DBHandler::Init] Error when connecting to database system! Token invalid!\r\n{e}",
                             LogType.Error, true);
                var ex = new AggregateException("Unauthorized: wrong token inserted", e);
                throw ex;
            }
            catch (Exception e)
            {
                LogWriteLine($"[DBHandler::Init] Error when (re)initializing database system!\r\n{e}", LogType.Error, true);
                throw;
            }
        }

        private static void Dispose()
        {
            _database = null;
            _token = null;
            _uri = null;
            _userId = null;
            _userIdHash = null;
        }

        public static async Task<string?> QueryKey(string key, bool redirectThrow = false)
        {
            if (!IsEnabled) return null;
        #if DEBUG
            var r   = new Random();
            var sId = Math.Abs(r.Next(0, 1000).ToString().GetHashCode());
            LogWriteLine($"[DBHandler::QueryKey][{sId}] Invoked!\r\n\tKey: {key}", LogType.Debug, true);
            var t = Stopwatch.StartNew();
        #endif
            const int retryCount = 3;
            for (var i = 0; i < retryCount; i++)
            {
                try
                {
                    if (_database == null) await Init(true);
                    // Get table row for exact key
                    var rs =
                        await
                            _database!
                               .Execute($"SELECT value FROM \"uid-{_userIdHash}\" WHERE key = ?", key);
                    if (rs == null)
                    {
                        continue;
                    }

                    // freaking black magic to convert the column row to the value 
                    var str =
                        string.Join("", rs.Rows.Select(row => string.Join("", row.Select(x => x.ToString()))));
                #if DEBUG
                    LogWriteLine($"[DBHandler::QueryKey][{sId}] Got value!\r\n\tKey: {key}\r\n\tValue:\r\n{str}", LogType.Debug,
                                 true);
                #endif
                    return str;
                }
                // No need to handle all these error catcher with sentry
                // The error should be handled in the method caller instead
                catch (LibsqlException ex) when ((ex.Message.Contains("STREAM_EXPIRED") ||
                                                  ex.Message.Contains("Received an invalid baton")) &&
                                                 i < retryCount - 1)
                {
                    if (i > 0)
                        LogWriteLine("[DBHandler::QueryKey] Database stream expired, retrying...", LogType.Error, true);

                    await Init();
                }
                catch (Exception ex) when (i < retryCount - 1)
                {
                    LogWriteLine($"[DBHandler::QueryKey] Failed when getting value for key {key}! Retrying...\r\n{ex}",
                                 LogType.Error, true);
                    break;
                }
                catch (Exception ex) when (!redirectThrow)
                {
                    LogWriteLine($"[DBHandler::QueryKey] Failed when getting value for key {key} after {retryCount} retries! Returning null...\r\n{ex}",
                                 LogType.Error, true);
                    return null;
                }
                catch (Exception ex)
                {
                    LogWriteLine($"[DBHandler::QueryKey] Failed when getting value for key {key} after {retryCount} retries! Returning null...\r\n{ex}",
                                 LogType.Error, true);
                    throw;
                }
            #if DEBUG
                finally
                {
                    t.Stop();
                    LogWriteLine($"[DBHandler::QueryKey][{sId}] Operation took {t.ElapsedMilliseconds} ms!", LogType.Debug, true);
                }
            #endif
            }

            return null;
        }

        public static async Task StoreKeyValue(string key, string value, bool redirectThrow = false)
        {
            if (!IsEnabled) return;
        #if DEBUG
            var t   = Stopwatch.StartNew();
            var r   = new Random();
            var sId = Math.Abs(r.Next(0, 1000).ToString().GetHashCode());
            LogWriteLine($"[DBHandler::StoreKeyValue][{sId}] Invoked!\r\n\tKey: {key}\r\n\tValue: {value}", LogType.Debug,
                         true);
        #endif
            const int retryCount = 5;
            for (var i = 0; i < retryCount; i++)
            {
                try
                {
                    if (_database == null) await Init(true);
                    
                    // Create key for storing value, if key already exist, just update the value (key column is set to UNIQUE)
                    var command = $"INSERT INTO \"uid-{_userIdHash}\" (key, value) VALUES (?, ?) " +
                                  $"ON CONFLICT(key) DO UPDATE SET value = ?";
                    var parameters = new object[] { key, value, value };
                    await _database!.Execute(command, parameters);
                    break;
                }
                catch (LibsqlException ex) when ((ex.Message.Contains("STREAM_EXPIRED") ||
                                                  ex.Message.Contains("Received an invalid baton")) &&
                                                 i < retryCount - 1)
                {
                    if (i > 0)
                        LogWriteLine("[DBHandler::StoreKeyValue] Database stream expired, retrying...", LogType.Error,
                                     true);

                    await Init();
                }
                // No need to handle all these error catcher with sentry
                // The error should be handled in the method caller instead
                catch (Exception ex) when (i < retryCount - 1)
                {
                    LogWriteLine($"[DBHandler::StoreKeyValue] Failed when saving value for key {key}! Retrying...\r\n{ex}",
                                 LogType.Error, true);
                    break;
                }
                catch (Exception ex) when (!redirectThrow)
                {
                    LogWriteLine($"[DBHandler::StoreKeyValue] Failed when saving value for key {key} after {retryCount} tries!\r\n{ex}",
                                 LogType.Error, true);
                    return;
                }
                catch (Exception ex)
                {
                    LogWriteLine($"[DBHandler::StoreKeyValue] Failed when saving value for key {key} after {retryCount} tries!\r\n{ex}",
                                 LogType.Error, true);
                    throw;
                }
            #if DEBUG
                finally
                {
                    t.Stop();
                    LogWriteLine($"[DBHandler::StoreKeyValue][{sId}] Operation took {t.ElapsedMilliseconds} ms!",
                                 LogType.Debug, true);
                }
            #endif
            }
        }
    }
}