using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Update;
using Hi3Helper;
using Hi3Helper.Http;
using Hi3Helper.Http.Legacy;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;

#if !USEVELOPACK
using Squirrel;
using Squirrel.Sources;
using System.Linq;
#else
using Velopack;
using Velopack.Locators;
using Velopack.Sources;
#endif

// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher;

public partial class Updater : IDisposable
{
    // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
    private readonly string          ChannelURL;
    private readonly string          ChannelName;
    private          Stopwatch       UpdateStopwatch;
    private readonly UpdateManager   UpdateManager;
    private readonly IFileDownloader UpdateDownloader;
#if USEVELOPACK
    private readonly ILogger       UpdateManagerLogger;
    private          VelopackAsset VelopackVersionToUpdate;
#endif
    private GameVersion     NewVersionTag;

    // ReSharper disable once RedundantDefaultMemberInitializer
    private bool IsUseLegacyDownload = false;
    // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable

    private static readonly string execPath          = Process.GetCurrentProcess().MainModule!.FileName;
    private static readonly string workingDir        = Path.GetDirectoryName(execPath);
    private static readonly string applyElevatedPath = Path.Combine(workingDir, "..\\", "ApplyUpdate.exe");

    public event EventHandler<UpdaterStatus>   UpdaterStatusChanged;
    public event EventHandler<UpdaterProgress> UpdaterProgressChanged;

    private readonly UpdaterStatus   Status;
    private          UpdaterProgress Progress;

    public Updater(string channelName)
    {
        ChannelName = channelName;
        ChannelURL = CombineURLFromString(FallbackCDNUtil.GetPreferredCDN().URLPrefix,
#if USEVELOPACK
                "velopack",
#else
                "squirrel",
#endif
                ChannelName
                );
        UpdateDownloader = new UpdateManagerHttpAdapter();
#if USEVELOPACK
        UpdateManagerLogger = ILoggerHelper.GetILogger();
        VelopackLocator updateManagerLocator = VelopackLocator.GetDefault(UpdateManagerLogger);
        UpdateOptions updateManagerOptions = new UpdateOptions
        {
            AllowVersionDowngrade = true,
            ExplicitChannel = ChannelName
        };

        // Initialize update manager source
        IUpdateSource updateSource = new SimpleWebSource(ChannelURL, UpdateDownloader);
        UpdateManager = new UpdateManager(
                updateSource,
                updateManagerOptions,
                UpdateManagerLogger,
                updateManagerLocator);
#else
        UpdateManager = new UpdateManager(ChannelURL, null, null, UpdateDownloader);
#endif
        UpdateStopwatch = Stopwatch.StartNew();
        Status = new UpdaterStatus();
        Progress = new UpdaterProgress(UpdateStopwatch, 0, 100);
    }

    ~Updater()
    {
        Dispose();
    }

    public void Dispose()
    {
#if !USEVELOPACK
        UpdateManager?.Dispose();
#endif
    }

    public async Task<UpdateInfo> StartCheck()
    {
#if !USEVELOPACK
        return await UpdateManager.CheckForUpdate();
#else
        return await UpdateManager.CheckForUpdatesAsync();
#endif
    }

    public async Task<bool> StartUpdate(UpdateInfo updateInfo, CancellationToken token = default)
    {
        if (token.IsCancellationRequested) return false;

        Status.status = string.Format(Lang._UpdatePage.UpdateStatus3, 1, 1);
        UpdateStatus();
        UpdateProgress();
        UpdateStopwatch = Stopwatch.StartNew();

        try
        {
#if !USEVELOPACK
            if (!updateInfo.ReleasesToApply.Any())
            {
                NewVersionTag = new GameVersion(updateInfo.FutureReleaseEntry.Version.Version);
                if (DoesLatestVersionExist(NewVersionTag.VersionString))
                {
                    Progress = new UpdaterProgress(UpdateStopwatch, 100, 100);
                    return true;
                }

                Status.status = string.Format(Lang._UpdatePage.UpdateStatus4,
                                              LauncherUpdateHelper.LauncherCurrentVersionString);
                Status.message = Lang._UpdatePage.UpdateMessage4;
                UpdateStatus();

                await Task.Delay(3000);
                return false;
            }
            NewVersionTag = new GameVersion(updateInfo.ReleasesToApply.FirstOrDefault()!.Version.Version);
#else
            NewVersionTag = new GameVersion(updateInfo.TargetFullRelease.Version.ToString());
            if (IsCurrentHasLatestVersion(NewVersionTag.VersionString))
            {
                Status.status = string.Format(Lang._UpdatePage.UpdateStatus4, LauncherUpdateHelper.LauncherCurrentVersionString);
                Status.message = Lang._UpdatePage.UpdateMessage4;
                UpdateStatus();

                await Task.Delay(3000);
                return false;
            }
#endif

#if !USEVELOPACK
            await UpdateManager.DownloadReleases(updateInfo.ReleasesToApply, InvokeDownloadUpdateProgress);

            await UpdateManager.ApplyReleases(updateInfo, InvokeApplyUpdateProgress);
#else
            await UpdateManager.DownloadUpdatesAsync(updateInfo, InvokeDownloadUpdateProgress, false, token);
            VelopackVersionToUpdate = updateInfo.TargetFullRelease;

            await EnsureVelopackUpdateExec(token);
#endif

            void InvokeDownloadUpdateProgress(int progress)
            {
                Progress = new UpdaterProgress(UpdateStopwatch, progress
#if !USEVELOPACK
                    / 2
#endif
                    , 100);
                UpdateProgress();
            }

#if !USEVELOPACK
            void InvokeApplyUpdateProgress(int progress)
            {
                Progress = new UpdaterProgress(UpdateStopwatch, progress / 2 + 50, 100);
                UpdateProgress();
            }
#endif
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"Failed while running update via Squirrel. Fallback to legacy method...\r\n{ex}",
                                LogType.Error, true);
            IsUseLegacyDownload = true;
            await StartLegacyUpdate();
        }

        return true;
    }

    private async Task EnsureVelopackUpdateExec(CancellationToken token)
    {
        string updateExecPath = Path.Combine(Path.GetDirectoryName(AppExecutablePath) ?? "", "..\\", "update.exe");
        FileInfo updateExecFileInfo = new FileInfo(updateExecPath);

        if (!IsFileVersionValid(updateExecFileInfo))
        {
            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder()
                                     .UseLauncherConfig()
                                     .SetAllowedDecompression(DecompressionMethods.None)
                                     .Create();
            DownloadClient downloadClient = DownloadClient.CreateInstance(client);

            await using FileStream updateExecStream = updateExecFileInfo.Create();
            await FallbackCDNUtil.DownloadCDNFallbackContent(downloadClient, updateExecStream, "Update.exe", token);
        }

        return;

        bool IsFileVersionValid(FileInfo fileInfo)
        {
            const string VelopackDesc = "Velopack";

            if (!fileInfo.Exists || fileInfo.Length == 0)
                return false;

            try
            {
                FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(fileInfo.FullName);
                bool isVelopack = fileVersionInfo.FileDescription?.StartsWith(VelopackDesc, StringComparison.OrdinalIgnoreCase) ?? false;

                return isVelopack;
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(new InvalidDataException($"Failed while reading FileVersionInfo of {fileInfo.FullName}", ex));
                Logger.LogWriteLine($"Failed while reading FileVersionInfo of {fileInfo.FullName}");
            }

            return false;
        }
    }

    private async Task StartLegacyUpdate()
    {
        // Initialize new proxy-aware HttpClient
        using HttpClient client = new HttpClientBuilder()
            .UseLauncherConfig()
            .SetAllowedDecompression(DecompressionMethods.None)
            .Create();

        DownloadClient downloadClient = DownloadClient.CreateInstance(client);

        UpdateStopwatch = Stopwatch.StartNew();

        CDNURLProperty preferredCdn = FallbackCDNUtil.GetPreferredCDN();
        string updateFileIndexUrl = CombineURLFromString(preferredCdn.URLPrefix, ChannelName.ToLower(), "fileindex.json");

        AppUpdateVersionProp updateInfo = await FallbackCDNUtil.DownloadAsJSONType(updateFileIndexUrl,
                                            AppUpdateVersionPropJsonContext.Default.AppUpdateVersionProp, default)!;

        GameVersion? gameVersion = updateInfo!.Version;

        if (gameVersion.HasValue) NewVersionTag = gameVersion.Value!;
        UpdateStatus();
        UpdateProgress();

        FallbackCDNUtil.DownloadProgress += FallbackCDNUtil_DownloadProgress;
        await FallbackCDNUtil.DownloadCDNFallbackContent(downloadClient, applyElevatedPath,
                                                         Environment.ProcessorCount > 8
                                                             ? 8
                                                             : Environment.ProcessorCount,
                                                         $"{ChannelName.ToLower()}/ApplyUpdate.exe", default);
        FallbackCDNUtil.DownloadProgress -= FallbackCDNUtil_DownloadProgress;

        await File.WriteAllTextAsync(Path.Combine(workingDir, "..\\", "release"), ChannelName.ToLower());
    }

    private void FallbackCDNUtil_DownloadProgress(object sender, DownloadEvent e)
    {
        Progress = new UpdaterProgress(UpdateStopwatch, (int)e.ProgressPercentage, 100);
        UpdateProgress();
    }

    private bool IsCurrentHasLatestVersion(string latestVersionString)
    {
        // Check legacy version first
        var filePath = Path.Combine(AppExecutableDir, $@"..\app-{latestVersionString}\{Path.GetFileName(AppExecutablePath)}");
        if (File.Exists(filePath)) return true;

        // If none does not exist, then check the latest version
        filePath = Path.Combine(AppExecutableDir, $@"..\current\{Path.GetFileName(AppExecutablePath)}");
        if (!Version.TryParse(latestVersionString, out Version latestVersion))
        {
            Logger.LogWriteLine($"[Updater::DoesLatestVersionExist] latestVersionString is not valid! {latestVersionString}", LogType.Error, true);
            return false;
        }

        // If the Velopack current folder doesn't exist, then return false
        if (!File.Exists(filePath))
        {
            return false;
        }

        // Try to get the version info and if the version info is null, return false
        FileVersionInfo toCheckVersionInfo = FileVersionInfo.GetVersionInfo(filePath);

        // Otherwise, try compare the version info.
        if (!Version.TryParse(toCheckVersionInfo.FileVersion, out Version currentVersion))
        {
            Logger.LogWriteLine($"[Updater::DoesLatestVersionExist] toCheckVersionInfo.FileVersion is not valid! {toCheckVersionInfo.FileVersion}", LogType.Error, true);
            return false;
        }

        // Try compare. If currentVersion is more or equal to latestVersion, return true. 
        // Otherwise, return false.
        return currentVersion >= latestVersion;
    }

    public async Task FinishUpdate(bool noSuicide = false)
    {
        var newVerTagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData",
                                         "LocalLow", "CollapseLauncher", "_NewVer");
        var needInnoLogUpdatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                                 "AppData", "LocalLow", "CollapseLauncher", "_NeedInnoLogUpdate");

        Status.status = string.Format(Lang._UpdatePage.UpdateStatus5 + $" {Lang._UpdatePage.UpdateMessage5}",
                                      NewVersionTag.VersionString);
        UpdateStatus();

        Progress = new UpdaterProgress(UpdateStopwatch, 100, 100);
        UpdateProgress();

        await File.WriteAllTextAsync(newVerTagPath,         NewVersionTag.VersionString);
        await File.WriteAllTextAsync(needInnoLogUpdatePath, NewVersionTag.VersionString);

        if (IsUseLegacyDownload)
        {
            SuicideLegacy();
            return;
        }

        if (!noSuicide)
            await Suicide();
    }

    private async Task Suicide()
    {
        await Task.Delay(3000);
#if !USEVELOPACK
        UpdateManager.RestartApp();
#else
        if (VelopackVersionToUpdate != null)
        {
            try
            {
                string currentAppPath = Path.Combine(Path.GetDirectoryName(AppExecutableDir) ?? string.Empty, "current");
                if (!Directory.Exists(currentAppPath))
                    Directory.CreateDirectory(currentAppPath);

                UpdateManager.ApplyUpdatesAndRestart(VelopackVersionToUpdate);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"Failed to suicide\r\n{ex}", LogType.Error);
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                Environment.Exit(100);
            }
        }
#endif
    }

    private static void SuicideLegacy()
    {
        var applyUpdate = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName        = applyElevatedPath,
                UseShellExecute = true
            }
        };
        applyUpdate.Start();
        (WindowUtility.CurrentWindow as MainWindow)?.CloseApp();
    }

    public void UpdateStatus()
    {
        UpdaterStatusChanged?.Invoke(this, Status);
    }

    public void UpdateProgress()
    {
        UpdaterProgressChanged?.Invoke(this, Progress);
    }

    public class UpdaterStatus
    {
        public string status { get; set; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public string message { get; set; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public string newver { get; set; }
    }

    public class UpdaterProgress
    {
        public UpdaterProgress(Stopwatch currentStopwatch, int counter, int counterGoal)
        {
            if (counter == 0) TimeLeft = TimeSpan.Zero;

            var elapsedMin = (float)currentStopwatch.ElapsedMilliseconds / 1000 / 60;
            var minLeft    = elapsedMin / counter * (counterGoal - counter);
            TimeLeft       = double.IsNaN(minLeft) || double.IsInfinity(minLeft) ?
                TimeSpan.Zero :
                TimeSpan.FromMinutes(minLeft);

            ProgressPercentage = counter;
        }

        // ReSharper disable UnusedMember.Global
        // ReSharper disable UnusedAutoPropertyAccessor.Local
        public long DownloadedSize { get; private set; }

        public long TotalSizeToDownload { get; private set; }

        public double ProgressPercentage { get; private set; }

        public long CurrentRead { get; private set; }

        public long CurrentSpeed => 0;

        public TimeSpan TimeLeft { get; private set; }
        // ReSharper restore UnusedMember.Global
        // ReSharper restore UnusedAutoPropertyAccessor.Local
    }
}