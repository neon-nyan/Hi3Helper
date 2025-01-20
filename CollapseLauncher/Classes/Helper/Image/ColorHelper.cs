using System;
using System.Runtime.InteropServices;
using Color = System.Drawing.Color;
using WColor = Windows.UI.Color;

/*
 * The code included here was mostly coming from Windows Forms Code
 */

namespace CollapseLauncher.Helper.Image
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct HlsColor
    {
        internal int hue;
        internal int saturation;
        internal int luminosity;

        public static HlsColor CreateFromWindowsColor(WColor color)
        {
            return new HlsColor(Color.FromArgb(color.A, color.R, color.G, color.B));
        }

        public HlsColor(Color color)
        {
            int r = color.R;
            int g = color.G;
            int b = color.B;
            int num4 = Math.Max(Math.Max(r, g), b);
            int num5 = Math.Min(Math.Min(r, g), b);
            int num6 = num4 + num5;
            luminosity = (num6 * 240 + 0xff) / 510;
            int num7 = num4 - num5;
            if (num7 == 0)
            {
                saturation = 0;
                hue = 160;
            }
            else
            {
                if (luminosity <= 120)
                {
                    saturation = (num7 * 240 + num6 / 2) / num6;
                }
                else
                {
                    saturation = (num7 * 240 + (510 - num6) / 2) / (510 - num6);
                }

                int num8 = ((num4 - r) * 40 + num7 / 2) / num7;
                int num9 = ((num4 - g) * 40 + num7 / 2) / num7;
                int num10 = ((num4 - b) * 40 + num7 / 2) / num7;
                if (r == num4)
                {
                    hue = num10 - num9;
                }
                else if (g == num4)
                {
                    hue = 80 + num8 - num10;
                }
                else
                {
                    hue = 160 + num9 - num8;
                }

                if (hue < 0)
                {
                    hue += 240;
                }

                if (hue > 240)
                {
                    hue -= 240;
                }
            }
        }

        public Color Lighter(float percentLighter)
        {
            int luminosityLocal = luminosity;
            int num5 = NewLuma(500, true);
            return ColorFromHls(hue, luminosityLocal + (int)((num5 - luminosityLocal) * percentLighter), saturation);
        }

        public Color Darker(float percentDarker)
        {
            int num5 = NewLuma(-333, true);
            return ColorFromHls(hue, num5 - (int)(num5 * percentDarker), saturation);
        }

        private int NewLuma(int n, bool scale)
        {
            return NewLuma(luminosity, n, scale);
        }

        private static int NewLuma(int luminanceLocal, int n, bool scale)
        {
            if (n == 0)
            {
                return luminanceLocal;
            }

            if (scale)
            {
                if (n > 0)
                {
                    return (int)((luminanceLocal * (0x3e8 - n) + 0xf1L * n) / 0x3e8L);
                }

                return luminanceLocal * (n + 0x3e8) / 0x3e8;
            }

            int num = luminanceLocal;
            num += (int)(n * 240L / 0x3e8L);
            if (num < 0)
            {
                num = 0;
            }

            if (num > 240)
            {
                num = 240;
            }

            return num;
        }

        internal static Color ColorFromHls(int thisHue, int thisLuminosity, int thisSaturation)
        {
            byte num;
            byte num2;
            byte num3;
            if (thisSaturation == 0)
            {
                num = num2 = num3 = (byte)(thisLuminosity * 0xff / 240);
                if (thisHue == 160)
                {
                }
            }
            else
            {
                int num5;
                if (thisLuminosity <= 120)
                {
                    num5 = (thisLuminosity * (240 + thisSaturation) + 120) / 240;
                }
                else
                {
                    num5 = thisLuminosity + thisSaturation - (thisLuminosity * thisSaturation + 120) / 240;
                }

                int num4 = 2 * thisLuminosity - num5;
                num = (byte)((HueToRgb(num4, num5, thisHue + 80) * 0xff + 120) / 240);
                num2 = (byte)((HueToRgb(num4, num5, thisHue) * 0xff + 120) / 240);
                num3 = (byte)((HueToRgb(num4, num5, thisHue - 80) * 0xff + 120) / 240);
            }

            return Color.FromArgb(num, num2, num3);
        }

        private static int HueToRgb(int n1, int n2, int thisHue)
        {
            if (thisHue < 0)
            {
                thisHue += 240;
            }

            if (thisHue > 240)
            {
                thisHue -= 240;
            }

            return thisHue switch
            {
                < 40 => n1 + ((n2 - n1) * thisHue + 20) / 40,
                < 120 => n2,
                < 160 => n1 + ((n2 - n1) * (160 - thisHue) + 20) / 40,
                _ => n1
            };
        }
    }

    internal static class ColorHelper
    {
        internal static WColor GetDarkColor(this WColor baseColor)
        {
            HlsColor color = HlsColor.CreateFromWindowsColor(baseColor);
            return color.Darker(0.3f).ToWColor();
        }

        internal static WColor GetLightColor(this WColor baseColor)
        {
            HlsColor color = HlsColor.CreateFromWindowsColor(baseColor);
            return color.Lighter(1f).ToWColor();
        }

        internal static WColor SetSaturation(this WColor baseColor, double saturation)
        {
            HlsColor color = HlsColor.CreateFromWindowsColor(baseColor);
            Color colorFromHls = HlsColor.ColorFromHls(color.hue, color.luminosity,
                (int)Math.Round(color.saturation * saturation, 2));
            return WColor.FromArgb(colorFromHls.A, colorFromHls.R, colorFromHls.G, colorFromHls.B);
        }

        private static WColor ToWColor(this Color color)
        {
            return WColor.FromArgb(color.A, color.R, color.G, color.B);
        }
    }
}