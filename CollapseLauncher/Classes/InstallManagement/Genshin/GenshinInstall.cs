using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.SentryHelper;
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

namespace CollapseLauncher.InstallManager.Genshin
{
    internal sealed partial class GenshinInstall : InstallManagerBase
    {
        #region Override Properties

        protected override int _gameVoiceLanguageID => GameVersionManager.GamePreset.GetVoiceLanguageID();

        #endregion

        #region Properties

        protected override string _gameAudioLangListPath
        {
            get
            {
                // If the persistent folder is not exist, then return null
                if (!Directory.Exists(_gameDataPersistentPath))
                {
                    return null;
                }

                // Try get the file list
                string[] audioPath =
                    Directory.GetFiles(_gameDataPersistentPath, "audio_lang_*", SearchOption.TopDirectoryOnly);
                // If the path is null or has no length, then return null
                return audioPath.Length == 0 ? null :
                    // If not, then return the first path
                    audioPath[0];
            }
        }

        protected override string _gameAudioLangListPathStatic =>
            Path.Combine(_gameDataPersistentPath, "audio_lang_14");

        private string _gameAudioNewPath => Path.Combine(_gameDataPath, "StreamingAssets", "AudioAssets");

        private string _gameAudioOldPath =>
            Path.Combine(_gameDataPath, "StreamingAssets", "Audio", "GeneratedSoundBanks", "Windows");

        #endregion

        public GenshinInstall(UIElement parentUI, IGameVersionCheck GameVersionManager)
            : base(parentUI, GameVersionManager)
        {
        }

        #region Public Methods

#nullable enable
        public override async ValueTask<bool> IsPreloadCompleted(CancellationToken token)
        {
            // Get the primary file first check
            List<RegionResourceVersion>? resource = GameVersionManager.GetGamePreloadZip();

            // Sanity Check: throw if resource returns null
            if (resource == null)
            {
                throw new
                    InvalidOperationException("You're trying to check this method while preload is not available!");
            }

            bool primaryAsset = resource.All(x =>
                                             {
                                                 string? name = Path.GetFileName(x.path);
                                                 if (string.IsNullOrEmpty(name))
                                                     return false;

                                                 string path = Path.Combine(GamePath, name);
                                                 return File.Exists(path);
                                             });

            // Get the second voice pack check
            List<GameInstallPackage> voicePackList = [];

            // Add another voice pack that already been installed
            await TryAddOtherInstalledVoicePacks(resource.FirstOrDefault()?.voice_packs, voicePackList,
                                                 resource.FirstOrDefault()?.version);

            // Get the secondary file check
            bool secondaryAsset = voicePackList.All(x => File.Exists(x.PathOutput));

            return (primaryAsset && secondaryAsset) || await base.IsPreloadCompleted(token);
        }
#nullable restore
        #endregion

        #region Override Methods - StartPackageInstallationInner

        protected override async Task StartPackageInstallationInner(List<GameInstallPackage> gamePackage = null,
                                                                    bool isOnlyInstallPackage = false,
                                                                    bool doNotDeleteZipExplicit = false)
        {
            if (!isOnlyInstallPackage)
                // Starting from 3.6 update, the Audio files have been moved to "AudioAssets" folder
            {
                EnsureMoveOldToNewAudioDirectory();
            }

            // Run the base installation process
            await base.StartPackageInstallationInner(gamePackage);

            // Then start on processing hdifffiles list and deletefiles list
            await ApplyHdiffListPatch();
            await ApplyDeleteFileActionAsync(Token.Token);
        }

        private void EnsureMoveOldToNewAudioDirectory()
        {
            // Return if the old path doesn't exist
            if (!Directory.Exists(_gameAudioOldPath))
            {
                return;
            }

            // If it exists, then enumerate the content of it and do move operation
            int offset = _gameAudioOldPath.Length + 1;
            foreach (string oldPath in Directory.EnumerateFiles(_gameAudioOldPath, "*", SearchOption.AllDirectories))
            {
                string basePath  = oldPath.AsSpan()[offset..].ToString();
                string newPath   = EnsureCreationOfDirectory(Path.Combine(_gameAudioNewPath, basePath));

                FileInfo oldFileInfo = new FileInfo(oldPath)
                {
                    IsReadOnly = false
                };
                oldFileInfo.MoveTo(newPath, true);
            }

            try
            {
                // Then if all the files are already moved, delete the old path
                if (Directory.Exists(_gameAudioOldPath))
                {
                    Directory.CreateDirectory(_gameAudioOldPath);
                }
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed while deleting old audio folder: {_gameAudioOldPath}\r\n{ex}", LogType.Error,
                             true);
            }
        }

        #endregion

        #region Override Methods - UninstallGame

        protected override UninstallGameProperty AssignUninstallFolders()
        {
            string execName = GameVersionManager.GamePreset.ZoneName switch
            {
                "Global" => "GenshinImpact",
                "Mainland China" => "YuanShen",
                "Bilibili" => "YuanShen",
                "Google Play" => "GenshinImpact",
                _ => throw new NotSupportedException($"Unknown GI Game Region!: {GameVersionManager.GamePreset.ZoneName}")
            };

            return new UninstallGameProperty
            {
                gameDataFolderName = $"{execName}_Data",
                foldersToDelete    = [$"{execName}_Data"],
                filesToDelete =
                [
                    "HoYoKProtect.sys", "pkg_version", $"{execName}.exe", "UnityPlayer.dll", "config.ini", "^mhyp.*",
                    "^Audio.*"
                ],
                foldersToKeepInData = []
            };
        }

        #endregion
    }
}