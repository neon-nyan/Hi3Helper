using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
// ReSharper disable CommentTypo

namespace CollapseLauncher
{
    internal partial class HonkaiCache
    {
        private async Task<List<CacheAsset>> Check(List<CacheAsset> assetIndex, CancellationToken token)
        {
            // Initialize asset index for the return
            List<CacheAsset> returnAsset = [];

            // Set Indetermined status as false
            Status.IsProgressAllIndetermined = false;

            // Show the asset entry panel
            Status.IsAssetEntryPanelShow = true;

            try
            {
                // Create the cache directory if it doesn't exist
                if (!Directory.Exists(GamePath))
                {
                    Directory.CreateDirectory(GamePath!);
                }

                // Check for unused files
                CheckUnusedAssets(assetIndex, returnAsset);

                // Do check in parallelization.
                await Parallel.ForEachAsync(assetIndex!, new ParallelOptions
                {
                    MaxDegreeOfParallelism = ThreadCount,
                    CancellationToken = token
                }, async (asset, localToken) =>
                {
                    await CheckAsset(asset, returnAsset, localToken);
                });
            }
            catch (AggregateException ex)
            {
                throw ex.Flatten().InnerExceptions.First();
            }

            // Return the asset index
            return returnAsset;
        }

        private void CheckUnusedAssets(List<CacheAsset> assetIndex, List<CacheAsset> returnAsset)
        {
            // Directory info and if the directory doesn't exist, return
            DirectoryInfo directoryInfo = new DirectoryInfo(GamePath);
            if (!directoryInfo.Exists)
            {
                return;
            }

            // Iterate the file contained in the _gamePath
            foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                .EnumerateNoReadOnly())
            {
                string filePath = fileInfo.FullName;

                if (filePath.Contains("output_log",    StringComparison.OrdinalIgnoreCase)
                    || filePath.Contains("Crashes",    StringComparison.OrdinalIgnoreCase)
                    || filePath.Contains("Verify.txt", StringComparison.OrdinalIgnoreCase)
                    || filePath.Contains("APM",        StringComparison.OrdinalIgnoreCase)
                    || filePath.Contains("FBData",     StringComparison.OrdinalIgnoreCase)
                    || filePath.Contains("asb.dat",    StringComparison.OrdinalIgnoreCase)
                    || assetIndex.Exists(x => x.ConcatPath == fileInfo.FullName))
                {
                    continue;
                }

                // Increment the total found count
                ProgressAllCountFound++;

                // Add asset to the returnAsset
                CacheAsset asset = new CacheAsset
                {
                    BasePath       = Path.GetDirectoryName(filePath),
                    N              = Path.GetFileName(filePath),
                    DataType       = CacheAssetType.Unused,
                    CS             = fileInfo.Length,
                    CRC            = null,
                    Status         = CacheAssetStatus.Unused,
                    IsUseLocalPath = true
                };
                returnAsset!.Add(asset);

                // Add to asset entry display
                Dispatch(() => AssetEntry!.Add(new AssetProperty<CacheAssetType>(
                                                    asset.N,
                                                    asset.DataType,
                                                    asset.BasePath,
                                                    asset.CS,
                                                    null,
                                                    null
                                                   ))
                        );

                LogWriteLine($"File: {asset.ConcatPath} is unused!", LogType.Warning, true);
            }
        }

        private async ValueTask CheckAsset(CacheAsset asset, List<CacheAsset> returnAsset, CancellationToken token)
        {
            // Increment the count and update the status
            Interlocked.Add(ref ProgressAllCountCurrent, 1);
            Status.ActivityStatus = string.Format(Lang!._CachesPage!.CachesStatusChecking!, asset!.DataType, asset.N);
            Status.ActivityAll = string.Format(Lang._CachesPage.CachesTotalStatusChecking!, ProgressAllCountCurrent, ProgressAllCountTotal);

            // Assign the file info.
            FileInfo fileInfo = new FileInfo(asset.ConcatPath).EnsureNoReadOnly(out bool isExist);

            // Check if the file exist. If not, then add it to asset index.
            if (!isExist)
            {
                AddGenericCheckAsset(asset, CacheAssetStatus.New, returnAsset, null, asset.CRCArray);
                return;
            }

            // Check if the file size matches. if not, then add it to asset index.
            if (fileInfo.Length != asset.CS)
            {
                AddGenericCheckAsset(asset, CacheAssetStatus.New, returnAsset, null, asset.CRCArray);
                return;
            }

            // Skip CRC check if fast method is used
            if (UseFastMethod)
            {
                return;
            }

            // If above passes, then run the CRC check
            await using FileStream fs = await NaivelyOpenFileStreamAsync(fileInfo, FileMode.Open, FileAccess.Read, FileShare.Read);
            // Calculate the asset CRC (SHA1)
            byte[] hashArray = await GetCryptoHashAsync<HMACSHA1>(fs, GameSalt, true, true, token);

            // If the asset CRC doesn't match, then add the file to asset index.
            if (!IsArrayMatch(asset.CRCArray, hashArray))
            {
                AddGenericCheckAsset(asset, CacheAssetStatus.Obsolete, returnAsset, hashArray, asset.CRCArray);
            }
        }

        private void AddGenericCheckAsset(CacheAsset asset, CacheAssetStatus assetStatus, List<CacheAsset> returnAsset, byte[] localCRC, byte[] remoteCRC)
        {
            // Increment the count and total size
            lock (this)
            {
                // Set Indetermined status as false
                Status.IsProgressAllIndetermined = false;
                ProgressAllCountFound++;
                ProgressAllSizeFound += asset!.CS;
            }

            // Add file into asset index
            lock (returnAsset!)
            {
                asset.Status = assetStatus;
                returnAsset.Add(asset);

                LogWriteLine($"[T: {asset.DataType}]: {asset.N} found to be \"{assetStatus}\"", LogType.Warning, true);
            }

            // Add to asset entry display
            Dispatch(() => AssetEntry!.Add(new AssetProperty<CacheAssetType>(
                    Path.GetFileName(asset.N),
                    asset.DataType,
                    Path.GetDirectoryName(asset.N),
                    asset.CS,
                    localCRC,
                    remoteCRC
                ))
            );
        }
    }
}
