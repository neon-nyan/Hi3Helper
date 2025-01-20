using CollapseLauncher.GameVersioning;
using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Hi3Helper.Locale;

namespace CollapseLauncher
{
    internal partial class StarRailCache(UIElement parentUI, IGameVersionCheck gameVersionManager)
        : ProgressBase<SRAsset>(parentUI,
                                gameVersionManager,
                                gameVersionManager.GameDirPath,
                                null,
                                gameVersionManager.GetGameVersionApi()?.VersionString), ICache
    {
        #region Properties
        private            GameTypeStarRailVersion InnerGameVersionManager { get; } = gameVersionManager as GameTypeStarRailVersion;
        private            List<SRAsset>           UpdateAssetIndex        { get; set; }
        protected override string                  UserAgent               { get; set; } = "UnityPlayer/2019.4.34f1 (UnityWebRequest/1.0, libcurl/7.75.0-DEV)";
        #endregion

        ~StarRailCache() => Dispose();

        public async Task<bool> StartCheckRoutine(bool useFastCheck)
        {
            UseFastMethod = useFastCheck;
            return await TryRunExamineThrow(CheckRoutine());
        }

        private async Task<bool> CheckRoutine()
        {
            // Initialize _updateAssetIndex
            UpdateAssetIndex = [];

            // Reset status and progress
            ResetStatusAndProgress();

            // Step 1: Fetch asset indexes
            AssetIndex = await Fetch(Token.Token);

            // Step 2: Start assets checking
            UpdateAssetIndex = await Check(AssetIndex, Token.Token);

            // Step 3: Summarize and returns true if the assetIndex count != 0 indicates caches needs to be update.
            //         either way, returns false.
            return SummarizeStatusAndProgress(
                UpdateAssetIndex,
                string.Format(Lang._CachesPage.CachesStatusNeedUpdate, ProgressAllCountFound, ConverterTool.SummarizeSizeSimple(ProgressAllSizeFound)),
                Lang._CachesPage.CachesStatusUpToDate);
        }

        public async Task StartUpdateRoutine(bool showInteractivePrompt = false)
        {
            if (UpdateAssetIndex.Count == 0) throw new InvalidOperationException("There's no cache file need to be update! You can't do the update process!");

            _ = await TryRunExamineThrow(UpdateRoutine());
        }

        private async Task<bool> UpdateRoutine()
        {
            // Assign update task
            Task<bool> updateTask = Update(UpdateAssetIndex, AssetIndex, Token.Token);

            // Run update process
            bool updateTaskSuccess = await TryRunExamineThrow(updateTask);

            // Reset status and progress
            ResetStatusAndProgress();

            // Set as completed
            Status.IsCompleted = true;
            Status.IsCanceled = false;
            Status.ActivityStatus = Lang._CachesPage.CachesStatusUpToDate;

            // Update status and progress
            UpdateAll();

            // Clean up _updateAssetIndex
            UpdateAssetIndex.Clear();

            return updateTaskSuccess;
        }

        public StarRailCache AsBaseType() => this;

        public void CancelRoutine()
        {
            Token.Cancel();
        }

        public void Dispose()
        {
            CancelRoutine();
            GC.SuppressFinalize(this);
        }
    }
}
