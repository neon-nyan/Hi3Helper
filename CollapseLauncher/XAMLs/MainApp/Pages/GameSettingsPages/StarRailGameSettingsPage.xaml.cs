using CollapseLauncher.Dialogs;
using CollapseLauncher.GameSettings.StarRail;
using CollapseLauncher.Helper.Animation;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using RegistryUtils;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using Windows.UI;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static CollapseLauncher.Statics.GamePropertyVault;
using Hi3Helper.SentryHelper;


#if !DISABLEDISCORD
using CollapseLauncher.DiscordPresence;
#endif

namespace CollapseLauncher.Pages
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public partial class StarRailGameSettingsPage
    {
        private GamePresetProperty CurrentGameProperty   { get; }
        private StarRailSettings   Settings              { get; }
        private Brush              InheritApplyTextColor { get; set; }
        private RegistryMonitor    RegistryWatcher       { get; set; }
        
        private const string AbValueName = "App_Settings_h2319593470";
        public StarRailGameSettingsPage()
        {
            try
            {
                CurrentGameProperty = GetCurrentGameProperty();
                Settings = CurrentGameProperty.GameSettings as StarRailSettings;

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
            LogWriteLine("[HSR GSP Module] RegistryMonitor has detected registry change outside of the launcher! Reloading the page...", LogType.Warning, true);
            DispatcherQueue?.TryEnqueue(MainFrameChanger.ReloadCurrentMainFrame);
        }

        private async void LoadPage()
        {
            try
            {
                Settings.ReloadSettings();

                InitializeComponent();
                ApplyButton.Translation           = Shadow32;
                GameSettingsApplyGrid.Translation = new Vector3(0, 0, 64);
                SettingsScrollViewer.EnableImplicitAnimation(true);

                InheritApplyTextColor = ApplyText.Foreground!;
            #nullable enable
                // A/B Testing as of 2023-12-26 (HSR v1.6.0)
                if (CheckAbTest())
                {
                    await SimpleDialogs.Dialog_GenericWarning(Content!);
                }
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }

        /// <summary>
        /// Returns true if A/B test registry identifier is found.
        /// </summary>
        public static bool CheckAbTest()
        {
            object? abValue = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Cognosphere\Star Rail", AbValueName, null);
            if (abValue == null)
            {
                return false;
            }

            LogWriteLine("A/B Value Found. Settings will not apply to the game.", LogType.Warning, true);
            return true;
        }
#nullable disable
        private async void RegistryExportClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ToggleRegistrySubscribe(false);
                Exception exc = await Settings.ExportSettings();

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

        private async void RegistryImportClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ToggleRegistrySubscribe(false);
                Exception exc = await Settings.ImportSettings();

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

        private void InitializeSettings(object sender, RoutedEventArgs e)
        {
            try
            {
                BackgroundImgChanger.ToggleBackground(true);
                GameResolutionSelector.ItemsSource = ScreenResolutionsList;

                if (CurrentGameProperty.IsGameRunning)
                {
                #if !GSPBYPASSGAMERUNNING
                    Overlay.Visibility = Visibility.Visible;
                    PageContent.Visibility = Visibility.Collapsed;
                    OverlayTitle.Text = Lang._StarRailGameSettingsPage.OverlayGameRunningTitle;
                    OverlaySubtitle.Text = Lang._StarRailGameSettingsPage.OverlayGameRunningSubtitle;
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
                    OverlayTitle.Text = Lang._StarRailGameSettingsPage.OverlayNotInstalledTitle;
                    OverlaySubtitle.Text = Lang._StarRailGameSettingsPage.OverlayNotInstalledSubtitle;
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

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyText.Foreground = InheritApplyTextColor;
                ApplyText.Text = Lang._StarRailGameSettingsPage.SettingsApplied;
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
