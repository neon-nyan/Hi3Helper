using System.Text.Json.Serialization;
// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher.GameSettings.Zenless.Context;

[JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
[JsonSerializable(typeof(GeneralData))]
internal sealed partial class ZenlessSettingsJsonContext : JsonSerializerContext {}