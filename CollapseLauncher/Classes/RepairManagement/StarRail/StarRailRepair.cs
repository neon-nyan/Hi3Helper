using CollapseLauncher.GameVersioning;
using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher
{
    internal partial class StarRailRepair : ProgressBase<FilePropertiesRemote>, IRepair, IRepairAssetIndex
    {
        #region Properties
        private GameTypeStarRailVersion    InnerGameVersionManager { get; }
        private bool                       IsOnlyRecoverMain       { get; }
        private List<FilePropertiesRemote> OriginAssetIndex        { get; set; }
        private string                     ExecName                { get; }
        private string                     GameDataPersistentPath  { get => Path.Combine(GamePath, $"{ExecName}_Data", "Persistent"); }
        private string GameAudioLangListPath
        {
            get
            {
                // If the persistent folder is not exist, then return null
                if (!Directory.Exists(GameDataPersistentPath)) return null;

                // Set the file list path
                string audioRecordPath = Path.Combine(GameDataPersistentPath, "AudioLaucherRecord.txt");

                // Check if the file exist. If not, return null
                return !File.Exists(audioRecordPath) ? null :
                    // If it exists, then return the path
                    audioRecordPath;
            }
        }
        private string GameAudioLangListPathStatic { get => Path.Combine(GameDataPersistentPath, "AudioLaucherRecord.txt"); }

        internal const string AssetGameAudioStreamingPath = @"{0}_Data\StreamingAssets\Audio\AudioPackage\Windows";
        internal const string AssetGameAudioPersistentPath = @"{0}_Data\Persistent\Audio\AudioPackage\Windows";

        internal const string AssetGameBlocksStreamingPath = @"{0}_Data\StreamingAssets\Asb\Windows";
        internal const string AssetGameBlocksPersistentPath = @"{0}_Data\Persistent\Asb\Windows";

        internal const string AssetGameVideoStreamingPath = @"{0}_Data\StreamingAssets\Video\Windows";
        internal const string AssetGameVideoPersistentPath = @"{0}_Data\Persistent\Video\Windows";
        protected override string UserAgent { get; set; } = "UnityPlayer/2019.4.34f1 (UnityWebRequest/1.0, libcurl/7.75.0-DEV)";
        #endregion

        public StarRailRepair(UIElement parentUI, IGameVersionCheck gameVersionManager, bool onlyRecoverMainAsset = false, string versionOverride = null)
            : base(parentUI, gameVersionManager, null, "", versionOverride)
        {
            // Get flag to only recover main assets
            IsOnlyRecoverMain = onlyRecoverMainAsset;
            InnerGameVersionManager = gameVersionManager as GameTypeStarRailVersion;
            ExecName = Path.GetFileNameWithoutExtension(InnerGameVersionManager!.GamePreset.GameExecutableName);
        }

        ~StarRailRepair() => Dispose();

        public List<FilePropertiesRemote> GetAssetIndex() => OriginAssetIndex;

        public async Task<bool> StartCheckRoutine(bool useFastCheck)
        {
            UseFastMethod = useFastCheck;
            return await TryRunExamineThrow(CheckRoutine());
        }

        public async Task StartRepairRoutine(bool showInteractivePrompt = false, Action actionIfInteractiveCancel = null)
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
            return SummarizeStatusAndProgress(
                AssetIndex,
                string.Format(Lang._GameRepairPage.Status3, ProgressAllCountFound, ConverterTool.SummarizeSizeSimple(ProgressAllSizeFound)),
                Lang._GameRepairPage.Status4);
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
            Status.ActivityStatus = Lang._GameRepairPage.Status7;

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
    }
}
