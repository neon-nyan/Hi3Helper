#if !DISABLEDISCORD
using CollapseLauncher.DiscordPresence;
#endif
using CollapseLauncher.Helper;
using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Win32.Native.ManagedTools;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Threading.Tasks;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Pages
{
    public sealed partial class CachesPage
    {
        private GamePresetProperty CurrentGameProperty { get; }

        public CachesPage()
        {
            BackgroundImgChanger.ToggleBackground(true);
            CurrentGameProperty = GamePropertyVault.GetCurrentGameProperty();
            InitializeComponent();
        }

        private void StartCachesCheckSplitButton(SplitButton sender, SplitButtonClickEventArgs args)
        {
            string tag = (string)sender.Tag;
            bool isFast = tag == "Fast";

            RunCheckRoutine(sender, isFast, true);
        }

        private void StartCachesCheck(object sender, RoutedEventArgs e)
        {
            string tag = (string)(sender as ButtonBase)?.Tag;
            bool isFast = tag == "Fast";

            RunCheckRoutine(sender, isFast, false);
        }

        public async void RunCheckRoutine(object sender, bool isFast, bool isMainButton)
        {
            try
            {
                CheckUpdateBtn.Flyout.Hide();
                CheckUpdateBtn.IsEnabled = false;
                CancelBtn.IsEnabled      = true;

                if (!isMainButton)
                {
                    SetMainCheckUpdateBtnProperty(sender);
                }

                Sleep.PreventSleep(ILoggerHelper.GetILogger());
                AddEvent();

                bool isNeedUpdate = await (CurrentGameProperty.GameCache?.StartCheckRoutine(isFast) ?? Task.FromResult(false));

                UpdateCachesBtn.IsEnabled = isNeedUpdate;
                CheckUpdateBtn.IsEnabled = !isNeedUpdate;
                CancelBtn.IsEnabled = false;

                UpdateCachesBtn.Visibility = isNeedUpdate ? Visibility.Visible : Visibility.Collapsed;
                CheckUpdateBtn.Visibility = isNeedUpdate ? Visibility.Collapsed : Visibility.Visible;

                // If the current window is not in focus, then spawn the notification toast
                if (!WindowUtility.IsCurrentWindowInFocus())
                {
                    WindowUtility.Tray_ShowNotification(
                        Lang._NotificationToast.CacheUpdateCheckCompleted_Title,
                        isNeedUpdate ?
                            string.Format(Lang._NotificationToast.CacheUpdateCheckCompletedFound_Subtitle, CurrentGameProperty.GameCache?.AssetEntry.Count) :
                            Lang._NotificationToast.CacheUpdateCheckCompletedNotFound_Subtitle
                        );
                }
            }
            catch (TaskCanceledException)
            {
                ResetStatusAndButtonState();
            }
            catch (OperationCanceledException)
            {
                ResetStatusAndButtonState();
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
                LogWriteLine($"An error occured while checking cache!\r\n{ex}", LogType.Error, true);
            }
            finally
            {
                RemoveEvent();
                Sleep.RestoreSleep();
            }
        }

        public async void StartCachesUpdate(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateCachesBtn.IsEnabled = false;
                CancelBtn.IsEnabled       = true;

                Sleep.PreventSleep(ILoggerHelper.GetILogger());
                AddEvent();

                int assetCount = CurrentGameProperty.GameCache?.AssetEntry.Count ?? 0;

                await (CurrentGameProperty.GameCache?.StartUpdateRoutine() ?? Task.CompletedTask);

                UpdateCachesBtn.IsEnabled = false;
                CheckUpdateBtn.IsEnabled = true;
                CancelBtn.IsEnabled = false;

                UpdateCachesBtn.Visibility = Visibility.Collapsed;
                CheckUpdateBtn.Visibility = Visibility.Visible;

                // If the current window is not in focus, then spawn the notification toast
                if (!WindowUtility.IsCurrentWindowInFocus())
                {
                    WindowUtility.Tray_ShowNotification(
                                                        Lang._NotificationToast.CacheUpdateDownloadCompleted_Title,
                                                        string.Format(Lang._NotificationToast.CacheUpdateDownloadCompleted_Subtitle, assetCount)
                                                       );
                }
            }
            catch (TaskCanceledException)
            {
                ResetStatusAndButtonState();
            }
            catch (OperationCanceledException)
            {
                ResetStatusAndButtonState();
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
                LogWriteLine($"An error occured while updating cache!\r\n{ex}", LogType.Error, true);
            }
            finally
            {
                Sleep.RestoreSleep();
                RemoveEvent();
            }
        }

        private void SetMainCheckUpdateBtnProperty(object sender)
        {
            string btnText = ((TextBlock)((Panel)((Button)sender).Content).Children[1]).Text;
            string btnTag = (string)((Button)sender).Tag;
            string btnToolTip = (string)ToolTipService.GetToolTip((Button)sender);

            ((TextBlock)((Panel)CheckUpdateBtn.Content).Children[1]).Text = btnText;
            CheckUpdateBtn.Tag = btnTag;
            ToolTipService.SetToolTip(CheckUpdateBtn, btnToolTip);
        }

        private void AddEvent()
        {
            if (CurrentGameProperty.GameCache == null)
            {
                return;
            }

            CurrentGameProperty.GameCache.ProgressChanged += _cacheTool_ProgressChanged;
            CurrentGameProperty.GameCache.StatusChanged   += _cacheTool_StatusChanged;

            CachesTotalProgressBar.IsIndeterminate = true;
        }

        private void RemoveEvent()
        {
            if (CurrentGameProperty.GameCache == null)
            {
                return;
            }

            CurrentGameProperty.GameCache.ProgressChanged -= _cacheTool_ProgressChanged;
            CurrentGameProperty.GameCache.StatusChanged -= _cacheTool_StatusChanged;

            CachesTotalProgressBar.IsIndeterminate = false;
        }

        private void _cacheTool_StatusChanged(object sender, TotalPerFileStatus e)
        {
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue?.TryEnqueue(Update);
                return;
            }

            Update();
            return;
            void Update()
            {
                CachesDataTableGrid.Visibility = e.IsAssetEntryPanelShow ? Visibility.Visible : Visibility.Collapsed;
                CachesStatus.Text              = e.ActivityStatus;

                CachesTotalStatus.Text                 = e.ActivityAll;
                CachesTotalProgressBar.IsIndeterminate = e.IsProgressAllIndetermined;
            }
        }

        private void _cacheTool_ProgressChanged(object sender, TotalPerFileProgress e)
        {
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue?.TryEnqueue(Update);
                return;
            }

            Update();
            return;
            void Update()
            {
                CachesTotalProgressBar.Value = e.ProgressAllPercentage;
            }
        }

        private void ResetStatusAndButtonState()
        {
            CachesStatus.Text = Lang._GameRepairPage.Status1;

            CancelBtn.IsEnabled = false;
            CheckUpdateBtn.Visibility = Visibility.Visible;
            CheckUpdateBtn.IsEnabled = true;
            UpdateCachesBtn.Visibility = Visibility.Collapsed;
        }

        public void CancelOperation(object sender, RoutedEventArgs e)
        {
            CurrentGameProperty.GameCache?.CancelRoutine();
        }

        private void InitializeLoaded(object sender, RoutedEventArgs e)
        {
            BackgroundImgChanger.ToggleBackground(true);
            if (m_appMode == AppMode.Hi3CacheUpdater) return;

            if (GameInstallationState
                is GameInstallStateEnum.NotInstalled
                or GameInstallStateEnum.NeedsUpdate
                or GameInstallStateEnum.InstalledHavePlugin
                or GameInstallStateEnum.GameBroken)
            {
                Overlay.Visibility = Visibility.Visible;
                PageContent.Visibility = Visibility.Collapsed;
                OverlayTitle.Text = Lang._CachesPage.OverlayNotInstalledTitle;
                OverlaySubtitle.Text = Lang._CachesPage.OverlayNotInstalledSubtitle;
            }
            else if (CurrentGameProperty.IsGameRunning)
            {
                Overlay.Visibility = Visibility.Visible;
                PageContent.Visibility = Visibility.Collapsed;
                OverlayTitle.Text = Lang._CachesPage.OverlayGameRunningTitle;
                OverlaySubtitle.Text = Lang._CachesPage.OverlayGameRunningSubtitle;
            }
            else
            {
            #if !DISABLEDISCORD
                AppDiscordPresence?.SetActivity(ActivityType.Cache);
            #endif
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            CurrentGameProperty.GameCache?.CancelRoutine();
            CurrentGameProperty.GameCache?.AssetEntry.Clear();
        }
    }
}
