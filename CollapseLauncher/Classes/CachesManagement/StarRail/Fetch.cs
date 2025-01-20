using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset;
using Hi3Helper.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault

namespace CollapseLauncher
{
    internal partial class StarRailCache
    {
        private async Task<List<SRAsset>> Fetch(CancellationToken token)
        {
            // Initialize asset index for the return
            List<SRAsset> returnAsset = [];

            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder()
                .UseLauncherConfig(DownloadThreadCount + DownloadThreadCountReserved)
                .SetUserAgent(UserAgent)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Initialize the new DownloadClient
            DownloadClient downloadClient = DownloadClient.CreateInstance(client);

            // Initialize metadata
            // Set total activity string as "Fetching Caches Type: Dispatcher"
            Status.ActivityStatus = string.Format(Lang!._CachesPage!.CachesStatusFetchingType!, CacheAssetType.Dispatcher);
            Status.IsProgressAllIndetermined = true;
            Status.IsIncludePerFileIndicator = false;
            UpdateStatus();

            if (!await InnerGameVersionManager!.StarRailMetadataTool.Initialize(token, downloadClient, _httpClient_FetchAssetProgress, GetExistingGameRegionID(), Path.Combine(GamePath!, $"{Path.GetFileNameWithoutExtension(GameVersionManager!.GamePreset!.GameExecutableName)}_Data\\Persistent")))
                throw new InvalidDataException("The dispatcher response is invalid! Please open an issue to our GitHub page to report this issue.");

            // Iterate type and do fetch
            await Parallel.ForEachAsync(Enum.GetValues<SRAssetType>(), token, async (type, innerCancelToken) =>
            {
                // Skip for unused type
                switch (type)
                {
                    case SRAssetType.Audio:
                    case SRAssetType.Video:
                    case SRAssetType.Block:
                    case SRAssetType.Asb:
                        return;
                }

                // uint = Count of the assets available
                // long = Total size of the assets available
                (int, long) count = await FetchByType(downloadClient, _httpClient_FetchAssetProgress, type, returnAsset, innerCancelToken);

                // Write a log about the metadata
                LogWriteLine($"Cache Metadata [T: {type}]:", LogType.Default, true);
                LogWriteLine($"    Cache Count = {count.Item1}", LogType.NoTag, true);
                LogWriteLine($"    Cache Size = {SummarizeSizeSimple(count.Item2)}", LogType.NoTag, true);

                // Increment the Total Size and Count
                Interlocked.Add(ref ProgressAllCountTotal, count.Item1);
                Interlocked.Add(ref ProgressAllSizeTotal,  count.Item2);
            }).ConfigureAwait(false);

            // Return asset index
            return returnAsset;
        }

        private async Task<(int, long)> FetchByType(DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, SRAssetType type, List<SRAsset> assetIndex, CancellationToken token)
        {
            // Set total activity string as "Fetching Caches Type: <type>"
            Status.ActivityStatus = string.Format(Lang!._CachesPage!.CachesStatusFetchingType!, type);
            Status.IsProgressAllIndetermined = true;
            Status.IsIncludePerFileIndicator = false;
            UpdateStatus();

            // Start reading the metadata and build the asset index of each type
            SRAssetProperty assetProperty;
            switch (type)
            {
                case SRAssetType.IFix:
                    await InnerGameVersionManager!.StarRailMetadataTool!.ReadIFixMetadataInformation(downloadClient, downloadProgress, token);
                    assetProperty = InnerGameVersionManager!.StarRailMetadataTool!.MetadataIFix!.GetAssets();
                    assetIndex!.AddRange(assetProperty!.AssetList!);
                    return (assetProperty.AssetList.Count, assetProperty.AssetTotalSize);
                case SRAssetType.DesignData:
                    await InnerGameVersionManager!.StarRailMetadataTool!.ReadDesignMetadataInformation(downloadClient, downloadProgress, token);
                    assetProperty = InnerGameVersionManager.StarRailMetadataTool.MetadataDesign!.GetAssets();
                    assetIndex!.AddRange(assetProperty!.AssetList!);
                    return (assetProperty.AssetList.Count, assetProperty.AssetTotalSize);
                case SRAssetType.Lua:
                    await InnerGameVersionManager!.StarRailMetadataTool!.ReadLuaMetadataInformation(downloadClient, downloadProgress, token);
                    assetProperty = InnerGameVersionManager.StarRailMetadataTool.MetadataLua!.GetAssets();
                    assetIndex!.AddRange(assetProperty!.AssetList!);
                    return (assetProperty.AssetList.Count, assetProperty.AssetTotalSize);
            }

            return (0, 0);
        }

        #region Utilities
        private unsafe string GetExistingGameRegionID()
        {
#nullable enable
            object? value = RegistryRoot?.GetValue("App_LastServerName_h2577443795", null);
            if (value == null)
            {
                return GameVersionManager!.GamePreset!.GameDispatchDefaultName ?? throw new KeyNotFoundException("Default dispatcher name in metadata is not exist!");
            }
#nullable disable

            ReadOnlySpan<byte> span = (value as byte[]).AsSpan();
            fixed (byte* valueSpan = span)
            {
                string name = Encoding.UTF8.GetString(valueSpan, span.Length - 1);
                return name;
            }
        }

        private static CacheAssetType ConvertCacheAssetTypeEnum(SRAssetType assetType) => assetType switch
                                                                                          {
                                                                                              SRAssetType.IFix => CacheAssetType.IFix,
                                                                                              SRAssetType.DesignData => CacheAssetType.DesignData,
                                                                                              SRAssetType.Lua => CacheAssetType.Lua,
                                                                                              _ => CacheAssetType.General
                                                                                          };
        #endregion
    }
}
