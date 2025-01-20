using Hi3Helper;
using Hi3Helper.EncTool;
using Microsoft.Win32;
using System;
using Hi3Helper.SentryHelper;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

#pragma warning disable CS0659
namespace CollapseLauncher.GameSettings.Genshin
{
    internal class VisibleBackground
    {
        #region Fields
        private const string _ValueName = "UnityVisibleBackground_h3203912122";
        #endregion

        #region Properties
        /// <summary>
        /// This defines "<c>Borderless</c>" native settings. Available only on native resolution picker in Resolution setting.<br/><br/>
        /// Range: 0 - 1
        /// Default: 0
        /// </summary>
        public int borderless { get; set; }

        /// <summary>
        /// Converted value from borderless integer inside UnityVisibleBackground registry to usable boolean.
        /// </summary>
        public bool isBorderless { get => borderless == 1; set => borderless = value ? 1 : 0; }
        #endregion

        #region Methods
        #nullable enable
        public static VisibleBackground Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} since RegistryRoot is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);
                if (value != null)
                {
                    int borderless = (int)value;
#if DEBUG
                    LogWriteLine($"Loaded Genshin Settings: {_ValueName} : {value}", LogType.Debug, true);
#endif 
                    return new VisibleBackground { borderless = borderless };
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}" +
                             $"\r\n  Please open the game and change any Graphics Settings, then close normally. After that you can use this feature." +
                             $"\r\n  If the issue persist, please report it on GitHub" +
                             $"\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception(
                    $"Failed when reading game settings {_ValueName}\r\n" +
                    $"Please open the game and change any graphics settings, then safely close the game. If the problem persist, report the issue on our GitHub\r\n" +
                    $"{ex}", ex));
            }
            return new VisibleBackground();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");
                RegistryRoot.SetValue(_ValueName, borderless, RegistryValueKind.DWord);
#if DEBUG
                LogWriteLine($"Saved Genshin Settings: {_ValueName} : {borderless}", LogType.Debug, true);
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(new Exception($"Failed to save {_ValueName}!", ex), SentryHelper.ExceptionType.UnhandledOther);
            }
        }

        public override bool Equals(object? comparedTo) => comparedTo is VisibleBackground toThis && TypeExtensions.IsInstancePropertyEqual(this, toThis);
        #endregion
    }
}
