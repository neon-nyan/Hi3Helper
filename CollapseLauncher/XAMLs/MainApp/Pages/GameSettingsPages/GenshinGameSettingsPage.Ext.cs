using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.GameSettings.Genshin.Enums;
using Hi3Helper.Win32.Screen;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.CompilerServices;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher.Pages
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public partial class GenshinGameSettingsPage : INotifyPropertyChanged
    {
        #region Methods
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Raise the PropertyChanged event, passing the name of the property whose value has changed.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region GameResolution
        public bool IsFullscreenEnabled
        {
            get
            {
               bool value = Settings!.SettingsScreen!.isfullScreen;
               if (value)
               {
                   GameWindowResizable.IsEnabled      = false;
                   GameWindowResizable.IsChecked      = false;
                   GameResolutionBorderless.IsEnabled = false;
                   GameResolutionBorderless.IsChecked = false;
               }
               else
               {
                   GameWindowResizable.IsEnabled      = true;
                   GameResolutionBorderless.IsEnabled = true;
               }
               return value;
            }
            set
            {
                Settings.SettingsScreen.isfullScreen = value;
                if (value)
                {
                    GameWindowResizable.IsEnabled               = false;
                    GameWindowResizable.IsChecked               = false;
                    GameResolutionFullscreenExclusive!.IsEnabled = !IsCustomResolutionEnabled;
                    GameResolutionFullscreenExclusive.IsChecked = false;
                    GameResolutionBorderless.IsChecked          = false;
                    return;
                }
                GameWindowResizable.IsEnabled = true;
                GameResolutionFullscreenExclusive!.IsEnabled = false;
                GameResolutionBorderless.IsEnabled = true;
            }
        }

        public bool IsBorderlessEnabled
        {
            get
            { 
                bool valueCl = Settings.SettingsCollapseScreen.UseBorderlessScreen;
                bool valueGi = Settings.SettingVisibleBackground.isBorderless;
                if (valueCl || valueGi)
                {
                    Settings.SettingsCollapseScreen.UseBorderlessScreen = true;
                    Settings.SettingVisibleBackground.isBorderless      = true;

                    GameWindowResizable.IsEnabled      = false;
                    GameWindowResizable.IsChecked      = false;
                    GameResolutionFullscreen.IsChecked = false;

                    return true;
                }
                GameWindowResizable.IsEnabled      = true;
                GameResolutionFullscreen.IsEnabled = true;

                return false;
            } 
            set
            {
                Settings.SettingsCollapseScreen.UseBorderlessScreen = value;
                Settings.SettingVisibleBackground.isBorderless      = value;
                if (value)
                {
                    GameWindowResizable.IsEnabled = false;
                    GameWindowResizable.IsChecked = false;
                    GameResolutionFullscreen.IsChecked = false;
                }
                else
                {
                    GameWindowResizable.IsEnabled = true;
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
            set
            {
                Settings.SettingsCollapseScreen.UseResizableWindow = value;
                if (value)
                {
                    GameResolutionFullscreen.IsChecked = false;
                    GameResolutionFullscreen.IsEnabled = false;
                    GameResolutionBorderless.IsChecked = false;
                    GameResolutionBorderless.IsEnabled = false;
                }
                else
                {
                    GameResolutionFullscreen.IsEnabled = true;
                    GameResolutionBorderless.IsEnabled = true;
                }
            }
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

        #region Graphics Settings
        public double Gamma
        {
            get => Math.Round(Settings.SettingsGeneralData.gammaValue * -1 + 4.4, 5);
            set => Settings.SettingsGeneralData.gammaValue = Math.Round(value * -1 + 4.4, 5);
            // This should belong in programmer horror stories, *DO NOT EVER DO THIS*
            // Basically, calculate the stepper function which amounts to y = -x + 4.4, so we inverse the value and add 4.4
            // DON'T ASK HOW WE DID THIS, IT'S a 4AM THING :)
            // Round it to x.xxxxx because floating point
        }

        public bool VerticalSync
        {
            get => Convert.ToBoolean((int)Settings.SettingsGeneralData.globalPerfData.VerticalSync);
            set => Settings.SettingsGeneralData.globalPerfData.VerticalSync = (VerticalSyncOption)Convert.ToInt32(value);
        }

        public bool VolumetricFog
        {
            get => Convert.ToBoolean((int)Settings.SettingsGeneralData.globalPerfData.VolumetricFog);
            set => Settings.SettingsGeneralData.globalPerfData.VolumetricFog = (VolumetricFogOption)Convert.ToInt32(value);
        }

        public bool Reflections
        {
            get => Convert.ToBoolean((int)Settings.SettingsGeneralData.globalPerfData.Reflections);
            set => Settings.SettingsGeneralData.globalPerfData.Reflections = (ReflectionsOption)Convert.ToInt32(value);
        }

        public bool Bloom
        {
            get => Convert.ToBoolean((int)Settings.SettingsGeneralData.globalPerfData.Bloom);
            set => Settings.SettingsGeneralData.globalPerfData.Bloom = (BloomOption)Convert.ToInt32(value);
        }

        public int FPS
        {
            get
            {
                // Get the current value
                FPSOption curValue = Settings.SettingsGeneralData.globalPerfData.FPS;
                // Get the index of the current value in FPSOptionsList array
                int indexOfValue = Array.IndexOf(GlobalPerfData.FPSOptionsList!, curValue);
                // Return the index of the value
                return indexOfValue;
            }
            set
            {
                // [HACK]: Fix some rare occasion where the "value" turned into -1. If so, then return
                if (value < 0) return;

                // Get the FPSOption based on the selected index by the "value"
                FPSOption valueFromIndex = GlobalPerfData.FPSOptionsList[value];
                // Set the actual value to its property
                Settings.SettingsGeneralData.globalPerfData.FPS = valueFromIndex;
            }
        }

        public int RenderScale
        {
            get
            {
                int enumIndex = Settings.SettingsGeneralData.globalPerfData.RenderResolution;
                int valueIndex = GlobalPerfData.RenderScaleIndex.IndexOf(enumIndex);
                double enumValue = GlobalPerfData.RenderScaleValues[valueIndex];
                return GlobalPerfData.RenderScaleValues.IndexOf(enumValue);
            }
            set
            {
                double enumValue = GlobalPerfData.RenderScaleValues[value];
                int enumIndex = DictionaryCategory.RenderResolutionOption[enumValue];
                Settings.SettingsGeneralData.globalPerfData.RenderResolution = enumIndex;
            }
        }

        public int ShadowQuality
        {
            get
            {
                int curValue = (int)Settings.SettingsGeneralData.globalPerfData.ShadowQuality;

                // Disable Volumetric Fog when ShadowQuality is not Medium or higher
                if (curValue < 2)
                {
                    VolumetricFogToggle.IsChecked = false;
                    VolumetricFogToggle.IsEnabled = false;
                }
                else
                {
                    VolumetricFogToggle.IsEnabled = true;
                }

                return curValue;
            }
            set
            {
                if (value < 2)
                {
                    VolumetricFogToggle.IsChecked = false;
                    VolumetricFogToggle.IsEnabled = false;
                }
                else
                {
                    VolumetricFogToggle.IsEnabled = true;
                }

                Settings.SettingsGeneralData.globalPerfData.ShadowQuality = (ShadowQualityOption)value;
            }
        }

        public int VisualEffects
        {
            get => (int)Settings.SettingsGeneralData.globalPerfData.VisualEffects;
            set => Settings.SettingsGeneralData.globalPerfData.VisualEffects = (VisualEffectsOption)value;
        }

        public int SFXQuality
        {
            get => (int)Settings.SettingsGeneralData.globalPerfData.SFXQuality;
            set => Settings.SettingsGeneralData.globalPerfData.SFXQuality = (SFXQualityOption)value;
        }

        public int EnvironmentDetail
        {
            get => (int)Settings.SettingsGeneralData.globalPerfData.EnvironmentDetail;
            set => Settings.SettingsGeneralData.globalPerfData.EnvironmentDetail = (EnvironmentDetailOption)value;
        }

        public int MotionBlur
        {
            get => (int)Settings.SettingsGeneralData.globalPerfData.MotionBlur;
            set => Settings.SettingsGeneralData.globalPerfData.MotionBlur = (MotionBlurOption)value;
        }

        public int CrowdDensity
        {
            get => (int)Settings.SettingsGeneralData.globalPerfData.CrowdDensity;
            set => Settings.SettingsGeneralData.globalPerfData.CrowdDensity = (CrowdDensityOption)value;
        }

        public int SubsurfaceScattering
        {
            get => (int)Settings.SettingsGeneralData.globalPerfData.SubsurfaceScattering;
            set => Settings.SettingsGeneralData.globalPerfData.SubsurfaceScattering = (SubsurfaceScatteringOption)value;
        }

        public int CoOpTeammateEffects
        {
            get => (int)Settings.SettingsGeneralData.globalPerfData.CoOpTeammateEffects;
            set => Settings.SettingsGeneralData.globalPerfData.CoOpTeammateEffects = (CoOpTeammateEffectsOption)value;
        }

        public int AnisotropicFiltering
        {
            get => (int)Settings.SettingsGeneralData.globalPerfData.AnisotropicFiltering;
            set => Settings.SettingsGeneralData.globalPerfData.AnisotropicFiltering = (AnisotropicFilteringOption)value;
        }

        public int Antialiasing
        {
            get => (int)Settings.SettingsGeneralData.globalPerfData.Antialiasing;
            set => Settings.SettingsGeneralData.globalPerfData.Antialiasing = (AntialiasingOption)value;
        }

        public bool TeamPageBackground
        {
            get => !Settings.SettingsGeneralData.disableTeamPageBackgroundSwitch;
            set => Settings.SettingsGeneralData.disableTeamPageBackgroundSwitch = !value;
        }

        public int GlobalIllumination
        {
            get => (int)Settings.SettingsGeneralData.globalPerfData.GlobalIllumination;
            set => Settings.SettingsGeneralData.globalPerfData.GlobalIllumination = (GlobalIlluminationOption)value;
        }

        public bool DynamicCharacterResolution
        {
            get => Convert.ToBoolean((int)Settings.SettingsGeneralData.globalPerfData.DynamicCharacterResolution);
            set => Settings.SettingsGeneralData.globalPerfData.DynamicCharacterResolution = (DynamicCharacterResolutionOption)Convert.ToInt32(value);
        }
        #endregion

        #region Graphics Settings - HDR
        public bool IsHDR
        {
            get => GetAppConfigValue("ForceGIHDREnable").ToBool() || Settings.SettingsWindowsHDR.isHDR || Settings.SettingsGeneralData.enableHDR;
            set
            {
                Settings.SettingsWindowsHDR.isHDR = value;
                Settings.SettingsGeneralData.enableHDR = value;
                SetAndSaveConfigValue("ForceGIHDREnable", value);
            } 
        }

        public double MaxLuminosity
        {
            get => (double)Settings.SettingsGeneralData.maxLuminosity;
            set => Settings.SettingsGeneralData.maxLuminosity = (decimal)Math.Round(value, 1);
        }

        public double UiPaperWhite
        {
            get => (double)Settings.SettingsGeneralData.uiPaperWhite;
            set => Settings.SettingsGeneralData.uiPaperWhite = (decimal)Math.Round(value, 1);
        }

        public double ScenePaperWhite
        {
            get => (double)Settings.SettingsGeneralData.scenePaperWhite;
            set => Settings.SettingsGeneralData.scenePaperWhite = (decimal)Math.Round(value, 1);
        }
        #endregion

        #region Audio
        public int Audio_Global
        {
            get => Settings.SettingsGeneralData.volumeGlobal;
            set => Settings.SettingsGeneralData.volumeGlobal = value;
        }

        public int Audio_SFX
        {
            get => Settings.SettingsGeneralData.volumeSFX;
            set => Settings.SettingsGeneralData.volumeSFX = value;
        }

        public int Audio_Music
        {
            get => Settings.SettingsGeneralData.volumeMusic;
            set => Settings.SettingsGeneralData.volumeMusic = value;
        }

        public int Audio_Voice
        {
            get => Settings.SettingsGeneralData.volumeVoice;
            set => Settings.SettingsGeneralData.volumeVoice = value;
        }

        public bool Audio_DynamicRange
        {
            get => !Convert.ToBoolean(Settings.SettingsGeneralData.audioDynamicRange);
            set => Settings.SettingsGeneralData.audioDynamicRange = Convert.ToInt32(!value);
        }

        public bool Audio_Surround
        {
            get => Convert.ToBoolean(Settings.SettingsGeneralData.audioOutput);
            set => Settings.SettingsGeneralData.audioOutput = Convert.ToInt32(value);
        }

        public bool Audio_MuteOnMinimized
        {
            get => Settings.SettingsGeneralData.muteAudioOnAppMinimized;
            set => Settings.SettingsGeneralData.muteAudioOnAppMinimized = value;
        }
        #endregion

        #region Language
        public int AudioLang
        {
            get => Settings.SettingsGeneralData.deviceVoiceLanguageType;
            set => Settings.SettingsGeneralData.deviceVoiceLanguageType = value;
        }

        public int TextLang
        {
            get => Settings.SettingsGeneralData.deviceLanguageType - 1;
            set => Settings.SettingsGeneralData.deviceLanguageType = value + 1;
        }
        #endregion

        #region Misc
        public bool IsGameBoost
        {
            get => Settings.SettingsCollapseMisc.UseGameBoost;
            set => Settings.SettingsCollapseMisc.UseGameBoost = value;
        }

        public bool IsMobileMode
        {
            get => Settings.SettingsCollapseMisc.LaunchMobileMode;
            set => Settings.SettingsCollapseMisc.LaunchMobileMode = value;
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
                AdvancedSettingsPanel.Visibility                      = value ? Visibility.Visible : Visibility.Collapsed;
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
                }
                else
                {
                    PreLaunchCommandTextBox.IsEnabled   = false;
                    PreLaunchForceCloseToggle.IsEnabled = false;
                }

                return value;
            }
            set
            {
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
