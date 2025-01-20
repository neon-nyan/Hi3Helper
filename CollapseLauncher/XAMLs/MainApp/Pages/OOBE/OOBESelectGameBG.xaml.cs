using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.Metadata;
using CommunityToolkit.WinUI;
using Hi3Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.Pages.OOBE.OOBESelectGameBGProp;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace CollapseLauncher.Pages.OOBE
{
    public sealed partial class OOBESelectGameBG
    {
        public OOBESelectGameBG()
        {
            Loaded += LoadedRoutine;
        }

        private void LoadedRoutine(object sender, RoutedEventArgs e)
        {
            InitializeComponent();

            if (IsAppThemeLight && Resources["DetailsLogoShadowController"] is AttachedDropShadow attachedShadow)
            {
                attachedShadow.Opacity = 0.75;
            }

            if (!IsLoadDescription || !IsSuccess)
            {
                GameDetails.Visibility = Visibility.Collapsed;
                GameDetailsDummy.Visibility = Visibility.Visible;

                return;
            }

            GameDetailsDescription.Text = _gameDescription;
            GameDetailsPoster.Source = _gamePosterBitmapImage;
            GameDetailsLogo.Source = _gameLogoBitmapImage;
        }

        private void GameDetailsHomepage_Click(object sender, RoutedEventArgs e)
        {
            string  link = (string)((Button)sender).Tag;
            Process proc = new Process { StartInfo = new ProcessStartInfo(link) { UseShellExecute = true } };

            proc.Start();
        }

        internal static string GameDetailsHomepageLink { get => _gameHomepageLink; }
    }

    public static class OOBESelectGameBGProp
    {
        internal static string      _gameDescription;
        internal static string      _gamePosterPath;
        internal static string      _gameLogoPath;
        internal static string      _gameHomepageLink;
        internal static Bitmap      _gamePosterBitmap;
        internal static BitmapImage _gamePosterBitmapImage;
        internal static BitmapImage _gameLogoBitmapImage;
        internal static bool        IsLoadDescription;
        internal static bool        IsSuccess;

        internal static async Task<bool> TryLoadGameDetails(PresetConfig config = null)
        {
            try
            {
                if (!(IsLoadDescription = !(config?.ZoneDescription is null
                                            || config.ZonePosterURL is null
                                            || config.ZoneLogoURL is null
                                            || config.ZoneURL is null)))
                {
                    _gamePosterBitmap = null;
                    _gamePosterBitmapImage = null;
                    _gameLogoBitmapImage = null;
                    _gameDescription = null;
                    _gamePosterPath = null;
                    _gameLogoPath = null;
                    _gameHomepageLink = null;
                    return IsSuccess = true;
                }

                _gameHomepageLink = config.ZoneURL;
                _gameDescription = config.ZoneDescription;
                
                _gamePosterPath = config.GameType switch
                                {
                                    GameNameType.Honkai => Path.Combine(AppExecutableDir,   @"Assets\Images\GamePoster\poster_honkai.png"),
                                    GameNameType.Genshin => Path.Combine(AppExecutableDir,  @"Assets\Images\GamePoster\poster_genshin.png"),
                                    GameNameType.StarRail => Path.Combine(AppExecutableDir, @"Assets\Images\GamePoster\poster_starrail.png"),
                                    GameNameType.Zenless => Path.Combine(AppExecutableDir,  @"Assets\Images\GamePoster\poster_zzz.png"),
                                    _ => AppDefaultBG
                                };

                // TODO: Use FallbackCDNUtil to get the sprites
                //_gamePosterPath = await ImageLoaderHelper.GetCachedSpritesAsync(FallbackCDNUtil.TryGetAbsoluteToRelativeCDNURL(config.ZonePosterURL, "metadata/"), default);
                _gameLogoPath = await ImageLoaderHelper.GetCachedSpritesAsync(FallbackCDNUtil.TryGetAbsoluteToRelativeCDNURL(config.ZoneLogoURL, "metadata/"), default);

                if (_gameLogoPath == null)
                {
                    LogWriteLine("Failed while loading poster image as _gameLogoPath returns null!", LogType.Error, true);
                    return IsSuccess = false;
                }

                using IRandomAccessStream fs2 = new FileStream(_gameLogoPath, FileMode.Open, FileAccess.Read, FileShare.Read).AsRandomAccessStream();
                _gameLogoBitmapImage                        = await ImageLoaderHelper.Stream2BitmapImage(fs2);
                (_gamePosterBitmap, _gamePosterBitmapImage) = await ImageLoaderHelper.GetResizedBitmapNew(_gamePosterPath);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while loading poster image!\r\n{ex}", LogType.Error, true);
                return IsSuccess = false;
            }

            return IsSuccess = true;
        }
    }
}
