using CollapseLauncher.Helper.JsonConverter;
using System.Collections.Generic;
using System.Text.Json.Serialization;
// ReSharper disable StringLiteralTypo

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(HoYoPlayLauncherResources))]
    internal sealed partial class HoYoPlayLauncherResourcesJsonContext : JsonSerializerContext;

    public class HoYoPlayLauncherResources
    {
        [JsonPropertyName("data")]
        public LauncherResourceData? Data { get; set; }

        [JsonPropertyName("message")]
        public string? ResultMessage { get; set; }

        [JsonPropertyName("retcode")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? ReturnCode { get; set; }
    }

    public class LauncherResourceData
    {
        [JsonPropertyName("game_packages")]
        public List<LauncherPackages>? LauncherPackages { get; set; }

        [JsonPropertyName("plugin_releases")]
        public List<LauncherPackages>? PluginPackages { get; set; }

        [JsonPropertyName("game_channel_sdks")]
        public List<LauncherSdkPackages>? ChannelSdks { get; set; }
    }

    public class LauncherSdkPackages
    {
        [JsonPropertyName("game")]
        public GameDetail? GameDetail { get; set; }

        [JsonPropertyName("channel_sdk_pkg")]
        public PackageDetails? SdkPackageDetail { get; set; }

        [JsonPropertyName("pkg_version_file_name")]
        public string? PkgVersionFileName { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }

    public class LauncherPackages
    {
        [JsonPropertyName("game")]
        public GameDetail? GameDetail { get; set; }

        [JsonPropertyName("main")]
        public PackagePartition? MainPackage { get; set; }

        [JsonPropertyName("plugins")]
        public List<PackagePluginSections>? PluginPackageSections { get; set; }

        [JsonPropertyName("pre_download")]
        public PackagePartition? PreDownload { get; set; }
    }

    public class GameDetail
    {
        [JsonPropertyName("biz")]
        public string? GameBiz { get; set; }

        [JsonPropertyName("id")]
        public string? LauncherId { get; set; }
    }

    public class PackagePartition
    {
        [JsonPropertyName("major")]
        public PackageResourceSections? CurrentVersion { get; set; }

        [JsonPropertyName("patches")]
        public List<PackageResourceSections>? Patches { get; set; }
    }

    public class PackagePluginSections
    {
        [JsonPropertyName("plugin_id")]
        public string? PluginId { get; set; }

        [JsonPropertyName("plugin_pkg")]
        public PackageDetails? PluginPackage { get; set; }

        [JsonPropertyName("release_id")]
        public string? ReleaseId { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }

    public class PackageResourceSections
    {
        [JsonPropertyName("audio_pkgs")]
        public List<PackageDetails>? AudioPackages { get; set; }

        [JsonPropertyName("game_pkgs")]
        public List<PackageDetails>? GamePackages { get; set; }

        [JsonPropertyName("res_list_url")]
        public string? ResourceListUrl { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }

    public class PackageDetails
    {
        [JsonPropertyName("decompressed_size")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long PackageDecompressSize { get; init; }

        [JsonPropertyName("md5")]
        public string? PackageMD5Hash { get; init; }

        [JsonPropertyName("size")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long? PackageSize { get; init; }

        [JsonPropertyName("url")]
        public string? PackageUrl { get; init; }

        [JsonPropertyName("pkg_version_file_name")]
        public string? UnpackedPkgVersionFileName { get; init; }

        [JsonPropertyName("path")]
        public string? UnpackedBaseUrl { get; init; }

        [JsonPropertyName("channel_sdk_pkg")]
        public string? ChannelSdkPkg { get; init; }

        [JsonPropertyName("command")]
        public string? PackageRunCommand { get; init; }

        [JsonPropertyName("validation")]
        [JsonConverter(typeof(RegionResourcePluginValidateConverter))]
        public List<RegionResourcePluginValidate>? PackageAssetValidationList { get; init; }

        [JsonPropertyName("language")]
        public string? Language { get; init; }
    }
}
