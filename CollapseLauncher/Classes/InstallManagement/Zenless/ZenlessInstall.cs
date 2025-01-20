using CollapseLauncher.GameSettings.Zenless;
using CollapseLauncher.GameSettings.Zenless.Enums;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// ReSharper disable GrammarMistakeInComment
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace
// ReSharper disable ArrangeObjectCreationWhenTypeEvident
// ReSharper disable EmptyRegion
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor

#nullable enable
namespace CollapseLauncher.InstallManager.Zenless
{
    internal sealed class ZenlessInstall : InstallManagerBase
    {
        #region Private Properties
        private ZenlessSettings? ZenlessSettings          { get; }
        private ZenlessRepair?   ZenlessGameRepairManager { get; set; }
        #endregion

        #region Override Properties
        protected override int _gameVoiceLanguageID
        {
            get => ZenlessSettings?.GeneralData?.DeviceLanguageVoiceType switch
            {
                LanguageVoice.zh_cn => 0,
                LanguageVoice.en_us => 1,
                LanguageVoice.ja_jp => 2,
                LanguageVoice.ko_kr => 3,
                LanguageVoice.Unset => 2,  // Default to ja_jp
                _ => throw new NotSupportedException("Type of the language voice type is not valid!")
            };
        }

        protected override string? _gameAudioLangListPath
        {
            get
            {
                // If the persistent folder is not exist, then return null
                if (!Directory.Exists(_gameDataPersistentPath))
                    return null;

                // Get the audio lang path index
                string audioLangPath = _gameAudioLangListPathStatic;
                return File.Exists(audioLangPath) ? audioLangPath : null;
            }
        }

        protected override string _gameAudioLangListPathStatic =>
            Path.Combine(_gameDataPersistentPath, "audio_lang_launcher");
        private            string _gameAudioLangListPathAlternateStatic =>
            Path.Combine(_gameDataPersistentPath, "audio_lang");
        #endregion

        public ZenlessInstall(UIElement parentUI, IGameVersionCheck GameVersionManager, ZenlessSettings zenlessSettings)
            : base(parentUI, GameVersionManager)
        {
            ZenlessSettings = zenlessSettings;
        }

        #region Override Methods - StartPackageInstallationInner

        public override async ValueTask<int> StartPackageVerification(List<GameInstallPackage> gamePackage)
        {
            IsRunning = true;

            // Get the delta patch confirmation if the property is not null
            if (_gameDeltaPatchProperty == null)
                return await base.StartPackageVerification(gamePackage);

            // If the confirm is 1 (verified) or -1 (cancelled), then return the code
            int deltaPatchConfirm = await ConfirmDeltaPatchDialog(_gameDeltaPatchProperty,
                                                                  ZenlessGameRepairManager = GetGameRepairInstance(_gameDeltaPatchProperty.SourceVer) as ZenlessRepair);
            if (deltaPatchConfirm is -1 or 1)
            {
                return deltaPatchConfirm;
            }

            // If no delta patch is happening as deltaPatchConfirm returns 0 (normal update), then do the base verification
            return await base.StartPackageVerification(gamePackage);
        }

        protected override IRepair GetGameRepairInstance(string? versionString) =>
            new ZenlessRepair(ParentUI,
                               GameVersionManager, ZenlessSettings!, true,
                               versionString);

        protected override async Task StartPackageInstallationInner(List<GameInstallPackage>? gamePackage = null,
                                                                    bool isOnlyInstallPackage = false,
                                                                    bool doNotDeleteZipExplicit = false)
        {
            // If the delta patch is performed, then return
            if (!isOnlyInstallPackage && await StartDeltaPatch(ZenlessGameRepairManager, false, true))
            {
                // Assign the game package to delta-patch requirement list
                // and start the additional patching process (like Audio patch, etc)
                gamePackage ??= _gameDeltaPatchPreReqList;
            }

            // Run the base installation process
            await base.StartPackageInstallationInner(gamePackage, isOnlyInstallPackage, doNotDeleteZipExplicit);

            // Then start on processing hdifffiles list and deletefiles list
            await ApplyHdiffListPatch();
            await ApplyDeleteFileActionAsync(Token.Token);

            // Update the audio lang list if not in isOnlyInstallPackage mode
            if (!isOnlyInstallPackage)
            {
                WriteAudioLangList(AssetIndex);
            }
        }
        #endregion

        #region Override Methods - Audio Lang List
        protected override void WriteAudioLangList(List<GameInstallPackage> gamePackage)
        {
            // Run the writing method from the base first
            base.WriteAudioLangList(gamePackage);

            // Then create the one from the alternate one
            // Read all the existing list
            List<string> langList = File.Exists(_gameAudioLangListPathAlternateStatic)
                ? File.ReadAllLines(_gameAudioLangListPathAlternateStatic).ToList()
                : [];

            // Try lookup if there is a new language list, then add it to the list
            foreach (GameInstallPackage package in
                     AssetIndex.Where(x => x.PackageType == GameInstallPackageType.Audio))
            {
                string langString = GetLanguageStringByLocaleCodeAlternate(package.LanguageID);
                if (!langList.Contains(langString, StringComparer.OrdinalIgnoreCase))
                {
                    langList.Add(langString);
                }
            }

            // Create the audio lang list file
            using StreamWriter sw = new StreamWriter(_gameAudioLangListPathAlternateStatic,
                                                     new FileStreamOptions
                                                     { Mode = FileMode.Create, Access = FileAccess.Write });
            // Iterate the package list
            foreach (string langString in langList)
            {
                // Write the language string as per ID
                sw.WriteLine(langString);
            }
        }

        protected override void WriteAudioLangListSophon(List<string> sophonVOList)
        {
            // Run the writing method from the base first
            base.WriteAudioLangListSophon(sophonVOList);

            // Then create the one from the alternate one
            // Read all the existing list
            List<string> langList = File.Exists(_gameAudioLangListPathAlternateStatic)
                ? File.ReadAllLines(_gameAudioLangListPathAlternateStatic).ToList()
                : [];

            // Try lookup if there is a new language list, then add it to the list
            for (int index = sophonVOList.Count - 1; index >= 0; index--)
            {
                var packageLocaleCodeString = sophonVOList[index];
                string langString = GetLanguageStringByLocaleCodeAlternate(packageLocaleCodeString);
                if (!langList.Contains(langString, StringComparer.OrdinalIgnoreCase))
                {
                    langList.Add(langString);
                }
            }

            // Create the audio lang list file
            using var sw = new StreamWriter(_gameAudioLangListPathAlternateStatic,
                                            new FileStreamOptions
                                            { Mode = FileMode.Create, Access = FileAccess.Write });
            // Iterate the package list
            foreach (var voIds in langList)
            // Write the language string as per ID
            {
                sw.WriteLine(voIds);
            }
        }

        private static string GetLanguageStringByLocaleCodeAlternate(string localeCode)
        {
            return localeCode switch
            {
                "zh-cn" => "Cn",
                "en-us" => "En",
                "ja-jp" => "Jp",
                "ko-kr" => "Kr",
                _ => throw new KeyNotFoundException($"Alternate locale code: {localeCode} is not supported!")
            };
        }
        #endregion

        #region Override Methods - UninstallGame
        protected override UninstallGameProperty AssignUninstallFolders()
        {
            return new UninstallGameProperty
            {
                gameDataFolderName = "ZenlessZoneZero_Data",
                foldersToDelete    = ["APMCrashReporter"],
                filesToDelete =
                [
                    "mhypbase.dll", "HoYoKProtect.sys", "GameAssembly.dll", "pkg_version", "config.ini", "^ZZZ.*",
                    "^Unity.*"
                ],
                foldersToKeepInData = ["ScreenShots"]
            };
        }
        #endregion
    }
}