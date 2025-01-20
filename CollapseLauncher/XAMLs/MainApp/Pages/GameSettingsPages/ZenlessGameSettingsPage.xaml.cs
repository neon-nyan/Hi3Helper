#if !DISABLEDISCORD
using CollapseLauncher.DiscordPresence;
#endif
using CollapseLauncher.GameSettings.Zenless;
using CollapseLauncher.Helper.Animation;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using Hi3Helper.Win32.Screen;
using RegistryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static CollapseLauncher.Statics.GamePropertyVault;
using Brush = Microsoft.UI.Xaml.Media.Brush;
using Color = Windows.UI.Color;
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher.Pages
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public partial class ZenlessGameSettingsPage
    {
        private GamePresetProperty           CurrentGameProperty   { get; }
        private ZenlessSettings              Settings              { get; }
        private Brush                        InheritApplyTextColor { get; set; }
        private RegistryMonitor              RegistryWatcher       { get; set; }
        
        public ZenlessGameSettingsPage()
        {
            try
            {
                CurrentGameProperty = GetCurrentGameProperty();
                Settings = CurrentGameProperty.GameSettings as ZenlessSettings;

                DispatcherQueue?.TryEnqueue(() =>
                {
                    RegistryWatcher = new RegistryMonitor(RegistryHive.CurrentUser, Path.Combine($"Software\\{CurrentGameProperty.GameVersion.VendorTypeProp.VendorType}", CurrentGameProperty.GameVersion.GamePreset.InternalGameNameInConfig!));
                    ToggleRegistrySubscribe(true);
                });

                LoadPage();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void ToggleRegistrySubscribe(bool doSubscribe)
        {
            if (doSubscribe)
                RegistryWatcher.RegChanged += RegistryListener;
            else
                RegistryWatcher.RegChanged -= RegistryListener;
        }

        private void RegistryListener(object sender, EventArgs e)
        {
            LogWriteLine("[Zenless GSP Module] RegistryMonitor has detected registry change outside of the launcher! Reloading the page...", LogType.Warning, true);
            DispatcherQueue?.TryEnqueue(MainFrameChanger.ReloadCurrentMainFrame);
        }

        private void LoadPage()
        {
            InitializeComponent();
            ApplyButton.Translation = Shadow32;
            GameSettingsApplyGrid.Translation = new Vector3(0, 0, 64);
            SettingsScrollViewer.EnableImplicitAnimation(true);
            Settings?.ReloadSettings();

            InheritApplyTextColor = ApplyText.Foreground!;
        }

        private async void RegistryExportClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ToggleRegistrySubscribe(false);
                string gameBasePath = ConverterTool.NormalizePath(CurrentGameProperty.GameVersion.GameDirPath);
                string[] relativePaths = GetFilesRelativePaths(gameBasePath, $@"{CurrentGameProperty?.GameExecutableNameWithoutExtension}_Data\Persistent\LocalStorage");
                Exception exc = await Settings.ExportSettings(true, gameBasePath, relativePaths);

                if (exc != null) throw exc;

                ApplyText.Foreground = InheritApplyTextColor;
                ApplyText.Text = Lang._GameSettingsPage.SettingsRegExported;
                ApplyText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error has occured while exporting registry!\r\n{ex}", LogType.Error, true);
                ApplyText.Foreground = new SolidColorBrush(new Color { A = 255, R = 255, B = 0, G = 0 });
                ApplyText.Text = ex.Message;
                ApplyText.Visibility = Visibility.Visible;
            }
            finally
            {
                ToggleRegistrySubscribe(true);
            }
        }

        private static string[] GetFilesRelativePaths(string gameDir, string relativePath)
        {
            string sourceDirPath = Path.Combine(gameDir, relativePath);
            return Directory.EnumerateFiles(sourceDirPath, "*", SearchOption.AllDirectories).Select(filePath =>
            {
                string fileName = Path.GetFileName(filePath);
                string trimmedRelativeDir = Path.GetDirectoryName(filePath.Substring(gameDir.Length)).Trim('\\');
                string relativeFilePath = Path.Combine(trimmedRelativeDir, fileName);
                return relativeFilePath;
            }).ToArray();
        }
        
        private async void RegistryImportClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ToggleRegistrySubscribe(false);
                string gameBasePath = ConverterTool.NormalizePath(CurrentGameProperty.GameVersion.GameDirPath);
                Exception exc = await Settings.ImportSettings(gameBasePath);

                if (exc != null) throw exc;

                ApplyText.Foreground = InheritApplyTextColor;
                ApplyText.Text = Lang._GameSettingsPage.SettingsRegImported;
                ApplyText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error has occured while importing registry!\r\n{ex}", LogType.Error, true);
                ApplyText.Foreground = new SolidColorBrush(new Color { A = 255, R = 255, B = 0, G = 0 });
                ApplyText.Text = ex.Message;
                ApplyText.Visibility = Visibility.Visible;
            }
            finally
            {
                ToggleRegistrySubscribe(true);
            }
        }
        
        private Size SizeProp           { get; set; }

        private void InitializeSettings(object sender, RoutedEventArgs e)
        {
            try
            {
                BackgroundImgChanger.ToggleBackground(true);

                SizeProp = ScreenProp.CurrentResolution;

                // Get the native resolution first
                var nativeResSize = GetNativeDefaultResolution();
                var nativeResString = string.Format(Lang._GameSettingsPage.Graphics_ResPrefixFullscreen, nativeResSize.Width, nativeResSize.Height) + $" [{Lang._Misc.Default}]";

                // Then get the rest of the list
                List<string> resFullscreen = GetResPairs_Fullscreen(nativeResSize);
                List<string> resWindowed   = GetResPairs_Windowed();

                // Add the index of fullscreen and windowed resolution booleans
                ScreenResolutionIsFullscreenIdx.Add(true);
                ScreenResolutionIsFullscreenIdx.AddRange(Enumerable.Range(0, resFullscreen.Count).Select(_ => true));
                ScreenResolutionIsFullscreenIdx.AddRange(Enumerable.Range(0, resWindowed.Count).Select(_ => false));

                // Add native resolution string, other fullscreen resolutions, and windowed resolutions
                List<string> resolutionList =
                [
                    nativeResString
                ];
                resolutionList.AddRange(resFullscreen);
                resolutionList.AddRange(resWindowed);

                GameResolutionSelector.ItemsSource   = resolutionList;
                _isAllowResolutionIndexChanged       = true; // Unlock resolution change
                GameResolutionSelector.SelectedIndex = ResolutionIndexSelected; // Refresh

                if (CurrentGameProperty.IsGameRunning)
                {
                #if !GSPBYPASSGAMERUNNING
                    Overlay.Visibility = Visibility.Visible;
                    PageContent.Visibility = Visibility.Collapsed;
                    OverlayTitle.Text = Lang._GameSettingsPage.OverlayGameRunningTitle;
                    OverlaySubtitle.Text = Lang._GameSettingsPage.OverlayGameRunningSubtitle;
                #endif
                }
                else if (GameInstallationState
                    is GameInstallStateEnum.NotInstalled
                    or GameInstallStateEnum.NeedsUpdate
                    or GameInstallStateEnum.InstalledHavePlugin
                    or GameInstallStateEnum.GameBroken)
                {
                    Overlay.Visibility = Visibility.Visible;
                    PageContent.Visibility = Visibility.Collapsed;
                    OverlayTitle.Text = Lang._GameSettingsPage.OverlayNotInstalledTitle;
                    OverlaySubtitle.Text = Lang._GameSettingsPage.OverlayNotInstalledSubtitle;
                }
                else
                {
                #if !DISABLEDISCORD
                    InnerLauncherConfig.AppDiscordPresence.SetActivity(ActivityType.GameSettings);
                #endif
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL ERROR!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private static readonly List<int> AcceptableHeight = [4320, 2880, 2160, 1440, 1280, 1080, 900, 720];

        private Size GetNativeDefaultResolution()
        {
            const int maxAcceptedNativeW = 2560; // This is the maximum native resolution width that is accepted.
                                                 // Tested on both 3840x2160 and 1920x1080 screen. The game keeps to only accept
                                                 // the resolution where width <= 2560 as its default resolution.
                                                 // In other scenario, if the screen has selection with width > 2569 but does
                                                 // not have one with width == 2560, the game will keep to use the
                                                 // resolution that has the width of 2560, like.... WTF?????
                                                 // HOYOOOOOOO!!!!!!!

            // Get the list of available resolutions. Otherwise, throw an exception.
            List<Size> currentAcceptedRes = ScreenProp.EnumerateScreenSizes().ToList();
            if (currentAcceptedRes.Count == 0)
                throw new NullReferenceException("Cannot get screen resolution. Prolly the app cannot communicate with Win32 API???");
            var maxAcceptedResW = currentAcceptedRes.Max(x => x.Width); // Find the maximum resolution width that can be accepted.

            // If the max accepted resolution width is more than or equal to maxAcceptedNativeW,
            // then clamp the resolution to the resolution that is equal to maxAcceptedNativeW.
            if (maxAcceptedResW < maxAcceptedNativeW)
            {
                return currentAcceptedRes.LastOrDefault(x => x.Width == maxAcceptedResW);
            }

            var nativeAspRatio        = (double)SizeProp.Height / SizeProp.Width;
            var nativeAspRationHeight = (int)(maxAcceptedNativeW * nativeAspRatio);

            Size nativeRes = new Size(maxAcceptedNativeW, nativeAspRationHeight);
            return nativeRes;

            // Otherwise, get the last resolution which always be the maximum for other less than width: 2560 (for example: 1920x1080).
        }
        
        private List<string> GetResPairs_Fullscreen(Size defaultResolution)
        {
            var       nativeAspRatio    = (double)SizeProp.Width / SizeProp.Height;
            List<int> acH               = AcceptableHeight;
            var       acceptedMaxHeight = ScreenProp.GetMaxHeight();

            acH.RemoveAll(h => h > acceptedMaxHeight);
            //acH.RemoveAll(h => h > 1600);

            // Get the resolution pairs and initialize default resolution index
            List<string> resPairs          = [];
            int          indexOfDefaultRes = -1;

            for (int i = 0; i < acH.Count; i++)
            {
                // Get height and calculate width
                int h = acH[i];
                int w = (int)Math.Round(h * nativeAspRatio);

                // If the resolution is the same as default, set the index
                if (h == defaultResolution.Height && w == defaultResolution.Width)
                    indexOfDefaultRes = i;

                // Add the resolution pair to the list
                resPairs.Add(string.Format(Lang._GameSettingsPage.Graphics_ResPrefixFullscreen, w, h));
            }

            // If the index of default resolution is found, remove it from the list
            if (indexOfDefaultRes != -1)
            {
                resPairs.RemoveAt(indexOfDefaultRes);
            }

            return resPairs;
        }

        private List<string> GetResPairs_Windowed()
        {
            var          nativeAspRatio    = (double)SizeProp.Width / SizeProp.Height;
            const double wideRatio         = (double)16 / 9;
            const double ulWideRatio       = (double)21 / 9;
            List<int>    acH               = AcceptableHeight;
            var          acceptedMaxHeight = ScreenProp.GetMaxHeight();

            acH.RemoveAll(h => h > acceptedMaxHeight);
            //acH.RemoveAll(h => h > 1600);
            List<string> resPairs = [];

            // If res is 21:9 then add proper native to the list
            if (Math.Abs(nativeAspRatio - ulWideRatio) < 0.01)
                resPairs.Add($"{SizeProp.Width}x{SizeProp.Height}");

            for (int i = acH.Count - 1; i >= 0; i--)
            {
                // Get height and calculate width
                int h = acH[i];
                int w = (int)Math.Round(h * wideRatio);

                // Add the resolution pair to the list
                resPairs.Add(string.Format(Lang._GameSettingsPage.Graphics_ResPrefixWindowed, w, h));
            }

            return resPairs;
        }
        
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyText.Foreground = InheritApplyTextColor;
                ApplyText.Text = Lang._GameSettingsPage.SettingsApplied;
                ApplyText.Visibility = Visibility.Visible;

                ToggleRegistrySubscribe(false);
                Settings.SaveSettings();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
            finally
            {
                ToggleRegistrySubscribe(true);
            }
        }

        public string CustomArgsValue
        {
            get => CurrentGameProperty.GameSettings.SettingsCustomArgument.CustomArgumentValue;
            set
            {
                ToggleRegistrySubscribe(false);
                CurrentGameProperty.GameSettings.SettingsCustomArgument.CustomArgumentValue = value;
                ToggleRegistrySubscribe(true);
            }
        }
        
        public bool IsUseCustomArgs
        {
            get
            {
                bool value = CurrentGameProperty.GameSettings.SettingsCollapseMisc.UseCustomArguments;
                CustomArgsTextBox.IsEnabled = value;
                return value;
            }
            set
            {
                CurrentGameProperty.GameSettings.SettingsCollapseMisc.UseCustomArguments = value;
                CustomArgsTextBox.IsEnabled = value;
            }
        }

        private void OnUnload(object sender, RoutedEventArgs e)
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                ToggleRegistrySubscribe(false);
                RegistryWatcher?.Dispose();
            });
        }
    }
}
