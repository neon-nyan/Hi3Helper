using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.Http;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
// ReSharper disable IdentifierTypo

namespace CollapseLauncher
{
    internal partial class GenshinRepair
    {
        private async Task<bool> Repair(List<PkgVersionProperties> repairAssetIndex, CancellationToken token)
        {
            // Set total activity string as "Waiting for repair process to start..."
            Status.ActivityStatus = Lang._GameRepairPage.Status11;
            Status.IsProgressAllIndetermined = true;
            Status.IsProgressPerFileIndetermined = true;

            // Update status
            UpdateStatus();

            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder<SocketsHttpHandler>()
                .UseLauncherConfig(DownloadThreadCount + DownloadThreadCountReserved)
                .SetUserAgent(UserAgent)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Use the new DownloadClient instance
            DownloadClient downloadClient = DownloadClient.CreateInstance(client);

            // Iterate repair asset
            ObservableCollection<IAssetProperty> assetProperty = [.. AssetEntry];
            if (IsBurstDownloadEnabled)
            {
                ConcurrentDictionary<(PkgVersionProperties, IAssetProperty), byte> processingAsset = new();
                await Parallel.ForEachAsync(
                    PairEnumeratePropertyAndAssetIndexPackage(
#if ENABLEHTTPREPAIR
                    EnforceHttpSchemeToAssetIndex(repairAssetIndex)
#else
                    repairAssetIndex
#endif
                    , assetProperty),
                    new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = DownloadThreadCount },
                    async (asset, innerToken) =>
                    {
                        if (!processingAsset.TryAdd(asset, 0))
                        {
                            LogWriteLine($"Asset {asset.AssetIndex.localName} is already being processed, skipping...",
                                         LogType.Warning, true);
                            return;
                        }
                        await RepairAssetTypeGeneric(asset, downloadClient, _httpClient_RepairAssetProgress, innerToken);
                        processingAsset.TryRemove(asset, out _);
                    });
            }
            else
            {
                ConcurrentDictionary<(PkgVersionProperties, IAssetProperty), byte> processingAsset = new();
                foreach ((PkgVersionProperties AssetIndex, IAssetProperty AssetProperty) asset in
                    PairEnumeratePropertyAndAssetIndexPackage(
#if ENABLEHTTPREPAIR
                    EnforceHttpSchemeToAssetIndex(repairAssetIndex)
#else
                    repairAssetIndex
#endif
                    , assetProperty))
                {
                    if (!processingAsset.TryAdd(asset, 0))
                    {
                        LogWriteLine($"Asset {asset.AssetIndex.localName} is already being processed, skipping...",
                                     LogType.Warning, true);
                        continue;
                    }
                    await RepairAssetTypeGeneric(asset, downloadClient, _httpClient_RepairAssetProgress, token);
                    processingAsset.TryRemove(asset, out _);
                }
            }

            return true;
        }

        #region GenericRepair
        private async Task RepairAssetTypeGeneric((PkgVersionProperties AssetIndex, IAssetProperty AssetProperty) asset, DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token)
        {
            // Increment total count current
            ProgressAllCountCurrent++;
            // Set repair activity status
            string timeLeftString = string.Format(Lang!._Misc!.TimeRemainHMSFormat!, Progress.ProgressAllTimeLeft);
            UpdateRepairStatus(
                string.Format(Lang._GameRepairPage.Status8, asset.AssetIndex.remoteName),
                string.Format(Lang._GameRepairPage.PerProgressSubtitle2, ConverterTool.SummarizeSizeSimple(ProgressAllSizeCurrent), ConverterTool.SummarizeSizeSimple(ProgressAllSizeTotal)) + $" | {timeLeftString}",
                true);

            string   assetPath     = ConverterTool.NormalizePath(asset.AssetIndex.localName);
            FileInfo assetFileInfo = new FileInfo(assetPath).EnsureCreationOfDirectory().EnsureNoReadOnly();

            // If file is unused, then delete
            if (asset.AssetIndex.type == "Unused")
            {
                // Delete the file
                assetFileInfo.Delete();
            }
            else
            {
                // or start asset download task
                await RunDownloadTask(asset.AssetIndex.fileSize, assetFileInfo, asset.AssetIndex.remoteURL, downloadClient, downloadProgress, token);
                LogWriteLine($"File [T: {RepairAssetType.Generic}] {asset.AssetIndex.remoteName} has been downloaded!", LogType.Default, true);
            }

            // Pop repair asset display entry
            PopRepairAssetEntry(asset.AssetProperty);
        }
        #endregion
    }
}
