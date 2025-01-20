using System.Text.Json.Serialization;
// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher.GameSettings.StarRail.Context
//Modified from Honkai codes because of similarities in reg structure
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(Model))]
    [JsonSerializable(typeof(PCResolution))]
    internal sealed partial class StarRailSettingsJsonContext : JsonSerializerContext { }
}
