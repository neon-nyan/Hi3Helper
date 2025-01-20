using CollapseLauncher.GameSettings.StarRail;
using Hi3Helper;
using Hi3Helper.Win32.Screen;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.CompilerServices;
// ReSharper disable InconsistentNaming
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo

namespace CollapseLauncher.Pages
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public partial class StarRailGameSettingsPage : INotifyPropertyChanged
    {
        #region Methods
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        // ReSharper disable once UnusedMember.Local
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Raise the PropertyChanged event, passing the name of the property whose value has changed.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
                    GameWindowResizable.IsEnabled = false;
                    GameWindowResizable.IsChecked = false;
                    GameResolutionFullscreenExclusive.IsEnabled = !IsCustomResolutionEnabled;
                    GameResolutionBorderless.IsChecked = false;
                    return;
                }
                GameWindowResizable.IsEnabled = true;
                GameResolutionFullscreenExclusive.IsEnabled = false;
                GameResolutionFullscreenExclusive.IsChecked = false;
                GameResolutionBorderless.IsEnabled = true;
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
                    GameWindowResizable.IsEnabled = false;
                    GameWindowResizable.IsChecked = false;
                    GameResolutionFullscreen.IsEnabled = false;
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
                    GameCustomResolutionCheckbox.IsEnabled = true;
                }
                else
                {
                    GameCustomResolutionCheckbox.IsEnabled = false;
                    GameCustomResolutionCheckbox.IsChecked = false;
                }
            }
        }

        public bool IsCanCustomResolution
        {
            get => Settings.SettingsCollapseScreen.UseResizableWindow && !IsExclusiveFullscreenEnabled;
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

        #region Models
        //FPS
        public int FPS
        {
            get
            {
                int value = Model.FPSIndexDict[NormalizeFPSNumber(Settings.GraphicsSettings.FPS)];
                if (value != 2)
                {
                    return value;
                }

                VSyncToggle.IsChecked = false;
                VSyncToggle.IsEnabled = false;

                return value;
            }
            set
            {
                if (value == 2)
                {
                    VSyncToggle.IsChecked = false;
                    VSyncToggle.IsEnabled = false;
                }
                else { VSyncToggle.IsEnabled = true; }

                Settings.GraphicsSettings.FPS = Model.FPSIndex[value];
            }
        }

        // Set it to 60 (default) if the value isn't within Model.FPSIndexDict
        private static int NormalizeFPSNumber(int input) => !Model.FPSIndexDict.ContainsKey(input) ? Model.FPSIndex[Model.FPSDefaultIndex] : input;

        //VSync
        public bool EnableVSync
        {
            get => Settings.GraphicsSettings.EnableVSync;
            set => Settings.GraphicsSettings.EnableVSync = value;
        }
        //RenderScale
        public double RenderScale
        {
            get => Math.Round(Settings.GraphicsSettings.RenderScale, 1);
            set => Settings.GraphicsSettings.RenderScale = Math.Round(value, 1); // Round it to x.x (0.1) to fix floating-point rounding issue
        }
        //ResolutionQuality
        public int ResolutionQuality
        {
            get => Math.Clamp((int)Settings.GraphicsSettings.ResolutionQuality, 0, 5);
            set => Settings.GraphicsSettings.ResolutionQuality = (Quality)value;
        }
        //ShadowQuality
        public int ShadowQuality
        {
            get
            {
                var v = Math.Clamp((int)Settings.GraphicsSettings.ShadowQuality, 0, 4);
                if (v == 1) v = 2;
                return v;
            }
            set => Settings.GraphicsSettings.ShadowQuality = (Quality)value;
        }
        //LightQuality
        public int LightQuality
        {
            get => Math.Clamp((int)Settings.GraphicsSettings.LightQuality, 1, 5);
            set => Settings.GraphicsSettings.LightQuality = (Quality)value;
        }
        //CharacterQuality
        public int CharacterQuality
        {
            get => Math.Clamp((int)Settings.GraphicsSettings.CharacterQuality, 2, 4);
            set => Settings.GraphicsSettings.CharacterQuality = (Quality)value;
        }
        //EnvDetailQuality
        public int EnvDetailQuality
        {
            get => Math.Clamp((int)Settings.GraphicsSettings.EnvDetailQuality, 1, 5);
            set => Settings.GraphicsSettings.EnvDetailQuality = (Quality)value;
        }
        //ReflectionQuality
        public int ReflectionQuality
        {
            get => Math.Clamp((int)Settings.GraphicsSettings.ReflectionQuality, 1, 5);
            set => Settings.GraphicsSettings.ReflectionQuality = (Quality)value;
        }
        //SFXQuality
        public int SFXQuality
        {
            get => Math.Clamp((int)Settings.GraphicsSettings.SFXQuality, 1, 4);
            set => Settings.GraphicsSettings.SFXQuality = (Quality)value;
        }

        //DLSSQuality
        public int DlssQuality
        {
            get => Math.Clamp((int)Settings.GraphicsSettings.DlssQuality, 0, 5);
            set => Settings.GraphicsSettings.DlssQuality = (DLSSMode)value;
        }
        //BloomQuality
        public int BloomQuality
        {
            get => Math.Clamp((int)Settings.GraphicsSettings.BloomQuality, 0, 5);
            set => Settings.GraphicsSettings.BloomQuality = (Quality)value;
        }
        //AAMode
        public int AAMode
        {
            get => Math.Clamp((int)Settings.GraphicsSettings.AAMode, 0, 2);
            set => Settings.GraphicsSettings.AAMode = (AntialiasingMode)value;
        }
        //EnableSelfShadow
        public bool SelfShadow
        {
            get
            {
                var v = Settings.GraphicsSettings.EnableSelfShadow;
                switch (v)
                {
                    case 1:
                        return true;
                    case 2:
                        return false;
                    default:
                        Logger.LogWriteLine($"Self Shadow value is unknown! Val: {v}", LogType.Error, true);
                        return false;
                }
            }
            set => Settings.GraphicsSettings.EnableSelfShadow = value ? 1 : 2;
        }
        //HalfResTransparent
        public bool HalfResTransparent
        {
            get => Settings.GraphicsSettings.EnableHalfResTransparent;
            set => Settings.GraphicsSettings.EnableHalfResTransparent = value;
        }
        #endregion

        #region Audio
        public int AudioMasterVolume
        {
            get => Settings.AudioSettings_Master.MasterVol = Settings.AudioSettings_Master.MasterVol;
            set => Settings.AudioSettings_Master.MasterVol = value;
        }

        public int AudioBGMVolume
        {
            get => Settings.AudioSettings_BGM.BGMVol = Settings.AudioSettings_BGM.BGMVol;
            set => Settings.AudioSettings_BGM.BGMVol = value;
        }

        public int AudioSFXVolume
        {
            get => Settings.AudioSettings_SFX.SFXVol = Settings.AudioSettings_SFX.SFXVol;
            set => Settings.AudioSettings_SFX.SFXVol = value;
        }

        public int AudioVOVolume
        {
            get => Settings.AudioSettings_VO.VOVol = Settings.AudioSettings_VO.VOVol;
            set => Settings.AudioSettings_VO.VOVol = value;
        }

        public int AudioLang
        {
            get => Settings.AudioLanguage.LocalAudioLangInt = Settings.AudioLanguage.LocalAudioLangInt;
            set => Settings.AudioLanguage.LocalAudioLangInt = value;
        }

        public int TextLang
        {
            get => Settings.TextLanguage.LocalTextLangInt = Settings.TextLanguage.LocalTextLangInt;
            set => Settings.TextLanguage.LocalTextLangInt = value;
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
                PostExitCommandTextBox.IsEnabled                     = value;
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
