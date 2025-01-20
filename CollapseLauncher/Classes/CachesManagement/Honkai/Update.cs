using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Helper;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Http;
using System;
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
// ReSharper disable CommentTypo
// ReSharper disable GrammarMistakeInComment

namespace CollapseLauncher
{
    internal partial class HonkaiCache
    {
        private async Task<bool> Update(List<CacheAsset> updateAssetIndex, List<CacheAsset> assetIndex, CancellationToken token)
        {
            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder<SocketsHttpHandler>()
                .UseLauncherConfig(DownloadThreadCount + DownloadThreadCountReserved)
                .SetUserAgent(UserAgent)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Use the new DownloadClient instance
            DownloadClient downloadClient = DownloadClient.CreateInstance(client);
            try
            {
                // Set IsProgressAllIndetermined as false and update the status 
                Status.IsProgressAllIndetermined = true;
                UpdateStatus();

                // Iterate the asset index and do update operation
                ObservableCollection<IAssetProperty> assetProperty   = [.. AssetEntry];

                ConcurrentDictionary<(CacheAsset, IAssetProperty), byte> runningTask = new();
                if (IsBurstDownloadEnabled)
                {
                    await Parallel.ForEachAsync(
                        PairEnumeratePropertyAndAssetIndexPackage(
#if ENABLEHTTPREPAIR    
                        EnforceHttpSchemeToAssetIndex(updateAssetIndex)
#else
                        updateAssetIndex
#endif
                        , assetProperty),
                        new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = DownloadThreadCount },
                        async (asset, innerToken) =>
                        {
                            if (!runningTask.TryAdd(asset, 0))
                            {
                                LogWriteLine($"Found duplicated task for {asset.AssetProperty.Name}! Skipping...", LogType.Warning, true);
                                return;
                            }
                            await UpdateCacheAsset(asset, downloadClient, _httpClient_UpdateAssetProgress, innerToken);
                            runningTask.Remove(asset, out _);
                        });
                }
                else
                {
                    foreach ((CacheAsset, IAssetProperty) asset in
                        PairEnumeratePropertyAndAssetIndexPackage(
#if ENABLEHTTPREPAIR    
                        EnforceHttpSchemeToAssetIndex(updateAssetIndex)
#else
                        updateAssetIndex
#endif
                        , assetProperty))
                    {
                        if (!runningTask.TryAdd(asset, 0))
                        {
                            LogWriteLine($"Found duplicated task for {asset.Item1.N}! Skipping...", LogType.Warning, true);
                            break;
                        }
                        await UpdateCacheAsset(asset, downloadClient, _httpClient_UpdateAssetProgress, token);
                        runningTask.Remove(asset, out _);
                    }
                }

                // Reindex the asset index in Verify.txt
                UpdateCacheVerifyList(assetIndex);

                return true;
            }
            catch (TaskCanceledException) { throw; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LogWriteLine($"An error occured while updating cache file!\r\n{ex}", LogType.Error, true);
                throw;
            }
        }

        private void UpdateCacheVerifyList(List<CacheAsset> assetIndex)
        {
            // Get listFile path
            string listFile = Path.Combine(GamePath!, "Data", "Verify.txt");

            // Initialize listFile File Stream
            using FileStream   fs = new FileStream(listFile, FileMode.Create, FileAccess.Write);
            using StreamWriter sw = new StreamWriter(fs);
            // Iterate asset index and generate the path for the cache path
            for (var index = 0; index < assetIndex!.Count; index++)
            {
                var asset = assetIndex![index];
                // Yes, the path is written in this way. Idk why miHoYo did this...
                // Update 6.8: They finally notices that they use "//" instead of "/"
                string basePath = GetAssetBasePathByType(asset!.DataType)!.Replace('\\', '/');
                string path     = basePath + "/" + asset.ConcatN;
                sw.WriteLine(path);
            }
        }

        private async Task UpdateCacheAsset((CacheAsset AssetIndex, IAssetProperty AssetProperty) asset, DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token)
        {
            // Increment total count and update the status
            ProgressAllCountCurrent++;
            Status.ActivityStatus = string.Format(Lang!._Misc!.Downloading + " {0}: {1}", asset!.AssetIndex.DataType, asset.AssetIndex.N);
            UpdateAll();

            FileInfo fileInfo = new FileInfo(asset.AssetIndex.ConcatPath!)
                               .EnsureCreationOfDirectory()
                               .EnsureNoReadOnly(out bool isExist);

            // This is a action for Unused asset.
            if (asset.AssetIndex.DataType == CacheAssetType.Unused)
            {
                if (isExist)
                    fileInfo.Delete();

                LogWriteLine($"Deleted unused file: {fileInfo.FullName}", LogType.Default, true);
            }
            // Other than unused file, do this action
            else
            {
            #if DEBUG
                LogWriteLine($"Downloading cache [T: {asset.AssetIndex.DataType}]: {asset.AssetIndex.N} at URL: {asset.AssetIndex.ConcatURL}", LogType.Debug, true);
            #endif

                await RunDownloadTask(asset.AssetIndex.CS, fileInfo, asset.AssetIndex.ConcatURL, downloadClient, downloadProgress, token);

            #if !DEBUG
                LogWriteLine($"Downloaded cache [T: {asset.AssetIndex.DataType}]: {asset.AssetIndex.N}", LogType.Default, true);
            #endif
            }

            // Remove Asset Entry display
            PopRepairAssetEntry(asset.AssetProperty);
        }
    }
}
