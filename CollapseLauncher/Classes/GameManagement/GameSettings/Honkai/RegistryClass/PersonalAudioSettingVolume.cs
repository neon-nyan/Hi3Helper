using CollapseLauncher.GameSettings.Honkai.Context;
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
namespace CollapseLauncher.GameSettings.Honkai
{
    internal class PersonalAudioSettingVolume : IGameSettingsValue<PersonalAudioSettingVolume>
    {
        #region Fields
        private const string _ValueName = "GENERAL_DATA_V2_PersonalAudioSettingVolume_h600615720";
        #endregion

        #region Properties
        /// <summary>
        /// This defines "<c>Master Volume</c>" slider In-game settings -> Audio.<br/><br/>
        /// Range: 0.0f - 100.0f<br/>
        /// Default: 100.0f
        /// </summary>
        public float MasterVolumeValue { get; set; } = 100.0f;

        /// <summary>
        /// This defines "<c>BGM</c>" slider In-game settings -> Audio -> Volume Balance.<br/><br/>
        /// Range: 0.0f - 3.0f<br/>
        /// Default: 3.0f
        /// </summary>
        public float BGMVolumeValue { get; set; } = 3.0f;

        /// <summary>
        /// This defines "<c>SFX</c>" slider In-game settings -> Audio -> Volume Balance.<br/><br/>
        /// Range: 0.0f - 3.0f<br/>
        /// Default: 3.0f
        /// </summary>
        public float SoundEffectVolumeValue { get; set; } = 3.0f;

        /// <summary>
        /// This defines "<c>Voice Acting</c>" slider In-game settings -> Audio -> Volume Balance.<br/><br/>
        /// Range: 0.0f - 3.0f<br/>
        /// Default: 3.0f
        /// </summary>
        public float VoiceVolumeValue { get; set; } = 3.0f;

        /// <summary>
        /// This defines "<c>ELF VO</c>" slider In-game settings -> Audio -> Volume Balance.<br/><br/>
        /// Range: 0.0f - 3.0f<br/>
        /// Default: 3.0f
        /// </summary>
        public float ElfVolumeValue { get; set; } = 3.0f;

        /// <summary>
        /// This defines "<c>CG</c>" slider In-game settings -> Audio -> Volume Balance.<br/><br/>
        /// Range: 0.0f - 3.0f<br/>
        /// Default: 3.0f
        /// </summary>
        public float CGVolumeValue { get; set; } = 1.8f;

        /// <summary>
        /// Default: false
        /// </summary>
        public bool CreateByDefault { get; set; } = false;
        #endregion

        #region Methods
#nullable enable
        public static PersonalAudioSettingVolume Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);

                if (value != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])value;
#if DEBUG
                    LogWriteLine($"Loaded HI3 Settings: {_ValueName}\r\n{Encoding.UTF8.GetString((byte[])value, 0, ((byte[])value).Length - 1)}", LogType.Debug, true);
#endif
                    return byteStr.Deserialize(HonkaiSettingsJsonContext.Default.PersonalAudioSettingVolume) ?? new PersonalAudioSettingVolume();
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
            return new PersonalAudioSettingVolume();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");

                string data = this.Serialize(HonkaiSettingsJsonContext.Default.PersonalAudioSettingVolume);
                byte[] dataByte = Encoding.UTF8.GetBytes(data);

                RegistryRoot.SetValue(_ValueName, dataByte, RegistryValueKind.Binary);
#if DEBUG
                LogWriteLine($"Saved HI3 Settings: {_ValueName}\r\n{data}", LogType.Debug, true);
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(new Exception($"Failed to save {_ValueName}!", ex), SentryHelper.ExceptionType.UnhandledOther);
            }
        }

        public override bool Equals(object? comparedTo) => comparedTo is PersonalAudioSettingVolume toThis && TypeExtensions.IsInstancePropertyEqual(this, toThis);
        #endregion
    }
}
