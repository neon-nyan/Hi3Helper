using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Background;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.Loading;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.Update;
using CollapseLauncher.Pages;
using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]      
    public sealed partial class MainPage
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        private GamePresetProperty CurrentGameProperty { get; set; }
        private bool IsLoadRegionComplete { get; set; }

        private static  string        RegionToChangeName { get => $"{GetGameTitleRegionTranslationString(LauncherMetadataHelper.CurrentMetadataConfigGameName, Lang._GameClientTitles)} - {GetGameTitleRegionTranslationString(LauncherMetadataHelper.CurrentMetadataConfigGameRegion, Lang._GameClientRegions)}"; }
        private         List<object>  LastMenuNavigationItem;
        private         List<object>  LastFooterNavigationItem;
        internal static string        PreviousTag = string.Empty;

        internal async Task<bool> LoadRegionFromCurrentConfigV2(PresetConfig preset, string gameName, string gameRegion)
        {
            CancellationTokenSourceWrapper tokenSource = new CancellationTokenSourceWrapper();

            string regionToChangeName = $"{preset.GameLauncherApi.GameNameTranslation} - {preset.GameLauncherApi.GameRegionTranslation}";

            return await preset.GameLauncherApi.LoadAsync(BeforeLoadRoutine, AfterLoadRoutine, ActionOnTimeOutRetry, OnErrorRoutine, tokenSource.Token);

            void OnErrorRoutine(Exception ex) => OnErrorRoutineInner(ex, ErrorType.Unhandled);

            void OnErrorRoutineInner(Exception ex, ErrorType errorType)
            {
                LoadingMessageHelper.HideActionButton();
                LoadingMessageHelper.HideLoadingFrame();

                LogWriteLine($"Error has occurred while loading: {regionToChangeName}!\r\n{ex}", LogType.Scheme, true);
                ErrorSender.SendExceptionWithoutPage(ex, errorType);
                MainFrameChanger.ChangeWindowFrame(typeof(DisconnectedPage));
            }

            void CancelLoadEvent(object sender, RoutedEventArgs args)
            {
                tokenSource.Cancel();

                // If explicit cancel was triggered, restore the navigation menu item then return false
                foreach (object item in LastMenuNavigationItem)
                {
                    NavigationViewControl.MenuItems.Add(item);
                }
                foreach (object item in LastFooterNavigationItem)
                {
                    NavigationViewControl.FooterMenuItems.Add(item);
                }
                NavigationViewControl.IsSettingsVisible = true;
                LastMenuNavigationItem.Clear();
                LastFooterNavigationItem.Clear();
                if (m_arguments.StartGame != null)
                    m_arguments.StartGame.Play = false;

                ChangeRegionConfirmProgressBar.Visibility = Visibility.Collapsed;
                ChangeRegionConfirmBtn.IsEnabled          = true;
                ChangeRegionConfirmBtnNoWarning.IsEnabled = true;
                ChangeRegionBtn.IsEnabled                 = true;

                DisableKbShortcuts();
            }

            async void AfterLoadRoutine(CancellationToken token)
            {
                try
                {
                    LogWriteLine($"Game: {regionToChangeName} has been completely initialized!", LogType.Scheme, true);
                    await FinalizeLoadRegion(gameName, gameRegion);
                    ChangeBackgroundImageAsRegionAsync();
                    IsLoadRegionComplete = true;

                    LoadingMessageHelper.HideActionButton();
                    LoadingMessageHelper.HideLoadingFrame();
                }
                catch (Exception ex)
                {
                    OnErrorRoutineInner(ex, ErrorType.Unhandled);
                }
            }

            void ActionOnTimeOutRetry(int retryAttemptCount, int retryAttemptTotal, int timeOutSecond, int timeOutStep)
            {
                LoadingMessageHelper.SetMessage(Lang._MainPage.RegionLoadingTitle,
                                                string.Format($"[{retryAttemptCount} / {retryAttemptTotal}] " + Lang._MainPage.RegionLoadingSubtitleTimeOut,
                                                              regionToChangeName,
                                                              timeOutSecond));
                LoadingMessageHelper.ShowActionButton(Lang._Misc.Cancel, "", CancelLoadEvent);
            }

            async void BeforeLoadRoutine(CancellationToken token)
            {
                try
                {
                    LogWriteLine($"Initializing game: {regionToChangeName}...", LogType.Scheme, true);

                    ClearMainPageState();
                    DisableKbShortcuts(1000);
                    await Task.Delay(TimeSpan.FromSeconds(1), token);
                    if (preset.GameLauncherApi.IsLoadingCompleted || token.IsCancellationRequested) return;

                    LoadingMessageHelper.SetMessage(Lang._MainPage.RegionLoadingTitle, regionToChangeName);
                    LoadingMessageHelper.SetProgressBarState(isProgressIndeterminate: true);
                    LoadingMessageHelper.ShowLoadingFrame();

                    IsLoadRegionComplete = false;
                }
                catch (Exception ex)
                {
                    OnErrorRoutineInner(ex, ErrorType.Unhandled);
                }
            }
        }

        public void ClearMainPageState()
        {
            // Clear NavigationViewControl Items and Reset Region props
            LastMenuNavigationItem = [..NavigationViewControl.MenuItems];
            LastFooterNavigationItem = [..NavigationViewControl.FooterMenuItems];
            NavigationViewControl.MenuItems.Clear();
            NavigationViewControl.FooterMenuItems.Clear();
            NavigationViewControl.IsSettingsVisible = false;
            PreviousTag = "launcher";
            PreviousTagString.Clear();
            PreviousTagString.Add(PreviousTag);

            // Clear cache on navigation reset
            LauncherFrame.BackStack.Clear();
            int cacheSizeOld = LauncherFrame.CacheSize;
            LauncherFrame.CacheSize = 0;
            LauncherFrame.CacheSize = cacheSizeOld;
        }

        private async Task DownloadBackgroundImage(CancellationToken Token)
        {
            // Get and set the current path of the image
            string backgroundFolder = Path.Combine(AppGameImgFolder, "bg");
            string backgroundFileName = Path.GetFileName(LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImg);
            LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal =  Path.Combine(backgroundFolder, backgroundFileName);
            SetAndSaveConfigValue("CurrentBackground", LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal);

            // Check if the background folder exist
            if (!Directory.Exists(backgroundFolder))
                Directory.CreateDirectory(backgroundFolder);

            var imgFileInfo =
                new FileInfo(LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImgLocal);

            // Start downloading the background image
            var isDownloaded = await ImageLoaderHelper.IsFileCompletelyDownloadedAsync(imgFileInfo, true);

            if (isDownloaded)
            {
                BackgroundImgChanger.ChangeBackground(imgFileInfo.FullName, () =>
                                                                 {
                                                                     IsFirstStartup = false;
                                                                     ColorPaletteUtility.ReloadPageTheme(this, CurrentAppTheme);
                                                                 }, false, false, true);
                return;
            }
            
        #nullable enable
            string? tempImage = null;
            var lastBgCfg = "lastBg-" + LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameName +
                               "-" + LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameRegion;
            
            // Check if the last background image exist, then use that temporarily instead
            var lastGameBackground = GetAppConfigValue(lastBgCfg).ToString();
            if (!string.IsNullOrEmpty(lastGameBackground))
            {
                if (File.Exists(lastGameBackground))
                {
                    tempImage = lastGameBackground;
                }
            }
            
            // If the file is not downloaded, use template image first, then download the image
            GameNameType? currentGameType = GamePropertyVault.GetCurrentGameProperty().GameVersion.GameType;
            tempImage ??= currentGameType switch
            {
                GameNameType.Honkai => Path.Combine(AppExecutableDir,   @"Assets\Images\GameBackground\honkai.webp"),
                GameNameType.Genshin => Path.Combine(AppExecutableDir,  @"Assets\Images\GameBackground\genshin.webp"),
                GameNameType.StarRail => Path.Combine(AppExecutableDir, @"Assets\Images\GameBackground\starrail.webp"),
                GameNameType.Zenless => Path.Combine(AppExecutableDir,  @"Assets\Images\GameBackground\zzz.webp"),
                _ => AppDefaultBG
            };
            BackgroundImgChanger.ChangeBackground(tempImage, () =>
                                                             {
                                                                 IsFirstStartup = false;
                                                                 ColorPaletteUtility.ReloadPageTheme(this, CurrentAppTheme);
                                                             }, false, false, true);
            if (await ImageLoaderHelper.TryDownloadToCompletenessAsync(LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.GameBackgroundImg,
                                                                       LauncherMetadataHelper.CurrentMetadataConfig.GameLauncherApi.ApiResourceHttpClient,
                                                                       imgFileInfo,
                                                                       Token))
            {
                BackgroundImgChanger.ChangeBackground(imgFileInfo.FullName, () =>
                {
                    IsFirstStartup = false;
                    ColorPaletteUtility.ReloadPageTheme(this, CurrentAppTheme);
                }, false, true, true);
                SetAndSaveConfigValue(lastBgCfg, imgFileInfo.FullName);
            }
        #nullable disable
        }

        private async ValueTask FinalizeLoadRegion(string gameName, string gameRegion)
        {
            PresetConfig preset = LauncherMetadataHelper.LauncherMetadataConfig[gameName][gameRegion];

            // Log if region has been successfully loaded
            LogWriteLine($"Initializing Region {preset.ZoneFullname} Done!", LogType.Scheme, true);

            // Initializing Game Statics
            await LoadGameStaticsByGameType(preset, gameName, gameRegion);

            // Init NavigationPanel Items
            InitializeNavigationItems();
        }

        private async ValueTask LoadGameStaticsByGameType(PresetConfig preset, string gameName, string gameRegion)
        {
            await GamePropertyVault.AttachNotificationForCurrentGame();
            DisposeAllPageStatics();

            GamePropertyVault.LoadGameProperty(this, preset.GameLauncherApi.LauncherGameResource, gameName, gameRegion);

            // Spawn Region Notification
            SpawnRegionNotification(preset.ProfileName);
            GamePropertyVault.DetachNotificationForCurrentGame();
        }

        private void DisposeAllPageStatics()
        {
            // CurrentGameProperty._GameInstall?.CancelRoutine();
            CurrentGameProperty?.GameRepair?.CancelRoutine();
            CurrentGameProperty?.GameRepair?.Dispose();
            CurrentGameProperty?.GameCache?.CancelRoutine();
            CurrentGameProperty?.GameCache?.Dispose();
#if DEBUG
            LogWriteLine("Page statics have been disposed!", LogType.Debug, true);
#endif
        }

        private async void SpawnRegionNotification(string RegionProfileName)
        {
            try
            {
                // Wait until the notification is ready
                while (!IsLoadNotifComplete)
                {
                    await Task.Delay(250);
                }

                if (NotificationData.RegionPush == null) return;

                foreach (NotificationProp Entry in NotificationData.RegionPush)
                {
                    NotificationInvokerProp toEntry = new NotificationInvokerProp
                    {
                        CloseAction = null,
                        IsAppNotif = false,
                        Notification = Entry,
                        OtherContent = null
                    };

                    if (Entry.ActionProperty != null)
                    {
                        toEntry.OtherContent = Entry.ActionProperty.GetFrameworkElement();
                    }

                    GameVersion? ValidForVerBelow = Entry.ValidForVerBelow != null ? new GameVersion(Entry.ValidForVerBelow) : null;
                    GameVersion? ValidForVerAbove = Entry.ValidForVerAbove != null ? new GameVersion(Entry.ValidForVerAbove) : null;

                    if (Entry.RegionProfile == RegionProfileName && IsNotificationTimestampValid(Entry) && (Entry.ValidForVerBelow == null
                            || (LauncherUpdateHelper.LauncherCurrentVersion.Compare(ValidForVerBelow)
                                && ValidForVerAbove.Compare(LauncherUpdateHelper.LauncherCurrentVersion))
                            || LauncherUpdateHelper.LauncherCurrentVersion.Compare(ValidForVerBelow)))
                    {
                        NotificationSender.SendNotification(toEntry);
                    }
                    await Task.Delay(250);
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while sending notification to the UI\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }

        private static bool IsNotificationTimestampValid(NotificationProp Entry)
        {
            long nowDateTime = DateTime.Now.ToLocalTime().ToFileTime();
            long? beginDateTime = Entry.TimeBegin?.ToLocalTime().ToFileTime() ?? 0;
            long? endDateTime = Entry.TimeEnd?.ToLocalTime().ToFileTime() ?? 0;

            bool isBeginValid = !Entry.TimeBegin.HasValue || beginDateTime < nowDateTime;
            bool isEndValid   = !Entry.TimeEnd.HasValue || endDateTime > nowDateTime;

            return isBeginValid && isEndValid;
        }

        private async void ChangeRegionNoWarning(object sender, RoutedEventArgs e)
        {
            try
            {
                (sender as Button).IsEnabled = false;
                CurrentGameCategory          = ComboBoxGameCategory.SelectedIndex;
                CurrentGameRegion            = ComboBoxGameRegion.SelectedIndex;
                await LoadRegionRootButton();
                InvokeLoadingRegionPopup(false);
                MainFrameChanger.ChangeMainFrame(m_appMode == AppMode.Hi3CacheUpdater ? typeof(CachesPage) : typeof(HomePage));
                LauncherFrame.BackStack.Clear();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while changing region with no warning\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }

        private async void ChangeRegionInstant()
        {
            try
            {
                CurrentGameCategory = ComboBoxGameCategory.SelectedIndex;
                CurrentGameRegion   = ComboBoxGameRegion.SelectedIndex;
                await LoadRegionRootButton();
                InvokeLoadingRegionPopup(false);
                MainFrameChanger.ChangeMainFrame(m_appMode == AppMode.Hi3CacheUpdater ? typeof(CachesPage) : typeof(HomePage));
                LauncherFrame.BackStack.Clear();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while changing region with instant method\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }

        private async void ChangeRegion(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable ChangeRegionBtn and hide flyout
                ToggleChangeRegionBtn(sender, true);
                if (!await LoadRegionRootButton())
                {
                    return;
                }

                // Finalize loading
                ToggleChangeRegionBtn(sender, false);
                CurrentGameCategory = ComboBoxGameCategory.SelectedIndex;
                CurrentGameRegion   = ComboBoxGameRegion.SelectedIndex;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while changing region with normal method\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }

        private async Task<bool> LoadRegionRootButton()
        {
            string GameCategory = GetComboBoxGameRegionValue(ComboBoxGameCategory.SelectedValue);
            string GameRegion = GetComboBoxGameRegionValue(ComboBoxGameRegion.SelectedValue);

            // Set and Save CurrentRegion in AppConfig
            SetAndSaveConfigValue("GameCategory", GameCategory);
            LauncherMetadataHelper.SetPreviousGameRegion(GameCategory, GameRegion);

            // Load Game ConfigV2 List before loading the region
            IsLoadRegionComplete = false;
            PresetConfig Preset = await LauncherMetadataHelper.GetMetadataConfig(GameCategory, GameRegion);

            // Start region loading
            ShowAsyncLoadingTimedOutPill();
            if (!await LoadRegionFromCurrentConfigV2(Preset, GameCategory, GameRegion))
            {
                return false;
            }

            LogWriteLine($"Region changed to {Preset.ZoneFullname}", LogType.Scheme, true);
        #if !DISABLEDISCORD
            if (GetAppConfigValue("EnableDiscordRPC").ToBool())
                AppDiscordPresence.SetupPresence();
        #endif
            return true;

        }

        private void ToggleChangeRegionBtn(in object sender, bool IsHide)
        {
            if (IsHide)
            {
                // Hide element
                ChangeRegionConfirmBtn.Flyout.Hide();
                ChangeRegionConfirmProgressBar.Visibility = Visibility.Visible;
            }
            else
            {
                // Show element
                ChangeRegionConfirmBtn.IsEnabled = false;
                ChangeRegionConfirmProgressBar.Visibility = Visibility.Collapsed;
                InvokeLoadingRegionPopup(false);
                MainFrameChanger.ChangeMainFrame(m_appMode == AppMode.Hi3CacheUpdater ? typeof(CachesPage) : typeof(HomePage));
                LauncherFrame.BackStack.Clear();
            }

            (sender as Button).IsEnabled = !IsHide;
        }

        private async void ShowAsyncLoadingTimedOutPill()
        {
            try
            {
                await Task.Delay(1000);
                if (!IsLoadRegionComplete)
                {
                    InvokeLoadingRegionPopup(true, Lang._MainPage.RegionLoadingTitle, RegionToChangeName);
                    // MainFrameChanger.ChangeMainFrame(typeof(BlankPage));
                    while (!IsLoadRegionComplete) { await Task.Delay(1000); }
                }
                InvokeLoadingRegionPopup(false);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while trying to show Timed-out Loading Pill\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }

        private static void InvokeLoadingRegionPopup(bool ShowLoadingMessage = true, string Title = null, string Message = null)
        {
            if (ShowLoadingMessage)
            {
                LoadingMessageHelper.SetMessage(Title, Message);
                LoadingMessageHelper.SetProgressBarState(isProgressIndeterminate: true);
                LoadingMessageHelper.ShowLoadingFrame();
                return;
            }

            LoadingMessageHelper.HideLoadingFrame();
        }
    }
}
