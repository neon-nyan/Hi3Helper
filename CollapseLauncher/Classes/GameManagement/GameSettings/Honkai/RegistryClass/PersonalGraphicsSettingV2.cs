using CollapseLauncher.GameSettings.Honkai.Context;
using CollapseLauncher.GameSettings.Honkai.Enums;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.SentryHelper;
using Microsoft.Win32;
using System;
using System.Text;
using System.Text.Json.Serialization;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

#pragma warning disable CS0659
namespace CollapseLauncher.GameSettings.Honkai
{
    internal class PersonalGraphicsSettingV2 : IGameSettingsValue<PersonalGraphicsSettingV2>
    {
        #region Fields
        private const string _ValueName = "GENERAL_DATA_V2_PersonalGraphicsSettingV2_h3480068519";
        private short _TargetFrameRateForInLevel = 60;
        private short _TargetFrameRateForOthers = 60;
        #endregion

        #region Properties
        /// <summary>
        /// This defines "<c>Rendering Accuracy</c>" combobox In-game settings -> Video.<br/>
        /// <inheritdoc cref="SelectResolutionQuality"/><br/>
        /// Default: Middle
        /// </summary>
        public SelectResolutionQuality ResolutionQuality { get; set; } = SelectResolutionQuality.Middle;

        /// <summary>
        /// This defines "<c>Shadow</c>" combobox In-game settings -> Video.<br/>
        /// <inheritdoc cref="SelectShadowLevel"/>
        /// Default: MIDDLE
        /// </summary>
        public SelectShadowLevel ShadowLevel { get; set; } = SelectShadowLevel.MIDDLE;

        /// <summary>
        /// This defines "<c>FPS in combat</c>" combobox In-game settings -> Video.<br/>
        /// Range: 1 - 32767<br/>
        /// Default: 60
        /// </summary>
        public short TargetFrameRateForInLevel
        {
            get => _TargetFrameRateForInLevel;
            set => _TargetFrameRateForInLevel = value < 1 ? _TargetFrameRateForInLevel : value;
        }

        /// <summary>
        /// This defines "<c>FPS out of combat</c>" combobox In-game settings -> Video.<br/>
        /// Range: 1 - 32767<br/>
        /// Default: 60
        /// </summary>
        public short TargetFrameRateForOthers
        {
            get => _TargetFrameRateForOthers;
            set => _TargetFrameRateForOthers = value < 1 ? _TargetFrameRateForOthers : value;
        }

        /// <summary>
        /// This defines "<c>Reflection</c>" combobox In-game settings -> Video.<br/>
        /// <inheritdoc cref="SelectReflectionQuality"/>
        /// Default: LOW
        /// </summary>
        public SelectReflectionQuality ReflectionQuality { get; set; } = SelectReflectionQuality.LOW;

        /// <summary>
        /// This defines "<c>Lighting Quality</c>" combobox In-game settings -> Video.<br/>
        /// <inheritdoc cref="SelectLightningQuality"/>
        /// Default: LOW
        /// </summary>
        public SelectLightningQuality LightingQuality { get; set; } = SelectLightningQuality.Low;

        /// <summary>
        /// This defines "<c>Post FX Quality</c>" combobox In-game settings -> Video.<br/>
        /// <inheritdoc cref="SelectPostFXQuality"/>
        /// Default: LOW
        /// </summary>
        public SelectPostFXQuality PostFXQuality { get; set; } = SelectPostFXQuality.Low;

        /// <summary>
        /// This defines "<c>Anti Aliasing</c>" combobox In-game settings -> Video.<br/>
        /// <inheritdoc cref="SelectAAType"/>
        /// Default: FXAA
        /// </summary>
        public SelectAAType AAType { get; set; } = SelectAAType.FXAA;

        /// <summary>
        /// This defines "<c>Character Quality</c>" combobox In-game settings -> Video.<br/>
        /// <inheritdoc cref="SelectCharacterQuality"/>
        /// Default: Low
        /// </summary>
        public SelectCharacterQuality CharacterQuality { get; set; } = SelectCharacterQuality.Low;

        /// <summary>
        /// This defines "<c>Weather Quality</c>" combobox In-game settings -> Video.<br/>
        /// <inheritdoc cref="SelectWeatherQuality"/>
        /// Default: Low
        /// </summary>
        public SelectWeatherQuality WeatherQuality { get; set; } = SelectWeatherQuality.Low;
        
        /// <summary>
        /// Unused ?
        /// </summary>
        public bool UseFXAA { get; set; } = true;

        /// <summary>
        /// Unused ?
        /// </summary>
        public SelectGlobalIllumination GlobalIllumination { get; set; } = SelectGlobalIllumination.Low;

        /// <summary>
        /// Unused ?
        /// </summary>
        public SelectAmbientOcclusion AmbientOcclusion { get; set; } = SelectAmbientOcclusion.LOW;

        /// <summary>
        /// Unused ?
        /// </summary>
        public SelectVolumetricLight VolumetricLight { get; set; } = SelectVolumetricLight.Medium;

        /// <summary>
        /// Unused ?
        /// </summary>
        public bool UsePostFX { get; set; } = true;

        /// <summary>
        /// This seems to be unused on 7.3.0+<br/><br/>
        /// This defines "<c>High Quality</c>" checkbox In-game settings -> Video -> Post Processing.<br/><br/>
        /// This value is referenced to PostFXGrade.<br/>
        /// - If true, then it will set PostFXGrade to High.<br/>
        /// - If false, then it will set PostFXGrade to Low.<br/><br/>
        /// See also: <seealso cref="SelectPostFXGrade"/><br/><br/>
        /// Default: false -> Low
        /// </summary>
        [JsonIgnore]
        public bool PostFXGradeBool
        {
            get => PostFXGrade == SelectPostFXGrade.High;
            set => PostFXGrade = value ? SelectPostFXGrade.High : SelectPostFXGrade.Low;
        }

        /// <summary>
        /// This seems to be unused on 7.3.0+<br/><br/>
        /// This defines "<c>High Quality</c>" checkbox In-game settings -> Video -> Post Processing.<br/><br/>
        /// <inheritdoc cref="SelectPostFXGrade"/><br/>
        /// Default: Low
        /// Unused ?
        /// </summary>
        public SelectPostFXGrade PostFXGrade { get; set; } = SelectPostFXGrade.Low;

        /// <summary>
        /// Unused ?
        /// </summary>
        public bool UseHDR { get; set; } = true;

        /// <summary>
        /// Unused ?
        /// </summary>
        public bool UseDistortion { get; set; } = true;

        /// <summary>
        /// This defines "<c>Setting Details</c>" combobox In-game settings -> Video.<br/><br/>
        /// <inheritdoc cref="SelectLodGrade"/><br/>
        /// Default: Medium
        /// </summary>
        public SelectLodGrade LodGrade { get; set; } = SelectLodGrade.Medium;

        /// <summary>
        /// This defines "<c>Particle Quality</c>" combobox In-game settings -> Video.<br/><br/>
        /// <inheritdoc cref="SelectParticleEmitLevel"/><br/>
        /// Default: Low
        /// </summary>
        public SelectParticleEmitLevel ParticleEmitLevel { get; set; } = SelectParticleEmitLevel.Low;
        #endregion

        #region Methods
#nullable enable
        public static PersonalGraphicsSettingV2 Load()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot load {_ValueName} RegistryKey is unexpectedly not initialized!");

                object? value = RegistryRoot.GetValue(_ValueName, null);

                if (value != null)
                {
                    ReadOnlySpan<byte> byteStr = (byte[])value;
#if DEBUG
                    LogWriteLine($"Loaded HI3 Settings: {_ValueName}\r\n{Encoding.UTF8.GetString(byteStr.TrimEnd((byte)0))}", LogType.Debug, true);
#endif
                    return byteStr.Deserialize(HonkaiSettingsJsonContext.Default.PersonalGraphicsSettingV2) ?? new PersonalGraphicsSettingV2();
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

            return new PersonalGraphicsSettingV2();
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");

                string data = this.Serialize(HonkaiSettingsJsonContext.Default.PersonalGraphicsSettingV2);
                byte[] dataByte = Encoding.UTF8.GetBytes(data);

                RegistryRoot.SetValue(_ValueName, dataByte, RegistryValueKind.Binary);
#if DEBUG
                LogWriteLine($"Saved HI3 Settings: {_ValueName}\r\n{data}", LogType.Debug, true);
#endif
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
            }
        }

        public override bool Equals(object? comparedTo) => comparedTo is PersonalGraphicsSettingV2 toThis && TypeExtensions.IsInstancePropertyEqual(this, toThis);
        #endregion
    }
}
