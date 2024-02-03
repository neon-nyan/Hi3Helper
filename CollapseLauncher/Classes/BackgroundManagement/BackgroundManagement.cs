﻿using ColorThiefDotNet;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Preset;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using PhotoSauce.MagicScaler;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.RegionResourceListHelper;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public sealed partial class MainPage : Page
    {
        private RegionResourceProp _gameAPIProp { get; set; }

        private BitmapImage BackgroundBitmap;
        private Bitmap PaletteBitmap;
        private bool BGLastState = true;
        private bool IsFirstStartup = true;

        internal async void ChangeBackgroundImageAsRegionAsync(bool ShowLoadingMsg = false)
        {
            IsCustomBG = GetAppConfigValue("UseCustomBG").ToBool();
            if (IsCustomBG)
            {
                string BGPath = GetAppConfigValue("CustomBGPath").ToString();
                regionBackgroundProp.imgLocalPath = string.IsNullOrEmpty(BGPath) ? AppDefaultBG : BGPath;
            }
            else
            {
                if (!await TryLoadResourceInfo(ResourceLoadingType.DownloadBackground, ConfigV2Store.CurrentConfigV2, ShowLoadingMsg))
                {
                    regionBackgroundProp.imgLocalPath = AppDefaultBG;
                }
            }

            if (!IsCustomBG || IsFirstStartup)
            {
                BackgroundImgChanger.ChangeBackground(regionBackgroundProp.imgLocalPath, IsCustomBG);
                await BackgroundImgChanger.WaitForBackgroundToLoad();
            }

            IsFirstStartup = false;

            ReloadPageTheme(this, ConvertAppThemeToElementTheme(CurrentAppTheme));
        }

        public static async void ApplyAccentColor(Page page, Bitmap bitmapInput, string bitmapPath)
        {
            bool IsLight = IsAppThemeLight;

            Windows.UI.Color[] _colors = await TryGetCachedPalette(bitmapInput, IsLight, bitmapPath);

            SetColorPalette(page, _colors);
        }

        private static async ValueTask<Windows.UI.Color[]> TryGetCachedPalette(Bitmap bitmapInput, bool isLight, string bitmapPath)
        {
            string cachedPalettePath = bitmapPath + $".palette{(isLight ? "Light" : "Dark")}";
            string cachedFileHash = ConverterTool.BytesToCRC32Simple(cachedPalettePath);
            cachedPalettePath = Path.Combine(AppGameImgCachedFolder, cachedFileHash);
            if (File.Exists(cachedPalettePath))
            {
                byte[] data = await File.ReadAllBytesAsync(cachedPalettePath);
                if (!ConverterTool.TryDeserializeStruct(data, 4, out Windows.UI.Color[] output))
                {
                    return await TryGenerateNewCachedPalette(bitmapInput, isLight, cachedPalettePath);
                }

                return output;
            }

            return await TryGenerateNewCachedPalette(bitmapInput, isLight, cachedPalettePath);
        }

        private static async ValueTask<Windows.UI.Color[]> TryGenerateNewCachedPalette(Bitmap bitmapInput, bool IsLight, string cachedPalettePath)
        {
            byte[] buffer = new byte[1 << 10];

            string cachedPaletteDirPath = Path.GetDirectoryName(cachedPalettePath);
            if (!Directory.Exists(cachedPaletteDirPath)) Directory.CreateDirectory(cachedPaletteDirPath);

            Windows.UI.Color[] _colors = await GetPaletteList(bitmapInput, 10, IsLight, 1);

            if (!ConverterTool.TrySerializeStruct(_colors, buffer, out int read))
            {
                byte DefVal = (byte)(IsLight ? 80 : 255);
                Windows.UI.Color defColor = DrawingColorToColor(new QuantizedColor(Color.FromArgb(255, DefVal, DefVal, DefVal), 1));
                return new Windows.UI.Color[] { defColor, defColor, defColor, defColor };
            }

            await File.WriteAllBytesAsync(cachedPalettePath, buffer[..read]);
            return _colors;
        }

        public static void SetColorPalette(Page page, Windows.UI.Color[] palette = null)
        {
            if (palette == null || palette?.Length < 2)
                palette = EnsureLengthCopyLast(new Windows.UI.Color[] { (Windows.UI.Color)Application.Current.Resources["TemplateAccentColor"] }, 2);

            if (IsAppThemeLight)
            {
                Application.Current.Resources["SystemAccentColor"] = palette[0];
                Application.Current.Resources["SystemAccentColorDark1"] = palette[0];
                Application.Current.Resources["SystemAccentColorDark2"] = palette[1];
                Application.Current.Resources["SystemAccentColorDark3"] = palette[1];
                Application.Current.Resources["AccentColor"] = new SolidColorBrush(palette[1]);
            }
            else
            {
                Application.Current.Resources["SystemAccentColor"] = palette[0];
                Application.Current.Resources["SystemAccentColorLight1"] = palette[0];
                Application.Current.Resources["SystemAccentColorLight2"] = palette[1];
                Application.Current.Resources["SystemAccentColorLight3"] = palette[0];
                Application.Current.Resources["AccentColor"] = new SolidColorBrush(palette[0]);
            }

            ReloadPageTheme(page, ConvertAppThemeToElementTheme(CurrentAppTheme));
        }

        private static List<QuantizedColor> _generatedColors = new List<QuantizedColor>();
        private static async Task<Windows.UI.Color[]> GetPaletteList(Bitmap bitmapinput, int ColorCount, bool IsLight, int quality)
        {
            byte DefVal = (byte)(IsLight ? 80 : 255);

            try
            {
                LumaUtils.DarkThreshold = IsLight ? 200f : 400f;
                LumaUtils.IgnoreWhiteThreshold = IsLight ? 900f : 800f;
                if (!IsLight)
                    LumaUtils.ChangeCoeToBT709();
                else
                    LumaUtils.ChangeCoeToBT601();

                return await Task.Run(() =>
                {
                    _generatedColors.Clear();

                    while (true)
                    {
                        try
                        {
                            IEnumerable<QuantizedColor> averageColors = ColorThief.GetPalette(bitmapinput, ColorCount, quality, !IsLight)
                                .Where(x => IsLight ? x.IsDark : !x.IsDark)
                                .OrderBy(x => x.Population);

                            QuantizedColor dominatedColor = new QuantizedColor(
                                Color.FromArgb(
                                    255,
                                    (byte)averageColors.Average(a => a.Color.R),
                                    (byte)averageColors.Average(a => a.Color.G),
                                    (byte)averageColors.Average(a => a.Color.B)
                                ), (int)averageColors.Average(a => a.Population));

                            _generatedColors.Add(dominatedColor);
                            _generatedColors.AddRange(averageColors);

                            break;
                        }
                        catch (InvalidOperationException)
                        {
                            if (ColorCount > 100) throw;
                            LogWriteLine($"Regenerating colors by adding 20 more colors to generate: {ColorCount} to {ColorCount + 20}", LogType.Warning, true);
                            ColorCount += 20;
                        }
                    }

                    return EnsureLengthCopyLast(_generatedColors
                        .Select(DrawingColorToColor)
                        .ToArray(), 4);
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Warning, true);
            }

            Windows.UI.Color defColor = DrawingColorToColor(new QuantizedColor(Color.FromArgb(255, DefVal, DefVal, DefVal), 1));
            return new Windows.UI.Color[] { defColor, defColor, defColor, defColor };
        }

        private static T[] EnsureLengthCopyLast<T>(T[] array, int toLength)
        {
            if (array.Length == 0) throw new IndexOutOfRangeException("Array has no content in it");
            if (array.Length >= toLength) return array;

            T lastArray = array[array.Length - 1];
            T[] newArray = new T[toLength];
            Array.Copy(array, newArray, array.Length);

            for (int i = array.Length; i < newArray.Length; i++)
            {
                newArray[i] = lastArray;
            }

            return newArray;
        }

        private static Windows.UI.Color DrawingColorToColor(QuantizedColor i) => new Windows.UI.Color { R = i.Color.R, G = i.Color.G, B = i.Color.B, A = i.Color.A };

        public static async Task<(Bitmap, BitmapImage)> GetResizedBitmapNew(string FilePath, uint ToWidth, uint ToHeight)
        {
            Bitmap bitmapRet;
            BitmapImage bitmapImageRet;

            FileStream cachedFileStream = await ImageLoaderHelper.LoadImage(FilePath, false, false);
            if (cachedFileStream == null) return (null, null);
            using (cachedFileStream)
            {
                bitmapRet = await Task.Run(() => Stream2Bitmap(cachedFileStream.AsRandomAccessStream()));
                bitmapImageRet = await Stream2BitmapImage(cachedFileStream.AsRandomAccessStream());
            }

            return (bitmapRet, bitmapImageRet);
        }

        public static async Task<(Bitmap, BitmapImage)> GetResizedBitmap(FileStream stream, uint ToWidth, uint ToHeight)
        {
            Bitmap bitmapRet;
            BitmapImage bitmapImageRet;

            using (stream)
            using (FileStream cachedFileStream = await GetResizedImageStream(stream, ToWidth, ToHeight))
            {
                bitmapRet = await Task.Run(() => Stream2Bitmap(cachedFileStream.AsRandomAccessStream()));
                bitmapImageRet = await Stream2BitmapImage(cachedFileStream.AsRandomAccessStream());
            }

            return (bitmapRet, bitmapImageRet);
        }

        public static async Task<FileStream> GetResizedImageStream(FileStream stream, uint ToWidth, uint ToHeight)
        {
            if (!Directory.Exists(AppGameImgCachedFolder)) Directory.CreateDirectory(AppGameImgCachedFolder);

            string cachedFileHash = ConverterTool.BytesToCRC32Simple(stream.Name + stream.Length);
            string cachedFilePath = Path.Combine(AppGameImgCachedFolder, cachedFileHash);

            FileInfo cachedFileInfo = new FileInfo(cachedFilePath);

            bool isCachedFileExist = cachedFileInfo.Exists && cachedFileInfo.Length > 1 << 15;
            FileStream cachedFileStream = isCachedFileExist ? cachedFileInfo.OpenRead() : cachedFileInfo.Create();
            if (!isCachedFileExist) await GetResizedImageStream(stream, cachedFileStream, ToWidth, ToHeight);

            return cachedFileStream;
        }

        private static async Task GetResizedImageStream(FileStream input, FileStream output, uint ToWidth, uint ToHeight)
        {
            ProcessImageSettings settings = new ProcessImageSettings
            {
                Width = (int)ToWidth,
                Height = (int)ToHeight,
                HybridMode = HybridScaleMode.Off,
                Interpolation = InterpolationSettings.CubicSmoother
            };

            await Task.Run(() => MagicImageProcessor.ProcessImage(input, output, settings));
        }

        public static async Task<BitmapImage> Stream2BitmapImage(IRandomAccessStream image)
        {
            BitmapImage ret = new BitmapImage();
            image.Seek(0);
            await ret.SetSourceAsync(image);
            return ret;
        }

        public static Bitmap Stream2Bitmap(IRandomAccessStream image)
        {
            image.Seek(0);
            return new Bitmap(image.AsStream());
        }

        private async Task ApplyBackground(bool IsFirstStartup)
        {
            uint Width = (uint)((double)m_actualMainFrameSize.Width * 1.5 * m_appDPIScale);
            uint Height = (uint)((double)m_actualMainFrameSize.Height * 1.5 * m_appDPIScale);

            BitmapImage ReplacementBitmap;
            (PaletteBitmap, ReplacementBitmap) = await GetResizedBitmapNew(regionBackgroundProp.imgLocalPath, Width, Height);
            if (PaletteBitmap == null || ReplacementBitmap == null) return;

            ApplyAccentColor(this, PaletteBitmap, regionBackgroundProp.imgLocalPath);

            if (!IsFirstStartup)
                FadeSwitchAllBg(0.125f, ReplacementBitmap);
            else
                FadeInAllBg(0.125f, ReplacementBitmap);
        }

        private async void FadeInAllBg(double duration, BitmapImage ReplacementImage)
        {
            Storyboard storyBefore = new Storyboard();
            AddDoubleAnimationFadeToObject(BackgroundFrontBuffer, "Opacity", 0.125, BackgroundFrontBuffer.Opacity, 0f, ref storyBefore);
            AddDoubleAnimationFadeToObject(BackgroundBackBuffer, "Opacity", 0.125, BackgroundBackBuffer.Opacity, 0f, ref storyBefore);
            AddDoubleAnimationFadeToObject(BackgroundFront, "Opacity", 0.125, BackgroundFront.Opacity, 0f, ref storyBefore);
            AddDoubleAnimationFadeToObject(BackgroundBack, "Opacity", 0.125, BackgroundBack.Opacity, 0f, ref storyBefore);
            storyBefore.Begin();
            await Task.Delay(250);

            BackgroundBack.Source = ReplacementImage;
            BackgroundFront.Source = ReplacementImage;

            Storyboard storyAfter = new Storyboard();
            if (m_appMode != AppMode.Hi3CacheUpdater)
                AddDoubleAnimationFadeToObject(BackgroundFront, "Opacity", duration, 0f, 1f, ref storyAfter);
            AddDoubleAnimationFadeToObject(BackgroundBack, "Opacity", duration, 0f, 1f, ref storyAfter);
            storyAfter.Begin();
            await Task.Delay((int)(duration * 1000));

            BackgroundBitmap = ReplacementImage;
        }

        private async void FadeSwitchAllBg(double duration, BitmapImage ReplacementImage)
        {
            Storyboard storyBuf = new Storyboard();

            BackgroundBackBuffer.Source = BackgroundBitmap;
            BackgroundBackBuffer.Opacity = 1f;

            BackgroundFrontBuffer.Source = BackgroundBitmap;
            if (m_appCurrentFrameName == "HomePage")
            {
                BackgroundFrontBuffer.Opacity = 1f;
            }

            BackgroundBack.Opacity = 1f;

            if (m_appCurrentFrameName == "HomePage")
            {
                BackgroundFront.Opacity = 1f;
                AddDoubleAnimationFadeToObject(BackgroundFrontBuffer, "Opacity", duration, 1, 0, ref storyBuf);
            }

            AddDoubleAnimationFadeToObject(BackgroundBackBuffer, "Opacity", duration, 1, 0, ref storyBuf);
            BackgroundBack.Source = ReplacementImage;
            BackgroundFront.Source = ReplacementImage;

            storyBuf.Begin();
            await Task.Delay((int)duration * 1000);

            BackgroundBitmap = ReplacementImage;
        }

        private void AddDoubleAnimationFadeToObject<T>(T objectToAnimate, string targetProperty,
            double duration, double valueFrom, double valueTo, ref Storyboard storyboard)
            where T : DependencyObject
        {
            DoubleAnimation Animation = new DoubleAnimation();
            Animation.Duration = new Duration(TimeSpan.FromSeconds(duration));
            Animation.From = valueFrom; Animation.To = valueTo;

            Storyboard.SetTarget(Animation, objectToAnimate);
            Storyboard.SetTargetProperty(Animation, targetProperty);
            storyboard.Children.Add(Animation);
        }

        private async void HideLoadingPopup(bool hide, string title, string subtitle)
        {
            Storyboard storyboard = new Storyboard();

            LoadingTitle.Text = title;
            LoadingSubtitle.Text = subtitle;

            if (hide)
            {
                LoadingRing.IsIndeterminate = false;

                DoubleAnimation OpacityAnimation = new DoubleAnimation();
                OpacityAnimation.From = 1;
                OpacityAnimation.To = 0;
                OpacityAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.125));

                Storyboard.SetTarget(OpacityAnimation, LoadingPopup);
                Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
                storyboard.Children.Add(OpacityAnimation);

                await Task.Delay(125);

                Thickness lastMargin = LoadingPopupPill.Margin;
                lastMargin.Bottom = -72;
                LoadingPopupPill.Margin = lastMargin;

                storyboard.Begin();

                await Task.Delay(500);
                LoadingPopup.Visibility = Visibility.Collapsed;
                LoadingCancelBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                LoadingRing.IsIndeterminate = true;

                LoadingPopup.Visibility = Visibility.Visible;

                DoubleAnimation OpacityAnimation = new DoubleAnimation();
                OpacityAnimation.From = 0;
                OpacityAnimation.To = 1;
                OpacityAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.125));

                Storyboard.SetTarget(OpacityAnimation, LoadingPopup);
                Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
                storyboard.Children.Add(OpacityAnimation);
                storyboard.Begin();

                await Task.Delay(125);

                Thickness lastMargin = LoadingPopupPill.Margin;
                lastMargin.Bottom = 28;
                LoadingPopupPill.Margin = lastMargin;
            }
        }

        private void HideBackgroundImage(bool hideImage = true, bool absoluteTransparent = true)
        {
            Storyboard storyboardFront = new Storyboard();
            Storyboard storyboardBack = new Storyboard();

            if (!(hideImage && BackgroundFront.Opacity == 0))
            {
                DoubleAnimation OpacityAnimation = new DoubleAnimation();
                OpacityAnimation.From = hideImage ? 1 : 0;
                OpacityAnimation.To = hideImage ? 0 : 1;
                OpacityAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.25));

                DoubleAnimation OpacityAnimationBack = new DoubleAnimation();
                OpacityAnimationBack.From = hideImage ? 1 : 0.4;
                OpacityAnimationBack.To = hideImage ? 0.4 : 1;
                OpacityAnimationBack.Duration = new Duration(TimeSpan.FromSeconds(0.25));

                if (!IsFirstStartup)
                {
                    Storyboard.SetTarget(OpacityAnimation, BackgroundFront);
                    Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
                    storyboardFront.Children.Add(OpacityAnimation);
                }

                Storyboard.SetTarget(OpacityAnimationBack, Background);
                Storyboard.SetTargetProperty(OpacityAnimationBack, "Opacity");
                storyboardBack.Children.Add(OpacityAnimationBack);
            }

            if (BGLastState != hideImage)
            {
                storyboardFront.Begin();
                storyboardBack.Begin();
                BGLastState = hideImage;
            }
        }
    }
}