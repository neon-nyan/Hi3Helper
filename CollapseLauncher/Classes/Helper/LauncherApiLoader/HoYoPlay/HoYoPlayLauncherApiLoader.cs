using CollapseLauncher.Extension;
using CollapseLauncher.Helper.LauncherApiLoader.Sophon;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable IdentifierTypo
// ReSharper disable once CheckNamespace
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay
{
    internal sealed partial class HoYoPlayLauncherApiLoader : LauncherApiBase
    {
        public override HttpClient ApiGeneralHttpClient { get; protected set; }

        public override HttpClient ApiResourceHttpClient { get; protected set; }

        private HoYoPlayLauncherApiLoader(PresetConfig presetConfig, string gameName, string gameRegion)
            : base(presetConfig, gameName, gameRegion, true)
        {
            // Set the HttpClientBuilder for HoYoPlay's own General API.
            HttpClientBuilder<SocketsHttpHandler> apiGeneralHttpBuilder = new HttpClientBuilder()
                .UseLauncherConfig()
                .AllowUntrustedCert()
                .SetHttpVersion(HttpVersion.Version30)
                .SetAllowedDecompression()
                .AddHeader("x-rpc-device_id", GetDeviceId(presetConfig));

            // Set the HttpClientBuilder for HoYoPlay's own Resource API.
            HttpClientBuilder<SocketsHttpHandler> apiResourceHttpBuilder = new HttpClientBuilder()
                .UseLauncherConfig()
                .AllowUntrustedCert()
                .SetHttpVersion(HttpVersion.Version30)
                .SetAllowedDecompression(DecompressionMethods.None)
                .AddHeader("x-rpc-device_id", GetDeviceId(presetConfig));

            // If the metadata has user-agent defined, set the resource's HttpClient user-agent
            if (!string.IsNullOrEmpty(presetConfig.ApiGeneralUserAgent))
            {
                apiGeneralHttpBuilder.SetUserAgent(presetConfig.ApiGeneralUserAgent);
            }
            if (!string.IsNullOrEmpty(presetConfig.ApiResourceUserAgent))
            {
                apiResourceHttpBuilder.SetUserAgent(string.Format(presetConfig.ApiResourceUserAgent, InnerLauncherConfig.m_isWindows11 ? "11" : "10"));
            }

            // Add other API general and resource headers from the metadata configuration
            presetConfig.AddApiGeneralAdditionalHeaders((key, value) => apiGeneralHttpBuilder.AddHeader(key, value));
            presetConfig.AddApiResourceAdditionalHeaders((key, value) => apiResourceHttpBuilder.AddHeader(key, value));

            // Create HttpClient instances for both General and Resource APIs.
            ApiGeneralHttpClient  = apiGeneralHttpBuilder.Create();
            ApiResourceHttpClient = apiResourceHttpBuilder.Create();
        }

        public static ILauncherApi CreateApiInstance(PresetConfig presetConfig, string gameName, string gameRegion)
            => new HoYoPlayLauncherApiLoader(presetConfig, gameName, gameRegion);

        protected override async Task LoadLauncherGameResource(ActionOnTimeOutRetry? onTimeoutRoutine, CancellationToken token)
        {
            ActionTimeoutValueTaskCallback<HoYoPlayLauncherResources?> hypResourceResponseCallback =
                async innerToken => await ApiGeneralHttpClient.GetFromJsonAsync(
                    PresetConfig?.LauncherResourceURL,
                    HoYoPlayLauncherResourcesJsonContext.Default.HoYoPlayLauncherResources,
                    innerToken);

            // Assign as 3 Task array
            Task[] tasks = [
                Task.CompletedTask,
                Task.CompletedTask,
                Task.CompletedTask
                ];

            // Init as null first before being assigned when the backing task is called
            HoYoPlayLauncherResources? hypResourceResponse = null;
            HoYoPlayLauncherResources? hypPluginResource = null;
            HoYoPlayLauncherResources? hypSdkResource = null;

            tasks[0] = hypResourceResponseCallback
                .WaitForRetryAsync(ExecutionTimeout, ExecutionTimeoutStep, ExecutionTimeoutAttempt, onTimeoutRoutine, token)
                .AsTaskAndDoAction((result) => hypResourceResponse = result);

            if (!string.IsNullOrEmpty(PresetConfig?.LauncherPluginURL) && (PresetConfig.IsPluginUpdateEnabled ?? false))
            {
                ActionTimeoutValueTaskCallback<HoYoPlayLauncherResources?> hypPluginResourceCallback =
                    async innerToken =>
                        await ApiGeneralHttpClient.GetFromJsonAsync(
                            PresetConfig?.LauncherPluginURL,
                            HoYoPlayLauncherResourcesJsonContext.Default.HoYoPlayLauncherResources,
                            innerToken);

                tasks[1] = hypPluginResourceCallback
                    .WaitForRetryAsync(ExecutionTimeout, ExecutionTimeoutStep, ExecutionTimeoutAttempt, onTimeoutRoutine, token)
                    .AsTaskAndDoAction((result) => hypPluginResource = result);
            }

            if (!string.IsNullOrEmpty(PresetConfig?.LauncherGameChannelSDKURL))
            {
                ActionTimeoutValueTaskCallback<HoYoPlayLauncherResources?> hypSdkResourceCallback =
                    async innerToken =>
                        await ApiGeneralHttpClient.GetFromJsonAsync(
                            PresetConfig?.LauncherGameChannelSDKURL,
                            HoYoPlayLauncherResourcesJsonContext.Default.HoYoPlayLauncherResources,
                            innerToken);

                tasks[2] = hypSdkResourceCallback
                    .WaitForRetryAsync(ExecutionTimeout, ExecutionTimeoutStep, ExecutionTimeoutAttempt, onTimeoutRoutine, token)
                    .AsTaskAndDoAction((result) => hypSdkResource = result);
            }

            RegionResourceLatest sophonResourceCurrentPackage = new RegionResourceLatest();
            RegionResourceGame sophonResourceData = new RegionResourceGame
            {
                game = sophonResourceCurrentPackage
            };

            RegionResourceProp sophonResourcePropRoot = new RegionResourceProp
            {
                data = sophonResourceData
            };

            // Await all callbacks
            await Task.WhenAll(tasks);

            ConvertPluginResources(ref sophonResourceData, hypPluginResource);
            ConvertSdkResources(ref sophonResourceData, hypSdkResource);
            ConvertPackageResources(sophonResourceData, hypResourceResponse?.Data?.LauncherPackages);

            LauncherGameResource = sophonResourcePropRoot;

            PerformDebugRoutines();
        }

        #region Convert Sdk Resources
        private void ConvertSdkResources(ref RegionResourceGame sophonResourceData, HoYoPlayLauncherResources? hypSdkResources)
        {
            LauncherSdkPackages? sdkPackages = hypSdkResources?.Data?.ChannelSdks?
                .FirstOrDefault(x => x.GameDetail?
                    .GameBiz?
                    .Equals(PresetConfig?.LauncherBizName, StringComparison.OrdinalIgnoreCase) ?? false);

            if (sdkPackages?.SdkPackageDetail == null) return;

            sophonResourceData.sdk = new RegionResourceVersion
            {
                md5 = sdkPackages.SdkPackageDetail.PackageMD5Hash,
                size = sdkPackages.SdkPackageDetail.PackageDecompressSize,
                package_size = sdkPackages.SdkPackageDetail.PackageSize ?? 0,
                path = sdkPackages.SdkPackageDetail.PackageUrl,
                url = sdkPackages.SdkPackageDetail.PackageUrl,
                version = sdkPackages.Version,
                pkg_version = sdkPackages.PkgVersionFileName
            };
        }
        #endregion

        #region Convert Plugin Resources
        private void ConvertPluginResources(ref RegionResourceGame sophonResourceData, HoYoPlayLauncherResources? hypPluginResources)
        {
            LauncherPackages? hypPluginPackage = hypPluginResources?.Data?
                .PluginPackages?
                .FirstOrDefault(x => x.GameDetail?
                    .GameBiz?
                    .Equals(PresetConfig?.LauncherBizName, StringComparison.OrdinalIgnoreCase) ?? false);

            if (hypPluginPackage == null) return;

            List<RegionResourcePlugin> pluginCurrentPackageList = [];
            GuessAssignPluginConversion(pluginCurrentPackageList, hypPluginPackage);
            sophonResourceData.plugins = pluginCurrentPackageList;
        }

        private static void GuessAssignPluginConversion(List<RegionResourcePlugin> sophonPluginList, LauncherPackages hypPlugin)
        {
            List<PackagePluginSections>? pluginSectionsList = hypPlugin.PluginPackageSections;
            if ((pluginSectionsList?.Count ?? 0) == 0) return;
            if (pluginSectionsList == null) return;

            sophonPluginList.AddRange(pluginSectionsList.Select(firstPluginSection => new RegionResourcePlugin
            {
                version    = firstPluginSection.Version,
                plugin_id  = firstPluginSection.PluginId,
                release_id = firstPluginSection.ReleaseId,
                package = new RegionResourceVersion
                {
                    validate     = firstPluginSection.PluginPackage?.PackageAssetValidationList,
                    md5          = firstPluginSection.PluginPackage?.PackageMD5Hash,
                    url          = firstPluginSection.PluginPackage?.PackageUrl,
                    path         = firstPluginSection.PluginPackage?.PackageUrl,
                    size         = firstPluginSection.PluginPackage?.PackageDecompressSize ?? 0,
                    package_size = firstPluginSection.PluginPackage?.PackageSize ?? 0,
                    run_command  = firstPluginSection.PluginPackage?.PackageRunCommand,
                    version      = firstPluginSection.Version
                }
            }));
        }
        #endregion

        #region Convert Package Resources
        private void ConvertPackageResources(RegionResourceGame sophonPackageResources, List<LauncherPackages>? hypLauncherPackagesList)
        {
            if (hypLauncherPackagesList == null) throw new NullReferenceException("HoYoPlay package list is null!");

            foreach (LauncherPackages hypRootPackage in hypLauncherPackagesList
                .Where(x => x.GameDetail?.GameBiz?
                    .Equals(PresetConfig?.LauncherBizName, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                // Assign and convert main game package (latest)
                PackageResourceSections? hypMainPackageSection    = hypRootPackage.MainPackage?.CurrentVersion;
                RegionResourceVersion    sophonMainPackageSection = new RegionResourceVersion();
                if (hypMainPackageSection != null)
                    ConvertHYPSectionToResourceVersion(ref hypMainPackageSection, ref sophonMainPackageSection);

                // If the main game package reference is null, skip it
                if (sophonPackageResources.game == null)
                    continue;

                sophonPackageResources.game.latest = sophonMainPackageSection;

                // Assign and convert main game package (diff)
                if (hypRootPackage.MainPackage?.Patches != null)
                {
                    sophonPackageResources.game.diffs = [];
                    foreach (PackageResourceSections hypMainDiffPackageSection in hypRootPackage.MainPackage.Patches)
                    {
                        PackageResourceSections hypMainDiffPackageSectionRef = hypMainDiffPackageSection;
                        RegionResourceVersion   sophonResourceVersion        = new RegionResourceVersion();
                        ConvertHYPSectionToResourceVersion(ref hypMainDiffPackageSectionRef, ref sophonResourceVersion);
                        sophonPackageResources.game.diffs.Add(sophonResourceVersion);
                    }
                }
                sophonPackageResources.pre_download_game = new RegionResourceLatest();

                // Convert if preload entry is not empty or null
                if (hypRootPackage.PreDownload?.CurrentVersion == null &&
                    (hypRootPackage.PreDownload?.Patches?.Count ?? 0) == 0)
                {
                    continue;
                }

                {
                    // Assign and convert preload game package (latest)
                    PackageResourceSections? hypPreloadPackageSection = hypRootPackage.PreDownload?.CurrentVersion;
                    if (hypPreloadPackageSection != null)
                    {
                        RegionResourceVersion sophonPreloadPackageSection = new RegionResourceVersion();
                        ConvertHYPSectionToResourceVersion(ref hypPreloadPackageSection, ref sophonPreloadPackageSection);
                        sophonPackageResources.pre_download_game.latest = sophonPreloadPackageSection;
                    }

                    // Assign and convert preload game package (diff)
                    if (hypRootPackage.PreDownload?.Patches == null || hypRootPackage.PreDownload.Patches.Count == 0)
                    {
                        continue;
                    }

                    sophonPackageResources.pre_download_game.diffs = [];
                    foreach (PackageResourceSections hypPreloadDiffPackageSection in hypRootPackage.PreDownload
                                .Patches)
                    {
                        PackageResourceSections hypPreloadDiffPackageSectionRef = hypPreloadDiffPackageSection;
                        RegionResourceVersion   sophonResourceVersion           = new RegionResourceVersion();
                        ConvertHYPSectionToResourceVersion(ref hypPreloadDiffPackageSectionRef,
                                                           ref sophonResourceVersion);
                        sophonPackageResources.pre_download_game.diffs.Add(sophonResourceVersion);
                    }
                }
            }
        }

        private void ConvertHYPSectionToResourceVersion(ref PackageResourceSections hypPackageResourceSection, ref RegionResourceVersion sophonResourceVersion)
        {
            ArgumentNullException.ThrowIfNull(hypPackageResourceSection, nameof(hypPackageResourceSection));
            ArgumentNullException.ThrowIfNull(sophonResourceVersion,     nameof(sophonResourceVersion));

            // Convert game packages
            RegionResourceVersion packagesVersion = new RegionResourceVersion();
            DelegatePackageResConversionMode(ref packagesVersion, hypPackageResourceSection.GamePackages, hypPackageResourceSection.AudioPackages, hypPackageResourceSection.Version, hypPackageResourceSection.ResourceListUrl);
            sophonResourceVersion = packagesVersion;
        }

        private delegate void PackageResConversionModeDelegate(ref RegionResourceVersion sophonPackageVersion, List<PackageDetails> hypPackageList,
            string? version, string? resourceListUrl);

        private void DelegatePackageResConversionMode(ref RegionResourceVersion sophonPackageVersion, List<PackageDetails>? hypGamePackageList, List<PackageDetails>? hypAudioPackageList,
            string? version, string? resourceListUrl)
        {
            // If the main package list is not null or empty, then process
            if (hypGamePackageList != null && hypGamePackageList.Count != 0)
            {
                // Delegate the conversion mode for the resource, then process it
                PackageResConversionModeDelegate conversionDelegate = hypGamePackageList.Count > 1 ? ConvertMultiPackageResource : ConvertSinglePackageResource;
                conversionDelegate(ref sophonPackageVersion, hypGamePackageList, version, resourceListUrl);
            }

            // If the audio package list is not null or empty, then process
            if (hypAudioPackageList == null || hypAudioPackageList.Count == 0)
            {
                return;
            }

            sophonPackageVersion.voice_packs = [];
            foreach (PackageDetails hypAudioPackage in hypAudioPackageList)
            {
                sophonPackageVersion.voice_packs.Add(new RegionResourceVersion
                {
                    url          = hypAudioPackage.PackageUrl,
                    path         = hypAudioPackage.PackageUrl, // As fallback for PackageUrl
                    size         = hypAudioPackage.PackageDecompressSize,
                    package_size = hypAudioPackage.PackageSize ?? 0,
                    md5          = hypAudioPackage.PackageMD5Hash,
                    language     = hypAudioPackage.Language
                });
            }
        }

        private void ConvertSinglePackageResource(ref RegionResourceVersion sophonPackageVersion, List<PackageDetails> hypPackageList,
            string? version, string? resourceListUrl)
        {
            PackageDetails packageDetail = hypPackageList[0];
            sophonPackageVersion.url = packageDetail.PackageUrl;
            sophonPackageVersion.path = packageDetail.PackageUrl; // As fallback for PackageUrl
            sophonPackageVersion.size = packageDetail.PackageDecompressSize;
            sophonPackageVersion.package_size = packageDetail.PackageSize ?? 0;
            sophonPackageVersion.md5 = packageDetail.PackageMD5Hash;
            sophonPackageVersion.entry = PresetConfig?.GameExecutableName;
            sophonPackageVersion.version = version;
            sophonPackageVersion.decompressed_path = resourceListUrl;
        }

        private void ConvertMultiPackageResource(ref RegionResourceVersion sophonPackageVersion, List<PackageDetails> hypPackageList,
            string? version, string? resourceListUrl)
        {
            long totalSize = hypPackageList.Sum(x => x.PackageDecompressSize);
            long totalPackageSize = hypPackageList.Sum(x => x.PackageSize ?? 0);

            sophonPackageVersion.url = string.Empty;
            sophonPackageVersion.path = string.Empty; // As fallback for PackageUrl
            sophonPackageVersion.md5 = string.Empty;
            sophonPackageVersion.size = totalSize;
            sophonPackageVersion.package_size = totalPackageSize;
            sophonPackageVersion.entry = PresetConfig?.GameExecutableName;
            sophonPackageVersion.version = version;
            sophonPackageVersion.decompressed_path = resourceListUrl;

            sophonPackageVersion.segments = [];

            foreach (PackageDetails packageDetail in hypPackageList)
            {
                sophonPackageVersion.segments.Add(new RegionResourceVersion
                {
                    url = packageDetail.PackageUrl,
                    path = packageDetail.PackageUrl, // As fallback for PackageUrl
                    size = packageDetail.PackageDecompressSize,
                    package_size = packageDetail.PackageSize ?? 0,
                    md5 = packageDetail.PackageMD5Hash
                });
            }
        }
        #endregion

        protected override async Task LoadLauncherNews(ActionOnTimeOutRetry? onTimeoutRoutine, CancellationToken token)
        {
            bool isUseMultiLang = PresetConfig?.LauncherSpriteURLMultiLang ?? false;

            string localeCode = isUseMultiLang ? Locale.Lang.LanguageID.ToLower() : PresetConfig?.LauncherSpriteURLMultiLangFallback!;
            string launcherSpriteUrl = string.Format(PresetConfig?.LauncherSpriteURL!, localeCode);
            string launcherNewsUrl = string.Format(PresetConfig?.LauncherNewsURL!, localeCode);

            ActionTimeoutValueTaskCallback<HoYoPlayLauncherNews?> hypLauncherBackgroundCallback =
                async innerToken =>
                    await ApiResourceHttpClient!.GetFromJsonAsync(
                        launcherSpriteUrl,
                        HoYoPlayLauncherNewsJsonContext.Default.HoYoPlayLauncherNews,
                        innerToken);

            ActionTimeoutValueTaskCallback<HoYoPlayLauncherNews?> hypLauncherNewsCallback =
                async innerToken =>
                    await ApiResourceHttpClient!.GetFromJsonAsync(
                        launcherNewsUrl,
                        HoYoPlayLauncherNewsJsonContext.Default.HoYoPlayLauncherNews,
                        innerToken);

            HoYoPlayLauncherNews? hypLauncherBackground = null;
            HoYoPlayLauncherNews? hypLauncherNews = null;

            // Load both in parallel
            Task[] tasks = [
                hypLauncherBackgroundCallback.WaitForRetryAsync(ExecutionTimeout, ExecutionTimeoutStep,
                                                           ExecutionTimeoutAttempt, onTimeoutRoutine, token).AsTaskAndDoAction((result) => hypLauncherBackground = result),
                hypLauncherNewsCallback.WaitForRetryAsync(ExecutionTimeout, ExecutionTimeoutStep,
                                                           ExecutionTimeoutAttempt, onTimeoutRoutine, token).AsTaskAndDoAction((result) => hypLauncherNews = result)
                ];

            // Await the result
            await Task.WhenAll(tasks);

            // Merge background image
            if (hypLauncherBackground?.Data?.GameInfoList != null && hypLauncherNews?.Data != null)
                hypLauncherNews.Data.GameInfoList = hypLauncherBackground.Data?.GameInfoList;

            LauncherGameNews? sophonLauncherNewsRoot = null;
            EnsureInitializeSophonLauncherNews(ref sophonLauncherNewsRoot);
            ConvertLauncherBackground(ref sophonLauncherNewsRoot, hypLauncherNews?.Data);
            ConvertLauncherNews(ref sophonLauncherNewsRoot, hypLauncherNews?.Data);
            ConvertLauncherSocialMedia(ref sophonLauncherNewsRoot, hypLauncherNews?.Data);

            LauncherGameNews = sophonLauncherNewsRoot;
            LauncherGameNews?.Content?.InjectDownloadableItemCancelToken(this, token);
        }

        #region Convert Launcher News
        private static void EnsureInitializeSophonLauncherNews(ref LauncherGameNews? sophonLauncherNewsData)
        {
            sophonLauncherNewsData                      ??= new LauncherGameNews();
            sophonLauncherNewsData.Content              ??= new LauncherGameNewsData();
            sophonLauncherNewsData.Content.Background   ??= new LauncherGameNewsBackground();
            sophonLauncherNewsData.Content.NewsPost     ??= [];
            sophonLauncherNewsData.Content.NewsCarousel ??= [];
            sophonLauncherNewsData.Content.SocialMedia  ??= [];
        }

        private static void ConvertLauncherBackground(ref LauncherGameNews? sophonLauncherNewsData, LauncherInfoData? hypLauncherInfoData)
        {
            if (string.IsNullOrEmpty(hypLauncherInfoData?.BackgroundImageUrl)) return;

            if (sophonLauncherNewsData?.Content?.Background == null) return;
            sophonLauncherNewsData.Content.Background.BackgroundImg           = hypLauncherInfoData.BackgroundImageUrl;
            sophonLauncherNewsData.Content.Background.FeaturedEventIconBtnImg = hypLauncherInfoData.FeaturedEventIconUrl;
            sophonLauncherNewsData.Content.Background.FeaturedEventIconBtnUrl = hypLauncherInfoData.FeaturedEventIconClickLink;
        }

        private static void ConvertLauncherNews(ref LauncherGameNews? sophonLauncherNewsData, LauncherInfoData? hypLauncherInfoData)
        {
            int index = 0;
            if (hypLauncherInfoData?.GameNewsContent?.NewsPostsList != null)
            {
                // Post
                foreach (LauncherContentData hypPostData in hypLauncherInfoData.GameNewsContent.NewsPostsList)
                {
                    sophonLauncherNewsData?.Content?.NewsPost?.Add(new LauncherGameNewsPost
                    {
                        PostDate = hypPostData.Date,
                        PostUrl = hypPostData.ClickLink,
                        PostId = string.Empty,
                        PostOrder = index++,
                        PostType = hypPostData.ContentType,
                        Title = hypPostData.Title
                    });
                }
            }

            index = 0;
            if (hypLauncherInfoData?.GameNewsContent?.NewsCarouselList == null)
            {
                return;
            }

            // Carousel
            foreach (LauncherNewsBanner hypCarouselData in hypLauncherInfoData.GameNewsContent.NewsCarouselList)
            {
                sophonLauncherNewsData?.Content?.NewsCarousel?.Add(new LauncherGameNewsCarousel
                {
                    CarouselId    = hypCarouselData.Id,
                    CarouselOrder = index++,
                    CarouselImg   = hypCarouselData.Image?.ImageUrl,
                    CarouselUrl   = hypCarouselData.Image?.ClickLink,
                    CarouselTitle = hypCarouselData.Image?.Title
                });
            }
        }

        private static void ConvertLauncherSocialMedia(ref LauncherGameNews? sophonLauncherNewsData, LauncherInfoData? hypLauncherInfoData)
        {
            if (hypLauncherInfoData?.GameNewsContent?.SocialMediaList == null)
            {
                return;
            }

            // Social Media list
            foreach (LauncherSocialMedia hypSocMedData in hypLauncherInfoData.GameNewsContent.SocialMediaList)
            {
                sophonLauncherNewsData?.Content?.SocialMedia?.Add(new LauncherGameNewsSocialMedia
                {
                    IconId       = hypSocMedData.SocialMediaId,
                    IconImg      = hypSocMedData.SocialMediaIcon?.ImageUrl,
                    IconImgHover = string.IsNullOrEmpty(hypSocMedData.SocialMediaIcon?.ImageHoverUrl) ? hypSocMedData.SocialMediaIcon?.ImageUrl : hypSocMedData.SocialMediaIcon?.ImageHoverUrl,
                    Title        = hypSocMedData.SocialMediaIcon?.Title,
                    SocialMediaUrl = (hypSocMedData.SocialMediaLinks?.Count ?? 0) != 0 ?
                        hypSocMedData.SocialMediaLinks?.FirstOrDefault()?.ClickLink :
                        hypSocMedData.SocialMediaIcon?.ClickLink,
                    QrImg   = hypSocMedData.SocialMediaQrImage?.ImageUrl,
                    QrTitle = hypSocMedData.SocialMediaQrDescription,
                    QrLinks = hypSocMedData.SocialMediaLinks?.Select(x =>
                                                                     {
                                                                         LauncherGameNewsSocialMediaQrLinks qrLink = new LauncherGameNewsSocialMediaQrLinks
                                                                         {
                                                                             Title = x.Title,
                                                                             Url   = x.ClickLink
                                                                         };
                                                                         return qrLink;
                                                                     }).ToList()
                });
            }
        }
        #endregion

        protected override async Task LoadLauncherGameInfo(ActionOnTimeOutRetry? onTimeoutRoutine, CancellationToken token)
        {
            if (PresetConfig?.LauncherGameInfoDisplayURL == null)
            {
                LauncherGameInfoField = new HoYoPlayGameInfoField();
                return;
            }

            bool isUseMultiLang = PresetConfig?.LauncherSpriteURLMultiLang ?? false;

            string localeCode = isUseMultiLang ? Locale.Lang.LanguageID.ToLower() : PresetConfig?.LauncherSpriteURLMultiLangFallback!;
            string launcherGameInfoUrl = string.Format(PresetConfig?.LauncherGameInfoDisplayURL!, localeCode);

            ActionTimeoutValueTaskCallback<HoYoPlayLauncherGameInfo?> hypLauncherGameInfoCallback =
                async innerToken =>
                    await ApiResourceHttpClient!.GetFromJsonAsync(
                        launcherGameInfoUrl,
                        HoYoPlayLauncherGameInfoJsonContext.Default.HoYoPlayLauncherGameInfo,
                        innerToken);

            HoYoPlayLauncherGameInfo? hypLauncherGameInfo = await hypLauncherGameInfoCallback.WaitForRetryAsync(ExecutionTimeout, ExecutionTimeoutStep,
                                                           ExecutionTimeoutAttempt, onTimeoutRoutine, token);

            HoYoPlayGameInfoField? sophonLauncherGameInfoRoot = new HoYoPlayGameInfoField();
            if (hypLauncherGameInfo != null)
            {
                ConvertGameInfoResources(ref sophonLauncherGameInfoRoot, hypLauncherGameInfo.GameInfoData);

                LauncherGameInfoField = sophonLauncherGameInfoRoot ?? new HoYoPlayGameInfoField();
            }
        }

        #region Convert Game Info Resources
        private void ConvertGameInfoResources([DisallowNull] ref HoYoPlayGameInfoField? sophonGameInfo, HoYoPlayGameInfoData? hypLauncherGameInfoList)
        {
            if (sophonGameInfo != null)
            {
                if (hypLauncherGameInfoList != null)
                {
                    sophonGameInfo =
                        hypLauncherGameInfoList.Data?.FirstOrDefault(x =>
                                                                         x.BizName?.Equals(PresetConfig
                                                                           ?.LauncherBizName) ??
                                                                         false);
                }
                else
                {
                    throw new ArgumentNullException(nameof(hypLauncherGameInfoList));
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(sophonGameInfo));
            }
        }
        #endregion

        #region GetDeviceId override
        protected override string GetDeviceId(PresetConfig preset)
        {
            // Determine if the client is a mainland client based on the zone name
            bool isMainlandClient = (preset.ZoneName?.Equals("Mainland China") ?? false) || (preset.ZoneName?.Equals("Bilibili") ?? false);

            // Set the publisher name based on the client type
            string publisherName = isMainlandClient ? "miHoYo" : "Cognosphere";
            // Define the registry root path for the publisher
            string registryRootPath = $@"Software\{publisherName}\HYP";

            // Open the registry key for the root path
            RegistryKey? rootRegistryKey = Registry.CurrentUser.OpenSubKey(registryRootPath, true);
            // Find or create the HYP device ID
            string hypDeviceId = FindOrCreateHYPDeviceId(rootRegistryKey, isMainlandClient, registryRootPath);
            return hypDeviceId;
        }

        private static string FindOrCreateHYPDeviceId(RegistryKey? rootRegistryKey, bool isMainlandClient, string registryRootPath)
        {
            // Define default version keys for mainland and global clients
            const string HYPVerDefaultCN = "1_1";
            const string HYPVerDefaultGlb = "1_0";

            // Use the root registry key or create it if it doesn't exist
            using (rootRegistryKey ??= Registry.CurrentUser.CreateSubKey(registryRootPath, true))
            {
                // Get the subkey names under the root registry key
                string[] subKeyNames = rootRegistryKey.GetSubKeyNames();
                foreach (string subKeyNameString in subKeyNames)
                {
                    // Open each subkey and check for the HYPDeviceId value
                    using RegistryKey? subKeyNameKey = rootRegistryKey.OpenSubKey(subKeyNameString, true);
                    if (subKeyNameKey == null)
                    {
                        continue;
                    }

                    // Get the current HYP device ID from the subkey
                    string? currentHypDeviceId = (string?)subKeyNameKey.GetValue("HYPDeviceId", null);
                    if (string.IsNullOrEmpty(currentHypDeviceId))
                    {
                        continue;
                    }

                    // Return the current HYP device ID if found
                    return currentHypDeviceId;
                }

                // Open or create the subkey for the default version based on the client type
                using RegistryKey subRegistryKey = rootRegistryKey.OpenSubKey(isMainlandClient ? HYPVerDefaultCN : HYPVerDefaultGlb, true)
                    ?? rootRegistryKey.CreateSubKey(isMainlandClient ? HYPVerDefaultCN : HYPVerDefaultGlb, true);

                // Generate a new HYP device ID
                string newHypDeviceId = CreateNewDeviceId();
                // Set the new HYP device ID in the subkey
                subRegistryKey.SetValue("HYPDeviceId", newHypDeviceId, RegistryValueKind.String);

                return newHypDeviceId;
            }
        }

        private static string CreateNewDeviceId()
        {
            // Define the registry key path for cryptography settings
            const string regKeyCryptography = @"SOFTWARE\Microsoft\Cryptography";

            // Open the registry key for reading
            using RegistryKey? rootRegistryKey = Registry.LocalMachine.OpenSubKey(regKeyCryptography, true);
            // Retrieve the MachineGuid value from the registry, or generate a new GUID if it doesn't exist
            string guid = ((string?)rootRegistryKey?.GetValue("MachineGuid", null) ??
                Guid.NewGuid().ToString()).Replace("-", string.Empty);

            // Append the current Unix timestamp in milliseconds to the GUID
            string guidWithEpochMs = guid + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            // Return the combined GUID and timestamp
            return guidWithEpochMs;
        }
        #endregion
    }
}
