using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Microsoft.Win32;
using System;
using System.Text;
using Hi3Helper.SentryHelper;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

#pragma warning disable CS0659
namespace CollapseLauncher.GameSettings.Universal
{
    internal class CollapseScreenSetting : IGameSettingsValue<CollapseScreenSetting>
    {
        #region Fields
        private const string _ValueName = "CollapseLauncher_ScreenSetting";
        #endregion

        #region Properties
        /// <summary>
        /// This defines if the game should run in a custom resolution.<br/><br/>
        /// Default: false
        /// </summary>
        public bool UseCustomResolution { get; set; } = false;

        /// <summary>
        /// This defines if the game should run in Exclusive Fullscreen mode.<br/><br/>
        /// Default: false
        /// </summary>
        public bool UseExclusiveFullscreen { get; set; } = false;

        /// <summary>
        /// This defines if the game should run in Borderless Screen mode. <br/><br/>
        /// Default: false
        /// </summary>
        public bool UseBorderlessScreen { get; set; } = false;

        /// <summary>
        /// This defines if the game should run in Resizable window. <br/>
        /// The window should run in windowed-mode and would not work with any fullscreen modes. <br/><br/>
        /// Default: false
        /// </summary>
        public bool UseResizableWindow { get; set; } = false;

        /// <summary>
        /// This defines the Graphics API will be used for the game to run.<br/><br/>
        /// Values:<br/>
        ///     - 0 = DirectX 11 (Feature Level: 10.1)<br/>
        ///     - 1 = DirectX 11 (Feature Level: 11.0) No Single-thread<br/>
        ///     - 2 = DirectX 11 (Feature Level: 11.1)<br/>
        ///     - 3 = DirectX 11 (Feature Level: 11.1) No Single-thread<br/>
        ///     - 4 = DirectX 12 (Feature Level: 12.0) [Experimental]<br/><br/>
        /// Default: 3
        /// </summary>
        public byte GameGraphicsAPI { get; set; } = 3;
        #endregion

        #region Methods
#nullable enable
        public static CollapseScreenSetting Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);

                if (value != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])value;
#if DEBUG
                    LogWriteLine($"Loaded Collapse Screen Settings:\r\n{Encoding.UTF8.GetString(byteStr.TrimEnd((byte)0))}", LogType.Debug, true);
#endif
                    return byteStr.Deserialize(UniversalSettingsJsonContext.Default.CollapseScreenSetting) ?? new CollapseScreenSetting();
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(new Exception($"Failed to read {_ValueName}!", ex), SentryHelper.ExceptionType.UnhandledOther);
            }

            return new CollapseScreenSetting();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");

                string data = this.Serialize(UniversalSettingsJsonContext.Default.CollapseScreenSetting);
                byte[] dataByte = Encoding.UTF8.GetBytes(data);
#if DEBUG
                LogWriteLine($"Saved Collapse Screen Settings:\r\n{data}", LogType.Debug, true);
#endif
                RegistryRoot.SetValue(_ValueName, dataByte, RegistryValueKind.Binary);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(new Exception($"Failed to save {_ValueName}!", ex), SentryHelper.ExceptionType.UnhandledOther);
            }
        }

        public override bool Equals(object? comparedTo) => comparedTo is CollapseScreenSetting toThis && TypeExtensions.IsInstancePropertyEqual(this, toThis);
        #endregion
    }
}
