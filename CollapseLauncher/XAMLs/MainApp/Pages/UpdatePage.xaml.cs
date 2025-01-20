using CollapseLauncher.Helper.Update;
using CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
#if !USEVELOPACK
using Squirrel;
#else
using Velopack;
#endif
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable RedundantExtendsListEntry
// ReSharper disable AsyncVoidMethod
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher.Pages
{
    public sealed partial class UpdatePage : Page
    {
        private readonly CancellationTokenSource _tokenSource    = new();
        private readonly MarkdownConfig          _markdownConfig = new();

        public UpdatePage()
        {
            InitializeComponent();
            Loaded += LoadedAsyncRoutine;
            Unloaded += UpdatePage_Unloaded;
        }

        private static void UpdatePage_Unloaded(object sender, RoutedEventArgs e) => ChangeTitleDragArea.Change(DragAreaTemplate.Default);

        private async void LoadedAsyncRoutine(object sender, RoutedEventArgs e)
        {
            ChangeTitleDragArea.Change(DragAreaTemplate.Full);

            string channelName = IsPreview ? Lang._Misc.BuildChannelPreview : Lang._Misc.BuildChannelStable;
            CurrentVersionLabel.Text = $"{LauncherUpdateHelper.LauncherCurrentVersionString}";

            if (LauncherUpdateHelper.AppUpdateVersionProp == null)
                throw new NullReferenceException("New version property in LauncherUpdateHelper.AppUpdateVersionProp should haven't be null!");

            if (LauncherUpdateHelper.AppUpdateVersionProp.Version != null)
            {
                    GameVersion newUpdateVersion = LauncherUpdateHelper.AppUpdateVersionProp.Version.Value;

                    NewVersionLabel.Text = newUpdateVersion.VersionString;
            }

            UpdateChannelLabel.Text = channelName;
            AskUpdateCheckbox.IsChecked = GetAppConfigValue("DontAskUpdate").ToBoolNullable() ?? false;
            BuildTimestampLabel.Text = LauncherUpdateHelper.AppUpdateVersionProp?.TimeLocalTime?.ToString("f");

            await GetReleaseNote();

            try
            {
                if (!(LauncherUpdateHelper.AppUpdateVersionProp?.IsForceUpdate ?? false))
                    await WaitForCountdown();

                await StartUpdateRoutine();
            }
            catch (TaskCanceledException)
            {
                Logger.LogWriteLine("Update countdown has been cancelled!", LogType.Default, true);
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
                Logger.LogWriteLine($"Update has failed!\r\n{ex}", LogType.Error, true);
            }
        }

        private async Task StartUpdateRoutine()
        {
            try
            {
                if (LauncherUpdateWatcher.IsMetered && !(LauncherUpdateHelper.AppUpdateVersionProp?.IsForceUpdate ?? false))
                {
                    switch (await Dialog_MeteredConnectionWarning(Content))
                    {
                        case ContentDialogResult.Primary:
                            await _StartUpdateRoutine();
                            break;
                        case ContentDialogResult.None:
                            CancelUpdateCountdownBox.Visibility = Visibility.Collapsed;
                            UpdateCountdownPanel.Visibility = Visibility.Collapsed;
                            UpdateBox.Visibility = Visibility.Visible;
                            return;
                        case ContentDialogResult.Secondary:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                await _StartUpdateRoutine();
            }
            catch (OperationCanceledException)
            {
                Logger.LogWriteLine("Update has been canceled!", LogType.Warning, true);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"Error occurred while updating the launcher!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
                ForceInvokeUpdate = true;
                LauncherUpdateWatcher.GetStatus(new LauncherUpdateProperty { QuitFromUpdateMenu = true });
            }
        }

        private async Task _StartUpdateRoutine()
        {
            // Hide/Show progress
            UpdateCountdownPanel.Visibility = Visibility.Collapsed;
            UpdateBox.Visibility = Visibility.Collapsed;
            CancelUpdateCountdownBox.Visibility = Visibility.Collapsed;
            AskUpdateCheckbox.Visibility = Visibility.Collapsed;
            UpdateProgressBox.Visibility = Visibility.Visible;

            // Start Update manager check routine
            await GetUpdateInformation();
        }

        private async Task GetUpdateInformation()
        {
            string channelName = IsPreview ? "Preview" : "Stable";

            Updater updater = new Updater(channelName.ToLower());
            updater.UpdaterProgressChanged += Updater_UpdaterProgressChanged;
            updater.UpdaterStatusChanged += Updater_UpdaterStatusChanged;

            UpdateInfo updateInfo = await updater.StartCheck();

            if (!await updater.StartUpdate(updateInfo))
            {
                RemindMeClick(new Button(), new RoutedEventArgs());
                return;
            }
            await updater.FinishUpdate();
        }

        private async Task WaitForCountdown()
        {
            CancelUpdateCountdownBox.Visibility = Visibility.Visible;
            UpdateCountdownPanel.Visibility = Visibility.Visible;
            int maxCount = 10;
            while (maxCount > 0)
            {
                UpdateCountdownText.Text = string.Format(Lang._UpdatePage.UpdateCountdownMessage1, maxCount);
                await Task.Delay(1000, _tokenSource.Token);
                maxCount--;
            }
        }

        private async Task GetReleaseNote()
        {
            ReleaseNotesBox.Text = Lang._UpdatePage.LoadingRelease;

            try
            {
                await using BridgedNetworkStream networkStream = await FallbackCDNUtil.TryGetCDNFallbackStream($"changelog_{(IsPreview ? "preview" : "stable")}.md", _tokenSource.Token, true);
                byte[] buffer = new byte[networkStream.Length];
                await networkStream.ReadExactlyAsync(buffer, _tokenSource.Token);

                ReleaseNotesBox.Text = Encoding.UTF8.GetString(buffer);
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex);
                ReleaseNotesBox.Text = string.Format(Lang._UpdatePage.LoadingReleaseFailed, ex);
            }
        }

        private void AskUpdateToggle(object sender, RoutedEventArgs e)
        {
            bool askForUpdateLater = ((CheckBox)sender).IsChecked ?? false;
            SetAndSaveConfigValue("DontAskUpdate", askForUpdateLater);
        }

        private async void DoUpdateClick(object sender, RoutedEventArgs e)
        {
            await StartUpdateRoutine();
        }

        private void CancelCountdownClick(object sender, RoutedEventArgs e)
        {
            _tokenSource.Cancel();
            ((Button)sender).Visibility = Visibility.Collapsed;
            UpdateCountdownPanel.Visibility = Visibility.Collapsed;
            UpdateBox.Visibility = Visibility.Visible;
        }

        private void RemindMeClick(object sender, RoutedEventArgs e)
        {
            _tokenSource.Cancel();
            ForceInvokeUpdate = true;
            LauncherUpdateWatcher.GetStatus(new LauncherUpdateProperty { QuitFromUpdateMenu = true });
        }

        private void Updater_UpdaterStatusChanged(object sender, Updater.UpdaterStatus e)
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                Status.Text = e.status;
                if (string.IsNullOrEmpty(e.newver))
                {
                    return;
                }

                GameVersion version = new GameVersion(e.newver);
                NewVersionLabel.Text = version.VersionString;
            });
        }

        private void Updater_UpdaterProgressChanged(object sender, Updater.UpdaterProgress e)
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                progressBar.IsIndeterminate = false;
                progressBar.Value = e.ProgressPercentage;
                TimeEstimation.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.TimeLeft);
            });
        }
    }
}
