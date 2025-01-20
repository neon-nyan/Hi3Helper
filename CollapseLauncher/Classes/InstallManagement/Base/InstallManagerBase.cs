// ReSharper disable MergeIntoLogicalPattern
// ReSharper disable MemberCanBeMadeStatic.Local
// ReSharper disable ConditionIsAlwaysTrueOrFalse
// ReSharper disable ShiftExpressionResultEqualsZero
// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable GrammarMistakeInComment
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable SuggestBaseTypeForParameter
// ReSharper disable ArrangeObjectCreationWhenTypeEvident
// ReSharper disable EmptyRegion
// ReSharper disable ConvertToPrimaryConstructor

using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.DiscordPresence;
using CollapseLauncher.Extension;
using CollapseLauncher.FileDialogCOM;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.InstallManagement.Base;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Pages;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.Http;
using Hi3Helper.Http.Legacy;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using SevenZipExtractor;
using SevenZipExtractor.Event;
using SharpHDiffPatch.Core;
using SharpHDiffPatch.Core.Event;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using CoreCombinedStream = Hi3Helper.EncTool.CombinedStream;

#if USENEWZIPDECOMPRESS
using ZipArchive = SharpCompress.Archives.Zip.ZipArchive;
using ZipArchiveEntry = SharpCompress.Archives.Zip.ZipArchiveEntry;
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
#endif

// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable AccessToDisposedClosure

// ReSharper disable UnusedMember.Global

namespace CollapseLauncher.InstallManager.Base
{
    public enum CompletenessStatus
    {
        Running,
        Completed,
        Cancelled,
        Idle
    }

    public enum MigrateFromLauncherType
    {
        Official,
        BetterHi3Launcher,
        Steam,
        Unknown
    }

    // ReSharper disable once UnusedTypeParameter
    internal partial class InstallManagerBase : ProgressBase<GameInstallPackage>, IGameInstallManager
    {
        #region Internal Struct

        protected struct UninstallGameProperty
        {
            public string   gameDataFolderName;
            public string[] filesToDelete;
            public string[] foldersToDelete;
            public string[] foldersToKeepInData;
        }

        protected delegate Task InstallPackageExtractorDelegate(GameInstallPackage asset);

        #endregion

        #region Properties

        private const string PreloadVerifiedFileName = "preload.verified";

        protected readonly string _gamePersistentFolderBasePath;
        protected readonly string _gameStreamingAssetsFolderBasePath;
        protected          RegionResourceGame _gameRegion => GameVersionManager!.GameApiProp!.data;
        protected          GameVersion? _gameLatestVersion => GameVersionManager!.GetGameVersionApi();
        protected          GameVersion? _gameLatestPreloadVersion => GameVersionManager!.GetGameVersionApiPreload();
        protected          GameVersion? _gameInstalledVersion => GameVersionManager!.GetGameExistingVersion();

        // TODO: Override if the game was supposed to have voice packs (For example: Genshin)
        protected virtual int _gameVoiceLanguageID => int.MinValue;

        protected virtual string[] _gameVoiceLanguageLocaleIdOrdered => [
                "zh-cn",
                "en-us",
                "ja-jp",
                "ko-kr"
                ];

        protected virtual string _gameDataPath =>
            Path.Combine(GamePath,
                         $"{Path.GetFileNameWithoutExtension(GameVersionManager.GamePreset.GameExecutableName)}_Data");

        protected virtual string _gameDataPersistentPath => Path.Combine(_gameDataPath, "Persistent");
        protected virtual string _gameAudioLangListPath => null;
        protected virtual string _gameAudioLangListPathStatic => null;
        protected IRepair _gameRepairTool { get; set; }
        protected bool _canDeleteHdiffReference => !File.Exists(Path.Combine(GamePath!, "@NoDeleteHdiffReference"));
        protected bool _canDeleteZip => !File.Exists(Path.Combine(GamePath!, "@NoDeleteZip"));
        protected bool _canSkipVerif => File.Exists(Path.Combine(GamePath!, "@NoVerification"));
        protected bool _canSkipAudio => File.Exists(Path.Combine(GamePath!, "@NoAudioPatch"));
        protected bool _canSkipExtract => File.Exists(Path.Combine(GamePath!, "@NoExtraction"));
        protected bool _canMergeDownloadChunks => LauncherConfig.GetAppConfigValue("UseDownloadChunksMerging").ToBool();
        protected virtual bool _canDeltaPatch => false;
        protected virtual DeltaPatchProperty _gameDeltaPatchProperty => null;

        protected List<GameInstallPackage> _gameDeltaPatchPreReqList { get; } = [];
        protected bool                     _forceIgnoreDeltaPatch;
        private   long                     _totalLastSizeCurrent;

        protected bool _isAllowExtractCorruptZip  { get; set; }
        protected UninstallGameProperty? _uninstallGameProperty { get; set; }

        #endregion

        #region Public Properties

        public event EventHandler FlushingTrigger;
        public virtual bool       StartAfterInstall { get; set; }
        public virtual bool       IsRunning         { get; protected set; }
        #endregion

        public InstallManagerBase(UIElement parentUI, IGameVersionCheck GameVersionManager)
            : base(parentUI, GameVersionManager, "", "", null)
        {
            _gamePersistentFolderBasePath =
                $"{Path.GetFileNameWithoutExtension(GameVersionManager!.GamePreset!.GameExecutableName)}_Data\\Persistent";
            _gameStreamingAssetsFolderBasePath =
                $"{Path.GetFileNameWithoutExtension(GameVersionManager!.GamePreset!.GameExecutableName)}_Data\\StreamingAssets";
            UpdateCompletenessStatus(CompletenessStatus.Idle);
        }

        protected void ResetToken()
        {
            Token = new CancellationTokenSourceWrapper();
        }

        public void Dispose()
        {
            _gameRepairTool?.Dispose();
            Token?.Cancel();
            IsRunning = false;
            Flush();
        }

        public virtual void Flush()
        {
            UpdateCompletenessStatus(CompletenessStatus.Idle);
            _gameRepairTool?.Dispose();
            AssetIndex?.Clear();

            FlushingTrigger?.Invoke(this, EventArgs.Empty);

            // Reset _forceIgnoreDeltaPatch state to false
            _forceIgnoreDeltaPatch = false;
        }

        #region Public Methods

        protected virtual async ValueTask<int> ConfirmDeltaPatchDialog(DeltaPatchProperty patchProperty,
                                                                       IRepair            gameRepair)
        {
            // Reinitialize the cancellation token
            ResetToken();

            // Initialize the game state and game package list
            _gameDeltaPatchPreReqList?.Clear();

            GameInstallStateEnum gameState = await GameVersionManager.GetGameState();

            // Check if the game has delta patch and in NeedsUpdate status. If true, then
            // proceed with the delta patch update
            if (!_canDeltaPatch || gameState != GameInstallStateEnum.NeedsUpdate || _forceIgnoreDeltaPatch)
            {
                return 0;
            }

            // If the requirement returns false, then cancel the delta patch
            // and back to use the normal update (0)
            if (!await GetAndDownloadDeltaPatchPreReq(_gameDeltaPatchPreReqList, gameState))
            {
                return 0;
            }

            switch (await Dialog_DeltaPatchFileDetected(ParentUI, patchProperty!.SourceVer,
                                                        patchProperty.TargetVer))
            {
                // If no, then proceed with normal update (0)
                // Also set ignore delta patch process if this method is re-called
                case ContentDialogResult.Secondary:
                    _forceIgnoreDeltaPatch = true;
                    return 0;
                // If cancel. then proceed to cancel (-1)
                case ContentDialogResult.None:
                    return -1;
            }

            // Always reset the token
            ResetToken();

            // Initialize repair tool
            _gameRepairTool = gameRepair;
            try
            {
                // Set the activity
                Status.ActivityStatus            = Lang!._GameRepairPage!.Status2!;
                Status.IsIncludePerFileIndicator = false;
                Status.IsProgressAllIndetermined = true;
                UpdateStatus();

                // Start the check routine and get the state if download needed
                _gameRepairTool!.ProgressChanged += DeltaPatchCheckProgress;
                bool isDownloadNeeded = await _gameRepairTool.StartCheckRoutine()!;
                if (isDownloadNeeded)
                {
                    Status.ActivityStatus   = Lang._GameRepairPage.Status8!.Replace(": ", "");
                    ProgressAllSizeCurrent  = 0;
                    ProgressAllCountCurrent = 1;
                    UpdateStatus();

                    // If download needed, then start the repair (download) routine
                    await _gameRepairTool!.StartRepairRoutine(true)!;
                }
            }
            catch (Exception ex)

            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                IsRunning = false;
                throw;
            }
            finally
            {
                // Unsubscribe the progress event
                _gameRepairTool!.ProgressChanged -= DeltaPatchCheckProgress;
            }

            // Then return 1 as continue to other steps
            return 1;

        }

        protected virtual async ValueTask<bool> StartDeltaPatch(IRepairAssetIndex repairGame, bool isHonkai,
                                                                bool              isSR = false)
        {
            // Initialize the state and the package
            GameInstallStateEnum gameState = await GameVersionManager.GetGameState();

            // ReSharper disable once UnusedVariable
            List<GameInstallPackage> gamePackage = [];

            // Return false to indicate that there's no delta patch running
            if (!_canDeltaPatch || gameState != GameInstallStateEnum.NeedsUpdate || _forceIgnoreDeltaPatch)
            {
                return false;
            }

            DeltaPatchProperty patchProperty = _gameDeltaPatchProperty;

            string previousPath   = GamePath!;
            string ingredientPath = previousPath.TrimEnd('\\') + "_Ingredients";

            try
            {
                List<FilePropertiesRemote> localAssetIndex = repairGame!.GetAssetIndex();
                MoveFileToIngredientList(localAssetIndex, previousPath, ingredientPath, isSR);

                // Get the sum of uncompressed size and
                // Set progress count to beginning
                ProgressAllSizeTotal              = localAssetIndex!.Sum(x => x!.S);
                ProgressAllSizeCurrent            = 0;
                ProgressAllCountCurrent           = 1;
                Status.IsIncludePerFileIndicator = false;
                Status.IsProgressAllIndetermined = true;
                Status.ActivityStatus            = Lang!._Misc!.ApplyingPatch;
                UpdateStatus();

                // Start the patching process
                HDiffPatch.LogVerbosity   =  Verbosity.Verbose;
                EventListener.PatchEvent  += DeltaPatchCheckProgress;
                EventListener.LoggerEvent += DeltaPatchCheckLogEvent;
                await Task.Run(() =>
                               {
                                   HDiffPatch patch = new HDiffPatch();
                                   patch.Initialize(patchProperty!.PatchPath);
                                   patch.Patch(ingredientPath, previousPath, true, Token!.Token, false, true);
                               }).ConfigureAwait(false);

                // Remove ingredient folder
                Directory.Delete(ingredientPath, true);

                if (!_canDeleteZip)
                {
                    return true;
                }

                if (patchProperty == null || string.IsNullOrEmpty(patchProperty.PatchPath))
                {
                    return true;
                }

                // Delete the necessary files after delta patch operation
                // Delete the delta patch file
                File.Delete(patchProperty!.PatchPath!);

                // Then return
                return true;
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Error has occurred while performing delta-patch!\r\n{ex}", LogType.Error, true);
                throw;
            }
            finally
            {
                EventListener.PatchEvent  -= DeltaPatchCheckProgress;
                EventListener.LoggerEvent -= DeltaPatchCheckLogEvent;
            }
        }

        protected virtual async ValueTask<bool> GetAndDownloadDeltaPatchPreReq(
            List<GameInstallPackage> gamePackage, GameInstallStateEnum gameState)
        {
            // Iterate the latest game package list
            foreach (RegionResourceVersion asset in GameVersionManager!.GetGameLatestZip(gameState)!)
            {
                await TryAddResourceVersionList(asset, gamePackage, true);
            }

            // Start getting the size of the packages
            await GetPackagesRemoteSize(gamePackage, Token!.Token);

            // If the game package list is empty, then return true as skips the prerequisite download
            if (gamePackage == null || gamePackage.Count == 0)
            {
                return true;
            }

            // Get the required disk size
            long totalDownloadSize = gamePackage.Sum(x => x!.Size);
            //long requiredDiskSpace = gamePackage.Sum(x => x!.SizeRequired);

            // Build the dialog UI
            var locDialogs = Lang!._Dialogs!;
            var locMisc    = Lang!._Misc!;

            TextBlock message = new TextBlock { TextWrapping = TextWrapping.Wrap };
            message.AddTextBlockLine(locDialogs.DeltaPatchPreReqSubtitle1);
            message.AddTextBlockLine(ConverterTool.SummarizeSizeSimple(totalDownloadSize), FontWeights.SemiBold);
            message.AddTextBlockLine(locDialogs.DeltaPatchPreReqSubtitle2);
            message.AddTextBlockNewLine(2);
            message.AddTextBlockLine(locDialogs.DeltaPatchPreReqSubtitle3);
            message.AddTextBlockNewLine(2);
            message.AddTextBlockLine(locDialogs.DeltaPatchPreReqSubtitle4, null,                 10);
            message.AddTextBlockLine(locMisc.YesContinue,                  FontWeights.SemiBold, 10);
            message.AddTextBlockLine(locDialogs.DeltaPatchPreReqSubtitle5, null,                 10);
            message.AddTextBlockLine(locMisc.No,                           FontWeights.SemiBold, 10);
            message.AddTextBlockLine(locDialogs.DeltaPatchPreReqSubtitle6, null,                 10);
            message.AddTextBlockLine(locMisc.Cancel,                       FontWeights.SemiBold, 10);
            message.AddTextBlockLine(locDialogs.DeltaPatchPreReqSubtitle7, null,                 10);
            ContentDialogResult dialogResult = await SpawnDialog(
                                                                 locDialogs.DeltaPatchPreReqTitle,
                                                                 message,
                                                                 ParentUI,
                                                                 locMisc.Cancel,
                                                                 locMisc.YesContinue,
                                                                 locMisc.No,
                                                                 ContentDialogButton.Primary,
                                                                 ContentDialogTheme.Warning
                                                                );

            switch (dialogResult)
            {
                // If the dialog result is "No", then return false as back to the normal update
                case ContentDialogResult.Secondary:
                    return false;
                // If the dialog result is "Cancel", then throw cancellation
                case ContentDialogResult.None:
                    throw new OperationCanceledException();
            }

            // Skip installation if sophon is used and not in update state
            if (IsUseSophon && gameState == GameInstallStateEnum.NotInstalled)
            {
                return true;
            }

            // Start the download routine
            await StartDeltaPatchPreReqDownload(gamePackage);

            return true;
        }

        protected virtual async ValueTask StartDeltaPatchPreReqDownload(List<GameInstallPackage> gamePackage)
        {
            if (Status == null || gamePackage == null)
            {
                throw new
                    Exception("InstallManagement::StartDeltaPatchPreReqDownload() is unexceptedly not initialized!");
            }

            // Initialize new proxy-aware HttpClient
            using HttpClient httpClientNew = new HttpClientBuilder()
                .UseLauncherConfig(DownloadThreadCount + DownloadThreadCountReserved)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Use the new DownloadClient (if available)
            DownloadClient downloadClient = DownloadClient.CreateInstance(httpClientNew);

            // Set the progress bar to indetermined
            Status.IsIncludePerFileIndicator = gamePackage.Sum(x => x!.Segments != null ? x.Segments.Count : 1) > 1;
            Status.IsProgressPerFileIndetermined = true;
            Status.IsProgressAllIndetermined = true;
            UpdateStatus();

            // Start getting the size of the packages
            await GetPackagesRemoteSize(gamePackage, Token!.Token);

            // Get the remote total size and current total size
            ProgressAllSizeTotal   = gamePackage.Sum(x => x!.Size);
            ProgressAllSizeCurrent = await GetExistingDownloadPackageSize(downloadClient, gamePackage, Token!.Token);

            // Sanitize Check: Check for the free space of the drive and show the dialog if necessary
            await CheckDriveFreeSpace(ParentUI, gamePackage, ProgressAllSizeCurrent);

            // Check for the existing download
            await CheckExistingDownloadAsync(ParentUI, gamePackage);

            StartDeltaPatchPreReqVerification:
            // Set progress bar to not indetermined
            Status.IsProgressPerFileIndetermined = false;
            Status.IsProgressAllIndetermined     = false;

            // Start downloading process
            await InvokePackageDownloadRoutine(downloadClient, gamePackage, Token!.Token);

            // Reset the progress status
            ProgressAllSizeCurrent  = 0;
            ProgressAllCountCurrent = 1;

            // Start the verification routine
            foreach (GameInstallPackage asset in gamePackage.ToList())
            {
                switch (await RunPackageVerificationRoutine(asset, Token!.Token))
                {
                    // 0 means redownload the corrupted file 
                    case 0:
                        goto StartDeltaPatchPreReqVerification;
                    // -1 means cancel
                    case -1:
                        throw new OperationCanceledException();
                }
            }
        }

#nullable enable
        protected virtual IRepair? GetGameRepairInstance(string? sourceVer) => null;
#nullable restore
        private delegate ValueTask<int> CheckExistingInstallDelegate(bool isHasOnlyMigrateOption);

        // Bool:  0      -> Indicates that the action is completed and no need to step further
        //        1      -> Continue to the next step
        //       -1      -> Cancel the operation
        public virtual async ValueTask<int> GetInstallationPath(bool isHasOnlyMigrateOption = false)
        {
            // Assign check delegates
            CheckExistingInstallDelegate[] checkExistingDelegates = [
                CheckExistingSteamInstallation,
                CheckExistingBHI3LInstallation,
                CheckExistingOfficialInstallation
                ];

            int checkResult = 0;
            foreach (CheckExistingInstallDelegate checkDelegate in checkExistingDelegates)
            {
                // Try get the existing path. If it return true, then continue to the next step
                checkResult = await checkDelegate(isHasOnlyMigrateOption);
                if (isHasOnlyMigrateOption && checkResult == 0)
                {
                    return 0;
                }

                // Decide the result and continue to ask folder dialog
                // if the choice is to install a new game
                switch (checkResult)
                {
                    case > 1:
                        return await CheckExistingOrAskFolderDialog();
                    case <= 0:
                        return checkResult;
                }
            }

            // If all results returns 1 (cannot detect existing game and proceed) while
            // isHasOnlyMigrateOption is set, then cancel it.
            if (isHasOnlyMigrateOption && checkResult == 1)
            {
                return -1;
            }

            return await CheckExistingOrAskFolderDialog();
        }

        public virtual async Task StartPackageDownload(bool skipDialog)
        {
            ResetToken();

            // Get the game state and run the action for each of them
            GameInstallStateEnum gameState = await GameVersionManager!.GetGameState();
            LogWriteLine($"Gathering packages information for installation (State: {gameState})...", LogType.Default,
                         true);

            if (IsUseSophon)
            {
                switch (gameState)
                {
                    case GameInstallStateEnum.NotInstalled:
                        await StartPackageInstallSophon(gameState);
                        gameState = GameInstallStateEnum.InstalledHavePlugin;
                        break;
                    case GameInstallStateEnum.NeedsUpdate:
                        await StartPackageUpdateSophon(gameState, false);
                        gameState = GameInstallStateEnum.InstalledHavePlugin;
                        break;
                    case GameInstallStateEnum.InstalledHavePreload:
                        await StartPackageUpdateSophon(gameState, true);
                        return;
                }
            }

            switch (gameState)
            {
                case GameInstallStateEnum.NotInstalled:
                case GameInstallStateEnum.GameBroken:
                case GameInstallStateEnum.NeedsUpdate:
                case GameInstallStateEnum.InstalledHavePlugin:
                    await GetLatestPackageList(AssetIndex, gameState, false);
                    break;
                case GameInstallStateEnum.InstalledHavePreload:
                    await GetLatestPackageList(AssetIndex, gameState, true);
                    break;
            }

            // Initialize new proxy-aware HttpClient
            using HttpClient httpClientNew = new HttpClientBuilder()
                .UseLauncherConfig(DownloadThreadCount + DownloadThreadCountReserved)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Use the new DownloadClient (if available)
            DownloadClient downloadClient = DownloadClient.CreateInstance(httpClientNew);

            // Set the progress bar to indetermined
            Status.IsIncludePerFileIndicator =
                AssetIndex!.Sum(x => x!.Segments != null ? x.Segments.Count : 1) > 1;
            Status.IsProgressPerFileIndetermined = true;
            Status.IsProgressAllIndetermined     = true;
            UpdateStatus();

            // Start getting the size of the packages
            await GetPackagesRemoteSize(AssetIndex, Token!.Token);

            // Get the remote total size and current total size
            ProgressAllSizeTotal   = AssetIndex!.Sum(x => x!.Size);
            ProgressAllSizeCurrent = await GetExistingDownloadPackageSize(downloadClient, AssetIndex, Token!.Token);

            // Sanitize Check: Check for the free space of the drive and show the dialog if necessary
            await CheckDriveFreeSpace(ParentUI, AssetIndex, ProgressAllSizeCurrent);

            // Sanitize Check: Show dialog for resuming/reset the existing download
            if (!skipDialog)
            {
                await CheckExistingDownloadAsync(ParentUI, AssetIndex);
            }

            // Start downloading process
            await InvokePackageDownloadRoutine(downloadClient, AssetIndex, Token!.Token);
        }

        // Bool:  0      -> Indicates that one of the package is failing and need to redownload
        //                  or the verification can't be started because the download never being performed first
        //        1      -> Continue to the next step (all passes)
        //       -1      -> Cancel the operation
        public virtual async ValueTask<int> StartPackageVerification(List<GameInstallPackage> gamePackage)
        {
            // Skip routine if sophon is use
            GameInstallStateEnum gameState = await GameVersionManager!.GetGameState();
            if ((IsUseSophon && gameState == GameInstallStateEnum.NotInstalled)
                || _isSophonDownloadCompleted)
            {
                // We are going to override the verification method from base class. So in order to bypass the loop,
                // we need to ensure if the IsDownloadCompleted is true. If so, set it to false and return 1 as successful.
                // Otherwise, return 0 as continue the installation.
                if (!_isSophonDownloadCompleted)
                {
                    return 0;
                }

                _isSophonDownloadCompleted = false;
                return 1;
            }

            gamePackage ??= AssetIndex;
            ArgumentNullException.ThrowIfNull(gamePackage);

            // Get the total asset count
            int assetCount = gamePackage.Sum(x => x!.Segments != null ? x.Segments.Count : 1);

            // If the assetIndex is empty, then skip and return 0
            if (assetCount == 0)
            {
                return 0;
            }

            // If _canSkipVerif flag is true, then return 1 (skip) the verification;
            if (_canSkipVerif)
            {
                return 1;
            }

            // Set progress count to beginning
            ProgressAllSizeTotal              = gamePackage.Sum(x => x!.Size);
            ProgressAllSizeCurrent            = 0;
            ProgressAllCountCurrent           = 1;
            ProgressAllCountTotal             = assetCount;
            Status.IsIncludePerFileIndicator = assetCount > 1;

            // Set progress bar to not indetermined
            Status.IsProgressPerFileIndetermined = false;
            Status.IsProgressAllIndetermined     = false;

            // Iterate the asset
            foreach (GameInstallPackage asset in gamePackage.ToList())
            {
                int returnCode;

                if (asset == null)
                {
                    return 0;
                }

                // Iterate if the package has segment
                if (asset.Segments != null)
                {
                    for (int i = 0; i < asset.Segments.Count; i++)
                    {
                        // Run the package verification routine
                        if ((returnCode = await RunPackageVerificationRoutine(asset.Segments[i], Token!.Token)) < 1)
                        {
                            return returnCode;
                        }
                    }

                    continue;
                }

                // Run the package verification routine as a single package
                if ((returnCode = await RunPackageVerificationRoutine(asset, Token!.Token)) < 1)
                {
                    return returnCode;
                }
            }

            return 1;
        }

        private async ValueTask<int> RunPackageVerificationRoutine(GameInstallPackage asset, CancellationToken token)
        {
            // Reset per size counter
            ProgressPerFileSizeCurrent = 0;

            byte[] hashLocal;
            await using (Stream fs = asset!.GetReadStream(DownloadThreadCount)!)
            {
                // Reset the per file size
                ProgressPerFileSizeTotal = fs.Length;
                Status.ActivityStatus =
                    $"{Lang!._Misc!.Verifying}: {string.Format(Lang._Misc.PerFromTo!, ProgressAllCountCurrent,
                                                               ProgressAllCountTotal)}";
                UpdateStatus();

                // Run the check and assign to hashLocal variable
                hashLocal = await GetCryptoHashAsync<MD5>(fs, null, true, true, token);
            }

            // Check for the hash differences. If found, then show dialog to delete or cancel the process
            if (!IsArrayMatch(hashLocal, asset.Hash))
            {
                switch (await Dialog_GameInstallationFileCorrupt(ParentUI, asset.HashString,
                                                                 HexTool.BytesToHexUnsafe(hashLocal)))
                {
                    case ContentDialogResult.Primary:
                        ProgressAllSizeCurrent -= asset.Size;
                        DeleteDownloadedFile(asset.PathOutput, DownloadThreadCount);
                        return 0;
                    case ContentDialogResult.None:
                        return -1;
                    case ContentDialogResult.Secondary: // To proceed on extracting the file even it's corrupted
                        string fileName = Path.GetFileName(asset.PathOutput);
                        ContentDialogResult installCorruptDialogResult =
                            await Dialog_GameInstallCorruptedDataAnyway(ParentUI, fileName, asset.Size);
                        // If cancel is pressed, then cancel the whole process
                        if (installCorruptDialogResult == ContentDialogResult.None)
                        {
                            return -1;
                        }

                        _isAllowExtractCorruptZip = true;
                        break;
                }
            }

            // Increment the total current count
            ProgressAllCountCurrent++;

            // Return 1 as OK
            return 1;
        }

        private long GetAssetIndexTotalUncompressSize(List<GameInstallPackage> assetIndex)
        {
            ArgumentNullException.ThrowIfNull(assetIndex);

            long returnSize = assetIndex.Sum(GetSingleOrSegmentedUncompressedSize);
            return returnSize;
        }

        private long GetSingleOrSegmentedUncompressedSize(GameInstallPackage asset)
        {
            using Stream      stream      = GetSingleOrSegmentedDownloadStream(asset);
            using ArchiveFile archiveFile = ArchiveFile.Create(stream, true);
            return archiveFile.Entries.Sum(x => (long)x!.Size);
        }

        private Stream GetSingleOrSegmentedDownloadStream(GameInstallPackage asset)
        {
            if (asset == null)
            {
                return null;
            }

            return asset.Segments != null && asset.Segments.Count != 0
                ? new CoreCombinedStream(asset.Segments.Select(x => x!.GetReadStream(DownloadThreadCount)).ToArray())
                : asset.GetReadStream(DownloadThreadCount);
        }

        private void DeleteSingleOrSegmentedDownloadStream(GameInstallPackage asset)
        {
            if (asset == null)
            {
                return;
            }

            if (asset.Segments != null && asset.Segments.Count != 0)
            {
                asset.Segments.ForEach(x => x!.DeleteFile(DownloadThreadCount));
                return;
            }

            asset.DeleteFile(DownloadThreadCount);
        }

        public async Task StartPackageInstallation()
        {
            await StartPackageInstallationInner();
        }

        protected virtual async Task StartPackageInstallationInner(List<GameInstallPackage> gamePackage = null,
                                                                   bool isOnlyInstallPackage = false,
                                                                   bool doNotDeleteZipExplicit = false)
        {
            // Sanity Check: Check if the _gamePath is null, then throw
            if (string.IsNullOrEmpty(GamePath))
            {
                throw new NullReferenceException("_gamePath cannot be null or empty!");
            }

            // If the gamePackage arg is null, then assign one from _assetIndex
            gamePackage ??= AssetIndex;

            // Get the sum of uncompressed size and
            // Set progress count to beginning
            ProgressAllSizeTotal = GetAssetIndexTotalUncompressSize(gamePackage);

            // Sanity Check: Check if the package list is empty or not
            if (gamePackage == null || gamePackage.Count == 0)
            {
                return;
            }

            // If _canSkipExtract flag is true, then return (skip) the extraction
            if (_canSkipExtract)
            {
                return;
            }

            ProgressAllSizeCurrent            = 0;
            ProgressAllCountCurrent           = 1;
            ProgressAllCountTotal             = gamePackage.Count;
            Status.IsIncludePerFileIndicator = gamePackage.Count > 1;

            // Reset the last size counter
            _totalLastSizeCurrent = 0;

            // Try to unassign read-only and redundant diff files
            TryUnassignReadOnlyFiles();
            TryRemoveRedundantHDiffList();

            // Enumerate the installation package
            foreach (GameInstallPackage asset in gamePackage.ToList())
            {
                // Update the status
                Status.ActivityStatus =
                    $"{Lang!._Misc!.Extracting}: {string.Format(Lang._Misc.PerFromTo!, ProgressAllCountCurrent,
                                                                ProgressAllCountTotal)}";
                Status.IsProgressPerFileIndetermined = false;
                Status.IsProgressAllIndetermined     = false;
                UpdateStatus();

                ProgressPerFileSizeCurrent = 0;
                ProgressPerFileSizeTotal = GetSingleOrSegmentedUncompressedSize(asset);

                // Assign extractor
            #if USENEWZIPDECOMPRESS
                InstallPackageExtractorDelegate installTaskDelegate;
                if (LauncherConfig.IsEnforceToUse7zipOnExtract)
                {
                    installTaskDelegate = ExtractUsing7zip;
                }
                else if ((asset!.PathOutput.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                       || asset!.PathOutput.EndsWith(".zip.001", StringComparison.OrdinalIgnoreCase))
                       && !_isAllowExtractCorruptZip)
                {
                    installTaskDelegate = ExtractUsingNativeZip;
                }
                else
                {
                    installTaskDelegate = ExtractUsing7zip;
                }
            #else
                InstallPackageExtractorDelegate installTaskDelegate = ExtractUsing7zip;
            #endif

                // Execute method delegate for the extractor
                await installTaskDelegate(asset);

                // Get the information about diff and delete list file
                FileInfo hdiffMapList = new FileInfo(Path.Combine(GamePath, "hdiffmap.json")).EnsureNoReadOnly();
                FileInfo hdiffList    = new FileInfo(Path.Combine(GamePath, "hdifffiles.txt")).EnsureNoReadOnly();
                FileInfo deleteList   = new FileInfo(Path.Combine(GamePath, "deletefiles.txt")).EnsureNoReadOnly();

                // If diffmap list file exist, then rename the file
                if (hdiffMapList.Exists)
                {
                    hdiffMapList.MoveTo(Path.Combine(GamePath, $"hdiffmap_{Path.GetFileNameWithoutExtension(asset!.PathOutput)}.json"),
                                        true);
                }

                // If diff list file exist, then rename the file
                if (hdiffList.Exists)
                {
                    hdiffList.MoveTo(Path.Combine(GamePath, $"hdifffiles_{Path.GetFileNameWithoutExtension(asset!.PathOutput)}.txt"),
                                     true);
                }

                // If the delete zip file exist, then rename the file
                if (deleteList.Exists)
                {
                    deleteList
                       .MoveTo(Path.Combine(GamePath, $"deletefiles_{Path.GetFileNameWithoutExtension(asset!.PathOutput)}.txt"),
                               true);
                }

                // If the asset is a plugin and it has running command statement, then
                // try run the command.
                if (!string.IsNullOrEmpty(asset.RunCommand))
                {
                    // Update the status
                    Status.ActivityStatus = $"{Lang!._Misc!.FinishingUp}: {string.Format(Lang._Misc.PerFromTo!,
                        ProgressAllCountCurrent,
                        ProgressAllCountTotal)}";
                    Status.IsProgressPerFileIndetermined = true;
                    Status.IsProgressAllIndetermined     = true;
                    UpdateStatus();

                    try
                    {
                        string arguments;
                        string executableName;
                        // int indexOfArgument = asset.RunCommand.IndexOf(".exe ", StringComparison.OrdinalIgnoreCase) + 5;
                        // if (indexOfArgument < 5 && !asset.RunCommand.EndsWith(".exe"))
                        // {
                        //     indexOfArgument = asset.RunCommand.IndexOf(' ');
                        // }
                        // else
                        // {
                        //     indexOfArgument = -1;
                        // }
                        //
                        // if (indexOfArgument >= 0)
                        // {
                        //     argument = asset.RunCommand.Substring(indexOfArgument);
                        //     executableName =
                        //         ConverterTool.NormalizePath(asset.RunCommand.Substring(0, indexOfArgument)
                        //                                          .TrimEnd(' '));
                        // }
                        // else
                        // {
                        //     executableName = asset.RunCommand;
                        // }

                        var firstSpaceIndex = asset.RunCommand.IndexOf(' ');
                        if (firstSpaceIndex != -1)
                        {
                            // Split into executable and arguments
                            executableName = asset.RunCommand[..firstSpaceIndex];
                            arguments      = asset.RunCommand[(firstSpaceIndex + 1)..];
                        }
                        else
                        {
                            // No arguments, only executable
                            executableName = asset.RunCommand;
                            arguments = string.Empty;
                        }
                        
                        
                        string executablePath = ConverterTool.NormalizePath(Path.Combine(GamePath, executableName));
                        Process commandProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName        = executablePath,
                                Arguments       = arguments,
                                UseShellExecute = true,
                                WorkingDirectory = Path.GetDirectoryName(executablePath)
                            }
                        };

                        if (!commandProcess.Start())
                        {
                            LogWriteLine($"Run command for the plugin cannot be started! Exit code: {commandProcess.ExitCode}",
                                         LogType.Warning, true);
                        }
                        else
                        {
                            LogWriteLine($"Starting plugin process {executablePath} with argument {arguments}");
                            await commandProcess.WaitForExitAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        await SentryHelper.ExceptionHandler_ForLoopAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                        LogWriteLine($"Error has occurred while trying to run the plugin with id: {asset.PluginId} and command: {asset.RunCommand}\r\n{ex}",
                                     LogType.Error, true);
                    }
                    finally
                    {
                        // Check if the DXSETUP file is exist, then delete it.
                        // The DXSETUP files causes some false positive detection of data modification
                        // for some games (like Genshin, which causes 4302-x errors for some reason)
                        string dxSetupDir = Path.Combine(GamePath, "DXSETUP");
                        TryDeleteReadOnlyDir(dxSetupDir);
                    }
                }

                // If the _canDeleteZip flag is true and not in doNotDeleteZipExplicit mode, then delete the zip
                if (_canDeleteZip && !doNotDeleteZipExplicit)
                {
                    DeleteSingleOrSegmentedDownloadStream(asset);
                }

                ProgressAllCountCurrent++;
            }
        }

    #if USENEWZIPDECOMPRESS
        private async Task ExtractUsingNativeZip(GameInstallPackage asset)
        {
            int threadCounts = ThreadCount;

            await using Stream packageStream = GetSingleOrSegmentedDownloadStream(asset);
            using ZipArchive   archive       = ZipArchive.Open(packageStream);

            int entriesCount = archive.Entries.Count;
            int entriesChunk = (int)Math.Ceiling((double)entriesCount / threadCounts);

            if (entriesCount < threadCounts)
            {
                entriesChunk = entriesCount;
            }

            IReadOnlyCollection<int>      zipEntries       = Enumerable.Range(0, entriesCount).ToList();
            IEnumerable<IEnumerable<int>> zipEntriesChunks = zipEntries.Chunk(entriesChunk);

            // Run the workers
            await Parallel.ForEachAsync(
                                        zipEntriesChunks, new ParallelOptions
                                        {
                                            CancellationToken = Token.Token
                                        },
                                        async (entry, token) =>
                                        {
                                            await using Stream    fs = GetSingleOrSegmentedDownloadStream(asset);
                                            using var             zipArchive = ZipArchive.Open(fs);
                                            List<ZipArchiveEntry> entries = zipArchive.Entries.ToList();
                                            await ExtractUsingNativeZipWorker(entry, entries, token);
                                        });
        }

        private async Task ExtractUsingNativeZipWorker(IEnumerable<int>  entriesIndex, List<ZipArchiveEntry> entries,
                                                       CancellationToken cancellationToken)
        {
            byte[] buffer = GC.AllocateUninitializedArray<byte>(BufferBigLength);

            foreach (int entryIndex in entriesIndex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                ZipArchiveEntry zipEntry = entries[entryIndex];

                if (zipEntry.IsDirectory)
                {
                    continue;
                }

                if (zipEntry.Key == null)
                {
                    continue;
                }

                string outputPath = Path.Combine(GamePath, zipEntry.Key);
                FileInfo outputFile = new FileInfo(outputPath).EnsureCreationOfDirectory().EnsureNoReadOnly();

                await using FileStream outputStream = outputFile.Open(FileMode.Create, FileAccess.Write, FileShare.Write);
                await using Stream entryStream = zipEntry.OpenEntryStream();

                Task runningTask = Task.Factory.StartNew(
                    () => StartWriteInner(buffer, outputStream, entryStream, cancellationToken),
                    cancellationToken,
                    TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default);

                await runningTask.ConfigureAwait(false);
            }

            return;

            void StartWriteInner(byte[] bufferInner, FileStream outputStream, Stream entryStream, CancellationToken cancellationTokenInner)
            {
                int read;

                // Perform async read
                while ((read = entryStream.Read(bufferInner, 0, bufferInner.Length)) > 0)
                {
                    // Throw if cancellation requested
                    cancellationTokenInner.ThrowIfCancellationRequested();

                    // Perform sync write
                    outputStream.Write(bufferInner, 0, read);

                    // Increment total size
                    ProgressAllSizeCurrent     += read;
                    ProgressPerFileSizeCurrent += read;
                    
                    // Calculate the speed
                    double speed = CalculateSpeed(read);

                    if (!CheckIfNeedRefreshStopwatch())
                    {
                        continue;
                    }

                    // Assign local sizes to progress
                    lock (Progress)
                    {
                        Progress.ProgressPerFileSizeCurrent = ProgressPerFileSizeCurrent;
                        Progress.ProgressPerFileSizeTotal   = ProgressPerFileSizeTotal;
                        Progress.ProgressAllSizeCurrent     = ProgressAllSizeCurrent;
                        Progress.ProgressAllSizeTotal       = ProgressAllSizeTotal;
                        Progress.ProgressAllSpeed           = speed;

                        // Calculate percentage
                        Progress.ProgressAllPercentage     = ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent);
                        Progress.ProgressPerFilePercentage = ConverterTool.ToPercentage(ProgressPerFileSizeTotal, ProgressPerFileSizeCurrent);
                        // Calculate the timelapse
                        Progress.ProgressAllTimeLeft       = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal, ProgressAllSizeCurrent, speed);
                    }

                    UpdateAll();
                }
            }
        }
    #endif

        private async Task ExtractUsing7zip(GameInstallPackage asset)
        {
            // Start Async Thread
            // Since the ArchiveFile (especially with the callbacks) can't run under
            // different thread, so the async call will be called at the start
            Stream      stream      = null;
            ArchiveFile archiveFile = null;

            try
            {
                // Load the zip
                stream      = GetSingleOrSegmentedDownloadStream(asset);
                archiveFile = ArchiveFile.Create(stream, true);

                // Start extraction
                archiveFile.ExtractProgress += ZipProgressAdapter;
                await archiveFile.ExtractAsync(e => Path.Combine(GamePath, e!.FileName!), true, BufferMediumLength, Token!.Token);
            }
            finally
            {
                if (archiveFile != null)
                {
                    archiveFile.ExtractProgress -= ZipProgressAdapter;
                    archiveFile.Dispose();
                }

                if (stream != null)
                {
                    await stream.DisposeAsync();
                }
            }
        }

        public virtual void ApplyGameConfig(bool forceUpdateToLatest = false)
        {
            GameVersionManager!.UpdateGamePath(GamePath);
            if (forceUpdateToLatest)
            {
                GameVersionManager.UpdateGameVersionToLatest();
            #nullable enable
                List<RegionResourcePlugin>? gamePluginList = GameVersionManager.GetGamePluginZip();
                if (gamePluginList != null && gamePluginList.Count != 0)
                {
                    Dictionary<string, GameVersion> gamePluginVersionDictionary = new Dictionary<string, GameVersion>();
                    foreach (RegionResourcePlugin plugins in gamePluginList)
                    {
                        if (plugins.plugin_id == null)
                        {
                            continue;
                        }

                        gamePluginVersionDictionary.Add(plugins.plugin_id, new GameVersion(plugins.version));
                    }

                    GameVersionManager.UpdatePluginVersions(gamePluginVersionDictionary);
                }

                RegionResourcePlugin? gameSdkList = GameVersionManager.GetGameSdkZip()?.FirstOrDefault();
                if (gameSdkList != null && GameVersion.TryParse(gameSdkList.version, out GameVersion? sdkVersionResult))
                {
                    GameVersionManager.UpdateSdkVersion(sdkVersionResult);
                }
            #nullable restore
            }

            GameVersionManager.Reinitialize();

            // Write the audio lang list file
            if (IsUseSophon && _sophonVOLanguageList.Count != 0)
            {
                WriteAudioLangListSophon(_sophonVOLanguageList);
            }
            else
            {
                WriteAudioLangList(AssetIndex);
            }
        }

        public virtual async ValueTask<bool> IsPreloadCompleted(CancellationToken token)
        {
            // If the game uses sophon download method, then check directly for the status
            if (IsUseSophon)
            {
                return _isSophonPreloadCompleted;
            }

            // Get the latest package list and await
            await GetLatestPackageList(AssetIndex, GameInstallStateEnum.InstalledHavePreload, true);
            // Get the total size of the packages
            await GetPackagesRemoteSize(AssetIndex, token);
            long totalPackageSize = AssetIndex!.Sum(x => x!.Size);

            // Get the sum of the total size of the single or segmented packages
            return AssetIndex.Sum(asset =>
                                   {
                                       // Check if the package is segmented
                                       if (asset!.Segments != null && asset.Segments.Count != 0)
                                       {
                                           // Get the sum of the total size/length for each of its streams
                                           return asset.Segments.Sum(segment =>
                                                                     {
                                                                         // Check if the read stream exist
                                                                         if (segment!
                                                                            .IsReadStreamExist(DownloadThreadCount))
                                                                         {
                                                                             // Return the size/length of the chunk stream
                                                                             return segment
                                                                                .GetStreamLength(DownloadThreadCount);
                                                                         }

                                                                         // If not, then return 0
                                                                         return 0;
                                                                     });
                                       }

                                       // If segment is none, check if the single stream exist
                                       return asset.IsReadStreamExist(DownloadThreadCount) ?
                                           // If yes, then return the size of the single stream
                                           asset.GetStreamLength(DownloadThreadCount) :
                                           // If neither of both exist, then return 0
                                           0;
                                   }) == totalPackageSize; // Then compare if the total package size is equal

            // Note:
            // x.GetReadStream() will check if the single package/zip exist.
            // So checking the fully downloaded single package is unnecessary.
        }

        public async ValueTask<bool> MoveGameLocation()
        {
            // Get the Game folder
            string GameFolder = ConverterTool.NormalizePath(GamePath);

            // Initialize and run the FileMigration utility
            int migrationOptionReturn = await PerformMigrationOption(GameFolder, MigrateFromLauncherType.Unknown, true);
            // If all the operation is complete, then return true as completed
            return migrationOptionReturn != -1;
        }

        public async ValueTask<bool> UninstallGame()
        {
            // Get the Game folder
            string GameFolder = ConverterTool.NormalizePath(GamePath)!;

            // Get translated fullname
            string translatedGameTitle =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(GameVersionManager!.GamePreset!.GameName!,
                                                                        Lang!._GameClientTitles!)!;
            string translatedGameRegion =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(GameVersionManager!.GamePreset!.ZoneName!,
                                                                        Lang!._GameClientRegions!)!;
            string translatedFullName = $"{translatedGameTitle} - {translatedGameRegion}";

            // Check if the dialog result is Okay (Primary). If not, then return false
            ContentDialogResult DialogResult = await Dialog_UninstallGame(ParentUI!, GameFolder, translatedFullName);
            if (DialogResult != ContentDialogResult.Primary)
            {
                return false;
            }

            try
            {
            #nullable enable
                // Assign UninstallProperty from each overrides
                _uninstallGameProperty ??= AssignUninstallFolders();

                //Preparing paths
                string _DataFolderFullPath = Path.Combine(GameFolder, _uninstallGameProperty?.gameDataFolderName ?? "");

                string[]? foldersToKeepInDataFullPath;
                if (_uninstallGameProperty?.foldersToKeepInData != null &&
                    _uninstallGameProperty?.foldersToKeepInData.Length != 0)
                {
                    foldersToKeepInDataFullPath = new string[_uninstallGameProperty?.foldersToKeepInData?.Length ?? 0];
                    for (int i = 0; i < (_uninstallGameProperty?.foldersToKeepInData?.Length ?? 0); i++)
                    {
                        // ReSharper disable once AssignNullToNotNullAttribute
                        foldersToKeepInDataFullPath[i] =
                            Path.Combine(_DataFolderFullPath, _uninstallGameProperty?.foldersToKeepInData?[i] ?? "");
                    }
                }
                else
                {
                    foldersToKeepInDataFullPath = [];
                }

            #pragma warning disable CS8604 // Possible null reference argument.
                LogWriteLine($"Uninstalling game: {GameVersionManager.GameType} - region: {GameVersionManager.GamePreset.ZoneName ?? string.Empty}\r\n" +
                             $"  GameFolder          : {GameFolder}\r\n" +
                             $"  gameDataFolderName  : {_uninstallGameProperty?.gameDataFolderName}\r\n" +
                             $"  foldersToDelete     : {string.Join(", ", _uninstallGameProperty?.foldersToDelete)}\r\n" +
                             $"  filesToDelete       : {string.Join(", ", _uninstallGameProperty?.filesToDelete)}\r\n" +
                             $"  foldersToKeepInData : {string.Join(", ", _uninstallGameProperty?.foldersToKeepInData)}\r\n" +
                             $"  _Data folder path   : {_DataFolderFullPath}\r\n" +
                             $"  Excluded full paths : {string.Join(", ", foldersToKeepInDataFullPath)}",
                             LogType.Warning, true);
            #pragma warning restore CS8604 // Possible null reference argument.

                // Cleanup Game_Data folder while keeping whatever specified in foldersToKeepInData
                foreach (string folderGameData in Directory.EnumerateFileSystemEntries(_DataFolderFullPath))
                {
                    try
                    {
                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        if (_uninstallGameProperty?.foldersToKeepInData == null ||
                            _uninstallGameProperty?.foldersToKeepInData.Length == 0 ||
                            foldersToKeepInDataFullPath
                               .Contains(folderGameData)) // Skip this entire process if foldersToKeepInData is null
                        {
                            continue;
                        }

                        // Delete directories inside gameDataFolderName that is not included in foldersToKeepInData
                        if (File.GetAttributes(folderGameData).HasFlag(FileAttributes.Directory))
                        {
                            TryUnassignReadOnlyFiles(folderGameData);
                            Directory.Delete(folderGameData, true);
                            LogWriteLine($"Deleted folder: {folderGameData}", LogType.Default, true);
                        }
                        // Delete files inside gameDataFolderName that is not included in foldersToKeepInData
                        else
                        {
                            TryDeleteReadOnlyFile(folderGameData);
                            LogWriteLine($"Deleted file: {folderGameData}", LogType.Default, true);
                        }

                        // ReSharper disable once RedundantJumpStatement
                        continue;
                    }
                    catch (Exception ex)
                    {
                        await SentryHelper.ExceptionHandler_ForLoopAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                        LogWriteLine($"An error occurred while deleting object {folderGameData}\r\n{ex}", LogType.Error,
                                     true);
                    }
                }

                // Check if _DataFolderPath folder empty after cleaning up 
                if (!Directory.EnumerateFileSystemEntries(_DataFolderFullPath).Any())
                {
                    Directory.Delete(_DataFolderFullPath);
                    LogWriteLine($"Deleted empty game folder: {_DataFolderFullPath}", LogType.Default, true);
                }

                // Cleanup any folders in foldersToDelete
                foreach (string folderNames in Directory.EnumerateDirectories(GameFolder))
                {
                    if (_uninstallGameProperty?.foldersToDelete.Length == 0 ||
                        (!(_uninstallGameProperty?.foldersToDelete.Contains(Path.GetFileName(folderNames)) ?? false)))
                    {
                        continue;
                    }

                    try
                    {
                        Directory.Delete(folderNames, true);
                        LogWriteLine($"Deleted {folderNames}", LogType.Default, true);
                    }
                    catch (Exception ex)
                    {
                        await SentryHelper.ExceptionHandler_ForLoopAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                        LogWriteLine($"An error occurred while deleting folder {folderNames}\r\n{ex}",
                                     LogType.Error, true);
                    }
                }

                // Cleanup any files in filesToDelete
                foreach (string fileNames in Directory.EnumerateFiles(GameFolder))
                {
                    if ((_uninstallGameProperty?.filesToDelete.Length == 0 ||
                         (!(_uninstallGameProperty?.filesToDelete.Contains(Path.GetFileName(fileNames)) ?? false))) &&
                        (_uninstallGameProperty?.filesToDelete.Length == 0 ||
                         (!(_uninstallGameProperty?.filesToDelete.Any(pattern =>
                                                                          Regex.IsMatch(Path.GetFileName(fileNames),
                                                                                   pattern,
                                                                                   RegexOptions.Compiled |
                                                                                   RegexOptions.NonBacktracking
                                                                              )) ?? false))))
                    {
                        continue;
                    }

                    TryDeleteReadOnlyFile(fileNames);
                    LogWriteLine($"Deleted {fileNames}", LogType.Default, true);
                }

                // Cleanup Game App Data
                string appDataPath = GameVersionManager.GameDirAppDataPath;
                try
                {
                    if (Directory.Exists(appDataPath))
                        Directory.Delete(appDataPath, true);

                    LogWriteLine($"Deleted {appDataPath}", LogType.Default, true);
                }
                catch (Exception ex)
                {
                    await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                    LogWriteLine($"An error occurred while deleting game AppData folder: {GameVersionManager.GameDirAppDataPath}\r\n{ex}",
                                 LogType.Error, true);
                }

                // Remove the entire folder if nothing is there
                if (Directory.Exists(GameFolder) && !Directory.EnumerateFileSystemEntries(GameFolder).Any())
                {
                    try
                    {
                        Directory.Delete(GameFolder);
                        LogWriteLine($"Deleted empty game folder: {GameFolder}", LogType.Default, true);
                    }
                    catch (Exception ex)
                    {
                        await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                        LogWriteLine($"An error occurred while deleting empty game folder: {GameFolder}\r\n{ex}",
                                     LogType.Error, true);
                    }
                }
                else
                {
                    LogWriteLine($"Game folder {GameFolder} is not empty, skipping delete root directory...",
                                 LogType.Default, true);
                }
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed while uninstalling game: {GameVersionManager.GameType} - Region: {GameVersionManager.GamePreset.ZoneName}\r\n{ex}",
                             LogType.Error, true);
            }

            GameVersionManager.UpdateGamePath("");
            GameVersionManager.Reinitialize();
            return true;
        #nullable disable
        }

        public void CancelRoutine()
        {
            // Always cancel PreventSleep token when cancelling any installation process
            _gameRepairTool?.CancelRoutine();
            Token.Cancel();
            Flush();
        }

        public virtual async ValueTask<bool> TryShowFailedDeltaPatchState()
        {
            // Get the target and source path
            string GamePath            = GameVersionManager.GameDirPath;
            string GamePathIngredients = GamePath + "_Ingredients";
            // If path doesn't exist, then return false
            if (!Directory.Exists(GamePathIngredients))
            {
                return false;
            }

            LogWriteLine($"Previous failed delta patch has been detected on Game {GameVersionManager.GamePreset.ZoneFullname} ({GamePathIngredients})",
                         LogType.Warning, true);
            // Show action dialog
            switch (await Dialog_PreviousDeltaPatchInstallFailed(ParentUI))
            {
                // If primary button clicked, then move the folder and get back to HomePage
                case ContentDialogResult.Primary:
                    MoveFolderContent(GamePathIngredients, GamePath);
                    MainFrameChanger.ChangeMainFrame(typeof(HomePage));
                    break;
            }

            // Then reinitialize the version manager
            GameVersionManager.Reinitialize();
            return true;
        }

        public virtual async ValueTask<bool> TryShowFailedGameConversionState()
        {
            return await Task.FromResult(false);
        }

        public virtual async Task ApplyDeleteFileActionAsync(CancellationToken token = default)
        {
            await Parallel.ForEachAsync(EnumerateFileInfoAsync(token), token, (fileInfo, innerToken) =>
            {
                return new ValueTask(Task.Run(() =>
                {
                    innerToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (!fileInfo.Exists)
                        {
                            return;
                        }

                        fileInfo.IsReadOnly = false;
                        fileInfo.Delete();
                        LogWriteLine($"Deleting old file: {fileInfo.FullName}");
                    }
                    catch (Exception ex)
                    {
                        SentryHelper.ExceptionHandler_ForLoop(ex, SentryHelper.ExceptionType.UnhandledOther);
                        LogWriteLine($"Failed deleting old file: {fileInfo.FullName}\r\n{ex}", LogType.Warning, true);
                    }
                }, innerToken));
            });
            return;

            async IAsyncEnumerable<FileInfo> EnumerateFileInfoAsync([EnumeratorCancellation] CancellationToken innerToken)
            {
                foreach (string path in Directory.EnumerateFiles(GamePath, "deletefiles_*", SearchOption.TopDirectoryOnly))
                {
                    using StreamReader sw = new StreamReader(path,
                                                             new FileStreamOptions
                                                             {
                                                                 Mode   = FileMode.Open,
                                                                 Access = FileAccess.Read,
                                                                 Options = _canDeleteHdiffReference
                                                                     ? FileOptions.DeleteOnClose
                                                                     : FileOptions.None
                                                             });
                    while (!sw.EndOfStream)
                    {
                        string   deleteFile = GetBasePersistentDirectory(GamePath, await sw.ReadLineAsync(innerToken));
                        FileInfo fileInfo   = new FileInfo(deleteFile);
                        yield return fileInfo;
                    }
                }
            }
        }

        private string GetBasePersistentDirectory(string basePath, string input)
        {
            const string streamingAssetsName = "StreamingAssets";

            input = ConverterTool.NormalizePath(input);
            string inStreamingAssetsPath = Path.Combine(basePath, input);

            int baseStreamingAssetsIndex = input
                .LastIndexOf(streamingAssetsName, StringComparison.OrdinalIgnoreCase);
            if (baseStreamingAssetsIndex <= 0)
            {
                return inStreamingAssetsPath;
            }

            string inputTrimmed = input.AsSpan()[(baseStreamingAssetsIndex + streamingAssetsName.Length + 1)..]
                                       .ToString();
            string inPersistentPath = Path.Combine(basePath, _gamePersistentFolderBasePath, inputTrimmed);

            return File.Exists(inPersistentPath) ? inPersistentPath : inStreamingAssetsPath;
        }

        private async Task FileHdiffPatcherInner(string patchPath, string sourceBasePath, string destPath, CancellationToken token)
        {
            HDiffPatch patcher = new HDiffPatch();
            patcher.Initialize(patchPath);
            token.ThrowIfCancellationRequested();

            Task task = Task.Run(() =>
            {
                try
                {
                    patcher.Patch(sourceBasePath, destPath, true, token, false, true);
                    File.Move(destPath, sourceBasePath, true);
                }
                catch (InvalidDataException ex) when (!token.IsCancellationRequested)
                {
                    // ignored
                    // Get the base and new target file size
                    long newFileSize = HDiffPatch.GetHDiffNewSize(patchPath);
                    FileInfo fileInfo = new FileInfo(sourceBasePath);
                    long refFileSize = fileInfo.Exists ? fileInfo.Length : 0;

                    // Check if the throw happened for different file, then rethrow
                    if (newFileSize != refFileSize)
                        throw;

                    // Otherwise, log the error
                    SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                    LogWriteLine($"New: {newFileSize} == Ref: {refFileSize}. File is already new. Skipping! {sourceBasePath}", LogType.Warning, true);
                }
            }, token);
            await task;

            if (task.Exception != null)
                throw task.Exception;
        }

        protected virtual async Task<List<HDiffMapEntry>> GetHDiffMapEntryList(string gameDir)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(gameDir);
            if (!directoryInfo.Exists)
            {
                throw new DirectoryNotFoundException($"[InstallManagerBase::GetHDiffMapEntryList] Game directory: {gameDir} doesn't exist!");
            }

            List<HDiffMapEntry>               hDiffMapEntries  = [];
            Dictionary<string, HDiffMapEntry> sourcePathsOnMap = [];
            foreach (FileInfo hdiffMapFile in directoryInfo.EnumerateFiles("*hdiffmap*.json", SearchOption.TopDirectoryOnly)
                                                           .EnumerateNoReadOnly())
            {
                await using FileStream hdiffMapStream = hdiffMapFile.Open(new FileStreamOptions
                {
                    Mode    = FileMode.Open,
                    Access  = FileAccess.Read,
                    Options = FileOptions.DeleteOnClose
                });
#nullable enable
                HDiffMap? currentDeserialize = await JsonSerializer.DeserializeAsync(hdiffMapStream, HDiffMapEntryJsonContext.Default.HDiffMap, Token.Token);

                if (currentDeserialize?.Entries != null)
                {
                    hDiffMapEntries.AddRange(currentDeserialize.Entries);
                }
#nullable restore
            }

            if (hDiffMapEntries.Count == 0)
            {
                return hDiffMapEntries;
            }

            foreach (HDiffMapEntry sourceFile in hDiffMapEntries)
            {
                _ = sourcePathsOnMap.TryAdd(sourceFile.SourceFileName, sourceFile);
            }

            foreach (FileInfo deletedFileMap in directoryInfo.EnumerateFiles("*deletefiles*.txt", SearchOption.TopDirectoryOnly)
                                                             .EnumerateNoReadOnly())
            {
                using TextReader deletedFileMapReader = deletedFileMap.OpenText();
                while (await deletedFileMapReader.ReadLineAsync() is { } line)
                {
                    string normalizedPath = line.NormalizePath();
                    if (sourcePathsOnMap.TryGetValue(normalizedPath, out HDiffMapEntry valueEntry))
                    {
                        valueEntry.CanDeleteSource = true;
                    }
                }
            }

            return hDiffMapEntries;
        }

        protected virtual async Task ApplyHDiffMap()
        {
            string gameDir = GamePath;
            List<HDiffMapEntry> hDiffMapEntries = await GetHDiffMapEntryList(gameDir);

            if (hDiffMapEntries.Count == 0)
            {
                return;
            }

            Status.IsIncludePerFileIndicator = false;
            Progress.ProgressAllSizeCurrent  = 0;
            Progress.ProgressAllSizeTotal    = hDiffMapEntries.Sum(entry => entry.TargetFileSize);

            ProgressAllCountTotal = 1;
            ProgressAllCountFound = hDiffMapEntries.Count;

            HDiffPatch.LogVerbosity   =  Verbosity.Verbose;
            EventListener.LoggerEvent += EventListener_PatchLogEvent;
            EventListener.PatchEvent  += EventListener_PatchEvent;

            try
            {
                Task parallelTask = Parallel.ForEachAsync(hDiffMapEntries, new ParallelOptions
                {
                    MaxDegreeOfParallelism = ThreadCount,
                    CancellationToken      = Token.Token
                },
                async (entry, ctx) =>
                {
                    Status.ActivityStatus =
                        $"{Lang._Misc.Patching}: {string.Format(Lang._Misc.PerFromTo, ProgressAllCountTotal,
                                                                ProgressAllCountFound)}";

                    bool isSuccess = false;

                    FileInfo sourcePath = new FileInfo(GetBasePersistentDirectory(gameDir, entry.SourceFileName ?? ""))
                                             .EnsureNoReadOnly(out bool isSourceExist);
                    string sourcePathDir = sourcePath.DirectoryName;
                    FileInfo patchPath = new FileInfo(Path.Combine(gameDir, entry.PatchFileName ?? ""))
                                             .EnsureNoReadOnly(out bool isPatchExist);
                    string targetPathBasedOnSource = Path.Combine(sourcePathDir ?? "", Path.GetFileName(entry.TargetFileName ?? ""));
                    FileInfo targetPath = new FileInfo(targetPathBasedOnSource)
                                             .EnsureCreationOfDirectory()
                                             .EnsureNoReadOnly();
                    FileInfo targetPathTemp = new FileInfo(targetPath + "_tmp")
                                             .EnsureNoReadOnly();

                    try
                    {
                        if (string.IsNullOrEmpty(entry.SourceFileName))
                        {
                            ForceUpdateProgress(entry);
                            return;
                        }

                        if (!isPatchExist || !isSourceExist)
                        {
                            ForceUpdateProgress(entry);
                            return;
                        }

                        if (isSourceExist && sourcePath.Length != entry.SourceFileSize)
                        {
                            ForceUpdateProgress(entry);
                            LogWriteLine($"[InstallManagerBase::ApplyHDiffMap] Source file size mismatch: {sourcePath.FullName} ({sourcePath.Length} != {entry.SourceFileSize})", LogType.Warning, true);
                            return;
                        }

                        byte[] sourceLocalHash = entry.SourceMD5Hash?.Length switch
                                                 {
                                                     > 8 and 16 => await GetCryptoHashAsync<MD5>(sourcePath, null, false, true, Token.Token),
                                                     > 4 => await GetHashAsync<XxHash64>(sourcePath, false, true, Token.Token),
                                                     _ => await GetHashAsync<Crc32>(sourcePath, false, true, Token.Token)
                                                 };

                        if (!sourceLocalHash.AsSpan().SequenceEqual(entry.SourceMD5Hash))
                        {
                            ForceUpdateProgress(entry);
                            LogWriteLine("[InstallManagerBase::ApplyHDiffMap] Source file or patch has mismatch hash!\r\n"
                                + $"Source file: {sourcePath.FullName}\r\nLocal Hash: {HexTool.BytesToHexUnsafe(sourceLocalHash)}\r\nRemote Hash: {HexTool.BytesToHexUnsafe(entry.SourceMD5Hash)}",
                                LogType.Warning,
                                true);
                            return;
                        }

                        LogWriteLine($"Patching file {entry.SourceFileName} to {entry.TargetFileName}...", LogType.Default, true);
                        UpdateProgressBase();
                        UpdateStatus();

                        await Task.Factory.StartNew(state =>
                        {
                            CancellationToken thisInnerCtx = (CancellationToken)state;
                            try
                            {
                                thisInnerCtx.ThrowIfCancellationRequested();
                                HDiffPatch patcher = new HDiffPatch();
                                patcher.Initialize(patchPath.FullName);
                                patcher.Patch(sourcePath.FullName, targetPathTemp.FullName, true, thisInnerCtx, false, true);
                                isSuccess = true;
                            }
                            catch (InvalidDataException ex) when (!thisInnerCtx.IsCancellationRequested)
                            {
                                // ignored
                                // Get the base and new target file size
                                long newFileSize = HDiffPatch.GetHDiffNewSize(patchPath.FullName);
                                long refFileSize = targetPath.Exists ? targetPath.Length : 0;

                                // Check if the throw happened for different file, then rethrow
                                if (newFileSize != refFileSize)
                                    throw;

                                // Otherwise, log the error
                                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                                LogWriteLine($"New: {newFileSize} == Ref: {refFileSize}. File is already new. Skipping! {targetPath.FullName}", LogType.Warning, true);
                            }
                        },
                        ctx,
                        ctx,
                        TaskCreationOptions.DenyChildAttach,
                        TaskScheduler.Default);
                    }
                    catch (OperationCanceledException)
                    {
                        await Token.CancelAsync();
                        LogWriteLine("Cancelling patching process!...", LogType.Warning, true);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        await SentryHelper.ExceptionHandler_ForLoopAsync(ex);
                        LogWriteLine(
                            $"Error while patching file: {entry.SourceFileName ?? string.Empty} to: {entry.TargetFileName ?? string.Empty}. Skipping!\r\n{ex}",
                            LogType.Warning,
                            true);

                        ForceUpdateProgress(entry);
                    }
                    finally
                    {
                        Interlocked.Increment(ref ProgressAllCountTotal);
                        if (!string.IsNullOrEmpty(entry.PatchFileName))
                        {
                            _ = patchPath.TryDeleteFile();
                        }

                        if (isSuccess && entry.CanDeleteSource)
                        {
                            sourcePath.Refresh();
                            _ = sourcePath.TryDeleteFile();
                        }

                        targetPathTemp.Refresh();
                        if (targetPathTemp.Exists)
                        {
                            _ = targetPathTemp.TryMoveTo(targetPath);
                        }
                    }
                });

                await parallelTask;
            }
            finally
            {
                EventListener.LoggerEvent -= EventListener_PatchLogEvent;
                EventListener.PatchEvent -= EventListener_PatchEvent;
            }

            return;

            void ForceUpdateProgress(HDiffMapEntry entry)
            {
                Interlocked.Add(ref Progress.ProgressAllSizeCurrent, entry.TargetFileSize);
                Progress.ProgressAllPercentage = ConverterTool.ToPercentage(Progress.ProgressAllSizeTotal, Progress.ProgressAllSizeCurrent);
                Progress.ProgressAllSpeed = CalculateSpeed(entry.TargetFileSize);
                Progress.ProgressAllTimeLeft = ConverterTool.ToTimeSpanRemain(Progress.ProgressAllSizeTotal, Progress.ProgressAllSizeCurrent, Progress.ProgressAllSpeed);

                UpdateProgress();
            }
        }

        public virtual async Task ApplyHdiffListPatch()
        {
            // As per January 2025, the HDiff patcher uses the new HDiffMap method.
            // Run the HDiffMap method first before applying the legacy HDiffList patch.
            await ApplyHDiffMap();

            List<PkgVersionProperties> hdiffEntry = TryGetHDiffList();
            Status.IsIncludePerFileIndicator = false;

            Progress.ProgressAllSizeTotal   = hdiffEntry.Sum(x => x.fileSize);
            Progress.ProgressAllSizeCurrent = 0;

            ProgressAllCountTotal = 1;
            ProgressAllCountFound = hdiffEntry.Count;

            HDiffPatch.LogVerbosity = Verbosity.Verbose;
            EventListener.LoggerEvent   += EventListener_PatchLogEvent;
            EventListener.PatchEvent    += EventListener_PatchEvent;

            Task parallelTask = Parallel.ForEachAsync(hdiffEntry, new ParallelOptions
            {
                CancellationToken = Token.Token,
                MaxDegreeOfParallelism = ThreadCount
            }, async (entry, innerToken) =>
            {
                Status.ActivityStatus =
                    $"{Lang._Misc.Patching}: {string.Format(Lang._Misc.PerFromTo, ProgressAllCountTotal,
                                                            ProgressAllCountFound)}";

                string patchBasePath  = Path.Combine(GamePath, ConverterTool.NormalizePath(entry.remoteName));
                string sourceBasePath = GetBasePersistentDirectory(GamePath, entry.remoteName);
                string patchPath      = patchBasePath + ".hdiff";
                string destPath       = sourceBasePath + "_tmp";

                try
                {
                    if (File.Exists(sourceBasePath) && File.Exists(patchPath))
                    {
                        LogWriteLine($"Patching file {entry.remoteName}...", LogType.Default, true);
                        UpdateProgressBase();
                        UpdateStatus();

                        await FileHdiffPatcherInner(patchPath, sourceBasePath, destPath, innerToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    await Token.CancelAsync();
                    LogWriteLine("Cancelling patching process!...", LogType.Warning, true);
                    throw;
                }
                catch (Exception ex)
                {
                    await SentryHelper.ExceptionHandler_ForLoopAsync(ex);
                    LogWriteLine($"Error while patching file: {entry.remoteName}. Skipping!\r\n{ex}", LogType.Warning,
                                 true);

                    lock (Progress)
                    {
                        Progress.ProgressAllSizeCurrent += entry.fileSize;
                        Progress.ProgressAllPercentage = ConverterTool.ToPercentage(Progress.ProgressAllSizeTotal, Progress.ProgressAllSizeCurrent);
                        Progress.ProgressAllSpeed = CalculateSpeed(entry.fileSize);
                        Progress.ProgressAllTimeLeft = ConverterTool.ToTimeSpanRemain(Progress.ProgressAllSizeTotal, Progress.ProgressAllSizeCurrent, Progress.ProgressAllSpeed);
                    }

                    UpdateProgress();
                }
                finally
                {
                    try
                    {
                        if (File.Exists(destPath))
                        {
                            File.Delete(destPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        await SentryHelper.ExceptionHandler_ForLoopAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                        LogWriteLine($"Failed while trying to delete temporary file: {destPath}, skipping!\r\n{ex}",
                                     LogType.Warning, true);
                    }

                    Interlocked.Increment(ref ProgressAllCountTotal);
                    FileInfo patchFile = new FileInfo(patchPath).EnsureNoReadOnly();
                    _ = patchFile.TryDeleteFile();
                }
                Token.Token.ThrowIfCancellationRequested();
            });

            try
            {
                await parallelTask;
            }
            catch (AggregateException ex)
            {
                var innerExceptionsFirst = ex.Flatten().InnerExceptions.First();
                await SentryHelper.ExceptionHandlerAsync(innerExceptionsFirst, SentryHelper.ExceptionType.UnhandledOther);
                throw innerExceptionsFirst;
            }
            finally
            {
                EventListener.LoggerEvent   -= EventListener_PatchLogEvent;
                EventListener.PatchEvent    -= EventListener_PatchEvent;
            }
        }

        private void EventListener_PatchEvent(object sender, PatchEvent e)
        {
            Interlocked.Add(ref Progress.ProgressAllSizeCurrent, e.Read);
            double speed = CalculateSpeed(e.Read);

            if (!CheckIfNeedRefreshStopwatch())
            {
                return;
            }

            lock (Progress)
            {
                Progress.ProgressAllSpeed      = speed;
                Progress.ProgressAllPercentage = ConverterTool.ToPercentage(Progress.ProgressAllSizeTotal, Progress.ProgressAllSizeCurrent);
                Progress.ProgressAllTimeLeft   = ConverterTool.ToTimeSpanRemain(Progress.ProgressAllSizeTotal, Progress.ProgressAllSizeCurrent, Progress.ProgressAllSpeed);
            }
            UpdateProgress();
        }

        private void EventListener_PatchLogEvent(object sender, LoggerEvent e)
        {
            if (HDiffPatch.LogVerbosity == Verbosity.Quiet
                || (HDiffPatch.LogVerbosity == Verbosity.Debug
                    && !(e.LogLevel == Verbosity.Debug ||
                         e.LogLevel == Verbosity.Verbose ||
                         e.LogLevel == Verbosity.Info))
                || (HDiffPatch.LogVerbosity == Verbosity.Verbose
                    && !(e.LogLevel == Verbosity.Verbose ||
                         e.LogLevel == Verbosity.Info))
                || (HDiffPatch.LogVerbosity == Verbosity.Info
                    && e.LogLevel != Verbosity.Info))
            {
                return;
            }

            LogType type = e.LogLevel switch
                           {
                               Verbosity.Verbose => LogType.Debug,
                               Verbosity.Debug => LogType.Debug,
                               _ => LogType.Default
                           };

            LogWriteLine(e.Message, type, true);
        }

        public virtual List<PkgVersionProperties> TryGetHDiffList()
        {
            List<PkgVersionProperties> _out = [];
            foreach (string listFile in Directory.EnumerateFiles(GamePath, "*hdifffiles*",
                                                                 SearchOption.TopDirectoryOnly))
            {
                LogWriteLine($"hdiff File list path: {listFile}", LogType.Default, true);

                try
                {
                    using StreamReader listReader = new StreamReader(listFile,
                                                                     new FileStreamOptions
                                                                     {
                                                                         Mode   = FileMode.Open,
                                                                         Access = FileAccess.Read,
                                                                         Options = _canDeleteHdiffReference
                                                                             ? FileOptions.DeleteOnClose
                                                                             : FileOptions.None
                                                                     });
                    while (!listReader.EndOfStream)
                    {
                        string currentLine = listReader.ReadLine();
                        var    prop = currentLine?.Deserialize(CoreLibraryJsonContext.Default.PkgVersionProperties);

                        if (prop == null)
                        {
                            continue;
                        }

                        string filePath = Path.Combine(GamePath, prop.remoteName + ".hdiff");
                        if (!File.Exists(filePath))
                        {
                            continue;
                        }

                        try
                        {
                            prop.fileSize = HDiffPatch.GetHDiffNewSize(filePath);
                            LogWriteLine($"hdiff entry: {prop.remoteName}", LogType.Default, true);

                            _out.Add(prop);
                        }
                        catch (Exception ex)
                        {
                            SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                            LogWriteLine($"Error while parsing the size of the new file inside of diff: {filePath}\r\n{ex}",
                                         LogType.Warning, true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                    LogWriteLine($"Failed while trying to read hdiff file list: {listFile}\r\n{ex}", LogType.Warning,
                                 true);
                }
            }

            return _out;
        }

#nullable enable
        protected virtual string GetLanguageLocaleCodeByID(int id)
        {
            return id switch
                   {
                       0 => "zh-cn",
                       1 => "en-us",
                       2 => "ja-jp",
                       3 => "ko-kr",
                       _ => throw new KeyNotFoundException($"ID: {id} is not supported!")
                   };
        }

        protected virtual int GetIDByLanguageLocaleCode([NotNull] string? localeCode)
        {
            return localeCode switch
                   {
                       "zh-cn" => 0,
                       "en-us" => 1,
                       "ja-jp" => 2,
                       "ko-kr" => 3,
                       _ => throw new KeyNotFoundException($"Locale code: {localeCode} is not supported!")
                   };
        }

        protected virtual string GetLanguageStringByLocaleCode([NotNull] string? localeCode)
        {
            return localeCode switch
                   {
                       "zh-cn" => "Chinese",
                       "en-us" => "English(US)",
                       "ja-jp" => "Japanese",
                       "ko-kr" => "Korean",
                       _ => throw new KeyNotFoundException($"Locale code: {localeCode} is not supported!")
                   };
        }

        protected virtual string GetLanguageStringByID(int id)
        {
            return id switch
                   {
                       0 => "Chinese",
                       1 => "English(US)",
                       2 => "Japanese",
                       3 => "Korean",
                       _ => throw new KeyNotFoundException($"ID: {id} is not supported!")
                   };
        }

        protected virtual string? GetLanguageLocaleCodeByLanguageString([NotNullIfNotNull(nameof(langString))] string? langString, bool throwIfInvalid = true)
        {
            return langString switch
                   {
                       "Chinese" => "zh-cn",
                       "English" => "en-us",
                       "English(US)" => "en-us",
                       "Korean" => "ko-kr",
                       "Japanese" => "ja-jp",
                       _ => throwIfInvalid
                           ? throw new NotSupportedException($"This language string: {langString} is not supported")
                           : null
                   };
        }

        protected virtual string? GetLanguageDisplayByLocaleCode([NotNullIfNotNull(nameof(localeCode))] string? localeCode, bool throwIfInvalid = true)
        {
            return localeCode switch
                   {
                       "zh-cn" => Lang._Misc.LangNameCN,
                       "en-us" => Lang._Misc.LangNameENUS,
                       "ko-kr" => Lang._Misc.LangNameKR,
                       "ja-jp" => Lang._Misc.LangNameJP,
                       _ => throwIfInvalid
                           ? throw new NotSupportedException($"Locale code: {localeCode} is not supported!")
                           : null
                   };
        }

        protected virtual Dictionary<string, string> GetLanguageDisplayDictFromVoicePackList(
            List<RegionResourceVersion> voicePacks)
        {
            Dictionary<string, string> returnDict = new Dictionary<string, string>();
            foreach (RegionResourceVersion Entry in voicePacks)
            {
                // Check the lang ID and add the translation of the language to the list
                string? languageDisplay = GetLanguageDisplayByLocaleCode(Entry.language, false);
                if (string.IsNullOrEmpty(languageDisplay))
                {
                    continue;
                }

                if (Entry.language != null)
                {
                    returnDict.Add(Entry.language, languageDisplay);
                }
            }

            return returnDict;
        }

        protected virtual void RearrangeLegacyPackageLocaleOrder(RegionResourceVersion? regionResource)
        {
            // Rearrange the region resource list order based on matching field for the locale
            RearrangeDataListLocaleOrder(regionResource?.voice_packs, x => x.language);
        }

        protected virtual void RearrangeDataListLocaleOrder<T>(List<T>? assetDataList, Func<T, string?> matchingFieldPredicate)
        {
            // If the asset list is null or empty, return
            if (assetDataList == null || assetDataList.Count == 0)
            {
                return;
            }

            // Get ordered locale string
            string[] localeStringOrder = _gameVoiceLanguageLocaleIdOrdered;

            // Separate non-locale and locale manifest list
            List<T> manifestListMain = assetDataList
                .Where(x => !IsValidLocaleCode(matchingFieldPredicate(x)))
                .ToList();
            List<T> manifestListLocale = assetDataList
                .Where(x => IsValidLocaleCode(matchingFieldPredicate(x)))
                .ToList();

            // SLOW: Order the locale manifest list by the localeStringOrder
            for (int i = 0; i < localeStringOrder.Length; i++)
            {
                var localeFound = manifestListLocale.FirstOrDefault(x => matchingFieldPredicate(x)?.Equals(localeStringOrder[i], StringComparison.OrdinalIgnoreCase) ?? false);
                if (localeFound == null)
                {
                    continue;
                }

                // Move from main to locale
                manifestListMain.Add(localeFound);
                manifestListLocale.Remove(localeFound);
            }

            // Add the rest of the unknown locale if exist
            if (manifestListLocale.Count != 0)
            {
                manifestListMain.AddRange(manifestListLocale);
            }

            // Rearrange by cleaning the list and re-add the sorted list
            assetDataList.Clear();
            assetDataList.AddRange(manifestListMain);
        }

        protected virtual bool TryGetVoiceOverResourceByLocaleCode(List<RegionResourceVersion>? verResList,
                                                                   string localeCode, [NotNullWhen(true)] out RegionResourceVersion? outRes)
        {
            outRes = null;
            // Sanitation check: Check if the localeId argument is null or have no content
            if (verResList == null || verResList.Count == 0)
            {
                return false;
            }

            // Sanitation check: Check if the localeCode argument is a valid locale code format
            if (!IsValidLocaleCode(localeCode))
            {
                return false;
            }

            // Try find the asset and if it's null, return false
            outRes = verResList.FirstOrDefault(x => x.language?.Equals(localeCode, StringComparison.OrdinalIgnoreCase) ?? false);
            // Otherwise, check the nullability and return
            return outRes != null;
        }
#nullable restore

        protected virtual bool IsValidLocaleCode(ReadOnlySpan<char> localeCode)
        {
            // If it's empty, return false
            if (localeCode.IsEmpty)
            {
                return false;
            }

            // Alloc to stack and try start to split
            Span<Range> rangeSpan = stackalloc Range[2];
            int         rangeLen  = localeCode.Split(rangeSpan, '-', StringSplitOptions.RemoveEmptyEntries);

            // Try split it again with '_'
            if (rangeLen != 2)
            {
                rangeLen = localeCode.Split(rangeSpan, '_', StringSplitOptions.RemoveEmptyEntries);
            }

            // If the split still have no result, then return false
            if (rangeLen != 2)
            {
                return false;
            }

            // Do check on the locale identifier
            int index = 0;
            GoCheck:
            {
                if (rangeSpan[index].End.Value - rangeSpan[index].Start.Value != 2)
                {
                    return false;
                }

                ++index;
                if (index < rangeLen)
                {
                    goto GoCheck;
                }
            }

            // If all passed, return true
            return true;
        }

        protected virtual void WriteAudioLangList(List<GameInstallPackage> gamePackage)
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
            foreach (GameInstallPackage package in
                     AssetIndex.Where(x => x.PackageType == GameInstallPackageType.Audio))
            {
                string langString = GetLanguageStringByLocaleCode(package.LanguageID);
                if (!langList.Contains(langString, StringComparer.OrdinalIgnoreCase))
                {
                    langList.Add(langString);
                }
            }

            // Create the audio lang list file
            using StreamWriter sw = new StreamWriter(_gameAudioLangListPathStatic,
                                                     new FileStreamOptions
                                                         { Mode = FileMode.Create, Access = FileAccess.Write });
            // Iterate the package list
            foreach (string langString in langList)
            {
                // Write the language string as per ID
                sw.WriteLine(langString);
            }
        }
        #endregion

        #region Private Methods - GetInstallationPath
        private async ValueTask<int> CheckExistingBHI3LInstallation(bool isHasOnlyMigrateOption = false)
        {
            string pathOnBHi3L = "";
            if (!TryGetExistingBHI3LPath(ref pathOnBHi3L))
            {
                return 1;
            }

            // If the "Use current directory" option is chosen (migrationOptionReturn == 1), then proceed to another routine.
            // If not, then return the migrationOptionReturn value.
            int migrationOptionReturn = await PerformMigrationOption(pathOnBHi3L, MigrateFromLauncherType.BetterHi3Launcher, false, isHasOnlyMigrateOption);

            // If it's on "Migration option only" mode and it returns "Okay"/0, then update the game
            // path and return 0
            if (isHasOnlyMigrateOption && migrationOptionReturn == 1)
            {
                GameVersionManager.UpdateGamePath(GameVersionManager.GamePreset.ActualGameDataLocation,
                                                   false);
                return 0;
            }

            // If the option is applying to the current directory
            if (migrationOptionReturn == 0)
            {
                GameVersionManager.UpdateGamePath(pathOnBHi3L, false);
                return 0;
            }

            return migrationOptionReturn != 0 ? migrationOptionReturn :
                // Return 1 to continue to another check
                1;
        }

        private async ValueTask<int> CheckExistingOfficialInstallation(bool isHasOnlyMigrateOption = false)
        {
            if (!GameVersionManager.GamePreset.CheckExistingGame())
            {
                return 1;
            }

            // If the "Use current directory" option is chosen (migrationOptionReturn == 1), then proceed to another routine.
            // If not, then return the migrationOptionReturn value.
            int migrationOptionReturn =
                await PerformMigrationOption(GameVersionManager.GamePreset.ActualGameDataLocation,
                                             MigrateFromLauncherType.Official,
                                             false,
                                             isHasOnlyMigrateOption);

            // If it's on "Migration option only" mode and it returns "Okay"/0, then update the game
            // path and return 0
            if (isHasOnlyMigrateOption && migrationOptionReturn == 0)
            {
                GameVersionManager.UpdateGamePath(GameVersionManager.GamePreset.ActualGameDataLocation,
                                                   false);
                return 0;
            }

            if (migrationOptionReturn != 1)
            {
                return migrationOptionReturn;
            }

            switch (await Dialog_ExistingInstallation(ParentUI,
                                                      GameVersionManager.GamePreset.ActualGameDataLocation))
            {
                // If action to migrate was taken, then update the game path (but don't save it to the config file)
                case ContentDialogResult.Primary:
                    GameVersionManager.UpdateGamePath(GameVersionManager.GamePreset.ActualGameDataLocation,
                                                       false);
                    return 0;
                // If action to fresh install was taken, then return 2 (selecting path)
                case ContentDialogResult.Secondary:
                    return 2;
                // If action to cancel was taken, then return -1 (go back)
                case ContentDialogResult.None:
                    return -1;
            }

            // Return 1 to continue to another check
            return 1;
        }

        private async ValueTask<int> CheckExistingSteamInstallation(bool isHasOnlyMigrateOption = false)
        {
            string pathOnSteam = "";
            if (!TryGetExistingSteamPath(ref pathOnSteam))
            {
                return 1;
            }

            // If the "Use current directory" option is chosen (migrationOptionReturn == 1), then proceed to another routine.
            // If not, then return the migrationOptionReturn value.
            int migrationOptionReturn = await PerformMigrationOption(pathOnSteam, MigrateFromLauncherType.Steam, false, isHasOnlyMigrateOption);

            // If it's on "Migration option only" mode and it returns "Okay"/0, then update the game
            // path and return 0
            if (isHasOnlyMigrateOption && migrationOptionReturn == 1)
            {
                GameVersionManager.UpdateGamePath(GameVersionManager.GamePreset.ActualGameDataLocation,
                                                   false);
                return 0;
            }

            // If the option is applying to the current directory
            if (migrationOptionReturn == 0)
            {
                GameVersionManager.UpdateGamePath(pathOnSteam, false);
                await StartSteamMigration();
                GameVersionManager.UpdateGameVersionToLatest(false);
                return 0;
            }

            return migrationOptionReturn != 0 ? migrationOptionReturn :
                // Return 1 to continue to another check
                1;
        }

#nullable enable
        private async Task StartSteamMigration()
        {
            // Get game repair instance and if it's null, then return;
            string? latestGameVersionString = GameVersionManager.GetGameVersionApi()?.VersionString;
            if (string.IsNullOrEmpty(latestGameVersionString))
                return;

            using IRepair? gameRepairInstance = GetGameRepairInstance(latestGameVersionString);
            if (gameRepairInstance == null)
                return;

            // Build the UI
            Grid mainGrid = UIElementExtensions.CreateGrid()
                .WithWidth(590)
                .WithColumns([
                    new GridLength(1, GridUnitType.Star),
                    new GridLength(1, GridUnitType.Auto)
                    ])
                .WithRows([
                    new GridLength(1, GridUnitType.Star),
                    new GridLength(1, GridUnitType.Star),
                    new GridLength(1, GridUnitType.Star),
                    new GridLength(1, GridUnitType.Star),
                    new GridLength(1, GridUnitType.Star)
                    ])
                .WithColumnSpacing(16);

            TextBlock statusActivity = mainGrid.AddElementToGridRowColumn(
                new TextBlock
                    {
                    FontWeight = FontWeights.Medium,
                    FontSize = 18,
                    Text = Lang._InstallMigrateSteam.Step3Title,
                    TextWrapping = TextWrapping.Wrap
                }
                               .WithHorizontalAlignment(HorizontalAlignment.Left),
                0, 0, 0, 2)
                .WithMargin(0, 0, 0, 8);
            TextBlock fileActivityStatus = mainGrid.AddElementToGridRowColumn(
                new TextBlock { Text = "-", TextTrimming = TextTrimming.CharacterEllipsis }
                               .WithHorizontalAlignment(HorizontalAlignment.Left),
                1, 0, 0, 2);
            TextBlock speedStatus = mainGrid.AddElementToGridRowColumn(
                new TextBlock { FontWeight = FontWeights.Bold, Text = Lang._Misc.SpeedPlaceholder, TextTrimming = TextTrimming.CharacterEllipsis }
                               .WithHorizontalAlignment(HorizontalAlignment.Left),
                2, 0, 0, 2);
            TextBlock timeRemainStatus = mainGrid.AddElementToGridRowColumn(
                new TextBlock { FontWeight = FontWeights.Bold, Text = Lang._Misc.TimeRemainHMSFormatPlaceholder, TextTrimming = TextTrimming.CharacterEllipsis }
                               .WithHorizontalAlignment(HorizontalAlignment.Left),
                3, 0, 0, 2);
            TextBlock percentageStatus = mainGrid.AddElementToGridRowColumn(
                new TextBlock { FontWeight = FontWeights.Bold, Text = "0.00%" }
                               .WithHorizontalAlignment(HorizontalAlignment.Right),
                3, 1);
            ProgressBar progressBar = mainGrid.AddElementToGridRowColumn(
                new ProgressBar { IsIndeterminate = true },
                4, 0, 0, 2)
                .WithMargin(0, 16, 0, 0);

            gameRepairInstance.ProgressChanged += StartSteamMigration_ProgressChanged;
            gameRepairInstance.StatusChanged += StartSteamMigration_StatusChanged;
            ContentDialogCollapse contentDialog = new ContentDialogCollapse(ContentDialogTheme.Informational)
            {
                Title = Lang._InstallMigrateSteam.PageTitle,
                Content = mainGrid,
                XamlRoot = ParentUI.XamlRoot,
                CloseButtonText = Lang._Misc.Cancel
            };

            contentDialog.CloseButtonClick += (_, _) =>
            {
                gameRepairInstance.CancelRoutine();
            };

            try
            {
            #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                // This is intentional as the dialog is only to cancel the routine, not waiting for user input.
            #pragma warning disable CA2012
                contentDialog.QueueAndSpawnDialog();
            #pragma warning restore CA2012
            #pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                await gameRepairInstance.StartCheckRoutine();
                statusActivity.Text = Lang._InstallMigrateSteam.Step4Title;
                await gameRepairInstance.StartRepairRoutine();
                contentDialog.Hide();
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                contentDialog.Hide();
                throw;
            }
            finally
            {
                gameRepairInstance.ProgressChanged -= StartSteamMigration_ProgressChanged;
                gameRepairInstance.StatusChanged -= StartSteamMigration_StatusChanged;
            }

            return;

            void StartSteamMigration_ProgressChanged(object? sender, TotalPerFileProgress e)
            {
                Dispatch(() =>
                {
                    progressBar.IsIndeterminate = false;
                    progressBar.Value           = e.ProgressAllPercentage;
                    percentageStatus.Text       = $"{Math.Round(e.ProgressAllPercentage, 2)}%";
                });
            }

            void StartSteamMigration_StatusChanged(object? sender, TotalPerFileStatus e)
            {
                Dispatch(() =>
                {
                    fileActivityStatus.Text = e.ActivityStatus;
                    speedStatus.Text = e.ActivityPerFile;
                    timeRemainStatus.Text = e.ActivityAll;
                });
            }
        }
#nullable restore

        private async ValueTask<int> PerformMigrationOption(string                  pathIfUseExistingSelected,
                                                            MigrateFromLauncherType launcherType,
                                                            bool                    isMoveOperation = false,
                                                            bool                    isHasOnlyMigrateOption = false)
        {
            string launcherName = launcherType switch
                                  {
                                      MigrateFromLauncherType.Official => Lang._Misc.LauncherNameOfficial,
                                      MigrateFromLauncherType.Steam => Lang._Misc.LauncherNameSteam,
                                      MigrateFromLauncherType.BetterHi3Launcher => Lang._Misc.LauncherNameBHI3L,
                                      _ => Lang._Misc.LauncherNameUnknown
                                  };

            if (!isMoveOperation)
            {
                ContentDialogResult dialogResult = await Dialog_MigrationChoiceDialog(
                 ParentUI,
                 pathIfUseExistingSelected,
                 GameVersionManager.GamePreset.GameName,
                 GameVersionManager.GamePreset.ZoneName,
                 launcherName,
                 launcherType,
                 isHasOnlyMigrateOption);

                // If the routine is to use "migration option only" mode and the result
                // is "Okay", then return 0
                if (isHasOnlyMigrateOption && dialogResult == ContentDialogResult.Primary)
                {
                    return 0;
                }

                if (dialogResult == ContentDialogResult.None)
                {
                    return -1; // Cancel the installation
                }

                // This will continue to other routines if non-official migration
                // is detected.
                if (launcherType != MigrateFromLauncherType.Official
                && dialogResult == ContentDialogResult.Primary)
                {
                    return 0;
                }

                // This will use the "No, Keep Install It" option instead of migrating.
                if (launcherType != MigrateFromLauncherType.Official
                && dialogResult == ContentDialogResult.Secondary)
                {
                    return 2;
                }

                if (dialogResult == ContentDialogResult.Primary)
                {
                    return 1; // Use an existing path or continue to another routine
                }
            }

            // If secondary option is selected, then do the directory migration
            string translatedGameFullname = $"{InnerLauncherConfig
               .GetGameTitleRegionTranslationString(GameVersionManager.GamePreset.GameName,
                                                    Lang._GameClientTitles)} - {InnerLauncherConfig
                                                   .GetGameTitleRegionTranslationString(GameVersionManager.GamePreset.ZoneName,
                                                        Lang._GameClientRegions)}";

            FileMigrationProcess migrationProcessTool = await FileMigrationProcess.CreateJob(
             ParentUI,
             string.Format(Lang._Dialogs.MigrateExistingMoveDirectoryTitle, translatedGameFullname),
             pathIfUseExistingSelected);

            // If the migration tool is null (meaning that it's cancelled), then return -1 as cancelled.
            if (migrationProcessTool == null)
            {
                return -1;
            }

            string newDirectoryPath = await migrationProcessTool.StartRoutine();

            // If it's finished, then set the game data location to the new one
            GameVersionManager.UpdateGamePath(newDirectoryPath, false);

            return 0; // Return 0 as completed.
        }

        private bool TryGetExistingSteamPath(ref string OutputPath)
        {
            // If the game preset doesn't have SteamGameID, then return false
            if (GameVersionManager.GamePreset.SteamGameID == null)
            {
                return false;
            }

            // Assign Steam ID
            int steamID = GameVersionManager.GamePreset.SteamGameID ?? 0;

            // Try get the list of Steam Libs and Apps
            List<string> steamLibsList = SteamTool.GetSteamLibs();
            if (steamLibsList == null)
            {
                return false;
            }

            List<AppInfo> steamAppList = SteamTool.GetSteamApps(steamLibsList);
        #nullable enable
            AppInfo? steamAppInfo = steamAppList.FirstOrDefault(x => x.Id == steamID);

            // If the app info is not null, then assign OutputPath to the game path
            if (steamAppInfo == null)
            {
                return false;
            }

            OutputPath = steamAppInfo.GameRoot;
            return true;
        #nullable disable

            // If none of them has it, then return false
        }

        private bool TryGetExistingBHI3LPath(ref string OutputPath)
        {
        #nullable enable
            // If the preset doesn't have BetterHi3Launcher registry ver info, then return false
            if (GameVersionManager.GamePreset.BetterHi3LauncherVerInfoReg == null)
            {
                return false;
            }

            // Try open BHI3L registry key
            // If the key doesn't exist, then return false
            RegistryKey? Key = Registry.CurrentUser.OpenSubKey(@"Software\Bp\Better HI3 Launcher");

            // Try get the key value
            // If the key also doesn't exist, then return false
            byte[]? keyValue = (byte[]?)Key?.GetValue(GameVersionManager.GamePreset.BetterHi3LauncherVerInfoReg);
            if (keyValue == null)
            {
                return false;
            }

            BHI3LInfo? config;
            string     value = "";
            try
            {
                // Try parsing the config
                value  = Encoding.UTF8.GetString(keyValue);
                config = value.Deserialize(BHI3LInfoJsonContext.Default.BHI3LInfo);
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex);
                LogWriteLine($"Registry Value {GameVersionManager.GamePreset.BetterHi3LauncherVerInfoReg}:\r\n{value}\r\n\r\nException:\r\n{ex}",
                             LogType.Error, true);
                return false;
            }

            // Assign OutputPath to the path provided by the config
            if (config == null || !config.game_info.installed
                               || string.IsNullOrEmpty(config.game_info.install_path))
            {
                return false;
            }

            FileInfo execPath = new FileInfo(Path.Combine(config.game_info.install_path,
                                                          GameVersionManager.GamePreset.GameExecutableName!));
            OutputPath = config.game_info.install_path;
            // If all of those not passed, then return false
            return execPath is { Exists: true, Length: > 1 >> 16 };

        }

        private async Task<string?> AskGameFolderDialog(Func<string, string>? checkExistingGameDelegate = null)
        {
            // Set initial folder variable as empty
            string folder = "";

            // Do loop and break if choice is already been done
            bool isChoosen = false;
            while (!isChoosen)
            {
                // Show dialog
                switch (await Dialog_InstallationLocation(ParentUI))
                {
                    // If primary button is clicked, then set folder with the default path
                    case ContentDialogResult.Primary:
                        if (GameVersionManager.GamePreset.ProfileName != null &&
                            GameVersionManager.GamePreset.GameDirectoryName != null)
                        {
                            folder = Path.Combine(LauncherConfig.AppGameFolder,
                                                  GameVersionManager.GamePreset.ProfileName,
                                                  GameVersionManager.GamePreset.GameDirectoryName);
                        }

                        isChoosen = true;
                        break;
                    // If secondary, then show folder picker dialog to choose the folder
                    case ContentDialogResult.Secondary:
                        folder = await FileDialogHelper.GetRestrictedFolderPathDialog(Lang._Dialogs.FolderDialogTitle1, checkExistingGameDelegate);
                        isChoosen = !string.IsNullOrEmpty(folder);
                        break;
                    case ContentDialogResult.None:
                        return null;
                }
            }

            return folder;
        }
#nullable restore

        private async Task GetLatestPackageList(List<GameInstallPackage> packageList, GameInstallStateEnum gameState,
                                                bool                     usePreload)
        {
            // Clean the package list
            packageList.Clear();

            // If the package is not in plugin update state, then skip adding the package
            if (gameState != GameInstallStateEnum.InstalledHavePlugin)
            {
                // Iterate the package resource version and add it into packageList
                foreach (var asset in ((usePreload
                             ? GameVersionManager.GetGamePreloadZip()
                             : GameVersionManager.GetGameLatestZip(gameState))
                                ?? throw new InvalidOperationException("Cannot obtain any latest zip package from API"))
                             .Where(asset => asset != null))
                {
                    RearrangeLegacyPackageLocaleOrder(asset);
                    await TryAddResourceVersionList(asset, packageList);
                }
            }

            // Check if the existing installation has the plugin installed or not
            if (!await GameVersionManager.IsPluginVersionsMatch())
            {
                // Try get the plugin package
                TryAddPluginPackage(packageList);
            }

            // Check if the existing installation has the sdk installed or not
            if (!await GameVersionManager.IsSdkVersionsMatch())
            {
                // Get the sdk package
                foreach (RegionResourcePlugin sdkPackage in GameVersionManager.GetGameSdkZip()!)
                {
                    packageList.Add(new GameInstallPackage(sdkPackage, GamePath));
                }
            }
        }

    #nullable enable
        protected virtual void TryAddPluginPackage(List<GameInstallPackage> assetList)
        {
            const string pluginKeyStart = "plugin_";
            const string pluginKeyEnd   = "_version";

            // Get the plugin resource list and if it's empty, return
            List<RegionResourcePlugin>? pluginResourceList = GameVersionManager.GetGamePluginZip();
            if (pluginResourceList == null)
            {
                return;
            }

            // Parse game version INI configuration and search for the installed plugin.
            // Matching it if the latest version is found, then remove the corresponding
            Dictionary<string, RegionResourcePlugin> pluginResourceDictionary = pluginResourceList
               .ToDictionary(asset => asset.plugin_id!, StringComparer.OrdinalIgnoreCase);

            // If the game ini section is not null, then try eliminate the version section
            if (GameVersionManager.GameIniVersionSection != null)
            {
                foreach (KeyValuePair<string, IniValue> iniProperty in GameVersionManager.GameIniVersionSection
                            .Where(x => x.Key.StartsWith(pluginKeyStart) && x.Key.EndsWith(pluginKeyEnd)))
                {
                    // Get the plugin id from the ini property's key
                    string iniKey            = iniProperty.Key;
                    int    startIniKeyOffset = pluginKeyStart.Length;
                    int startIniKeyLength =
                        iniProperty.Key.LastIndexOf(pluginKeyEnd, StringComparison.OrdinalIgnoreCase) -
                        startIniKeyOffset;
                    string iniPluginId = iniKey.AsSpan(startIniKeyOffset, startIniKeyLength).ToString();

                    // Try remove the plugin resource from dictionary if found
                    if (!pluginResourceDictionary.TryGetValue(iniPluginId, out var pluginResource))
                    {
                        continue;
                    }

                    // Try to get the plugin version from both installed and api's one
                    string               pluginResourceVersion = pluginResource.version!;
                    if (!GameVersion.TryParse(pluginResourceVersion, out GameVersion? pluginResourceVersionResult)
                        || !GameVersion.TryParse(iniProperty.Value.ToString(),
                                                 out GameVersion? installedPluginVersionResult))
                    {
                        continue;
                    }

                    // If the both plugin versions from API and INI is equal, then remove the package
                    // from the dictionary.
                    if (pluginResourceVersionResult.Equals(installedPluginVersionResult))
                    {
                        // Remove the plugin resource
                        pluginResourceDictionary.Remove(iniPluginId);
                    }
                }
            }

            // Add the plugin resources to asset list
            foreach (KeyValuePair<string, RegionResourcePlugin> pluginResource in pluginResourceDictionary)
            {
                if (!GameVersion.TryParse(pluginResource.Value.version, out GameVersion? _))
                {
                    LogWriteLine($"Failed to parse plugin version: {pluginResource.Value.version} with id: {pluginResource.Key}",
                                 LogType.Error, true);
                    continue;
                }

                assetList.Add(new GameInstallPackage(pluginResource.Value, GamePath));
            }
        }
    #nullable restore

        private async ValueTask<int> CheckExistingOrAskFolderDialog()
        {
            // Try run the result and if it's null, then return -1 (Cancel the operation)
            string result = await AskGameFolderDialog(GameVersionManager.FindGameInstallationPath);
            if (result == null)
            {
                return -1;
            }

            // Check for existing installation and if it's found, then override result
            // with pathPossibleExisting value and return 0 to skip the process
            string pathPossibleExisting = GameVersionManager.FindGameInstallationPath(result);
            if (pathPossibleExisting != null)
            {
                GameVersionManager.UpdateGamePath(pathPossibleExisting, false);
                return 0;
            }

            // if above passes, start with the new installation
            GameVersionManager.UpdateGamePath(result, false);
            return 1;
        }

        #endregion

        #region Virtual Methods - GetInstallationPath

    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected virtual async ValueTask TryAddResourceVersionList(RegionResourceVersion    asset,
                                                                    List<GameInstallPackage> packageList,
                                                                    bool                     isSkipMainPackage = false)
        {
            // Try add the main resource version list first
            await AddMainResourceVersionList(asset, packageList, isSkipMainPackage);
            await AddVoiceOverResourceVersionList(asset, packageList);
        }

        protected virtual async ValueTask AddVoiceOverResourceVersionList(
            RegionResourceVersion asset, List<GameInstallPackage> packageList)
        {
            // Initialize langID

            // Skip if the asset doesn't have voice packs
            if (asset.voice_packs == null || asset.voice_packs.Count == 0)
            {
                return;
            }

            // Get available language names
            Dictionary<string, string> langStringsDict = GetLanguageDisplayDictFromVoicePackList(asset.voice_packs);

            if (!_canSkipAudio)
            {
                // If the game has already installed or in preload, then try get Voice language ID from registry
                GameInstallStateEnum gameState = await GameVersionManager.GetGameState();
                GameInstallPackage   package;
                if (gameState == GameInstallStateEnum.InstalledHavePreload
                    || gameState == GameInstallStateEnum.NeedsUpdate)
                {
                    // Try get the voice language ID from the registry
                    var    langID     = _gameVoiceLanguageID;
                    string localeCode = GetLanguageLocaleCodeByID(langID);

                    // Try find the VO resource by locale code
                    if (TryGetVoiceOverResourceByLocaleCode(asset.voice_packs, localeCode,
                                                            out RegionResourceVersion voRes))
                    {
                        package = new GameInstallPackage(voRes, GamePath, asset.version)
                        {
                            LanguageID  = localeCode,
                            PackageType = GameInstallPackageType.Audio
                        };
                        packageList.Add(package);
                        LogWriteLine($"Adding primary {package.LanguageID} audio package: {package.Name} to the list (Hash: {package.HashString})",
                                     LogType.Default, true);
                    }

                    // Also try add another voice pack that already been installed
                    await TryAddOtherInstalledVoicePacks(asset.voice_packs, packageList, asset.version);
                }
                // Else, show dialog to choose the language ID to be installed
                else
                {
                    // Get the dialog and go for the selection
                    (Dictionary<string, string> addedVO, string setAsDefaultVOLocalecode) =
                        await Dialog_ChooseAudioLanguageChoice(ParentUI, langStringsDict);
                    if (addedVO == null && string.IsNullOrEmpty(setAsDefaultVOLocalecode))
                    {
                        throw new TaskCanceledException();
                    }

                    // Get the game default VO index
                    int setAsDefaultVO = GetIDByLanguageLocaleCode(setAsDefaultVOLocalecode);

                    // Sanitize check for invalid values
                    if (addedVO == null || string.IsNullOrEmpty(setAsDefaultVOLocalecode))
                    {
                        throw new
                            InvalidOperationException("The addedVO variable or setAsDefaultVO should neither be null!");
                    }

                    // Lookup for the package
                    foreach (KeyValuePair<string, string> voChoice in addedVO)
                    {
                        // Try find the VO resource by locale code
                        if (!TryGetVoiceOverResourceByLocaleCode(asset.voice_packs, voChoice.Key,
                                                                 out RegionResourceVersion voRes))
                        {
                            continue;
                        }

                        package = new GameInstallPackage(voRes, GamePath, asset.version)
                        {
                            LanguageID  = voChoice.Key,
                            PackageType = GameInstallPackageType.Audio
                        };
                        packageList.Add(package);
                        LogWriteLine($"Adding primary {package.LanguageID} audio package: {package.Name} to the list (Hash: {package.HashString})",
                                     LogType.Default, true);
                    }

                    // Set the voice language ID to value given
                    GameVersionManager.GamePreset.SetVoiceLanguageID(setAsDefaultVO);
                }
            }
        }

        protected virtual async ValueTask AddMainResourceVersionList(RegionResourceVersion    asset,
                                                                     List<GameInstallPackage> packageList,
                                                                     bool                     isSkipMainPackage = false)
        {
            // Try add the package into the list
            GameInstallPackage package = new GameInstallPackage(asset, GamePath)
                { PackageType = GameInstallPackageType.General };

            // If the main package is not skipped, then add it.
            // Otherwise, ignore it.
            if (!isSkipMainPackage)
            {
                // Add the main package
                packageList.Add(package);
                LogWriteLine($"Adding general package: {package.Name} to the list (Hash: {package.HashString})",
                             LogType.Default, true);

                // Write to log if the package has segments (Example: Genshin Impact)
                if (package.Segments != null)
                {
                    foreach (GameInstallPackage segment in package.Segments.ToList())
                    {
                        LogWriteLine($"Adding segmented package: {segment.Name} to the list (Hash: {segment.HashString})",
                                     LogType.Default, true);
                    }
                }
            }
        }
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

        protected virtual async ValueTask TryAddOtherInstalledVoicePacks(
            List<RegionResourceVersion> packs, List<GameInstallPackage> packageList, string assetVersion)
        {
            // If not found (null), then return
            if (_gameAudioLangListPath == null)
            {
                return;
            }

            // Start read the file
            using StreamReader sw = new StreamReader(_gameAudioLangListPath);
#nullable enable
            while (await sw.ReadLineAsync() is { } langStr)
            {
                // Get the line and get the language locale code by language string
                string? localeCode = GetLanguageLocaleCodeByLanguageString(langStr
#if !DEBUG
                    , false
#endif
                    );

                if (string.IsNullOrEmpty(localeCode))
                {
                    continue;
                }
#nullable restore

                // Try get the voice over resource
                if (TryGetVoiceOverResourceByLocaleCode(packs, localeCode, out RegionResourceVersion outRes))
                {
                    // Check if the existing package is already exist or not.
                    GameInstallPackage outResDup =
                        packageList.FirstOrDefault(x => x.LanguageID != null &&
                                                   x.LanguageID.Equals(outRes.language,
                                                                    StringComparison.OrdinalIgnoreCase));
                    if (outResDup != null)
                    {
                        continue;
                    }

                    GameInstallPackage package = new GameInstallPackage(outRes, GamePath, assetVersion)
                        { LanguageID = localeCode, PackageType = GameInstallPackageType.Audio };
                    packageList.Add(package);
                    LogWriteLine($"Adding additional {package.LanguageID} audio package: {package.Name} to the list (Hash: {package.HashString})",
                                 LogType.Default, true);
                    continue;
                }

                // Throw if the language string is not supported
                throw new
                    KeyNotFoundException($"Value: {langStr} in {_gameAudioLangListPath} file is not a supported language string!\r\nPlease remove the line from the file manually.");
            }
        }

        #endregion

        #region Private Methods - StartPackageInstallation
        private void MoveFileToIngredientList(List<FilePropertiesRemote> assetIndex, string sourcePath,
                                              string                     targetPath, bool   isSR = false)
        {
            // HACK: Also move pkg_version on Star Rail delta patch application to prevent patch error
            if (isSR)
            {
                FilePropertiesRemote pkgVer = new FilePropertiesRemote
                {
                    N = "pkg_version"
                };
                assetIndex.Add(pkgVer);
            }

            // Iterate the asset
            foreach (FilePropertiesRemote index in assetIndex)
            {
                // Get the combined path from the asset name
                var inputPath    = Path.Combine(sourcePath, index.N);
                var outputPath   = EnsureCreationOfDirectory(Path.Combine(targetPath, index.N));

                // Sanity Check: If the file is still missing even after the process, then throw
                var fileInfo = new FileInfo(inputPath);
                if (!fileInfo.Exists)
                {
                    continue;
                }

                // Move the file to the target directory
                fileInfo.IsReadOnly = false;
                fileInfo.MoveTo(outputPath, true);
                LogWriteLine($"Moving from: {inputPath} to {outputPath}", LogType.Default, true);
            }
        }

        private void TryUnassignReadOnlyFiles()
        {
            DirectoryInfo dirInfo = new DirectoryInfo(GamePath);
            if (!dirInfo.Exists)
            {
                return;
            }

            foreach (FileInfo _ in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                                           .EnumerateNoReadOnly())
            {
                // Do nothing
            }
        }

        private void TryRemoveRedundantHDiffList()
        {
            DirectoryInfo dirInfo = new DirectoryInfo(GamePath);
            if (!dirInfo.Exists)
            {
                return;
            }

            foreach (FileInfo file in dirInfo.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly))
            {
                string name = file.Name;
                if (!(name.StartsWith("deletefiles", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".txt",  StringComparison.OrdinalIgnoreCase))
                 && !(name.StartsWith("hdifffiles",  StringComparison.OrdinalIgnoreCase) && name.EndsWith(".txt",  StringComparison.OrdinalIgnoreCase))
                 && !(name.StartsWith("hdiffmap",    StringComparison.OrdinalIgnoreCase) && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                try
                {
                    file.Delete();
                }
                catch (Exception ex)
                {
                    SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                    LogWriteLine($"Be careful that the installation process might have some problem since the launcher can't remove HDiff list file: {name}!\r\n{ex}",
                                 LogType.Warning, true);
                }
            }
        }

        #endregion

        #region Private Methods - StartPackageDownload

        private async ValueTask InvokePackageDownloadRoutine(DownloadClient           downloadClient,
                                                             List<GameInstallPackage> packageList,
                                                             CancellationToken        token)
        {
            // Get the package/segment count
            int packageCount = packageList.Sum(x => x.Segments?.Count ?? 1);

            // Set progress count to beginning
            ProgressAllCountCurrent = 1;
            ProgressAllCountTotal   = packageCount;

            // Initialize a legacy Http as well
            using Http _httpClient = new Http(true, customHttpClient: downloadClient.GetHttpClient());

            // Subscribe the download progress to the event adapter
            _httpClient.DownloadProgress += HttpClientDownloadProgressAdapter;

            try
            {
                // Iterate the package list
                foreach (GameInstallPackage package in packageList.ToList())
                {
                    // If the package is segmented, then iterate and run the routine for segmented packages
                    if (package.Segments != null)
                    {
                        // Iterate the segment list
                        for (int i = 0; i < package.Segments.Count; i++)
                        {
                            await RunPackageDownloadRoutine(_httpClient, downloadClient, package.Segments[i], packageCount, token);
                        }

                        // Skip action below and continue to the next segment
                        continue;
                    }

                    // Else, run the routine as normal
                    await RunPackageDownloadRoutine(_httpClient, downloadClient, package, packageCount, token);
                }
            }
            finally
            {
                // Unsubscribe the download progress from the event adapter
                _httpClient.DownloadProgress -= HttpClientDownloadProgressAdapter;
            }
        }

        private async ValueTask RunPackageDownloadRoutine(Http               httpClient,
                                                          DownloadClient     downloadClient,
                                                          GameInstallPackage package,
                                                          int                packageCount,
                                                          CancellationToken  token)
        {
            // Set the activity status
            Status.IsIncludePerFileIndicator = packageCount > 1;
            Status.ActivityStatus =
                $"{Lang._Misc.Downloading}: {string.Format(Lang._Misc.PerFromTo, ProgressAllCountCurrent,
                                                           ProgressAllCountTotal)}";
            LogWriteLine($"Downloading package URL {ProgressAllCountCurrent}/{ProgressAllCountTotal} ({ConverterTool.SummarizeSizeSimple(package.Size)}): {package.URL}");

            // If the file exist or package size is unmatched,
            // then start downloading
            long legacyExistingPackageFileSize  = package.GetStreamLength(DownloadThreadCount);
            long existingPackageFileSize        = package.SizeDownloaded > legacyExistingPackageFileSize ? package.SizeDownloaded : legacyExistingPackageFileSize;
            bool isExistingPackageFileExist     = package.IsReadStreamExist(DownloadThreadCount);

            if (!isExistingPackageFileExist
                || existingPackageFileSize != package.Size)
            {
                // Get the file path
                FileInfo fileInfo = new FileInfo(package.PathOutput)
                    .EnsureCreationOfDirectory()
                    .EnsureNoReadOnly();

                bool isCanMultiSession = false;
                // If a legacy downloader is used, then use the legacy Http downloader
                if (package.IsUseLegacyDownloader)
                {
                    // If the package size is more than or equal to 10 MB, then allow to use multi-session.
                    // Otherwise, forcefully use single-session.
                    isCanMultiSession = package.Size >= 10 << 20;
                    if (isCanMultiSession)
                    {
                        await httpClient.Download(package.URL, fileInfo.FullName, DownloadThreadCount, false, token);
                    }
                    else
                    {
                        await httpClient.Download(package.URL, fileInfo.FullName, false, null, null, token);
                    }
                }
                // Otherwise, use the new downloder
                else
                {
                    // Run the new downloader
                    await RunDownloadTask(
                        package.Size,
                        fileInfo,
                        package.URL,
                        downloadClient,
                        HttpClientDownloadProgressAdapter,
                        token,
                        false);
                }

                // Update status to merging
                Status.ActivityStatus =
                    $"{Lang._Misc.Merging}: {string.Format(Lang._Misc.PerFromTo, ProgressAllCountCurrent,
                                                           ProgressAllCountTotal)}";
                UpdateStatus();

                // Check if the merge chunk is enabled and the download could perform multisession,
                // then do merge (also if legacy downloader is used).
                if (_canMergeDownloadChunks && isCanMultiSession && package.IsUseLegacyDownloader)
                {
                    await httpClient.Merge(token);
                }
            }

            // Increment the total count
            ProgressAllCountCurrent++;
        }

        private async ValueTask CheckExistingDownloadAsync(UIElement Content, List<GameInstallPackage> packageList)
        {
            // If the _progressAllSizeCurrent has the size, then
            // display the reset or continue download dialog.
            // UPDATE: Ensure if the downloaded size is not the same as total. If no, then continue
            //         showing the dialog
            if (ProgressAllSizeCurrent > 0 && ProgressAllSizeTotal != ProgressAllSizeCurrent)
            {
                switch (await Dialog_ExistingDownload(Content, ProgressAllSizeCurrent, ProgressAllSizeTotal))
                {
                    case ContentDialogResult.Primary:
                        break;
                    // Reset the download (delete all existing files) if selected
                    case ContentDialogResult.Secondary:
                        ResetDownload(packageList);
                        break;
                    case ContentDialogResult.None:
                        throw new OperationCanceledException("Cancelling progress");
                }
            }
        }

        private void ResetDownload(List<GameInstallPackage> packageList)
        {
            // Reset the _progressAllSizeCurrent to 0
            ProgressAllSizeCurrent = 0;

            // Iterate and start deleting the existing file
            for (int i = 0; i < packageList.Count; i++)
            {
                // Check if the package has segment. If yes, then iterate it per segment
                if (packageList[i].Segments != null)
                {
                    for (int j = 0; j < packageList[i].Segments.Count; j++)
                    {
                        DeleteDownloadedFile(packageList[i].Segments[j].PathOutput, DownloadThreadCount);
                    }
                }

                // Otherwise, delete the package as a single file
                DeleteDownloadedFile(packageList[i].PathOutput, DownloadThreadCount);
            }
        }

        private void DeleteDownloadedFile(string FileOutput, byte Thread)
        {
            // Get the info of the existing file
            FileInfo fileInfo = new FileInfo(FileOutput);

            // If existed, then delete
            if (fileInfo.Exists)
            {
                fileInfo.IsReadOnly = false;
                fileInfo.Delete();
            }

            // Get the info of the new downloader metadata
            FileInfo fileInfoMetadata = new FileInfo(FileOutput + ".collapseMeta");

            // If metadata file existed, then delete
            if (fileInfoMetadata.Exists)
            {
                fileInfoMetadata.IsReadOnly = false;
                fileInfoMetadata.Delete();
            }

            // Delete the file of the chunk file too
            Http.DeleteMultisessionFiles(FileOutput, Thread);
        }

        private async ValueTask<long> GetExistingDownloadPackageSize(DownloadClient downloadClient, List<GameInstallPackage> packageList, CancellationToken token)
        {
            // Initialize total existing size and download thread count
            long totalSize = 0;

            // Iterate the size and find the total, then increment it to totalSize
            for (int i = 0; i < packageList.Count; i++)
            {
                if (packageList[i].Segments != null)
                {
                    long totalSegmentDownloaded = 0;
                    for (int j = 0; j < packageList[i].Segments.Count; j++)
                    {
                        long segmentDownloaded =
                            Http
                               .CalculateExistingMultisessionFilesWithExpctdSize(packageList[i].Segments[j].PathOutput,
                                    DownloadThreadCount, packageList[i].Segments[j].Size);

                        long newSegmentedDownloaded                         = await downloadClient.GetDownloadedFileSize(
                                                                                  packageList[i].Segments[j].URL,
                                                                                  packageList[i].Segments[j].PathOutput,
                                                                                  packageList[i].Segments[j].Size,
                                                                                  token
                                                                                  );
                        bool isUseLegacySegmentedSize                       = !LauncherConfig.IsUsePreallocatedDownloader
                            || segmentDownloaded > newSegmentedDownloaded
                            || newSegmentedDownloaded > packageList[i].Segments[j].Size;
                        totalSize                                           += isUseLegacySegmentedSize ? segmentDownloaded : newSegmentedDownloaded;
                        totalSegmentDownloaded                              += isUseLegacySegmentedSize ? segmentDownloaded : newSegmentedDownloaded;
                        packageList[i].Segments[j].SizeDownloaded           =  isUseLegacySegmentedSize ? segmentDownloaded : newSegmentedDownloaded;
                        packageList[i].Segments[j].IsUseLegacyDownloader    =  isUseLegacySegmentedSize;
                    }

                    packageList[i].SizeDownloaded = totalSegmentDownloaded;
                    continue;
                }

                long legacyDownloadedSize     =
                    Http.CalculateExistingMultisessionFilesWithExpctdSize(packageList[i].PathOutput,
                        DownloadThreadCount, packageList[i].Size);
                long newDownloaderSize        = await downloadClient.GetDownloadedFileSize(
                                                    packageList[i].URL,
                                                    packageList[i].PathOutput,
                                                    packageList[i].Size,
                                                    token
                                                    );
                bool isUseLegacySize                    = !LauncherConfig.IsUsePreallocatedDownloader
                    || legacyDownloadedSize > newDownloaderSize
                    || newDownloaderSize > packageList[i].Size;
                packageList[i].IsUseLegacyDownloader    =  isUseLegacySize;
                packageList[i].SizeDownloaded           =  isUseLegacySize ? legacyDownloadedSize : newDownloaderSize;
                totalSize                               += packageList[i].SizeDownloaded;
            }

            // return totalSize
            return totalSize;
        }

        private async ValueTask CheckDriveFreeSpace(UIElement Content, List<GameInstallPackage> packageList,
                                                    double    sizeDownloaded)
        {
            // Get the information about the disk
            DriveInfo driveInfo = new DriveInfo(GamePath);

            // Get the required space uncompressed
            long requiredSpaceUncompressed = packageList.Sum(x => x.SizeRequired);
            long requiredSpaceCompressed   = packageList.Sum(x => x.Size);

            // Get the progress of the compressed zip from 0.0d to 1.0d
            double packageSizeDownloadedProgress = sizeDownloaded / requiredSpaceCompressed;

            // Get the required size of the uncompressed, then multiply by the packageSizeDownloadedProgress
            double currentDownloadedUncompressed = requiredSpaceUncompressed * packageSizeDownloadedProgress;

            // Get the remained size to download
            double remainedDownloadUncompressed = requiredSpaceUncompressed - currentDownloadedUncompressed;
            double remainedDownloadCompressed   = requiredSpaceCompressed - sizeDownloaded;

            // Get the total free space of the disk and log the required size
            long diskFreeSpace = driveInfo.TotalFreeSpace;
            LogWriteLine($"Total free space required: {ConverterTool.SummarizeSizeSimple(remainedDownloadUncompressed)} remained to be downloaded (Total: {ConverterTool.SummarizeSizeSimple(requiredSpaceUncompressed)}) with {driveInfo.Name} remaining free space: {ConverterTool.SummarizeSizeSimple(diskFreeSpace)}",
                         LogType.Default, true);

        #if DEBUG
            double diskSpaceGb = Math.Round(ConverterTool.SummarizeSizeDouble(Convert.ToDouble(diskFreeSpace), 3), 4);
            double requiredSpaceGb = Convert.ToDouble(requiredSpaceUncompressed / (1L << 30));
            double existingPackageSizeGb = Convert.ToDouble(sizeDownloaded / (1L << 30));
            double remainingDownloadSizeGb =
                Math.Round(ConverterTool.SummarizeSizeDouble(Convert.ToDouble(remainedDownloadCompressed), 3), 4);
            LogWriteLine($"Available Drive Space (GB): {diskSpaceGb}",                              LogType.Debug);
            LogWriteLine($"Existing Package Size (Compressed Size): {sizeDownloaded}", LogType.Debug);
            LogWriteLine($"Required Space (Uncompressed Size): {requiredSpaceUncompressed}",        LogType.Debug);
            LogWriteLine($"Required Space Minus Existing Package Size (Uncompressed Size): {requiredSpaceUncompressed - currentDownloadedUncompressed}",
                         LogType.Debug);
            LogWriteLine("===============================================================================================",
                         LogType.Debug);
            LogWriteLine($"Existing Package Size (Compressed Size)(GB): {existingPackageSizeGb}", LogType.Debug);
            LogWriteLine($"Required Space (Uncompressed Size)(GB): {requiredSpaceGb}",            LogType.Debug);
            LogWriteLine($"Required Space Minus Existing Package Size (Uncompressed Size)(GB): {(requiredSpaceUncompressed - currentDownloadedUncompressed) / (1L << 30)}",
                         LogType.Debug);
            LogWriteLine($"Remaining Package Download Size (Compressed Size)(GB): {remainingDownloadSizeGb}",
                         LogType.Debug);
        #endif

            if (diskFreeSpace < remainedDownloadUncompressed)
            {
                string errStr = $"Free Space on {driveInfo.Name} is not sufficient! " +
                                $"(Free space: {ConverterTool.SummarizeSizeSimple(diskFreeSpace)}, Req. Space: {ConverterTool.SummarizeSizeSimple(remainedDownloadUncompressed)} (Total: {ConverterTool.SummarizeSizeSimple(requiredSpaceUncompressed)}), " +
                                $"Existing Package Size (Compressed): {sizeDownloaded} (Uncompressed): {currentDownloadedUncompressed}, Drive: {driveInfo.Name})";
                LogWriteLine(errStr, LogType.Error, true);
                await Dialog_InsufficientDriveSpace(Content, diskFreeSpace, remainedDownloadUncompressed,
                                                    driveInfo.Name);
                throw new TaskCanceledException(errStr);
            }
        }

        private async Task GetPackagesRemoteSize(List<GameInstallPackage> packageList, CancellationToken token)
        {
            // Iterate and assign the remote size to each package inside the list in parallel
            await Parallel.ForEachAsync(packageList, new ParallelOptions
            {
                CancellationToken = token
            }, async (package, innerToken) =>
            {
                if (package.Segments != null)
                {
                    await TryGetSegmentedPackageRemoteSize(package, innerToken);
                    return;
                }

                await TryGetPackageRemoteSize(package, innerToken);
            });
        }

        #endregion

        #region Virtual Methods - StartPackageDownload

        public void UpdateCompletenessStatus(CompletenessStatus status)
        {
            switch (status)
            {
                case CompletenessStatus.Running:
                    IsRunning           = true;
                    Status.IsRunning   = true;
                    Status.IsCompleted = false;
                    Status.IsCanceled  = false;
                    #if !DISABLEDISCORD
                    InnerLauncherConfig.AppDiscordPresence?.SetActivity(ActivityType.Update);
                    #endif
                    break;
                case CompletenessStatus.Completed:
                    IsRunning           = false;
                    Status.IsRunning   = false;
                    Status.IsCompleted = true;
                    Status.IsCanceled  = false;
                    #if !DISABLEDISCORD
                    InnerLauncherConfig.AppDiscordPresence?.SetActivity(ActivityType.Idle);
                    #endif
                    // HACK: Fix the progress not achieving 100% while completed
                    lock (Progress)
                    {
                        Progress.ProgressAllPercentage     = 100f;
                        Progress.ProgressPerFilePercentage = 100f;
                    }
                    break;
                case CompletenessStatus.Cancelled:
                    IsRunning           = false;
                    Status.IsRunning   = false;
                    Status.IsCompleted = false;
                    Status.IsCanceled  = true;
                    #if !DISABLEDISCORD
                    InnerLauncherConfig.AppDiscordPresence?.SetActivity(ActivityType.Idle);
                    #endif
                    break;
                case CompletenessStatus.Idle:
                    IsRunning           = false;
                    Status.IsRunning   = false;
                    Status.IsCompleted = false;
                    Status.IsCanceled  = false;
                    #if !DISABLEDISCORD
                    InnerLauncherConfig.AppDiscordPresence?.SetActivity(ActivityType.Idle);
                    #endif
                    break;
            }

            UpdateAll();
        }

        protected async Task TryGetPackageRemoteSize(GameInstallPackage asset, CancellationToken token)
        {
            asset.Size = await FallbackCDNUtil.GetContentLength(asset.URL, token);

            LogWriteLine($"Package: [T: {asset.PackageType}] {asset.Name} has {ConverterTool.SummarizeSizeSimple(asset.Size)} in size with {ConverterTool.SummarizeSizeSimple(asset.SizeRequired)} of free space required",
                         LogType.Default, true);
        }

        protected async Task TryGetSegmentedPackageRemoteSize(GameInstallPackage asset, CancellationToken token)
        {
            long totalSize = 0;
            await Parallel.ForAsync(0, asset.Segments.Count, new ParallelOptions
            {
                CancellationToken = token
            },
            async (i, _) =>
            {
                long segmentSize = await FallbackCDNUtil.GetContentLength(asset.Segments[i].URL, token);
                totalSize += segmentSize;
                asset.Segments[i].Size = segmentSize;
                LogWriteLine($"Package Segment: [T: {asset.PackageType}] {asset.Segments[i].Name} has {ConverterTool.SummarizeSizeSimple(segmentSize)} in size",
                             LogType.Default, true);
            });

            asset.Size = totalSize;
            LogWriteLine($"Package Segment (count: {asset.Segments.Count}) has {ConverterTool.SummarizeSizeSimple(asset.Size)} in total size with {ConverterTool.SummarizeSizeSimple(asset.SizeRequired)} of free space required",
                         LogType.Default, true);
        }

        #endregion

        #region Virtual Methods - UninstallGame

        protected virtual UninstallGameProperty AssignUninstallFolders()
        {
            throw new
                NotSupportedException($"Cannot uninstall game: {GameVersionManager.GamePreset.GameType}. Uninstall method is not yet implemented!");
        }

        #endregion

        #region Event Methods

        protected void UpdateProgressBase()
        {
            base.UpdateProgress();
        }

        protected void DeltaPatchCheckProgress(object sender, PatchEvent e)
        {
            lock (Progress)
            {
                Progress.ProgressAllPercentage = e.ProgressPercentage;

                Progress.ProgressAllTimeLeft = e.TimeLeft;
                Progress.ProgressAllSpeed    = e.Speed;

                Progress.ProgressAllSizeTotal   = e.TotalSizeToBePatched;
                Progress.ProgressAllSizeCurrent = e.CurrentSizePatched;
            }

            if (!CheckIfNeedRefreshStopwatch())
            {
                return;
            }

            Status.IsProgressAllIndetermined = false;
            UpdateProgressBase();
            UpdateStatus();
        }

        protected void DeltaPatchCheckLogEvent(object sender, LoggerEvent e)
        {
            if (HDiffPatch.LogVerbosity == Verbosity.Quiet
                || (HDiffPatch.LogVerbosity == Verbosity.Debug
                    && !(e.LogLevel == Verbosity.Debug ||
                         e.LogLevel == Verbosity.Verbose ||
                         e.LogLevel == Verbosity.Info))
                || (HDiffPatch.LogVerbosity == Verbosity.Verbose
                    && !(e.LogLevel == Verbosity.Verbose ||
                         e.LogLevel == Verbosity.Info))
                || (HDiffPatch.LogVerbosity == Verbosity.Info
                    && e.LogLevel != Verbosity.Info))
            {
                return;
            }

            LogType type = e.LogLevel switch
                           {
                               Verbosity.Verbose => LogType.Debug,
                               Verbosity.Debug => LogType.Debug,
                               _ => LogType.Default
                           };

            LogWriteLine(e.Message, type, true);
        }

        protected void DeltaPatchCheckProgress(object sender, TotalPerFileProgress e)
        {
            lock (Progress)
            {
                Progress.ProgressAllPercentage =
                    e.ProgressAllPercentage == 0 ? e.ProgressPerFilePercentage : e.ProgressAllPercentage;

                Progress.ProgressAllTimeLeft = e.ProgressAllTimeLeft;
                Progress.ProgressAllSpeed    = e.ProgressAllSpeed;

                Progress.ProgressAllSizeTotal   = e.ProgressAllSizeTotal;
                Progress.ProgressAllSizeCurrent = e.ProgressAllSizeCurrent;
            }

            if (!CheckIfNeedRefreshStopwatch())
            {
                return;
            }

            Status.IsProgressAllIndetermined = false;
            UpdateProgressBase();
            UpdateStatus();
        }

        private void ZipProgressAdapter(object sender, ExtractProgressProp e)
        {
            // Increment current total size
            lock (Progress)
            {
                long lastSize = GetLastSize((long)e.TotalRead);
                double speed = CalculateSpeed(lastSize);
                ProgressAllSizeCurrent += lastSize;

                if (!CheckIfNeedRefreshStopwatch())
                {
                    return;
                }

                // Assign per file size
                ProgressPerFileSizeCurrent = (long)e.TotalRead;
                ProgressPerFileSizeTotal   = (long)e.TotalSize;

                lock (Progress)
                {
                    // Assign local sizes to progress
                    Progress.ProgressPerFileSizeCurrent = ProgressPerFileSizeCurrent;
                    Progress.ProgressPerFileSizeTotal   = ProgressPerFileSizeTotal;
                    Progress.ProgressAllSizeCurrent     = ProgressAllSizeCurrent;
                    Progress.ProgressAllSizeTotal       = ProgressAllSizeTotal;

                    // Calculate the speed
                    Progress.ProgressAllSpeed = CalculateSpeed(lastSize);

                    // Calculate percentage
                    Progress.ProgressAllPercentage     = ConverterTool.ToPercentage(ProgressAllSizeTotal,     ProgressAllSizeCurrent);
                    Progress.ProgressPerFilePercentage = ConverterTool.ToPercentage(ProgressPerFileSizeTotal, ProgressPerFileSizeCurrent);
                    // Calculate the timelapse
                    Progress.ProgressAllTimeLeft = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal, ProgressAllSizeCurrent, speed);
                }

                UpdateAll();
            }
        }

        private long GetLastSize(long input)
        {
            if (_totalLastSizeCurrent > input)
            {
                _totalLastSizeCurrent = input;
            }

            long a = input - _totalLastSizeCurrent;
            _totalLastSizeCurrent = input;
            return a;
        }

        private void HttpClientDownloadProgressAdapter(int read, DownloadProgress downloadProgress)
        {
            // Set the progress bar not indetermined
            Status.IsProgressPerFileIndetermined = false;
            Status.IsProgressAllIndetermined = false;

            // Increment the total current size if status is not merging
            Interlocked.Add(ref ProgressAllSizeCurrent, read);

            // Calculate the speed
            double speedAll = CalculateSpeed(read);

            if (!CheckIfNeedRefreshStopwatch())
            {
                return;
            }

            lock (Progress)
            {
                // Assign speed with clamped value
                double speedClamped = speedAll.ClampLimitedSpeedNumber();

                // Assign local sizes to progress
                Progress.ProgressAllSizeCurrent = ProgressAllSizeCurrent;
                Progress.ProgressAllSizeTotal   = ProgressAllSizeTotal;
                Progress.ProgressAllSpeed       = speedClamped;
                Progress.ProgressAllPercentage  = ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent);
                Progress.ProgressAllTimeLeft    = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal, ProgressAllSizeCurrent, speedClamped);

                // Update the status of per file size and current progress from Http client
                Progress.ProgressPerFileSizeCurrent = downloadProgress.BytesDownloaded;
                Progress.ProgressPerFileSizeTotal   = downloadProgress.BytesTotal;
                Progress.ProgressPerFileSpeed       = speedClamped;
                Progress.ProgressPerFilePercentage  = ConverterTool.ToPercentage(downloadProgress.BytesTotal, downloadProgress.BytesDownloaded);
            }
            // Update the status
            UpdateAll();
        }

        private void HttpClientDownloadProgressAdapter(object sender, DownloadEvent e)
        {
            // Set the progress bar not indetermined
            Status.IsProgressPerFileIndetermined = false;
            Status.IsProgressAllIndetermined     = false;

            if (e.State != DownloadState.Merging)
            {
                // Increment the total current size if status is not merging
                ProgressAllSizeCurrent += e.Read;
            }

            // Calculate the speed
            double speedAll = CalculateSpeed(e.Read);

            if (!CheckIfNeedRefreshStopwatch())
            {
                return;
            }

            if (e.State != DownloadState.Merging)
            {
                lock (Progress)
                {
                    // Assign local sizes to progress
                    Progress.ProgressPerFileSizeCurrent = ProgressPerFileSizeCurrent;
                    Progress.ProgressPerFileSizeTotal   = ProgressPerFileSizeTotal;
                    Progress.ProgressAllSizeCurrent     = ProgressAllSizeCurrent;
                    Progress.ProgressAllSizeTotal       = ProgressAllSizeTotal;

                    // Calculate the speed
                    Progress.ProgressAllSpeed = speedAll;

                    // Calculate percentage
                    Progress.ProgressPerFilePercentage = ConverterTool.ToPercentage(ProgressPerFileSizeTotal, ProgressPerFileSizeCurrent);
                    Progress.ProgressAllPercentage     = ConverterTool.ToPercentage(ProgressAllSizeTotal,     ProgressAllSizeCurrent);
                    // Calculate the timelapse
                    Progress.ProgressAllTimeLeft       = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal, ProgressAllSizeCurrent, speedAll);
                }
            }
            else
            {
                // If merging, show per file indicator explicitly
                // and then update the normal progress
                Status.IsIncludePerFileIndicator = true;

                // If status is merging, then use progress for speed and timelapse from Http client
                // and set the rest from the base class
                lock (Progress)
                {
                    Progress.ProgressAllTimeLeft = e.TimeLeft;

                    Progress.ProgressAllSpeed = speedAll;

                    Progress.ProgressPerFileSizeCurrent = ProgressPerFileSizeCurrent;
                    Progress.ProgressPerFileSizeTotal   = ProgressPerFileSizeTotal;
                    Progress.ProgressAllSizeCurrent     = ProgressAllSizeCurrent;
                    Progress.ProgressAllSizeTotal       = ProgressAllSizeTotal;
                    Progress.ProgressAllPercentage      = ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent);
                }
            }

            // Update the status of per file size and current progress from Http client
            ProgressPerFileSizeCurrent = e.SizeDownloaded;
            ProgressPerFileSizeTotal   = e.SizeToBeDownloaded;
            lock (Progress) Progress.ProgressPerFilePercentage = e.ProgressPercentage;

            // Update the status
            UpdateAll();
        }
        #endregion
    }
}