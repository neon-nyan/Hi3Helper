using Hi3Helper.EncTool.Parser.AssetMetadata;
using System.Collections.Generic;
using System.Text.Json.Serialization;

#nullable enable
namespace CollapseLauncher
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(int[]))]
    [JsonSerializable(typeof(AudioPCKType[]))]
    [JsonSerializable(typeof(Dictionary<string, Dictionary<string, string>>))]
    internal sealed partial class GenericJsonContext : JsonSerializerContext;
}
