using CollapseLauncher.Helper.JsonConverter;
using System.Collections.Generic;
using System.Text.Json.Serialization;
// ReSharper disable PartialTypeWithSinglePart

#nullable enable
namespace CollapseLauncher.InstallManagement.Base
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(HDiffMapEntry))]
    [JsonSerializable(typeof(HDiffMap))]
    internal partial class HDiffMapEntryJsonContext : JsonSerializerContext;

    internal class HDiffMap
    {
        [JsonPropertyName("diff_map")]
        public List<HDiffMapEntry>? Entries { get; set; }
    }

    internal class HDiffMapEntry
    {
        [JsonPropertyName("patch_file_md5")]
        [JsonConverter(typeof(HexStringToBytesConverter))]
        public byte[]? PatchMD5Hash { get; set; }

        [JsonPropertyName("patch_file_name")]
        [JsonConverter(typeof(NormalizedPathStringConverter))]
        public string? PatchFileName { get; set; }

        [JsonPropertyName("patch_file_size")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long PatchFileSize { get;    set; }

        [JsonPropertyName("source_file_md5")]
        [JsonConverter(typeof(HexStringToBytesConverter))]
        public byte[]? SourceMD5Hash  { get; set; }

        [JsonPropertyName("source_file_name")]
        [JsonConverter(typeof(NormalizedPathStringConverter))]
        public string? SourceFileName { get; set; }

        [JsonPropertyName("source_file_size")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long SourceFileSize { get;   set; }

        [JsonPropertyName("target_file_md5")]
        [JsonConverter(typeof(HexStringToBytesConverter))]
        public byte[]? TargetMD5Hash  { get; set; }

        [JsonPropertyName("target_file_name")]
        [JsonConverter(typeof(NormalizedPathStringConverter))]
        public string? TargetFileName { get; set; }

        [JsonPropertyName("target_file_size")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long TargetFileSize { get; set; }

        [JsonIgnore]
        public bool CanDeleteSource { get; set; }
    }
}
