using System.Text.Json.Serialization;
// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher.GameSettings.Genshin.Context
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(GeneralData))]
    [JsonSerializable(typeof(GraphicsData))]
    [JsonSerializable(typeof(GlobalPerfData))]
    internal sealed partial class GenshinSettingsJsonContext : JsonSerializerContext;
}
