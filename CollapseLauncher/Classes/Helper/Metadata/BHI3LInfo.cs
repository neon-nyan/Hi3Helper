// ReSharper disable InconsistentNaming
using System.Text.Json.Serialization;

namespace CollapseLauncher.Helper.Metadata
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(BHI3LInfo))]
    internal sealed partial class BHI3LInfoJsonContext : JsonSerializerContext;

    internal sealed class BHI3LInfo
    {
        public BHI3LInfo_GameInfo game_info { get; set; }
    }

    internal sealed class BHI3LInfo_GameInfo
    {
        public string version { get; set; }
        public string install_path { get; set; }
        public bool installed { get; set; }
    }
}
