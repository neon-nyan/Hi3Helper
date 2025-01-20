using CollapseLauncher.Dialogs;
using CollapseLauncher.GameSettings;
using CollapseLauncher.GameSettings.Honkai;
using CollapseLauncher.GameSettings.Honkai.Enums;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.Screen;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.CompilerServices;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace CollapseLauncher.Pages
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
    public partial class HonkaiGameSettingsPage : INotifyPropertyChanged
    {
        #region Fields
        private int _prevGraphSelect;
        #endregion

        #region Methods
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Raise the PropertyChanged event, passing the name of the property whose value has changed.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Presets
        public ICollection<string> PresetRenderingNames
        {
            get => Settings.Preset_SettingsGraphics.PresetKeys;
        }

        public int PresetRenderingIndex
        {
            get
            {
                string name = Settings.Preset_SettingsGraphics.GetPresetKey();
                int index = Settings.Preset_SettingsGraphics.PresetKeys.IndexOf(name);
                PersonalGraphicsSettingV2 presetValue = Settings.Preset_SettingsGraphics.GetPresetFromKey(name);

                if (presetValue != null)
                {
                    Settings.SettingsGraphics = presetValue;
                }

                ToggleRenderingSettings(name == PresetConst.DefaultPresetName);
                return index;
            }
            set
            {
                if (value < 0) return;

                string name = Settings.Preset_SettingsGraphics.PresetKeys[value];
                PersonalGraphicsSettingV2 presetValue = Settings.Preset_SettingsGraphics.GetPresetFromKey(name);

                if (presetValue != null)
                {
                    Settings.SettingsGraphics = presetValue;
                }
                Settings.Preset_SettingsGraphics.SetPresetKey(presetValue);

                ToggleRenderingSettings(name == PresetConst.DefaultPresetName);
                UpdatePresetRenderingSettings();
            }
        }

        private void ToggleRenderingSettings(bool isEnable = true)
        {
            RenderingAccuracySelector.IsEnabled   = isEnable;
            ShadowQualitySelector.IsEnabled       = isEnable;
            ReflectionQualitySelector.IsEnabled   = isEnable;
            GameFXPostProcExpander.IsEnabled      = isEnable;
            GlobalIlluminationSelector.IsEnabled  = isEnable;
            AmbientOcclusionSelector.IsEnabled    = isEnable;
            LevelOfDetailSelector.IsEnabled       = isEnable;
            GameVolumetricLightSelector.IsEnabled = isEnable;
            GameMaxFPSInCombatValue.IsEnabled     = isEnable;
            GameMaxFPSInMainMenuValue.IsEnabled   = isEnable;
        }

        private void UpdatePresetRenderingSettings()
        {
            UpdatePresetFPS();
            UpdatePresetRendering();
        }
        #endregion

        #region GameResolution
        public bool IsFullscreenEnabled
        {
            get => Settings.SettingsScreen.isfullScreen;
            set
            {
                Settings.SettingsScreen.isfullScreen = value;
                if (value)
                {
                    GameWindowResizable.IsEnabled               = false;
                    GameWindowResizable.IsChecked               = false;
                    GameResolutionFullscreenExclusive.IsEnabled = !IsCustomResolutionEnabled;
                    GameResolutionBorderless.IsChecked          = false;
                    return;
                }
                GameWindowResizable.IsEnabled               = true;
                GameResolutionFullscreenExclusive.IsEnabled = false;
                GameResolutionFullscreenExclusive.IsChecked = false;
                GameResolutionBorderless.IsEnabled          = true;
            }
        }

        public bool IsBorderlessEnabled
        {
            get => Settings.SettingsCollapseScreen.UseBorderlessScreen;
            set
            {
                Settings.SettingsCollapseScreen.UseBorderlessScreen = value;
                if (value)
                {
                    GameWindowResizable.IsEnabled      = false;
                    GameWindowResizable.IsChecked      = false;
                    GameResolutionFullscreen.IsEnabled = false;
                    GameResolutionFullscreen.IsChecked = false;
                }
                else
                {
                    GameWindowResizable.IsEnabled      = true;
                    GameResolutionFullscreen.IsEnabled = true;
                }
            }
        }

        public bool IsCustomResolutionEnabled
        {
            get => Settings.SettingsCollapseScreen.UseCustomResolution;
            set
            {
                Settings.SettingsCollapseScreen.UseCustomResolution = value;
                if (value)
                {
                    GameResolutionFullscreenExclusive.IsEnabled = false;
                    GameResolutionSelector.IsEnabled = false;
                    GameCustomResolutionWidth.IsEnabled = true;
                    GameCustomResolutionHeight.IsEnabled = true;

                    string[] sizes = ResolutionSelected.Split('x');
                    GameCustomResolutionWidth.Value = int.Parse(sizes[0]);
                    GameCustomResolutionHeight.Value = int.Parse(sizes[1]);

                    return;
                }
                GameCustomResolutionWidth.IsEnabled = false;
                GameCustomResolutionHeight.IsEnabled = false;
                GameResolutionFullscreenExclusive.IsEnabled = IsFullscreenEnabled;
                GameResolutionSelector.IsEnabled = true;

                Size size = ScreenProp.CurrentResolution;
                GameResolutionSelector.SelectedItem = $"{size.Width}x{size.Height}";
            }
        }

        public bool IsCanExclusiveFullscreen
        {
            get => !(!IsFullscreenEnabled || IsCustomResolutionEnabled);
        }

        public bool IsExclusiveFullscreenEnabled
        {
            get
            {
                return IsFullscreenEnabled && Settings.SettingsCollapseScreen.UseExclusiveFullscreen;
            }
            set
            {
                Settings.SettingsCollapseScreen.UseExclusiveFullscreen = value;
                if (value)
                {
                    GameCustomResolutionCheckbox.IsEnabled = false;
                    GameCustomResolutionCheckbox.IsChecked = false;
                    GameCustomResolutionWidth.IsEnabled = false;
                    GameCustomResolutionHeight.IsEnabled = false;
                    return;
                }
                GameCustomResolutionCheckbox.IsEnabled = true;
            }
        }

        public bool IsCanResizableWindow
        {
            get => !Settings.SettingsScreen.isfullScreen && !IsExclusiveFullscreenEnabled;
        }

        public bool IsResizableWindow
        {
            get => Settings.SettingsCollapseScreen.UseResizableWindow;
            set => Settings.SettingsCollapseScreen.UseResizableWindow = value;
        }

        public int ResolutionW
        {
            get => Settings.SettingsScreen.sizeRes.Width;
            set => Settings.SettingsScreen.sizeRes = new Size(value, ResolutionH);
        }

        public int ResolutionH
        {
            get => Settings.SettingsScreen.sizeRes.Height;
            set => Settings.SettingsScreen.sizeRes = new Size(ResolutionW, value);
        }

        public bool IsCanResolutionWH
        {
            get => IsCustomResolutionEnabled;
        }

        public string ResolutionSelected
        {
            get
            {
                string res = Settings.SettingsScreen.sizeResString;
                if (!string.IsNullOrEmpty(res))
                {
                    return res;
                }

                Size size = ScreenProp.CurrentResolution;
                return $"{size.Width}x{size.Height}";
            }
            set => Settings.SettingsScreen.sizeResString = value;
        }
        #endregion

        #region FPS
        private void UpdatePresetFPS()
        {
            OnPropertyChanged(nameof(FPSInCombat));
            OnPropertyChanged(nameof(FPSInMainMenu));
        }
        public short FPSInCombat
        {
            get => Settings.SettingsGraphics.TargetFrameRateForInLevel;
            set => Settings.SettingsGraphics.TargetFrameRateForInLevel = value;
        }

        public short FPSInMainMenu
        {
            get => Settings.SettingsGraphics.TargetFrameRateForOthers;
            set => Settings.SettingsGraphics.TargetFrameRateForOthers = value;
        }
        #endregion

        #region Rendering
        private void UpdatePresetRendering()
        {
            OnPropertyChanged(nameof(GraphicsRenderingAccuracy));
            OnPropertyChanged(nameof(GraphicsShadowQuality));
            OnPropertyChanged(nameof(GraphicsReflectionQuality));
            OnPropertyChanged(nameof(IsGraphicsPostFXEnabled));
            OnPropertyChanged(nameof(IsGraphicsPhysicsEnabled));
            OnPropertyChanged(nameof(IsGraphicsFXHDREnabled));
            OnPropertyChanged(nameof(IsGraphicsFXHighQualityEnabled));
            OnPropertyChanged(nameof(IsGraphicsFXFXAAEnabled));
            OnPropertyChanged(nameof(IsGraphicsFXDistortionEnabled));
            OnPropertyChanged(nameof(GraphicsGlobalIllumination));
            OnPropertyChanged(nameof(GraphicsAmbientOcclusion));
            OnPropertyChanged(nameof(GraphicsLevelOfDetail));
            OnPropertyChanged(nameof(GraphicsVolumetricLight));
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.ResolutionQuality"/><br/>
        /// </summary>
        public int GraphicsRenderingAccuracy
        {
            get => _prevGraphSelect = (int)Settings.SettingsGraphics.ResolutionQuality;
            set => TryChallengeRenderingAccuracySet(value, value < 9);
        }

        private async void TryChallengeRenderingAccuracySet(int value, bool BypassChallenge = false)
        {
            try
            {
                if (!BypassChallenge)
                {
                    _prevGraphSelect = (int)Settings.SettingsGraphics.ResolutionQuality;
                    var result = await SimpleDialogs.Dialog_GraphicsVeryHighWarning(Content);

                    RenderingAccuracySelector.SelectedIndex = result switch
                                                              {
                                                                  ContentDialogResult.Secondary => _prevGraphSelect,
                                                                  _ => RenderingAccuracySelector.SelectedIndex
                                                              };
                }

                Settings.SettingsGraphics.ResolutionQuality = (SelectResolutionQuality)value;
            }
            catch (Exception e)
            {
                // ignored
                await SentryHelper.ExceptionHandlerAsync(e);
            }
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.ShadowLevel"/><br/>
        /// </summary>
        public int GraphicsShadowQuality
        {
            get => (int)Settings.SettingsGraphics.ShadowLevel;
            set => Settings.SettingsGraphics.ShadowLevel = (SelectShadowLevel)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.ReflectionQuality"/><br/>
        /// </summary>
        public int GraphicsReflectionQuality
        {
            get => (int)Settings.SettingsGraphics.ReflectionQuality;
            set => Settings.SettingsGraphics.ReflectionQuality = (SelectReflectionQuality)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.LightingQuality"/><br/>
        /// </summary>
        public int GraphicsLightingQuality
        {
            get => (int)Settings.SettingsGraphics.LightingQuality;
            set => Settings.SettingsGraphics.LightingQuality = (SelectLightningQuality)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.PostFXQuality"/><br/>
        /// </summary>
        public int GraphicsPostFXQuality
        {
            get => (int)Settings.SettingsGraphics.PostFXQuality;
            set => Settings.SettingsGraphics.PostFXQuality = (SelectPostFXQuality)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.AAType"/><br/>
        /// </summary>
        public int GraphicsAAType
        {
            get => (int)Settings.SettingsGraphics.AAType;
            set => Settings.SettingsGraphics.AAType = (SelectAAType)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.CharacterQuality"/><br/>
        /// </summary>
        public int GraphicsCharacterQuality
        {
            get => (int)Settings.SettingsGraphics.CharacterQuality;
            set => Settings.SettingsGraphics.CharacterQuality = (SelectCharacterQuality)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.WeatherQuality"/><br/>
        /// </summary>
        public int GraphicsWeatherQuality
        {
            get => (int)Settings.SettingsGraphics.WeatherQuality;
            set => Settings.SettingsGraphics.WeatherQuality = (SelectWeatherQuality)value;
        }
        
        /// <summary>
        /// Legacy
        /// </summary>
        public bool IsGraphicsPostFXEnabled
        {
            get => Settings.SettingsGraphics.UsePostFX;
            set
            {
                Settings.SettingsGraphics.UsePostFX = value;
                if (!(GameFXPostProcExpander.IsExpanded = value))
                {
                    GameFXHDRCheckBox.IsChecked = GameFXHDRCheckBox.IsEnabled = false;
                    GameFXHighQualityCheckBox.IsChecked = GameFXHighQualityCheckBox.IsEnabled = false;
                    GameFXFXAACheckBox.IsChecked = GameFXFXAACheckBox.IsEnabled = false;
                    GameFXDistortionCheckBox.IsChecked = GameFXDistortionCheckBox.IsEnabled = false;
                    return;
                }
                GameFXHDRCheckBox.IsEnabled = true;
                GameFXHighQualityCheckBox.IsEnabled = true;
                GameFXFXAACheckBox.IsEnabled = true;
                GameFXDistortionCheckBox.IsEnabled = true;
            }
        }

        /// <summary>
        /// <inheritdoc cref="PhysicsSimulation.UsePhysicsSimulation"/><br/>
        /// </summary>
        public bool IsGraphicsPhysicsEnabled
        {
            get => Settings.SettingsPhysics.PhysicsSimulationBool;
            set => Settings.SettingsPhysics.PhysicsSimulationBool = value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public bool IsGraphicsFXHDREnabled
        {
            get => IsGraphicsPostFXEnabled && Settings.SettingsGraphics.UseHDR;
            set => Settings.SettingsGraphics.UseHDR = value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public bool IsGraphicsFXHighQualityEnabled
        {
            get => IsGraphicsPostFXEnabled && Settings.SettingsGraphics.PostFXGradeBool;
            set => Settings.SettingsGraphics.PostFXGradeBool = value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public bool IsGraphicsFXFXAAEnabled
        {
            get => IsGraphicsPostFXEnabled && Settings.SettingsGraphics.UseFXAA;
            set => Settings.SettingsGraphics.UseFXAA = value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public bool IsGraphicsFXDistortionEnabled
        {
            get => IsGraphicsPostFXEnabled && Settings.SettingsGraphics.UseDistortion;
            set => Settings.SettingsGraphics.UseDistortion = value;
        }

        public byte GraphicsAPI
        {
            get => Settings.SettingsCollapseScreen.GameGraphicsAPI;
            set => Settings.SettingsCollapseScreen.GameGraphicsAPI = value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public int GraphicsGlobalIllumination
        {
            get => (int)Settings.SettingsGraphics.GlobalIllumination;
            set => Settings.SettingsGraphics.GlobalIllumination = (SelectGlobalIllumination)value;
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public int GraphicsAmbientOcclusion
        {
            get => (int)Settings.SettingsGraphics.AmbientOcclusion;
            set => Settings.SettingsGraphics.AmbientOcclusion = (SelectAmbientOcclusion)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.LodGrade"/><br/>
        /// </summary>
        public int GraphicsLevelOfDetail
        {
            get => Settings.SettingsGraphics.LodGrade switch
            {
                SelectLodGrade.High => 2,
                SelectLodGrade.Medium => 1,
                _ => 0
            };
            set => Settings.SettingsGraphics.LodGrade = value switch
            {
                2 => SelectLodGrade.High,
                1 => SelectLodGrade.Medium,
                _ => SelectLodGrade.Low
            };
        }

        /// <summary>
        /// Legacy
        /// </summary>
        public int GraphicsVolumetricLight
        {
            get => (int)Settings.SettingsGraphics.VolumetricLight;
            set => Settings.SettingsGraphics.VolumetricLight = (SelectVolumetricLight)value;
        }

        /// <summary>
        /// <inheritdoc cref="PersonalGraphicsSettingV2.ParticleEmitLevel"/><br/>
        /// </summary>
        public int GraphicsParticleQuality
        {
            get => (int)Settings.SettingsGraphics.ParticleEmitLevel;
            set => Settings.SettingsGraphics.ParticleEmitLevel = (SelectParticleEmitLevel)value;
        }
        #endregion

        #region Audio
        public int AudioMasterVolume
        {
            get => Settings.SettingsAudio.MasterVolume;
            set => Settings.SettingsAudio.MasterVolume = value;
        }

        public int AudioBGMVolume
        {
            get => Settings.SettingsAudio.BGMVolume;
            set => Settings.SettingsAudio.BGMVolume = value;
        }

        public int AudioSFXVolume
        {
            get => Settings.SettingsAudio.SoundEffectVolume;
            set => Settings.SettingsAudio.SoundEffectVolume = value;
        }

        public int AudioVoiceVolume
        {
            get => Settings.SettingsAudio.VoiceVolume;
            set => Settings.SettingsAudio.VoiceVolume = value;
        }

        public int AudioElfVolume
        {
            get => Settings.SettingsAudio.ElfVolume;
            set => Settings.SettingsAudio.ElfVolume = value;
        }

        public int AudioCutsceneVolume
        {
            get => Settings.SettingsAudio.CGVolumeV2;
            set => Settings.SettingsAudio.CGVolumeV2 = value;
        }

        public int AudioVoiceLanguage
        {
            get => Settings.SettingsAudio._userCVLanguageInt;
            set => Settings.SettingsAudio._userCVLanguageInt = value;
        }

        public bool AudioMute
        {
            get => Settings.SettingsAudio.Mute;
            set => Settings.SettingsAudio.Mute = value;
        }

        #endregion

        #region Misc
        public bool IsGameBoost
        {
            get => Settings.SettingsCollapseMisc.UseGameBoost;
            set => Settings.SettingsCollapseMisc.UseGameBoost = value;
        }
        #endregion

        #region Advanced Settings
        public bool IsUseAdvancedSettings
        {
            get
            {
                bool value = Settings.SettingsCollapseMisc.UseAdvancedGameSettings;
                AdvancedSettingsPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                return value;
            }
            set
            {
                Settings.SettingsCollapseMisc.UseAdvancedGameSettings = value;
                AdvancedSettingsPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public bool IsUsePreLaunchCommand
        {
            get 
            { 
                bool value = Settings.SettingsCollapseMisc.UseGamePreLaunchCommand;

                if (value)
                {
                    PreLaunchCommandTextBox.IsEnabled   = true;
                    PreLaunchForceCloseToggle.IsEnabled = true;
                    GameLaunchDelay.IsEnabled           = true;
                }
                else
                {
                    PreLaunchCommandTextBox.IsEnabled   = false;
                    PreLaunchForceCloseToggle.IsEnabled = false;
                    GameLaunchDelay.IsEnabled           = false;
                }

                return value;
            }
            set
            {
                if (value)
                {
                    PreLaunchCommandTextBox.IsEnabled   = true;
                    PreLaunchForceCloseToggle.IsEnabled = true;
                }
                else
                {
                    PreLaunchCommandTextBox.IsEnabled   = false;
                    PreLaunchForceCloseToggle.IsEnabled = false;
                }

                Settings.SettingsCollapseMisc.UseGamePreLaunchCommand = value;
            }
        }

        public string PreLaunchCommand
        {
            get => Settings.SettingsCollapseMisc.GamePreLaunchCommand;
            set => Settings.SettingsCollapseMisc.GamePreLaunchCommand = value;
        }

        public bool IsPreLaunchCommandExitOnGameClose
        {
            get => Settings.SettingsCollapseMisc.GamePreLaunchExitOnGameStop;
            set => Settings.SettingsCollapseMisc.GamePreLaunchExitOnGameStop = value;
        }
        
        public int LaunchDelay
        {
            get => Settings.SettingsCollapseMisc.GameLaunchDelay;
            set => Settings.SettingsCollapseMisc.GameLaunchDelay = value;
        }

        public bool IsUsePostExitCommand
        {
            get 
            {
                bool value = Settings.SettingsCollapseMisc.UseGamePostExitCommand;
                PostExitCommandTextBox.IsEnabled = value;
                return value;
            }
            set
            {
                PostExitCommandTextBox.IsEnabled = value;
                Settings.SettingsCollapseMisc.UseGamePostExitCommand = value;
            }
        }

        public string PostExitCommand
        {
            get => Settings.SettingsCollapseMisc.GamePostExitCommand;
            set => Settings.SettingsCollapseMisc.GamePostExitCommand = value;
        }

    #pragma warning disable CA1822
        private void GameLaunchDelay_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    #pragma warning restore CA1822
        {
            // clamp for negative value when clearing the number box
            if ((int)sender.Value < 0)
                sender.Value = 0;
        }
        #endregion
    }
}
