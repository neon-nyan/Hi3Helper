using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.StarRail.Context;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Text;
using System.Text.Json.Serialization;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.Screen;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

#pragma warning disable CS0659
namespace CollapseLauncher.GameSettings.StarRail
{
    internal class PCResolution : BaseScreenSettingData, IGameSettingsValue<PCResolution>
    {
        #region Fields
        private const           string _ValueName                        = "GraphicsSettings_PCResolution_h431323223";
        private const           string _ValueNameScreenManagerWidth      = "Screenmanager Resolution Width_h182942802";
        private const           string _ValueNameScreenManagerHeight     = "Screenmanager Resolution Height_h2627697771";
        private const           string _ValueNameScreenManagerFullscreen = "Screenmanager Fullscreen mode_h3630240806";
        private static readonly Size   currentRes                        = ScreenProp.CurrentResolution;
        #endregion

        #region Properties
        /// <summary>
        /// This value references values from inner width and height.<br/><br/>
        /// Range: 64 x 64 - int.MaxValue x int.MaxValue<br/>
        /// Default: Your screen resolution
        /// </summary>
        [JsonIgnore]
        public override Size sizeRes
        {
            get => new(width, height);
            set
            {
                width = value.Width < 64 ? currentRes.Width : value.Width;
                height = value.Height < 64 ? currentRes.Height : value.Height;
            }
        }

        /// <summary>
        /// This value references values from inner width and height as string.<br/><br/>
        /// Range: 64 x 64 - int.MaxValue x int.MaxValue<br/>
        /// Default: Your screen resolution
        /// </summary>
        [JsonIgnore]
        public override string sizeResString
        {
            get => $"{width}x{height}";
            set
            {
                string[] size = value.Split('x');
                int w;
                int h;
                if (!int.TryParse(size[0], out w) || !int.TryParse(size[1], out h))
                {
                    width = currentRes.Width;
                    height = currentRes.Height;
                }
                else
                {
                    width = w;
                    height = h;
                }
            }
        }

        /// <summary>
        /// This defines "<c>Game Resolution</c>'s Width" combobox In-game settings -> Video.<br/><br/>
        /// Range: 0 - int.MaxValue<br/>
        /// Default: Your screen width
        /// </summary>
        public override int width { get; set; } = currentRes.Width;

        /// <summary>
        /// This defines "<c>Game Resolution</c>'s Height" combobox In-game settings -> Video.<br/><br/>
        /// Range: 0 - int.MaxValue<br/>
        /// Default: Your screen Height
        /// </summary>
        public override int height { get; set; } = currentRes.Height;

        /// <summary>
        /// This defines "<c>Fullscreen</c>" checkbox In-game settings -> Video.<br/><br/>
        /// Default: true
        /// </summary>
        public bool isFullScreen { get; set; } = true;

        /// <summary>
        /// Same as <c>isFullScreen</c><br/><br/>
        /// </summary>
        [JsonIgnore]
        public override bool isfullScreen
        {
            get => isFullScreen;
            set => isFullScreen = value;
        }
        #endregion

        #region Methods
#nullable enable
        public static PCResolution Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);

                if (value != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])value;
#if DEBUG
                    LogWriteLine($"Loaded StarRail Settings: {_ValueName}\r\n{Encoding.UTF8.GetString(byteStr.TrimEnd((byte)0))}", LogType.Debug, true);
#endif
                    return byteStr.Deserialize(StarRailSettingsJsonContext.Default.PCResolution) ?? new PCResolution();
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}" +
                             $"\r\n  Please open the game and change any settings, then close normally. After that you can use this feature." +
                             $"\r\n  If the issue persist, please report it on GitHub" +
                             $"\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception(
                    $"Failed when reading game settings {_ValueName}\r\n" +
                    $"Please open the game and change any settings, then safely close the game. If the problem persist, report the issue on our GitHub\r\n" +
                    $"{ex}", ex));
            }

            return new PCResolution();
        }

        public override void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");

                string data = this.Serialize(StarRailSettingsJsonContext.Default.PCResolution);
                byte[] dataByte = Encoding.UTF8.GetBytes(data);

                RegistryRoot.SetValue(_ValueName, dataByte, RegistryValueKind.Binary);
#if DEBUG
                LogWriteLine($"Saved StarRail Settings: {_ValueName}\r\n{data}", LogType.Debug, true);
#endif
                SaveIndividualRegistry();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(new Exception($"Failed to save {_ValueName}!", ex), SentryHelper.ExceptionType.UnhandledOther);
            }
        }

        private void SaveIndividualRegistry()
        {
            RegistryRoot?.SetValue(_ValueNameScreenManagerFullscreen, isfullScreen ? 1 : 3, RegistryValueKind.DWord);
            RegistryRoot?.SetValue(_ValueNameScreenManagerWidth, width, RegistryValueKind.DWord);
            RegistryRoot?.SetValue(_ValueNameScreenManagerHeight, height, RegistryValueKind.DWord);
#if DEBUG
            LogWriteLine($"Saved StarRail Settings: {_ValueNameScreenManagerFullscreen} : {RegistryRoot?.GetValue(_ValueNameScreenManagerFullscreen, null)}", LogType.Debug, true);
            LogWriteLine($"Saved StarRail Settings: {_ValueNameScreenManagerWidth} : {RegistryRoot?.GetValue(_ValueNameScreenManagerWidth, null)}", LogType.Debug, true);
            LogWriteLine($"Saved StarRail Settings: {_ValueNameScreenManagerHeight} : {RegistryRoot?.GetValue(_ValueNameScreenManagerHeight, null)}", LogType.Debug, true);
#endif
        }

        public override bool Equals(object? comparedTo) => comparedTo is PCResolution toThis && TypeExtensions.IsInstancePropertyEqual(this, toThis);
        #endregion
    }
}
