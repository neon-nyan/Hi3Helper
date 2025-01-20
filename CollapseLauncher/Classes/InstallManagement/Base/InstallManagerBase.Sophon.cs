// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable InconsistentNaming
// ReSharper disable GrammarMistakeInComment

using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Structs;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using SophonLogger = Hi3Helper.Sophon.Helper.Logger;
// ReSharper disable LoopCanBeConvertedToQuery

#nullable enable
namespace CollapseLauncher.InstallManager.Base
{
    internal delegate ValueTask<TResult> SignedValueTaskSelectorAsync<in TFrom, TResult>(
        TFrom item, CancellationToken ctx)
        where TResult : struct, ISignedNumber<TResult>;

    internal partial class InstallManagerBase
    {
        #region Protected Virtual Properties
        protected virtual string _gameSophonChunkDir => Path.Combine(GamePath, "chunk_collapse");
        #endregion

        #region Protected Properties
        protected List<string> _sophonVOLanguageList      { get; set; } = [];
        protected bool         _isSophonDownloadCompleted { get; set; }
        protected bool         _isSophonPreloadCompleted
        {
            get => File.Exists(Path.Combine(_gameSophonChunkDir, PreloadVerifiedFileName));
            set
            {
                string verifiedFile =
                    EnsureCreationOfDirectory(Path.Combine(_gameSophonChunkDir, PreloadVerifiedFileName));
                try
                {
                    FileInfo fileInfo = new FileInfo(verifiedFile)
                        .EnsureCreationOfDirectory()
                        .EnsureNoReadOnly(out bool isExist);

                    if (value)
                    {
                        fileInfo.Create().Dispose();
                        return;
                    }

                    if (isExist)
                    {
                        fileInfo.Delete();
                    }
                }
                catch (Exception ex)
                {
                    SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                    Logger.LogWriteLine($"Error while deleting/creating sophon preload completion file! {ex}",
                                        LogType.Warning, true);
                }
            }
        }
        #endregion

        #region Public Virtual Properties
        public virtual bool IsUseSophon =>
            GameVersionManager.GamePreset.LauncherResourceChunksURL != null
            && !File.Exists(Path.Combine(GamePath, "@DisableSophon"))
            && !_canDeltaPatch && !_forceIgnoreDeltaPatch
            && LauncherConfig.GetAppConfigValue("IsEnableSophon").ToBool();
        #endregion

        #region Sophon Verification Methods
        protected virtual void CleanupTempSophonVerifiedFiles()
        {
            DirectoryInfo dirPath = new(_gameSophonChunkDir);
            try
            {
                if (!dirPath.Exists)
                {
                    return;
                }

                foreach (FileInfo file in dirPath.EnumerateFiles("*.verified", SearchOption.TopDirectoryOnly)
                                                 .EnumerateNoReadOnly())
                {
                    file.Delete();
                }
            }
            catch
            {
                // ignored
            }
        }
        #endregion

        #region Sophon Download and Install/Update/Preload Methods
        public virtual async Task StartPackageInstallSophon(GameInstallStateEnum gameState, bool fallbackFromUpdate = false)
        {
            // Set the flag to false
            _isSophonDownloadCompleted = false;

            // Set the max thread and httpHandler based on settings
            int maxThread       = SophonGetThreadNum();
            int maxChunksThread = Math.Clamp(maxThread / 2, 2, 32);
            int maxHttpHandler  = Math.Max(maxThread, SophonGetHttpHandler());

            Logger.LogWriteLine($"Initializing Sophon Chunk download method with Main Thread: {maxThread}, Chunks Thread: {maxChunksThread} and Max HTTP handle: {maxHttpHandler}",
                                LogType.Default, true);

            // Initialize the HTTP client
            HttpClient httpClient = new HttpClientBuilder()
                                   .UseLauncherConfig(maxHttpHandler)
                                   .Create();

            using (ThreadPoolThrottle.Start())
            {
                // Create a sophon download speed limiter instance
                SophonDownloadSpeedLimiter downloadSpeedLimiter =
                    SophonDownloadSpeedLimiter.CreateInstance(LauncherConfig.DownloadSpeedLimitCached);

                // Reset status and progress properties
                ResetStatusAndProgress();

                // Set the progress bar to indetermined
                IsSophonInUpdateMode                 = false;
                Status.IsIncludePerFileIndicator     = false;
                Status.IsProgressPerFileIndetermined = true;
                Status.IsProgressAllIndetermined     = true;
                UpdateStatus();

                // Clear the VO language list
                _sophonVOLanguageList.Clear();

                // Subscribe the logger event if fallback is not used
                if (!fallbackFromUpdate)
                {
                    SophonLogger.LogHandler += UpdateSophonLogHandler;
                }

                // Get the requested URL and version based on current state.
                if (GameVersionManager.GamePreset
                                       .LauncherResourceChunksURL != null)
                {
                    // Reassociate the URL if branch url exist
                    string? branchUrl = GameVersionManager.GamePreset
                                                           .LauncherResourceChunksURL
                                                           .BranchUrl;
                    if (!string.IsNullOrEmpty(branchUrl)
                        && !string.IsNullOrEmpty(GameVersionManager.GamePreset.LauncherBizName))
                    {
                        await GameVersionManager.GamePreset
                                                 .LauncherResourceChunksURL
                                                 .EnsureReassociated(
                                                                     httpClient,
                                                                     branchUrl,
                                                                     GameVersionManager.GamePreset.LauncherBizName,
                                                                     Token.Token);
                    }

                #if SIMULATEAPPLYPRELOAD
                    string requestedUrl = gameState switch
                    {
                        GameInstallStateEnum.InstalledHavePreload => _gameVersionManager.GamePreset
                           .LauncherResourceChunksURL.PreloadUrl,
                        _ => _gameVersionManager.GamePreset.LauncherResourceChunksURL.MainUrl
                    };
                    GameVersion? requestedVersion = gameState switch
                    {
                        GameInstallStateEnum.InstalledHavePreload => _gameVersionManager!
                           .GetGameVersionAPIPreload(),
                        _ => _gameVersionManager!.GetGameVersionAPIPreload()
                    } ?? _gameVersionManager!.GetGameVersionAPI();
                #else
                    string? requestedUrl = gameState switch
                                           {
                                               GameInstallStateEnum.InstalledHavePreload => GameVersionManager
                                                  .GamePreset
                                                  .LauncherResourceChunksURL.PreloadUrl,
                                               _ => GameVersionManager.GamePreset.LauncherResourceChunksURL.MainUrl
                                           };
                    GameVersion? requestedVersion = gameState switch
                                                    {
                                                        GameInstallStateEnum.InstalledHavePreload =>
                                                            GameVersionManager!
                                                               .GetGameVersionApiPreload(),
                                                        _ => GameVersionManager!.GetGameVersionApi()
                                                    } ?? GameVersionManager!.GetGameVersionApi();

                    // Add the tag query to the Url
                    requestedUrl += $"&tag={requestedVersion.ToString()}";
#endif

                    try
                    {
                        // Initialize the info pair list
                        List<SophonChunkManifestInfoPair> sophonInfoPairList = [];

                        // Get the info pair based on info provided above (for main game file)
                        var sophonMainInfoPair = await
                            SophonManifest.CreateSophonChunkManifestInfoPair(
                                                                             httpClient,
                                                                             requestedUrl,
                                                                             "game",
                                                                             Token.Token);

                        // Ensure that the manifest is ordered based on _gameVoiceLanguageLocaleIdOrdered
                        RearrangeSophonDataLocaleOrder(sophonMainInfoPair.OtherSophonData);

                        // Add the manifest to the pair list
                        sophonInfoPairList.Add(sophonMainInfoPair);

                        List<string> voLanguageList =
                            GetSophonLanguageDisplayDictFromVoicePackList(sophonMainInfoPair.OtherSophonData);

                        // Get Audio Choices first.
                        // If the fallbackFromUpdate flag is set, then don't show the dialog and instead
                        // use the default language (ja-jp) as the fallback and read the existing audio_lang file
                        List<int> addedVo;
                        int setAsDefaultVo = GetSophonLocaleCodeIndex(
                                              sophonMainInfoPair.OtherSophonData,
                                              "ja-jp"
                                             );

                        if (fallbackFromUpdate)
                        {
                            addedVo = [];
                            if (!File.Exists(_gameAudioLangListPathStatic))
                            {
                                addedVo.Add(setAsDefaultVo);
                            }
                            else
                            {
                                string[] voLangList = await File.ReadAllLinesAsync(_gameAudioLangListPathStatic);
                                foreach (string voLang in voLangList)
                                {
                                    string? voLocaleId = GetLanguageLocaleCodeByLanguageString(
                                        voLang
#if !DEBUG
                                        , false
#endif
                                        );

                                    if (string.IsNullOrEmpty(voLocaleId))
                                    {
                                        continue;
                                    }

                                    int voLocaleIndex = GetSophonLocaleCodeIndex(
                                         sophonMainInfoPair.OtherSophonData,
                                         voLocaleId
                                        );
                                    addedVo.Add(voLocaleIndex);
                                }

                                if (addedVo.Count == 0)
                                {
                                    addedVo.Add(setAsDefaultVo);
                                }
                            }
                        }
                        else
                        {
                            (addedVo, setAsDefaultVo) =
                                await SimpleDialogs.Dialog_ChooseAudioLanguageChoice(
                                 voLanguageList,
                                 setAsDefaultVo);
                        }

                        if (addedVo == null || setAsDefaultVo < 0)
                        {
                            throw new TaskCanceledException();
                        }

                        for (int i = 0; i < addedVo.Count; i++)
                        {
                            int    voLangIndex      = addedVo[i];
                            string voLangLocaleCode = GetLanguageLocaleCodeByID(voLangIndex);
                            _sophonVOLanguageList?.Add(voLangLocaleCode);

                            // Get the info pair based on info provided above (for the selected VO audio file)
                            SophonChunkManifestInfoPair sophonSelectedVoLang =
                                sophonMainInfoPair.GetOtherManifestInfoPair(voLangLocaleCode);
                            sophonInfoPairList.Add(sophonSelectedVoLang);
                        }

                        // Set the voice language ID to value given
                        GameVersionManager.GamePreset.SetVoiceLanguageID(setAsDefaultVo);

                        // Get the remote total size and current total size
                        ProgressAllCountTotal    = sophonInfoPairList.Sum(x => x.ChunksInfo.FilesCount);
                        ProgressAllSizeTotal     = sophonInfoPairList.Sum(x => x.ChunksInfo.TotalSize);
                        ProgressAllSizeCurrent   = 0;

                        // If the fallback is used from update, use the same display as All Size for Per File progress.
                        if (fallbackFromUpdate)
                        {
                            ProgressPerFileSizeTotal = ProgressAllSizeTotal;
                        }

                        // Set the display to Install Mode
                        UpdateStatus();

                        // Get game install path and create directory if not exist
                        string gameInstallPath = GamePath;
                        if (!string.IsNullOrEmpty(gameInstallPath))
                        {
                            Directory.CreateDirectory(gameInstallPath);
                        }

                        // Get the list of the Sophon Assets first
                        List<SophonAsset> sophonAssetList = await GetSophonAssetListFromPair(
                            httpClient,
                            sophonInfoPairList,
                            downloadSpeedLimiter,
                            Token.Token);

                        // Check for the disk space requirement first and ensure that the space is sufficient
                        await EnsureDiskSpaceSufficiencyAsync(
                            ProgressAllSizeTotal,
                            gameInstallPath,
                            sophonAssetList,
                            async (sophonAsset, ctx) =>
                            {
                                return await Task<long>.Factory.StartNew(() =>
                                {
                                    // Get the file path and start the write process
                                    string   assetName      = sophonAsset.AssetName;
                                    string   assetFullPath  = Path.Combine(gameInstallPath, assetName);
                                    long     sophonAssetLen = sophonAsset.AssetSize;
                                    FileInfo filePath       = new FileInfo(assetFullPath + "_tempSophon");
                                    FileInfo origFilePath   = new FileInfo(assetFullPath);

                                    // If the original file path exist and the length is the same as the asset size
                                    // or if the temp file path exist and the length is the same as the asset size
                                    // (means the file has already been downloaded, then return sophonAssetLen)
                                    if ((origFilePath.Exists && origFilePath.Length == sophonAssetLen)
                                     || (filePath.Exists     && filePath.Length     == sophonAssetLen))
                                    {
                                        return sophonAssetLen;
                                    }
                            
                                    // If both orig and temp file don't exist or has different size, then return 0 as it doesn't exist
                                    return 0L;
                                }, ctx,
                                TaskCreationOptions.DenyChildAttach,
                                TaskScheduler.Default);
                            }, Token.Token);

                        // Get the parallel options
                        var parallelOptions = new ParallelOptions
                        {
                            MaxDegreeOfParallelism = maxThread,
                            CancellationToken      = Token.Token
                        };
                        var parallelChunksOptions = new ParallelOptions
                        {
                            MaxDegreeOfParallelism = maxChunksThread,
                            CancellationToken      = Token.Token
                        };

                        // Set the progress bar to indetermined
                        Status.IsIncludePerFileIndicator     = false;
                        Status.IsProgressPerFileIndetermined = false;
                        Status.IsProgressAllIndetermined     = false;
                        UpdateStatus();

                        // Enumerate the asset in parallel and start the download process
                        await Parallel.ForEachAsync(sophonAssetList, parallelOptions, DelegateAssetDownload)
                                      .ConfigureAwait(false);

                        // Rename temporary files
                        await Parallel.ForEachAsync(sophonAssetList, parallelOptions, DelegateAssetRenameTempFile)
                                      .ConfigureAwait(false);

                        // Remove sophon verified files
                        CleanupTempSophonVerifiedFiles();

                        // Declare the download delegate
                        ValueTask DelegateAssetDownload(SophonAsset asset, CancellationToken _)
                        // ReSharper disable once AccessToDisposedClosure
                        {
                            return RunSophonAssetDownloadThread(httpClient, asset, parallelChunksOptions);
                        }

                        // Declare the rename temp file delegate
                        async ValueTask DelegateAssetRenameTempFile(SophonAsset asset, CancellationToken token)
                        {
                            await Task.Run(() =>
                            {
                                // If the asset is a dictionary, then return
                                if (asset.IsDirectory)
                                {
                                    return;
                                }

                                // Throw if the token cancellation requested
                                token.ThrowIfCancellationRequested();

                                // Get the file path and start the write process
                                var assetName     = asset.AssetName;
                                var assetFullPath = Path.Combine(gameInstallPath, assetName);
                                var tempFilePath  = new FileInfo(assetFullPath + "_tempSophon")
                                                   .EnsureCreationOfDirectory()
                                                   .EnsureNoReadOnly(out bool isExistTemp);

                                // If the temp file is not exist, then return (ignore)
                                if (!isExistTemp)
                                {
                                    return;
                                }

                                // Get the original file path, ensure the existing file is not read only,
                                // then move the temp file to the original file path
                                var origFilePath  = new FileInfo(assetFullPath)
                                                   .EnsureCreationOfDirectory()
                                                   .EnsureNoReadOnly();

                                // Move the thing
                                tempFilePath.MoveTo(origFilePath.FullName, true);
                                origFilePath.Refresh();
                            }, token);
                        }

                        _isSophonDownloadCompleted = true;
                    }
                    finally
                    {
                        // Unsubscribe the logger event if fallback is not used
                        if (!fallbackFromUpdate)
                        {
                            SophonLogger.LogHandler -= UpdateSophonLogHandler;
                        }
                        httpClient.Dispose();

                        // Unsubscribe download limiter
                        LauncherConfig.DownloadSpeedLimitChanged -= downloadSpeedLimiter.GetListener();
                    }
                }
            }
        }

        private static async Task<List<SophonAsset>> GetSophonAssetListFromPair(
            HttpClient                               client,
            IEnumerable<SophonChunkManifestInfoPair> sophonInfoPairs,
            SophonDownloadSpeedLimiter               downloadSpeedLimiter,
            CancellationToken                        token)
        {
            List<SophonAsset> sophonAssetList = [];

            // Avoid duplicates by using HashSet of the url
            HashSet<string> currentlyProcessedPair = [];
            foreach (SophonChunkManifestInfoPair sophonDownloadInfoPair in sophonInfoPairs)
            {
                // Try add and if the hashset already contains the same Manifest ID registered, then skip
                if (!currentlyProcessedPair.Add(sophonDownloadInfoPair.ManifestInfo.ManifestId))
                {
                    Logger.LogWriteLine($"Found duplicate operation for {sophonDownloadInfoPair.ManifestInfo.ManifestId}! Skipping...",
                                        LogType.Warning, true);
                    continue;
                }

                // Register to hashset to avoid duplication
                // Enumerate the pair to get the SophonAsset
                await foreach (SophonAsset sophonAsset in SophonManifest.EnumerateAsync(
                                client,
                                sophonDownloadInfoPair,
                                downloadSpeedLimiter,
                                token))
                {
                    // If the asset is a directory, skip
                    if (sophonAsset.IsDirectory)
                    {
                        continue;
                    }

                    sophonAssetList.Add(sophonAsset);
                }
            }

            // Return the list
            return sophonAssetList;
        }

        public virtual async Task StartPackageUpdateSophon(GameInstallStateEnum gameState, bool isPreloadMode)
        {
            if (!Enum.IsDefined(gameState))
            {
                throw new InvalidEnumArgumentException(nameof(gameState), (int)gameState, typeof(GameInstallStateEnum));
            }

            // Set the flag to false
            _isSophonDownloadCompleted = false;

            // Set the max thread and httpHandler based on settings
            int maxThread       = SophonGetThreadNum();
            int maxChunksThread = Math.Clamp(maxThread / 2, 2, 32);
            int maxHttpHandler  = Math.Max(maxThread, SophonGetHttpHandler());

            Logger.LogWriteLine($"Initializing Sophon Chunk update method with Main Thread: {maxThread}, Chunks Thread: {maxChunksThread} and Max HTTP handle: {maxHttpHandler}",
                                LogType.Default, true);

            // Initialize the HTTP client
            HttpClient httpClient = new HttpClientBuilder()
                                   .UseLauncherConfig(maxHttpHandler)
                                   .Create();

            try
            {
                // Reset status and progress properties
                ResetStatusAndProgress();

                // Set the progress bar to indetermined
                IsSophonInUpdateMode                 = !isPreloadMode;
                Status.IsIncludePerFileIndicator     = !isPreloadMode;
                Status.IsProgressPerFileIndetermined = true;
                Status.IsProgressAllIndetermined     = true;
                UpdateStatus();

                // Clear the VO language list
                _sophonVOLanguageList.Clear();

                // Subscribe the logger event
                SophonLogger.LogHandler += UpdateSophonLogHandler;

                // Init asset list
                List<SophonAsset> sophonUpdateAssetList = [];

                // Get the previous version details of the preload or the recent update.
                GameVersion? requestedVersionFrom = GameVersionManager!.GetGameExistingVersion();
                if (GameVersionManager.GamePreset.LauncherResourceChunksURL != null)
                {
                    // Reassociate the URL if branch url exist
                    string? branchUrl = GameVersionManager.GamePreset
                                                           .LauncherResourceChunksURL
                                                           .BranchUrl;
                    if (!string.IsNullOrEmpty(branchUrl)
                        && !string.IsNullOrEmpty(GameVersionManager.GamePreset.LauncherBizName))
                    {
                        await GameVersionManager.GamePreset
                                                 .LauncherResourceChunksURL
                                                 .EnsureReassociated(
                                                                     httpClient,
                                                                     branchUrl,
                                                                     GameVersionManager.GamePreset.LauncherBizName,
                                                                     Token.Token);
                    }

                    string? requestedBaseUrlFrom = isPreloadMode
                        ? GameVersionManager.GamePreset.LauncherResourceChunksURL.PreloadUrl
                        : GameVersionManager.GamePreset.LauncherResourceChunksURL.MainUrl;
                #if SIMULATEAPPLYPRELOAD
                    string requestedBaseUrlTo = _gameVersionManager.GamePreset.LauncherResourceChunksURL.PreloadUrl!;
                #else
                    string requestedBaseUrlTo = requestedBaseUrlFrom!;
                #endif
                    // Add the tag query to the previous version's Url
                    requestedBaseUrlFrom += $"&tag={requestedVersionFrom.ToString()}";

                    // Create a sophon download speed limiter instance
                    SophonDownloadSpeedLimiter downloadSpeedLimiter =
                        SophonDownloadSpeedLimiter.CreateInstance(LauncherConfig.DownloadSpeedLimitCached);

                    // Add base game diff data
                    bool isSuccess = await AddSophonDiffAssetsToList(
                        httpClient,
                        requestedBaseUrlFrom,
                        requestedBaseUrlTo,
                        sophonUpdateAssetList,
                        "game",
                        downloadSpeedLimiter);

                    // If it doesn't success to get the base diff, then fallback to actually download the whole game
                    if (!isSuccess)
                    {
                        Logger.LogWriteLine($"The current game version: {requestedVersionFrom.ToString()} is too obsolete and incremental update is unavailable. Falling back to full update", LogType.Warning, true);

                        // Spawn the confirmation dialog
                        ContentDialogResult fallbackResultConfirm = await SimpleDialogs.SpawnDialog(
                            Locale.Lang._Dialogs.SophonIncrementUpdateUnavailTitle,
                            new TextBlock
                            {
                                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                            }
                            .AddTextBlockLine(string.Format(Locale.Lang._Dialogs.SophonIncrementUpdateUnavailSubtitle1, requestedVersionFrom.ToString()))
                            .AddTextBlockLine(Locale.Lang._Dialogs.SophonIncrementUpdateUnavailSubtitle2, Microsoft.UI.Text.FontWeights.Bold)
                            .AddTextBlockLine(Locale.Lang._Dialogs.SophonIncrementUpdateUnavailSubtitle3)
                            .AddTextBlockNewLine(2)
                            .AddTextBlockLine(
                                string.Format(Locale.Lang._Dialogs.SophonIncrementUpdateUnavailSubtitle4,
                                              Locale.Lang._Misc.YesContinue,
                                              Locale.Lang._Misc.NoCancel),
                                Microsoft.UI.Text.FontWeights.Bold,
                                10),
                            ParentUI,
                            Locale.Lang._Misc.NoCancel,
                            Locale.Lang._Misc.YesContinue,
                            defaultButton: ContentDialogButton.Primary,
                            dialogTheme: CustomControls.ContentDialogTheme.Warning);

                        // If cancelled, then throw
                        if (ContentDialogResult.Primary != fallbackResultConfirm)
                        {
                            throw new TaskCanceledException("The full update routine has been cancelled by the user");
                        }

                        // Otherwise, continue updating the entire game
                        gameState = GameInstallStateEnum.NotInstalled;
                        await StartPackageInstallSophon(gameState, true);
                        return;
                    }

                    // If the game has lang list path, then add it
                    if (_gameAudioLangListPath != null)
                    {
                        // Add existing voice-over diff data
                        await AddSophonAdditionalVODiffAssetsToList(httpClient,         requestedBaseUrlFrom,
                                                                    requestedBaseUrlTo, sophonUpdateAssetList,
                                                                    downloadSpeedLimiter);
                    }
                }

                // Get the remote chunk size
                ProgressPerFileSizeTotal   = sophonUpdateAssetList.GetCalculatedDiffSize(!isPreloadMode);
                ProgressPerFileSizeCurrent = 0;

                // Get the remote total size and current total size
                ProgressAllCountTotal = sophonUpdateAssetList.Count(x => !x.IsDirectory);
                ProgressAllSizeTotal = !isPreloadMode
                    ? sophonUpdateAssetList.Sum(x => x.AssetSize)
                    : ProgressPerFileSizeTotal;
                ProgressAllSizeCurrent = 0;

                // Get the parallel options
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxThread,
                    CancellationToken      = Token.Token
                };
                var parallelChunksOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxChunksThread,
                    CancellationToken      = Token.Token
                };

                // Get the update source and destination, also where the staging chunk files will be stored
                string chunkPath = _gameSophonChunkDir;
                string gamePath  = GamePath;

                // If the chunk directory is not exist, then create one.
                if (!string.IsNullOrEmpty(chunkPath))
                {
                    Directory.CreateDirectory(chunkPath);
                }

                bool canDeleteChunks          = _canDeleteZip;
                bool isSophonPreloadCompleted = _isSophonPreloadCompleted;

                // If preload completed and perf mode is on, use all CPU cores 
                if (isSophonPreloadCompleted && LauncherConfig.GetAppConfigValue("SophonPreloadApplyPerfMode").ToBool())
                {
                    maxThread       = Environment.ProcessorCount;
                    maxChunksThread = Math.Clamp(maxThread / 2, 2, 32);

                    parallelOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = maxThread,
                        CancellationToken      = Token.Token
                    };
                    parallelChunksOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = maxChunksThread,
                        CancellationToken      = Token.Token
                    };
                }

                // Test the disk space requirement first and ensure that the space is sufficient
                await EnsureDiskSpaceSufficiencyAsync(
                                                      ProgressPerFileSizeTotal,
                                                      chunkPath,
                                                      sophonUpdateAssetList,
                                                      async (x, ctx) =>
                                                          await x.GetDownloadedPreloadSize(
                                                              chunkPath,
                                                              gamePath,
                                                              isPreloadMode,
                                                              ctx),
                                                      Token.Token);

                Status.IsProgressPerFileIndetermined = false;
                Status.IsProgressAllIndetermined     = false;
                Status.ActivityStatus = $"{(IsSophonInUpdateMode && !isPreloadMode
                    ? Locale.Lang._Misc.UpdatingAndApplying
                    : Locale.Lang._Misc.Downloading)}: {string.Format(Locale.Lang._Misc.PerFromTo, ProgressAllCountCurrent,
                                                                      ProgressAllCountTotal)}";
                UpdateStatus();

                ConcurrentDictionary<SophonAsset, byte> processingAsset = new();

                // Set the delegate function for the download action
                async ValueTask Action(HttpClient localHttpClient, SophonAsset asset)
                {
                    if (!processingAsset.TryAdd(asset, 0))
                    {
                        Logger.LogWriteLine($"Found duplicate operation for {asset.AssetName}! Skipping...",
                                            LogType.Warning, true);
                        return;
                    }

                    if (isPreloadMode)
                    {
                        // If preload mode, then only download the chunks
                        await asset.DownloadDiffChunksAsync(localHttpClient, chunkPath, parallelChunksOptions,
                                                            UpdateSophonFileTotalProgress, null,
                                                            UpdateSophonDownloadStatus, isSophonPreloadCompleted);
                        return;
                    }

                    // Ensure to remove the read-only attribute
                    string currentAssetPath = Path.Combine(gamePath, asset.AssetName);
                    TryUnassignReadOnlyFileSingle(currentAssetPath);

                    // Otherwise, start the patching process
                    await asset.WriteUpdateAsync(localHttpClient, gamePath, gamePath, chunkPath, canDeleteChunks,
                                                 parallelChunksOptions, UpdateSophonFileTotalProgress,
                                                 UpdateSophonFileDownloadProgress, UpdateSophonDownloadStatus);
                    processingAsset.Remove(asset, out _);
                }

                // Enumerate in parallel and process the assets
                await Parallel.ForEachAsync(sophonUpdateAssetList.Where(x => !x.IsDirectory),
                                            parallelOptions,
                                            (asset, _) => Action(httpClient, asset));

                _isSophonPreloadCompleted = isPreloadMode;

                // If it's in update mode, then clean up the temp sophon verified files
                if (!isPreloadMode)
                {
                    CleanupTempSophonVerifiedFiles();
                }

                _isSophonDownloadCompleted = true;
            }
            finally
            {
                // Unsubscribe the logger event
                SophonLogger.LogHandler -= UpdateSophonLogHandler;
                httpClient.Dispose();

                // Check if the DXSETUP file is exist, then delete it.
                // The DXSETUP files causes some false positive detection of data modification
                // for some games (like Genshin, which causes 4302-x errors for some reason)
                string dxSetupDir = Path.Combine(GamePath, "DXSETUP");
                TryDeleteReadOnlyDir(dxSetupDir);
            }
        }

        private ValueTask RunSophonAssetDownloadThread(HttpClient      client, SophonAsset asset,
                                                       ParallelOptions parallelOptions)
        {
            // If the asset is a dictionary, then return
            if (asset.IsDirectory)
            {
                return ValueTask.CompletedTask;
            }

            // Get the file path and start the write process
            var assetName = asset.AssetName;
            var filePath  = EnsureCreationOfDirectory(Path.Combine(GamePath, assetName));

            // Get the target and temp file info
            FileInfo existingFileInfo = new FileInfo(filePath).EnsureNoReadOnly(out bool isExistingFileInfoExist);
            FileInfo sophonFileInfo =
                new FileInfo(filePath + "_tempSophon").EnsureNoReadOnly(out bool isSophonFileInfoExist);

            // Use "_tempSophon" if file is new or if "_tempSophon" file exist. Otherwise use original file if exist
            if (!isExistingFileInfoExist || isSophonFileInfoExist
                                         || (isExistingFileInfoExist && isSophonFileInfoExist))
            {
                filePath = sophonFileInfo.FullName;
            }

            // However if the file has already been existed and completely downloaded while _tempSophon is exist,
            // delete the _tempSophon one to avoid uncompleted files being applied instead.
            else if (isExistingFileInfoExist && existingFileInfo.Length == asset.AssetSize && isSophonFileInfoExist)
            {
                sophonFileInfo.Delete();
            }

            return asset.WriteToStreamAsync(
                                            client,
                                            () => new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                                                                 FileShare.ReadWrite),
                                            parallelOptions,
                                            UpdateSophonFileTotalProgress,
                                            UpdateSophonFileDownloadProgress,
                                            UpdateSophonDownloadStatus
                                           );
        }

        private async Task EnsureDiskSpaceSufficiencyAsync<TFrom>(
            long                                      sizeToCompare,
            string                                    gamePath,
            List<TFrom>                               assetList,
            SignedValueTaskSelectorAsync<TFrom, long> sizeSelector,
            CancellationToken                         token)
        {
            // Get SIMD'ed total sizes
            long downloadedSize = await assetList.SumParallelAsync(
                                                                   async (x, ctx) => await sizeSelector(x, ctx),
                                                                   token);

            long sizeRemainedToDownload = sizeToCompare - downloadedSize;

            // Push log regarding size
            Logger.LogWriteLine($"Total free space required to download: {ConverterTool.SummarizeSizeSimple(sizeToCompare)}"
                                + $" and {ConverterTool.SummarizeSizeSimple(sizeRemainedToDownload)} remained to be downloaded.",
                                LogType.Default, true);

            // Get the information about the disk
            DriveInfo driveInfo = new DriveInfo(gamePath);

            // Push log regarding disk space
            Logger.LogWriteLine($"Total free space remained on disk: {driveInfo.Name}: {ConverterTool.SummarizeSizeSimple(driveInfo.TotalFreeSpace)}.",
                                LogType.Default, true);

            // If the space is insufficient, then show the dialog and throw
            if (sizeRemainedToDownload > driveInfo.TotalFreeSpace)
            {
                string errStr = $"Free Space on {driveInfo.Name} is not sufficient! " +
                                $"(Free space: {ConverterTool.SummarizeSizeSimple(driveInfo.TotalFreeSpace)}, Req. Space: {ConverterTool.SummarizeSizeSimple(sizeRemainedToDownload)} (Total: {ConverterTool.SummarizeSizeSimple(sizeToCompare)}), " +
                                $"Drive: {driveInfo.Name})";
                await SimpleDialogs.Dialog_InsufficientDriveSpace(ParentUI, driveInfo.TotalFreeSpace,
                                                                  sizeRemainedToDownload, driveInfo.Name);

                // Push log for the disk space error
                Logger.LogWriteLine(errStr, LogType.Error, true);
                throw new TaskCanceledException(errStr);
            }
        }

        #endregion

        #region Sophon Asset Package Methods
        private async Task<bool> AddSophonDiffAssetsToList(HttpClient                 httpClient,
                                                           string                     requestedUrlFrom,
                                                           string                     requestedUrlTo,
                                                           List<SophonAsset>          sophonPreloadAssetList,
                                                           string                     matchingField,
                                                           SophonDownloadSpeedLimiter downloadSpeedLimiter)
        {
            // Get the manifest pair for both previous (from) and next (to) version
            SophonChunkManifestInfoPair requestPairFrom = await SophonManifest
               .CreateSophonChunkManifestInfoPair(httpClient, requestedUrlFrom, matchingField, Token.Token);
            SophonChunkManifestInfoPair requestPairTo = await SophonManifest
               .CreateSophonChunkManifestInfoPair(httpClient, requestedUrlTo, matchingField, Token.Token);

            // If the request pair source is not found, then return false
            if (!requestPairFrom.IsFound)
            {
                Logger.LogWriteLine($"Sophon manifest for source via URL: {requestedUrlFrom} is not found! Return message: {requestPairFrom.ReturnMessage} ({requestPairFrom.ReturnCode})", LogType.Warning, true);
                return false;
            }

            // If the request pair target is not found, then return false
            if (!requestPairTo.IsFound)
            {
                Logger.LogWriteLine($"Sophon manifest for target via URL: {requestedUrlTo} is not found! Return message: {requestPairTo.ReturnMessage} ({requestPairTo.ReturnCode})", LogType.Warning, true);
                return false;
            }

            // Ensure that the manifest is ordered based on _gameVoiceLanguageLocaleIdOrdered
            RearrangeSophonDataLocaleOrder(requestPairFrom.OtherSophonData);
            RearrangeSophonDataLocaleOrder(requestPairTo.OtherSophonData);

            // Add asset to the list
            await foreach (SophonAsset sophonAsset in SophonUpdate
                                                     .EnumerateUpdateAsync(httpClient, requestPairFrom, requestPairTo,
                                                                           false,      downloadSpeedLimiter)
                                                     .WithCancellation(Token.Token))
            {
                sophonPreloadAssetList.Add(sophonAsset);
            }

            // Return as success
            return true;
        }

        private async Task AddSophonAdditionalVODiffAssetsToList(HttpClient                 httpClient,
                                                                 string                     requestedUrlFrom,
                                                                 string                     requestedUrlTo,
                                                                 List<SophonAsset>          sophonPreloadAssetList,
                                                                 SophonDownloadSpeedLimiter downloadSpeedLimiter)
        {
            // Get the main VO language name from Id
            string mainLangId = GetLanguageLocaleCodeByID(_gameVoiceLanguageID);
            // Get the manifest pair for both previous (from) and next (to) version for the main VO
            await AddSophonDiffAssetsToList(httpClient,             requestedUrlFrom, requestedUrlTo,
                                            sophonPreloadAssetList, mainLangId,       downloadSpeedLimiter);

            // Check if the audio lang list file is exist, then try add others
            FileInfo fileInfo = new FileInfo(_gameAudioLangListPath).EnsureNoReadOnly();
            if (fileInfo.Exists)
            {
                // Use stream reader to read the list one-by-one
                using StreamReader reader = new StreamReader(_gameAudioLangListPath);
                // Read until EOF
                while (await reader.ReadLineAsync() is { } line)
                {
                    // Get other lang Id, pass it and try add to the list
                    string? otherLangId = GetLanguageLocaleCodeByLanguageString(line
                                                                            #if !DEBUG
                        , false
                                                                            #endif
                                                                               );

                    // Check if the voice pack is actually the same as default.
                    if (string.IsNullOrEmpty(otherLangId) ||
                        otherLangId.Equals(mainLangId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Get the manifest pair for both previous (from) and next (to) version for other VOs
                    await AddSophonDiffAssetsToList(httpClient,             requestedUrlFrom, requestedUrlTo,
                                                    sophonPreloadAssetList, otherLangId,      downloadSpeedLimiter);
                }
            }
        }

        #endregion

        #region Sophon Thread Related Methods

        protected virtual int SophonGetThreadNum()
        {
            // Get from config
            var n = LauncherConfig.GetAppConfigValue("SophonCpuThread").ToInt();
            if (n == 0) // If config is default "0", then use sqrt of thread number as safe number
            {
                n = (int)Math.Sqrt(Environment.ProcessorCount);
            }

            return Math.Clamp(n, 2, 64); // Clamp value to prevent errors
        }

        protected virtual int SophonGetHttpHandler()
        {
            var n = LauncherConfig.GetAppConfigValue("SophonHttpConnInt").ToInt();
            if (n == 0)
            {
                n = (int)Math.Sqrt(Environment.ProcessorCount) * 2;
            }

            return Math.Clamp(n, 4, 128);
        }

        #endregion

        #region Sophon Audio/Voice-Packs Locale Methods

        protected virtual int GetSophonLocaleCodeIndex(SophonData sophonData, string lookupName)
        {
            List<string> localeList = sophonData.ManifestIdentityList
                                                .Where(x => IsValidLocaleCode(x.MatchingField))
                                                .Select(x => x.MatchingField.ToLower())
                                                .ToList();

            int index = localeList.IndexOf(lookupName);
            return Math.Max(0, index);
        }

        protected virtual List<string> GetSophonLanguageDisplayDictFromVoicePackList(SophonData sophonData)
        {
            List<string> value = [];
            for (var index = 0; index < sophonData.ManifestIdentityList.Count; index++)
            {
                var identity = sophonData.ManifestIdentityList[index];
                // Check the lang ID and add the translation of the language to the list
                string localeCode = identity.MatchingField.ToLower();
                if (!IsValidLocaleCode(localeCode))
                {
                    continue;
                }

                string? languageDisplay = GetLanguageDisplayByLocaleCode(localeCode, false);
                if (string.IsNullOrEmpty(languageDisplay))
                {
                    continue;
                }

                value.Add(languageDisplay);
            }

            return value;
        }

        protected virtual void RearrangeSophonDataLocaleOrder(SophonData? sophonData)
        {
            // Rearrange the sophon data list order based on matching field for the locale
            RearrangeDataListLocaleOrder(sophonData?.ManifestIdentityList, x => x.MatchingField);
        }

        protected virtual void WriteAudioLangListSophon(List<string> sophonVOList)
        {
            // Create persistent directory if not exist
            if (!Directory.Exists(_gameDataPersistentPath))
            {
                Directory.CreateDirectory(_gameDataPersistentPath);
            }

            // If the game does not have audio lang list, then return
            if (string.IsNullOrEmpty(_gameAudioLangListPathStatic))
            {
                return;
            }

            // Read all the existing list
            List<string> langList = File.Exists(_gameAudioLangListPathStatic)
                ? File.ReadAllLines(_gameAudioLangListPathStatic).ToList()
                : [];

            // Try lookup if there is a new language list, then add it to the list
            for (int index = 0; index < sophonVOList.Count; index++)
            {
                var    packageLocaleCodeString = sophonVOList[index];
                string langString              = GetLanguageStringByLocaleCode(packageLocaleCodeString);
                if (!langList.Contains(langString, StringComparer.OrdinalIgnoreCase))
                {
                    langList.Add(langString);
                }
            }

            // Create the audio lang list file
            using var sw = new StreamWriter(_gameAudioLangListPathStatic,
                                            new FileStreamOptions
                                                { Mode = FileMode.Create, Access = FileAccess.Write });
            // Iterate the package list
            foreach (var voIds in langList)
                // Write the language string as per ID
            {
                sw.WriteLine(voIds);
            }
        }

        #endregion
    }
}