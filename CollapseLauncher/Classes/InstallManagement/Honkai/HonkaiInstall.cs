using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Pages;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Logger;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable BaseMethodCallWithDefaultParameter
// ReSharper disable GrammarMistakeInComment
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace
// ReSharper disable ArrangeObjectCreationWhenTypeEvident
// ReSharper disable EmptyRegion
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable IdentifierTypo
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault

namespace CollapseLauncher.InstallManager.Honkai
{
    internal sealed partial class HonkaiInstall : InstallManagerBase
    {
        #region Override Properties

        protected override bool               _canDeltaPatch          => GameVersionManager.IsGameHasDeltaPatch();
        protected override DeltaPatchProperty _gameDeltaPatchProperty => GameVersionManager.GetDeltaPatchInfo();

        #endregion

        #region Properties

        private HonkaiCache  _gameCacheManager  { get; }
        private HonkaiRepair _gameRepairManager { get; set; }

        #endregion

        public HonkaiInstall(UIElement     parentUI, IGameVersionCheck GameVersionManager, ICache GameCacheManager)
            : base(parentUI, GameVersionManager)
        {
            _gameCacheManager = GameCacheManager as HonkaiCache;
        }

        #region Public Methods

        public override async ValueTask<int> StartPackageVerification(List<GameInstallPackage> gamePackage)
        {
            IsRunning = true;

            // Get the delta patch confirmation if the property is not null
            if (_gameDeltaPatchProperty == null)
            {
                return await base.StartPackageVerification(gamePackage);
            }

            // If the confirm is 1 (verified) or -1 (cancelled), then return the code
            int deltaPatchConfirm = await ConfirmDeltaPatchDialog(_gameDeltaPatchProperty,
                                                                  _gameRepairManager = GetGameRepairInstance(_gameDeltaPatchProperty.SourceVer) as HonkaiRepair);
            if (deltaPatchConfirm is -1 or 1)
            {
                return deltaPatchConfirm;
            }

            // If no delta patch is happening as deltaPatchConfirm returns 0 (normal update), then do the base verification
            return await base.StartPackageVerification(gamePackage);
        }

#nullable enable
        protected override IRepair GetGameRepairInstance(string? versionString) =>
            new HonkaiRepair(ParentUI,
                        GameVersionManager,
                        _gameCacheManager, GameSettings,
                        true,
                        versionString);
#nullable restore

        protected override async Task StartPackageInstallationInner(List<GameInstallPackage> gamePackage = null,
                                                                    bool isOnlyInstallPackage = false,
                                                                    bool doNotDeleteZipExplicit = false)
        {
            // If the delta patch is performed, then return
            if (!isOnlyInstallPackage && await StartDeltaPatch(_gameRepairManager, true))
            {
                return;
            }

            // If no delta patch is happening, then do the base installation
            await base.StartPackageInstallationInner(gamePackage);
        }

        public override async ValueTask<bool> TryShowFailedGameConversionState()
        {
            // Get the target and source path
            string GamePath            = GameVersionManager.GameDirPath;
            string GamePathIngredients = GetFailedGameConversionFolder(GamePath);
            // If path doesn't exist or null, then return false
            if (GamePathIngredients is null || !Directory.Exists(GamePathIngredients))
            {
                return false;
            }

            // Get the size of entire folder and check if it's below 1 MB, then return false
            long FileSize = Directory.EnumerateFiles(GamePathIngredients).Sum(x => new FileInfo(x).Length);
            if (FileSize < 1 << 20)
            {
                return false;
            }

            LogWriteLine($"Previous failed game conversion has been detected on Game: {GameVersionManager.GamePreset.ZoneFullname} ({GamePathIngredients})",
                         LogType.Warning, true);
            // Show action dialog
            switch (await Dialog_PreviousGameConversionFailed(ParentUI))
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

        #endregion

        #region Private Methods - Utilities

        private string GetFailedGameConversionFolder(string basepath)
        {
            try
            {
                // Step back once from the game directory
                string ParentPath = Path.GetDirectoryName(basepath);
                // Get the ingredient path
                if (ParentPath != null)
                {
                    string IngredientPath = Directory
                                           .EnumerateDirectories(ParentPath,
                                                                 $"{GameVersionManager.GamePreset.GameDirectoryName}*_ConvertedTo-*_Ingredients",
                                                                 SearchOption.TopDirectoryOnly)
                                           .FirstOrDefault();
                    // If the path is not null, then return
                    if (IngredientPath is not null)
                    {
                        return IngredientPath;
                    }
                }
            }
        #if DEBUG
            catch (DirectoryNotFoundException)
            {
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
            #else
            catch
            {
            #endif
            }

            // If not, then return null (not found)
            return null;
        }

        #endregion

        #region Override Methods - UninstallGame

        protected override UninstallGameProperty AssignUninstallFolders()
        {
            return new UninstallGameProperty
            {
                gameDataFolderName  = "BH3_Data",
                foldersToDelete     = ["BH3_Data", "AntiCheatExpert"],
                filesToDelete       = ["ACE-BASE.sys", "bugtrace.dll", "pkg_version", "UnityPlayer.dll", "config.ini"],
                foldersToKeepInData = []
            };
        }

        #endregion

        #region Override Methods - CleanUpGameFiles

        protected override bool IsCategorizedAsGameFile(FileInfo fileInfo, string gamePath, bool includeZipCheck,
                                                        GameInstallStateEnum gameState, out LocalFileInfo localFileInfo)
        {
            // Convert to LocalFileInfo and get the relative path
            localFileInfo = new LocalFileInfo(fileInfo, gamePath);
            string             relativePath     = localFileInfo.RelativePath;
            ReadOnlySpan<char> relativePathSpan = relativePath;
            string             fileName         = localFileInfo.FileName;
            string             gameFolder       = _uninstallGameProperty?.gameDataFolderName ?? string.Empty;

            // 1st check: Ensure that the file is not a config or pkg_version file
            if (relativePathSpan.EndsWith("config.ini",     StringComparison.OrdinalIgnoreCase)
                || relativePathSpan.EndsWith("pkg_version", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 2nd check: Ensure that the file is not a web cache file
            if (relativePathSpan.Contains("webCache",    StringComparison.OrdinalIgnoreCase)
                || relativePathSpan.Contains("SDKCache", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 3rd check: Ensure that the file isn't in excluded list
            if (_uninstallGameProperty?.foldersToKeepInData
                                       .Any(x => relativePath
                                                .AsSpan() // As Span<T> since StartsWith() in it is typically faster
                                                 // than the one from String primitive
                                                .Contains(x.AsSpan(), StringComparison.OrdinalIgnoreCase)) ?? false)
            {
                return false; // Return false if it's not actually in excluded list
            }

            // 4th check: Ensure that the file is not a StreamingAssets file
            if (relativePathSpan.Contains("StreamingAssets", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 5th check: Ensure if the path includes the folder name at start
            if (!string.IsNullOrEmpty(gameFolder) && relativePathSpan
                   .StartsWith(gameFolder, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 6th check: Ensure that the file is one of the files included
            //            in the Regex pattern list
            if (_uninstallGameProperty?.filesToDelete
                                       .Any(pattern => Regex.IsMatch(fileName,
                                                                     pattern,
                                                                     RegexOptions.Compiled |
                                                                     RegexOptions.NonBacktracking
                                                                    )) ?? false)
            {
                return true;
            }

            // 7th check: Ensure that the file is one of package files
            if (includeZipCheck && Regex.IsMatch(fileName,
                                                 NonGameFileRegexPattern,
                                                 RegexOptions.Compiled |
                                                 RegexOptions.NonBacktracking
                                                ))
            {
                return true;
            }

            switch (gameState)
            {
                // 8th check: Ensure that the file is Sophon Chunk file
                // if game state is installed.
                case GameInstallStateEnum.Installed
                    when localFileInfo.RelativePath.StartsWith("chunk_collapse", StringComparison.OrdinalIgnoreCase):
                // 9th check: If ACE-BASE.sys is detected
                // AND game state is installed.
                case GameInstallStateEnum.Installed when
                    localFileInfo.RelativePath.EndsWith("ACE-BASE.sys", StringComparison.OrdinalIgnoreCase):
                    return true;
                default:
                    // If all those matches failed, then return them as a non-game file
                    return false;
            }
        }

        #endregion
    }
}