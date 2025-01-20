using System.Text.Json.Serialization;
// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher.GameSettings.Universal
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(CollapseScreenSetting))]
    [JsonSerializable(typeof(CollapseMiscSetting))]
    internal sealed partial class UniversalSettingsJsonContext : JsonSerializerContext { }
}
