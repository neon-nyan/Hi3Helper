using CollapseLauncher.Helper.LauncherApiLoader.Sophon;
using System.Collections.Generic;
using System.Text.Json.Serialization;
// ReSharper disable StringLiteralTypo

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(HoYoPlayLauncherGameInfo))]
    internal sealed partial class HoYoPlayLauncherGameInfoJsonContext : JsonSerializerContext;

    public sealed class HoYoPlayLauncherGameInfo
    {
        [JsonPropertyName("retcode")] public int? ReturnCode { get; init; }

        [JsonPropertyName("message")] public string? ReturnMessage { get; init; }

        [JsonPropertyName("data")] public HoYoPlayGameInfoData? GameInfoData { get; init; }
    }

    public sealed class HoYoPlayGameInfoData
    {
        [JsonPropertyName("games")] public List<HoYoPlayGameInfoField>? Data { get; init; }
        [JsonPropertyName("game_branches")] public List<HoYoPlayGameInfoBranch>? GameBranchesInfo { get; init; }
    }

    public sealed class HoYoPlayGameInfoBranch
    {
        [JsonPropertyName("game")] public HoYoPlayGameInfoField? GameInfo { get; init; }
        [JsonPropertyName("main")] public HoYoPlayGameInfoBranchField? GameMainField { get; init; }
        [JsonPropertyName("pre_download")] public HoYoPlayGameInfoBranchField? GamePreloadField { get; init; }
    }

    public sealed class HoYoPlayGameInfoBranchField
    {
        [JsonPropertyName("package_id")] public string? PackageId { get; init; }
        [JsonPropertyName("branch")] public string? Branch { get; init; }
        [JsonPropertyName("password")] public string? Password { get; init; }
        [JsonPropertyName("tag")] public string? Tag { get; init; }
    }

    public sealed class HoYoPlayGameInfoField
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("biz")]
        public string? BizName { get; init; }

        [JsonPropertyName("display")]
        public HoYoPlayGameInfoDisplay? Display { get; init; } = new();

        [JsonPropertyName("reservation")]
        public LauncherContentData? ReservationLink { get; init; } = new();

        [JsonPropertyName("display_status")]
        public LauncherGameAvailabilityStatus DisplayStatus { get; init; } = LauncherGameAvailabilityStatus.LAUNCHER_GAME_DISPLAY_STATUS_AVAILABLE;
    }

    public sealed class HoYoPlayGameInfoDisplay
    {
        [JsonPropertyName("language")]
        public string? Language { get; init; }

        [JsonPropertyName("name")]
        public string? GameName { get; init; }

        [JsonPropertyName("title")]
        public string? DisplayTitle { get; init; }

        [JsonPropertyName("subtitle")]
        public string? DisplaySubtitle { get; init; }

        [JsonPropertyName("icon")]
        public LauncherContentData? GamePreviewIcon { get; init; }

        [JsonPropertyName("background")]
        public LauncherContentData? GamePreviewBackground { get; init; }

        [JsonPropertyName("logo")]
        public LauncherContentData? GamePreviewLogo { get; init; }

        [JsonPropertyName("thumbnail")]
        public LauncherContentData? GamePreviewThumbnail { get; init; }
    }
}
