using CollapseLauncher.Helper.JsonConverter;
using CollapseLauncher.Helper.LauncherApiLoader.Sophon;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
// ReSharper disable StringLiteralTypo

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(HoYoPlayLauncherNews))]
    internal sealed partial class HoYoPlayLauncherNewsJsonContext : JsonSerializerContext;

    public class HoYoPlayLauncherNews
    {
        [JsonPropertyName("data")]
        public LauncherInfoData? Data { get; set; }

        [JsonPropertyName("message")]
        public string? ResultMessage { get; set; }

        [JsonPropertyName("retcode")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? ReturnCode { get; set; }
    }

    public class LauncherInfoData
    {
        [JsonPropertyName("game_info_list")]
        public List<LauncherGameInfo>? GameInfoList { get; set; }

        [JsonPropertyName("content")]
        public LauncherNewsContent? GameNewsContent { get; set; }

        [JsonIgnore]
        public string? BackgroundImageUrl { get => GameInfoList?.FirstOrDefault()?.BackgroundsDetail?.FirstOrDefault()?.BackgroundImage?.ImageUrl; }

        [JsonIgnore]
        public string? FeaturedEventIconUrl { get => GameInfoList?.FirstOrDefault()?.BackgroundsDetail?.FirstOrDefault()?.FeaturedEventIcon?.ImageUrl; }

        [JsonIgnore]
        public string? FeaturedEventIconHoverUrl { get => GameInfoList?.FirstOrDefault()?.BackgroundsDetail?.FirstOrDefault()?.FeaturedEventIcon?.ImageHoverUrl; }

        [JsonIgnore]
        public string? FeaturedEventIconClickLink { get => GameInfoList?.FirstOrDefault()?.BackgroundsDetail?.FirstOrDefault()?.FeaturedEventIcon?.ClickLink; }
    }

    public class LauncherGameInfo
    {
        [JsonPropertyName("backgrounds")]
        public List<LauncherBackgroundsDetail>? BackgroundsDetail { get; set; }

        [JsonPropertyName("game")]
        public GameDetail? GameBiz { get; set; }
    }

    public class LauncherBackgroundsDetail
    {
        [JsonPropertyName("background")]
        public LauncherContentData? BackgroundImage
        {
            get => string.IsNullOrEmpty(field?.ImageUrl) && string.IsNullOrEmpty(field?.ImageHoverUrl) &&
                   string.IsNullOrEmpty(field?.Title)
                   && string.IsNullOrEmpty(field?.ClickLink) && string.IsNullOrEmpty(field?.Date) &&
                   field?.ContentType == null
                ? null
                : field;
            set;
        }

        [JsonPropertyName("icon")]
        public LauncherContentData? FeaturedEventIcon
        {
            get => string.IsNullOrEmpty(field?.ImageUrl) && string.IsNullOrEmpty(field?.ImageHoverUrl) &&
                   string.IsNullOrEmpty(field?.Title)
                   && string.IsNullOrEmpty(field?.ClickLink) && string.IsNullOrEmpty(field?.Date) &&
                   field?.ContentType == null
                ? null
                : field;
            set;
        }

        [JsonPropertyName("id")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Id { get; set; }
    }

    public class LauncherContentData
    {
        [JsonPropertyName("type")]
        public LauncherGameNewsPostType? ContentType { get; set; }

        [JsonPropertyName("date")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Date { get; set; }

        [JsonPropertyName("title")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Title { get; set; }

        [JsonPropertyName("link")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? ClickLink { get; set; }

        [JsonPropertyName("url")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("hover_url")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? ImageHoverUrl { get; set; }
    }

    public class LauncherSocialMedia
    {
        [JsonPropertyName("enable_red_dot")]
        public bool NotificationDotEnabled { get; set; }

        [JsonPropertyName("red_dot_content")]
        public string? NotificationDotContent { get; set; }

        [JsonPropertyName("icon")]
        public LauncherContentData? SocialMediaIcon { get; set; }

        [JsonPropertyName("id")]
        public string? SocialMediaId { get; set; }

        [JsonPropertyName("links")]
        public List<LauncherContentData>? SocialMediaLinks { get; set; }

        [JsonPropertyName("qr_desc")]
        public string? SocialMediaQrDescription { get; set; }

        [JsonPropertyName("qr_image")]
        public LauncherContentData? SocialMediaQrImage { get; set; }
    }

    public class LauncherNewsContent
    {
        [JsonPropertyName("banners")]
        public List<LauncherNewsBanner>? NewsCarouselList { get; set; }

        [JsonPropertyName("posts")]
        public List<LauncherContentData>? NewsPostsList { get; set; }

        [JsonPropertyName("social_media_list")]
        public List<LauncherSocialMedia>? SocialMediaList { get; set; }
    }

    public class LauncherNewsBanner
    {
        [JsonPropertyName("id")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Id { get; set; }

        [JsonPropertyName("image")]
        public LauncherContentData? Image { get; set; }
    }
}
