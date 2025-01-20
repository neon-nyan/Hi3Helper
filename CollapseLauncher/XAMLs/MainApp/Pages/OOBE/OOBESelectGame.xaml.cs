using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Background;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.ToastCOM.Notification;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.Pages.OOBE.OOBESelectGameBGProp;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

#nullable enable
namespace CollapseLauncher.Pages.OOBE
{
    public sealed partial class OOBESelectGame
    {
        private string? SelectedCategory { get; set; }
        private string? SelectedRegion   { get; set; }
        private string? _lastSelectedCategory = "";

        public OOBESelectGame()
        {
            InitializeComponent();
            GameCategorySelect.ItemsSource = BuildGameTitleListUI();
            BackgroundFrame.Navigate(typeof(OOBESelectGameBG));
            RequestedTheme = IsAppThemeLight ? ElementTheme.Light : ElementTheme.Dark;
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            // Set and Save CurrentRegion in AppConfig
            string? categorySelected = GetComboBoxGameRegionValue(GameCategorySelect.SelectedValue);
            string? regionSelected = GetComboBoxGameRegionValue(GameRegionSelect.SelectedValue);
            SetAppConfigValue("GameCategory", categorySelected);
            LauncherMetadataHelper.SetPreviousGameRegion(categorySelected,
                                                         regionSelected,
                                                         false);
            SaveAppConfig();

            (WindowUtility.CurrentWindow as MainWindow)?.rootFrame.Navigate(typeof(MainPage), null,
                                                         new SlideNavigationTransitionInfo
                                                             { Effect = SlideNavigationTransitionEffect.FromBottom });

            WindowUtility.SetWindowBackdrop(WindowBackdropKind.None);

            // Spawn welcome toast after clicking in
            SpawnGreetingsToastNotification(categorySelected, regionSelected);
        }

        private static void SpawnGreetingsToastNotification(string? gameName, string? regionName)
        {
            if (string.IsNullOrEmpty(gameName)
                || string.IsNullOrEmpty(regionName))
            {
                return;
            }

            string gameNameTranslated   = GetGameTitleRegionTranslationString(gameName,   Locale.Lang._GameClientTitles) ?? gameName;
            string gameRegionTranslated = GetGameTitleRegionTranslationString(regionName, Locale.Lang._GameClientRegions) ?? regionName;

            // Get game preset config
            PresetConfig? gamePresetConfig = LauncherMetadataHelper.LauncherMetadataConfig?[gameName]?[regionName];
            if (gamePresetConfig == null) // If empty, return
                return;

            // Get logo name and poster name
            (string? logoName, string? posterName) = GetLogoAndHeroImgPath(gamePresetConfig);

            // Create Toast Notification Content
            NotificationContent toastContent = NotificationContent.Create()
                                                                  .SetTitle(Locale.Lang._NotificationToast.OOBE_WelcomeTitle)
                                                                  .SetContent(
                                                                              string.Format(
                                                                                   Locale.Lang._NotificationToast.OOBE_WelcomeSubtitle,
                                                                                   gameNameTranslated,
                                                                                   gameRegionTranslated))
                                                                  .SetAppLogoPath(
                                                                        logoName,
                                                                        true)
                                                                  .AddAppHeroImagePath(
                                                                        posterName);

            // Create Toast Notification Service
            ToastNotification? toastService = WindowUtility.CurrentToastNotificationService?.CreateToastNotification(toastContent);

            // Create Toast Notifier
            ToastNotifier? toastNotifier = WindowUtility.CurrentToastNotificationService?.CreateToastNotifier();
            toastNotifier?.Show(toastService);
        }

        internal static (string? LogoPath, string? HeroPath) GetLogoAndHeroImgPath(PresetConfig? gamePresetConfig)
        {
            if (gamePresetConfig == null) // If config is null, return
                return (null, null);

            // Get logo name and poster name
            (string logoName, string posterName) = gamePresetConfig.GameType switch
            {
                GameNameType.Honkai => ("honkai", "honkai"),
                GameNameType.StarRail => ("starrail", "starrail"),
                GameNameType.Zenless => ("zenless", "zzz"),
                _ => ("genshin", "genshin") // Fallback to Genshin by default
            };

            // Return paths
            return ($@"Assets\Images\GameLogo\{logoName}-logo.png",
                    $@"Assets\Images\GamePoster\poster_{posterName}.png");
        }


        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            OOBEStartUpMenu.ThisCurrent?.OverlayFrameGoBack();
            // (m_window as MainWindow).rootFrame.GoBack();
        }


        private async void GameSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                object value = ((ComboBox)sender).SelectedValue;
                if (value is not null)
                {
                    SelectedRegion = GetComboBoxGameRegionValue(value);

                    NextPage.IsEnabled = true;
                    NextPage.Opacity   = 1;

                    BarBGLoading.Visibility      = Visibility.Visible;
                    BarBGLoading.IsIndeterminate = true;
                    FadeBackground(1, 0.25);
                    PresetConfig? gameConfig = await LauncherMetadataHelper.GetMetadataConfig(SelectedCategory, SelectedRegion);
                    bool          isSuccess  = await TryLoadGameDetails(gameConfig);

                    BitmapData? bitmapData = null;

                    try
                    {
                        int bitmapChannelCount = _gamePosterBitmap.PixelFormat switch
                                                 {
                                                     PixelFormat.Format32bppRgb => 4,
                                                     PixelFormat.Format32bppArgb => 4,
                                                     PixelFormat.Format24bppRgb => 3,
                                                     _ => throw new NotSupportedException($"Pixel format of the image: {_gamePosterBitmap.PixelFormat} is unsupported!")
                                                 };

                        bitmapData = _gamePosterBitmap.LockBits(new Rectangle(new Point(), _gamePosterBitmap.Size),
                                                                ImageLockMode.ReadOnly, _gamePosterBitmap.PixelFormat);

                        BitmapInputStruct bitmapInputStruct = new BitmapInputStruct
                        {
                            Buffer  = bitmapData.Scan0,
                            Width   = bitmapData.Width,
                            Height  = bitmapData.Height,
                            Channel = bitmapChannelCount
                        };

                        if (isSuccess)
                        {
                            await ColorPaletteUtility.ApplyAccentColor(this, bitmapInputStruct, _gamePosterPath);
                        }
                    }
                    finally
                    {
                        if (bitmapData != null)
                        {
                            _gamePosterBitmap.UnlockBits(bitmapData);
                        }
                    }

                    NavigationTransitionInfo transition = _lastSelectedCategory == SelectedCategory
                        ? new SuppressNavigationTransitionInfo()
                        : new DrillInNavigationTransitionInfo();

                    BackgroundFrame.Navigate(typeof(OOBESelectGameBG), null, transition);
                    FadeBackground(0.25, 1);
                    BarBGLoading.IsIndeterminate = false;
                    BarBGLoading.Visibility      = Visibility.Collapsed;

                    _lastSelectedCategory = SelectedCategory;

                    return;
                }

                NextPage.IsEnabled = true;
                NextPage.Opacity   = 1;
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }

        private async void FadeBackground(double from, double to)
        {
            try
            {
                const double dur          = 0.250;
                Storyboard   storyBufBack = new Storyboard();

                DoubleAnimation opacityBufBack = new DoubleAnimation
                {
                    Duration = new Duration(TimeSpan.FromSeconds(dur)),
                    From     = from,
                    To       = to
                };

                Storyboard.SetTarget(opacityBufBack, BackgroundFrame);
                Storyboard.SetTargetProperty(opacityBufBack, "Opacity");
                storyBufBack.Children.Add(opacityBufBack);

                storyBufBack.Begin();

                await Task.Delay((int)(dur * 1000));
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }

        private void GameCategorySelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedCategory = GetComboBoxGameRegionValue(((ComboBox)sender).SelectedValue);
            GameRegionSelect.ItemsSource = BuildGameRegionListUI(SelectedCategory);
            GameRegionSelect.IsEnabled   = true;
            NextPage.IsEnabled           = false;
            NextPage.Opacity             = 0;
        }
    }
}