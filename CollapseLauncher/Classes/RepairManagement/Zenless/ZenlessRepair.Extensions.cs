using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.EncTool.Parser.Sleepy;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable PartialTypeWithSinglePart

#nullable enable
namespace CollapseLauncher
{
    [JsonSerializable(typeof(ZenlessResManifestAsset))]
    [JsonSourceGenerationOptions(AllowOutOfOrderMetadataProperties = true, AllowTrailingCommas = true, GenerationMode = JsonSourceGenerationMode.Metadata, IncludeFields = false, IgnoreReadOnlyFields = true)]
    internal partial class ZenlessManifestContext : JsonSerializerContext { }

    internal static class ZenlessRepairExtensions
    {
        private const string StreamingAssetsPath = @"StreamingAssets\";
        private const string AssetTypeAudioPath  = StreamingAssetsPath + @"Audio\Windows\";
        private const string AssetTypeBlockPath  = StreamingAssetsPath + @"Blocks\";
        private const string AssetTypeVideoPath  = StreamingAssetsPath + @"Video\HD\";

        private const string PersistentAssetsPath = @"Persistent\";
        private const string AssetTypeAudioPersistentPath = PersistentAssetsPath + @"Audio\Windows\";
        private const string AssetTypeBlockPersistentPath = PersistentAssetsPath + @"Blocks\";
        private const string AssetTypeVideoPersistentPath = PersistentAssetsPath + @"Video\HD\";

        internal static async IAsyncEnumerable<T?> MergeAsyncEnumerable<T>(params IAsyncEnumerable<T?>[] sources)
        {
            foreach (IAsyncEnumerable<T?> enumerable in sources)
            {
                await foreach (T? item in enumerable)
                {
                    yield return item;
                }
            }
        }

        internal static async IAsyncEnumerable<PkgVersionProperties> RegisterSleepyFileInfoToManifest(
            this SleepyFileInfoResult fileInfo,
            HttpClient httpClient,
            List<FilePropertiesRemote> assetIndex,
            bool needWriteToLocal,
            string persistentPath,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            string manifestFileUrl = ConverterTool.CombineURLFromString(fileInfo.BaseUrl, fileInfo.ReferenceFileInfo.FileName);
            using HttpResponseMessage responseMessage = await httpClient.GetAsync(manifestFileUrl, HttpCompletionOption.ResponseHeadersRead, token);

            string filePath = Path.Combine(persistentPath, fileInfo.ReferenceFileInfo.FileName + "_persist");

            await using Stream responseStream = await responseMessage.Content.ReadAsStreamAsync(token);
            await using Stream responseInterceptedStream = new JsonFieldToEnumerableStream(needWriteToLocal ? filePath : null, responseStream);

            IAsyncEnumerable<ZenlessResManifestAsset?> enumerable = JsonSerializer
                .DeserializeAsyncEnumerable(
                    responseInterceptedStream,
                    ZenlessManifestContext.Default.ZenlessResManifestAsset,
                    false,
                    token
                );

            await foreach (ZenlessResManifestAsset? manifest in enumerable)
            {
                if (manifest == null)
                {
                    continue;
                }

                yield return new PkgVersionProperties
                {
                    fileSize = manifest.FileSize,
                    isForceStoreInPersistent = manifest.IsPersistentFile,
                    isPatch = manifest.IsPersistentFile,
                    md5 = Convert.ToHexStringLower(manifest.Xxh64Hash),
                    remoteName = manifest.FileRelativePath
                };
            }
        }

        internal static IEnumerable<FilePropertiesRemote?> RegisterMainCategorizedAssetsToHashSet(this IEnumerable<PkgVersionProperties> assetEnumerable, List<FilePropertiesRemote> assetIndex, Dictionary<string, FilePropertiesRemote> hashSet, string baseLocalPath, string baseUrl)
            => assetEnumerable.Select(asset => ReturnCategorizedYieldValue(hashSet, assetIndex, asset, baseLocalPath, baseUrl));

        internal static async IAsyncEnumerable<FilePropertiesRemote?> RegisterMainCategorizedAssetsToHashSetAsync(this IAsyncEnumerable<PkgVersionProperties> assetEnumerable, List<FilePropertiesRemote> assetIndex, Dictionary<string, FilePropertiesRemote> hashSet, string baseLocalPath, string baseUrl, [EnumeratorCancellation] CancellationToken token = default)

        {
            await foreach (PkgVersionProperties asset in assetEnumerable.WithCancellation(token))
            {
                yield return ReturnCategorizedYieldValue(hashSet, assetIndex, asset, baseLocalPath, baseUrl);
            }
        }

        internal static async IAsyncEnumerable<FilePropertiesRemote?> RegisterResCategorizedAssetsToHashSetAsync(this IAsyncEnumerable<PkgVersionProperties> assetEnumerable, List<FilePropertiesRemote> assetIndex, Dictionary<string, FilePropertiesRemote> hashSet, string baseLocalPath, string basePatchUrl, string baseResUrl)
        {
            await foreach (PkgVersionProperties asset in assetEnumerable)
            {
                string baseLocalPathMerged = Path.Combine(baseLocalPath, asset.isPatch ? PersistentAssetsPath : StreamingAssetsPath);

                yield return ReturnCategorizedYieldValue(hashSet, assetIndex, asset, baseLocalPathMerged, basePatchUrl, baseResUrl);
            }
        }

        private static FilePropertiesRemote? ReturnCategorizedYieldValue(Dictionary<string, FilePropertiesRemote> hashSet, List<FilePropertiesRemote> assetIndex, PkgVersionProperties asset, string baseLocalPath, string baseUrl, string? alternativeUrlIfNonPatch = null)
        {
            FilePropertiesRemote asRemoteProperty = GetNormalizedFilePropertyTypeBased(
                    asset.isPatch || string.IsNullOrEmpty(alternativeUrlIfNonPatch) ? baseUrl : alternativeUrlIfNonPatch,
                    baseLocalPath,
                    asset.remoteName,
                    asset.fileSize,
                    asset.md5,
                    FileType.Generic,
                    asset.isPatch);

            ReadOnlySpan<char> relTypeRelativePath = asRemoteProperty.GetAssetRelativePath(out RepairAssetType assetType);
            asRemoteProperty.FT = assetType switch
            {
                RepairAssetType.Audio => FileType.Audio,
                RepairAssetType.Block => FileType.Block,
                RepairAssetType.Video => FileType.Video,
                _ => FileType.Generic
            };

            if (relTypeRelativePath.IsEmpty)
            {
                return asRemoteProperty;
            }

            string relTypeRelativePathStr = relTypeRelativePath.ToString();
            if (hashSet.TryAdd(relTypeRelativePathStr, asRemoteProperty) || !asset.isPatch)
            {
                return asRemoteProperty;
            }

            FilePropertiesRemote existingValue = hashSet[relTypeRelativePathStr];
            int                  indexOf       = assetIndex.IndexOf(existingValue);
            if (indexOf < -1)
                return asRemoteProperty;

            assetIndex[indexOf]             = asRemoteProperty;
            hashSet[relTypeRelativePathStr] = asRemoteProperty;

            return null;

        }

        private static FilePropertiesRemote GetNormalizedFilePropertyTypeBased(string remoteParentURL,
                                                                               string baseLocalPath,
                                                                               string remoteRelativePath,
                                                                               long fileSize,
                                                                               string hash,
                                                                               FileType type = FileType.Generic,
                                                                               bool isPatchApplicable = false)
        {
            string remoteAbsolutePath = type switch
            {
                FileType.Generic => ConverterTool.CombineURLFromString(remoteParentURL, remoteRelativePath),
                _ => remoteParentURL
            };
            string localAbsolutePath = Path.Combine(baseLocalPath, ConverterTool.NormalizePath(remoteRelativePath));

            return new FilePropertiesRemote
            {
                FT = type,
                CRC = hash,
                S = fileSize,
                N = localAbsolutePath,
                RN = remoteAbsolutePath,
                IsPatchApplicable = isPatchApplicable
            };
        }

        internal static ReadOnlySpan<char> GetAssetRelativePath(this FilePropertiesRemote asset, out RepairAssetType assetType)
        {
            assetType = RepairAssetType.Generic;

            int indexOfOffset;
            if ((indexOfOffset = asset.N.LastIndexOf(AssetTypeAudioPath, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                assetType = RepairAssetType.Audio;
            }
            else if ((indexOfOffset = asset.N.LastIndexOf(AssetTypeBlockPath, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                assetType = RepairAssetType.Block;
            }
            else if ((indexOfOffset = asset.N.LastIndexOf(AssetTypeVideoPath, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                assetType = RepairAssetType.Video;
            }
            else if ((indexOfOffset = asset.N.LastIndexOf(AssetTypeAudioPersistentPath, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                assetType = RepairAssetType.Audio;
            }
            else if ((indexOfOffset = asset.N.LastIndexOf(AssetTypeBlockPersistentPath, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                assetType = RepairAssetType.Block;
            }
            else if ((indexOfOffset = asset.N.LastIndexOf(AssetTypeVideoPersistentPath, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                assetType = RepairAssetType.Video;
            }

            return indexOfOffset >= 0 ? asset.N.AsSpan(indexOfOffset) : ReadOnlySpan<char>.Empty;
        }

        internal static async IAsyncEnumerable<PkgVersionProperties> EnumerateStreamToPkgVersionPropertiesAsync(
            this Stream stream,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            using TextReader reader = new StreamReader(stream);
            string? currentLine;
            while (!string.IsNullOrEmpty(currentLine = await reader.ReadLineAsync(token)))
            {
                PkgVersionProperties? property = currentLine.Deserialize(CoreLibraryJsonContext.Default.PkgVersionProperties);
                if (property == null)
                    continue;

                yield return property;
            }
        }
    }
}
