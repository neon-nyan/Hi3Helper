using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset;
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
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault

namespace CollapseLauncher
{
    internal partial class StarRailCache
    {
        private async Task<List<SRAsset>> Check(List<SRAsset> assetIndex, CancellationToken token)
        {
            // Initialize asset index for the return
            List<SRAsset> returnAsset = [];

            // Set Indetermined status as false
            Status.IsProgressAllIndetermined = false;

            // Show the asset entry panel
            Status.IsAssetEntryPanelShow = true;

            // Get persistent and streaming paths
            string execName = Path.GetFileNameWithoutExtension(InnerGameVersionManager!.GamePreset!.GameExecutableName);
            string baseDesignDataPathPersistent = Path.Combine(GamePath!, @$"{execName}_Data\Persistent\DesignData\Windows");
            string baseDesignDataPathStreaming = Path.Combine(GamePath!, @$"{execName}_Data\StreamingAssets\DesignData\Windows");

            string baseLuaPathPersistent = Path.Combine(GamePath!, @$"{execName}_Data\Persistent\Lua\Windows");
            string baseLuaPathStreaming = Path.Combine(GamePath!, @$"{execName}_Data\StreamingAssets\Lua\Windows");

            string baseIFixPathPersistent = Path.Combine(GamePath!, @$"{execName}_Data\Persistent\IFix\Windows");
            string baseIFixPathStreaming = Path.Combine(GamePath!, @$"{execName}_Data\StreamingAssets\IFix\Windows");

            try
            {
                // Do check in parallelization.
                await Parallel.ForEachAsync(assetIndex!, new ParallelOptions
                {
                    MaxDegreeOfParallelism = ThreadCount,
                    CancellationToken = token
                }, async (asset, threadToken) =>
                {
                    switch (asset!.AssetType)
                    {
                        case SRAssetType.DesignData:
                            await CheckAsset(asset, returnAsset, baseDesignDataPathPersistent, baseDesignDataPathStreaming, threadToken);
                            break;
                        case SRAssetType.Lua:
                            await CheckAsset(asset, returnAsset, baseLuaPathPersistent, baseLuaPathStreaming, threadToken);
                            break;
                        case SRAssetType.IFix:
                            await CheckAsset(asset, returnAsset, baseIFixPathPersistent, baseIFixPathStreaming, threadToken);
                            break;
                    }
                });
            }
            catch (AggregateException ex)
            {
                throw ex.Flatten().InnerExceptions.First();
            }

            // Return the asset index
            return returnAsset;
        }

        private async ValueTask CheckAsset(SRAsset asset, List<SRAsset> returnAsset, string basePersistent, string baseStreaming, CancellationToken token)
        {
            // Increment the count and update the status
            lock (this)
            {
                ProgressAllCountCurrent++;
                Status.ActivityStatus = string.Format(Lang!._CachesPage!.CachesStatusChecking!, asset!.AssetType, asset.LocalName);
                Status.ActivityAll = string.Format(Lang!._CachesPage!.CachesTotalStatusChecking!, ProgressAllCountCurrent, ProgressAllCountTotal);
            }

            // Get persistent and streaming paths
            FileInfo fileInfoPersistent = new FileInfo(Path.Combine(basePersistent!, asset.LocalName!)).EnsureNoReadOnly(out bool isFileInfoPersistentExist);
            FileInfo fileInfoStreaming = new FileInfo(Path.Combine(baseStreaming!, asset.LocalName!)).EnsureNoReadOnly(out bool isStreamingExist);

            bool usePersistent = !isStreamingExist;
            bool isPersistentExist = isFileInfoPersistentExist && fileInfoPersistent.Length == asset.Size;
            asset.LocalName = usePersistent ? fileInfoPersistent.FullName : fileInfoStreaming.FullName;

            // Check if the file exist. If not, then add it to asset index.
            if (usePersistent && !isPersistentExist)
            {
                AddGenericCheckAsset(asset, CacheAssetStatus.New, returnAsset, null, asset.Hash);
                return;
            }

            // Skip CRC check if fast method is used
            if (UseFastMethod)
            {
                return;
            }

            // If above passes, then run the CRC check
            await using FileStream fs = await NaivelyOpenFileStreamAsync(usePersistent ? fileInfoPersistent : fileInfoStreaming,
                                                                         FileMode.Open, FileAccess.Read, FileShare.Read);
            // Calculate the asset CRC (MD5)
            byte[] hashArray = await GetCryptoHashAsync<MD5>(fs, null, true, true, token);

            // If the asset CRC doesn't match, then add the file to asset index.
            if (!IsArrayMatch(asset.Hash, hashArray))
            {
                AddGenericCheckAsset(asset, CacheAssetStatus.Obsolete, returnAsset, hashArray, asset.Hash);
            }
        }

        private void AddGenericCheckAsset(SRAsset asset, CacheAssetStatus assetStatus, List<SRAsset> returnAsset, byte[] localCrc, byte[] remoteCrc)
        {
            // Increment the count and total size
            lock (this)
            {
                // Set Indetermined status as false
                Status.IsProgressAllIndetermined = false;
                ProgressAllCountFound++;
                ProgressAllSizeFound += asset!.Size;
            }

            // Add file into asset index
            lock (returnAsset!)
            {
                returnAsset.Add(asset);

                LogWriteLine($"[T: {asset.AssetType}]: {asset.LocalName} found to be \"{assetStatus}\"", LogType.Warning, true);
            }

            // Add to asset entry display
            Dispatch(() => AssetEntry!.Add(new AssetProperty<CacheAssetType>(
                    Path.GetFileName(asset.LocalName),
                    ConvertCacheAssetTypeEnum(asset.AssetType),
                    $"{asset.AssetType}",
                    asset.Size,
                    localCrc,
                    remoteCrc
                ))
            );
        }
    }
}
