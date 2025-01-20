using CollapseLauncher.Interfaces;
using CollapseLauncher.Helper.Database;
using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Hi3Helper.SentryHelper;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GamePlaytime
{
    internal class CollapsePlaytime
    {
        #region Fields
        private static DateTime BaseDate => new(2012, 2, 13, 0, 0, 0, DateTimeKind.Utc);

        private const string TotalTimeValueName  = "CollapseLauncher_Playtime";
        private const string LastPlayedValueName = "CollapseLauncher_LastPlayed";
        private const string StatsValueName      = "CollapseLauncher_PlaytimeStats";

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private  static HashSet<int>      _isDeserializing = [];
        private         RegistryKey       _registryRoot;
        private         int               _hashID;
        private         IGameVersionCheck _gameVersion;
        private         IGameSettings     _gameSettings;
        
        #endregion

        #region Properties
        /// <summary>
        /// Represents the total time a game was played.<br/><br/>
        /// Default: TimeSpan.Zero
        /// </summary>
        [JsonIgnore]
        public TimeSpan TotalPlaytime { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Represents the daily playtime.<br/>
        /// The ControlDate field is used to check if this value should be reset.<br/><br/>
        /// Default: TimeSpan.Zero
        /// </summary>
        public TimeSpan DailyPlaytime { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Represents the weekly playtime.<br/>
        /// The ControlDate field is used to check if this value should be reset.<br/><br/>
        /// Default: TimeSpan.Zero
        /// </summary>
        public TimeSpan WeeklyPlaytime { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Represents the monthly playtime.<br/>
        /// The ControlDate field is used to check if this value should be reset.<br/><br/>
        /// Default: TimeSpan.Zero
        /// </summary>
        public TimeSpan MonthlyPlaytime { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Represents the total time the last/current session lasted.<br/><br/>
        /// Default: TimeSpan.Zero
        /// </summary>
        public TimeSpan LastSession { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Represents the last time the game was launched.<br/><br/>
        /// Default: null
        /// </summary>
        [JsonIgnore]
        public DateTime? LastPlayed { get; set; }

        /// <summary>
        /// Represents a control date.<br/>
        /// This date is used to check if a specific playtime statistic should be reset.<br/><br/>
        /// Default: DateTime.Today
        /// </summary>
        public DateTime ControlDate { get; set; } = DateTime.Today;
        #endregion

        #region Methods
#nullable enable
        /// <summary>
        /// Reads from the Registry and deserializes the contents.
        /// </summary>
        public static CollapsePlaytime Load(RegistryKey root, int hashID,
                                            IGameVersionCheck gameVersion,
                                            IGameSettings gameSettings)
        {
            try
            {
                _isDeserializing.Add(hashID);
                if (root == null) throw new NullReferenceException("Cannot load playtime. RegistryKey is unexpectedly not initialized!");

                int? totalTime = (int?)root.GetValue(TotalTimeValueName,null);
                int? lastPlayed = (int?)root.GetValue(LastPlayedValueName,null);
                object? stats = root.GetValue(StatsValueName, null);

                CollapsePlaytime? playtimeInner;

                if (stats != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])stats;
#if DEBUG
                    LogWriteLine($"Loaded Playtime:\r\nTotal: {totalTime}s\r\nLastPlayed: {lastPlayed}\r\nStats: {Encoding.UTF8.GetString(byteStr.TrimEnd((byte)0))}", LogType.Debug, true);
#endif
                    playtimeInner = byteStr.Deserialize(UniversalPlaytimeJsonContext.Default.CollapsePlaytime, new CollapsePlaytime())!;
                }
                else
                {
                    playtimeInner = new CollapsePlaytime();
                }

                playtimeInner._gameVersion  = gameVersion;
                playtimeInner._gameSettings = gameSettings;
                playtimeInner._registryRoot = root;
                playtimeInner._hashID       = hashID;
                playtimeInner.TotalPlaytime = TimeSpan.FromSeconds(totalTime ?? 0);
                playtimeInner.LastPlayed    = lastPlayed != null ? BaseDate.AddSeconds((int)lastPlayed) : null;
                playtimeInner.CheckStatsReset();
                
                return playtimeInner;
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed while reading playtime.\r\n{ex}", LogType.Error, true);
            }
            finally
            {
                _isDeserializing.Remove(hashID);
            }

            return new CollapsePlaytime
            { 
                _hashID = hashID,
                _registryRoot = root,
                _gameVersion = gameVersion,
                _gameSettings = gameSettings
            };
        }

        /// <summary>
        /// Serializes all fields and saves them to the Registry.
        /// </summary>
        public void Save(bool forceUpdateDb = false)
        {
            try
            {
                if (_registryRoot == null) throw new NullReferenceException("Cannot save playtime since RegistryKey is unexpectedly not initialized!");

                string data = this.Serialize(UniversalPlaytimeJsonContext.Default.CollapsePlaytime);
                byte[] dataByte = Encoding.UTF8.GetBytes(data);
#if DEBUG
                LogWriteLine($"Saved Playtime:\r\n{data}", LogType.Debug, true);
#endif
                _registryRoot.SetValue(StatsValueName, dataByte, RegistryValueKind.Binary);
                _registryRoot.SetValue(TotalTimeValueName, TotalPlaytime.TotalSeconds, RegistryValueKind.DWord);
                
                double? lastPlayed = (LastPlayed?.ToUniversalTime() - BaseDate)?.TotalSeconds;
                if (lastPlayed != null)
                    _registryRoot.SetValue(LastPlayedValueName, lastPlayed, RegistryValueKind.DWord);
                
                if (DbHandler.IsEnabled && _gameSettings.AsIGameSettingsUniversal().SettingsCollapseMisc.IsSyncPlaytimeToDatabase &&
                    ((DateTime.Now - LastDbUpdate).TotalMinutes >= 5 || forceUpdateDb)) // Sync only every 5 minutes to reduce database usage
                {
                    _ = UpdatePlaytime_Database_Push(data, TotalPlaytime.TotalSeconds, lastPlayed);
                }
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed to save playtime!\r\n{ex}", LogType.Error, true);
            }
        }

        /// <summary>
        /// Resets all fields and saves to the Registry.
        /// </summary>
        public void Reset()
        {
            TotalPlaytime   = TimeSpan.Zero;
            LastSession     = TimeSpan.Zero;
            DailyPlaytime   = TimeSpan.Zero;
            WeeklyPlaytime  = TimeSpan.Zero;
            MonthlyPlaytime = TimeSpan.Zero;
            ControlDate     = DateTime.Today;
            LastPlayed      = null;

            if (!_isDeserializing.Contains(_hashID)) Save(true);
        }

        /// <summary>
        /// Updates the current Playtime TimeSpan to the provided value and saves to the registry.<br/><br/>
        /// </summary>
        /// <param name="timeSpan">New playtime value</param>
        /// <param name="reset">Reset all other fields</param>
        /// <param name="forceUpdateDb">Force update database data</param>
        public void Update(TimeSpan timeSpan, bool reset = true, bool forceUpdateDb = false)
        {
            if (reset)
            {
                LastSession     = TimeSpan.Zero;
                DailyPlaytime   = TimeSpan.Zero;
                WeeklyPlaytime  = TimeSpan.Zero;
                MonthlyPlaytime = TimeSpan.Zero;
                ControlDate     = DateTime.Today;
            }
            
            TotalPlaytime = timeSpan;

            if (!_isDeserializing.Contains(_hashID)) Save(forceUpdateDb);
        }

        /// <summary>
        /// Checks if any stats should be reset.<br/>
        /// Afterwards, the values are saved to the registry.<br/><br/>
        /// </summary>
        private void CheckStatsReset()
        {
            DateTime today  = DateTime.Today;
            
            if (ControlDate == today)
                return;
            
            DailyPlaytime = TimeSpan.Zero;
            if (IsDifferentWeek(ControlDate, today))
                WeeklyPlaytime = TimeSpan.Zero;
            if (IsDifferentMonth(ControlDate, today))
                MonthlyPlaytime = TimeSpan.Zero;
            
            ControlDate = today;
        }

        /// <summary>
        /// Adds a minute to all fields, and checks if any should be reset.<br/>
        /// Afterwards, the values are saved to the registry.<br/><br/>
        /// </summary>
        public void AddMinute()
        {
            TimeSpan minute = TimeSpan.FromMinutes(1);

            TotalPlaytime = TotalPlaytime.Add(minute);
            LastSession   = LastSession.Add(minute);
            
            CheckStatsReset();
            
            DailyPlaytime   = DailyPlaytime.Add(minute);
            WeeklyPlaytime  = WeeklyPlaytime.Add(minute);
            MonthlyPlaytime = MonthlyPlaytime.Add(minute);

            if (!_isDeserializing.Contains(_hashID)) Save();
        }

        #endregion

        #region Utility
        private static bool IsDifferentMonth(DateTime date1, DateTime date2) => date1.Year != date2.Year || date1.Month != date2.Month;

        private static bool IsDifferentWeek(DateTime date1, DateTime date2) => date1.Year != date2.Year || ISOWeek.GetWeekOfYear(date1) != ISOWeek.GetWeekOfYear(date2);
        #endregion

        #region Database Extension
        // processing flags, prevents double task
        private bool _isDbSyncing;
        private bool _isDbPulling;
        private bool _isDbPullSuccess;

        public DateTime LastDbUpdate = DateTime.MinValue;
        
        // value store
        private string? _jsonDataDb;
        private double? _totalTimeDb;
        private double? _lastPlayedDb;
        private int?    _unixStampDb;
        
        // Key names
        private string KeyPlaytimeJson => $"{_gameVersion.GameType.ToString()}-{_gameVersion.GameRegion}-pt-js";
        private string KeyTotalTime    => $"{_gameVersion.GameType.ToString()}-{_gameVersion.GameRegion}-pt-total";
        private string KeyLastPlayed   => $"{_gameVersion.GameType.ToString()}-{_gameVersion.GameRegion}-pt-lastPlayed";
        private string KeyLastUpdated  => $"{_gameVersion.GameType.ToString()}-{_gameVersion.GameRegion}-pt-lu";
        

        #region Sync Methods
        /// <summary>
        /// Sync from/to DB at init
        /// </summary>
        /// <returns>true if require refresh, false if dont.</returns>
        public async ValueTask<(bool IsUpdated, CollapsePlaytime? PlaytimeData)> DbSync(bool redirectThrow = false)
        {
            LogWriteLine("[CollapsePlaytime::DbSync] Starting sync operation...", LogType.Default, true);
            try
            {
                // Fetch database last update stamp
                var stampDbStr = await DbHandler.QueryKey(KeyLastUpdated);
                _unixStampDb     = !string.IsNullOrEmpty(stampDbStr) ? Convert.ToInt32(stampDbStr) : null;
                
                // Compare unix stamp from config
                var unixStampLocal = Convert.ToInt32(DbConfig.GetConfig(KeyLastUpdated).ToString());
                if (_unixStampDb == unixStampLocal) 
                {
                    LogWriteLine("[CollapsePlaytime::DbSync] Sync stamp equal, nothing needs to be done~", LogType.Default, true);
                    return (false, null); // Do nothing if stamp is equal
                }

                // When Db stamp is newer, sync from Db
                if (_unixStampDb > unixStampLocal)
                {
                    // Pull values from DB
                    await UpdatePlaytime_Database_Pull();
                    if (!_isDbPullSuccess) 
                    {
                        LogWriteLine("[CollapsePlaytime::DbSync] Database pull failed, skipping sync~", LogType.Error);
                        return (false, null); // Return if pull failed
                    }
                    
                    if (string.IsNullOrEmpty(_jsonDataDb))
                    {
                        LogWriteLine("[CollapsePlaytime::DbSync] _jsonDataDb is empty, skipping sync~", default, true);
                        return (false, null);
                    }
                    LogWriteLine("[CollapsePlaytime::DbSync] Database data is newer! Pulling data~", LogType.Default, true);
                    CollapsePlaytime? playtimeInner = _jsonDataDb.Deserialize(UniversalPlaytimeJsonContext.Default.CollapsePlaytime,
                                                        new CollapsePlaytime());

                    playtimeInner!.TotalPlaytime = TimeSpan.FromSeconds(_totalTimeDb ?? 0);
                    if (_lastPlayedDb != null) playtimeInner.LastPlayed = BaseDate.AddSeconds((int)_lastPlayedDb);
                    playtimeInner._registryRoot = _registryRoot;
                    playtimeInner._gameSettings = _gameSettings;
                    playtimeInner._hashID       = _hashID;
                    playtimeInner._gameVersion  = _gameVersion;
                    
                    playtimeInner.CheckStatsReset();
                    playtimeInner.Save();
                    
                    DbConfig.SetAndSaveValue(KeyLastUpdated, _unixStampDb.ToString());
                    LastDbUpdate = DateTime.Now;
                    
                    return (true, playtimeInner);
                }

                if (_unixStampDb < unixStampLocal)
                {
                    LogWriteLine("[CollapsePlaytime::DbSync] Database data is older! Pushing data~", default, true);
                    Save(true);
                }
                return (false, null);
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"[CollapsePlaytime::DbSync] Failed when trying to do sync operation\r\n{ex}",
                             LogType.Error, true);
                if (redirectThrow)
                {
                    throw;
                }

                return (false, null);
            }
        }
        #endregion
        
        #region DB Operation Methods
        private async Task UpdatePlaytime_Database_Push(string jsonData, double totalTime, double? lastPlayed)
        {
            if (_isDbSyncing) return;
            var curDateTime = DateTime.Now;
            _isDbSyncing = true;
            try
            {
                var unixStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await DbHandler.StoreKeyValue(KeyPlaytimeJson, jsonData, true);
                await DbHandler.StoreKeyValue(KeyTotalTime,    totalTime.ToString(CultureInfo.InvariantCulture), true);
                await DbHandler.StoreKeyValue(KeyLastPlayed,   lastPlayed != null ? lastPlayed.Value.ToString(CultureInfo.InvariantCulture) : "null", true);
                await DbHandler.StoreKeyValue(KeyLastUpdated,  unixStamp.ToString(), true);
                DbConfig.SetAndSaveValue(KeyLastUpdated, unixStamp);
                _unixStampDb = Convert.ToInt32(unixStamp);
                LastDbUpdate = curDateTime;
                LogWriteLine("[CollapsePlaytime::UpdatePlaytime_Database_Push()] Successfully uploaded playtime data to database!", LogType.Scheme, true);
            }
            catch (Exception e)
            {
                await SentryHelper.ExceptionHandlerAsync(e, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed when syncing Playtime to DB!\r\n{e}", LogType.Error, true);
            }
            finally
            {
                _isDbSyncing = false;
            }
        }
        
        private async Task UpdatePlaytime_Database_Pull()
        {
            if (_isDbPulling) return;
            _isDbPullSuccess = false;
            try
            {
                _isDbPulling = true;

                _jsonDataDb   = await DbHandler.QueryKey(KeyPlaytimeJson, true);
                
                var totalTimeDbStr = await DbHandler.QueryKey(KeyTotalTime, true);
                _totalTimeDb  = string.IsNullOrEmpty(totalTimeDbStr) ? null : Convert.ToDouble(totalTimeDbStr, CultureInfo.InvariantCulture);
                
                var lpDb = await DbHandler.QueryKey(KeyLastPlayed, true);
                _lastPlayedDb    = !string.IsNullOrEmpty(lpDb) && !lpDb.Contains("null") ? Convert.ToDouble(lpDb, CultureInfo.InvariantCulture) : null; // if Db data is null, return null
                
                _isDbPullSuccess = true;
                LogWriteLine("[CollapsePlaytime::UpdatePlaytime_Database_Pull()] Successfully pulled data from database!", LogType.Scheme, true);
            }
            catch (Exception e)
            {
                await SentryHelper.ExceptionHandlerAsync(e, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed when syncing Playtime to DB!\r\n{e}", LogType.Error, true);
            }
            finally
            {
                _isDbPulling = false;
            }
        }
        #endregion
        #endregion
    }
}
