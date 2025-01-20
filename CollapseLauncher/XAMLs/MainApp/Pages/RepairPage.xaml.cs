#if !DISABLEDISCORD
using CollapseLauncher.DiscordPresence;
#endif
using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Win32.Native.ManagedTools;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Threading.Tasks;
using static CollapseLauncher.Statics.GamePropertyVault;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable RedundantExtendsListEntry
// ReSharper disable AsyncVoidMethod

namespace CollapseLauncher.Pages
{
    public sealed partial class RepairPage : Page
    {
        private GamePresetProperty CurrentGameProperty { get; }
        public RepairPage()
        {
            BackgroundImgChanger.ToggleBackground(true);
            CurrentGameProperty = GetCurrentGameProperty();
            InitializeComponent();
        }

        private void StartGameCheckSplitButton(SplitButton sender, SplitButtonClickEventArgs args)
        {
            string tag = (string)sender.Tag;
            bool isFast = tag == "Fast";

            RunCheckRoutine(sender, isFast, true);
        }

        private void StartGameCheck(object sender, RoutedEventArgs e)
        {
            string tag = (string)(sender as ButtonBase)?.Tag;
            bool isFast = tag == "Fast";

            RunCheckRoutine(sender, isFast, false);
        }

        private async void RunCheckRoutine(object sender, bool isFast, bool isMainButton)
        {
            Sleep.PreventSleep(ILoggerHelper.GetILogger());
            
            CheckFilesBtn.Flyout.Hide();
            CheckFilesBtn.IsEnabled = false;
            CancelBtn.IsEnabled = true;

            if (!isMainButton)
            {
                SetMainCheckFilesBtnProperty(sender);
            }

            try
            {
                AddEvent();

                bool isGameBroken = await (CurrentGameProperty.GameRepair?.StartCheckRoutine(isFast) ?? Task.FromResult(false));

                RepairFilesBtn.IsEnabled = isGameBroken;
                CheckFilesBtn.IsEnabled = !isGameBroken;
                CancelBtn.IsEnabled = false;

                RepairFilesBtn.Visibility = isGameBroken ? Visibility.Visible : Visibility.Collapsed;
                CheckFilesBtn.Visibility = isGameBroken ? Visibility.Collapsed : Visibility.Visible;

                // If the current window is not in focus, then spawn the notification toast
                if (!WindowUtility.IsCurrentWindowInFocus())
                {
                    WindowUtility.Tray_ShowNotification(
                                                        Lang._NotificationToast.GameRepairCheckCompleted_Title,
                                                        isGameBroken ?
                                                            string.Format(Lang._NotificationToast.GameRepairCheckCompletedFound_Subtitle, CurrentGameProperty.GameRepair?.AssetEntry.Count) :
                                                            Lang._NotificationToast.GameRepairCheckCompletedNotFound_Subtitle
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
                LogWriteLine($"An error occured while checking asset!\r\n{ex}", LogType.Error, true);
            }
            finally
            {
                RemoveEvent();
                Sleep.RestoreSleep();
            }
        }

        private async void StartGameRepair(object sender, RoutedEventArgs e)
        {
            try
            {
                Sleep.PreventSleep(ILoggerHelper.GetILogger());
                RepairFilesBtn.IsEnabled = false;
                CancelBtn.IsEnabled = true;

                AddEvent();

                int assetCount = CurrentGameProperty.GameRepair?.AssetEntry.Count ?? 0;

                await (CurrentGameProperty.GameRepair?.StartRepairRoutine() ?? Task.CompletedTask);

                RepairFilesBtn.IsEnabled = false;
                CheckFilesBtn.IsEnabled = true;
                CancelBtn.IsEnabled = false;

                RepairFilesBtn.Visibility = Visibility.Collapsed;
                CheckFilesBtn.Visibility = Visibility.Visible;

                // If the current window is not in focus, then spawn the notification toast
                if (!WindowUtility.IsCurrentWindowInFocus())
                {
                    WindowUtility.Tray_ShowNotification(
                                                        Lang._NotificationToast.GameRepairDownloadCompleted_Title,
                                                        string.Format(Lang._NotificationToast.GameRepairDownloadCompleted_Subtitle, assetCount)
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
                LogWriteLine($"An error occured while repairing asset!\r\n{ex}", LogType.Error, true);
            }
            finally
            {
                RemoveEvent();
                Sleep.RestoreSleep();
            }
        }

        private void SetMainCheckFilesBtnProperty(object sender)
        {
            string btnText = ((TextBlock)((Panel)((Button)sender).Content).Children[1]).Text;
            string btnTag = (string)((Button)sender).Tag;
            string btnToolTip = (string)ToolTipService.GetToolTip((Button)sender);

            ((TextBlock)((Panel)CheckFilesBtn.Content).Children[1]).Text = btnText;
            CheckFilesBtn.Tag = btnTag;
            ToolTipService.SetToolTip(CheckFilesBtn, btnToolTip);
        }

        private void AddEvent()
        {
            if (CurrentGameProperty.GameRepair == null)
                return;

            CurrentGameProperty.GameRepair.ProgressChanged += _repairTool_ProgressChanged;
            CurrentGameProperty.GameRepair.StatusChanged += _repairTool_StatusChanged;

            RepairTotalProgressBar.IsIndeterminate = true;
            RepairPerFileProgressBar.IsIndeterminate = true;
        }

        private void RemoveEvent()
        {
            if (CurrentGameProperty.GameRepair == null)
                return;

            CurrentGameProperty.GameRepair.ProgressChanged -= _repairTool_ProgressChanged;
            CurrentGameProperty.GameRepair.StatusChanged -= _repairTool_StatusChanged;

            RepairTotalProgressBar.IsIndeterminate = false;
            RepairPerFileProgressBar.IsIndeterminate = false;
        }

        private void _repairTool_StatusChanged(object sender, TotalPerFileStatus e)
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
                RepairDataTableGrid.Visibility = e.IsAssetEntryPanelShow ? Visibility.Visible : Visibility.Collapsed;
                RepairStatus.Text = e.ActivityStatus;

                RepairPerFileStatus.Text = e.ActivityPerFile;
                RepairTotalStatus.Text = e.ActivityAll;
                RepairTotalProgressBar.IsIndeterminate = e.IsProgressAllIndetermined;
                RepairPerFileProgressBar.IsIndeterminate = e.IsProgressPerFileIndetermined;
            }
        }

        private void _repairTool_ProgressChanged(object sender, TotalPerFileProgress e)
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
                RepairPerFileProgressBar.Value = Math.Min(e.ProgressPerFilePercentage, 100);
                RepairTotalProgressBar.Value   = Math.Min(e.ProgressAllPercentage, 100);
            }
        }

        private void ResetStatusAndButtonState()
        {
            RepairStatus.Text = Lang._GameRepairPage.Status1;

            CancelBtn.IsEnabled = false;
            CheckFilesBtn.Visibility = Visibility.Visible;
            CheckFilesBtn.IsEnabled = true;
            RepairFilesBtn.Visibility = Visibility.Collapsed;
        }

        private void CancelOperation(object sender, RoutedEventArgs e)
        {
            CurrentGameProperty.GameRepair?.CancelRoutine();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            CurrentGameProperty.GameRepair?.CancelRoutine();
            CurrentGameProperty.GameRepair?.AssetEntry.Clear();
        }

        private void InitializeLoaded(object sender, RoutedEventArgs e)
        {
            BackgroundImgChanger.ToggleBackground(true);
            if (GameInstallationState
                is GameInstallStateEnum.NotInstalled
                or GameInstallStateEnum.NeedsUpdate
                or GameInstallStateEnum.InstalledHavePlugin
                or GameInstallStateEnum.GameBroken)
            {
                Overlay.Visibility = Visibility.Visible;
                PageContent.Visibility = Visibility.Collapsed;
                OverlayTitle.Text = Lang._GameRepairPage.OverlayNotInstalledTitle;
                OverlaySubtitle.Text = Lang._GameRepairPage.OverlayNotInstalledSubtitle;
            }
            else if (CurrentGameProperty.IsGameRunning)
            {
                Overlay.Visibility = Visibility.Visible;
                PageContent.Visibility = Visibility.Collapsed;
                OverlayTitle.Text = Lang._GameRepairPage.OverlayGameRunningTitle;
                OverlaySubtitle.Text = Lang._GameRepairPage.OverlayGameRunningSubtitle;
            }
        #if !DISABLEDISCORD
            else
            {
                InnerLauncherConfig.AppDiscordPresence?.SetActivity(ActivityType.Repair);
            }
        #endif
        }
    }
}
