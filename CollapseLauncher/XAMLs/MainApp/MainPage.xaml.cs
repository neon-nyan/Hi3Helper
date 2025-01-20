using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.DiscordPresence;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Background;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.Update;
using CollapseLauncher.Interfaces;
using CollapseLauncher.Pages;
using CollapseLauncher.Pages.OOBE;
using CommunityToolkit.WinUI;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Win32.ToastCOM.Notification;
using InnoSetupHelper;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using static CollapseLauncher.Dialogs.KeyboardShortcuts;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.Statics.GamePropertyVault;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using UIElementExtensions = CollapseLauncher.Extension.UIElementExtensions;
// ReSharper disable CheckNamespace
// ReSharper disable RedundantExtendsListEntry
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable IdentifierTypo
// ReSharper disable AsyncVoidMethod
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault

namespace CollapseLauncher
{
    using KeybindAction = TypedEventHandler<KeyboardAccelerator, KeyboardAcceleratorInvokedEventArgs>;

    public partial class MainPage : Page
    {
        #region Properties
        private bool LockRegionChangeBtn;
        private bool DisableInstantRegionChange;
        private bool IsTitleIconForceShow;
        private bool IsNotificationPanelShow;
        private bool IsLoadNotifComplete;
        private bool IsLoadFrameCompleted = true;
        private bool IsFirstStartup       = true;
        private int  CurrentGameCategory  = -1;
        private int  CurrentGameRegion    = -1;

        internal static List<string> PreviousTagString       = [];

#nullable enable
        internal static BackgroundMediaUtility? CurrentBackgroundHandler;
        private         BackgroundMediaUtility? _localBackgroundHandler;
#nullable restore
        #endregion

        #region Main Routine
        public MainPage()
        {
            try
            {
                LogWriteLine($"Welcome to Collapse Launcher v{LauncherUpdateHelper.LauncherCurrentVersionString} - {MainEntryPoint.GetVersionString()}");
                LogWriteLine($"Application Data Location:\r\n\t{AppDataFolder}");
                InitializeComponent();
                m_mainPage                             =  this;
                ToggleNotificationPanelBtn.Translation += Shadow16;
                WebView2Frame.Navigate(typeof(BlankPage));
                Loaded += StartRoutine;

                // Enable implicit animation on certain elements
                AnimationHelper.EnableImplicitAnimation(true, null, GridBG_RegionGrid, GridBG_NotifBtn, NotificationPanelClearAllGrid);
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeEvents();
#if !DISABLEDISCORD
            AppDiscordPresence?.Dispose();
#endif
            ImageLoaderHelper.DestroyWaifu2X();
            _localBackgroundHandler?.Dispose();
            CurrentBackgroundHandler = null;
            _localBackgroundHandler = null;
        }

        private async void StartRoutine(object sender, RoutedEventArgs e)
        {
            SubscribeEvents();
            try
            {
                if (!IsShowRegionChangeWarning && IsInstantRegionChange)
                {
                    ChangeGameBtnGrid.Visibility = Visibility.Collapsed;
                    ChangeGameBtnGridShadow.Visibility = Visibility.Collapsed;
                    ChangeRegionConfirmBtn.Visibility = Visibility.Collapsed;
                    ChangeRegionConfirmBtnNoWarning.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ChangeRegionConfirmBtn.Visibility = !IsShowRegionChangeWarning ? Visibility.Collapsed : Visibility.Visible;
                    ChangeRegionConfirmBtnNoWarning.Visibility = !IsShowRegionChangeWarning ? Visibility.Visible : Visibility.Collapsed;
                }
                
                if (!await CheckForAdminAccess(this))
                {
                    if (WindowUtility.CurrentWindow is MainWindow mainWindow)
                        mainWindow.CloseApp();
                    return;
                }

                LoadGamePreset();
                SetThemeParameters();

                VersionNumberIndicator.Text = LauncherUpdateHelper.LauncherCurrentVersionString;
                #if DEBUG
                VersionNumberIndicator.Text += "d";
                #endif
                if (IsPreview) VersionNumberIndicator.Text += "-PRE";

                if (WindowUtility.CurrentWindow is MainWindow)
                    m_actualMainFrameSize = new Size((float)WindowUtility.CurrentWindow.Bounds.Width, (float)WindowUtility.CurrentWindow.Bounds.Height);

                ChangeTitleDragArea.Change(DragAreaTemplate.Default);

                await InitializeStartup();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async Task InitializeStartup()
        {
            // Initialize the background image utility
            await InitBackgroundHandler();

            Type Page = typeof(HomePage);

            bool isCacheUpdaterMode = m_appMode == AppMode.Hi3CacheUpdater;
            await LauncherMetadataHelper.Initialize(isCacheUpdaterMode);

            if (!isCacheUpdaterMode) SetActivatedRegion();

#if !DISABLEDISCORD
            bool isInitialStart = GetAppConfigValue("EnableDiscordRPC").ToBool();
            AppDiscordPresence = new DiscordPresenceManager(isInitialStart);
            AppDiscordPresence.SetActivity(ActivityType.Idle);
#endif

            // Lock ChangeBtn for first start
            LockRegionChangeBtn = true;

            (PresetConfig presetConfig, string gameName, string gameRegion) = await LoadSavedGameSelection();
            if (m_appMode == AppMode.Hi3CacheUpdater)
                Page = m_appMode == AppMode.Hi3CacheUpdater && presetConfig.GameType == GameNameType.Honkai ? typeof(CachesPage) : typeof(NotInstalledPage);

            InitKeyboardShortcuts();

            InvokeLoadingRegionPopup(true, Lang._MainPage.RegionLoadingTitle, RegionToChangeName);
            if (await LoadRegionFromCurrentConfigV2(presetConfig, gameName, gameRegion))
            {
                MainFrameChanger.ChangeMainFrame(Page);
            }

            // Unlock ChangeBtn for first start
            LockRegionChangeBtn = false;
            InvokeLoadingRegionPopup(false);

            // After all activities were complete, run background check, including
            // invoking notifications
            RunBackgroundCheck();
        }

        private async Task InitBackgroundHandler()
        {
            CurrentBackgroundHandler ??= await BackgroundMediaUtility.CreateInstanceAsync(this, BackgroundAcrylicMask, BackgroundOverlayTitleBar, BackgroundNewBackGrid, BackgroundNewMediaPlayerGrid);
            _localBackgroundHandler = CurrentBackgroundHandler;
        }
        #endregion

        #region Invokers
        private void UpdateBindingsEvent(object sender, EventArgs e)
        {
            // Find the last selected category/title and region
            string lastName = LauncherMetadataHelper.CurrentMetadataConfigGameName;
            string lastRegion = LauncherMetadataHelper.CurrentMetadataConfigGameRegion;

#nullable enable
            NavigationViewControl?.ApplyNavigationViewItemLocaleTextBindings();

            List<string>? gameNameCollection = LauncherMetadataHelper.GetGameNameCollection()!;
            List<string>? gameRegionCollection = LauncherMetadataHelper.GetGameRegionCollection(lastName!)!;

            int indexOfName = gameNameCollection.IndexOf(lastName!);
            int indexOfRegion = gameRegionCollection.IndexOf(lastRegion!);
#nullable restore
                
            // Rebuild Game Titles and Regions ComboBox items
            ComboBoxGameCategory.ItemsSource = BuildGameTitleListUI();
            ComboBoxGameCategory.SelectedIndex = indexOfName;
            ComboBoxGameRegion.SelectedIndex = indexOfRegion;

            ChangeTitleDragArea.Change(DragAreaTemplate.Default);

            UpdateLayout();
            Bindings.Update();
        }

        private static void ShowLoadingPageInvoker_PageEvent(object sender, ShowLoadingPageProperty e)
        {
            BackgroundImgChanger.ToggleBackground(e.Hide);
            InvokeLoadingRegionPopup(!e.Hide, e.Title, e.Subtitle);
        }

        private void SpawnWebView2Invoker_SpawnEvent(object sender, SpawnWebView2Property e)
        {
            if (e.URL == null)
            {
                WebView2Frame.Navigate(typeof(BlankPage));
                return;
            }

            SpawnWebView2Panel(new Uri(e.URL));
        }

        private async void ErrorSenderInvoker_ExceptionEvent(object sender, ErrorProperties e)
        {
            if (e.Exception.GetType() == typeof(NotImplementedException))
            {
                PreviousTag = "unavailable";
                if (!DispatcherQueue?.HasThreadAccess ?? false)
                    DispatcherQueue?.TryEnqueue(() => MainFrameChanger.ChangeMainFrame(typeof(UnavailablePage)));
                else
                    MainFrameChanger.ChangeMainFrame(typeof(UnavailablePage));

            }
            // CRC error show
            else if (e.Exception.GetType() == typeof(IOException) && e.Exception.HResult == unchecked((int)0x80070017))
            {
                PreviousTag = "crashinfo";
                ErrorSender.ExceptionType = ErrorType.DiskCrc;
                await SimpleDialogs.Dialog_ShowUnhandledExceptionMenu(this);
            }
            else
            {
                PreviousTag = "crashinfo";
                await SimpleDialogs.Dialog_ShowUnhandledExceptionMenu(this);
                // MainFrameChanger.ChangeMainFrame(typeof(UnhandledExceptionPage));
            }
        }

        private void MainFrameChangerInvoker_FrameEvent(object sender, MainFrameProperties e)
        {
            IsLoadFrameCompleted  = false;
            m_appCurrentFrameName = e.FrameTo.Name;
            LauncherFrame.Navigate(e.FrameTo, null, e.Transition);
            IsLoadFrameCompleted = true;
        }

        private void MainFrameChangerInvoker_FrameGoBackEvent(object sender, EventArgs e)
        {
            if (LauncherFrame.CanGoBack)
                LauncherFrame.GoBack();
        }
        #endregion

        #region Drag Area
        private RectInt32[] DragAreaMode_Normal
        {
            get
            {
                double scaleFactor = WindowUtility.CurrentWindowMonitorScaleFactor;
                RectInt32[] rect =
                [
                    new((int)(TitleBarDrag1.ActualOffset.X * scaleFactor),
                        0,
                        (int)(TitleBarDrag1.ActualWidth * scaleFactor),
                        (int)(48 * scaleFactor)),
                    new((int)(TitleBarDrag2.ActualOffset.X * scaleFactor),
                        0,
                        (int)(TitleBarDrag2.ActualWidth * scaleFactor),
                        (int)(48 * scaleFactor))
                ];
                return rect;
            }
        }

        private static RectInt32[] DragAreaMode_Full
        {
            get
            {
                Rect currentWindowPos = WindowUtility.CurrentWindowPosition;
                double scaleFactor = WindowUtility.CurrentWindowMonitorScaleFactor;

                RectInt32[] rect =
                [
                    new(0,
                        0,
                        (int)((currentWindowPos.Width - 96) * scaleFactor),
                        (int)(48 * scaleFactor))
                ];
                return rect;
            }
        }

        private static RectInt32 GetElementPos(FrameworkElement element)
        {
            GeneralTransform transformTransform = element.TransformToVisual(null);
            Rect bounds = transformTransform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            double scaleFactor = WindowUtility.CurrentWindowMonitorScaleFactor;

            return new RectInt32(
                _X: (int)Math.Round(bounds.X * scaleFactor),
                _Y: (int)Math.Round(bounds.Y * scaleFactor),
                _Width: (int)Math.Round(bounds.Width * scaleFactor),
                _Height: (int)Math.Round(bounds.Height * scaleFactor)
            );
        }

        private static void GridBG_RegionGrid_SizeChanged(object sender, SizeChangedEventArgs e) => ChangeTitleDragArea.Change(DragAreaTemplate.Default);

        private static void MainPageGrid_SizeChanged(object sender, SizeChangedEventArgs e) => ChangeTitleDragArea.Change(DragAreaTemplate.Default);

        private void ChangeTitleDragAreaInvoker_TitleBarEvent(object sender, ChangeTitleDragAreaProperty e)
        {
            UpdateLayout();

            InputNonClientPointerSource nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(WindowUtility.CurrentWindowId ?? throw new NullReferenceException());
            WindowUtility.EnableWindowNonClientArea();
            WindowUtility.SetWindowTitlebarDragArea(DragAreaMode_Full);

            switch (e.Template)
            {
                case DragAreaTemplate.None:
                    nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, [
                        GetElementPos((WindowUtility.CurrentWindow as MainWindow)?.AppTitleBar)
                    ]);
                    break;
                case DragAreaTemplate.Full:
                    nonClientInputSrc.ClearRegionRects(NonClientRegionKind.Passthrough);
                    break;
                case DragAreaTemplate.Default:
                    nonClientInputSrc.ClearAllRegionRects();
                    nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, [
                        GetElementPos(GridBG_RegionGrid),
                        GetElementPos(GridBG_IconGrid),
                        GetElementPos(GridBG_NotifBtn),
                        GetElementPos((WindowUtility.CurrentWindow as MainWindow)?.MinimizeButton),
                        GetElementPos((WindowUtility.CurrentWindow as MainWindow)?.CloseButton)
                    ]);
                    break;
            }

            nonClientInputSrc.SetRegionRects(NonClientRegionKind.Close, null);
            nonClientInputSrc.SetRegionRects(NonClientRegionKind.Minimize, null);
        }
        #endregion

        #region Admin Checks
        public static async Task<bool> CheckForAdminAccess(UIElement root)
        {
            if (!IsPrincipalHasNoAdministratorAccess()) return true;

            ContentDialogCollapse dialog = new ContentDialogCollapse(ContentDialogTheme.Warning)
            {
                Title = Lang._Dialogs.PrivilegeMustRunTitle,
                Content = Lang._Dialogs.PrivilegeMustRunSubtitle,
                PrimaryButtonText = Lang._Misc.Yes,
                CloseButtonText = Lang._Misc.Close,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = root.XamlRoot
            };

            while (true)
            {
                switch (await dialog.QueueAndSpawnDialog())
                {
                    case ContentDialogResult.Primary:
                        try
                        {
                            Process proc = new()
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    UseShellExecute = true,
                                    Verb = "runas",
                                    FileName = AppExecutablePath,
                                    WorkingDirectory = AppExecutableDir,
                                    Arguments = string.Join(' ', AppCurrentArgument)
                                }
                            };
                            proc.Start();
                            return false;
                        }
                        catch (Exception ex)
                        {
                            await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                            LogWriteLine($"Restarting the launcher can't be completed! {ex}", LogType.Error, true);
                        }
                        break;
                    default:
                        return false;
                }
            }
        }

        private static bool IsPrincipalHasNoAdministratorAccess()
        {
            using WindowsIdentity identity  = WindowsIdentity.GetCurrent();
            WindowsPrincipal      principal = new WindowsPrincipal(identity);
            return !principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        #endregion

        #region Theme Methods
        public void SetThemeParameters()
        {
            if (m_windowSupportCustomTitle)
            {
                return;
            }

            GridBG_RegionMargin.Width              = new GridLength(0, GridUnitType.Pixel);
            GridBG_RegionGrid.HorizontalAlignment  = HorizontalAlignment.Left;
            GridBG_RegionInner.HorizontalAlignment = HorizontalAlignment.Left;
        }
        #endregion

        #region Background Image
        private static void BackgroundImg_IsImageHideEvent(object sender, bool e)
        {
            if (e) CurrentBackgroundHandler?.Dimm();
            else CurrentBackgroundHandler?.Undimm();
        }

        private readonly HashSet<string> _processingBackground = [];
        private async void CustomBackgroundChanger_Event(object sender, BackgroundImgProperty e)
        {
            if (_processingBackground.Contains(e.ImgPath))
            {
                LogWriteLine($"Background {e.ImgPath} is already being processed!", LogType.Warning, true);
                return;
            }

            try
            {
                _processingBackground.Add(e.ImgPath);
                var gameLauncherApi = LauncherMetadataHelper.CurrentMetadataConfig?.GameLauncherApi;
                if (gameLauncherApi == null)
                {
                    return;
                }

                gameLauncherApi.GameBackgroundImgLocal = e.ImgPath;
                IsCustomBG                             = e.IsCustom;

                // if (e.IsCustom)
                //     SetAndSaveConfigValue("CustomBGPath",
                //                           gameLauncherApi.GameBackgroundImgLocal);

                if (!File.Exists(gameLauncherApi.GameBackgroundImgLocal))
                {
                    LogWriteLine($"Custom background file {e.ImgPath} is missing!", LogType.Warning, true);
                    gameLauncherApi.GameBackgroundImgLocal = AppDefaultBG;
                }

                var mType = BackgroundMediaUtility.GetMediaType(gameLauncherApi.GameBackgroundImgLocal);
                switch (mType)
                {
                    case BackgroundMediaUtility.MediaType.Media:
                        BackgroundNewMediaPlayerGrid.Visibility = Visibility.Visible;
                        BackgroundNewBackGrid.Visibility        = Visibility.Collapsed;
                        break;
                    case BackgroundMediaUtility.MediaType.StillImage:
                        FileStream imgStream =
                            await ImageLoaderHelper.LoadImage(gameLauncherApi.GameBackgroundImgLocal);
                        BackgroundMediaUtility.SetAlternativeFileStream(imgStream);
                        BackgroundNewMediaPlayerGrid.Visibility = Visibility.Collapsed;
                        BackgroundNewBackGrid.Visibility        = Visibility.Visible;
                        break;
                    case BackgroundMediaUtility.MediaType.Unknown:
                    default:
                        throw new InvalidCastException();
                }

                await InitBackgroundHandler();
                CurrentBackgroundHandler?.LoadBackground(gameLauncherApi.GameBackgroundImgLocal, e.IsRequestInit,
                                                         e.IsForceRecreateCache, ex =>
                                                             {
                                                                 gameLauncherApi.GameBackgroundImgLocal =
                                                                     AppDefaultBG;
                                                                 LogWriteLine($"An error occured while loading background {e.ImgPath}\r\n{ex}",
                                                                              LogType.Error, true);
                                                                 ErrorSender.SendException(ex);
                                                             }, e.ActionAfterLoaded);
            }
            catch (Exception ex)
            {
                LogWriteLine($"An error occured while loading background {e.ImgPath}\r\n{ex}",
                             LogType.Error, true);
                ErrorSender.SendException(new Exception($"An error occured while loading background {e.ImgPath}", ex));
            }
            finally
            {
                _processingBackground.Remove(e.ImgPath);
            }
        }

        internal async void ChangeBackgroundImageAsRegionAsync(bool ShowLoadingMsg = false)
        {
            var gameLauncherApi = LauncherMetadataHelper.CurrentMetadataConfig?.GameLauncherApi;
            if (gameLauncherApi == null)
            {
                return;
            }

            GamePresetProperty currentGameProperty = GetCurrentGameProperty();
            bool isUseCustomPerRegionBg = currentGameProperty.GameSettings?.SettingsCollapseMisc?.UseCustomRegionBG ?? false;

            IsCustomBG = GetAppConfigValue("UseCustomBG").ToBool();
            bool isAPIBackgroundAvailable =
                !string.IsNullOrEmpty(gameLauncherApi.GameBackgroundImg);

            var posterBg = currentGameProperty.GameVersion.GameType switch
                           {
                               GameNameType.Honkai => Path.Combine(AppExecutableDir,
                                                                   @"Assets\Images\GameBackground\honkai.webp"),
                               GameNameType.Genshin => Path.Combine(AppExecutableDir,
                                                                    @"Assets\Images\GameBackground\genshin.webp"),
                               GameNameType.StarRail => Path.Combine(AppExecutableDir,
                                                                     @"Assets\Images\GameBackground\starrail.webp"),
                               GameNameType.Zenless => Path.Combine(AppExecutableDir,
                                                                    @"Assets\Images\GameBackground\zzz.webp"),
                               _ => AppDefaultBG
                           };

            // Check if Regional Custom BG is enabled and available
            if (isUseCustomPerRegionBg)
            {
                var regionBgPath = currentGameProperty.GameSettings?.SettingsCollapseMisc?.CustomRegionBGPath;
                if (!string.IsNullOrEmpty(regionBgPath) && File.Exists(regionBgPath))
                {
                    if (BackgroundMediaUtility.GetMediaType(regionBgPath) == BackgroundMediaUtility.MediaType.StillImage)
                    {
                        FileStream imgStream = await ImageLoaderHelper.LoadImage(regionBgPath);
                        BackgroundMediaUtility.SetAlternativeFileStream(imgStream);
                    }
                    
                    gameLauncherApi.GameBackgroundImgLocal = regionBgPath;
                }
            }
            // If not, then check for global Custom BG
            else
            {
                var BGPath = IsCustomBG ? GetAppConfigValue("CustomBGPath").ToString() : null;
                if (!string.IsNullOrEmpty(BGPath))
                {
                    gameLauncherApi.GameBackgroundImgLocal = BGPath;
                }
                // If it's still not, then check if API gives any background
                else if (isAPIBackgroundAvailable)
                {
                    try
                    {
                        await DownloadBackgroundImage(default);
                        return; // Return after successfully loading
                    }
                    catch (Exception ex)
                    {
                        ErrorSender.SendException(ex);
                        LogWriteLine($"Failed while downloading default background image!\r\n{ex}", LogType.Error, true);
                        gameLauncherApi.GameBackgroundImgLocal = AppDefaultBG;
                    }
                }
                // IF ITS STILL NOT THERE, then use fallback game poster, IF ITS STILL NOT THEREEEE!! use paimon cute deadge pic :)
                else
                {
                    gameLauncherApi.GameBackgroundImgLocal = posterBg;
                }
            }
            
            // Use default background if the API background is empty (in-case HoYo did something catchy)
            if (!isAPIBackgroundAvailable && !IsCustomBG && LauncherMetadataHelper.CurrentMetadataConfig is { GameLauncherApi: not null })
                gameLauncherApi.GameBackgroundImgLocal ??= posterBg;
            
            // If the custom per region is enabled, then execute below
            BackgroundImgChanger.ChangeBackground(gameLauncherApi.GameBackgroundImgLocal,
                                                  () =>
                                                  {
                                                      IsFirstStartup = false;
                                                      ColorPaletteUtility.ReloadPageTheme(this, CurrentAppTheme);
                                                  },
                                                  IsCustomBG || isUseCustomPerRegionBg, true, true);
        }
        #endregion

        #region Events
        private void SubscribeEvents()
        {
            ErrorSenderInvoker.ExceptionEvent += ErrorSenderInvoker_ExceptionEvent;
            MainFrameChangerInvoker.FrameEvent += MainFrameChangerInvoker_FrameEvent;
            MainFrameChangerInvoker.FrameGoBackEvent += MainFrameChangerInvoker_FrameGoBackEvent;
            NotificationInvoker.EventInvoker += NotificationInvoker_EventInvoker;
            BackgroundImgChangerInvoker.ImgEvent += CustomBackgroundChanger_Event;
            BackgroundImgChangerInvoker.IsImageHide += BackgroundImg_IsImageHideEvent;
            SpawnWebView2Invoker.SpawnEvent += SpawnWebView2Invoker_SpawnEvent;
            ShowLoadingPageInvoker.PageEvent += ShowLoadingPageInvoker_PageEvent;
            ChangeTitleDragAreaInvoker.TitleBarEvent += ChangeTitleDragAreaInvoker_TitleBarEvent;
            SettingsPage.KeyboardShortcutsEvent += SettingsPage_KeyboardShortcutsEvent;
            KeyboardShortcutsEvent += SettingsPage_KeyboardShortcutsEvent;
            UpdateBindingsInvoker.UpdateEvents += UpdateBindingsEvent;
            GridBG_RegionGrid.SizeChanged += GridBG_RegionGrid_SizeChanged;
            MainPageGrid.SizeChanged += MainPageGrid_SizeChanged;
        }

        private void UnsubscribeEvents()
        {
            ErrorSenderInvoker.ExceptionEvent -= ErrorSenderInvoker_ExceptionEvent;
            MainFrameChangerInvoker.FrameEvent -= MainFrameChangerInvoker_FrameEvent;
            NotificationInvoker.EventInvoker -= NotificationInvoker_EventInvoker;
            BackgroundImgChangerInvoker.ImgEvent -= CustomBackgroundChanger_Event;
            BackgroundImgChangerInvoker.IsImageHide -= BackgroundImg_IsImageHideEvent;
            SpawnWebView2Invoker.SpawnEvent -= SpawnWebView2Invoker_SpawnEvent;
            ShowLoadingPageInvoker.PageEvent -= ShowLoadingPageInvoker_PageEvent;
            ChangeTitleDragAreaInvoker.TitleBarEvent -= ChangeTitleDragAreaInvoker_TitleBarEvent;
            SettingsPage.KeyboardShortcutsEvent -= SettingsPage_KeyboardShortcutsEvent;
            KeyboardShortcutsEvent -= SettingsPage_KeyboardShortcutsEvent;
            UpdateBindingsInvoker.UpdateEvents -= UpdateBindingsEvent;
            GridBG_RegionGrid.SizeChanged -= GridBG_RegionGrid_SizeChanged;
            MainPageGrid.SizeChanged -= MainPageGrid_SizeChanged;
        }
        #endregion

        #region Background Tasks
        private async void RunBackgroundCheck()
        {
            try
            {
                // Fetch the Notification Feed in CollapseLauncher-Repo
                await FetchNotificationFeed();

                // Generate local notification
                // For Example: Starter notification
                GenerateLocalAppNotification();

                // Spawn Updated App Notification if Applicable
                SpawnAppUpdatedNotification();

                // Load local settings
                // For example: Ignore list
                LoadLocalNotificationData();

                // Then Spawn the Notification Feed
                await SpawnPushAppNotification();

                // Check Metadata Update in Background
                if (await CheckMetadataUpdateInBackground())
                    return; // Cancel any routine below to avoid conflict with app update

#if !DEBUG
                // Run the update check and trigger routine
                await LauncherUpdateHelper.RunUpdateCheckDetached();
#else 
                LogWriteLine("Running debug build, stopping update checks!", LogType.Error);
#endif
            }
            catch (JsonException ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex);
                LogWriteLine($"Error while trying to get Notification Feed or Metadata Update\r\n{ex}", LogType.Error, true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while trying to run background tasks!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }
        #endregion

        #region Notification
        private void NotificationInvoker_EventInvoker(object sender, NotificationInvokerProp e)
        {
            if (e.IsCustomNotif)
            {
                if (e.CustomNotifAction == NotificationCustomAction.Add)
                {
                    SpawnNotificationoUI(e.Notification.MsgId, e.OtherContent as InfoBar);
                }
                else
                {
                    RemoveNotificationUI(e.Notification.MsgId);
                }
                return;
            }

            SpawnNotificationPush(e.Notification.Title, e.Notification.Message, e.Notification.Severity,
                e.Notification.MsgId, e.Notification.IsClosable ?? true, e.Notification.IsDisposable ?? true, e.CloseAction,
                e.OtherContent, e.IsAppNotif, e.Notification.Show, e.Notification.IsForceShowNotificationPanel);
        }

        private async Task FetchNotificationFeed()
        {
            try
            {
                NotificationData = new NotificationPush();
                IsLoadNotifComplete = false;
                CancellationTokenSource TokenSource = new CancellationTokenSource();
                RunTimeoutCancel(TokenSource);

                await using BridgedNetworkStream networkStream = await FallbackCDNUtil.TryGetCDNFallbackStream(string.Format(AppNotifURLPrefix, IsPreview ? "preview" : "stable"), TokenSource.Token);
                NotificationData = await networkStream.DeserializeAsync(NotificationPushJsonContext.Default.NotificationPush, token: TokenSource.Token);
                IsLoadNotifComplete = true;

                NotificationData?.EliminatePushList();
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed to load notification push!\r\n{ex}", LogType.Warning, true);
            }
        }

        private void GenerateLocalAppNotification()
        {
            NotificationData?.AppPush.Add(new NotificationProp
            {
                Show = true,
                MsgId = 0,
                IsDisposable = false,
                Severity = NotifSeverity.Success,
                Title = Lang._AppNotification.NotifFirstWelcomeTitle,
                Message = string.Format(Lang._AppNotification.NotifFirstWelcomeSubtitle, Lang._AppNotification.NotifFirstWelcomeBtn),
                OtherUIElement = GenerateNotificationButtonStartProcess(
                                                                        "",
                                                                        "https://github.com/CollapseLauncher/Collapse/wiki",
                                                                        Lang._AppNotification.NotifFirstWelcomeBtn)
            });

            if (IsPreview)
            {
                NotificationData?.AppPush.Add(new NotificationProp
                {
                    Show = true,
                    MsgId = -1,
                    IsDisposable = true,
                    Severity = NotifSeverity.Informational,
                    Title = Lang._AppNotification.NotifPreviewBuildUsedTitle,
                    Message = string.Format(Lang._AppNotification.NotifPreviewBuildUsedSubtitle, Lang._AppNotification.NotifPreviewBuildUsedBtn),
                    OtherUIElement = GenerateNotificationButtonStartProcess(
                                                                            "",
                                                                            "https://github.com/CollapseLauncher/Collapse/issues",
                                                                            Lang._AppNotification.NotifPreviewBuildUsedBtn)
                });
            }

            if (!IsNotificationPanelShow && IsFirstInstall)
            {
                ForceShowNotificationPanel();
            }
        }

        private static Button GenerateNotificationButtonStartProcess(string IconGlyph, string PathOrURL, string Text, bool IsUseShellExecute = true)
        {
            return NotificationPush.GenerateNotificationButton(IconGlyph, Text, (_, _) =>
            {
                new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = IsUseShellExecute,
                        FileName = PathOrURL
                    }
                }.Start();
            });
        }

        private async void RunTimeoutCancel(CancellationTokenSource Token)
        {
            await Task.Delay(10000);
            if (IsLoadNotifComplete)
            {
                return;
            }

            LogWriteLine("Cancel to load notification push! > 10 seconds", LogType.Error, true);
            await Token.CancelAsync();
        }

        private async Task SpawnPushAppNotification()
        {
            if (NotificationData?.AppPush == null) return;
            foreach (NotificationProp Entry in NotificationData.AppPush.ToList())
            {
                // Check for Close Action for certain MsgIds
                TypedEventHandler<InfoBar, object> ClickCloseAction = Entry.MsgId switch
                                                                      {
                                                                          0 => (_, _) =>
                                                                               {
                                                                                   NotificationData?.AddIgnoredMsgIds(0);
                                                                                   SaveLocalNotificationData();
                                                                               },
                                                                          _ => null
                                                                      };

                GameVersion? ValidForVerBelow = Entry.ValidForVerBelow != null ? new GameVersion(Entry.ValidForVerBelow) : null;
                GameVersion? ValidForVerAbove = Entry.ValidForVerAbove != null ? new GameVersion(Entry.ValidForVerAbove) : null;

                if (Entry.ValidForVerBelow == null && IsNotificationTimestampValid(Entry)
                    || (LauncherUpdateHelper.LauncherCurrentVersion.Compare(ValidForVerBelow)
                        && ValidForVerAbove.Compare(LauncherUpdateHelper.LauncherCurrentVersion))
                    || LauncherUpdateHelper.LauncherCurrentVersion.Compare(ValidForVerBelow))
                {
                    if (Entry.ActionProperty != null)
                    {
                        Entry.OtherUIElement = Entry.ActionProperty.GetFrameworkElement();
                    }

                    SpawnNotificationPush(Entry.Title, Entry.Message, Entry.Severity, Entry.MsgId, Entry.IsClosable ?? true,
                        Entry.IsDisposable ?? true, ClickCloseAction, (FrameworkElement)Entry.OtherUIElement, true, Entry.Show, Entry.IsForceShowNotificationPanel);
                }
                await Task.Delay(250);
            }
        }

        private void SpawnAppUpdatedNotification()
        {
            try
            {
                string UpdateNotifFile = Path.Combine(AppDataFolder, "_NewVer");
                string NeedInnoUpdateFile = Path.Combine(AppDataFolder, "_NeedInnoLogUpdate");

                void ClickClose(InfoBar infoBar, object o)
                {
                    File.Delete(UpdateNotifFile);
                }

                // If the update was handled by squirrel and it needs Inno Setup Log file to get updated, then do the routine
                if (File.Exists(NeedInnoUpdateFile))
                {
                    try
                    {
                        string InnoLogPath = Path.Combine(Path.GetDirectoryName(AppExecutableDir) ?? string.Empty, "unins000.dat");
                        if (File.Exists(InnoLogPath)) InnoSetupLogUpdate.UpdateInnoSetupLog(InnoLogPath);
                        File.Delete(NeedInnoUpdateFile);
                    }
                    catch (Exception ex)
                    {
                        SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                        LogWriteLine($"Something wrong while opening the \"unins000.dat\" or deleting the \"_NeedInnoLogUpdate\" file\r\n{ex}", LogType.Error, true);
                    }
                }

                if (!File.Exists(UpdateNotifFile))
                {
                    return;
                }

                string VerString = File.ReadAllLines(UpdateNotifFile)[0];
                GameVersion Version = new GameVersion(VerString);
                SpawnNotificationPush(
                                      Lang._Misc.UpdateCompleteTitle,
                                      string.Format(Lang._Misc.UpdateCompleteSubtitle, Version.VersionString, IsPreview ? "Preview" : "Stable"),
                                      NotifSeverity.Success,
                                      0xAF,
                                      true,
                                      false,
                                      ClickClose,
                                      null,
                                      true,
                                      true,
                                      true
                                     );

                string fold = Path.Combine(AppExecutableDir, "_Temp");
                if (Directory.Exists(fold))
                {
                    foreach (string file in Directory.EnumerateFiles(fold))
                    {
                        if (!Path.GetFileNameWithoutExtension(file).Contains("ApplyUpdate"))
                        {
                            continue;
                        }

                        var target = Path.Combine(AppExecutableDir, Path.GetFileName(file));
                        File.Move(file, target, true);
                    }

                    Directory.Delete(fold, true);
                }

                try
                {
                    // Remove update notif mark file to avoid it showing the same notification again.
                    File.Delete(UpdateNotifFile);

                    // Get current game property, including game preset
                    GamePresetProperty currentGameProperty = GetCurrentGameProperty();
                    (_, string heroImage) = OOBESelectGame.GetLogoAndHeroImgPath(currentGameProperty.GamePreset);

                    // Create notification
                    NotificationContent toastContent = NotificationContent.Create()
                                                                          .SetTitle(Lang._NotificationToast
                                                                                       .LauncherUpdated_NotifTitle)
                                                                          .SetContent(
                                                                                string
                                                                                   .Format(Lang._NotificationToast.LauncherUpdated_NotifSubtitle,
                                                                                             VerString + (IsPreview
                                                                                                 ? "-preview"
                                                                                                 : ""),
                                                                                             Lang._SettingsPage
                                                                                                .PageTitle,
                                                                                             Lang._SettingsPage
                                                                                                .Update_SeeChangelog)
                                                                               )
                                                                          .AddAppHeroImagePath(heroImage);

                    // Get notification service
                    Windows.UI.Notifications.ToastNotification notificationService =
                        WindowUtility.CurrentToastNotificationService?.CreateToastNotification(toastContent);

                    // Spawn notification service
                    Windows.UI.Notifications.ToastNotifier notifier =
                        WindowUtility.CurrentToastNotificationService?.CreateToastNotifier();
                    notifier?.Show(notificationService);
                }
                catch (Exception ex)
                {
                    LogWriteLine($"[SpawnAppUpdatedNotification] Failed to spawn toast notification!\r\n{ex}",
                                 LogType.Error, true);
                    SentryHelper.ExceptionHandler(ex);
                }
            }
            catch
            {
                // ignored
            }
        }

        private static InfoBarSeverity NotifSeverity2InfoBarSeverity(NotifSeverity inp)
        {
            return inp switch
                   {
                       NotifSeverity.Success => InfoBarSeverity.Success,
                       NotifSeverity.Warning => InfoBarSeverity.Warning,
                       NotifSeverity.Error => InfoBarSeverity.Error,
                       _ => InfoBarSeverity.Informational
                   };
        }

        private void SpawnNotificationPush(string Title, string TextContent, NotifSeverity Severity, int MsgId = 0, bool IsClosable = true,
            bool Disposable = false, TypedEventHandler<InfoBar, object> CloseClickHandler = null, FrameworkElement OtherContent = null, bool IsAppNotif = true,
            bool? Show = false, bool ForceShowNotificationPanel = false)
        {
            if (!(Show ?? false)) return;
            if (NotificationData?.CurrentShowMsgIds.Contains(MsgId) ?? false) return;

            if (NotificationData?.IsMsgIdIgnored(MsgId) ?? false) return;

            NotificationData?.CurrentShowMsgIds.Add(MsgId);

            DispatcherQueue?.TryEnqueue(() =>
            {
                StackPanel OtherContentContainer = UIElementExtensions.CreateStackPanel().WithMargin(0d, -4d, 0d, 8d);

                InfoBar Notification = new InfoBar
                {
                    Title         = Title,
                    Message       = TextContent,
                    Severity      = NotifSeverity2InfoBarSeverity(Severity),
                    IsClosable    = IsClosable,
                    IsIconVisible = true,
                    Shadow        = SharedShadow,
                    IsOpen        = true
                }
                .WithMargin(4d, 4d, 4d, 0d).WithWidth(600)
                .WithCornerRadius(8).WithHorizontalAlignment(HorizontalAlignment.Right);

                Notification.Translation += Shadow32;

                if (OtherContent != null)
                    OtherContentContainer.AddElementToStackPanel(OtherContent);

                if (Disposable)
                {
                    CheckBox NeverAskNotif = new CheckBox
                    {
                        Content = new TextBlock { Text = Lang._MainPage.NotifNeverAsk, FontWeight = FontWeights.Medium },
                        Tag = $"{MsgId},{IsAppNotif}"
                    };
                    NeverAskNotif.Checked += NeverAskNotif_Checked;
                    NeverAskNotif.Unchecked += NeverAskNotif_Unchecked;
                    OtherContentContainer.AddElementToStackPanel(NeverAskNotif);
                }

                if (Disposable || OtherContent != null)
                    Notification.Content = OtherContentContainer;

                Notification.Tag = MsgId;
                Notification.CloseButtonClick += CloseClickHandler;

                SpawnNotificationoUI(MsgId, Notification);

                if (ForceShowNotificationPanel && !IsNotificationPanelShow)
                {
                    this.ForceShowNotificationPanel();
                }
            });
        }

        private void SpawnNotificationoUI(int tagID, InfoBar Notification)
        {
            Grid Container = UIElementExtensions.CreateGrid().WithTag(tagID);
            Notification.Loaded += (_, _) =>
            {
                NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;
                NewNotificationCountBadge.Visibility = Visibility.Visible;
                NewNotificationCountBadge.Value++;

                NotificationPanelClearAllGrid.Visibility = NotificationContainer.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            };

            Notification.Closed += (s, _) =>
            {
                s.Translation -= Shadow32;
                s.SetHeight(0d);
                s.SetMargin(0d);
                int msg = (int)s.Tag;

                if (NotificationData?.CurrentShowMsgIds.Contains(msg) ?? false)
                {
                    NotificationData?.CurrentShowMsgIds.Remove(msg);
                }
                NotificationContainer.Children.Remove(Container);
                NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;

                if (NewNotificationCountBadge.Value > 0)
                {
                    NewNotificationCountBadge.Value--;
                }
                NoNotificationIndicator.Opacity = NotificationContainer.Children.Count > 0 ? 0f : 1f;
                NewNotificationCountBadge.Visibility = NewNotificationCountBadge.Value > 0 ? Visibility.Visible : Visibility.Collapsed;
                NotificationPanelClearAllGrid.Visibility = NotificationContainer.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            };

            Container.AddElementToGridRowColumn(Notification);
            NotificationContainer.AddElementToStackPanel(Container);
        }

        private void RemoveNotificationUI(int tagID)
        {
            Grid notif = NotificationContainer.Children.OfType<Grid>().FirstOrDefault(x => (int)x.Tag == tagID);
            if (notif != null)
            {
                NotificationContainer.Children.Remove(notif);
                InfoBar notifBar = notif.Children.OfType<InfoBar>().FirstOrDefault();
                if (notifBar != null && notifBar.IsClosable)
                    notifBar.IsOpen = false;
            }
        }

        private async void ClearAllNotification(object sender, RoutedEventArgs args)
        {
            Button button = sender as Button;
            if (button != null) button.IsEnabled = false;

            int stackIndex = 0;
            for (; stackIndex < NotificationContainer.Children.Count;)
            {
                if (NotificationContainer.Children[stackIndex] is not Grid container
                    || container.Children == null || container.Children.Count == 0
                    || container.Children[0] is not InfoBar { IsClosable: true } notifBar)
                {
                    ++stackIndex;
                    continue;
                }

                NotificationContainer.Children.RemoveAt(stackIndex);
                notifBar.IsOpen = false;
                await Task.Delay(100);
            }

            if (NotificationContainer.Children.Count == 0)
            {
                await Task.Delay(500);
                ToggleNotificationPanelBtn.IsChecked = false;
                IsNotificationPanelShow = false;
                ShowHideNotificationPanel();
            }

            if (button != null) button.IsEnabled = true;
        }

        private static void NeverAskNotif_Checked(object sender, RoutedEventArgs e)
        {
            string[] Data = (sender as CheckBox)?.Tag.ToString()?.Split(',');
            if (Data == null)
            {
                return;
            }

            NotificationData?.AddIgnoredMsgIds(int.Parse(Data[0]), bool.Parse(Data[1]));
            SaveLocalNotificationData();
        }

        private static void NeverAskNotif_Unchecked(object sender, RoutedEventArgs e)
        {
            string[] Data = (sender as CheckBox)?.Tag.ToString()?.Split(',');
            if (Data == null)
            {
                return;
            }

            NotificationData?.RemoveIgnoredMsgIds(int.Parse(Data[0]), bool.Parse(Data[1]));
            SaveLocalNotificationData();
        }

        private async void ForceShowNotificationPanel()
        {
            ToggleNotificationPanelBtn.IsChecked = true;
            IsNotificationPanelShow              = true;
            ShowHideNotificationPanel();
            await Task.Delay(250);
            double currentVOffset = NotificationContainer.ActualHeight;

            NotificationPanelScrollViewer.ScrollToVerticalOffset(currentVOffset);
        }
        #endregion

        #region Game Selector Method
        private async ValueTask<(PresetConfig, string, string)> LoadSavedGameSelection()
        {
            ComboBoxGameCategory.ItemsSource = BuildGameTitleListUI();

            string gameName = GetAppConfigValue("GameCategory")!;

            #nullable enable
            List<string>? gameCollection   = LauncherMetadataHelper.GetGameNameCollection();
            List<string?>? regionCollection = LauncherMetadataHelper.GetGameRegionCollection(gameName);
            
            if (regionCollection == null)
                gameName = LauncherMetadataHelper.LauncherGameNameRegionCollection?.Keys.FirstOrDefault();

            ComboBoxGameRegion.ItemsSource = BuildGameRegionListUI(gameName);

            var indexCategory                    = gameCollection?.IndexOf(gameName!) ?? -1;
            if (indexCategory < 0) indexCategory = 0;

            var indexRegion = LauncherMetadataHelper.GetPreviousGameRegion(gameName);

            ComboBoxGameCategory.SelectedIndex = indexCategory;
            ComboBoxGameRegion.SelectedIndex   = indexRegion;
            CurrentGameCategory                = ComboBoxGameCategory.SelectedIndex;
            CurrentGameRegion                  = ComboBoxGameRegion.SelectedIndex;

            string? gameNameLookup = GetComboBoxGameRegionValue(ComboBoxGameCategory.SelectedValue);
            string? gameRegionLookup = GetComboBoxGameRegionValue(ComboBoxGameRegion.SelectedValue);

            return (await LauncherMetadataHelper.GetMetadataConfig(gameNameLookup, gameRegionLookup),
                                                                  gameNameLookup,
                                                                  gameRegionLookup);
        }
        
        private void SetGameCategoryChange(object sender, SelectionChangedEventArgs e)
        {
            object? selectedItem = ((ComboBox)sender).SelectedItem;
            if (selectedItem == null) return;
            string? selectedCategoryString = GetComboBoxGameRegionValue(selectedItem);
            // REMOVED: GetConfigV2Regions(SelectedCategoryString);
            
            ComboBoxGameRegion.ItemsSource   = BuildGameRegionListUI(selectedCategoryString);
            ComboBoxGameRegion.SelectedIndex = GetIndexOfRegionStringOrDefault(selectedCategoryString);
        }
        #nullable disable

        private async void EnableRegionChangeButton(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxGameCategory.SelectedIndex == CurrentGameCategory && ComboBoxGameRegion.SelectedIndex == CurrentGameRegion)
            {
                ChangeRegionConfirmBtn.IsEnabled          = false;
                ChangeRegionConfirmBtnNoWarning.IsEnabled = false;
                return;
            }

            object selValue = ((ComboBox)sender).SelectedValue;
            if (selValue == null) return;

            string category = GetComboBoxGameRegionValue(ComboBoxGameCategory.SelectedValue);
            string region = GetComboBoxGameRegionValue(selValue);
            PresetConfig preset = await LauncherMetadataHelper.GetMetadataConfig(category, region);
            
            ChangeRegionWarningText.Text = preset!.Channel != GameChannel.Stable
                ? string.Format(Lang._MainPage.RegionChangeWarnExper1, preset.Channel)
                : string.Empty;
            ChangeRegionWarning.Visibility =
                preset.Channel != GameChannel.Stable ? Visibility.Visible : Visibility.Collapsed;
            
            ChangeRegionConfirmBtn.IsEnabled          = !LockRegionChangeBtn;
            ChangeRegionConfirmBtnNoWarning.IsEnabled = !LockRegionChangeBtn;

            if (!IsShowRegionChangeWarning && IsInstantRegionChange && !DisableInstantRegionChange && !IsFirstStartup)
                ChangeRegionInstant();
        }

    #pragma warning disable CA1822
        private void GameComboBox_OnDropDownOpened(object sender, object e)
    #pragma warning restore CA1822
        {
            ChangeTitleDragArea.Change(DragAreaTemplate.None);
        }

    #pragma warning disable CA1822
        private void GameComboBox_OnDropDownClosed(object sender, object e)
    #pragma warning restore CA1822
        {
            ChangeTitleDragArea.Change(DragAreaTemplate.Default);
        }
        #endregion

        #region Metadata Update Method
        private async ValueTask<bool> CheckMetadataUpdateInBackground()
        {
            bool IsUpdate = await LauncherMetadataHelper.IsMetadataHasUpdate();
            if (!IsUpdate)
            {
                return false;
            }

            Button UpdateMetadatabtn =
                UIElementExtensions.CreateButtonWithIcon<Button>(
                                                                 Lang._AppNotification!.NotifMetadataUpdateBtn,
                                                                 "",
                                                                 "FontAwesomeSolid",
                                                                 "AccentButtonStyle"
                                                                )
                                   .WithMargin(0d, 0d, 0d, 16d);

            UpdateMetadatabtn.Loaded += async (a, _) =>
                                        {
                                            TextBlock Text = new TextBlock
                                            {
                                                Text       = Lang._AppNotification.NotifMetadataUpdateBtnUpdating,
                                                FontWeight = FontWeights.Medium
                                            }.WithVerticalAlignment(VerticalAlignment.Center);
                                            ProgressRing LoadBar = new ProgressRing
                                            {
                                                IsIndeterminate = true,
                                                Visibility      = Visibility.Collapsed
                                            }.WithWidthAndHeight(16d).WithMargin(0d, 0d, 8d, 0d).WithVerticalAlignment(VerticalAlignment.Center);
                                            StackPanel StackPane = UIElementExtensions.CreateStackPanel(Orientation.Horizontal);
                                            StackPane.AddElementToStackPanel(LoadBar);
                                            StackPane.AddElementToStackPanel(Text);
                                            Button aButton = a as Button;
                                            if (aButton != null)
                                            {
                                                aButton.Content   = StackPane;
                                                aButton.IsEnabled = false;
                                            }

                                            // Put 2 seconds delay before updating
                                            int i = 2;
                                            while (i != 0)
                                            {
                                                Text.Text = string.Format(Lang._AppNotification.NotifMetadataUpdateBtnCountdown, i);
                                                await Task.Delay(1000);
                                                i--;
                                            }

                                            LoadBar.Visibility = Visibility.Visible;
                                            Text.Text          = Lang._AppNotification.NotifMetadataUpdateBtnUpdating;

                                            try
                                            {
                                                await LauncherMetadataHelper.RunMetadataUpdate();
                                                MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
                                            }
                                            catch (Exception ex)
                                            {
                                                LogWriteLine($"Error has occured while updating metadata!\r\n{ex}", LogType.Error, true);
                                                ErrorSender.SendException(ex);
                                            }
                                        };
            SpawnNotificationPush(
                                  Lang._AppNotification.NotifMetadataUpdateTitle,
                                  Lang._AppNotification.NotifMetadataUpdateSubtitle,
                                  NotifSeverity.Informational,
                                  -886135731,
                                  true,
                                  false,
                                  null,
                                  UpdateMetadatabtn,
                                  true,
                                  true,
                                  true
                                 );
            return true;
        }
        #endregion

        #region Navigation
        private void InitializeNavigationItems(bool ResetSelection = true)
        {
            NavigationViewControl.IsSettingsVisible = true;
            NavigationViewControl.MenuItems.Clear();
            NavigationViewControl.FooterMenuItems.Clear();

            IGameVersionCheck CurrentGameVersionCheck = GetCurrentGameProperty().GameVersion;

            FontIcon IconLauncher = new FontIcon { Glyph = "" };
            FontIcon IconRepair = new FontIcon { Glyph = "" };
            FontIcon IconCaches = new FontIcon { Glyph = m_isWindows11 ? "" : "" };
            FontIcon IconGameSettings = new FontIcon { Glyph = "" };
            FontIcon IconAppSettings = new FontIcon { Glyph = "" };

            if (m_appMode == AppMode.Hi3CacheUpdater)
            {
                if (CurrentGameVersionCheck.GamePreset.IsCacheUpdateEnabled ?? false)
                {
                    NavigationViewControl.MenuItems.Add(new NavigationViewItem
                                                                { Icon = IconCaches, Tag = "caches" }
                    .BindNavigationViewItemText("_CachesPage", "PageTitle"));
                }
                return;
            }

            NavigationViewControl.MenuItems.Add(new NavigationViewItem
                                                        { Icon = IconLauncher, Tag = "launcher" }
            .BindNavigationViewItemText("_HomePage", "PageTitle"));

            NavigationViewControl.MenuItems.Add(new NavigationViewItemHeader()
                .BindNavigationViewItemText("_MainPage", "NavigationUtilities"));

            if (CurrentGameVersionCheck.GamePreset.IsRepairEnabled ?? false)
            {
                NavigationViewControl.MenuItems.Add(new NavigationViewItem
                                                            { Icon = IconRepair, Tag = "repair" }
                .BindNavigationViewItemText("_GameRepairPage", "PageTitle"));
            }

            if (CurrentGameVersionCheck.GamePreset.IsCacheUpdateEnabled ?? false)
            {
                NavigationViewControl.MenuItems.Add(new NavigationViewItem
                                                            { Icon = IconCaches, Tag = "caches" }
                .BindNavigationViewItemText("_CachesPage", "PageTitle"));
            }

            switch (CurrentGameVersionCheck.GameType)
            {
                case GameNameType.Honkai:
                    NavigationViewControl.FooterMenuItems.Add(new NavigationViewItem
                                                                      { Icon = IconGameSettings, Tag = "honkaigamesettings" }
                    .BindNavigationViewItemText("_GameSettingsPage", "PageTitle"));
                    break;
                case GameNameType.StarRail:
                    NavigationViewControl.FooterMenuItems.Add(new NavigationViewItem
                                                                      { Icon = IconGameSettings, Tag = "starrailgamesettings" }
                    .BindNavigationViewItemText("_StarRailGameSettingsPage", "PageTitle"));
                    break;
                case GameNameType.Genshin:
                    NavigationViewControl.FooterMenuItems.Add(new NavigationViewItem
                                                                      { Icon = IconGameSettings, Tag = "genshingamesettings" }
                    .BindNavigationViewItemText("_GenshinGameSettingsPage", "PageTitle"));
                    break;
                case GameNameType.Zenless:
                    NavigationViewControl.FooterMenuItems.Add(new NavigationViewItem
                                                                      { Icon = IconGameSettings, Tag = "zenlessgamesettings" }
                    .BindNavigationViewItemText("_GameSettingsPage", "PageTitle"));
                    break;
            }

            if (NavigationViewControl.SettingsItem is NavigationViewItem SettingsItem)
            {
                SettingsItem.Icon = IconAppSettings;
                _ = SettingsItem.BindNavigationViewItemText("_SettingsPage", "PageTitle");
            }

            foreach (var dependency in NavigationViewControl.FindDescendants().OfType<FrameworkElement>())
            {
                // Avoid any icons to have shadow attached if it's not from this page
                if (dependency.BaseUri.AbsolutePath != BaseUri.AbsolutePath)
                {
                    continue;
                }

                switch (dependency)
                {
                    case FontIcon icon:
                        AttachShadowNavigationPanelItem(icon);
                        break;
                    case AnimatedIcon animIcon:
                        AttachShadowNavigationPanelItem(animIcon);
                        break;
                }
            }
            AttachShadowNavigationPanelItem(IconAppSettings);

            if (ResetSelection)
            {
                NavigationViewControl.SelectedItem = (NavigationViewItem)NavigationViewControl.MenuItems[0];
            }

            NavigationViewControl.ApplyNavigationViewItemLocaleTextBindings();

            InputSystemCursor handCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
            MainPageGrid.SetAllControlsCursorRecursive(handCursor);
        }

        public static void AttachShadowNavigationPanelItem(FrameworkElement element)
        {
            bool isAppLight = IsAppThemeLight;
            Windows.UI.Color shadowColor = isAppLight ? Colors.White : Colors.Black;
            double shadowBlurRadius = isAppLight ? 20 : 15;
            double shadowOpacity = isAppLight ? 0.5 : 0.3;

            element.ApplyDropShadow(shadowColor, shadowBlurRadius, shadowOpacity);
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (NavigationViewItemBase item in NavigationViewControl.MenuItems)
            {
                if (item is not NavigationViewItem || item.Tag.ToString() != "launcher")
                {
                    continue;
                }

                NavigationViewControl.SelectedItem = item;
                break;
            }

            NavViewPaneBackground.OpacityTransition = new ScalarTransition
            {
                Duration = TimeSpan.FromMilliseconds(150)
            };
            NavViewPaneBackground.TranslationTransition = new Vector3Transition
            {
                Duration = TimeSpan.FromMilliseconds(150)
            };

            var paneMainGrid = NavigationViewControl.FindDescendant("PaneContentGrid");
            if (paneMainGrid is Grid paneMainGridAsGrid)
            {
                paneMainGridAsGrid.PointerEntered += NavView_PanePointerEntered;
                paneMainGridAsGrid.PointerExited  += NavView_PanePointerExited;
            }

            // The toggle button is not a part of pane. Why Microsoft!!!
            var paneToggleButtonGrid = (Grid)NavigationViewControl.FindDescendant("PaneToggleButtonGrid");
            if (paneToggleButtonGrid != null)
            {
                paneToggleButtonGrid.PointerEntered += NavView_PanePointerEntered;
                paneToggleButtonGrid.PointerExited  += NavView_PanePointerExited;
            }

            // var backIcon = NavigationViewControl.FindDescendant("NavigationViewBackButton")?.FindDescendant<AnimatedIcon>();
            // backIcon?.ApplyDropShadow(Colors.Gray, 20);

            var toggleIcon = NavigationViewControl.FindDescendant("TogglePaneButton")?.FindDescendant<AnimatedIcon>();
            toggleIcon?.ApplyDropShadow(Colors.Gray, 20);
        }

        private void NavView_PanePointerEntered(object sender, PointerRoutedEventArgs e)
        {
            IsCursorInNavBarHoverArea = true;
            NavViewPaneBackground.Opacity = 1;
            NavViewPaneBackground.Translation = new System.Numerics.Vector3(0, 0, 32);
            /*
            if (!NavigationViewControl.IsPaneOpen)
            {
                var duration = TimeSpan.FromSeconds(0.25);
                var current = (float)NavViewPaneBackground.Opacity;
                var animation = NavViewPaneBackground.GetElementCompositor()!
                                                     .CreateScalarKeyFrameAnimation("Opacity", 1, current);
                await NavViewPaneBackground.StartAnimation(duration, animation);
            }
            */
        }

        private bool IsCursorInNavBarHoverArea;

        private void NavView_PanePointerExited(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint pointerPoint = e.GetCurrentPoint(NavViewPaneBackgroundHoverArea);
            IsCursorInNavBarHoverArea = pointerPoint.Position.X <= NavViewPaneBackgroundHoverArea.Width - 8 && pointerPoint.Position.X > 4;

            switch (IsCursorInNavBarHoverArea)
            {
                case false when !NavigationViewControl.IsPaneOpen:
                    NavViewPaneBackground.Opacity     = 0;
                    NavViewPaneBackground.Translation = new System.Numerics.Vector3(-48, 0, 0);
                    break;
                case true when !NavigationViewControl.IsPaneOpen:
                    NavViewPaneBackground.Opacity     = 1;
                    NavViewPaneBackground.Translation = new System.Numerics.Vector3(0, 0, 32);
                    break;
            }

            /*
            var duration = TimeSpan.FromSeconds(0.25);
            var current = (float)NavViewPaneBackground.Opacity;
            var animation = NavViewPaneBackground.GetElementCompositor()!
                                                 .CreateScalarKeyFrameAnimation("Opacity", 0, current);
            await NavViewPaneBackground.StartAnimation(duration, animation);
            */
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (!IsLoadFrameCompleted) return;
            if (args.IsSettingsInvoked && PreviousTag != "settings") Navigate(typeof(SettingsPage), "settings");

#nullable enable
            NavigationViewItem? item = sender.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => (x.Content as TextBlock)?.Text == (args.InvokedItem as TextBlock)?.Text);
            item ??= sender.FooterMenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => (x.Content as TextBlock)?.Text == (args.InvokedItem as TextBlock)?.Text);
            if (item == null) return;
#nullable restore

            string itemTag = (string)item.Tag;

            NavigateInnerSwitch(itemTag);
        }

        private void NavigateInnerSwitch(string itemTag)
        {
            if (itemTag == PreviousTag) return;
            switch (itemTag)
            {
                case "launcher":
                    Navigate(typeof(HomePage), itemTag);
                    break;

                case "repair":
                    if (!(GetCurrentGameProperty().GameVersion.GamePreset.IsRepairEnabled ?? false))
                        Navigate(typeof(UnavailablePage), itemTag);
                    else
                        Navigate(IsGameInstalled() ? typeof(RepairPage) : typeof(NotInstalledPage), itemTag);
                    break;

                case "caches":
                    if (GetCurrentGameProperty().GameVersion.GamePreset.IsCacheUpdateEnabled ?? false)
                        Navigate(IsGameInstalled() || (m_appMode == AppMode.Hi3CacheUpdater && GetCurrentGameProperty().GameVersion.GamePreset.GameType == GameNameType.Honkai) ? typeof(CachesPage) : typeof(NotInstalledPage), itemTag);
                    else
                        Navigate(typeof(UnavailablePage), itemTag);
                    break;

                case "honkaigamesettings":
                    Navigate(IsGameInstalled() ? typeof(HonkaiGameSettingsPage) : typeof(NotInstalledPage), itemTag);
                    break;

                case "starrailgamesettings":
                    Navigate(IsGameInstalled() ? typeof(StarRailGameSettingsPage) : typeof(NotInstalledPage), itemTag);
                    break;

                case "genshingamesettings":
                    Navigate(IsGameInstalled() ? typeof(GenshinGameSettingsPage) : typeof(NotInstalledPage), itemTag);
                    break;
                
                case "zenlessgamesettings":
                    Navigate(IsGameInstalled() ? typeof(ZenlessGameSettingsPage) : typeof(NotInstalledPage), itemTag);
                    break;
            }
        }

        private void Navigate(Type sourceType, string tagStr)
        {
            MainFrameChanger.ChangeMainFrame(sourceType, new DrillInNavigationTransitionInfo());
            PreviousTag = tagStr;
            PreviousTagString.Add(tagStr);
            LogWriteLine($"Page changed to {sourceType.Name} with Tag: {tagStr}", LogType.Scheme);
        }

        internal void InvokeMainPageNavigateByTag(string tagStr)
        {
            NavigationViewItem item = NavigationViewControl.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => x.Tag is string tag && tag == tagStr);
            if (item == null)
            {
                return;
            }

            NavigationViewControl.SelectedItem = item;
            string tag = (string)item.Tag;
            NavigateInnerSwitch(tag);
        }

        private void ToggleNotificationPanelBtnClick(object sender, RoutedEventArgs e)
        {
            IsNotificationPanelShow = ToggleNotificationPanelBtn.IsChecked ?? false;
            ShowHideNotificationPanel();
        }

        private void ShowHideNotificationPanel()
        {
            NewNotificationCountBadge.Value = 0;
            NewNotificationCountBadge.Visibility = Visibility.Collapsed;
            Thickness lastMargin = NotificationPanel.Margin;
            lastMargin.Right = IsNotificationPanelShow ? 0 : NotificationPanel.ActualWidth * -1;
            NotificationPanel.Margin = lastMargin;

            ShowHideNotificationLostFocusBackground(IsNotificationPanelShow);
        }

        private async void ShowHideNotificationLostFocusBackground(bool show)
        {
            if (show)
            {
                NotificationLostFocusBackground.Visibility                =  Visibility.Visible;
                NotificationLostFocusBackground.Opacity                   =  0.3;
                NotificationPanel.Translation                             += Shadow48;
                ToggleNotificationPanelBtn.Translation                    -= Shadow16;
                ((FontIcon)ToggleNotificationPanelBtn.Content).FontFamily =  FontCollections.FontAwesomeSolid;
            }
            else
            {
                NotificationLostFocusBackground.Opacity                   =  0;
                NotificationPanel.Translation                             -= Shadow48;
                ToggleNotificationPanelBtn.Translation                    += Shadow16;
                ((FontIcon)ToggleNotificationPanelBtn.Content).FontFamily =  FontCollections.FontAwesomeRegular;
                await Task.Delay(200);
                NotificationLostFocusBackground.Visibility = Visibility.Collapsed;
            }
        }

        private void NotificationContainerBackground_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            IsNotificationPanelShow = false;
            ToggleNotificationPanelBtn.IsChecked = false;
            ShowHideNotificationPanel();
        }

        private void NavigationViewControl_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (!LauncherFrame.CanGoBack || !IsLoadFrameCompleted)
            {
                return;
            }

            LauncherFrame.GoBack();
            if (PreviousTagString.Count < 1) return;

            string lastPreviousTag          = PreviousTagString[^1];
            string currentNavigationItemTag = (string)((NavigationViewItem)sender.SelectedItem).Tag;

            if (!string.Equals(lastPreviousTag, currentNavigationItemTag, StringComparison.CurrentCultureIgnoreCase))
            {
                return;
            }

            string goLastPreviousTag = PreviousTagString[^2];

        #nullable enable
            NavigationViewItem? goPreviousNavigationItem = sender.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => goLastPreviousTag == (string)x.Tag);
            goPreviousNavigationItem ??= sender.FooterMenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => goLastPreviousTag == (string)x.Tag);
        #nullable restore

            if (goLastPreviousTag == "settings")
            {
                PreviousTag = goLastPreviousTag;
                PreviousTagString.RemoveAt(PreviousTagString.Count - 1);
                sender.SelectedItem = sender.SettingsItem;
                return;
            }

            if (goPreviousNavigationItem == null)
            {
                return;
            }

            PreviousTag = goLastPreviousTag;
            PreviousTagString.RemoveAt(PreviousTagString.Count - 1);
            sender.SelectedItem = goPreviousNavigationItem;
        }

        private void NavigationPanelOpening_Event(NavigationView sender, object args)
        {
            Thickness curMargin = GridBG_Icon.Margin;
            curMargin.Left       = 48;
            GridBG_Icon.Margin   = curMargin;
            IsTitleIconForceShow = true;
            ToggleTitleIcon(false);

            NavViewPaneBackgroundHoverArea.Width = NavigationViewControl.OpenPaneLength;
        }

        private async void NavigationPanelClosing_Event(NavigationView sender, NavigationViewPaneClosingEventArgs args)
        {
            Thickness curMargin = GridBG_Icon.Margin;
            curMargin.Left       = 58;
            GridBG_Icon.Margin   = curMargin;
            IsTitleIconForceShow = false;
            ToggleTitleIcon(true);

            NavViewPaneBackgroundHoverArea.Width = NavViewPaneBackground.Width;

            await Task.Delay(200);
            if (IsCursorInNavBarHoverArea)
            {
                return;
            }

            NavViewPaneBackground.Opacity     = 0;
            NavViewPaneBackground.Translation = new System.Numerics.Vector3(-48, 0, 0);
        }
        #endregion

        #region Icons
        private void ToggleTitleIcon(bool hide)
        {
            if (!hide)
            {
                GridBG_IconTitle.Width = double.NaN;
                GridBG_IconTitle.Opacity = 1d;
                GridBG_IconImg.Opacity = 1d;
                return;
            }

            GridBG_IconTitle.Width = 0d;
            GridBG_IconTitle.Opacity = 0d;
            GridBG_IconImg.Opacity = 0.8d;
        }

        private void GridBG_Icon_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (IsTitleIconForceShow)
            {
                return;
            }

            Thickness curMargin = GridBG_Icon.Margin;
            curMargin.Left     = 50;
            GridBG_Icon.Margin = curMargin;
            ToggleTitleIcon(false);
        }

        private void GridBG_Icon_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (IsTitleIconForceShow)
            {
                return;
            }

            Thickness curMargin = GridBG_Icon.Margin;
            curMargin.Left     = 58;
            GridBG_Icon.Margin = curMargin;
            ToggleTitleIcon(true);
        }

        private void GridBG_Icon_Click(object sender, RoutedEventArgs e)
        {
            if (PreviousTag.Equals("launcher", StringComparison.OrdinalIgnoreCase)) return;

            MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            PreviousTag = "launcher";
            PreviousTagString.Add(PreviousTag);

            NavigationViewItem navItem = NavigationViewControl.MenuItems
                                                              .OfType<NavigationViewItem>()
                                                              .FirstOrDefault(x => ((string)x.Tag).Equals(PreviousTag, StringComparison.OrdinalIgnoreCase));

            if (navItem != null)
            {
                NavigationViewControl.SelectedItem = navItem;
            }
        }
        #endregion

        #region Misc Methods
        private static bool IsGameInstalled() => GameInstallationState
            is GameInstallStateEnum.Installed
            or GameInstallStateEnum.InstalledHavePreload
            or GameInstallStateEnum.InstalledHavePlugin
            or GameInstallStateEnum.NeedsUpdate;

        private void SpawnWebView2Panel(Uri URL)
        {
            try
            {
                WebView2FramePage.WebView2URL = URL;
                WebView2Frame.Navigate(typeof(WebView2FramePage), null, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromBottom });
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex);
                LogWriteLine($"Error while initialize EdgeWebView2. Opening browser instead!\r\n{ex}", LogType.Error, true);
                new Process
                {
                    StartInfo = new ProcessStartInfo
                                {
                                    UseShellExecute = true,
                                    FileName        = URL.ToString()
                                }
                }.Start();
            }
        }
        #endregion

        #region Keyboard Shortcuts Methods
        private void InitKeyboardShortcuts()
        {
            if (GetAppConfigValue("EnableShortcuts").ToBoolNullable() == null)
            {
                SetAndSaveConfigValue("EnableShortcuts", true);
                KbShortcutList = null;

                SpawnNotificationPush(
                    Lang._AppNotification.NotifKbShortcutTitle,
                    Lang._AppNotification.NotifKbShortcutSubtitle,
                    NotifSeverity.Informational,
                    -20,
                    true,
                    false,
                    null,
                    NotificationPush.GenerateNotificationButton("", Lang._AppNotification.NotifKbShortcutBtn, (_, _) => ShowKeybinds_Invoked(null, null)),
                    true,
                    true,
                    true
                    );
            }

            if (AreShortcutsEnabled) CreateKeyboardShortcutHandlers();
        }

        private void CreateKeyboardShortcutHandlers()
        {
            try
            {
                if (KbShortcutList == null || KbShortcutList.Count == 0)
                    LoadKbShortcuts();

                int numIndex = 0;
                if (KbShortcutList != null)
                {
                    VirtualKeyModifiers keyModifier = KbShortcutList["GameSelection"].Modifier;
                    for (; numIndex <= LauncherMetadataHelper.CurrentGameNameCount; numIndex++)
                    {
                        KeyboardAccelerator keystroke = new KeyboardAccelerator
                        {
                            Modifiers = keyModifier,
                            Key       = VirtualKey.Number1 + numIndex
                        };
                        keystroke.Invoked += KeyboardGameShortcut_Invoked;
                        KeyboardHandler.KeyboardAccelerators.Add(keystroke);

                        KeyboardAccelerator keystrokeNP = new KeyboardAccelerator
                        {
                            Key = VirtualKey.NumberPad1 + numIndex
                        };
                        keystrokeNP.Invoked += KeyboardGameShortcut_Invoked;
                        KeyboardHandler.KeyboardAccelerators.Add(keystrokeNP);
                    }

                    numIndex    = 0;
                    keyModifier = KbShortcutList["RegionSelection"].Modifier;
                    while (numIndex < LauncherMetadataHelper.CurrentGameRegionMaxCount)
                    {
                        KeyboardAccelerator keystroke = new KeyboardAccelerator
                        {
                            Modifiers = keyModifier,
                            Key       = VirtualKey.Number1 + numIndex++
                        };
                        keystroke.Invoked += KeyboardGameRegionShortcut_Invoked;
                        KeyboardHandler.KeyboardAccelerators.Add(keystroke);
                    }
                }

                KeyboardAccelerator keystrokeF5 = new KeyboardAccelerator
                {
                    Key = VirtualKey.F5
                };
                keystrokeF5.Invoked += RefreshPage_Invoked;
                KeyboardHandler.KeyboardAccelerators.Add(keystrokeF5);

                Dictionary<string, KeybindAction> actions = new()
                {
                    // General
                    { "KbShortcutsMenu", ShowKeybinds_Invoked },
                    { "HomePage", GoHome_Invoked },
                    { "SettingsPage", GoSettings_Invoked },
                    { "NotificationPanel", OpenNotify_Invoked },

                    // Game Related
                    { "ScreenshotFolder", OpenScreenshot_Invoked},
                    { "GameFolder", OpenGameFolder_Invoked },
                    { "CacheFolder", OpenGameCacheFolder_Invoked },
                    { "ForceCloseGame", ForceCloseGame_Invoked },

                    { "RepairPage", GoGameRepair_Invoked },
                    { "GameSettingsPage", GoGameSettings_Invoked },
                    { "CachesPage", GoGameCaches_Invoked },

                    { "ReloadRegion", RefreshPage_Invoked }
                };

                foreach (KeyValuePair<string, KeybindAction> func in actions)
                {
                    if (KbShortcutList == null)
                    {
                        continue;
                    }

                    KeyboardAccelerator kbfunc = new KeyboardAccelerator
                    {
                        Modifiers = KbShortcutList[func.Key].Modifier,
                        Key       = KbShortcutList[func.Key].Key
                    };
                    kbfunc.Invoked += func.Value;
                    KeyboardHandler.KeyboardAccelerators.Add(kbfunc);
                }
            }
            catch (Exception error)
            {
                SentryHelper.ExceptionHandler(error);
                LogWriteLine(error.ToString());
                KbShortcutList = null;
                CreateKeyboardShortcutHandlers();
            }
        }

        private void RefreshPage_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (CannotUseKbShortcuts || !IsLoadRegionComplete)
                return;

            switch (PreviousTag)
            {
                case "launcher":
                    RestoreCurrentRegion();
                    ChangeRegionNoWarning(IsShowRegionChangeWarning ? ChangeRegionConfirmBtn : ChangeRegionConfirmBtnNoWarning, null);
                    return;
                case "settings":
                    return;
                default:
                    string itemTag = PreviousTag;
                    PreviousTag = "Empty";
                    NavigateInnerSwitch(itemTag);
                    if (LauncherFrame != null && LauncherFrame.BackStack is { Count: > 0 })
                        LauncherFrame.BackStack.RemoveAt(LauncherFrame.BackStack.Count - 1);
                    if (PreviousTagString is { Count: > 0 })
                        PreviousTagString.RemoveAt(PreviousTagString.Count - 1);
                    return;
            }
        }

        private void DeleteKeyboardShortcutHandlers() => KeyboardHandler.KeyboardAccelerators.Clear();

        private static async void DisableKbShortcuts(int time = 500)
        {
            try
            {
                CannotUseKbShortcuts = true;
                await Task.Delay(time);
                CannotUseKbShortcuts = false;
            }
            catch
            {
                // Ignore warnings
            }
        }

        private void RestoreCurrentRegion()
        {
            string gameName = GetAppConfigValue("GameCategory")!;
            #nullable enable
            List<string>? gameNameCollection = LauncherMetadataHelper.GetGameNameCollection();
            _ = LauncherMetadataHelper.GetGameRegionCollection(gameName);

            var indexCategory                    = gameNameCollection?.IndexOf(gameName) ?? -1;
            if (indexCategory < 0) indexCategory = 0;

            var indexRegion = LauncherMetadataHelper.GetPreviousGameRegion(gameName);

            ComboBoxGameCategory.SelectedIndex = indexCategory;
            ComboBoxGameRegion.SelectedIndex   = indexRegion;
        }

        private void KeyboardGameShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            int index = (int)sender.Key; index -= index < 96 ? 49 : 97;

            DisableInstantRegionChange = true;
            RestoreCurrentRegion();
            
            if (CannotUseKbShortcuts || !IsLoadRegionComplete
                                     || index >= ComboBoxGameCategory.Items.Count
                                     || ComboBoxGameCategory.SelectedValue == ComboBoxGameCategory.Items[index]
               )
            {
                DisableInstantRegionChange = false;
                return;
            }

            ComboBoxGameCategory.SelectedValue = ComboBoxGameCategory.Items[index];
            ComboBoxGameRegion.SelectedIndex = GetIndexOfRegionStringOrDefault(GetComboBoxGameRegionValue(ComboBoxGameCategory.SelectedValue));
            ChangeRegionNoWarning(ChangeRegionConfirmBtn, null);
            ChangeRegionConfirmBtn.IsEnabled          = false;
            ChangeRegionConfirmBtnNoWarning.IsEnabled = false;
            CannotUseKbShortcuts                      = true;
            DisableInstantRegionChange                = false;
        }

        private void KeyboardGameRegionShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            int index = (int)sender.Key; index -= index < 96 ? 49 : 97;

            DisableInstantRegionChange = true;
            RestoreCurrentRegion();
            

            if (CannotUseKbShortcuts || !IsLoadRegionComplete
                                     || index >= ComboBoxGameRegion.Items.Count 
                                     || ComboBoxGameRegion.SelectedValue == ComboBoxGameRegion.Items[index])
            {
                DisableInstantRegionChange = false;
                return;
            }

            ComboBoxGameRegion.SelectedValue = ComboBoxGameRegion.Items[index];
            ChangeRegionNoWarning(ChangeRegionConfirmBtn, null);
            ChangeRegionConfirmBtn.IsEnabled          = false;
            ChangeRegionConfirmBtnNoWarning.IsEnabled = false;
            CannotUseKbShortcuts                      = true;
            DisableInstantRegionChange                = false;
        }

        private async void ShowKeybinds_Invoked(KeyboardAccelerator? sender, KeyboardAcceleratorInvokedEventArgs? args)
        {
            if (CannotUseKbShortcuts) return;

            await Dialog_ShowKbShortcuts(this);
        }

        private void GoHome_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!IsLoadRegionComplete || CannotUseKbShortcuts) return;

            if (NavigationViewControl.SelectedItem == NavigationViewControl.MenuItems[0]) return;

            DisableKbShortcuts();
            NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems[0];
            NavigateInnerSwitch("launcher");

        }

        private void GoSettings_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!IsLoadRegionComplete || CannotUseKbShortcuts) return;

            if (NavigationViewControl.SelectedItem == NavigationViewControl.SettingsItem) return;

            DisableKbShortcuts();
            NavigationViewControl.SelectedItem = NavigationViewControl.SettingsItem;
            Navigate(typeof(SettingsPage), "settings");
        }

        private void OpenNotify_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ToggleNotificationPanelBtn.IsChecked = !ToggleNotificationPanelBtn.IsChecked;
            ToggleNotificationPanelBtnClick(null, null);
        }

        private string GameDirPath { get => CurrentGameProperty.GameVersion.GameDirPath ?? ""; }
        private void OpenScreenshot_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!IsGameInstalled()) return;

            string ScreenshotFolder = Path.Combine(NormalizePath(GameDirPath), CurrentGameProperty.GameVersion.GamePreset.GameType switch
            {
                GameNameType.StarRail => $"{Path.GetFileNameWithoutExtension(CurrentGameProperty.GameVersion.GamePreset.GameExecutableName)}_Data\\ScreenShots",
                _ => "ScreenShot"
            });

            LogWriteLine($"Opening Screenshot Folder:\r\n\t{ScreenshotFolder}");

            if (!Directory.Exists(ScreenshotFolder))
                Directory.CreateDirectory(ScreenshotFolder);

            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = "explorer.exe",
                    Arguments = ScreenshotFolder
                }
            }.Start();
        }

        private async void OpenGameFolder_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            try
            {
                if (!IsGameInstalled()) return;

                string GameFolder = NormalizePath(GameDirPath);
                LogWriteLine($"Opening Game Folder:\r\n\t{GameFolder}");
                await Task.Run(() =>
                    new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            UseShellExecute = true,
                            FileName = "explorer.exe",
                            Arguments = GameFolder
                        }
                    }.Start());
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed when trying to open game folder!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async void OpenGameCacheFolder_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            try
            {
                if (!IsGameInstalled()) return;

                string? GameFolder = CurrentGameProperty.GameVersion.GameDirAppDataPath;
                LogWriteLine($"Opening Game Folder:\r\n\t{GameFolder}");
                await Task.Run(() =>
                    new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            UseShellExecute = true,
                            FileName = "explorer.exe",
                            Arguments = GameFolder
                        }
                    }.Start());
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed when trying to open game cache folder!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private static void ForceCloseGame_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!GetCurrentGameProperty().IsGameRunning) return;

            PresetConfig? gamePreset = GetCurrentGameProperty().GameVersion.GamePreset;
            try
            {
                Process[] gameProcess = Process.GetProcessesByName(gamePreset?.GameExecutableName?.Split('.')[0]);
                foreach (var p in gameProcess)
                {
                    LogWriteLine($"Trying to stop game process {gamePreset?.GameExecutableName?.Split('.')[0]} at PID {p.Id}", LogType.Scheme, true);
                    p.Kill();
                }
            }
            catch (Win32Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"There is a problem while trying to stop Game with Region: {gamePreset?.ZoneName}\r\nTraceback: {ex}", LogType.Error, true);
            }
        }
        private void GoGameRepair_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!IsLoadRegionComplete || CannotUseKbShortcuts) return;

            if (NavigationViewControl.SelectedItem == NavigationViewControl.MenuItems[2]) return;

            DisableKbShortcuts();
            NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems[2];
            NavigateInnerSwitch("repair");
        }

        private void GoGameCaches_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!IsLoadRegionComplete || CannotUseKbShortcuts)
                return;
            if (NavigationViewControl.SelectedItem == NavigationViewControl.MenuItems[3])
                return;

            DisableKbShortcuts();
            NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems[3];
            NavigateInnerSwitch("caches");
        }

        private void GoGameSettings_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!IsLoadRegionComplete || CannotUseKbShortcuts)
                return;

            if (NavigationViewControl.SelectedItem == NavigationViewControl.FooterMenuItems.Last())
                return;

            DisableKbShortcuts();
            NavigationViewControl.SelectedItem = NavigationViewControl.FooterMenuItems.Last();
            switch (CurrentGameProperty.GamePreset)
            {
                case { GameType: GameNameType.Honkai }:
                    Navigate(typeof(HonkaiGameSettingsPage), "honkaigamesettings");
                    break;
                case { GameType: GameNameType.Genshin }:
                    Navigate(typeof(GenshinGameSettingsPage), "genshingamesettings");
                    break;
                case { GameType: GameNameType.StarRail }:
                    Navigate(typeof(StarRailGameSettingsPage), "starrailgamesettings");
                    break;
            }
        }

        private static bool AreShortcutsEnabled
        {
            get => GetAppConfigValue("EnableShortcuts").ToBool(true);
        }

        private void SettingsPage_KeyboardShortcutsEvent(object sender, int e)
        {
            switch (e)
            {
                case 0:
                    CreateKeyboardShortcutHandlers();
                    break;
                case 1:
                    DeleteKeyboardShortcutHandlers();
                    CreateKeyboardShortcutHandlers();
                    break;
                case 2:
                    DeleteKeyboardShortcutHandlers();
                    break;
            }
        }
        #endregion

        #region AppActivation
        private static bool SetActivatedRegion()
        {
            var args = m_arguments.StartGame;
            if (args == null) return true;

            string? oldGameCategory = GetAppConfigValue("GameCategory");

            string gameName = args.Game;

            List<string>? gameNameCollection = LauncherMetadataHelper.GetGameNameCollection()!;
            List<string>? gameRegionCollection = LauncherMetadataHelper.GetGameRegionCollection(gameName)!;
            if (gameRegionCollection == null)
            {
                bool res = int.TryParse(args.Game, out int gameIndex);
                if (!res || gameIndex < 0 || gameIndex >= gameNameCollection.Count) return true;

                gameName = gameNameCollection[gameIndex];
                gameRegionCollection = LauncherMetadataHelper.GetGameRegionCollection(gameName)!;
            }
            SetAndSaveConfigValue("GameCategory", gameName);

            if (args is not { Region: not null })
            {
                return false;
            }

            string gameRegion = args.Region;
            if (!gameRegionCollection.Contains(gameRegion))
            {
                bool res = int.TryParse(args.Region, out int regionIndex);
                if (!res || regionIndex < 0 || regionIndex >= gameRegionCollection.Count) return true;

                gameRegion = gameRegionCollection[regionIndex];
            }

            int oldGameRegionIndex = LauncherMetadataHelper.GetPreviousGameRegion(gameName);
            string oldGameRegion = gameRegionCollection.ElementAt(oldGameRegionIndex);

            LauncherMetadataHelper.SetPreviousGameRegion(gameName, gameRegion);
            SetAndSaveConfigValue("GameRegion", gameRegion);

            return oldGameCategory == gameName && oldGameRegion == gameRegion;
        }

        private async void ChangeToActivatedRegion()
        {
            if (!IsLoadRegionComplete || CannotUseKbShortcuts) return;

            bool sameRegion = SetActivatedRegion();

            DisableInstantRegionChange = true;
            LockRegionChangeBtn        = true;
            IsLoadRegionComplete       = false;

            (PresetConfig preset, string gameName, string gameRegion) = await LoadSavedGameSelection();

            ShowAsyncLoadingTimedOutPill();
            if (await LoadRegionFromCurrentConfigV2(preset, gameName, gameRegion))
            {
            #if !DISABLEDISCORD
                if (GetAppConfigValue("EnableDiscordRPC").ToBool() && !sameRegion)
                    AppDiscordPresence?.SetupPresence();
            #endif
                InvokeLoadingRegionPopup(false);
                LauncherFrame.BackStack.Clear();
                MainFrameChanger.ChangeMainFrame(m_appMode == AppMode.Hi3CacheUpdater? typeof(CachesPage) : typeof(HomePage));
                LogWriteLine($"Region changed to {preset.ZoneFullname}", LogType.Scheme, true);
            }

            LockRegionChangeBtn        = false;
            DisableInstantRegionChange = false;
        }

        public void OpenAppActivation()
        {
            if (m_arguments is { StartGame: null }) return;

            DispatcherQueue?.TryEnqueue(ChangeToActivatedRegion);
        }
        #endregion
    }
}
