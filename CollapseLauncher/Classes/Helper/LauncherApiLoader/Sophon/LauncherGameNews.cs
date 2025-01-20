#nullable enable
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.JsonConverter;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading;
using WinRT;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher.Helper.LauncherApiLoader.Sophon
{
    [JsonConverter(typeof(JsonStringEnumConverter<LauncherGameNewsPostType>))]
    public enum LauncherGameNewsPostType
    {
        POST_TYPE_INFO,
        POST_TYPE_ACTIVITY,
        POST_TYPE_ANNOUNCE
    }

    [JsonConverter(typeof(JsonStringEnumConverter<LauncherGameAvailabilityStatus>))]
    public enum LauncherGameAvailabilityStatus
    {
        LAUNCHER_GAME_DISPLAY_STATUS_AVAILABLE,
        LAUNCHER_GAME_DISPLAY_STATUS_RESERVATION_ENABLED,
        LAUNCHER_GAME_DISPLAY_STATUS_COMING_SOON
    }

    public interface ILauncherGameNewsDataTokenized
    {
        public CancellationToken? InnerToken { get; set; }
        public ILauncherApi? LauncherApi { get; set; }
    }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(LauncherGameNews))]
    internal sealed partial class LauncherGameNewsJsonContext : JsonSerializerContext;

    public sealed class LauncherGameNews
    {
        [JsonPropertyName("retcode")] public int? ReturnCode { get; init; }

        [JsonPropertyName("message")] public string? ReturnMessage { get; init; }

        [JsonPropertyName("data")] public LauncherGameNewsData? Content { get; set; }
    }

    public sealed class LauncherGameNewsData
    {
        public HoYoPlayGameInfoField GameInfoField { get; init; } = new();

        [JsonPropertyName("adv")] public LauncherGameNewsBackground? Background { get; set; }

        [JsonPropertyName("banner")]
        public List<LauncherGameNewsCarousel>? NewsCarousel
        {
            get;
            set => field = value?.OrderBy(x => x.CarouselOrder).ToList();
        }

        [JsonPropertyName("post")]
        public List<LauncherGameNewsPost>? NewsPost
        {
            get;
            set => field = value?.OrderBy(x => x.PostOrder).ToList();
        }

        [JsonPropertyName("icon")] public List<LauncherGameNewsSocialMedia>? SocialMedia { get; set; }

        [JsonIgnore]
        public List<LauncherGameNewsPost>? NewsPostTypeInfo
        {
            get
            {
                if (NewsPost == null)
                {
                    return null;
                }

                if (field != null)
                {
                    return field;
                }

                field = NewsPost.Where(x => x.PostType == LauncherGameNewsPostType.POST_TYPE_INFO)
                                .OrderBy(x => x.PostOrder)
                                .ToList();
                return field;
            }
        }

        [JsonIgnore]
        public List<LauncherGameNewsPost>? NewsPostTypeActivity
        {
            get
            {
                if (NewsPost == null)
                {
                    return null;
                }

                if (field != null)
                {
                    return field;
                }

                field = NewsPost.Where(x => x.PostType == LauncherGameNewsPostType.POST_TYPE_ACTIVITY)
                                .OrderBy(x => x.PostOrder)
                                .ToList();
                return field;
            }
        }

        [JsonIgnore]
        public List<LauncherGameNewsPost>? NewsPostTypeAnnouncement
        {
            get
            {
                if (NewsPost == null)
                {
                    return null;
                }

                if (field != null)
                {
                    return field;
                }

                field = NewsPost
                       .Where(x => x.PostType == LauncherGameNewsPostType.POST_TYPE_ANNOUNCE)
                       .OrderBy(x => x.PostOrder)
                       .ToList();
                return field;
            }
        }

        public void InjectDownloadableItemCancelToken(ILauncherApi? launcherApi, CancellationToken token)
        {
            InjectDownloadableItemCancelTokenInner(NewsCarousel, launcherApi, token);
            InjectDownloadableItemCancelTokenInner(SocialMedia,  launcherApi, token);
        }

        private static void InjectDownloadableItemCancelTokenInner<T>(IEnumerable<T>? prop, ILauncherApi? launcherApi, CancellationToken token)
            where T : ILauncherGameNewsDataTokenized
        {
            if (prop == null)
            {
                return;
            }

            foreach (T? propValue in prop)
            {
                InjectDownloadableItemCancelTokenInner(propValue, launcherApi, token);
            }
        }

        private static void InjectDownloadableItemCancelTokenInner<T>(T? prop, ILauncherApi? launcherApi, CancellationToken token)
            where T : ILauncherGameNewsDataTokenized
        {
            if (prop == null)
            {
                return;
            }

            prop.LauncherApi = launcherApi;
            prop.InnerToken = token;
        }
    }

    public class LauncherGameNewsBackground
    {
        [JsonPropertyName("background")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? BackgroundImg { get; set; }

        [JsonPropertyName("version")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? BackgroundVersion { get; init; }

        [JsonPropertyName("bg_checksum")] public string? BackgroundChecksum { get; init; }

        [JsonPropertyName("icon")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? FeaturedEventIconBtnImg { get; set; }

        [JsonPropertyName("url")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]

        public string? FeaturedEventIconBtnUrl { get; set; }
    }

    public class LauncherUIResourceBase
    {
        public LauncherUIResourceBase()
        { }

        public LauncherUIResourceBase(ILauncherApi? launcherApi)
        {
            LauncherApi = launcherApi;
        }

        public ILauncherApi? LauncherApi { get; set; }

        public HttpClient? CurrentHttpClient { get => field ??= LauncherApi?.ApiResourceHttpClient; }
    }

    public class LauncherGameNewsCarousel : LauncherUIResourceBase, ILauncherGameNewsDataTokenized
    {
        [JsonIgnore] public CancellationToken? InnerToken { get; set; }

        [JsonPropertyName("banner_id")] public string? CarouselId { get; init; }

        [JsonPropertyName("name")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? CarouselTitle { get; init; }

        [JsonPropertyName("img")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? CarouselImg
        {
            get => ImageLoaderHelper.GetCachedSprites(CurrentHttpClient, field, InnerToken ?? default);
            init;
        }

        [JsonPropertyName("url")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? CarouselUrl { get; init; }

        // TODO: Make sure that the value always be a number. If yes, then we
        //       will change the type from string? to int?
        [JsonPropertyName("order")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? CarouselOrder { get; init; }
    }

    [GeneratedBindableCustomProperty]
    public partial class LauncherGameNewsSocialMedia : LauncherUIResourceBase, ILauncherGameNewsDataTokenized
    {
        private readonly string? _socialMediaUrl;

        [JsonIgnore] public CancellationToken? InnerToken { get; set; }

        [JsonPropertyName("icon_id")] public string? IconId { get; init; }

        [JsonPropertyName("img")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? IconImg
        {
            get => ImageLoaderHelper.GetCachedSprites(CurrentHttpClient, field, InnerToken ?? default);
            init;
        }

        [JsonPropertyName("img_hover")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? IconImgHover
        {
            get => ImageLoaderHelper.GetCachedSprites(CurrentHttpClient, field, InnerToken ?? default);
            init;
        }

        [JsonPropertyName("url")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? SocialMediaUrl
        {
            get => string.IsNullOrEmpty(_socialMediaUrl) && QrLinks != null && QrLinks.Count != 0
                ? QrLinks[0].Url
                : _socialMediaUrl;

            init => _socialMediaUrl = value;
        }

        [JsonPropertyName("title")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Title
        {
            get => string.IsNullOrEmpty(field) || (QrLinks?.Any(x => x.Title == field) ?? false) ? null : field;
            init;
        }

        [JsonPropertyName("qr_img")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? QrImg
        {
            get => ImageLoaderHelper.GetCachedSprites(CurrentHttpClient, field, InnerToken ?? default);
            init;
        }

        [JsonPropertyName("qr_desc")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? QrTitle { get; init; }

        [JsonPropertyName("links")]
        public List<LauncherGameNewsSocialMediaQrLinks>? QrLinks
        {
            get;
            init
            {
                field = value?.Where(x => !(string.IsNullOrEmpty(x.Url) || string.IsNullOrEmpty(x.Title))).ToList();
                if (field?.Count == 0)
                    field = null;
            }
        }

        [JsonIgnore] public bool IsHasDescription => !string.IsNullOrEmpty(Title);

        [JsonIgnore]
        public bool IsHasLinks => (QrLinks?.Count ?? 0) != 0 || (!string.IsNullOrEmpty(QrLinks?[0].Url) &&
                                  QrLinks?[0].Url != _socialMediaUrl);

        [JsonIgnore] public bool IsHasQr => !string.IsNullOrEmpty(QrImg);

        [JsonIgnore] public bool IsHasQrDescription => !string.IsNullOrEmpty(QrTitle);
    }

    [GeneratedBindableCustomProperty]
    public partial class LauncherGameNewsSocialMediaQrLinks
    {
        [JsonPropertyName("title")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Title { get; init; }

        [JsonPropertyName("url")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? Url { get; init; }
    }

    public class LauncherGameNewsPost
    {
        [JsonPropertyName("post_id")]
        public string? PostId { get; init; }

        [JsonPropertyName("type")]
        public LauncherGameNewsPostType? PostType { get; init; }

        [JsonPropertyName("url")]
        [JsonConverter(typeof(SanitizeUrlStringConverter))]
        public string? PostUrl { get; init; }

        [JsonPropertyName("show_time")]
        public string? PostDate { get; init; }

        [JsonPropertyName("title")]
        [JsonConverter(typeof(EmptyStringAsNullConverter))]
        public string? Title
        {
            get => string.IsNullOrEmpty(field) ? PostUrl : field;
            init;
        }

        // TODO: Make sure that the value always be a number. If yes, then we
        //       will change the type from string? to int?
        [JsonPropertyName("order")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? PostOrder { get; init; }
    }
}