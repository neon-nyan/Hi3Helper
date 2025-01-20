using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.Preset;
using System.IO;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming
// ReSharper disable CommentTypo

namespace CollapseLauncher.Interfaces
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(CacheAsset))]
    internal sealed partial class CacheAssetJsonContext : JsonSerializerContext;

    internal sealed class CacheAsset : IAssetIndexSummary
    {
        // Concatenate N and CRC to get the filepath.
        public string ConcatN => IsUseLocalPath ? $"{N}" : $"{N}_{CRC}.unity3d";
        public string ConcatNRemote => $"{N}_{CRC}";
        public string BaseURL { get; set; }
        public string BasePath { get; set; }
        public string ConcatURL => ConverterTool.CombineURLFromString(BaseURL, ConcatNRemote);
        public string ConcatPath => Path.Combine(BasePath, ConverterTool.NormalizePath(ConcatN));

        // Filepath for input.
        // You have to concatenate the N with CRC to get the filepath using ConcatN()
        public string N { get; set; }

        // Hash of the file (HMACSHA1)
        // For more information:
        // https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.hmacsha1
        public string CRC { get; set; }
        public byte[] CRCArray => HexTool.HexToBytesUnsafe(CRC.ToLower());
        public string CRCLower { get => CRC.ToLower(); }

        // File size of the cache file.
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long CS { get; set; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long DLM { get; set; }
        public CacheAssetType DataType { get; set; }
        public CacheAssetStatus Status { get; set; }
        public CachePatchInfo? PatchInfo { get; set; }
        public bool IsUseLocalPath { get; set; } = false;
        public bool IsHasPatch { get => PatchInfo.HasValue; }

        public string PrintSummary() => $"File [T: {DataType}]: {N}\t{ConverterTool.SummarizeSizeSimple(CS)} ({CS} bytes)";
        public long GetAssetSize() => Status == CacheAssetStatus.Unused ? 0 : CS;
        public string GetRemoteURL() => BaseURL;
        public void SetRemoteURL(string url) => BaseURL = url;
    }
}
