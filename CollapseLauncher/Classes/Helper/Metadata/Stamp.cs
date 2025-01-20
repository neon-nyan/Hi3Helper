#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CollapseLauncher.Helper.Metadata
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(List<Stamp>))]
    internal sealed partial class StampJsonContext : JsonSerializerContext;

    public sealed class Stamp
    {
        public long LastUpdated { get; set; }
        public string? MetadataPath { get; set; } = null;
        public MetadataType? MetadataType { get; set; } = null;
        public bool? MetadataInclude { get; set; } = null;
        public string? GameName { get; set; } = null;
        public string? GameRegion { get; set; } = null;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime LastModifiedTimeUtc { get; set; } = default;
        public string? PresetConfigVersion { get; set; }
    }
}
