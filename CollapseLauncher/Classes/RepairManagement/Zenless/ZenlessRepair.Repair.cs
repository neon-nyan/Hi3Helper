using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    internal partial class ZenlessRepair
    {
        private async Task<bool> Repair(List<FilePropertiesRemote> repairAssetIndex, CancellationToken token)
        {
            // Set total activity string as "Waiting for repair process to start..."
            Status.ActivityStatus = Locale.Lang._GameRepairPage.Status11;
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

            // Iterate repair asset and check it using different method for each type
            ObservableCollection<IAssetProperty> assetProperty = [.. AssetEntry];
            if (IsBurstDownloadEnabled)
            {
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
                        // Assign a task depends on the asset type
                        Task assetTask = asset.AssetIndex.FT switch
                        {
                            FileType.Block => RepairAssetTypeGeneric(asset, downloadClient, IsCacheUpdateMode ? _httpClient_UpdateAssetProgress : _httpClient_RepairAssetProgress, innerToken),
                            FileType.Audio => RepairAssetTypeGeneric(asset, downloadClient, IsCacheUpdateMode ? _httpClient_UpdateAssetProgress : _httpClient_RepairAssetProgress, innerToken),
                            FileType.Video => RepairAssetTypeGeneric(asset, downloadClient, IsCacheUpdateMode ? _httpClient_UpdateAssetProgress : _httpClient_RepairAssetProgress, innerToken),
                            _ => RepairAssetTypeGeneric(asset, downloadClient, IsCacheUpdateMode ? _httpClient_UpdateAssetProgress : _httpClient_RepairAssetProgress, innerToken)
                        };

                        // Await the task
                        await assetTask;
                    });
            }
            else
            {
                foreach ((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset in
                    PairEnumeratePropertyAndAssetIndexPackage(
#if ENABLEHTTPREPAIR
                    EnforceHttpSchemeToAssetIndex(repairAssetIndex)
#else
                    repairAssetIndex
#endif
                    , assetProperty))
                {
                    // Assign a task depends on the asset type
                    Task assetTask = asset.AssetIndex.FT switch
                    {
                        FileType.Block => RepairAssetTypeGeneric(asset, downloadClient, IsCacheUpdateMode ? _httpClient_UpdateAssetProgress : _httpClient_RepairAssetProgress, token),
                        FileType.Audio => RepairAssetTypeGeneric(asset, downloadClient, IsCacheUpdateMode ? _httpClient_UpdateAssetProgress : _httpClient_RepairAssetProgress, token),
                        FileType.Video => RepairAssetTypeGeneric(asset, downloadClient, IsCacheUpdateMode ? _httpClient_UpdateAssetProgress : _httpClient_RepairAssetProgress, token),
                        _ => RepairAssetTypeGeneric(asset, downloadClient, IsCacheUpdateMode ? _httpClient_UpdateAssetProgress : _httpClient_RepairAssetProgress, token)
                    };

                    // Await the task
                    await assetTask;
                }
            }

            return true;
        }

        #region GenericRepair
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private ConcurrentDictionary<(FilePropertiesRemote, IAssetProperty), byte> _repairAssetEntry = new();
        private async Task RepairAssetTypeGeneric((FilePropertiesRemote AssetIndex, IAssetProperty AssetProperty) asset, DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token)
        {
            if (!_repairAssetEntry.TryAdd(asset, 0))
            {
                Logger.LogWriteLine($"[RepairAssetTypeGeneric] Skipping duplicate assignment for asset:\r\n\tN : {asset.AssetIndex.N}\r\n\tT : {asset.AssetIndex.FT}", LogType.Error, true);
                return;
            }
            // Increment total count current
            ProgressAllCountCurrent++;
            // Set repair activity status
            string timeLeftString = string.Format(Locale.Lang!._Misc!.TimeRemainHMSFormat!, Progress.ProgressAllTimeLeft);
            UpdateRepairStatus(
                string.Format(Locale.Lang._GameRepairPage.Status8, Path.GetFileName(asset.AssetIndex.N)),
                string.Format(Locale.Lang._GameRepairPage.PerProgressSubtitle2, ConverterTool.SummarizeSizeSimple(ProgressAllSizeCurrent), ConverterTool.SummarizeSizeSimple(ProgressAllSizeTotal)) + $" | {timeLeftString}",
                true);

            FileInfo fileInfo = new FileInfo(asset.AssetIndex.N!).EnsureNoReadOnly();

            // If asset type is unused, then delete it
            if (asset.AssetIndex.FT == FileType.Unused)
            {
                if (fileInfo.Exists)
                {
                    fileInfo.Delete();
                    Logger.LogWriteLine($"File [T: {asset.AssetIndex.FT}] {(asset.AssetIndex.FT == FileType.Block ? asset.AssetIndex.CRC : asset.AssetIndex.N)} deleted!", LogType.Default, true);
                }
            }
            else
            {
                // Start asset download task
                await RunDownloadTask(asset.AssetIndex.S, fileInfo, asset.AssetIndex.RN, downloadClient, downloadProgress, token);
                Logger.LogWriteLine($"File [T: {asset.AssetIndex.FT}] {(asset.AssetIndex.FT == FileType.Block ? asset.AssetIndex.CRC : asset.AssetIndex.N)} has been downloaded!", LogType.Default, true);
            }

            // Pop repair asset display entry
            PopRepairAssetEntry(asset.AssetProperty);
            _repairAssetEntry.Remove(asset, out _);
        }
        #endregion
    }
}
