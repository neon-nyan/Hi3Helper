using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Microsoft.Win32;
using Hi3Helper.Win32.Screen;
using System;
using System.Drawing;
using Hi3Helper.SentryHelper;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Logger;
// ReSharper disable NonReadonlyMemberInGetHashCode
// ReSharper disable InconsistentNaming

namespace CollapseLauncher.GameSettings.Zenless
{
    internal class ScreenManager : BaseScreenSettingData, IGameSettingsValue<ScreenManager>
    {
        #region Fields
        private const           string ValueNameScreenManagerWidth      = "Screenmanager Resolution Width_h182942802";
        private const           string ValueNameScreenManagerWidthDef   = "Screenmanager Resolution Width Default_h680557497";
        private const           string ValueNameScreenManagerHeight     = "Screenmanager Resolution Height_h2627697771";
        private const           string ValueNameScreenManagerHeightDef  = "Screenmanager Resolution Height Default_h1380706816";
        private const           string ValueNameScreenManagerFullscreen = "Screenmanager Fullscreen mode_h3630240806";
        private static readonly Size   CurrentRes                       = ScreenProp.CurrentResolution;
        #endregion

        #region Enums
        public enum FullScreenMode
        {
            Fullscreen = 1,
            Something,
            Windowed
        }

        #endregion

        #region Properties
        /// <summary>
        /// This value references values from inner width and height.<br/><br/>
        /// Range: 64 x 64 - int.MaxValue x int.MaxValue<br/>
        /// Default: Your screen resolution
        /// </summary>
        public override Size sizeRes
        {
            get => new(width, height);
            set
            {
                width  = value.Width < 64 ? CurrentRes.Width : value.Width;
                height = value.Height < 64 ? CurrentRes.Height : value.Height;
            }
        }

        /// <summary>
        /// This value references values from inner width and height as string.<br/><br/>
        /// Range: 64 x 64 - int.MaxValue x int.MaxValue<br/>
        /// Default: Your screen resolution
        /// </summary>
        public override string sizeResString
        {
            get => $"{width}x{height}";
            set
            {
                string[] size = value.Split('x');
                if (!int.TryParse(size[0], out var w) || !int.TryParse(size[1], out var h))
                {
                    width  = CurrentRes.Width;
                    height = CurrentRes.Height;
                }
                else
                {
                    width  = w;
                    height = h;
                }
            }
        }

        /// <summary>
        /// This defines "<c>Game Resolution</c>'s Width" combobox In-game settings -> Video.<br/><br/>
        /// Range: 0 - int.MaxValue<br/>
        /// Default: Your screen width
        /// </summary>
        public override int width { get; set; } = CurrentRes.Width;

        /// <summary>
        /// This defines "<c>Game Resolution</c>'s Height" combobox In-game settings -> Video.<br/><br/>
        /// Range: 0 - int.MaxValue<br/>
        /// Default: Your screen Height
        /// </summary>
        public override int height { get; set; } = CurrentRes.Height;

        /// <summary>
        /// This defines "<c>Fullscreen</c>" checkbox In-game settings -> Video.<br/><br/>
        /// Range: 0 - 1
        /// Default: 1
        /// </summary>
        public FullScreenMode fullscreen { get; set; } = FullScreenMode.Fullscreen;

        public override bool isfullScreen
        {
            get => fullscreen switch
                   {
                       FullScreenMode.Fullscreen => true,
                       _ => false
                   };
            set => fullscreen = value switch
                   {
                       true => FullScreenMode.Fullscreen,
                       false => FullScreenMode.Windowed
                   };
        }
        #endregion

        #region Methods
#nullable enable
        public static ScreenManager Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException("Cannot load Zenless Screen Manager settings as RegistryKey is unexpectedly not initialized!");

                object? valueWidth = RegistryRoot.GetValue(ValueNameScreenManagerWidth, null);
                object? valueHeight = RegistryRoot.GetValue(ValueNameScreenManagerHeight, null);
                object? valueFullscreen = RegistryRoot.GetValue(ValueNameScreenManagerFullscreen, null);
                if (valueWidth != null && valueHeight != null && valueFullscreen != null)
                {
                    int width = (int)valueWidth;
                    int height = (int)valueHeight;
                    FullScreenMode fullscreen = (FullScreenMode)valueFullscreen;
#if DEBUG
                    LogWriteLine($"Loaded Zenless Settings:\r\n\t" +
                                 $"{ValueNameScreenManagerWidth} : {width}\r\n\t" +
                                 $"{ValueNameScreenManagerHeight} : {height}\r\n\t" +
                                 $"{ValueNameScreenManagerFullscreen} : {fullscreen}", LogType.Debug, true);
#endif
                    return new ScreenManager { width = width, height = height, fullscreen = fullscreen };
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading Zenless ScreenManager Data" +
                             $"\r\n  Please open the game and change any Graphics Settings, then close normally. After that you can use this feature." +
                             $"\r\n  If the issue persist, please report it on GitHub" +
                             $"\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception(
                    $"Failed when reading Zenless ScreenManager Data\r\n" +
                    $"Please open the game and change any graphics settings, then safely close the game. If the problem persist, report the issue on our GitHub\r\n" +
                    $"{ex}", ex));
            }

            return new ScreenManager();
        }

        public override void Save()
        {
            try
            {
                RegistryRoot?.SetValue(ValueNameScreenManagerFullscreen, fullscreen, RegistryValueKind.DWord);
                RegistryRoot?.SetValue(ValueNameScreenManagerWidth,      width,      RegistryValueKind.DWord);
                RegistryRoot?.SetValue(ValueNameScreenManagerWidthDef,   width,      RegistryValueKind.DWord);
                RegistryRoot?.SetValue(ValueNameScreenManagerHeight,     height,     RegistryValueKind.DWord);
                RegistryRoot?.SetValue(ValueNameScreenManagerHeightDef,  height,     RegistryValueKind.DWord);
#if DEBUG
                LogWriteLine($"Saved Zenless Settings:\r\n\t" +
                             $"{ValueNameScreenManagerFullscreen} : {RegistryRoot?.GetValue(ValueNameScreenManagerFullscreen, null)}\r\n\t" +
                             $"{ValueNameScreenManagerWidth} : {RegistryRoot?.GetValue(ValueNameScreenManagerWidth, null)}\r\n\t" +
                             $"{ValueNameScreenManagerHeight} : {RegistryRoot?.GetValue(ValueNameScreenManagerHeight, null)}", LogType.Debug, true);
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save Zenless ScreenManager Values!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
            }
        }
        #endregion

        public override bool Equals(object? comparedTo)
        {
            if (comparedTo is ScreenManager compared)
            {
                return compared.width == width && compared.height == height && compared.fullscreen == fullscreen;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(width, height, (int)fullscreen);
        }
    }
}