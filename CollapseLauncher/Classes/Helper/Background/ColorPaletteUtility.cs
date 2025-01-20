using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Image;
using ColorThiefDotNet;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Hashing;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Hi3Helper.SentryHelper;
using static Hi3Helper.Logger;
using WColor = Windows.UI.Color;

#nullable enable
namespace CollapseLauncher.Helper.Background
{
    internal struct BitmapInputStruct
    {
        internal IntPtr Buffer;
        internal int    Width;
        internal int    Height;
        internal int    Channel;
    }

    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    internal static class ColorPaletteUtility
    {
        internal static async Task ApplyAccentColor<T>(T    page, IRandomAccessStream stream, string filePath,
                                                       bool isImageLoadForFirstTime, bool isContinuousGeneration)
            where T : FrameworkElement
        {
            using Bitmap bitmapAccentColor = await Task.Run(() => ImageLoaderHelper.Stream2Bitmap(stream));

            BitmapData? bitmapData = null;
            try
            {
                int bitmapChannelCount = bitmapAccentColor.PixelFormat switch
                                         {
                                             PixelFormat.Format32bppRgb => 4,
                                             PixelFormat.Format32bppArgb => 4,
                                             PixelFormat.Format24bppRgb => 3,
                                             _ => throw new NotSupportedException(
                                              $"Pixel format of the image: {bitmapAccentColor.PixelFormat} is unsupported!")
                                         };

                bitmapData = bitmapAccentColor.LockBits(new Rectangle(new Point(), bitmapAccentColor.Size),
                                                        ImageLockMode.ReadOnly, bitmapAccentColor.PixelFormat);

                BitmapInputStruct bitmapInputStruct = new BitmapInputStruct
                {
                    Buffer  = bitmapData.Scan0,
                    Width   = bitmapData.Width,
                    Height  = bitmapData.Height,
                    Channel = bitmapChannelCount
                };

                await ApplyAccentColor(page, bitmapInputStruct, filePath, isImageLoadForFirstTime,
                                       isContinuousGeneration);
            }
            finally
            {
                if (bitmapData != null)
                {
                    bitmapAccentColor.UnlockBits(bitmapData);
                }
            }
        }

        internal static async Task ApplyAccentColor<T>(T    page, BitmapInputStruct bitmapInput, string bitmapPath,
                                                       bool forceCreateNewCache    = false,
                                                       bool isContinuousGeneration = false)
            where T : FrameworkElement
        {
            bool isLight = InnerLauncherConfig.IsAppThemeLight;
            WColor[] colors = await TryGetCachedPalette(bitmapInput, isLight, bitmapPath, forceCreateNewCache,
                                                        isContinuousGeneration);
            SetColorPalette(page, colors[0]);
        }

        private static async ValueTask<WColor[]> TryGetCachedPalette(BitmapInputStruct bitmapInput, bool isLight,
                                                                     string bitmapPath, bool forceCreateNewCache,
                                                                     bool isContinuousGeneration)
        {
            if (isContinuousGeneration)
            {
                return await TryGenerateNewCachedPalette(bitmapInput, isLight, null);
            }

            string cachedPalettePath = bitmapPath + $".palette{(isLight ? "Light" : "Dark")}";
            string cachedFileHash    = Hash.GetHashStringFromString<Crc32>(cachedPalettePath);
            cachedPalettePath = Path.Combine(LauncherConfig.AppGameImgCachedFolder, cachedFileHash);

            if (!File.Exists(cachedPalettePath) || forceCreateNewCache)
            {
                await TryGenerateNewCachedPalette(bitmapInput, isLight, cachedPalettePath);
            }

            byte[] data = await File.ReadAllBytesAsync(cachedPalettePath);
            if (!ConverterTool.TryDeserializeStruct(data, 4, out WColor[] output))
            {
                await TryGenerateNewCachedPalette(bitmapInput, isLight, cachedPalettePath);
            }

            return output;
        }

        internal static void SetColorPalette<T>(T page, WColor? palette = null)
            where T : FrameworkElement
        {
            if (!palette.HasValue) return;

            string dictColorNameTheme = InnerLauncherConfig.IsAppThemeLight ? "Dark" : "Light";
            UIElementExtensions.SetApplicationResource("SystemAccentColor", palette);
            UIElementExtensions.SetApplicationResource($"SystemAccentColor{dictColorNameTheme}1", palette);
            UIElementExtensions.SetApplicationResource($"SystemAccentColor{dictColorNameTheme}2", palette);
            UIElementExtensions.SetApplicationResource($"SystemAccentColor{dictColorNameTheme}3", palette);
            UIElementExtensions.SetApplicationResource("AccentColor", new SolidColorBrush(palette.Value));

            ReloadPageTheme(page, ConvertAppThemeToElementTheme(InnerLauncherConfig.CurrentAppTheme));
        }

        internal static void ReloadPageTheme(FrameworkElement page, AppThemeMode startTheme)
        {
            ReloadPageTheme(page, ConvertAppThemeToElementTheme(startTheme));
        }

        internal static void ReloadPageTheme(FrameworkElement page, ElementTheme startTheme)
        {
            bool isComplete = false;
            while (!isComplete)
            {
                try
                {
                    page.RequestedTheme = page.RequestedTheme switch
                                          {
                                              ElementTheme.Dark => ElementTheme.Light,
                                              ElementTheme.Light => ElementTheme.Default,
                                              ElementTheme.Default => ElementTheme.Dark,
                                              _ => page.RequestedTheme
                                          };

                    if (page.RequestedTheme != startTheme)
                    {
                        ReloadPageTheme(page, startTheme);
                    }

                    isComplete = true;
                }
                catch
                {
                    // ignored
                }
            }
        }

        public static ElementTheme ConvertAppThemeToElementTheme(AppThemeMode theme)
        {
            return theme switch
                   {
                       AppThemeMode.Dark => ElementTheme.Dark,
                       AppThemeMode.Light => ElementTheme.Light,
                       _ => ElementTheme.Default
                   };
        }

        private static async ValueTask<WColor[]> TryGenerateNewCachedPalette(BitmapInputStruct bitmapInput,
                                                                             bool isLight, string? cachedPalettePath)
        {
            WColor[] colors = [await GetPaletteList(bitmapInput, isLight)];
            colors = ConverterTool.EnsureLengthCopyLast(colors, 4);

            if (string.IsNullOrEmpty(cachedPalettePath))
            {
                return colors;
            }

            byte[] buffer = new byte[1 << 10];

            string? cachedPaletteDirPath = Path.GetDirectoryName(cachedPalettePath);
            if (!Directory.Exists(cachedPaletteDirPath))
            {
                Directory.CreateDirectory(cachedPaletteDirPath ?? "");
            }

            if (!ConverterTool.TrySerializeStruct(colors, buffer, out int read))
            {
                byte defVal = (byte)(isLight ? 80 : 255);
                WColor defColor =
                    DrawingColorToColor(new QuantizedColor(Color.FromArgb(255, defVal, defVal, defVal), 1));
                return [defColor, defColor, defColor, defColor];
            }

            await File.WriteAllBytesAsync(cachedPalettePath, buffer[..read]);

            return colors;
        }

        private static async Task<WColor> GetPaletteList(BitmapInputStruct bitmapInput, bool isLight)
        {
            byte defVal = (byte)(isLight ? 80 : 255);

            try
            {
                LumaUtils.DarkThreshold        = isLight ? 200f : 400f;
                LumaUtils.IgnoreWhiteThreshold = isLight ? 900f : 800f;
                // LumaUtils.ChangeCoeToBT601();

                QuantizedColor averageColor = await Task.Run(() =>
                                                                 ColorThief.GetColor(bitmapInput.Buffer,
                                                                     bitmapInput.Channel, bitmapInput.Width,
                                                                     bitmapInput.Height, 1))
                                                        ;

                WColor wColor        = DrawingColorToColor(averageColor);
                WColor adjustedColor = wColor.SetSaturation(1.2);
                adjustedColor = isLight ? adjustedColor.GetDarkColor() : adjustedColor.GetLightColor();

                return adjustedColor;
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Warning, true);
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
            }

            WColor defColor = DrawingColorToColor(new QuantizedColor(Color.FromArgb(255, defVal, defVal, defVal), 1));
            return defColor;
        }

        private static WColor DrawingColorToColor(QuantizedColor i)
        {
            return new WColor { R = i.Color.R, G = i.Color.G, B = i.Color.B, A = i.Color.A };
        }
    }
}