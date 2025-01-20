#if !DISABLEDISCORD
using CollapseLauncher.DiscordPresence;
#endif
using CollapseLauncher.CustomControls;
using CollapseLauncher.Helper.LauncherApiLoader.Sophon;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.GamePlaytime;
using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Database;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using CollapseLauncher.ShortcutUtils;
using CollapseLauncher.Statics;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using H.NotifyIcon;
using Hi3Helper;
using Hi3Helper.EncTool.WindowTool;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Win32.FileDialogCOM;
using Hi3Helper.Win32.Native.ManagedTools;
using Hi3Helper.Win32.Screen;
using Microsoft.UI.Composition;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Win32;
using PhotoSauce.MagicScaler;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.Helper.Background.BackgroundMediaUtility;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using Brush = Microsoft.UI.Xaml.Media.Brush;
using Image = Microsoft.UI.Xaml.Controls.Image;
using Point = Windows.Foundation.Point;
using Size = System.Drawing.Size;
using UIElementExtensions = CollapseLauncher.Extension.UIElementExtensions;
// ReSharper disable InconsistentNaming
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable AsyncVoidMethod
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault

namespace CollapseLauncher.Pages
{
    public sealed partial class HomePage
    {
        #region Properties
        private GamePresetProperty CurrentGameProperty { get; set; }
        private CancellationTokenSourceWrapper PageToken { get; set; }
        private CancellationTokenSourceWrapper CarouselToken { get; set; }

        private Button _lastSocMedButton;

        private int barWidth;
        private int consoleWidth;

        public static int RefreshRateDefault => 500;
        public static int RefreshRateSlow    => 1000;

        /// <summary>
        /// Holds the value for how long a checks needs to be delayed before continuing the loop in milliseconds.
        /// Default : 200 (Please set it using RefreshRateDefault instead)
        /// </summary>
        public static int RefreshRate
        {
            get;
            set
            {
            #if DEBUG
                LogWriteLine($"HomePage Refresh Rate changed to {value}", LogType.Debug, true);
            #endif
                field = value;
            }
        }

        /// <summary>
        /// Hold cached state for IsGameRunning. The state is controlled inside CheckRunningGameInstance() method.
        /// </summary>
        private static bool _cachedIsGameRunning { get; set; }
        #endregion

        #region PageMethod
        public HomePage()
        {
            RefreshRate = RefreshRateDefault;
            Loaded += StartLoadedRoutine;

            m_homePage = this;
            InitializeConsoleValues();
        }

        ~HomePage()
        {
            // HACK: Fix random crash by always unsubscribing the StartLoadedRoutine if the GC is calling the deconstructor.
            Loaded -= StartLoadedRoutine;
        }

        private void InitializeConsoleValues()
        {
            consoleWidth = 24;
            try { consoleWidth = Console.BufferWidth; }
            catch
            {
                // ignored
            }

            barWidth = (consoleWidth - 22) / 2 - 1;
        }

        private bool IsPageUnload { get; set; }

        private static bool NeedShowEventIcon => GetAppConfigValue("ShowEventsPanel").ToBool();

        private void ReturnToHomePage()
        {
            var presetConfig = GamePropertyVault.GetCurrentGameProperty().GamePreset;
            if (IsPageUnload && presetConfig.HashID != CurrentGameProperty.GamePreset.HashID)
            {
                return;
            }

            MainPage.PreviousTagString.Add(MainPage.PreviousTag);
            MainFrameChanger.ChangeMainFrame(typeof(HomePage));
        }

        private async void StartLoadedRoutine(object sender, RoutedEventArgs e)
        {
            try
            {
                // HACK: Fix random crash by manually load the XAML part
                //       But first, let it initialize its properties.
                CurrentGameProperty = GamePropertyVault.GetCurrentGameProperty();
                PageToken = new CancellationTokenSourceWrapper();
                CarouselToken = new CancellationTokenSourceWrapper();

                InitializeComponent();

                BackgroundImgChanger.ToggleBackground(false);

                await GetCurrentGameState();

                if (!GetAppConfigValue("ShowEventsPanel").ToBool())
                {
                    SidePanel.Visibility = Visibility.Collapsed;
                }

                if (!GetAppConfigValue("ShowSocialMediaPanel").ToBool())
                    SocMedPanel.Visibility = Visibility.Collapsed;

                if (!GetAppConfigValue("ShowGamePlaytime").ToBool())
                    PlaytimeBtn.Visibility = Visibility.Collapsed;

                if (!DbConfig.DbEnabled)
                {
                    PlaytimeDbSyncToggle.IsEnabled = false;
                }
                
                if (!DbConfig.DbEnabled || !(CurrentGameProperty.GameSettings?.SettingsCollapseMisc.IsSyncPlaytimeToDatabase ?? false))
                    SyncDbPlaytimeBtn.IsEnabled = false;

                TryLoadEventPanelImage();

                SocMedPanel.Translation += Shadow48;
                GameStartupSetting.Translation += Shadow32;
                CommunityToolsBtn.Translation += Shadow32;

                if (IsCarouselPanelAvailable || IsPostPanelAvailable)
                {
                    ImageCarousel.SelectedIndex = 0;
                    ImageCarousel.Visibility = Visibility.Visible;
                    ImageCarouselPipsPager.Visibility = Visibility.Visible;

                    ShowEventsPanelToggle.IsEnabled = true;
                    ScaleUpEventsPanelToggle.IsEnabled = true;
                    PostPanel.Visibility = Visibility.Visible;
                    PostPanel.Translation += Shadow48;
                }

                InputSystemCursor cursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
                SophonProgressStatusGrid.SetAllControlsCursorRecursive(cursor);
                ProgressStatusGrid.SetAllControlsCursorRecursive(cursor);
                RightBottomButtons.SetAllControlsCursorRecursive(cursor);
                LeftBottomButtons.SetAllControlsCursorRecursive(cursor);
                GameStartupSettingFlyoutContainer.SetAllControlsCursorRecursive(cursor);

                if (await CurrentGameProperty.GameInstall.TryShowFailedDeltaPatchState()) return;
                if (await CurrentGameProperty.GameInstall.TryShowFailedGameConversionState()) return;
                CurrentGameProperty.GamePlaytime.PlaytimeUpdated += UpdatePlaytime;
                UpdatePlaytime(null, CurrentGameProperty.GamePlaytime.CollapsePlaytime);

                _ = StartCarouselAutoScroll();

#if !DISABLEDISCORD
                AppDiscordPresence?.SetActivity(ActivityType.Idle);
#endif

                if (IsGameStatusComingSoon || IsGameStatusPreRegister)
                {
                    LauncherBtn.Visibility = Visibility.Collapsed;
                    LauncherGameStatusPlaceholderBtn.Visibility = Visibility.Visible;

                    if (IsGameStatusComingSoon) GamePlaceholderBtnComingSoon.Visibility = Visibility.Visible;
                    if (IsGameStatusPreRegister) GamePlaceholderBtnPreRegister.Visibility = Visibility.Visible;

                    return;
                }

                if (CurrentGameProperty.IsGameRunning)
                {
                    CheckRunningGameInstance(PageToken.Token);
                    return;
                }

                // Get game state
                GameInstallStateEnum gameState = await CurrentGameProperty.GameVersion.GetGameState();

                // Start automatic scan if the game is in NotInstalled state
                // and if the return is 0 (yes), then save the config
                if (gameState == GameInstallStateEnum.NotInstalled &&
                    await CurrentGameProperty.GameInstall.GetInstallationPath(true) == 0)
                {
                    // Save the config
                    CurrentGameProperty.GameInstall.ApplyGameConfig();

                    // Refresh the Home page.
                    ReturnToHomePage();
                    return;
                }

                // Check if the game state returns NotInstalled, double-check by doing config.ini validation
                if (!await CurrentGameProperty.GameVersion
                          .EnsureGameConfigIniCorrectiveness(this))
                {
                    // If the EnsureGameConfigIniCorrectiveness() returns false,
                    // means config.ini has been changed. Then reload and return to the HomePage
                    ReturnToHomePage();
                    return;
                }

                if (!(m_arguments.StartGame?.Play ?? false))
                {
                    await CheckUserAccountControlStatus();
                    return;
                }

                m_arguments.StartGame.Play = false;

                if (CurrentGameProperty.GameInstall.IsRunning)
                {
                    CurrentGameProperty.GameInstall.StartAfterInstall = CurrentGameProperty.GameInstall.IsRunning;
                    return;
                }

                switch (gameState)
                {
                    case GameInstallStateEnum.InstalledHavePreload:
                    case GameInstallStateEnum.Installed:
                        StartGame(null, null);
                        break;
                    case GameInstallStateEnum.InstalledHavePlugin:
                    case GameInstallStateEnum.NeedsUpdate:
                        CurrentGameProperty.GameInstall.StartAfterInstall = true;
                        UpdateGameDialog(null, null);
                        break;
                    case GameInstallStateEnum.NotInstalled:
                    case GameInstallStateEnum.GameBroken:
                        CurrentGameProperty.GameInstall.StartAfterInstall = true;
                        InstallGameDialog(null, null);
                        break;
                }
            }
            catch (ArgumentNullException ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"The necessary section of Launcher Scope's config.ini is broken.\r\n{ex}", LogType.Error, true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            IsPageUnload                                     =  true;
            CurrentGameProperty.GamePlaytime.PlaytimeUpdated -= UpdatePlaytime;

            if (!PageToken.IsDisposed && !PageToken.IsCancelled) PageToken.Cancel();
            if (!CarouselToken.IsDisposed && !CarouselToken.IsCancelled) CarouselToken.Cancel();
        }
        #endregion

        #region EventPanel
        private readonly ConcurrentDictionary<string, byte> _eventPanelProcessing = new();
        private async void TryLoadEventPanelImage()
        {
            // Get the url and article image path
            string featuredEventArticleUrl = LauncherMetadataHelper.CurrentMetadataConfig?.GameLauncherApi?
                .LauncherGameNews?.Content?.Background?.FeaturedEventIconBtnUrl;
            string featuredEventIconImg = LauncherMetadataHelper.CurrentMetadataConfig?.GameLauncherApi?
                .LauncherGameNews?.Content?.Background?.FeaturedEventIconBtnImg;

            // If the region event panel property is null, then return
            if (string.IsNullOrEmpty(featuredEventIconImg)
             || string.IsNullOrEmpty(featuredEventArticleUrl)) return;

            if (!_eventPanelProcessing.TryAdd(featuredEventArticleUrl, 0) ||
                !_eventPanelProcessing.TryAdd(featuredEventIconImg, 0))
            {
                LogWriteLine($"[TryLoadEventPanelImage] Stopped processing {featuredEventIconImg} and {featuredEventArticleUrl} due to double processing",
                             LogType.Warning, true);
                return;
            }

            // Get the cached filename and path
            string cachedFileHash = Hash.GetHashStringFromString<Crc32>(featuredEventIconImg);
            string cachedFilePath = Path.Combine(AppGameImgCachedFolder, cachedFileHash);
            if (ImageLoaderHelper.IsWaifu2XEnabled)
                cachedFilePath += "_waifu2x";

            // Create a cached image folder if not exist
            if (!Directory.Exists(AppGameImgCachedFolder))
                Directory.CreateDirectory(AppGameImgCachedFolder);

            // Init BitmapImage to load the image and the info for cached event icon file
            BitmapImage source = new BitmapImage();
            FileInfo cachedIconFileInfo = new FileInfo(cachedFilePath);

            // Determine if the cache icon exist and the file is completed (more than 1kB in size)
            bool isCacheIconExist = cachedIconFileInfo.Exists && cachedIconFileInfo.Length > 1 << 10;

            try
            {
                // Using the original icon file and cached icon file streams
                if (!isCacheIconExist)
                {
                    await using FileStream cachedIconFileStream = new FileStream(cachedIconFileInfo.FullName,
                             FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                    await using MemoryStream copyIconFileStream = new MemoryStream();
                    await using Stream iconFileStream =
                        await FallbackCDNUtil.GetHttpStreamFromResponse(featuredEventIconImg,
                                                                        PageToken.Token);
                    var scaleFactor = WindowUtility.CurrentWindowMonitorScaleFactor;
                    // Copy remote stream to memory stream
                    await iconFileStream.CopyToAsync(copyIconFileStream);
                    copyIconFileStream.Position = 0;
                    // Get the icon image information and set the resized frame size
                    var iconImageInfo = await Task.Run(() => ImageFileInfo.Load(copyIconFileStream));
                    var width         = (int)(iconImageInfo.Frames[0].Width * scaleFactor);
                    var height        = (int)(iconImageInfo.Frames[0].Height * scaleFactor);

                    copyIconFileStream.Position = 0; // Reset the original icon stream position
                    await ImageLoaderHelper.ResizeImageStream(copyIconFileStream, cachedIconFileStream,
                                                              (uint)width, (uint)height); // Start resizing
                    cachedIconFileStream.Position = 0; // Reset the cached icon stream position

                    // Set the source from cached icon stream
                    source.SetSource(cachedIconFileStream.AsRandomAccessStream());
                }
                else
                {
                    await using Stream cachedIconFileStream = cachedIconFileInfo.OpenRead();
                    // Set the source from cached icon stream
                    source.SetSource(cachedIconFileStream.AsRandomAccessStream());
                }

                // Set event icon props
                ImageEventImgGrid.Visibility = !NeedShowEventIcon ? Visibility.Collapsed : Visibility.Visible;
                ImageEventImg.Source         = source;
                ImageEventImg.Tag            = featuredEventArticleUrl;
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed while loading EventPanel image icon\r\n{ex}", LogType.Error, true);
            }
            finally
            {
                _eventPanelProcessing.Remove(featuredEventIconImg,    out _);
                _eventPanelProcessing.Remove(featuredEventArticleUrl, out _);
            }
        }
        #endregion

        #region Carousel

        private async Task StartCarouselAutoScroll(int delaySeconds = 5)
        {
            if (!IsCarouselPanelAvailable) return;
            if (delaySeconds < 5) delaySeconds = 5;
            
            try
            {
                while (true)
                {
                    if (CarouselToken == null || CarouselToken.IsCancellationRequested || CarouselToken.IsDisposed || CarouselToken.IsCancelled)
                    {
                        CarouselToken = new CancellationTokenSourceWrapper();
                    }
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), CarouselToken.Token);
                    if (!IsCarouselPanelAvailable) return;
                    if (ImageCarousel.SelectedIndex != GameNewsData!.NewsCarousel!.Count - 1 
                        && ImageCarousel.SelectedIndex < ImageCarousel.Items.Count - 1)
                        ImageCarousel.SelectedIndex++;
                    else
                        for (int i = GameNewsData.NewsCarousel.Count; i > 0; i--)
                        {
                            if (i - 1 >= 0 && i - 1 < ImageCarousel.Items.Count)
                            {
                                ImageCarousel.SelectedIndex = i - 1;
                            }
                            if (CarouselToken is { IsDisposed: false, IsCancellationRequested: false })
                            {
                                await Task.Delay(100, CarouselToken.Token);
                            }
                        }
                }
            }
            catch (TaskCanceledException)
            {
                // Ignore
            }
            catch (Exception ex)
            {
                LogWriteLine($"[HomePage::StartCarouselAutoScroll] Task returns error!\r\n{ex}", LogType.Error, true);
                _ = CarouselRestartScroll();
            }
        }

        private async void CarouselPointerExited(object sender = null, PointerRoutedEventArgs e = null) =>
            await CarouselRestartScroll();

        private async void CarouselPointerEntered(object sender = null, PointerRoutedEventArgs e = null) =>
            await CarouselStopScroll();

        public async Task CarouselRestartScroll(int delaySeconds = 5)
        {
            // Don't restart carousel if game is running and LoPrio is on
            if (_cachedIsGameRunning && GetAppConfigValue("LowerCollapsePrioOnGameLaunch").ToBool()) return;
            await CarouselStopScroll();

            CarouselToken = new CancellationTokenSourceWrapper();
            _ = StartCarouselAutoScroll(delaySeconds);
        }

        public async ValueTask CarouselStopScroll()
        {
            if (CarouselToken is { IsCancellationRequested: false, IsDisposed: false, IsCancelled: false })
            {
                await CarouselToken.CancelAsync();
                CarouselToken.Dispose();
            }
        }

        private async void HideImageCarousel(bool hide)
        {
            if (!hide)
                SidePanel.Visibility = Visibility.Visible;

            HideImageEventImg(hide);

            Storyboard      storyboard       = new Storyboard();
            DoubleAnimation OpacityAnimation = new DoubleAnimation
            {
                From     = hide ? 1 : 0,
                To       = hide ? 0 : 1,
                Duration = new Duration(TimeSpan.FromSeconds(0.10))
            };

            Storyboard.SetTarget(OpacityAnimation, SidePanel);
            Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
            storyboard.Children.Add(OpacityAnimation);

            storyboard.Begin();

            await Task.Delay(100);

            SidePanel.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }
        #endregion

        #region SocMed Buttons
        private void FadeInSocMedButton(object sender, PointerRoutedEventArgs e)
        {
            Button btn = (Button)sender;
            btn.Translation = Shadow16;

            Grid             iconGrid   = btn.FindDescendant<Grid>();
            FrameworkElement iconFirst  = iconGrid!.FindDescendant("Icon");
            FrameworkElement iconSecond = iconGrid!.FindDescendant("IconHover");

            ElementScaleOutHoveredPointerEnteredInner(iconGrid, 0, -2);

            TimeSpan dur = TimeSpan.FromSeconds(0.25f);
            iconFirst.StartAnimationDetached(dur, iconFirst.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 0.0f, delay: TimeSpan.FromSeconds(0.08f)));
            iconSecond.StartAnimationDetached(dur, iconFirst.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 1.0f));
        }

        private void FadeOutSocMedButton(object sender, PointerRoutedEventArgs e)
        {
            Button btn = (Button)sender;
            btn.Translation = new Vector3(0);

            FlyoutBase flyout = btn.Flyout;
            Point pos = e.GetCurrentPoint(btn).Position;
            if (pos.Y <= 0 || pos.Y >= btn.Height || pos.X <= -8 || pos.X >= btn.Width)
            {
                flyout?.Hide();
            }

            Grid             iconGrid   = btn.FindDescendant<Grid>();
            FrameworkElement iconFirst  = iconGrid!.FindDescendant("Icon");
            FrameworkElement iconSecond = iconGrid!.FindDescendant("IconHover");

            ElementScaleInHoveredPointerExitedInner(iconGrid, 0, -2);

            TimeSpan dur = TimeSpan.FromSeconds(0.25f);
            iconFirst.StartAnimationDetached(dur, iconFirst.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 1.0f));
            iconSecond.StartAnimationDetached(dur, iconFirst.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 0.0f, delay: TimeSpan.FromSeconds(0.08f)));
        }

        private async void HideSocialMediaPanel(bool hide)
        {
            if (!hide)
            {
                SocMedPanel.Visibility = Visibility.Visible;
            }

            Storyboard      storyboard       = new Storyboard();
            DoubleAnimation OpacityAnimation = new DoubleAnimation
            {
                From     = hide ? 1 : 0,
                To       = hide ? 0 : 1,
                Duration = new Duration(TimeSpan.FromSeconds(0.10))
            };

            Storyboard.SetTarget(OpacityAnimation, SocMedPanel);
            Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
            storyboard.Children.Add(OpacityAnimation);

            storyboard.Begin();

            await Task.Delay(100);

            SocMedPanel.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void HidePlaytimeButton(bool hide)
        {
            if (!hide) PlaytimeBtn.Visibility = Visibility.Visible;

            Storyboard      storyboard       = new Storyboard();
            DoubleAnimation OpacityAnimation = new DoubleAnimation
            {
                From     = hide ? 1 : 0,
                To       = hide ? 0 : 1,
                Duration = new Duration(TimeSpan.FromSeconds(0.10))
            };

            Storyboard.SetTarget(OpacityAnimation, PlaytimeBtn);
            Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
            storyboard.Children.Add(OpacityAnimation);

            storyboard.Begin();

            await Task.Delay(100);

            PlaytimeBtn.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OpenSocMedLink(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty((sender as Button)?.Tag as string)) return;

            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = ((Button)sender).Tag.ToString()!
                }
            }.Start();
        }

        private void ShowSocMedFlyout(object sender, RoutedEventArgs e)
        {
            if (sender is not ToolTip { Tag: Button button })
            {
                return;
            }

            if (!button.IsPointerOver && _lastSocMedButton == button)
                return;
            _lastSocMedButton = button;

            Flyout flyout = button.Flyout as Flyout;
            if (flyout != null)
            {
                Panel contextPanel = flyout.Content as Panel;
                if (contextPanel != null && contextPanel.Tag is LauncherGameNewsSocialMedia socMedData)
                {
                    if (!socMedData.IsHasDescription && !socMedData.IsHasLinks && !socMedData.IsHasQr)
                    {
                        return;
                    }
                }
            }

            FlyoutBase.ShowAttachedFlyout(button);
        }

        private void HideSocMedFlyout(object sender, RoutedEventArgs e)
        {
            Grid dummyGrid = (sender as Panel ?? throw new InvalidOperationException()).FindChild<Grid>();
            if (dummyGrid == null)
            {
                return;
            }

            Flyout flyout = dummyGrid.Tag as Flyout;
            flyout?.Hide();
        }

        private void OnLoadedSocMedFlyout(object sender, RoutedEventArgs e)
        {
            // Prevent the flyout showing when there is no content visible
            StackPanel stackPanel = sender as StackPanel;

            if (stackPanel == null)
            {
                return;
            }

            ApplySocialMediaBinding(stackPanel);

            bool visible = false;
            foreach (var child in stackPanel!.Children)
            {
                if (child.Visibility == Visibility.Visible)
                    visible = true;
            }

            if (!visible)
            {
                HideSocMedFlyout(sender, e);
            }
        }
        #endregion

        #region Event Image
        private async void HideImageEventImg(bool hide)
        {
            //if (!NeedShowEventIcon) return;

            if (!hide)
                ImageEventImgGrid.Visibility = Visibility.Visible;

            Storyboard      storyboard       = new Storyboard();
            DoubleAnimation OpacityAnimation = new DoubleAnimation
            {
                From     = hide ? 1 : 0,
                To       = hide ? 0 : 1,
                Duration = new Duration(TimeSpan.FromSeconds(0.10))
            };

            Storyboard.SetTarget(OpacityAnimation, ImageEventImgGrid);
            Storyboard.SetTargetProperty(OpacityAnimation, "Opacity");
            storyboard.Children.Add(OpacityAnimation);

            storyboard.Begin();

            await Task.Delay(100);

            ImageEventImgGrid.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        }
        #endregion

        #region Open Link from Tag
        private void OpenImageLinkFromTag(object sender, PointerRoutedEventArgs e)
        {
            if (!e.GetCurrentPoint((UIElement)sender).Properties.IsLeftButtonPressed) return;
            SpawnWebView2.SpawnWebView2Window(((ImageEx.ImageEx)sender).Tag.ToString(), Content);
        }

        private async void OpenButtonLinkFromTag(object sender, RoutedEventArgs e)
        {
            // Get the tag string.
            string tagContent = ((ButtonBase)sender).Tag.ToString();

            // Split the tag string by $ character as separator.
            string[] tagProperty = tagContent!.Split('$');

            // If the tagProperty has more than 1 array (that means it has tag action property),
            // then generate the tag action and execute it.
            if (tagProperty.Length > 1)
            {
                // Check if the tag has "OpenUrlIfCancel". This will be used to check if the
                // tag action is getting cancelled, then open the URL of the tag.
                bool isOpenUrlIfCancel = tagProperty.Contains("OpenUrlIfCancel");

                // Build the tag Task to be executed. The action will return boolean as the result.
                //    true          => Task has been executed successfully.
                //    false         => Task was cancelled or error has occurred.
                //    Task<null>    => Means the tag action property failed to be deserialized caused by
                //                     invalid tag or parameter/argument.
                Task<bool> action = TryBuildTagPropertyAction(tagProperty[1]);

                // If the action returns a null task (Task<null>), then fallback to open the URL instead.
                if (action == null)
                {
                    LogWriteLine($"Tag Property seems to be invalid or incomplete. Fallback to open the URL instead!\r\nTag String: {tagProperty[1]}", LogType.Warning, true);
                    SpawnWebView2.SpawnWebView2Window(tagProperty[0], Content);
                    return;
                }

                // Await and run the tag action task and put the action result to isActionCompleted
                bool isActionCompleted = await action;
                // If the action is true (successfully executed), then return
                if (isActionCompleted) return;
                // If the action is false (failed/cancel) and doesn't have "OpenUrlIfCancel" tag, then return
                // Otherwise, fallback to open the URL.
                if (!isOpenUrlIfCancel) return;
            }

            // Open the URL and spawn WebView2 window
            SpawnWebView2.SpawnWebView2Window(tagProperty[0], Content);
        }

        private void ClickImageEventSpriteLink(object sender, PointerRoutedEventArgs e)
        {
            if (!e.GetCurrentPoint((UIElement)sender).Properties.IsLeftButtonPressed) return;
            object ImageTag = ((Image)sender).Tag;
            if (ImageTag == null) return;
            SpawnWebView2.SpawnWebView2Window((string)ImageTag, Content);
        }
        #endregion

        #region Tag Property
        private Task<bool> TryBuildTagPropertyAction(string tagProperty)
        {
            try
            {
                // Split the property string by : mark to get the tag action type and its parameter.
                string[] property = tagProperty.Split(':');
                // If the property array has less than 2, then return null to fallback (open the URL).
                if (property.Length < 2) return null;

                // Check the tag action type
                switch (property[0].ToLower())
                {
                    case "openexternalapp":
                        return TagPropertyAction_OpenExternalApp(property[1]);
                }
            }
            // If the error has occured, then return null to fallback (open the URL).
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex);
                LogWriteLine($"Failed while parsing Tag Property: {tagProperty}!\r\n{ex}", LogType.Warning, true);
            }
            return null;
        }

        private async Task<bool> TagPropertyAction_OpenExternalApp(string propertiesString)
        {
            // Split the properties string by , mark to get the argument string.
            string[] properties = propertiesString.Split(',');
            // Initialize the application properties
            string applicationName = "";
            string applicationExecName = "";
            bool runAsAdmin = false;

            // If the properties array is empty, then throw and back to fallback (open the URL).
            if (properties.Length == 0) throw new ArgumentNullException(properties.Length.ToString(),
                                                                        "Properties for OpenExternalApp can't be empty!");

            // Iterate the properties array
            for (int i = properties.Length - 1; i >= 0; i--)
            {
                // Split the property by = mark to get the argument and its value.
                string[] argumentStr = properties[i].Split("=");

                // If the argument array is empty, then throw and back to fallback (open the URL).
                if (argumentStr.Length == 0) throw new ArgumentNullException(argumentStr.Length.ToString(),
                                                                             "Argument can't be empty!");

                // Check the argument type
                switch (argumentStr[0].ToLower())
                {
                    case "applicationexecname":
                        // If the value is empty, then throw and back to fallback (open the URL).
                        if (argumentStr.Length < 2) throw new ArgumentException($"Argument error on {argumentStr[0]}: Executable name must be defined!");

                        // Get the application executable 
                        applicationExecName = argumentStr[1];
# if DEBUG
                        LogWriteLine($"Got '{argumentStr[0]}' as parameter for application executable", LogType.Debug, true);
# endif
                        break;
                    case "applicationname":
                        // Get the application name. If it's empty, then fallback to default "MyApplication" name.
                        // Else, return the defined application name.
                        applicationName = argumentStr.Length < 2 || string.IsNullOrEmpty(argumentStr[1]) ? "MyApplication" : argumentStr[1];
#if DEBUG
                        LogWriteLine($"Got '{argumentStr[1]}' as parameter for application name", LogType.Debug, true);
# endif
                        break;
                    case "runasadmin":
                        // Try parse the boolean value. If it's valid and the value of runAsAdmin is true, then set
                        // runAsAdmin as true and "AND" it with isBoolValid.
                        bool isBoolValid = bool.TryParse(argumentStr[1], out runAsAdmin);
                        runAsAdmin = isBoolValid && runAsAdmin;
#if DEBUG
                        LogWriteLine($"Got '{isBoolValid}' as parameter for application executable", LogType.Debug, true);
# endif
                        break;
                    default:
                        // If the argument type is unknown, then throw and back to fallback (open the URL).
                        throw new ArgumentException($"Argument {argumentStr[0]} is unknown!");
                }
            }

            // Trim the space in the application name to be used for app config.
            string applicationNameTrimmed = applicationName.Replace(" ", "");
            // If the RunAsAdmin config key doesn't exist, then create one.
            if (!IsConfigKeyExist($"Exec_RunAsAdmin_{applicationNameTrimmed}")) SetAndSaveConfigValue($"Exec_RunAsAdmin_{applicationNameTrimmed}", runAsAdmin);

            // Check if the Application path config exists. If not, then return empty string. Otherwise, return the actual value.
            string applicationPath = IsConfigKeyExist($"Exec_Path_{applicationNameTrimmed}") ? GetAppConfigValue($"Exec_Path_{applicationNameTrimmed}").ToString() : "";
            // Check if the applicationPath variable is empty or if the application path in applicationPath variable.
            // If the variable is empty or the path is not exist, then spawn File Picker dialog.
            if (string.IsNullOrEmpty(applicationPath) || !File.Exists(applicationPath))
            {
                // Run the loop
                while (true)
                {
                    // Set initial value to null
                    string file = null;
                    switch (await Dialog_OpenExecutable(Content))
                    {
                        case ContentDialogResult.Primary:
                            // Try to get the file path
                            file = await FileDialogNative.GetFilePicker(new Dictionary<string, string> { { applicationName, applicationExecName } }, string.Format(Lang._HomePage.CommunityToolsBtn_OpenExecutableAppDialogTitle, applicationName));
                            // If the file returns null because of getting cancelled, then back to loop again.
                            if (string.IsNullOrEmpty(file)) continue;
                            // Otherwise, assign the value to applicationPath variable and save it to the app config
                            applicationPath = file;
                            SetAndSaveConfigValue($"Exec_Path_{applicationNameTrimmed}", file);
                            break;
                        case ContentDialogResult.Secondary:
                            // If the main dialog is getting cancelled, then return false (as cancel and fallback to URL [if enabled]).
                            return false;
                        case ContentDialogResult.None:
                            // Return true when cancelled
                            return true;
                    }

                    // If the file variable is not null anymore, then break from the loop and continue
                    // the call below.
                    if (!string.IsNullOrEmpty(file)) break;
                }
            }

            try
            {
                // Try to run the application
                Process proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        Verb = runAsAdmin ? "runas" : "",
                        FileName = applicationPath,
                        WorkingDirectory = Path.GetDirectoryName(applicationPath)!
                    }
                };
                proc.Start();
            }
            catch (Exception ex)
            {
                // If error happened while running the app, then log and return true as successful
                // Thoughts @Cry0? Should we mark it as successful (true) or failed (false)?
                // Mark is as true, since the app still ran, but it's an error outside our scope.
                await SentryHelper.ExceptionHandlerAsync(ex);
                LogWriteLine($"Unable to start app {applicationName}! {ex}", LogType.Error, true);
                return true;
            }

            // If all above are executed successfully, then return true as "successful"
            return true;
        }
        #endregion

        #region Game State
        private async ValueTask GetCurrentGameState()
        {
            Visibility repairGameButtonVisible = CurrentGameProperty.GameVersion.GamePreset.IsRepairEnabled ?? false ?
                Visibility.Visible : Visibility.Collapsed;

            if (!(CurrentGameProperty.GameVersion.GamePreset.IsConvertible ?? false)
                || CurrentGameProperty.GameVersion.GameType != GameNameType.Honkai)
                ConvertVersionButton.Visibility = Visibility.Collapsed;

            // Clear the _CommunityToolsProperty statics
            PageStatics.CommunityToolsProperty.Clear();

            // Check if the _CommunityToolsProperty has the official tool list for current game type
            if (PageStatics.CommunityToolsProperty.OfficialToolsDictionary.TryGetValue(CurrentGameProperty.GameVersion.GameType, out List<CommunityToolsEntry> officialEntryList))
            {
                // If yes, then iterate it and add it to the list, to then getting read by the
                // DataTemplate from HomePage
                for (var index = officialEntryList.Count - 1; index >= 0; index--)
                {
                    var iconProperty = officialEntryList[index];
                    if (iconProperty.Profiles.Contains(CurrentGameProperty.GamePreset.ProfileName))
                    {
                        PageStatics.CommunityToolsProperty.OfficialToolsList.Add(iconProperty);
                    }
                }
            }

            // Check if the _CommunityToolsProperty has the community tool list for current game type
            if (PageStatics.CommunityToolsProperty.CommunityToolsDictionary.TryGetValue(CurrentGameProperty.GameVersion.GameType, out List<CommunityToolsEntry> communityEntryList))
            {
                // If yes, then iterate it and add it to the list, to then getting read by the
                // DataTemplate from HomePage
                for (var index = communityEntryList.Count - 1; index >= 0; index--)
                {
                    var iconProperty = communityEntryList[index];
                    if (iconProperty.Profiles.Contains(CurrentGameProperty.GamePreset.ProfileName))
                    {
                        PageStatics.CommunityToolsProperty.CommunityToolsList.Add(iconProperty);
                    }
                }
            }

            if (CurrentGameProperty.GameVersion.GameType == GameNameType.Genshin) OpenCacheFolderButton.Visibility = Visibility.Collapsed;
            GameInstallationState = await CurrentGameProperty.GameVersion.GetGameState();

            switch (GameInstallationState)
            {
                case GameInstallStateEnum.Installed:
                    {
                        RepairGameButton.Visibility = repairGameButtonVisible;
                        InstallGameBtn.Visibility = Visibility.Collapsed;
                        StartGameBtn.Visibility = Visibility.Visible;
                        CustomStartupArgs.Visibility = Visibility.Visible;
                    }
                    break;
                case GameInstallStateEnum.InstalledHavePreload:
                    {
                        RepairGameButton.Visibility = repairGameButtonVisible;
                        CustomStartupArgs.Visibility = Visibility.Visible;
                        InstallGameBtn.Visibility = Visibility.Collapsed;
                        StartGameBtn.Visibility = Visibility.Visible;
                        //NeedShowEventIcon = false;
                        SpawnPreloadBox();
                    }
                    break;
                case GameInstallStateEnum.NeedsUpdate:
                case GameInstallStateEnum.InstalledHavePlugin:
                    {
                        RepairGameButton.Visibility = repairGameButtonVisible;
                        RepairGameButton.IsEnabled = false;
                        CleanupFilesButton.IsEnabled = false;
                        UpdateGameBtn.Visibility = Visibility.Visible;
                        StartGameBtn.Visibility = Visibility.Collapsed;
                        InstallGameBtn.Visibility = Visibility.Collapsed;
                    }
                    break;
                default:
                    {
                        UninstallGameButton.IsEnabled = false;
                        RepairGameButton.IsEnabled = false;
                        OpenGameFolderButton.IsEnabled = false;
                        CleanupFilesButton.IsEnabled = false;
                        OpenCacheFolderButton.IsEnabled = false;
                        ConvertVersionButton.IsEnabled = false;
                        CustomArgsTextBox.IsEnabled = false;
                        OpenScreenshotFolderButton.IsEnabled = false;
                        ConvertVersionButton.Visibility = Visibility.Collapsed;
                        RepairGameButton.Visibility = Visibility.Collapsed;
                        UninstallGameButton.Visibility = Visibility.Collapsed;
                        MoveGameLocationButton.Visibility = Visibility.Collapsed;
                    }
                    break;
            }

            if (CurrentGameProperty.GameInstall.IsRunning)
                RaiseBackgroundInstallationStatus(GameInstallationState);
        }

        private void RaiseBackgroundInstallationStatus(GameInstallStateEnum gameInstallationState)
        {
            if (gameInstallationState != GameInstallStateEnum.NeedsUpdate
                && gameInstallationState != GameInstallStateEnum.InstalledHavePlugin
                && gameInstallationState != GameInstallStateEnum.GameBroken
                && gameInstallationState != GameInstallStateEnum.NotInstalled)
            {
                return;
            }

            HideImageCarousel(true);

            progressRing.Value           = 0;
            progressRing.IsIndeterminate = true;

            InstallGameBtn.Visibility    = Visibility.Collapsed;
            UpdateGameBtn.Visibility     = Visibility.Collapsed;
            CancelDownloadBtn.Visibility = Visibility.Visible;
            ProgressTimeLeft.Visibility  = Visibility.Visible;

            bool isUseSophon = CurrentGameProperty.GameInstall.IsUseSophon;
            if (isUseSophon)
            {
                SophonProgressStatusGrid.Visibility             =  Visibility.Visible;
                CurrentGameProperty.GameInstall.ProgressChanged += GameInstallSophon_ProgressChanged;
                CurrentGameProperty.GameInstall.StatusChanged   += GameInstallSophon_StatusChanged;
            }
            else
            {
                ProgressStatusGrid.Visibility                   =  Visibility.Visible;
                CurrentGameProperty.GameInstall.ProgressChanged += GameInstall_ProgressChanged;
                CurrentGameProperty.GameInstall.StatusChanged   += GameInstall_StatusChanged;
            }
        }

        private async void CheckRunningGameInstance(CancellationToken token)
        {
            TextBlock startGameBtnText = (StartGameBtn.Content as Grid)!.Children.OfType<TextBlock>().FirstOrDefault();
            FontIcon startGameBtnIcon = (StartGameBtn.Content as Grid)!.Children.OfType<FontIcon>().FirstOrDefault();
            Grid startGameBtnAnimatedIconGrid = (StartGameBtn.Content as Grid)!.Children.OfType<Grid>().FirstOrDefault();
            // AnimatedVisualPlayer    StartGameBtnAnimatedIcon      = StartGameBtnAnimatedIconGrid!.Children.OfType<AnimatedVisualPlayer>().FirstOrDefault();
            string       startGameBtnIconGlyph        = startGameBtnIcon!.Glyph;
            const string startGameBtnRunningIconGlyph = "";

            startGameBtnIcon.EnableSingleImplicitAnimation(VisualPropertyType.Opacity);
            startGameBtnAnimatedIconGrid.EnableSingleImplicitAnimation(VisualPropertyType.Opacity);

            try
            {
                while (CurrentGameProperty.IsGameRunning)
                {
                    _cachedIsGameRunning = true;

                    StartGameBtn.IsEnabled = false;
                    if (startGameBtnText != null && startGameBtnAnimatedIconGrid != null)
                    {
                        startGameBtnText.Text                = Lang._HomePage.StartBtnRunning;
                        startGameBtnIcon.Glyph               = startGameBtnRunningIconGlyph;
                        startGameBtnAnimatedIconGrid.Opacity = 0;
                        startGameBtnIcon.Opacity             = 1;

                        startGameBtnText.UpdateLayout();

                        RepairGameButton.IsEnabled       = false;
                        UninstallGameButton.IsEnabled    = false;
                        ConvertVersionButton.IsEnabled   = false;
                        CustomArgsTextBox.IsEnabled      = false;
                        MoveGameLocationButton.IsEnabled = false;
                        StopGameButton.IsEnabled = true;

                        PlaytimeIdleStack.Visibility = Visibility.Collapsed;
                        PlaytimeRunningStack.Visibility = Visibility.Visible;

                        Process currentGameProcess = CurrentGameProperty.GetGameProcessWithActiveWindow();
                        if (currentGameProcess != null)
                        {
                            // HACK: For some reason, the text still unchanged.
                            //       Make sure the start game button text also changed.
                            startGameBtnText.Text = Lang._HomePage.StartBtnRunning;
                            var fromActivityOffset = currentGameProcess.StartTime;
                            var gameSettings       = CurrentGameProperty!.GameSettings!.AsIGameSettingsUniversal();
                            var gamePreset         = CurrentGameProperty.GamePreset;
                            
#if !DISABLEDISCORD
                            if (ToggleRegionPlayingRpc)
                                AppDiscordPresence?.SetActivity(ActivityType.Play, fromActivityOffset.ToUniversalTime());
#endif

                            CurrentGameProperty!.GamePlaytime!.StartSession(currentGameProcess);

                            int? height = gameSettings.SettingsScreen.height;
                            int? width = gameSettings.SettingsScreen.width;

                            // Start the resizable window payload
                            StartResizableWindowPayload(
                                                        gamePreset.GameExecutableName,
                                                        gameSettings,
                                                        gamePreset.GameType, height, width);

                            await currentGameProcess.WaitForExitAsync(token);
                        }
                    }

                    await Task.Delay(RefreshRate, token);
                }

                _cachedIsGameRunning = false;

                StartGameBtn.IsEnabled = true;
                startGameBtnText!.Text = Lang._HomePage.StartBtn;
                startGameBtnIcon.Glyph = startGameBtnIconGlyph;
                if (startGameBtnAnimatedIconGrid != null)
                {
                    startGameBtnAnimatedIconGrid.Opacity = 1;
                }

                startGameBtnIcon.Opacity = 0;

                GameStartupSetting.IsEnabled = true;
                RepairGameButton.IsEnabled = true;
                MoveGameLocationButton.IsEnabled = true;
                UninstallGameButton.IsEnabled = true;
                ConvertVersionButton.IsEnabled = true;
                CustomArgsTextBox.IsEnabled = true;
                StopGameButton.IsEnabled = false;

                PlaytimeIdleStack.Visibility = Visibility.Visible;
                PlaytimeRunningStack.Visibility = Visibility.Collapsed;
                
            #if !DISABLEDISCORD
                AppDiscordPresence?.SetActivity(ActivityType.Idle);
            #endif
            }
            catch (TaskCanceledException)
            {
                // Ignore
                LogWriteLine("Game run watcher has been terminated!");
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Error when checking if game is running!\r\n{ex}", LogType.Error, true);
            }
        }
        #endregion

        #region Community Button
        private void OpenCommunityButtonLink(object sender, RoutedEventArgs e)
        {
            DispatcherQueue?.TryEnqueue(CommunityToolsBtn.Flyout.Hide);
            OpenButtonLinkFromTag(sender, e);
        }
        #endregion

        #region Preload
        private async void SpawnPreloadBox()
        {
            if (CurrentGameProperty.GameInstall.IsUseSophon)
            {
                DownloadModeLabelPreload.Visibility = Visibility.Visible;
                DownloadModeLabelPreloadText.Text = Lang._Misc.DownloadModeLabelSophon;
            }

            if (CurrentGameProperty.GameInstall.IsRunning)
            {
                // TODO
                PauseDownloadPreBtn.Visibility = Visibility.Visible;
                ResumeDownloadPreBtn.Visibility = Visibility.Collapsed;
                PreloadDialogBox.IsClosable = false;

                IsSkippingUpdateCheck = true;
                DownloadPreBtn.Visibility = Visibility.Collapsed;
                if (CurrentGameProperty.GameInstall.IsUseSophon)
                {
                    ProgressPreSophonStatusGrid.Visibility = Visibility.Visible;
                    ProgressPreStatusGrid.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ProgressPreStatusGrid.Visibility = Visibility.Visible;
                }
                ProgressPreButtonGrid.Visibility = Visibility.Visible;
                PreloadDialogBox.Title = Lang._HomePage.PreloadDownloadNotifbarTitle;
                PreloadDialogBox.Message = Lang._HomePage.PreloadDownloadNotifbarSubtitle;

                CurrentGameProperty.GameInstall.ProgressChanged += PreloadDownloadProgress;
                CurrentGameProperty.GameInstall.StatusChanged += PreloadDownloadStatus;
                SpawnPreloadDialogBox();
                return;
            }

            string ver = CurrentGameProperty.GameVersion.GetGameVersionApiPreload()?.VersionString;

            try
            {
                if (CurrentGameProperty.GameVersion.IsGameHasDeltaPatch())
                {
                    PreloadDialogBox.Title = string.Format(Lang._HomePage.PreloadNotifDeltaDetectTitle, ver);
                    PreloadDialogBox.Message = Lang._HomePage.PreloadNotifDeltaDetectSubtitle;
                    DownloadPreBtn.Visibility = Visibility.Collapsed;
                    SpawnPreloadDialogBox();
                    return;
                }

                if (!await CurrentGameProperty.GameInstall.IsPreloadCompleted(PageToken.Token))
                {
                    PreloadDialogBox.Title = string.Format(Lang._HomePage.PreloadNotifTitle, ver);
                }
                else
                {
                    PreloadDialogBox.Title = Lang._HomePage.PreloadNotifCompleteTitle;
                    PreloadDialogBox.Message = string.Format(Lang._HomePage.PreloadNotifCompleteSubtitle, ver);
                    PreloadDialogBox.IsClosable = true;
                    DownloadPreBtn.Content = UIElementExtensions.CreateIconTextGrid(
                        text: Lang._HomePage.PreloadNotifIntegrityCheckBtn,
                        iconGlyph: "",
                        iconFontFamily: "FontAwesomeSolid",
                        textWeight: FontWeights.Medium
                    );
                }
                SpawnPreloadDialogBox();
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"An error occured while trying to determine delta-patch availability\r\n{ex}", LogType.Error, true);
            }
        }

        private async void PredownloadDialog(object sender, RoutedEventArgs e)
        {
            ((Button)sender).IsEnabled = false;

            PauseDownloadPreBtn.Visibility = Visibility.Visible;
            ResumeDownloadPreBtn.Visibility = Visibility.Collapsed;
            PreloadDialogBox.IsClosable = false;

            try
            {
                // Prevent device from sleep
                Sleep.PreventSleep(ILoggerHelper.GetILogger());
                // Set the notification trigger to "Running" state
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Running);

                IsSkippingUpdateCheck = true;
                DownloadPreBtn.Visibility = Visibility.Collapsed;
                if (CurrentGameProperty.GameInstall.IsUseSophon)
                {
                    ProgressPreSophonStatusGrid.Visibility = Visibility.Visible;
                    ProgressPreStatusGrid.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ProgressPreStatusGrid.Visibility = Visibility.Visible;
                }
                ProgressPreButtonGrid.Visibility = Visibility.Visible;
                PreloadDialogBox.Title = Lang._HomePage.PreloadDownloadNotifbarTitle;
                PreloadDialogBox.Message = Lang._HomePage.PreloadDownloadNotifbarSubtitle;

                CurrentGameProperty.GameInstall.ProgressChanged += PreloadDownloadProgress;
                CurrentGameProperty.GameInstall.StatusChanged += PreloadDownloadStatus;

                int verifResult = 0;
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                while (verifResult != 1)
                {
                    await CurrentGameProperty.GameInstall.StartPackageDownload(true);

                    PauseDownloadPreBtn.IsEnabled = false;
                    PreloadDialogBox.Title = Lang._HomePage.PreloadDownloadNotifbarVerifyTitle;

                    verifResult = await CurrentGameProperty.GameInstall.StartPackageVerification();

                    // Restore sleep before the dialog
                    // so system won't be stuck when download is finished because of the download verified dialog
                    Sleep.RestoreSleep();

                    switch (verifResult)
                    {
                        case -1:
                            ReturnToHomePage();
                            return;
                        case 1:
                            await Dialog_PreDownloadPackageVerified(this);
                            ReturnToHomePage();
                            return;
                    }
                }

                // Set the notification trigger to "Completed" state
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Completed);

                // If the current window is not in focus, then spawn the notification toast
                if (WindowUtility.IsCurrentWindowInFocus())
                {
                    return;
                }

                string gameNameLocale = LauncherMetadataHelper.GetTranslatedCurrentGameTitleRegionString();

                WindowUtility.Tray_ShowNotification(
                                                    string.Format(Lang._NotificationToast.GamePreloadCompleted_Title, gameNameLocale),
                                                    Lang._NotificationToast.GenericClickNotifToGoBack_Subtitle
                                                   );
            }
            catch (OperationCanceledException)
            {
                LogWriteLine("Pre-Download paused!", LogType.Warning);
                // Set the notification trigger
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Cancelled);
            }
            catch (Exception ex)
            {
                LogWriteLine($"An error occurred while starting preload process: {ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
                // Set the notification trigger
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Cancelled);
            }
            finally
            {
                IsSkippingUpdateCheck = false;
                CurrentGameProperty.GameInstall.ProgressChanged -= PreloadDownloadProgress;
                CurrentGameProperty.GameInstall.StatusChanged -= PreloadDownloadStatus;
                CurrentGameProperty.GameInstall.Flush();

                // Turn the sleep back on
                Sleep.RestoreSleep();
            }
        }

        private void PreloadDownloadStatus(object sender, TotalPerFileStatus e)
        {
            DispatcherQueue?.TryEnqueue(() => ProgressPrePerFileStatusFooter.Text = e.ActivityStatus);
        }

        private void PreloadDownloadProgress(object sender, TotalPerFileProgress e)
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                string installDownloadSpeedString = SummarizeSizeSimple(e.ProgressAllSpeed);
                string installDownloadSizeString = SummarizeSizeSimple(e.ProgressAllSizeCurrent);
                string installDownloadPerSizeString = SummarizeSizeSimple(e.ProgressPerFileSizeCurrent);
                string downloadSizeString = SummarizeSizeSimple(e.ProgressAllSizeTotal);
                string downloadPerSizeString = SummarizeSizeSimple(e.ProgressPerFileSizeTotal);

                ProgressPreStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, installDownloadSizeString, downloadSizeString);
                ProgressPrePerFileStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, installDownloadPerSizeString, downloadPerSizeString);
                ProgressPreStatusFooter.Text = string.Format(Lang._Misc.Speed, installDownloadSpeedString);
                ProgressPreTimeLeft.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.ProgressAllTimeLeft);
                progressPreBar.Value = Math.Round(e.ProgressAllPercentage, 2);
                progressPrePerFileBar.Value = Math.Round(e.ProgressPerFilePercentage, 2);
                progressPreBar.IsIndeterminate = false;
                progressPrePerFileBar.IsIndeterminate = false;
            });
        }
        #endregion

        #region Game Install
        private async void InstallGameDialog(object sender, RoutedEventArgs e)
        {
            bool isUseSophon = CurrentGameProperty.GameInstall.IsUseSophon;
            try
            {
                // Prevent device from sleep
                Sleep.PreventSleep(ILoggerHelper.GetILogger());
                // Set the notification trigger to "Running" state
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Running);

                IsSkippingUpdateCheck = true;

                HideImageCarousel(true);

                progressRing.Value           = 0;
                progressRing.IsIndeterminate = true;
                InstallGameBtn.Visibility    = Visibility.Collapsed;
                CancelDownloadBtn.Visibility = Visibility.Visible;
                ProgressTimeLeft.Visibility  = Visibility.Visible;

                if (isUseSophon)
                {
                    SophonProgressStatusGrid.Visibility               =  Visibility.Visible;
                    SophonProgressStatusSizeDownloadedGrid.Visibility =  Visibility.Collapsed;
                    CurrentGameProperty.GameInstall.ProgressChanged  += GameInstallSophon_ProgressChanged;
                    CurrentGameProperty.GameInstall.StatusChanged    += GameInstallSophon_StatusChanged;
                }
                else
                {
                    ProgressStatusGrid.Visibility                    =  Visibility.Visible;
                    CurrentGameProperty.GameInstall.ProgressChanged += GameInstall_ProgressChanged;
                    CurrentGameProperty.GameInstall.StatusChanged   += GameInstall_StatusChanged;
                }

                int dialogResult = await CurrentGameProperty.GameInstall.GetInstallationPath();
                switch (dialogResult)
                {
                    case < 0:
                        return;
                    case 0:
                        CurrentGameProperty.GameInstall.ApplyGameConfig();
                        return;
                }

                if (CurrentGameProperty.GameInstall.IsUseSophon)
                {
                    DownloadModeLabel.Visibility = Visibility.Visible;
                    DownloadModeLabelText.Text   = Lang._Misc.DownloadModeLabelSophon;
                }

                int  verifResult;
                bool skipDialog = false;
                while ((verifResult = await CurrentGameProperty.GameInstall.StartPackageVerification()) == 0)
                {
                    await CurrentGameProperty.GameInstall.StartPackageDownload(skipDialog);
                    skipDialog = true;
                }

                if (verifResult == -1)
                {
                    CurrentGameProperty.GameInstall.ApplyGameConfig(true);
                    return;
                }

                await CurrentGameProperty.GameInstall.StartPackageInstallation();
                CurrentGameProperty.GameInstall.ApplyGameConfig(true);
                if (CurrentGameProperty.GameInstall.StartAfterInstall &&
                    CurrentGameProperty.GameVersion.IsGameInstalled())
                    StartGame(null, null);

                // Set the notification trigger to "Completed" state
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Completed);

                // If the current window is not in focus, then spawn the notification toast
                if (WindowUtility.IsCurrentWindowInFocus())
                {
                    return;
                }

                string gameNameLocale = LauncherMetadataHelper.GetTranslatedCurrentGameTitleRegionString();
                WindowUtility.Tray_ShowNotification(
                                                    string.Format(Lang._NotificationToast.GameInstallCompleted_Title,
                                                                  gameNameLocale),
                                                    string
                                                       .Format(Lang._NotificationToast.GameInstallCompleted_Subtitle,
                                                               gameNameLocale)
                                                   );
            }
            catch (TaskCanceledException)
            {
                LogWriteLine($"Installation cancelled for game {CurrentGameProperty.GameVersion.GamePreset.ZoneFullname}");
                // Set the notification trigger
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Cancelled);
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Installation cancelled for game {CurrentGameProperty.GameVersion.GamePreset.ZoneFullname}");
                // Set the notification trigger
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Cancelled);
            }
            catch (NotSupportedException ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex);
                // Set the notification trigger
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Cancelled);

                IsPageUnload = true;
                LogWriteLine($"Error while installing game {CurrentGameProperty.GameVersion.GamePreset.ZoneFullname}\r\n{ex}",
                              LogType.Error, true);
                
                await SpawnDialog(Lang._HomePage.InstallFolderRootTitle,
                            Lang._HomePage.InstallFolderRootSubtitle,
                            Content,
                            Lang._Misc.Close,
                            null, null, ContentDialogButton.Close, ContentDialogTheme.Error);
            }
            catch (NullReferenceException ex)
            {
                // Set the notification trigger
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Cancelled);

                IsPageUnload = true;
                LogWriteLine($"Error while installing game {CurrentGameProperty.GameVersion.GamePreset.ZoneFullname}\r\n{ex}",
                             LogType.Error, true);
                ErrorSender.SendException(new
                                              NullReferenceException("Collapse was not able to complete post-installation tasks, but your game has been successfully updated.\r\t" +
                                                                     $"Please report this issue to our GitHub here: https://github.com/CollapseLauncher/Collapse/issues/new or come back to the launcher and make sure to use Repair Game in Game Settings button later.\r\nThrow: {ex}",
                                                                     ex));
            }
            catch (TimeoutException ex)
            {
                // Set the notification trigger
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Cancelled);
                IsPageUnload = true;
                string exMessage = $"Timeout occurred when trying to install {CurrentGameProperty.GameVersion.GamePreset.ZoneFullname}.\r\n\t" +
                             $"Check stability of your internet! If your internet speed is slow, please lower the download thread count.\r\n\t" +
                             $"**WARNING** Changing download thread count WILL reset your download from 0, and you have to delete the existing download chunks manually!" +
                             $"\r\n{ex}";
                
                string exTitleLocalized = string.Format(Lang._HomePage.Exception_DownloadTimeout1, CurrentGameProperty.GameVersion.GamePreset.ZoneFullname);
                string exMessageLocalized = string.Format($"{exTitleLocalized}\r\n\t" +
                                                          $"{Lang._HomePage.Exception_DownloadTimeout2}\r\n\t" +
                                                          $"{Lang._HomePage.Exception_DownloadTimeout3}");

                LogWriteLine($"{exMessage}", LogType.Error, true);
                Exception newEx = new TimeoutException(exMessageLocalized, ex);
                ErrorSender.SendException(newEx);
            }
            catch (Exception ex)
            {
                // Set the notification trigger
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Cancelled);

                IsPageUnload = true;
                LogWriteLine($"Error while installing game {CurrentGameProperty.GameVersion.GamePreset.ZoneFullname}.\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
            finally
            {
                IsSkippingUpdateCheck = false;
                CurrentGameProperty.GameInstall.StartAfterInstall = false;

                CurrentGameProperty.GameInstall.ProgressChanged -= isUseSophon ?
                    GameInstallSophon_ProgressChanged : 
                    GameInstall_ProgressChanged;
                CurrentGameProperty.GameInstall.StatusChanged   -= isUseSophon ? 
                    GameInstallSophon_StatusChanged : 
                    GameInstall_StatusChanged;

                await Task.Delay(200);
                CurrentGameProperty.GameInstall.Flush();
                ReturnToHomePage();

                // Turn the sleep back on
                Sleep.RestoreSleep();
            }
        }

        private void GameInstall_StatusChanged(object sender, TotalPerFileStatus e)
        {
            if (DispatcherQueue.HasThreadAccess)
                GameInstall_StatusChanged_Inner(e);
            else
                DispatcherQueue?.TryEnqueue(() => GameInstall_StatusChanged_Inner(e));
        }

        private void GameInstall_StatusChanged_Inner(TotalPerFileStatus e)
        {
            ProgressStatusTitle.Text = e.ActivityStatus;
            progressPerFile.Visibility = e.IsIncludePerFileIndicator ? Visibility.Visible : Visibility.Collapsed;

            progressRing.IsIndeterminate = e.IsProgressAllIndetermined;
            progressRingPerFile.IsIndeterminate = e.IsProgressPerFileIndetermined;
        }

        private void GameInstall_ProgressChanged(object sender, TotalPerFileProgress e)
        {
            if (DispatcherQueue.HasThreadAccess)
                GameInstall_ProgressChanged_Inner(e);
            else
                DispatcherQueue?.TryEnqueue(() => GameInstall_ProgressChanged_Inner(e));
        }

        private void GameInstall_ProgressChanged_Inner(TotalPerFileProgress e)
        {
            progressRing.Value = e.ProgressAllPercentage;
            progressRingPerFile.Value = e.ProgressPerFilePercentage;
            ProgressStatusSubtitle.Text = string.Format(Lang._Misc.PerFromTo, SummarizeSizeSimple(e.ProgressAllSizeCurrent), SummarizeSizeSimple(e.ProgressAllSizeTotal));
            ProgressStatusFooter.Text = string.Format(Lang._Misc.Speed, SummarizeSizeSimple(e.ProgressAllSpeed));
            ProgressTimeLeft.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.ProgressAllTimeLeft);
        }

        private void GameInstallSophon_StatusChanged(object sender, TotalPerFileStatus e)
        {
            if (DispatcherQueue.HasThreadAccess)
                GameInstallSophon_StatusChanged_Inner(e);
            else
                DispatcherQueue?.TryEnqueue(() => GameInstallSophon_StatusChanged_Inner(e));
        }

        private void GameInstallSophon_ProgressChanged(object sender, TotalPerFileProgress e)
        {
            if (DispatcherQueue.HasThreadAccess)
                GameInstallSophon_ProgressChanged_Inner(e);
            else
                DispatcherQueue?.TryEnqueue(() => GameInstallSophon_ProgressChanged_Inner(e));
        }

        private void GameInstallSophon_StatusChanged_Inner(TotalPerFileStatus e)
        {
            SophonProgressStatusTitleText.Text = e.ActivityStatus;
            SophonProgressPerFile.Visibility = e.IsIncludePerFileIndicator ? Visibility.Visible : Visibility.Collapsed;

            SophonProgressRing.IsIndeterminate = e.IsProgressAllIndetermined;
            SophonProgressRingPerFile.IsIndeterminate = e.IsProgressPerFileIndetermined;
        }

        private void GameInstallSophon_ProgressChanged_Inner(TotalPerFileProgress e)
        {
            SophonProgressRing.Value = e.ProgressAllPercentage;
            SophonProgressRingPerFile.Value = e.ProgressPerFilePercentage;

            SophonProgressStatusSizeTotalText.Text = string.Format(Lang._Misc.PerFromTo, SummarizeSizeSimple(e.ProgressAllSizeCurrent), SummarizeSizeSimple(e.ProgressAllSizeTotal));
            SophonProgressStatusSizeDownloadedText.Text = string.Format(Lang._Misc.PerFromTo, SummarizeSizeSimple(e.ProgressPerFileSizeCurrent), SummarizeSizeSimple(e.ProgressPerFileSizeTotal));
            
            SophonProgressStatusSpeedTotalText.Text = string.Format(Lang._Misc.SpeedPerSec, SummarizeSizeSimple(Math.Max(e.ProgressAllSpeed, 0)));
            SophonProgressStatusSpeedDownloadedText.Text = string.Format(Lang._Misc.SpeedPerSec, SummarizeSizeSimple(Math.Max(e.ProgressPerFileSpeed, 0)));

            SophonProgressTimeLeft.Text = string.Format(Lang._Misc.TimeRemainHMSFormat, e.ProgressAllTimeLeft);
        }

        private void CancelInstallationProcedure(object sender, RoutedEventArgs e)
        {
            switch (GameInstallationState)
            {
                case GameInstallStateEnum.NeedsUpdate:
                case GameInstallStateEnum.InstalledHavePlugin:
                    CancelUpdateDownload();
                    break;
                case GameInstallStateEnum.InstalledHavePreload:
                    CancelPreDownload();
                    break;
                case GameInstallStateEnum.NotInstalled:
                case GameInstallStateEnum.GameBroken:
                case GameInstallStateEnum.Installed:
                    CancelInstallationDownload();
                    break;
            }
        }
        #endregion

        #region Download Cancellation
        private void CancelPreDownload()
        {
            CurrentGameProperty.GameInstall.CancelRoutine();

            PauseDownloadPreBtn.Visibility = Visibility.Collapsed;
            ResumeDownloadPreBtn.Visibility = Visibility.Visible;
            ResumeDownloadPreBtn.IsEnabled = true;
        }

        private void CancelUpdateDownload()
        {
            CurrentGameProperty.GameInstall.CancelRoutine();
        }

        private void CancelInstallationDownload()
        {
            CurrentGameProperty.GameInstall.CancelRoutine();
        }
        #endregion

        #region Game Start/Stop Method

        private CancellationTokenSource WatchOutputLog = new();
        private CancellationTokenSource ResizableWindowHookToken;
        private async void StartGame(object sender, RoutedEventArgs e)
        {
            // Initialize values
            IGameSettingsUniversal _Settings = CurrentGameProperty!.GameSettings!.AsIGameSettingsUniversal();
            PresetConfig _gamePreset = CurrentGameProperty!.GameVersion!.GamePreset!;

            var isGenshin = CurrentGameProperty!.GameVersion.GameType == GameNameType.Genshin;
            var giForceHDR = false;

            try
            {
                if (!await CheckMediaPackInstalled()) return;

                if (isGenshin)
                {
                    giForceHDR = GetAppConfigValue("ForceGIHDREnable").ToBool();
                    if (giForceHDR) GenshinHDREnforcer();
                }

                if (_Settings is { SettingsCollapseMisc: { UseAdvancedGameSettings: true, UseGamePreLaunchCommand: true } })
                {
                    var delay = _Settings.SettingsCollapseMisc.GameLaunchDelay;
                    PreLaunchCommand(_Settings);
                    if (delay > 0)
                        await Task.Delay(delay);
                }
                
                int? height = _Settings.SettingsScreen.height;
                int? width  = _Settings.SettingsScreen.width;

                Process proc = new Process();
                proc.StartInfo.FileName = Path.Combine(NormalizePath(GameDirPath)!, _gamePreset.GameExecutableName!);
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.Arguments = GetLaunchArguments(_Settings)!;
                LogWriteLine($"[HomePage::StartGame()] Running game with parameters:\r\n{proc.StartInfo.Arguments}");
                if (File.Exists(Path.Combine(GameDirPath!, "@AltLaunchMode")))
                {
                    LogWriteLine("[HomePage::StartGame()] Using alternative launch method!", LogType.Warning, true);
                    proc.StartInfo.WorkingDirectory = (CurrentGameProperty!.GameVersion.GamePreset!.ZoneName == "Bilibili" ||
                       (isGenshin && giForceHDR) ? NormalizePath(GameDirPath) :
                            Path.GetDirectoryName(NormalizePath(GameDirPath))!)!;
                }
                else
                {
                    proc.StartInfo.WorkingDirectory = NormalizePath(GameDirPath)!;
                }
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.Verb = "runas";
                proc.Start();

                if (GetAppConfigValue("EnableConsole").ToBool())
                {
                    WatchOutputLog = new CancellationTokenSource();
                    ReadOutputLog();
                }
                
                if (_Settings.SettingsCollapseScreen.UseCustomResolution && height != 0 && width != 0)
                {
                    SetBackScreenSettings(_Settings, (int)height, (int)width, CurrentGameProperty);
                }

                // Stop update check
                IsSkippingUpdateCheck = true;

                // Start the resizable window payload (also use the same token as PlaytimeToken)
                StartResizableWindowPayload(
                    _gamePreset.GameExecutableName,
                    _Settings,
                    _gamePreset.GameType, height, width);
                GameRunningWatcher(_Settings);
                
                switch (GetAppConfigValue("GameLaunchedBehavior").ToString())
                {
                    case "Minimize":
                        WindowUtility.WindowMinimize();
                        break;
                    case "ToTray":
                        WindowUtility.ToggleToTray_MainWindow();
                        break;
                    case "Nothing":
                        break;
                    default:
                        WindowUtility.WindowMinimize();
                        break;
                }

                CurrentGameProperty.GamePlaytime.StartSession(proc);

                if (GetAppConfigValue("LowerCollapsePrioOnGameLaunch").ToBool()) CollapsePrioControl(proc);

                // Set game process priority to Above Normal when GameBoost is on
                if (_Settings.SettingsCollapseMisc is { UseGameBoost: true })
            #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    Task.Run(() => Task.FromResult(_ = GameBoost_Invoke(CurrentGameProperty)));
            #pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                // Run game process watcher
                CheckRunningGameInstance(PageToken.Token);
            }
            catch (Win32Exception ex)
            {
                LogWriteLine($"There is a problem while trying to launch Game with Region: {_gamePreset.ZoneName}\r\nTraceback: {ex}", LogType.Error, true);
                ErrorSender.SendException(new Win32Exception($"There was an error while trying to launch the game!\r\tThrow: {ex}", ex));
                IsSkippingUpdateCheck = false;
            }
        }

        // Use this method to do something when game is closed
        private async void GameRunningWatcher(IGameSettingsUniversal _settings)
        {
            ArgumentNullException.ThrowIfNull(_settings);

            await Task.Delay(5000);
            while (_cachedIsGameRunning)
            {
                await Task.Delay(3000);
            }

            LogWriteLine($"{new string('=', barWidth)} GAME STOPPED {new string('=', barWidth)}", LogType.Warning, true);

            if (ResizableWindowHookToken != null)
            {
                await ResizableWindowHookToken.CancelAsync();
                ResizableWindowHookToken.Dispose();
            }

            // Stopping GameLogWatcher
            if (GetAppConfigValue("EnableConsole").ToBool())
            {
                if (WatchOutputLog == null) return;
                await WatchOutputLog.CancelAsync();
            }

            // Stop PreLaunchCommand process
            if (_settings.SettingsCollapseMisc!.GamePreLaunchExitOnGameStop) PreLaunchCommand_ForceClose();

            // Window manager on game closed
            switch (GetAppConfigValue("GameLaunchedBehavior").ToString())
            {
                case "Minimize":
                    WindowUtility.WindowRestore();
                    break;
                case "ToTray":
                    WindowUtility.CurrentWindow!.Show();
                    WindowUtility.WindowRestore();
                    break;
                case "Nothing":
                    break;
                default:
                    WindowUtility.WindowRestore();
                    break;
            }

            // Run Post Launch Command
            if (_settings.SettingsCollapseMisc.UseAdvancedGameSettings && _settings.SettingsCollapseMisc.UseGamePostExitCommand) PostExitCommand(_settings);

            // Re-enable update check
            IsSkippingUpdateCheck = false;
        }

        private static void StopGame(PresetConfig gamePreset)
        {
            ArgumentNullException.ThrowIfNull(gamePreset);
            try
            {
                Process[] gameProcess = Process.GetProcessesByName(gamePreset.GameExecutableName!.Split('.')[0]);
                foreach (var p in gameProcess)
                {
                    LogWriteLine($"Trying to stop game process {gamePreset.GameExecutableName.Split('.')[0]} at PID {p.Id}", LogType.Scheme, true);
                    p.Kill();
                }
            }
            catch (Win32Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"There is a problem while trying to stop Game with Region: {gamePreset.ZoneName}\r\nTraceback: {ex}", LogType.Error, true);
            }
        }
        #endregion

        #region Game Resizable Window Payload
        internal async void StartResizableWindowPayload(string       executableName, IGameSettingsUniversal settings,
                                                        GameNameType gameType,       int? height, int? width)
        {
            try
            {
                // Check if the game is using Resizable Window settings
                if (!settings.SettingsCollapseScreen.UseResizableWindow) return;
                ResizableWindowHookToken = new CancellationTokenSource();

                executableName = Path.GetFileNameWithoutExtension(executableName);
                string gameExecutableDirectory = CurrentGameProperty.GameVersion.GameDirPath;
                ResizableWindowHook resizableWindowHook = new ResizableWindowHook();

                // Set the pos + size reinitialization to true if the game is Honkai: Star Rail
                // This is required for Honkai: Star Rail since the game will reset its pos + size. Making
                // it impossible to use custom resolution (but since you are using Collapse, it's now
                // possible :teriStare:)
                bool isNeedToResetPos = gameType == GameNameType.StarRail;
                await Task.Run(() => resizableWindowHook.StartHook(executableName, height, width, ResizableWindowHookToken.Token,
                                                    isNeedToResetPos, ILoggerHelper.GetILogger(), gameExecutableDirectory));
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error while initializing Resizable Window payload!\r\n{ex}");
                ErrorSender.SendException(ex, ErrorType.GameError);
            }
        }

        private async void SetBackScreenSettings(IGameSettingsUniversal settingsUniversal, int height, int width,
                                                 GamePresetProperty     gameProp)
        {
            // Wait for the game to fully initialize
            await Task.Delay(20000);
            try
            {
                settingsUniversal.SettingsScreen.height = height;
                settingsUniversal.SettingsScreen.width  = width;
                settingsUniversal.SettingsScreen.Save();

                // For those stubborn game
                // Kinda unneeded but :FRICK:
                switch (gameProp.GamePreset.GameType)
                {
                    case GameNameType.Zenless:
                        var screenManagerZ = GameSettings.Zenless.ScreenManager.Load();
                        screenManagerZ.width  = width;
                        screenManagerZ.height = height;
                        screenManagerZ.Save();
                        break;
                    
                    case GameNameType.Honkai:
                        var screenManagerH = GameSettings.Honkai.ScreenSettingData.Load();
                        screenManagerH.width  = width;
                        screenManagerH.height = height;
                        screenManagerH.Save();
                        break;
                }
                
                LogWriteLine($"[SetBackScreenSettings] Completed task! {width}x{height}", LogType.Scheme, true);
            }
            catch(Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"[SetBackScreenSettings] Failed to set Screen Settings!\r\n{ex}", LogType.Error, true);
            }

        }
        #endregion

        #region Game Launch Argument Builder

        private bool RequireWindowExclusivePayload;

        internal string GetLaunchArguments(IGameSettingsUniversal _Settings)
        {
            StringBuilder parameter = new StringBuilder();

            switch (CurrentGameProperty.GameVersion.GameType)
            {
                case GameNameType.Honkai:
                {
                    if (_Settings.SettingsCollapseScreen.UseExclusiveFullscreen)
                    {
                        parameter.Append("-window-mode exclusive ");
                        RequireWindowExclusivePayload = true;
                    }

                    Size screenSize = _Settings.SettingsScreen.sizeRes;

                    byte apiID = _Settings.SettingsCollapseScreen.GameGraphicsAPI;

                    if (apiID == 4)
                    {
                        LogWriteLine("You are going to use DX12 mode in your game.\r\n\tUsing CustomScreenResolution or FullscreenExclusive value may break the game!", LogType.Warning);
                        if (_Settings.SettingsCollapseScreen.UseCustomResolution && _Settings.SettingsScreen.isfullScreen)
                        {
                            var size = ScreenProp.CurrentResolution;
                            parameter.Append($"-screen-width {size.Width} -screen-height {size.Height} ");
                        }
                        else
                            parameter.Append($"-screen-width {screenSize.Width} -screen-height {screenSize.Height} ");
                    }
                    else
                        parameter.Append($"-screen-width {screenSize.Width} -screen-height {screenSize.Height} ");

                    switch (apiID)
                    {
                        case 0:
                            parameter.Append("-force-feature-level-10-1 ");
                            break;
                        // case 1 is default
                        default:
                            parameter.Append("-force-feature-level-11-0 -force-d3d11-no-singlethreaded ");
                            break;
                        case 2:
                            parameter.Append("-force-feature-level-11-1 ");
                            break;
                        case 3:
                            parameter.Append("-force-feature-level-11-1 -force-d3d11-no-singlethreaded ");
                            break;
                        case 4:
                            parameter.Append("-force-d3d12 ");
                            break;
                    }

                    break;
                }
                case GameNameType.StarRail:
                {
                    if (_Settings.SettingsCollapseScreen.UseExclusiveFullscreen)
                    {
                        parameter.Append("-window-mode exclusive -screen-fullscreen 1 ");
                        RequireWindowExclusivePayload = true;
                    }

                    // Enable mobile mode
                    if (_Settings.SettingsCollapseMisc.LaunchMobileMode)
                    {
                        const string regLoc  = GameSettings.StarRail.Model._ValueName;
                        var          regRoot = GameSettings.Base.SettingsBase.RegistryRoot;

                        if (regRoot != null || !string.IsNullOrEmpty(regLoc))
                        {
                            var regModel = (byte[])regRoot!.GetValue(regLoc, null);

                            if (regModel != null)
                            {
                                string regB64 = Convert.ToBase64String(regModel);
                                parameter.Append($"-is_cloud 1 -platform_type CLOUD_WEB_TOUCH -graphics_setting {regB64} ");
                            }
                            else
                            {
                                LogWriteLine("Failed enabling MobileMode for HSR: regModel is null.", LogType.Error, true);
                            }
                        }
                        else
                        {
                            LogWriteLine("Failed enabling MobileMode for HSR: regRoot/regLoc is unexpectedly uninitialized.",
                                         LogType.Error, true);
                        }
                    }

                    Size screenSize = _Settings.SettingsScreen.sizeRes;

                    byte apiID = _Settings.SettingsCollapseScreen.GameGraphicsAPI;

                    if (apiID == 4)
                    {
                        LogWriteLine("You are going to use DX12 mode in your game.\r\n\tUsing CustomScreenResolution or FullscreenExclusive value may break the game!", LogType.Warning);
                        if (_Settings.SettingsCollapseScreen.UseCustomResolution && _Settings.SettingsScreen.isfullScreen)
                        {
                            var size = ScreenProp.CurrentResolution;
                            parameter.Append($"-screen-width {size.Width} -screen-height {size.Height} ");
                        }
                        else
                            parameter.Append($"-screen-width {screenSize.Width} -screen-height {screenSize.Height} ");
                    }
                    else
                        parameter.Append($"-screen-width {screenSize.Width} -screen-height {screenSize.Height} ");

                    break;
                }
                case GameNameType.Genshin:
                {
                    if (_Settings.SettingsCollapseScreen.UseExclusiveFullscreen)
                    {
                        parameter.Append("-window-mode exclusive -screen-fullscreen 1 ");
                        RequireWindowExclusivePayload = true;
                        LogWriteLine("Exclusive mode is enabled in Genshin Impact, stability may suffer!\r\nTry not to Alt+Tab when game is on its loading screen :)", LogType.Warning, true);
                    }

                    // Enable mobile mode
                    if (_Settings.SettingsCollapseMisc.LaunchMobileMode)
                        parameter.Append("use_mobile_platform -is_cloud 1 -platform_type CLOUD_THIRD_PARTY_MOBILE ");

                    Size screenSize = _Settings.SettingsScreen.sizeRes;

                    byte apiID = _Settings.SettingsCollapseScreen.GameGraphicsAPI;

                    if (apiID == 4)
                    {
                        LogWriteLine("You are going to use DX12 mode in your game.\r\n\tUsing CustomScreenResolution or FullscreenExclusive value may break the game!", LogType.Warning);
                        if (_Settings.SettingsCollapseScreen.UseCustomResolution && _Settings.SettingsScreen.isfullScreen)
                        {
                            var size = ScreenProp.CurrentResolution;
                            parameter.Append($"-screen-width {size.Width} -screen-height {size.Height} ");
                        }
                        else
                            parameter.Append($"-screen-width {screenSize.Width} -screen-height {screenSize.Height} ");
                    }
                    else
                        parameter.Append($"-screen-width {screenSize.Width} -screen-height {screenSize.Height} ");

                    break;
                }
                case GameNameType.Zenless:
                {
                    // does not support exclusive mode at all
                    // also doesn't properly support dx12 or dx11 st
                
                    if (_Settings.SettingsCollapseScreen.UseCustomResolution)
                    {
                        Size screenSize = _Settings.SettingsScreen.sizeRes;
                        parameter.Append($"-screen-width {screenSize.Width} -screen-height {screenSize.Height} ");
                    }

                    break;
                }
            }

            if (_Settings.SettingsCollapseScreen.UseBorderlessScreen)
            {
                parameter.Append("-popupwindow ");
            }

            if (!_Settings.SettingsCollapseMisc.UseCustomArguments)
            {
                return parameter.ToString();
            }

            string customArgs = _Settings.SettingsCustomArgument.CustomArgumentValue;
            if (!string.IsNullOrEmpty(customArgs))
                parameter.Append(customArgs);

            return parameter.ToString();
        }

        public string CustomArgsValue
        {
            get => CurrentGameProperty?.GameSettings?.SettingsCustomArgument.CustomArgumentValue;
            set
            {
                if (CurrentGameProperty.GameSettings == null)
                    return;

                CurrentGameProperty.GameSettings.SettingsCustomArgument.CustomArgumentValue = value;
            }
        }

        public bool UseCustomArgs
        {
            get => CurrentGameProperty?.GameSettings?.SettingsCollapseMisc.UseCustomArguments ?? false;
            set
            {
                if (CurrentGameProperty.GameSettings == null)
                    return;

                CustomArgsTextBox.IsEnabled = CustomStartupArgsSwitch.IsOn;
                CurrentGameProperty.GameSettings.SettingsCollapseMisc.UseCustomArguments = value;
            } 
            
        }
        
        public bool UseCustomBGRegion
        {
            get
            {
                bool value = CurrentGameProperty?.GameSettings?.SettingsCollapseMisc?.UseCustomRegionBG ?? false;
                ChangeGameBGButton.IsEnabled = value;
                string path = CurrentGameProperty?.GameSettings?.SettingsCollapseMisc?.CustomRegionBGPath ?? "";
                BGPathDisplay.Text = Path.GetFileName(path);
                return value;
            }
            set
            {
                ChangeGameBGButton.IsEnabled = value;

                if (CurrentGameProperty?.GameSettings == null)
                    return;

                var regionBgPath = CurrentGameProperty.GameSettings.SettingsCollapseMisc.CustomRegionBGPath;
                if (string.IsNullOrEmpty(regionBgPath) || !File.Exists(regionBgPath))
                {
                    regionBgPath = Path.GetFileName(GetAppConfigValue("CustomBGPath").ToString());
                    CurrentGameProperty.GameSettings.SettingsCollapseMisc
                        .CustomRegionBGPath = regionBgPath;
                }

                CurrentGameProperty.GameSettings.SettingsCollapseMisc.UseCustomRegionBG = value;
                CurrentGameProperty.GameSettings.SaveBaseSettings();
                m_mainPage?.ChangeBackgroundImageAsRegionAsync();

                BGPathDisplay.Text = Path.GetFileName(regionBgPath);
            } 
        }
        #endregion

        #region Media Pack
        public async Task<bool> CheckMediaPackInstalled()
        {
            if (CurrentGameProperty.GameVersion.GameType != GameNameType.Honkai) return true;

            RegistryKey reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\WindowsFeatures\WindowsMediaVersion");
            if (reg != null)
                return true;

            LogWriteLine($"Media pack is not installed!\r\n\t" +
                        $"If you encounter the 'cry_ware_unity' error, run this script as an administrator:\r\n\t" +
                        $"{Path.Combine(AppExecutableDir, "Misc", "InstallMediaPack.cmd")}", LogType.Warning, true);

            // Skip dialog if user asked before
            if (GetAppConfigValue("HI3IgnoreMediaPack").ToBool())
                return true;

            switch (await Dialog_NeedInstallMediaPackage(Content))
            {
                case ContentDialogResult.Primary:
                    TryInstallMediaPack();
                    break;
                case ContentDialogResult.Secondary:
                    SetAndSaveConfigValue("HI3IgnoreMediaPack", true);
                    return true;
            }
            return false;
        }

        public async void TryInstallMediaPack()
        {
            try
            {
                Process proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(AppExecutableDir, "Misc", "InstallMediaPack.cmd"),
                        UseShellExecute = true,
                        Verb = "runas"
                    }
                };

                ShowLoadingPage.ShowLoading(Lang._Dialogs.InstallingMediaPackTitle,
                                            Lang._Dialogs.InstallingMediaPackSubtitle);
                MainFrameChanger.ChangeMainFrame(typeof(BlankPage));
                proc.Start();
                await proc.WaitForExitAsync();
                ShowLoadingPage.ShowLoading(Lang._Dialogs.InstallingMediaPackTitle,
                                            Lang._Dialogs.InstallingMediaPackSubtitleFinished);
                await Dialog_InstallMediaPackageFinished(Content);
                MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
            }
            catch
            {
                // ignore
            }
        }
        #endregion

        #region Exclusive Window Payload
        public async void StartExclusiveWindowPayload()
        {
            IntPtr _windowPtr = ProcessChecker.GetProcessWindowHandle(CurrentGameProperty.GameVersion.GamePreset.GameExecutableName ?? "");
            await Task.Delay(1000);
            Windowing.HideWindow(_windowPtr);
            await Task.Delay(1000);
            Windowing.ShowWindow(_windowPtr);
        }
        #endregion

        #region Game Log Method
        private async void ReadOutputLog()
        {
            var saveGameLog = GetAppConfigValue("IncludeGameLogs").ToBool();
            InitializeConsoleValues();
            
            // JUST IN CASE
            // Sentry issue ref : COLLAPSE-LAUNCHER-55; Event ID: 13059407
            if (int.IsNegative(barWidth)) barWidth = 30;
            
            LogWriteLine($"{new string('=', barWidth)} GAME STARTED {new string('=', barWidth)}", LogType.Warning,
                         true);
            LogWriteLine($"Are Game logs getting saved to Collapse logs: {saveGameLog}", LogType.Scheme, true);
            
            try
            {
                string logPath = Path.Combine(CurrentGameProperty.GameVersion.GameDirAppDataPath,
                                              CurrentGameProperty.GameVersion.GameOutputLogName);
                if (!Directory.Exists(Path.GetDirectoryName(logPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                
                if (CurrentGameProperty.GamePreset.GameType == GameNameType.Zenless)
                {
                    var logDir = Path.Combine(CurrentGameProperty.GameVersion.GameDirPath,
                                              @"ZenlessZoneZero_Data\Persistent\LogDir\");

                    _ = Directory.CreateDirectory(logDir); // Always ensure that the LogDir will always be created.

                    var newLog = await FileUtility.WaitForNewFileAsync(logDir, 20000);
                    if (!newLog)
                    {
                        LogWriteLine("Cannot get Zenless' log file due to timeout! Your computer too fast XD",
                                     LogType.Warning, saveGameLog);
                        return;
                    }

                    var logPat = FileUtility.GetLatestFile(logDir, "NAP_*.log");

                    if (!string.IsNullOrEmpty(logPat)) logPath = logPat;
                }
                else
                {
                    // If the log file exist beforehand, move it and make a new one
                    if (File.Exists(logPath))
                    {
                        FileUtility.RenameFileWithPrefix(logPath, "-old", true);
                    } 
                }
                
                LogWriteLine($"Reading Game's log file from {logPath}", LogType.Default, saveGameLog);

                await using FileStream fs =
                    new FileStream(logPath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
                using StreamReader reader = new StreamReader(fs);
                while (true)
                {
                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync(WatchOutputLog.Token);
                        if (RequireWindowExclusivePayload && line == "MoleMole.MonoGameEntry:Awake()")
                        {
                            StartExclusiveWindowPayload();
                            RequireWindowExclusivePayload = false;
                        }

                        LogWriteLine(line!, LogType.Game, saveGameLog);
                    }

                    await Task.Delay(100, WatchOutputLog.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore when cancelled
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"There were a problem in Game Log Reader\r\n{ex}", LogType.Error);
            }
        }
        #endregion

        #region Open Button Method
        private async void OpenGameFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string gameFolder = NormalizePath(GameDirPath);
                LogWriteLine($"Opening Game Folder:\r\n\t{gameFolder}");

                await Task.Run(() =>
                    new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            UseShellExecute = true,
                            FileName = "explorer.exe",
                            Arguments = gameFolder
                        }
                    }.Start());
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed when trying to open game folder!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async void OpenCacheFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string cacheFolder = CurrentGameProperty.GameVersion.GameDirAppDataPath;
            LogWriteLine($"Opening Game Folder:\r\n\t{cacheFolder}");
            try
            {
                await Task.Run(() =>
                    new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            UseShellExecute = true,
                            FileName = "explorer.exe",
                            Arguments = cacheFolder
                        }
                    }.Start());
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed when trying to open game cache folder!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async void OpenScreenshotFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string ScreenshotFolder = Path.Combine(NormalizePath(GameDirPath), CurrentGameProperty.GameVersion.GamePreset.GameType switch
            {
                GameNameType.StarRail => $"{Path.GetFileNameWithoutExtension(CurrentGameProperty.GameVersion.GamePreset.GameExecutableName)}_Data\\ScreenShots",
                _ => "ScreenShot"
            });

            LogWriteLine($"Opening Screenshot Folder:\r\n\t{ScreenshotFolder}");

            if (!Directory.Exists(ScreenshotFolder))
                Directory.CreateDirectory(ScreenshotFolder);

            try
            {
                await Task.Run(() => 
                    new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            UseShellExecute = true,
                            FileName = "explorer.exe",
                            Arguments = ScreenshotFolder
                        }
                    }.Start());
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed when trying to open game screenshot folder!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private async void CleanupFilesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GameStartupSetting.Flyout.Hide();
                if (CurrentGameProperty is { GameInstall: not null })
                    await CurrentGameProperty.GameInstall.CleanUpGameFiles();
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
            }
        }
        #endregion

        #region Game Management Buttons
        private void RepairGameButton_Click(object sender, RoutedEventArgs e)
        {
            m_mainPage!.InvokeMainPageNavigateByTag("repair");
        }

        private async void UninstallGameButton_Click(object sender, RoutedEventArgs e)
        {
            if (await CurrentGameProperty.GameInstall.UninstallGame())
            {
                MainFrameChanger.ChangeMainFrame(typeof(HomePage));
            }
        }

        private void ConvertVersionButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrameChanger.ChangeWindowFrame(typeof(InstallationConvert));
        }

        private async void StopGameButton_Click(object sender, RoutedEventArgs e)
        {
            if (await Dialog_StopGame(this) != ContentDialogResult.Primary) return;
            StopGame(CurrentGameProperty.GameVersion.GamePreset);
        }

        private async void ChangeGameBGButton_Click(object sender, RoutedEventArgs e)
        {
            var file = await FileDialogNative.GetFilePicker(ImageLoaderHelper.SupportedImageFormats);
            if (string.IsNullOrEmpty(file)) return;

            var currentMediaType = GetMediaType(file);
            
            if (currentMediaType == MediaType.StillImage)
            {
                FileStream croppedImage = await ImageLoaderHelper.LoadImage(file, true, true);
            
                if (croppedImage == null) return;
                SetAlternativeFileStream(croppedImage);
            }

            if (CurrentGameProperty?.GameSettings?.SettingsCollapseMisc != null)
            {
                CurrentGameProperty.GameSettings.SettingsCollapseMisc.CustomRegionBGPath = file;
                CurrentGameProperty.GameSettings.SaveBaseSettings();
            }
            m_mainPage?.ChangeBackgroundImageAsRegionAsync();

            BGPathDisplay.Text = Path.GetFileName(file);
        }

        private async void MoveGameLocationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!await CurrentGameProperty.GameInstall.MoveGameLocation())
                {
                    return;
                }

                CurrentGameProperty.GameInstall.ApplyGameConfig();
                ReturnToHomePage();
            }
            catch (NotSupportedException ex)
            {
                LogWriteLine($"Error has occurred while running Move Game Location tool!\r\n{ex}", LogType.Error, true);
                ex = new NotSupportedException(Lang._HomePage.GameSettings_Panel2MoveGameLocationGame_SamePath, ex);
                ErrorSender.SendException(ex, ErrorType.Warning);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error has occurred while running Move Game Location tool!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }
        #endregion

        #region Playtime
        private void ForceUpdatePlaytimeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cachedIsGameRunning) return;

            UpdatePlaytime(null, CurrentGameProperty.GamePlaytime.CollapsePlaytime);
            PlaytimeFlyout.ShowAt(PlaytimeBtn);
        }

        private async void ChangePlaytimeButton_Click(object sender, RoutedEventArgs e)
        {
            if (await Dialog_ChangePlaytime(this) != ContentDialogResult.Primary) return;

            int mins = int.Parse("0" + MinutePlaytimeTextBox.Text);
            int hours = int.Parse("0" + HourPlaytimeTextBox.Text);

            TimeSpan time = TimeSpan.FromMinutes(hours * 60 + mins);
            if (time.Hours > 99999) time = new TimeSpan(99999, 59, 0);

            CurrentGameProperty.GamePlaytime.Update(time, true);
            PlaytimeFlyout.Hide();
        }

        private async void ResetPlaytimeButton_Click(object sender, RoutedEventArgs e)
        {
            if (await Dialog_ResetPlaytime(this) != ContentDialogResult.Primary) return;

            CurrentGameProperty.GamePlaytime.Reset();
            PlaytimeFlyout.Hide();
        }

        private async void SyncDbPlaytimeButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (sender != null)
                if (button != null)
                    button.IsEnabled = false;

            try
            {
                SyncDbPlaytimeBtnGlyph.Glyph = "\uf110"; // Loading
                SyncDbPlaytimeBtnText.Text   = Lang._HomePage.GamePlaytime_Idle_SyncDbSyncing;
                await CurrentGameProperty.GamePlaytime.CheckDb(true);
                
                await Task.Delay(500);
            
                SyncDbPlaytimeBtnGlyph.Glyph = "\uf00c"; // Completed (check)
                SyncDbPlaytimeBtnText.Text   = Lang._Misc.Completed + "!";
                await Task.Delay(1000);
            
                SyncDbPlaytimeBtnGlyph.Glyph = "\uf021"; // Default
                SyncDbPlaytimeBtnText.Text   = Lang._HomePage.GamePlaytime_Idle_SyncDb;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed when trying to sync playtime to database!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
                
                SyncDbPlaytimeBtnGlyph.Glyph = "\uf021"; // Default
                SyncDbPlaytimeBtnText.Text   = Lang._HomePage.GamePlaytime_Idle_SyncDb;
            }
            finally
            {
                if (sender != null)
                    if (button != null) 
                        button.IsEnabled = true;
            }
        }

        private void NumberValidationTextBox(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            sender.MaxLength = sender == HourPlaytimeTextBox ? 5 : 3;
            args.Cancel = args.NewText.Any(c => !char.IsDigit(c));
        }

        private void UpdatePlaytime(object sender, CollapsePlaytime playtime)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                PlaytimeMainBtn.Text = FormatTimeStamp(playtime.TotalPlaytime);
                HourPlaytimeTextBox.Text = (playtime.TotalPlaytime.Days * 24 + playtime.TotalPlaytime.Hours).ToString();
                MinutePlaytimeTextBox.Text = playtime.TotalPlaytime.Minutes.ToString();

                string lastPlayed = Lang._HomePage.GamePlaytime_Stats_NeverPlayed;
                if (playtime.LastPlayed != null)
                {
                    DateTime? last = playtime.LastPlayed?.ToLocalTime();
                    lastPlayed = string.Format(Lang._HomePage.GamePlaytime_DateDisplay, last?.Day,
                                                      last?.Month, last?.Year, last?.Hour, last?.Minute);
                }

                PlaytimeStatsDaily.Text       = FormatTimeStamp(playtime.DailyPlaytime);
                PlaytimeStatsWeekly.Text      = FormatTimeStamp(playtime.WeeklyPlaytime);
                PlaytimeStatsMonthly.Text     = FormatTimeStamp(playtime.MonthlyPlaytime);
                PlaytimeStatsLastSession.Text = FormatTimeStamp(playtime.LastSession);
                PlaytimeStatsLastPlayed.Text  = lastPlayed;
            });
            return;

            static string FormatTimeStamp(TimeSpan time) => string.Format(Lang._HomePage.GamePlaytime_Display, time.Days * 24 + time.Hours, time.Minutes);
        }

        private void ShowPlaytimeStatsFlyout(object sender, RoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout(PlaytimeBtn);
        }

        private void HidePlaytimeStatsFlyout(object sender, PointerRoutedEventArgs e)
        {
            PlaytimeStatsFlyout.Hide();
            PlaytimeStatsToolTip.IsOpen = false;
        }

        private void PlaytimeStatsFlyout_OnOpened(object sender, object e)
        {
            // Match PlaytimeStatsFlyout and set its transition animation offset to 0 (but keep animation itself)
            IReadOnlyList<Popup> popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(PlaytimeBtn.XamlRoot);
            foreach (var popup in popups.Where(x => x.Child is FlyoutPresenter {Content: Grid {Tag: "PlaytimeStatsFlyoutGrid"}}))
            {
                var transition = popup.ChildTransitions[0] as PopupThemeTransition;
                transition!.FromVerticalOffset = 0;
            }
        }
#nullable restore
        #endregion

        #region Game Update Dialog
        private async void UpdateGameDialog(object sender, RoutedEventArgs e)
        {
            bool isUseSophon = CurrentGameProperty.GameInstall.IsUseSophon;

            HideImageCarousel(true);

            try
            {
                // Prevent device from sleep
                Sleep.PreventSleep(ILoggerHelper.GetILogger());
                // Set the notification trigger to "Running" state
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Running);

                IsSkippingUpdateCheck = true;

                UpdateGameBtn.Visibility = Visibility.Collapsed;
                CancelDownloadBtn.Visibility = Visibility.Visible;

                if (isUseSophon)
                {
                    SophonProgressStatusGrid.Visibility = Visibility.Visible;
                    CurrentGameProperty.GameInstall.ProgressChanged += GameInstallSophon_ProgressChanged;
                    CurrentGameProperty.GameInstall.StatusChanged += GameInstallSophon_StatusChanged;
                }
                else
                {
                    ProgressStatusGrid.Visibility = Visibility.Visible;
                    CurrentGameProperty.GameInstall.ProgressChanged += GameInstall_ProgressChanged;
                    CurrentGameProperty.GameInstall.StatusChanged += GameInstall_StatusChanged;
                }

                int verifResult;
                bool skipDialog = false;
                while ((verifResult = await CurrentGameProperty.GameInstall.StartPackageVerification()) == 0)
                {
                    await CurrentGameProperty.GameInstall.StartPackageDownload(skipDialog);
                    skipDialog = true;
                }
                if (verifResult == -1)
                {
                    return;
                }

                await CurrentGameProperty.GameInstall.StartPackageInstallation();
                CurrentGameProperty.GameInstall.ApplyGameConfig(true);
                if (CurrentGameProperty.GameInstall.StartAfterInstall && CurrentGameProperty.GameVersion.IsGameInstalled())
                    StartGame(null, null);

                // Set the notification trigger to "Completed" state
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Completed);

                // If the current window is not in focus, then spawn the notification toast
                if (WindowUtility.IsCurrentWindowInFocus())
                {
                    return;
                }

                string gameNameLocale    = LauncherMetadataHelper.GetTranslatedCurrentGameTitleRegionString();
                string gameVersionString = CurrentGameProperty.GameVersion.GetGameVersionApi()?.VersionString;

                WindowUtility.Tray_ShowNotification(
                                                    string.Format(Lang._NotificationToast.GameUpdateCompleted_Title,    gameNameLocale),
                                                    string.Format(Lang._NotificationToast.GameUpdateCompleted_Subtitle, gameNameLocale, gameVersionString)
                                                   );
            }
            catch (TaskCanceledException)
            {
                // Set the notification trigger
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Cancelled);
                LogWriteLine("Update cancelled!", LogType.Warning);
            }
            catch (OperationCanceledException)
            {
                // Set the notification trigger
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Cancelled);
                LogWriteLine("Update cancelled!", LogType.Warning);
            }
            catch (NullReferenceException ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex);
                // Set the notification trigger
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Cancelled);

                IsPageUnload = true;
                LogWriteLine($"Update error on {CurrentGameProperty.GameVersion.GamePreset.ZoneFullname} game!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new NullReferenceException("Oops, the launcher cannot finalize the installation but don't worry, your game has been totally updated.\r\t" +
                    $"Please report this issue to our GitHub here: https://github.com/CollapseLauncher/Collapse/issues/new or come back to the launcher and make sure to use Repair Game in Game Settings button later.\r\nThrow: {ex}", ex));
            }
            catch (Exception ex)
            {
                // Set the notification trigger
                CurrentGameProperty.GameInstall.UpdateCompletenessStatus(CompletenessStatus.Cancelled);

                IsPageUnload = true;
                LogWriteLine($"Update error on {CurrentGameProperty.GameVersion.GamePreset.ZoneFullname} game!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
            finally
            {
                IsSkippingUpdateCheck = false;
                CurrentGameProperty.GameInstall.StartAfterInstall = false;

                CurrentGameProperty.GameInstall.ProgressChanged    -= isUseSophon ?
                    GameInstallSophon_ProgressChanged :
                    GameInstall_ProgressChanged;
                CurrentGameProperty.GameInstall.StatusChanged      -= isUseSophon ?
                    GameInstallSophon_StatusChanged :
                    GameInstall_StatusChanged;

                await Task.Delay(200);
                CurrentGameProperty.GameInstall.Flush();
                ReturnToHomePage();

                // Turn the sleep back on
                Sleep.RestoreSleep();
            }
        }
        #endregion

        #region Set Hand Cursor
        private void SetHandCursor(object sender, RoutedEventArgs e = null) =>
            (sender as UIElement)?.SetCursor(InputSystemCursor.Create(InputSystemCursorShape.Hand));
        #endregion

        #region Hyper Link Color
        private void HyperLink_OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            TextBlock textBlock = null;
            switch (sender)
            {
                case Grid grid when grid.Children[0] is TextBlock:
                    textBlock = (TextBlock)grid.Children[0];
                    break;
                case Grid grid when grid.Children[0] is CompressedTextBlock compressedTextBlock:
                    compressedTextBlock.Foreground = (Brush)Application.Current.Resources["AccentColor"];
                    return;
                case TextBlock block:
                    textBlock = block;
                    break;
            }
            if (textBlock != null)
                textBlock.Foreground = UIElementExtensions.GetApplicationResource<Brush>("AccentColor");
        }

        private void HyperLink_OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            TextBlock textBlock = null;
            switch (sender)
            {
                case Grid grid when grid.Children[0] is TextBlock:
                    textBlock = (TextBlock)grid.Children[0];
                    break;
                case Grid grid when grid.Children[0] is CompressedTextBlock compressedTextBlock:
                    compressedTextBlock.Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
                    return;
                case TextBlock block:
                    textBlock = block;
                    break;
            }
            if (textBlock != null)
                textBlock.Foreground = UIElementExtensions.GetApplicationResource<Brush>("TextFillColorPrimaryBrush");
        }
        #endregion

        #region Misc Methods
        private async void CollapsePrioControl(Process proc)
        {
            try
            {
                using (Process collapseProcess = Process.GetCurrentProcess())
                {
                    collapseProcess.PriorityBoostEnabled = false;
                    collapseProcess.PriorityClass = ProcessPriorityClass.BelowNormal;
                    LogWriteLine($"Collapse process [PID {collapseProcess.Id}] priority is set to Below Normal, " +
                                 $"PriorityBoost is off, carousel is temporarily stopped", LogType.Default, true);
                }

                await CarouselStopScroll();
                await proc.WaitForExitAsync();

                using (Process collapseProcess = Process.GetCurrentProcess())
                {
                    collapseProcess.PriorityBoostEnabled = true;
                    collapseProcess.PriorityClass = ProcessPriorityClass.Normal;
                    LogWriteLine($"Collapse process [PID {collapseProcess.Id}] priority is set to Normal, " +
                                 $"PriorityBoost is on, carousel is started", LogType.Default, true);
                }

                await CarouselRestartScroll();
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Error in Collapse Priority Control module!\r\n{ex}", LogType.Error, true);
            }
        }

        private static void GenshinHDREnforcer()
        {
            WindowsHDR GenshinHDR = new WindowsHDR();
            try
            {
                WindowsHDR.Load();
                GenshinHDR.isHDR = true;
                GenshinHDR.Save();
                LogWriteLine("Successfully forced Genshin HDR settings on!", LogType.Scheme, true);
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"There was an error trying to force enable HDR on Genshin!\r\n{ex}", LogType.Error, true);
            }
        }

        private int GameBoostInvokeTryCount { get; set; }
        private async Task GameBoost_Invoke(GamePresetProperty gameProp)
        {
#nullable enable
            // Init new target process
            Process? toTargetProc = null;
            try
            {
                // Try catching the non-zero MainWindowHandle pointer and assign it to "toTargetProc" variable by using GetGameProcessWithActiveWindow()
                while ((toTargetProc = gameProp.GetGameProcessWithActiveWindow()) == null)
                {
                    await Task.Delay(1000); // Waiting the process to be found and assigned to "toTargetProc" variable.
                    // This is where the magic happen. When the "toTargetProc" doesn't meet the comparison to be compared as null,
                    // it will instead return a non-null value and assign it to "toTargetProc" variable,
                    // which it will break the loop and execute the next code below it.
                }

                LogWriteLine($"[HomePage::GameBoost_Invoke] Found target process! Waiting 10 seconds for process initialization...\r\n\t" +
                             $"Target Process : {toTargetProc.ProcessName} [{toTargetProc.Id}]", LogType.Default, true);

                // Wait 20 (or 10 if its first try) seconds before applying
                if (GameBoostInvokeTryCount == 0)
                {
                    await Task.Delay(20000);
                }
                else
                {
                    await Task.Delay(10000);
                }

                // Check early exit
                if (toTargetProc.HasExited)
                {
                    LogWriteLine($"[HomePage::GameBoost_Invoke] Game process {toTargetProc.ProcessName} [{toTargetProc.Id}] has exited!",
                                 LogType.Warning, true);
                    return;
                }

                // Assign the priority to the process and write a log (just for displaying any info)
                toTargetProc.PriorityClass = ProcessPriorityClass.AboveNormal;
                GameBoostInvokeTryCount    = 0;
                LogWriteLine($"[HomePage::GameBoost_Invoke] Game process {toTargetProc.ProcessName} " +
                             $"[{toTargetProc.Id}] priority is boosted to above normal!", LogType.Warning, true);
            }
            catch (Exception ex) when (GameBoostInvokeTryCount < 5)
            {
                LogWriteLine($"[HomePage::GameBoost_Invoke] (Try #{GameBoostInvokeTryCount})" +
                             $"There has been error while boosting game priority to Above Normal! Retrying...\r\n" +
                             $"\tTarget Process : {toTargetProc?.ProcessName} [{toTargetProc?.Id}]\r\n{ex}",
                             LogType.Error, true);
                GameBoostInvokeTryCount++;
                _ = Task.Run(async () => { await GameBoost_Invoke(gameProp); });
            }
            catch (Exception ex)
            {
                LogWriteLine($"[HomePage::GameBoost_Invoke] There has been error while boosting game priority to Above Normal!\r\n" +
                             $"\tTarget Process : {toTargetProc?.ProcessName} [{toTargetProc?.Id}]\r\n{ex}",
                             LogType.Error, true);
            }
#nullable restore
        }
        #endregion

        #region Pre/Post Game Launch Command
        private Process _procPreGLC;

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private async void PreLaunchCommand(IGameSettingsUniversal settings)
        {
            try
            {
                var preGameLaunchCommand = settings?.SettingsCollapseMisc?.GamePreLaunchCommand;
                if (string.IsNullOrEmpty(preGameLaunchCommand)) return;

                LogWriteLine($"Using Pre-launch command : {preGameLaunchCommand}\r\n" +
                             $"Game launch is delayed by {settings.SettingsCollapseMisc.GameLaunchDelay} ms\r\n\t" +
                             $"BY USING THIS, NO SUPPORT IS PROVIDED IF SOMETHING HAPPENED TO YOUR ACCOUNT, GAME, OR SYSTEM!",
                             LogType.Warning, true);

                _procPreGLC = new Process();

                _procPreGLC.StartInfo.FileName = "cmd.exe";
                _procPreGLC.StartInfo.Arguments = "/S /C " + "\"" + preGameLaunchCommand + "\"";
                _procPreGLC.StartInfo.CreateNoWindow = true;
                _procPreGLC.StartInfo.UseShellExecute = false;
                _procPreGLC.StartInfo.RedirectStandardOutput = true;
                _procPreGLC.StartInfo.RedirectStandardError = true;

                _procPreGLC.OutputDataReceived += GLC_OutputHandler;
                _procPreGLC.ErrorDataReceived  += GLC_ErrorHandler;

                _procPreGLC.Start();

                _procPreGLC.BeginOutputReadLine();
                _procPreGLC.BeginErrorReadLine();

                await _procPreGLC.WaitForExitAsync();
                
                _procPreGLC.OutputDataReceived -= GLC_OutputHandler;
                _procPreGLC.ErrorDataReceived  -= GLC_ErrorHandler;
            }
            catch (Win32Exception ex)
            {
                LogWriteLine($"There is a problem while trying to launch Pre-Game Command with Region: " +
                             $"{CurrentGameProperty.GameVersion.GamePreset.ZoneName}\r\nTraceback: {ex}", LogType.Error, true);
                ErrorSender.SendException(new Win32Exception($"There was an error while trying to launch Pre-Launch command!\r\tThrow: {ex}", ex));
            }
            finally
            {
                _procPreGLC?.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private void PreLaunchCommand_ForceClose()
        {
            try
            {
                if (_procPreGLC == null || _procPreGLC.HasExited || _procPreGLC.Id == 0) return;

                // Kill main and child processes
                var taskKill = new Process();
                taskKill.StartInfo.FileName  = "taskkill";
                taskKill.StartInfo.Arguments = $"/F /T /PID {_procPreGLC.Id}";
                taskKill.Start();
                taskKill.WaitForExit();

                LogWriteLine("Pre-launch command has been forced to close!", LogType.Warning, true);
            }
            // Ignore external errors
            catch (InvalidOperationException ioe)
            {
                SentryHelper.ExceptionHandler(ioe);
            }
            catch (Win32Exception we)
            {
                SentryHelper.ExceptionHandler(we);
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Error when trying to close Pre-GLC!\r\n{ex}", LogType.Error, true);
            }
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private static async void PostExitCommand(IGameSettingsUniversal settings)
        {
            try
            {
                var postGameExitCommand = settings?.SettingsCollapseMisc?.GamePostExitCommand;
                if (string.IsNullOrEmpty(postGameExitCommand)) return;

                LogWriteLine($"Using Post-launch command : {postGameExitCommand}\r\n\t" +
                             $"BY USING THIS, NO SUPPORT IS PROVIDED IF SOMETHING HAPPENED TO YOUR ACCOUNT, GAME, OR SYSTEM!",
                             LogType.Warning, true);

                Process procPostGLC = new Process();

                procPostGLC.StartInfo.FileName = "cmd.exe";
                procPostGLC.StartInfo.Arguments = "/S /C " + "\"" + postGameExitCommand + "\"";
                procPostGLC.StartInfo.CreateNoWindow = true;
                procPostGLC.StartInfo.UseShellExecute = false;
                procPostGLC.StartInfo.RedirectStandardOutput = true;
                procPostGLC.StartInfo.RedirectStandardError = true;

                procPostGLC.OutputDataReceived += GLC_OutputHandler;
                procPostGLC.ErrorDataReceived  += GLC_ErrorHandler;

                procPostGLC.Start();
                procPostGLC.BeginOutputReadLine();
                procPostGLC.BeginErrorReadLine();

                await procPostGLC.WaitForExitAsync();

                procPostGLC.OutputDataReceived -= GLC_OutputHandler;
                procPostGLC.ErrorDataReceived  -= GLC_ErrorHandler;
            }
            catch (Win32Exception ex)
            {
                LogWriteLine($"There is a problem while trying to launch Post-Game Command with command:\r\n\t" +
                             $"{settings?.SettingsCollapseMisc?.GamePostExitCommand}\r\n" +
                             $"Traceback: {ex}", LogType.Error, true);
                ErrorSender.SendException(new Win32Exception($"There was an error while trying to launch Post-Exit command\r\tThrow: {ex}", ex));
            }
        }

        private static void GLC_OutputHandler(object _, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data)) LogWriteLine(e.Data, LogType.GLC, true);
        }

        private static void GLC_ErrorHandler(object _, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data)) LogWriteLine($"ERROR RECEIVED!\r\n\t" + $"{e.Data}", LogType.GLC, true);
        }
        #endregion

        #region Shortcut Creation
        private async void AddToSteamButton_Click(object sender, RoutedEventArgs e)
        {
            GameStartupSettingFlyout.Hide();

            Tuple<ContentDialogResult, bool> result = await Dialog_SteamShortcutCreationConfirm(this);

            if (result.Item1 != ContentDialogResult.Primary)
                return;

            if (await ShortcutCreator.AddToSteam(GamePropertyVault.GetCurrentGameProperty().GamePreset, result.Item2))
            {
                await Dialog_SteamShortcutCreationSuccess(this, result.Item2);
                return;
            }

            await Dialog_SteamShortcutCreationFailure(this);
        }

        private async void ShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            string folder = await FileDialogNative.GetFolderPicker(Lang._HomePage.CreateShortcut_FolderPicker);

            if (string.IsNullOrEmpty(folder))
                return;

            if (!IsUserHasPermission(folder))
            {
                await Dialog_InsufficientWritePermission(sender as UIElement, folder);
                return;
            }

            Tuple<ContentDialogResult, bool> result = await Dialog_ShortcutCreationConfirm(this, folder);

            if (result.Item1 != ContentDialogResult.Primary)
                return;

            ShortcutCreator.CreateShortcut(folder, GamePropertyVault.GetCurrentGameProperty().GamePreset, result.Item2);
            await Dialog_ShortcutCreationSuccess(this, folder, result.Item2);
        }
        #endregion

        private async void ProgressSettingsButton_OnClick(object sender, RoutedEventArgs e) => await Dialog_DownloadSettings(this, CurrentGameProperty);

        private void ApplyShadowToImageElement(object sender, RoutedEventArgs e)
        {
            if (sender is not ButtonBase { Content: Panel panel })
            {
                return;
            }

            bool isStart = true;
            foreach (Image imageElement in panel.Children.OfType<Image>())
            {
                imageElement.ApplyDropShadow(opacity: 0.5f);
                if (!isStart)
                {
                    continue;
                }

                imageElement.Opacity = 0.0f;
                imageElement.Loaded += (_, _) =>
                                       {
                                           Compositor compositor = imageElement.GetElementCompositor();
                                           imageElement.StartAnimationDetached(TimeSpan.FromSeconds(0.25f),
                                                                               compositor.CreateScalarKeyFrameAnimation("Opacity", 1.0f));
                                       };
                isStart = false;
            }

            foreach (ImageEx.ImageEx imageElement in panel.Children.OfType<ImageEx.ImageEx>())
            {
                imageElement.ApplyDropShadow(opacity: 0.5f);
                if (!isStart)
                {
                    continue;
                }

                imageElement.Opacity = 0.0f;
                imageElement.Loaded += (_, _) =>
                                       {
                                           Compositor compositor = imageElement.GetElementCompositor();
                                           imageElement.StartAnimationDetached(TimeSpan.FromSeconds(0.25f),
                                                                               compositor.CreateScalarKeyFrameAnimation("Opacity", 1.0f));
                                       };
                isStart = false;
            }
        }

        private bool IsPointerInsideSidePanel;
        private bool IsSidePanelCurrentlyScaledOut;

        private async void SidePanelScaleOutHoveredPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (!IsEventsPanelScaleUp) return;

            IsPointerInsideSidePanel = true;
            if (sender is not FrameworkElement elementPanel)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(0.2));
            if (IsSidePanelCurrentlyScaledOut) return;
            if (!IsPointerInsideSidePanel) return;

            var toScale    = WindowSize.WindowSize.CurrentWindowSize.PostEventPanelScaleFactor;
            var storyboard = new Storyboard();
            var transform  = (CompositeTransform)elementPanel.RenderTransform;
            transform.CenterY = elementPanel.ActualHeight + 8;
            var cubicEaseOut = new CubicEase
            {
                EasingMode = EasingMode.EaseOut
            };

            var scaleXAnim = new DoubleAnimation
            {
                From           = transform.ScaleX,
                To             = toScale,
                Duration       = new Duration(TimeSpan.FromSeconds(0.2)),
                EasingFunction = cubicEaseOut
            };
            Storyboard.SetTarget(scaleXAnim, transform);
            Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");
            storyboard.Children.Add(scaleXAnim);

            var scaleYAnim = new DoubleAnimation
            {
                From           = transform.ScaleY,
                To             = toScale,
                Duration       = new Duration(TimeSpan.FromSeconds(0.2)),
                EasingFunction = cubicEaseOut
            };
            Storyboard.SetTarget(scaleYAnim, transform);
            Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");
            storyboard.Children.Add(scaleYAnim);

            MainPage.CurrentBackgroundHandler?.Dimm();
            HideImageEventImg(true);

            IsSidePanelCurrentlyScaledOut = true;
            await storyboard.BeginAsync();
        }

        private async void SidePanelScaleInHoveredPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!IsEventsPanelScaleUp) return;

            IsPointerInsideSidePanel = false;
            if (sender is not FrameworkElement elementPanel)
            {
                return;
            }

            if (!IsSidePanelCurrentlyScaledOut) return;

            MainPage.CurrentBackgroundHandler?.Undimm();
            HideImageEventImg(false);

            var storyboard = new Storyboard();
            var transform  = (CompositeTransform)elementPanel.RenderTransform;
            transform.CenterY = elementPanel.ActualHeight + 8;
            var cubicEaseOut = new CubicEase
            {
                EasingMode = EasingMode.EaseOut
            };

            var scaleXAnim = new DoubleAnimation
            {
                From           = transform.ScaleX,
                To             = 1,
                Duration       = new Duration(TimeSpan.FromSeconds(0.25)),
                EasingFunction = cubicEaseOut
            };
            Storyboard.SetTarget(scaleXAnim, transform);
            Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");
            storyboard.Children.Add(scaleXAnim);

            var scaleYAnim = new DoubleAnimation
            {
                From           = transform.ScaleY,
                To             = 1,
                Duration       = new Duration(TimeSpan.FromSeconds(0.25)),
                EasingFunction = cubicEaseOut
            };
            Storyboard.SetTarget(scaleYAnim, transform);
            Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");
            storyboard.Children.Add(scaleYAnim);

            await storyboard.BeginAsync();
            IsSidePanelCurrentlyScaledOut = false;
        }

        private void ElementScaleOutHoveredPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement elementPanel)
            {
                ElementScaleOutHoveredPointerEnteredInner(elementPanel);
            }
        }

        private async void ElementScaleOutHoveredPointerEnteredInner(FrameworkElement element,
            float xElevation = 0, float yElevation = -4)
        {
            Compositor compositor = this.GetElementCompositor();

            const float toScale       = 1.05f;
            Vector3     fromTranslate = new Vector3(0, 0, element.Translation.Z);
            // ReSharper disable ConstantConditionalAccessQualifier
            // ReSharper disable ConstantNullCoalescingCondition
            Vector3 toTranslate = new Vector3(-((float)(element?.ActualWidth ?? 0) * (toScale - 1f) / 2) + xElevation,
                                              -((float)(element?.ActualHeight ?? 0) * (toScale - 1f)) + yElevation,
                                              element.Translation.Z);
            // ReSharper restore ConstantConditionalAccessQualifier
            // ReSharper restore ConstantNullCoalescingCondition
            await element.StartAnimation(
                TimeSpan.FromSeconds(0.25),
                compositor.CreateVector3KeyFrameAnimation("Translation", toTranslate, fromTranslate),
                compositor.CreateVector3KeyFrameAnimation("Scale", new Vector3(toScale))
                );
        }

        private void ElementScaleInHoveredPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement elementPanel)
            {
                ElementScaleInHoveredPointerExitedInner(elementPanel);
            }
        }

        private async void ElementScaleInHoveredPointerExitedInner(FrameworkElement element,
            float xElevation = 0, float yElevation = -4)
        {
            Compositor compositor = this.GetElementCompositor();

            const float toScale = 1.05f;
            // ReSharper disable ConstantConditionalAccessQualifier
            // ReSharper disable ConstantNullCoalescingCondition
            Vector3 fromTranslate = new Vector3(0, 0, element.Translation.Z);
            Vector3 toTranslate = new Vector3(-((float)(element?.ActualWidth ?? 0) * (toScale - 1f) / 2) + xElevation,
                                              -((float)(element?.ActualHeight ?? 0) * (toScale - 1f)) + yElevation,
                                              element.Translation.Z);
            // ReSharper restore ConstantConditionalAccessQualifier
            // ReSharper restore ConstantNullCoalescingCondition
            await element.StartAnimation(
                TimeSpan.FromSeconds(0.25),
                compositor.CreateVector3KeyFrameAnimation("Translation", fromTranslate, toTranslate),
                compositor.CreateVector3KeyFrameAnimation("Scale", new Vector3(1.0f))
                );
        }

        private async void SpawnPreloadDialogBox()
        {
            PreloadDialogBox.IsOpen = true;
            PreloadDialogBox.Translation = new Vector3(0, 0, 16);
            Compositor compositor = CompositionTarget.GetCompositorForCurrentThread();

            PreloadDialogBox.Opacity = 0.0f;
            const float toScale = 0.98f;
            Vector3 toTranslate = new Vector3(-((float)(PreloadDialogBox?.ActualWidth ?? 0) * (toScale - 1f) / 2),
                -((float)(PreloadDialogBox?.ActualHeight ?? 0) * (toScale - 1f)) - 16, 0);

            await PreloadDialogBox.StartAnimation(TimeSpan.FromSeconds(0.5),
                compositor.CreateScalarKeyFrameAnimation("Opacity", 1.0f, 0.0f),
                compositor.CreateVector3KeyFrameAnimation("Scale",
                                                          new Vector3(1.0f, 1.0f, PreloadDialogBox!.Translation.Z),
                                                          new Vector3(toScale, toScale,
                                                                      PreloadDialogBox.Translation.Z)),
                compositor.CreateVector3KeyFrameAnimation("Translation", PreloadDialogBox.Translation, toTranslate)
                );
        }

        private bool? _regionPlayingRpc;
        private bool ToggleRegionPlayingRpc
        {
            get => _regionPlayingRpc ??= CurrentGameProperty.GameSettings?.AsIGameSettingsUniversal()
                                                            .SettingsCollapseMisc.IsPlayingRpc ?? false;
            set
            {
                if (CurrentGameProperty.GameSettings == null)
                    return;

                CurrentGameProperty.GameSettings.AsIGameSettingsUniversal()
                                    .SettingsCollapseMisc.IsPlayingRpc = value;
                _regionPlayingRpc = value;
                CurrentGameProperty.GameSettings.SaveSettings();
            }
        }

        private async Task CheckUserAccountControlStatus()
        {
            try
            {
                var skipChecking = GetAppConfigValue("SkipCheckingUAC").ToBool();
                if (skipChecking)
                    return;

                var enabled =
                    (int)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                                           "EnableLUA", 1)!;
                if (enabled != 1)
                {
                    var result = await SpawnDialog(Lang._Dialogs.UACWarningTitle, Lang._Dialogs.UACWarningContent, this, Lang._Misc.Close,
                                                   Lang._Dialogs.UACWarningLearnMore, Lang._Dialogs.UACWarningDontShowAgain,
                                                   ContentDialogButton.Close, ContentDialogTheme.Warning);
                    switch (result)
                    {
                        case ContentDialogResult.Primary:
                            new Process
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    UseShellExecute = true,
                                    FileName        = "https://learn.microsoft.com/windows/security/application-security/application-control/user-account-control/settings-and-configuration?tabs=reg"
                                }
                            }.Start();
                            break;
                        case ContentDialogResult.Secondary:
                            SetAndSaveConfigValue("SkipCheckingUAC", true);
                            break;
                    }
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }
    }
}
