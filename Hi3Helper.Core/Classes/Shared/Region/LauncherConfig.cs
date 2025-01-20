using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Win32.Screen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using System.Text;
using static Hi3Helper.Locale;
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
#pragma warning disable CA2211

#nullable enable
namespace Hi3Helper.Shared.Region
{
    #region CDN Property

    public readonly struct CDNURLProperty : IEquatable<CDNURLProperty>
    {
        public string URLPrefix              { get; init; }
        public string Name                   { get; init; }
        public string Description            { get; init; }
        public bool   PartialDownloadSupport { get; init; }

        public bool Equals(CDNURLProperty other)
        {
            return URLPrefix == other.URLPrefix && Name == other.Name && Description == other.Description;
        }
    }

    #endregion

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "RedundantDefaultMemberInitializer")]
    public static class LauncherConfig
    {
        #region Main Launcher Config Methods

        public static void InitAppPreset()
        {
            // Initialize resolution settings first and assign AppConfigFile to ProfilePath
            InitScreenResSettings();
            AppConfigProperty.ProfilePath = AppConfigFile;

            // Set user permission check to its default and check for the existence of config file.
            bool IsConfigFileExist = File.Exists(AppConfigProperty.ProfilePath);

            // If the config file is exist, then continue to load the file
            if (IsConfigFileExist)
            {
                LoadAppConfig();
            }

            // If the section doesn't exist, then add the section template
            if (!AppConfigProperty.Profile.ContainsKey(SectionName))
            {
                AppConfigProperty.Profile.Add(SectionName, AppSettingsTemplate);
            }

            // Check and assign default for the null and non-existence values.
            CheckAndSetDefaultConfigValue();

            // Set the startup background path and GameFolder to check if user has permission.
            string? gameFolder = GetAppConfigValue("GameFolder").ToString();

            // Check if the drive exist. If not, then reset the GameFolder variable and set IsFirstInstall to true;
            if (!string.IsNullOrEmpty(gameFolder) && !IsDriveExist(gameFolder))
            {
                IsFirstInstall = true;

                // Reset GameFolder to default value
                SetAppConfigValue("GameFolder", AppSettingsTemplate["GameFolder"]);

                // Force enable Console Log and return
                Logger.CurrentLogger = new LoggerConsole(AppGameLogsFolder, Encoding.UTF8);
                Logger.LogWriteLine($"Game App Folder path: {gameFolder} doesn't exist! The launcher will be reinitialize the setup.",
                                    LogType.Error, true);
                return;
            }

            // Check if user has permission
            bool IsUserHasPermission = ConverterTool.IsUserHasPermission(gameFolder);

            // Assign boolean if IsConfigFileExist and IsUserHasPermission.
            IsFirstInstall = !(IsConfigFileExist && IsUserHasPermission);

            // Initialize the DownloadClient speed at start.
            // ignored
            _ = DownloadSpeedLimit;
        }

        public static bool IsConfigKeyExist(string key)
        {
            return AppConfigProperty.Profile[SectionName].ContainsKey(key);
        }

        public static IniValue GetAppConfigValue(string key)
        {
            return AppConfigProperty.Profile[SectionName][key];
        }

        public static void SetAndSaveConfigValue(string key, IniValue value, bool doNotLog = false)
        {
            SetAppConfigValue(key, value);
            SaveAppConfig();
        #if DEBUG
            if (!doNotLog)
                Logger.LogWriteLine($"SetAndSaveConfigValue::Key[{key}]::Value[{value}]", LogType.Debug);
        #endif
        }

        public static void SetAppConfigValue(string key, IniValue value)
        {
            AppConfigProperty.Profile[SectionName][key] = value;
        }

        public static void LoadAppConfig()
        {
            AppConfigProperty.Profile.Load(AppConfigProperty.ProfilePath);
        }

        public static void SaveAppConfig()
        {
            AppConfigProperty.Profile.Save(AppConfigProperty.ProfilePath);
        }

        public static void CheckAndSetDefaultConfigValue()
        {
            foreach (KeyValuePair<string, IniValue> Entry in AppSettingsTemplate)
            {
                if (!AppConfigProperty.Profile[SectionName].ContainsKey(Entry.Key) ||
                    AppConfigProperty.Profile[SectionName][Entry.Key].IsEmpty)
                {
                    SetAppConfigValue(Entry.Key, Entry.Value);
                }
            }
        }

        #endregion

        #region Misc Methods

        public static void LoadGamePreset()
        {
            AppGameFolder = Path.Combine(GetAppConfigValue("GameFolder")!);
        }

        private static bool IsDriveExist(string path)
        {
            return new DriveInfo(Path.GetPathRoot(path)!).IsReady;
        }

        private static void InitScreenResSettings()
        {
            foreach (var res in ScreenProp.EnumerateScreenSizes())
            {
                ScreenResolutionsList.Add($"{res.Width}x{res.Height}");
            }
        }

        #endregion

        #region CDN List

        public static List<CDNURLProperty> CDNList =>
        [
            new()
            {
                Name                   = "GitHub",
                URLPrefix              = "https://github.com/CollapseLauncher/CollapseLauncher-ReleaseRepo/raw/main",
                Description            = Lang._Misc!.CDNDescription_Github,
                PartialDownloadSupport = true
            },

            new()
            {
                Name                   = "Cloudflare",
                URLPrefix              = "https://r2.bagelnl.my.id/cl-cdn",
                Description            = Lang._Misc!.CDNDescription_Cloudflare,
                PartialDownloadSupport = true
            },

            new()
            {
                Name        = "GitLab",
                URLPrefix   = "https://gitlab.com/bagusnl/CollapseLauncher-ReleaseRepo/-/raw/main/",
                Description = Lang._Misc!.CDNDescription_GitLab
            },

            new()
            {
                Name        = "Coding",
                URLPrefix   = "https://ohly-generic.pkg.coding.net/collapse/release/",
                Description = Lang._Misc!.CDNDescription_Coding
            }
        ];

        #endregion

        #region Misc Fields

        public static Vector3 Shadow16 = new(0, 0, 16);
        public static Vector3 Shadow32 = new(0, 0, 32);
        public static Vector3 Shadow48 = new(0, 0, 48);


        public const           string       AppNotifURLPrefix           = "/notification_{0}.json";
        public const           string       AppGameRepairIndexURLPrefix = "/metadata/repair_indexes/{0}/{1}/index";
        public const           string       AppGameRepoIndexURLPrefix   = "/metadata/repair_indexes/{0}/repo";
        private const          string       SectionName                 = "app";
        public static readonly List<string> ScreenResolutionsList       = [];

        public const long AppDiscordApplicationID     = 1138126643592970251;
        public const long AppDiscordApplicationID_HI3 = 1124126288370737314;
        public const long AppDiscordApplicationID_GI  = 1124137436650426509;
        public const long AppDiscordApplicationID_HSR = 1124153902959431780;
        public const long AppDiscordApplicationID_ZZZ = 1124154024879456276;

        public static IntPtr AppIconLarge;
        public static IntPtr AppIconSmall;

        #endregion

        #region App Config Definitions
        public static AppIniProperty AppConfigProperty  { get; set; } = new();
        public static List<string>   AppCurrentArgument { get; set; } = [];

        [field: AllowNull, MaybeNull]
        public static Process AppCurrentProcess           { get => field ??= Process.GetCurrentProcess(); }
        public static int     AppCurrentDownloadThread    => GetAppConfigValue("DownloadThread");
        public static string  AppGameConfigMetadataFolder => Path.Combine(AppGameFolder, "_metadatav3");


        [field: AllowNull, MaybeNull]
        public static string AppExecutablePath
        {
            get
            {
                if (field != null)
                {
                    return field;
                }

                string execPath = AppCurrentProcess.MainModule?.FileName ?? "";
                return field = execPath;
            }
        }

        public static string AppGameFolder
        {
            get => GetAppConfigValue("GameFolder").Value ?? "";
            set => SetAppConfigValue("GameFolder", value);
        }

        [field: AllowNull, MaybeNull]
        public static  string  AppExecutableName => field ??= Path.GetFileName(AppExecutablePath);
        [field: AllowNull, MaybeNull]
        public static  string  AppExecutableDir => field ??= Path.GetDirectoryName(AppExecutablePath) ?? "";
        public static  string  AppGameImgFolder => Path.Combine(AppGameFolder, "_img");
        public static  string  AppGameImgCachedFolder => Path.Combine(AppGameImgFolder, "cached");
        public static  string  AppGameLogsFolder => Path.Combine(AppGameFolder, "_logs");

        [field: AllowNull, MaybeNull]
        public static Version AppCurrentVersion
        {
            get
            {
                if (field != null)
                {
                    return field;
                }

                if (string.IsNullOrEmpty(AppExecutablePath))
                {
                    return field = new Version();
                }

                FileVersionInfo verInfo = FileVersionInfo.GetVersionInfo(AppExecutablePath);
                return field = verInfo.FileVersion != null ? new Version(verInfo.FileMajorPart, verInfo.FileMinorPart, verInfo.FileBuildPart) : new Version();
            }
        }

        [field: AllowNull, MaybeNull]
        public static string AppCurrentVersionString =>
            field ??= $"{AppCurrentVersion.Major}.{AppCurrentVersion.Minor}.{AppCurrentVersion.Build}";

        [field: AllowNull, MaybeNull]
        public static Version WindowsAppSdkVersion
        {
            get
            {
                if (field != null)
                {
                    return field;
                }

                if (string.IsNullOrEmpty(AppExecutablePath))
                {
                    return field = new Version();
                }

                string dllPath = Path.Combine(AppExecutableDir, "Microsoft.ui.xaml.dll");
                if (!File.Exists(dllPath))
                {
                    return field = new Version();
                }

                FileVersionInfo verInfo = FileVersionInfo.GetVersionInfo(dllPath);
                return field = verInfo.FileVersion != null ? new Version(verInfo.FileMajorPart, verInfo.FileMinorPart, verInfo.FileBuildPart) : new Version();
            }
        }

        public static int AppCurrentThread
        {
            get
            {
                int val = GetAppConfigValue("ExtractionThread");
                return val <= 0 ? Environment.ProcessorCount : val;
            }
        }

        public static bool IsPreview                        = false;
        public static bool IsFirstInstall                   = false;
        public static bool IsAppLangNeedRestart             = false;
        public static bool IsChangeRegionWarningNeedRestart = false;
        public static bool IsAppThemeNeedRestart            = false;
        public static bool IsInstantRegionNeedRestart       = false;

        public static readonly string AppDefaultBG       = Path.Combine(AppExecutableDir, "Assets", "Images", "PageBackground", "default.png");
        public static readonly string AppLangFolder      = Path.Combine(AppExecutableDir, "Lang");
        public static readonly string AppDataFolder      = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "CollapseLauncher");
        public static readonly string AppImagesFolder    = Path.Combine(AppExecutableDir, "Assets", "Images");
        public static readonly string AppConfigFile      = Path.Combine(AppDataFolder, "config.ini");
        public static readonly string AppNotifIgnoreFile = Path.Combine(AppDataFolder, "ignore_notif_ids.json");

        public static bool IsConsoleEnabled
        {
            get => GetAppConfigValue("EnableConsole");
            set => SetAppConfigValue("EnableConsole", value);
        }

        public static bool IsMultipleInstanceEnabled
        {
            get => GetAppConfigValue("EnableMultipleInstance");
            set => SetAndSaveConfigValue("EnableMultipleInstance", value);
        }

        public static bool IsShowRegionChangeWarning
        {
            get => GetAppConfigValue("ShowRegionChangeWarning");
            set => SetAndSaveConfigValue("ShowRegionChangeWarning", value);
        }

        public static bool EnableAcrylicEffect
        {
            get => GetAppConfigValue("EnableAcrylicEffect");
            set => SetAndSaveConfigValue("EnableAcrylicEffect", value);
        }

        public static bool IsUseVideoBGDynamicColorUpdate
        {
            get => GetAppConfigValue("IsUseVideoBGDynamicColorUpdate");
            set => SetAndSaveConfigValue("IsUseVideoBGDynamicColorUpdate", value);
        }

        public static bool IsIntroEnabled
        {
            get => GetAppConfigValue("IsIntroEnabled");
            set => SetAndSaveConfigValue("IsIntroEnabled", value);
        }

        public static bool IsBurstDownloadModeEnabled
        {
            get => GetAppConfigValue("IsBurstDownloadModeEnabled");
            set => SetAndSaveConfigValue("IsBurstDownloadModeEnabled", value);
        }

        public static bool IsUsePreallocatedDownloader
        {
            get => GetAppConfigValue("IsUsePreallocatedDownloader");
            set => SetAndSaveConfigValue("IsUsePreallocatedDownloader", value);
        }

        public static bool IsUseDownloadSpeedLimiter
        {
            get => GetAppConfigValue("IsUseDownloadSpeedLimiter");
            set
            {
                SetAndSaveConfigValue("IsUseDownloadSpeedLimiter", value);

                _ = DownloadSpeedLimit;
            }
        }

        public static long DownloadSpeedLimit
        {
            get => DownloadSpeedLimitCached = GetAppConfigValue("DownloadSpeedLimit");
            set => SetAndSaveConfigValue("DownloadSpeedLimit", DownloadSpeedLimitCached = value);
        }

        public static int DownloadChunkSize
        {
            get
            {
                // Clamp value, Min: 32 MiB, Max: 512 MiB
                int configValue = GetAppConfigValue("DownloadChunkSize");
                configValue = Math.Clamp(configValue, 32 << 20, 512 << 20);
                return configValue;
            }
            set
            {
                // Clamp value, Min: 32 MiB, Max: 512 MiB
                int configValue = Math.Clamp(value, 32 << 20, 512 << 20);
                SetAndSaveConfigValue("DownloadChunkSize", configValue);
            }
        }

        public static bool IsEnforceToUse7zipOnExtract
        {
            get => GetAppConfigValue("EnforceToUse7zipOnExtract");
            set => SetAndSaveConfigValue("EnforceToUse7zipOnExtract", value);
        }

        public static long DownloadSpeedLimitCached
        {
            get;
            set
            {
                field = IsUseDownloadSpeedLimiter ? value : 0;
                DownloadSpeedLimitChanged?.Invoke(null, field);
            }
        }

        public static event EventHandler<long>? DownloadSpeedLimitChanged;

        private static bool? _cachedIsInstantRegionChange = null;

        public static bool IsInstantRegionChange
        {
            get
            {
                _cachedIsInstantRegionChange ??= GetAppConfigValue("UseInstantRegionChange");
                return (bool)_cachedIsInstantRegionChange;
            }
            set => SetAndSaveConfigValue("UseInstantRegionChange", value);
        }

        public static bool                 ForceInvokeUpdate     = false;
        public static GameInstallStateEnum GameInstallationState = GameInstallStateEnum.NotInstalled;

        public static Guid GetGuid(int sessionNum)
        {
            Guid guidString = GetAppConfigValue($"sessionGuid{sessionNum}");
            if (guidString == Guid.Empty)
            {
                var g = Guid.NewGuid();
                SetAndSaveConfigValue($"sessionGuid{sessionNum}", g);
                return g;
            }

            return guidString;
        }

        #endregion

        #region App Settings Template

        public static Dictionary<string, IniValue> AppSettingsTemplate = new()
        {
            { "CurrentBackground", "ms-appx:///Assets/Images/default.png" },
            { "DownloadThread", 4 },
            { "ExtractionThread", 0 },
            { "GameFolder", Path.Combine(AppDataFolder, "GameFolder") },
            {
                "UserAgent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Safari/537.36 Edg/118.0.0.0"
            },
        #if DEBUG
            { "EnableConsole", true },
        #else
            { "EnableConsole", false },
        #endif
            { "SendRemoteCrashData", true },
            { "EnableMultipleInstance", false },
            { "DontAskUpdate", false },
            { "ThemeMode", IniValue.Create(AppThemeMode.Default) },
            { "AppLanguage", "en-us" },
            { "UseCustomBG", false },
            { "IsUseVideoBGDynamicColorUpdate", false },
            { "ShowEventsPanel", true },
            { "ShowSocialMediaPanel", true },
            { "ShowGamePlaytime", true },
            { "CustomBGPath", "" },
            { "GameCategory", "Honkai Impact 3rd" },
            { "WindowSizeProfile", "Normal" },
            { "CurrentCDN", 0 },
            { "ShowRegionChangeWarning", false },
        #if !DISABLEDISCORD
            { "EnableDiscordRPC", false },
            { "EnableDiscordGameStatus", true },
            { "EnableDiscordIdleStatus", true },
        #endif
            { "EnableAcrylicEffect", true },
            { "IncludeGameLogs", false },
            { "UseDownloadChunksMerging", false },
            { "LowerCollapsePrioOnGameLaunch", false },
            { "EnableHTTPRepairOverride", false },
            { "ForceGIHDREnable", false },
            { "HI3IgnoreMediaPack", false },
            { "GameLaunchedBehavior", "Minimize" }, // Possible Values: "Minimize", "ToTray", and "Nothing"
            { "MinimizeToTray", false },
            { "UseExternalBrowser", false },
            { "EnableWaifu2X", false },
            { "BackgroundAudioVolume", 0.5d },
            { "BackgroundAudioIsMute", true },
            { "UseInstantRegionChange", true },
            { "IsIntroEnabled", true },
            { "IsEnableSophon", true },
            { "SophonCpuThread", 0 },
            { "SophonHttpConnInt", 0 },
            { "SophonPreloadApplyPerfMode", false },

            { "EnforceToUse7zipOnExtract", false },

            { "IsUseProxy", false },
            { "IsAllowHttpRedirections", true },
            { "IsAllowHttpCookies", false },
            { "IsAllowUntrustedCert", false },
            { "IsUseDownloadSpeedLimiter", false },
            { "IsUsePreallocatedDownloader", true },
            { "IsBurstDownloadModeEnabled", true },
            { "DownloadSpeedLimit", 0 },
            { "DownloadChunkSize", 64 << 20 },
            { "HttpProxyUrl", string.Empty },
            { "HttpProxyUsername", string.Empty },
            { "HttpProxyPassword", string.Empty },
            { "HttpClientTimeout", 90 }
        };

        #endregion
    }
}