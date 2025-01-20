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
namespace CollapseLauncher.GameSettings.StarRail
{
    internal class LocalAudioLanguage : IGameSettingsValue<LocalAudioLanguage>
    {
        #region Fields
        private const string _ValueName = "LanguageSettings_LocalAudioLanguage_h882585060";
        #endregion

        #region Properties
        /// <summary>
        /// This defines "<c>Audio Language</c>" in-game setting
        /// Range: en//jp//kr//cn
        /// Default: en
        /// </summary>
        public string LocalAudioLang { get; set; } = "en";

        /// <summary>
        /// This defines "<c>Voice-over</c>" language In-game settings -> Audio.<br/><br/>
        /// Values:<br/>
        ///     - 1 = jp<br/>
        ///     - 0 = en<br/><br/>
        /// Default: 0 (en)
        /// </summary>
        public int LocalAudioLangInt
        {
            get => LocalAudioLang switch
            {
                "kr" => 3,
                "cn" => 2,
                "tw" => 2, // Force Traditional Chinese value to use Simplified Chinese
                "jp" => 1,
                _ => 0
            };
            set => LocalAudioLang = value switch
            {
                3 => "kr",
                2 => "cn",
                1 => "jp",
                _ => "en"
            };
        }
        #endregion

        #region Methods
#nullable enable
        public static LocalAudioLanguage Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);
                if (value != null)
                {
                    string _localAudioLang = Encoding.UTF8.GetString((byte[])value, 0, ((byte[])value).Length - 1);
#if DEBUG
                    LogWriteLine($"Loaded StarRail Settings: {_ValueName} : {Encoding.UTF8.GetString((byte[])value, 0, ((byte[])value).Length - 1)}", LogType.Debug, true);
#endif
                    return new LocalAudioLanguage { LocalAudioLang = _localAudioLang };
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
            return new LocalAudioLanguage();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");
                string data = LocalAudioLang + '\0';
                byte[] dataByte = Encoding.UTF8.GetBytes(data);
                RegistryRoot.SetValue(_ValueName, dataByte, RegistryValueKind.Binary);
#if DEBUG
                LogWriteLine($"Saved StarRail Settings: {_ValueName} : {data}", LogType.Debug, true);
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(new Exception($"Failed to save {_ValueName}!", ex), SentryHelper.ExceptionType.UnhandledOther);
            }

        }

        public override bool Equals(object? comparedTo) => comparedTo is LocalAudioLanguage toThis && TypeExtensions.IsInstancePropertyEqual(this, toThis);
        #endregion
    }
}
