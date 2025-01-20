using CollapseLauncher.AnimatedVisuals.Lottie;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.Loading;
using CollapseLauncher.Pages;
using CollapseLauncher.Pages.OOBE;
using CommunityToolkit.WinUI.Animations;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.Native.LibraryImport;
using Microsoft.UI.Composition;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading.Tasks;
using Windows.UI;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable RedundantExtendsListEntry
// ReSharper disable IdentifierTypo
// ReSharper disable AsyncVoidMethod

namespace CollapseLauncher
{
    public sealed partial class MainWindow : Window
    {
        private static bool _isForceDisableIntro;

        public void InitializeWindowProperties(bool startOobe = false)
        {
            try
            {
                InitializeWindowSettings();
                LoadingMessageHelper.Initialize();

                if (WindowUtility.CurrentWindowTitlebarExtendContent) WindowUtility.CurrentWindowTitlebarHeightOption = TitleBarHeightOption.Tall;

                if (IsFirstInstall || startOobe)
                {
                    _isForceDisableIntro = true;
                    WindowUtility.CurrentWindowTitlebarExtendContent = true;
                    WindowUtility.SetWindowSize(WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Width, WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Height);
                    WindowUtility.ApplyWindowTitlebarLegacyColor();
                    WindowUtility.CurrentWindowIsResizable = false;
                    WindowUtility.CurrentWindowIsMaximizable = false;
                    rootFrame.Navigate(typeof(OOBEStartUpMenu), null, new DrillInNavigationTransitionInfo());
                }
                else
                    StartMainPage();

                TitleBarFrameGrid.EnableImplicitAnimation(true);
                LoadingStatusUIContainer.EnableImplicitAnimation(true);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failure while initializing window properties!!!\r\n{ex}", LogType.Error, true);
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                //Console.ReadLine();
                throw;
            }
        }

        public void StartMainPage()
        {
            WindowUtility.SetWindowSize(WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Width, WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Height);
            
            RunIntroSequence();
            rootFrame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }

        private async void RunIntroSequence()
        {
            bool isIntroEnabled = IsIntroEnabled && !_isForceDisableIntro;
            RootFrameGrid.Opacity = 0;

            if (isIntroEnabled)
            {
                WindowUtility.SetWindowBackdrop(WindowBackdropKind.Mica);
                IAnimatedVisualSource2 newIntro = new NewLogoTitleIntro();
                {
                    IntroAnimation.Source = newIntro;
                    IntroAnimation.AnimationOptimization = PlayerAnimationOptimization.Resources;

                    IntroSequenceToggle.Visibility = Visibility.Visible;
                    IntroAnimation.Visibility = Visibility.Visible;
                    IntroAnimation.PlaybackRate = 1.5d;
                    await Task.Delay(500);
                    await IntroAnimation.PlayAsync(0, 600d / 600d, false);
                    IntroAnimation.Stop();
                }
                IntroAnimation.Source = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();

                Task rootFrameAnimTask = RootFrameGrid.StartAnimation(TimeSpan.FromSeconds(0.75),
                    RootFrameGrid.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 1, 0)
                    );
                Task introFrameAnimTask = IntroAnimationGrid.StartAnimation(TimeSpan.FromSeconds(0.75),
                    IntroAnimationGrid.GetElementCompositor().CreateScalarKeyFrameAnimation("Opacity", 0, 1)
                    );

                await Task.WhenAll(rootFrameAnimTask, introFrameAnimTask);
            }
            else
            {
                RootFrameGrid.Opacity = 1;
            }
            WindowUtility.SetWindowBackdrop(WindowBackdropKind.None);

            _isForceDisableIntro = true;
            IntroSequenceToggle.Visibility = Visibility.Collapsed;
            IntroAnimationGrid.Visibility = Visibility.Collapsed;

            if (isIntroEnabled) await Task.Delay(250);
        }

        private void InitializeAppWindowAndIntPtr()
        {
            InitializeComponent();
            Activate();
            Closed += (_, _) => { App.IsAppKilled = true; };

            // Initialize Window Handlers and register to Window Utility
            this.RegisterWindow();

            string title = "Collapse";
            if (IsPreview)
                title += " Preview";
#if DEBUG
            title += " [Debug]";
#endif
            var instanceCount = MainEntryPoint.InstanceCount;
            if (instanceCount > 1)
                title += $" - #{instanceCount}";

            WindowUtility.CurrentWindowTitle = title;
        }

        private static void LoadWindowIcon()
        {
            WindowUtility.SetWindowTitlebarIcon(AppIconLarge, AppIconSmall);
            WindowUtility.CurrentWindowTitlebarIconShowOption = IconShowOptions.HideIconAndSystemMenu;
        }

        public void InitializeWindowSettings()
        {
            InitializeAppWindowAndIntPtr();
            LoadWindowIcon();

            WindowUtility.SetWindowSize(WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Width, WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Height);

            // Check to see if customization is supported.
            m_windowSupportCustomTitle = AppWindowTitleBar.IsCustomizationSupported();

            if (m_windowSupportCustomTitle)
            {
                WindowUtility.SetWindowTitlebarDefaultDragArea();
                WindowUtility.CurrentWindowTitlebarExtendContent = true;
                WindowUtility.CurrentWindowTitlebarIconShowOption = IconShowOptions.HideIconAndSystemMenu;

                WindowUtility.CurrentWindowIsResizable = false;
                WindowUtility.CurrentWindowIsMaximizable = false;

                if (WindowUtility.CurrentAppWindow != null)
                {
                    WindowUtility.CurrentAppWindow.TitleBar.ButtonBackgroundColor =
                        new Color { A = 0, B = 0, G = 0, R = 0 };
                    WindowUtility.CurrentAppWindow.TitleBar.ButtonInactiveBackgroundColor =
                        new Color { A = 0, B = 0, G = 0, R = 0 };
                }

                // Hide system menu
                var controlsHwnd = PInvoke.FindWindowEx(WindowUtility.CurrentWindowPtr, 0, "ReunionWindowingCaptionControls", "ReunionCaptionControlsWindow");
                if (controlsHwnd != IntPtr.Zero)
                {
                    PInvoke.DestroyWindow(controlsHwnd);
                }
            }
            else
            {
                // Shouldn't happen
                // https://learn.microsoft.com/en-us/windows/apps/develop/title-bar#colors

                if (WindowUtility.CurrentOverlappedPresenter != null && WindowUtility.CurrentWindow != null)
                {
                    WindowUtility.CurrentOverlappedPresenter.IsResizable = false;
                    WindowUtility.CurrentOverlappedPresenter.IsMaximizable = false;
                    WindowUtility.CurrentWindowTitlebarExtendContent = false;
                    AppTitleBar.Visibility = Visibility.Collapsed;
                }
            }

            MainFrameChangerInvoker.WindowFrameEvent += MainFrameChangerInvoker_WindowFrameEvent;
            MainFrameChangerInvoker.WindowFrameGoBackEvent += MainFrameChangerInvoker_WindowFrameGoBackEvent;
            LauncherUpdateInvoker.UpdateEvent += LauncherUpdateInvoker_UpdateEvent;

            m_consoleCtrlHandler += ConsoleCtrlHandler;
            if (m_consoleCtrlHandler != null)
            {
                PInvoke.SetConsoleCtrlHandler(m_consoleCtrlHandler, true);
            }
        }

        private static bool ConsoleCtrlHandler(uint dwCtrlType)
        {
            ImageLoaderHelper.DestroyWaifu2X();
            return true;
        }

        private void LauncherUpdateInvoker_UpdateEvent(object sender, LauncherUpdateProperty e)
        {
            if (e.QuitFromUpdateMenu)
            {
                overlayFrame.Navigate(typeof(NullPage), null, new EntranceNavigationTransitionInfo());
                return;
            }

            if (e.IsUpdateAvailable)
            {
                overlayFrame.Navigate(typeof(UpdatePage));
            }
        }

        private void MainFrameChangerInvoker_WindowFrameEvent(object sender, MainFrameProperties e)
        {
            rootFrame.Navigate(e.FrameTo, null, e.Transition);
        }

        private void MainFrameChangerInvoker_WindowFrameGoBackEvent(object sender, EventArgs e)
        {
            if (rootFrame.CanGoBack)
                rootFrame.GoBack();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowUtility.WindowMinimize();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseApp();
        
        /// <summary>
        /// Close app and do necessary events before closing
        /// </summary>
        public void CloseApp()
        {
            SentryHelper.StopSentrySdk();
            _TrayIcon?.Dispose();
            Close();
            Application.Current.Exit();
        }
        
        private void MainWindow_OnSizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            // Recalculate non-client area size
            WindowUtility.EnableWindowNonClientArea();
        }

        private static bool IsIntroEnabled
        {
            get => LauncherConfig.IsIntroEnabled;
            set => LauncherConfig.IsIntroEnabled = value;
        }

        private void IntroSequenceToggle_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Compositor curCompositor = Compositor;
            UIElement element = sender as UIElement;
            element.StartAnimationDetached(TimeSpan.FromSeconds(0.25),
                    curCompositor.CreateScalarKeyFrameAnimation("Opacity", 1f)
                );
        }

        private void IntroSequenceToggle_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Compositor curCompositor = Compositor;
            UIElement element = sender as UIElement;
            element.StartAnimationDetached(TimeSpan.FromSeconds(0.25),
                    curCompositor.CreateScalarKeyFrameAnimation("Opacity", 0.25f)
                );
        }

        private void SetWindowCaptionLoadedCursor(object sender, RoutedEventArgs e)
            => (sender as UIElement)?.SetCursor(InputSystemCursor.Create(InputSystemCursorShape.Hand));
    }
}
