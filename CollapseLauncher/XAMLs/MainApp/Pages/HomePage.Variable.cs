using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.LauncherApiLoader.Sophon;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Microsoft.UI.Xaml;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

#nullable enable
namespace CollapseLauncher.Pages
{
    public sealed partial class HomePage
    {
        internal string GameDirPath { get => CurrentGameProperty.GameVersion.GameDirPath; }
        internal static LauncherGameNewsData? GameNewsData { get => LauncherMetadataHelper.CurrentMetadataConfig?.GameLauncherApi?.LauncherGameNews?.Content; }
        internal static HoYoPlayGameInfoField? GameInfoDisplayField { get => LauncherMetadataHelper.CurrentMetadataConfig?.GameLauncherApi?.LauncherGameInfoField; }
        internal static bool IsPostPanelAvailable => (GameNewsData?.NewsPost?.Count ?? 0) > 0;
        internal static bool IsCarouselPanelAvailable => (GameNewsData?.NewsCarousel?.Count ?? 0) > 0;
        internal static bool IsGameStatusPreRegister => GameInfoDisplayField?.DisplayStatus == LauncherGameAvailabilityStatus.LAUNCHER_GAME_DISPLAY_STATUS_RESERVATION_ENABLED;
        internal static bool IsGameStatusComingSoon => GameInfoDisplayField?.DisplayStatus == LauncherGameAvailabilityStatus.LAUNCHER_GAME_DISPLAY_STATUS_COMING_SOON;
        internal static string? GamePreRegisterLink => GameInfoDisplayField?.ReservationLink?.ClickLink;

        internal static Visibility IsPostEventPanelVisible  => (GameNewsData?.NewsPostTypeActivity?.Count ?? 0) == 0 ? Visibility.Collapsed : Visibility.Visible;
        internal static Visibility IsPostEventPanelEmpty    => (GameNewsData?.NewsPostTypeActivity?.Count ?? 0) != 0 ? Visibility.Collapsed : Visibility.Visible;
        internal static Visibility IsPostNoticePanelVisible => (GameNewsData?.NewsPostTypeAnnouncement?.Count ?? 0) == 0 ? Visibility.Collapsed : Visibility.Visible;
        internal static Visibility IsPostNoticePanelEmpty   => (GameNewsData?.NewsPostTypeAnnouncement?.Count ?? 0) != 0 ? Visibility.Collapsed : Visibility.Visible;
        internal static Visibility IsPostInfoPanelVisible   => (GameNewsData?.NewsPostTypeInfo?.Count ?? 0) == 0 ? Visibility.Collapsed : Visibility.Visible;
        internal static Visibility IsPostInfoPanelEmpty     => (GameNewsData?.NewsPostTypeInfo?.Count ?? 0) != 0 ? Visibility.Collapsed : Visibility.Visible;
        internal static Visibility IsPostInfoPanelAllEmpty  =>
            IsPostEventPanelVisible == Visibility.Collapsed
            && IsPostNoticePanelVisible == Visibility.Collapsed
            && IsPostInfoPanelVisible == Visibility.Collapsed ? Visibility.Collapsed : Visibility.Visible;

        internal static int PostEmptyMascotTextWidth => Locale.Lang._HomePage.PostPanel_NoNews.Length > 30 ? 200 : 100;

        internal static int DefaultPostPanelIndex
        {
            get
            {
                if (IsPostEventPanelVisible != Visibility.Collapsed)
                    return 0;

                if (IsPostNoticePanelVisible != Visibility.Collapsed)
                    return 1;

                return IsPostInfoPanelVisible != Visibility.Collapsed ? 2 : 0;
            }
        }

        internal bool IsEventsPanelShow
        {
            get
            {
                bool ret = GetAppConfigValue("ShowEventsPanel").ToBoolNullable() ?? true;
                return ret;
            }
            set
            {
                SetAndSaveConfigValue("ShowEventsPanel", value);
                ToggleEventsPanel(value);
            }
        }

        internal static bool IsEventsPanelScaleUp
        {
            get
            {
                bool ret = GetAppConfigValue("ScaleUpEventsPanel").ToBoolNullable() ?? true;
                return ret;
            }
            set
            {
                SetAndSaveConfigValue("ScaleUpEventsPanel", value);
            }
        }

        internal bool IsSocMedPanelShow
        {
            get
            {
                bool ret = GetAppConfigValue("ShowSocialMediaPanel").ToBoolNullable() ?? true;
                return ret;
            }
            set
            {
                SetAndSaveConfigValue("ShowSocialMediaPanel", value);
                ToggleSocmedPanelPanel(value);
            }
        }

        internal bool IsPlaytimeBtnVisible
        {
            get
            {
                var v = GetAppConfigValue("ShowGamePlaytime").ToBoolNullable() ?? true;
                TogglePlaytimeBtn(v);

                return v;
            }
            set
            {
                SetAndSaveConfigValue("ShowGamePlaytime", value);
                TogglePlaytimeBtn(value);
            }
        }

        internal bool IsPlaytimeSyncDb
        {
            get => CurrentGameProperty.GameSettings?.SettingsCollapseMisc.IsSyncPlaytimeToDatabase ?? false;
            set
            {
                if (CurrentGameProperty.GameSettings == null)
                {
                    return;
                }

                CurrentGameProperty.GameSettings.SettingsCollapseMisc.IsSyncPlaytimeToDatabase = value;
                CurrentGameProperty?.GameSettings?.SaveBaseSettings();
                SyncDbPlaytimeBtn.IsEnabled = value;
                
                // Run DbSync if toggle is changed to enable
                if (value) CurrentGameProperty?.GamePlaytime.CheckDb();
            }
        }

        internal string NoNewsSplashMascot
        {
            get
            {
                GameNameType? gameType = CurrentGameProperty.GamePreset.GameType;
                return gameType switch
                {
                    GameNameType.Honkai => "ms-appx:///Assets/Images/GameMascot/AiShocked.png",
                    GameNameType.StarRail => "ms-appx:///Assets/Images/GameMascot/PomPomWhat.png",
                    GameNameType.Zenless => "ms-appx:///Assets/Images/GameMascot/BangbooShocked.png",
                    _ => "ms-appx:///Assets/Images/GameMascot/PaimonWhat.png"
                };
            }
        }

        internal int CurrentBannerIconWidth
        {
            get => CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
                   WindowSize.WindowSize.CurrentWindowSize.BannerIconWidth :
                   WindowSize.WindowSize.CurrentWindowSize.BannerIconWidthHYP;
        }

        internal Thickness CurrentBannerIconMargin
        {
            get => CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
                   WindowSize.WindowSize.CurrentWindowSize.BannerIconMargin :
                   WindowSize.WindowSize.CurrentWindowSize.BannerIconMarginHYP;
        }

        internal int CurrentBannerIconColumn
        {
            get => CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
                   1 :
                   0;
        }

        internal static int CurrentBannerIconColumnSpan
        {
            get => 1;
        }

        internal int CurrentBannerIconRow
        {
            get => CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
                   1 :
                   0;
        }

        internal static int CurrentBannerIconRowSpan
        {
            get => 1;
        }

        internal HorizontalAlignment CurrentBannerIconHorizontalAlign
        {
            get => CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
                   WindowSize.WindowSize.CurrentWindowSize.BannerIconAlignHorizontal :
                   WindowSize.WindowSize.CurrentWindowSize.BannerIconAlignHorizontalHYP;
        }

        internal VerticalAlignment CurrentBannerIconVerticalAlign
        {
            get => CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
                   WindowSize.WindowSize.CurrentWindowSize.BannerIconAlignVertical :
                   WindowSize.WindowSize.CurrentWindowSize.BannerIconAlignVerticalHYP;
        }

        public void ToggleEventsPanel(bool      hide) => HideImageCarousel(!hide);
        public void ToggleSocmedPanelPanel(bool hide) => HideSocialMediaPanel(!hide);
        public void TogglePlaytimeBtn(bool      hide) => HidePlaytimeButton(!hide);
    }
}
