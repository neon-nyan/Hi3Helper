using System.Text.Json.Serialization;
// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher.GamePlaytime
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(CollapsePlaytime))]
    internal sealed partial class UniversalPlaytimeJsonContext : JsonSerializerContext;
}
