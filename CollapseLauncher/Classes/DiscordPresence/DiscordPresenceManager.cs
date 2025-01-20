#if !DISABLEDISCORD
    using CollapseLauncher.Helper.Metadata;
    using CollapseLauncher.Helper.Update;
    using DiscordRPC;
    using Hi3Helper;
    using System;
    using System.Threading.Tasks.Dataflow;
    using static Hi3Helper.Locale;
    using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable StringLiteralTypo
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault

    namespace CollapseLauncher.DiscordPresence
    {
        #region Enums

        public enum ActivityType
        {
            None,
            Idle,
            Play,
            Update,
            Repair,
            Cache,
            GameSettings,
            AppSettings
        }

        #endregion

        public sealed partial class DiscordPresenceManager : IDisposable
        {
            #region Properties

            private DiscordRpcClient _client;

            private RichPresence                _presence;
            private ActivityType                _activityType;
            //private ActivityType                _lastAttemptedActivityType;
            private DateTime?                   _lastPlayTime;
            private bool                        _firstTimeConnect = true;
            private ActionBlock<RichPresence>   _presenceUpdateQueue;

            private bool _cachedIsIdleEnabled = true;

            public bool IdleEnabled
            {
                get
                {
                    bool value = GetAppConfigValue("EnableDiscordIdleStatus").ToBool();
                    _cachedIsIdleEnabled = value;
                    return value;
                }
                set
                {
                    SetAndSaveConfigValue("EnableDiscordIdleStatus", value);
                    _cachedIsIdleEnabled = value;
                }
            }

            #endregion

            public DiscordPresenceManager(bool initialStart = true)
            {
                if (!initialStart)
                {
                    return;
                }

                // Prepare idle cached setting
                Logger.LogWriteLine($"Doing initial start for Discord RPC!\r\n\tIdle status : {IdleEnabled}",
                                    LogType.Scheme);

                SetupPresence();
            }

            // Deconstruct and dispose unmanaged resources
            ~DiscordPresenceManager()
            {
                Dispose();
            }

            public void Dispose()
            {
                // Dispose Discord RPC client
                DisablePresence();

                // Suppress the GC from finalization
                GC.SuppressFinalize(this);
            }

            private void EnablePresence(long applicationId)
            {
                _firstTimeConnect = true;
                
                // Flush and dispose the session
                DisablePresence();

                // Initialize Discord RPC client
                _client = new DiscordRpcClient(applicationId.ToString(), ILoggerHelper.GetILogger("DiscordRPC"));
                _presenceUpdateQueue = new ActionBlock<RichPresence>(
                                            // ReSharper disable once UnusedParameter.Local
                                            presence => _client?.SetPresence(_presence),
                                                        new ExecutionDataflowBlockOptions
                                                        {
                                                            MaxMessagesPerTask      = 1,
                                                            MaxDegreeOfParallelism  = 1,
                                                            EnsureOrdered           = true
                                                        });

                _client.OnReady += (_, msg) =>
                                   {
                                       Logger.LogWriteLine($"Connected to Discord with user {msg.User.Username}");
                                       if (!_firstTimeConnect)
                                       {
                                           // Restart Discord RPC client
                                           _firstTimeConnect = true;
                                           SetupPresence();
                                       }
                                       else
                                       {
                                           // Restore our last activity
                                           if (!(!_cachedIsIdleEnabled && (_activityType == ActivityType.Idle ||
                                                                           _activityType == ActivityType.None)))
                                           {
                                               SetActivity(_activityType);
                                           }

                                           _firstTimeConnect = false;
                                       }
                                   };
                _client.OnPresenceUpdate += (_, msg) =>
                                            {
                                                if (msg.Presence == null)
                                                {
                                                    Logger.LogWriteLine("Activity cleared!");
                                                }
                                                else
                                                {
                                                    Logger.LogWriteLine(msg.Presence.State == null
                                                                            ? $"Activity updated! => {msg.Presence.Details}"
                                                                            : $"Activity updated! => {msg.Presence.Details} - {msg.Presence.State}");
                                                }
                                            };

                if (!_client.Initialize())
                {
                    Logger.LogWriteLine("Error initializing Discord Presence.", LogType.Warning, true);
                    return;
                }

                Logger.LogWriteLine("Discord Presence is Enabled!");
            }

            public void DisablePresence()
            {
                _presenceUpdateQueue?.Complete();
                _client?.SetPresence(null);
                _client?.Dispose();
                _client = null;
            }

            public void SetupPresence()
            {
                string gameCategory        = GetAppConfigValue("GameCategory").ToString();
                bool   isGameStatusEnabled = GetAppConfigValue("EnableDiscordGameStatus").ToBool();

                if (isGameStatusEnabled)
                {
                    switch (gameCategory)
                    {
                        case "Honkai: Star Rail":
                            EnablePresence(AppDiscordApplicationID_HSR);
                            return;
                        case "Honkai Impact 3rd":
                            EnablePresence(AppDiscordApplicationID_HI3);
                            return;
                        case "Genshin Impact":
                            EnablePresence(AppDiscordApplicationID_GI);
                            return;
                        case "Zenless Zone Zero":
                            EnablePresence(AppDiscordApplicationID_ZZZ);
                            return;
                    default:
                            Logger.LogWriteLine("Discord Presence (Unknown Game)", LogType.Error, true);
                            break;
                    }
                }

                EnablePresence(AppDiscordApplicationID);
            }

            public void SetActivity(ActivityType activity, DateTime? activityOffset = null)
            {
                if (!GetAppConfigValue("EnableDiscordRPC").ToBool())
                {
                    return;
                }

                //_lastAttemptedActivityType = activity;
                _activityType              = activity;

                switch (activity)
                {
                    case ActivityType.Play:
                    {
                        bool isGameStatusEnabled = GetAppConfigValue("EnableDiscordGameStatus").ToBool();
                        BuildActivityGameStatus(isGameStatusEnabled ? Lang._Misc.DiscordRP_InGame : Lang._Misc.DiscordRP_Play,
                                                isGameStatusEnabled, activityOffset);
                        break;
                    }
                    case ActivityType.Update:
                    {
                        bool isGameStatusEnabled = GetAppConfigValue("EnableDiscordGameStatus").ToBool();
                        BuildActivityGameStatus(Lang._Misc.DiscordRP_Update, isGameStatusEnabled);
                        break;
                    }
                    case ActivityType.Repair:
                        BuildActivityAppStatus(Lang._Misc.DiscordRP_Repair);
                        break;
                    case ActivityType.Cache:
                        BuildActivityAppStatus(Lang._Misc.DiscordRP_Cache);
                        break;
                    case ActivityType.GameSettings:
                        BuildActivityAppStatus(Lang._Misc.DiscordRP_GameSettings);
                        break;
                    case ActivityType.AppSettings:
                        BuildActivityAppStatus(Lang._Misc.DiscordRP_AppSettings);
                        break;
                    case ActivityType.Idle:
                        _lastPlayTime = null;
                        if (_cachedIsIdleEnabled)
                        {
                            BuildActivityAppStatus(Lang._Misc.DiscordRP_Idle);
                        }
                        else
                        {
                            _presence = null; // Clear presence
                        }

                        break;
                    default:
                        _presence = new RichPresence
                        {
                            Details = Lang._Misc.DiscordRP_Default,
                            Assets = new Assets
                            {
                                LargeImageKey = "launcher-logo-new",
                                LargeImageText =
                                    $"Collapse Launcher v{LauncherUpdateHelper.LauncherCurrentVersionString} {(IsPreview ? "Preview" : "Stable")}"
                            }
                        };
                        break;
                }

                UpdateActivity();
            }

            private void BuildActivityGameStatus(string activityName, bool isGameStatusEnabled, DateTime? activityOffset = null)
            {
                var curGameName   = LauncherMetadataHelper.CurrentMetadataConfigGameName;
                var curGameRegion = LauncherMetadataHelper.CurrentMetadataConfigGameRegion;

                if (string.IsNullOrEmpty(curGameName) || string.IsNullOrEmpty(curGameRegion))
                    return;

                var curGameNameTranslate =
                    InnerLauncherConfig.GetGameTitleRegionTranslationString(curGameName, Lang._GameClientTitles);
                var curGameRegionTranslate =
                    InnerLauncherConfig.GetGameTitleRegionTranslationString(curGameRegion, Lang._GameClientRegions);
                
                _presence = new RichPresence
                {
                    Details =
                        $"{activityName} {(!isGameStatusEnabled ? curGameName : null)}",
                    State = $"{Lang._Misc.DiscordRP_Region} " +
                            $"{curGameRegionTranslate}",
                    Assets = new Assets
                    {
                        LargeImageKey  = $"game-{LauncherMetadataHelper.CurrentMetadataConfig?.GameType.ToString().ToLower()}-logo",
                        LargeImageText = $"{curGameNameTranslate} - {curGameRegionTranslate}",
                        SmallImageKey  = "launcher-logo-new",
                        SmallImageText = $"Collapse Launcher v{LauncherUpdateHelper.LauncherCurrentVersionString} " +
                                         $"{(IsPreview ? "Preview" : "Stable")}"
                    },
                    Timestamps = new Timestamps
                    {
                        Start = GetCachedStartPlayTime(activityOffset)
                    }
                };
            }

            private DateTime GetCachedStartPlayTime(DateTime? activityOffset)
            {
                _lastPlayTime ??= activityOffset;
                _lastPlayTime ??= DateTime.UtcNow;
                return _lastPlayTime.Value;
            }

            private void BuildActivityAppStatus(string activityName)
            {
                var curGameName   = LauncherMetadataHelper.CurrentMetadataConfigGameName;
                var curGameRegion = LauncherMetadataHelper.CurrentMetadataConfigGameRegion;
                
                if (string.IsNullOrEmpty(curGameName) || string.IsNullOrEmpty(curGameRegion))
                    return;
                
                var curGameNameTranslate =
                    InnerLauncherConfig.GetGameTitleRegionTranslationString(curGameName, Lang._GameClientTitles);
                var curGameRegionTranslate =
                    InnerLauncherConfig.GetGameTitleRegionTranslationString(curGameRegion, Lang._GameClientRegions);
                
                _presence = new RichPresence
                {
                    Details = activityName,
                    State   = $"{Lang._Misc.DiscordRP_Region} {curGameRegionTranslate}",
                    Assets  = new Assets
                    {
                        LargeImageKey  = $"game-{LauncherMetadataHelper.CurrentMetadataConfig?.GameType.ToString().ToLower()}-logo",
                        LargeImageText = $"{curGameNameTranslate}",
                        SmallImageKey  = "launcher-logo-new",
                        SmallImageText = $"Collapse Launcher v{LauncherUpdateHelper.LauncherCurrentVersionString} " +
                                         $"{(IsPreview ? "Preview" : "Stable")}"
                    }
                };
            }

            private void UpdateActivity()
            {
                _presenceUpdateQueue?.Post(_presence);
            }
        }
    }
#endif