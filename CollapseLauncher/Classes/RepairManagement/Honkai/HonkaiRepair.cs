using CollapseLauncher.GameSettings.Honkai;
using CollapseLauncher.Interfaces;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.EncTool.Parser.KianaDispatch;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
// ReSharper disable IdentifierTypo

namespace CollapseLauncher
{
    internal partial class HonkaiRepair : ProgressBase<FilePropertiesRemote>, IRepair, IRepairAssetIndex
    {
        #region Properties
        private const    string        AssetBasePath   = "BH3_Data/StreamingAssets/";
        private readonly string[]      _skippableAssets = ["CG_Temp.usm$Generic", "BlockMeta.xmf$Generic", "Blocks.xmf$Generic"
        ];
        private        HonkaiCache   CacheUtil             { get; }
        private        string        AssetBaseURL          { get; set; }
        private        string        BlockBaseURL          { get => ConverterTool.CombineURLFromString(AssetBaseURL,      $"StreamingAsb/{string.Join('_', GameVersion.VersionArray)}/pc/HD"); }
        private        string        BlockAsbBaseURL       { get => ConverterTool.CombineURLFromString(BlockBaseURL,      "/asb"); }
        private        string        BlockPatchBaseURL     { get => ConverterTool.CombineURLFromString(BlockBaseURL,      "/patch"); }
        private        string        BlockPatchDiffBaseURL { get => ConverterTool.CombineURLFromString(BlockPatchBaseURL, "/{0}"); }
        private static string        BlockPatchDiffPath    { get => ConverterTool.CombineURLFromString(AssetBasePath,     "Asb/pc/Patch"); }
        private static string        BlockBasePath         { get => ConverterTool.CombineURLFromString(AssetBasePath,     "Asb/pc/"); }
        private        bool          IsOnlyRecoverMain     { get; }
        private        KianaDispatch GameServer            { get; set; }
        #endregion

        #region ExtensionProperties
        private protected AudioLanguageType AudioLanguage { get; set; }
        private protected static string AudioBaseLocalPath { get => ConverterTool.CombineURLFromString(AssetBasePath, "Audio/GeneratedSoundBanks/Windows/"); }
        private protected string AudioBaseRemotePath { get => ConverterTool.CombineURLFromString(AssetBaseURL, "Audio/Windows/{0}/{1}/"); }
        private protected static string AudioPatchBaseLocalPath { get => ConverterTool.CombineURLFromString(AudioBaseLocalPath, "Patch/"); }
        private protected string AudioPatchBaseRemotePath { get => ConverterTool.CombineURLFromString(AudioBaseRemotePath, "Patch/"); }
        private protected static string VideoBaseLocalPath { get => ConverterTool.CombineURLFromString(AssetBasePath, "Video/"); }
        private protected List<FilePropertiesRemote> OriginAssetIndex { get; set; }
        #endregion

        public HonkaiRepair(UIElement parentUI, IGameVersionCheck gameVersionManager, ICache gameCacheManager, IGameSettings gameSettings, bool onlyRecoverMainAsset = false, string versionOverride = null)
            : base(parentUI, gameVersionManager, gameSettings, null, "", versionOverride)
        {
            CacheUtil = (gameCacheManager as ICacheBase<HonkaiCache>)?.AsBaseType();

            // Get flag to only recover main assets
            IsOnlyRecoverMain = onlyRecoverMainAsset;

            // Initialize audio asset language
            string audioLanguage = (gameSettings as HonkaiSettings)?.SettingsAudio._userCVLanguage;
            AudioLanguage = audioLanguage switch
                             {
                                 "Chinese(PRC)" => AudioLanguageType.Chinese,
                                 _ => GameVersionManager.GamePreset.GameDefaultCVLanguage
                             };
        }

        ~HonkaiRepair() => Dispose();

        public List<FilePropertiesRemote> GetAssetIndex() => OriginAssetIndex;

        public async Task<bool> StartCheckRoutine(bool useFastCheck)
        {
            UseFastMethod = useFastCheck;
            return await TryRunExamineThrow(CheckRoutine());
        }

        public async Task StartRepairRoutine(bool showInteractivePrompt = false, Action actionIfInteractiveCancel = null)
        {
            if (AssetIndex.Count == 0) throw new InvalidOperationException("There's no broken file being reported! You can't do the repair process!");

            if (showInteractivePrompt && actionIfInteractiveCancel != null)
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

            // Copy list to _originAssetIndex
            OriginAssetIndex = [..AssetIndex];

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
            Status.IsCompleted    = true;
            Status.IsCanceled     = false;
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
