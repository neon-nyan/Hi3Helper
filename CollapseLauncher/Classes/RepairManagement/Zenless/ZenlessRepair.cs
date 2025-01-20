using CollapseLauncher.GameSettings.Zenless;
using CollapseLauncher.GameVersioning;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher
{
    internal partial class ZenlessRepair : ProgressBase<FilePropertiesRemote>, IRepair, IRepairAssetIndex
    {
        #region Properties
        internal const string AssetGamePersistentPath = @"{0}_Data\Persistent";

        // ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
        private bool IsOnlyRecoverMain { get; set; }
        private bool IsCacheUpdateMode { get; set; }
        private string? ExecutableName { get; set; }
        // ReSharper restore AutoPropertyCanBeMadeGetOnly.Local
        
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        private List<FilePropertiesRemote>? OriginAssetIndex { get; set; }
        private GameTypeZenlessVersion? GameVersionManagerCast { get => GameVersionManager as GameTypeZenlessVersion; }

        private string GameDataPersistentPath { get => Path.Combine(GamePath, string.Format(AssetGamePersistentPath, ExecutableName)); }

        protected string? GameAudioLangListPath
        {
            get
            {
                // If the persistent folder is not exist, then return null
                if (!Directory.Exists(GameDataPersistentPath))
                    return null;

                // Get the audio lang path index
                string audioLangPath = GameAudioLangListPathStatic;
                return File.Exists(audioLangPath) ? audioLangPath : null;
            }
        }

        private string? GameAudioLangListPathAlternate
        {
            get
            {
                // If the persistent folder is not exist, then return null
                if (!Directory.Exists(GameDataPersistentPath))
                    return null;

                // Get the audio lang path index
                string audioLangPath = GameAudioLangListPathAlternateStatic;
                return File.Exists(audioLangPath) ? audioLangPath : null;
            }
        }

        private string GameAudioLangListPathStatic =>
            Path.Combine(GameDataPersistentPath, "audio_lang_launcher");
        private string GameAudioLangListPathAlternateStatic =>
            Path.Combine(GameDataPersistentPath, "audio_lang");

        protected override string UserAgent { get; set; } = "UnityPlayer/2019.4.40f1 (UnityWebRequest/1.0, libcurl/7.80.0-DEV)";

        public ZenlessRepair(UIElement parentUI, IGameVersionCheck gameVersionManager, ZenlessSettings gameSettings, bool isOnlyRecoverMain = false, string? versionOverride = null, bool isCacheUpdateMode = false)
            : base(parentUI, gameVersionManager, null, "", versionOverride)
        {
            // Use IsOnlyRecoverMain for future delta-patch or main game only files
            IsOnlyRecoverMain = isOnlyRecoverMain;
            // We are merging cache functionality with cache update
            IsCacheUpdateMode = isCacheUpdateMode;
            ExecutableName = Path.GetFileNameWithoutExtension(gameVersionManager.GamePreset.GameExecutableName);
            GameSettings = gameSettings;
        }
        #endregion

        #region Public Methods

        ~ZenlessRepair() => Dispose();

        public List<FilePropertiesRemote> GetAssetIndex() => OriginAssetIndex!;

        public async Task<bool> StartCheckRoutine(bool useFastCheck)
        {
            UseFastMethod = useFastCheck;
            return await TryRunExamineThrow(CheckRoutine());
        }

        public async Task StartRepairRoutine(bool showInteractivePrompt = false, Action? actionIfInteractiveCancel = null)
        {
            if (AssetIndex.Count == 0) throw new InvalidOperationException("There's no broken file being reported! You can't do the repair process!");

            if (showInteractivePrompt)
            {
                await SpawnRepairDialog(AssetIndex, actionIfInteractiveCancel);
            }

            _ = await TryRunExamineThrow(RepairRoutine());
        }

        private async Task<bool> CheckRoutine()
        {
            // Always clear the asset index list
            AssetIndex.Clear();

            // Reset status and progress
            ResetStatusAndProgress();

            // Step 1: Fetch asset indexes
            await Fetch(AssetIndex, Token.Token);

            // Step 2: Calculate the total size and count of the files
            CountAssetIndex(AssetIndex);

            // Step 3: Check for the asset indexes integrity
            await Check(AssetIndex, Token.Token);

            // Step 4: Summarize and returns true if the assetIndex count != 0 indicates broken file was found.
            //         either way, returns false.
            string status3Msg = IsCacheUpdateMode ? Locale.Lang._CachesPage.CachesStatusNeedUpdate : Locale.Lang._GameRepairPage.Status3;
            string status4Msg = IsCacheUpdateMode ? Locale.Lang._CachesPage.CachesStatusUpToDate : Locale.Lang._GameRepairPage.Status4;
            return SummarizeStatusAndProgress(
                AssetIndex,
                string.Format(status3Msg, ProgressAllCountFound, ConverterTool.SummarizeSizeSimple(ProgressAllSizeFound)),
                status4Msg);
        }

        private async Task<bool> RepairRoutine()
        {
            // Assign repair task
            Task<bool> repairTask = Repair(AssetIndex, Token.Token);

            // Run repair process
            bool repairTaskSuccess = await TryRunExamineThrow(repairTask);

            // Reset status and progress
            ResetStatusAndProgress();

            // Set as completed
            Status.IsCompleted = true;
            Status.IsCanceled = false;
            Status.ActivityStatus = IsCacheUpdateMode ? Locale.Lang._CachesPage.CachesStatusUpToDate : Locale.Lang._GameRepairPage.Status7;
            
            // Update status and progress
            UpdateAll();

            return repairTaskSuccess;
        }

        public void CancelRoutine()
        {
            // Trigger token cancellation
            Token.Cancel();
        }

        public void Dispose()
        {
            CancelRoutine();
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
