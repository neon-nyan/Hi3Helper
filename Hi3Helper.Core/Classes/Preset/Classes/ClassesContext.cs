using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.Shared.ClassStruct;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using static Hi3Helper.Locale;

namespace Hi3Helper
{
    [JsonSourceGenerationOptions(IncludeFields = true, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = false)]
    [JsonSerializable(typeof(DataProperties))]
    [JsonSerializable(typeof(PkgVersionProperties))]
    [JsonSerializable(typeof(DataPropertiesContent))]
    [JsonSerializable(typeof(FilePropertiesRemote[]))]
    [JsonSerializable(typeof(List<FilePropertiesRemote>))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    public sealed partial class CoreLibraryJsonContext : JsonSerializerContext;

    [JsonSourceGenerationOptions(IncludeFields = true, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(LocalizationParams))]
    [JsonSerializable(typeof(LocalizationParamsBase))]
    internal sealed partial class CoreLibraryFieldsJsonContext : JsonSerializerContext;
}
