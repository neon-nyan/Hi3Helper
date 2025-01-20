using CollapseLauncher.Helper.JsonConverter;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using System.Collections.Generic;
using System.Text.Json.Serialization;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher
{
    public interface IRegionResourceCopyable<out T>
    {
        T Copy();
    }

    public static class RegionResourceListHelper
    {
        public static List<T>? Copy<T>(this List<T>? source)
            where T : IRegionResourceCopyable<T>
        {
            if (source == null)
            {
                return null;
            }

            return source.Count == 0 ? [] : source;
        }
    }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(RegionResourceProp))]
    internal sealed partial class RegionResourcePropJsonContext : JsonSerializerContext;

    public sealed class RegionResourceProp : IRegionResourceCopyable<RegionResourceProp>
    {
        public RegionResourceGame? data         { get; set; }
        public string              imgLocalPath { get; set; } = string.Empty;

        public RegionResourceProp Copy()
        {
            return new RegionResourceProp
            {
                data         = data?.Copy(),
                imgLocalPath = imgLocalPath
            };
        }
    }

    public sealed class RegionResourceGame : IRegionResourceCopyable<RegionResourceGame>
    {
        public List<RegionResourcePlugin>? plugins           { get; set; }
        public RegionResourceLatest?       game              { get; set; }
        public RegionResourceLatest?       pre_download_game { get; set; }
        public RegionResourceVersion?      sdk               { get; set; }

        public RegionResourceGame Copy()
        {
            return new RegionResourceGame
            {
                plugins           = plugins?.Copy(),
                game              = game?.Copy(),
                pre_download_game = pre_download_game?.Copy(),
                sdk               = sdk?.Copy()
            };
        }
    }

    public sealed class RegionResourcePlugin : IRegionResourceCopyable<RegionResourcePlugin>
    {
        public string?                release_id { get; set; }
        public string?                plugin_id  { get; set; }
        public string?                version    { get; set; }
        public RegionResourceVersion? package    { get; set; }

        public RegionResourcePlugin Copy()
        {
            return new RegionResourcePlugin
            {
                release_id = release_id,
                plugin_id  = plugin_id,
                version    = version,
                package    = package?.Copy()
            };
        }
    }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(List<RegionResourcePluginValidate>))]
    internal sealed partial class RegionResourcePluginValidateJsonContext : JsonSerializerContext;
    public sealed class RegionResourcePluginValidate : IRegionResourceCopyable<RegionResourcePluginValidate>
    {
        public string? path { get; set; }
        public string? md5  { get; set; }

        public RegionResourcePluginValidate Copy()
        {
            return new RegionResourcePluginValidate
            {
                path = path,
                md5  = md5
            };
        }
    }

    public sealed class RegionResourceLatest : IRegionResourceCopyable<RegionResourceLatest>
    {
        public RegionResourceVersion?       latest { get; set; }
        public List<RegionResourceVersion>? diffs  { get; set; }

        public RegionResourceLatest Copy()
        {
            return new RegionResourceLatest
            {
                latest = latest?.Copy(),
                diffs  = diffs?.Copy()
            };
        }
    }

    public sealed class RegionResourceVersion : IRegionResourceCopyable<RegionResourceVersion>
    {
        public string? run_command       { get; set; }
        public string? version           { get; set; }
        public string? url               { get; set; }
        public string? path              { get; set; }
        public string? decompressed_path { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long size { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long package_size { get; set; }

        public string? md5                   { get; set; }
        public string? language              { get; set; }
        public bool    is_recommended_update { get; set; }
        public string? entry                 { get; set; }
        public string? pkg_version           { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? channel_id { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? sub_channel_id { get; set; }

        public List<RegionResourceVersion>? voice_packs { get; set; }
        public List<RegionResourceVersion>? segments    { get; set; }

        [JsonConverter(typeof(RegionResourcePluginValidateConverter))]
        public List<RegionResourcePluginValidate>? validate { get; set; }

        public RegionResourceVersion Copy()
        {
            return new RegionResourceVersion
            {
                version               = version,
                pkg_version           = pkg_version,
                path                  = path,
                url                   = url,
                decompressed_path     = decompressed_path,
                size                  = size,
                package_size          = package_size,
                md5                   = md5,
                language              = language,
                is_recommended_update = is_recommended_update,
                entry                 = entry,
                voice_packs           = voice_packs?.Copy(),
                segments              = segments?.Copy(),
                validate              = validate?.Copy()
            };
        }
    }
}