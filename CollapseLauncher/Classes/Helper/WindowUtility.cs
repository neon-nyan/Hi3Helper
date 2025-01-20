#nullable enable
using CollapseLauncher.Extension;
using H.NotifyIcon.Core;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.FileDialogCOM;
using Hi3Helper.Win32.Native.Enums;
using Hi3Helper.Win32.Native.LibraryImport;
using Hi3Helper.Win32.Native.ManagedTools;
using Hi3Helper.Win32.Native.Structs;
using Hi3Helper.Win32.Screen;
using Hi3Helper.Win32.TaskbarListCOM;
using Hi3Helper.Win32.ToastCOM;
using Hi3Helper.Win32.ToastCOM.Notification;
using Microsoft.Graphics.Display;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;
using Size = System.Drawing.Size;
using WindowId = Microsoft.UI.WindowId;

#pragma warning disable CA2012
namespace CollapseLauncher.Helper
{
    internal enum WindowBackdropKind
    {
        Acrylic,
        Mica,
        MicaAlt,
        None
    }

    internal static class WindowUtility
    {
        private static event EventHandler<RectInt32[]>? DragAreaChangeEvent;

        private static nint _oldMainWndProcPtr;
        private static nint _oldDesktopSiteBridgeWndProcPtr;

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, UIntPtr wParam, IntPtr lParam);

        internal static nint CurrentWindowPtr = nint.Zero;
        internal static Window? CurrentWindow;
        internal static AppWindow? CurrentAppWindow;
        internal static WindowId? CurrentWindowId;
        internal static OverlappedPresenter? CurrentOverlappedPresenter;

        private static readonly TaskbarList Taskbar = new();

        internal static DisplayArea? CurrentWindowDisplayArea
        {
            get
            {
                return !CurrentWindowId.HasValue ? null : DisplayArea.GetFromWindowId(CurrentWindowId.Value, DisplayAreaFallback.Primary);
            }
        }

        internal static DisplayInformation? CurrentWindowDisplayInformation
        {
            get
            {
                try
                {
                    DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();
                    if (dispatcherQueue.HasThreadAccess)
                    {
                        return CurrentWindowId.HasValue
                            ? DisplayInformation.CreateForWindowId(CurrentWindowId.Value)
                            : null;
                    }
                    else
                    {
                        DisplayInformation? displayInfoInit = null;
                        dispatcherQueue.TryEnqueue(() =>
                        {
                            displayInfoInit = CurrentWindowId.HasValue
                                ? DisplayInformation.CreateForWindowId(CurrentWindowId.Value)
                                : null;
                        });
                        return displayInfoInit;
                    }
                }
                catch (Exception ex)
                {
                    SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                    Logger.LogWriteLine($"An error has occured while getting display information\r\n{ex}", LogType.Error, true);
                }
                return null;
            }
        }

        internal static DisplayAdvancedColorInfo? CurrentWindowDisplayColorInfo
        {
            get
            {
                DisplayInformation? displayInfo = CurrentWindowDisplayInformation;
                return displayInfo == null ? null : displayInfo.GetAdvancedColorInfo();
            }
        }

        internal static bool CurrentWindowIsMaximizable
        {
            get => CurrentOverlappedPresenter != null && CurrentOverlappedPresenter.IsMaximizable;
            set
            {
                if (CurrentOverlappedPresenter == null)
                {
                    return;
                }

                CurrentOverlappedPresenter.IsMaximizable = value;
            }
        }

        internal static bool CurrentWindowIsResizable
        {
            get => CurrentOverlappedPresenter != null && CurrentOverlappedPresenter.IsResizable;
            set
            {
                if (CurrentOverlappedPresenter == null)
                {
                    return;
                }

                CurrentOverlappedPresenter.IsResizable = value;
            }
        }

        internal static nint CurrentWindowMonitorPtr
        {
            get
            {
                DisplayArea? displayArea = CurrentWindowDisplayArea;
                if (displayArea == null)
                {
                    return nint.Zero;
                }

                nint monitorPtr = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);
                return monitorPtr;
            }
        }

        internal static uint CurrentWindowMonitorDpi
        {
            get
            {
                const uint defaultDpiValue = 96;
                nint monitorPtr = CurrentWindowMonitorPtr;
                if (monitorPtr == nint.Zero)
                {
                    return defaultDpiValue;
                }

                if (PInvoke.GetDpiForMonitor(monitorPtr, Monitor_DPI_Type.MDT_Default, out uint dpi,
                                                out uint _) == 0)
                {
                    return dpi;
                }

                Logger.LogWriteLine($"[WindowUtility::CurrentWindowMonitorDpi] Could not get DPI for the current monitor at 0x{monitorPtr:x8}");
                return defaultDpiValue;

            }
        }

        internal static double CurrentWindowMonitorScaleFactor
            // Deliberate loss of precision
            // ReSharper disable once PossibleLossOfFraction
            => (CurrentWindowMonitorDpi * 100 + (96 >> 1)) / 96 / 100.0;

        internal static Rect CurrentWindowPosition
        {
            get => CurrentWindow?.Bounds ?? default;
            set
            {
                if (CurrentWindowPtr == nint.Zero || CurrentAppWindow == null)
                {
                    return;
                }

                if (InnerLauncherConfig.m_isWindows11)
                {
                    // We have no title bar
                    var titleBarHeight = PInvoke.GetSystemMetrics(SystemMetric.SM_CYSIZEFRAME) +
                                         PInvoke.GetSystemMetrics(SystemMetric.SM_CYCAPTION) +
                                         PInvoke.GetSystemMetrics(SystemMetric.SM_CXPADDEDBORDER);
                    value.Height -= titleBarHeight;

                    CurrentAppWindow.ResizeClient(new SizeInt32
                    {
                        Width = (int)value.Width,
                        Height = (int)value.Height
                    });
                    CurrentAppWindow.Move(new PointInt32
                    {
                        X = (int)value.X,
                        Y = (int)value.Y
                    });
                }
                else
                {
                    CurrentAppWindow.MoveAndResize(new RectInt32
                    {
                        Width = (int)value.Width,
                        Height = (int)value.Height,
                        X = (int)value.X,
                        Y = (int)value.Y
                    });
                }
            }
        }

        internal static string? CurrentWindowTitle
        {
            get => CurrentAppWindow?.Title;
            set
            {
                if (CurrentAppWindow == null)
                {
                    return;
                }

                CurrentAppWindow.Title = value;
            }
        }

        internal static bool CurrentWindowTitlebarExtendContent
        {
            get => CurrentWindow?.ExtendsContentIntoTitleBar ?? false;
            set
            {
                if (CurrentWindow == null)
                {
                    return;
                }

                CurrentWindow.ExtendsContentIntoTitleBar = value;
            }
        }

        internal static TitleBarHeightOption CurrentWindowTitlebarHeightOption
        {
            get => CurrentAppWindow?.TitleBar.PreferredHeightOption ?? TitleBarHeightOption.Standard;
            set
            {
                if (CurrentAppWindow == null)
                {
                    return;
                }

                CurrentAppWindow.TitleBar.PreferredHeightOption = value;
            }
        }

        internal static IconShowOptions CurrentWindowTitlebarIconShowOption
        {
            get => CurrentAppWindow?.TitleBar.IconShowOptions ?? IconShowOptions.ShowIconAndSystemMenu;
            set
            {
                if (CurrentAppWindow == null)
                {
                    return;
                }

                CurrentAppWindow.TitleBar.IconShowOptions = value;
            }
        }

        internal static Guid? CurrentAumidInGuid
        {
            get => field ??= Extensions.GetGuidFromString(CurrentAumid ?? "");
            set;
        }

        internal static string? CurrentAumid
        {
            get
            {
                if (field == null)
                {
                    PInvoke.GetProcessAumid(out field);
                }

                return field;
            }
            set
            {
                if (value == null)
                {
                    return;
                }

                field = value;
                PInvoke.SetProcessAumid(value);
                CurrentAumidInGuid = Extensions.GetGuidFromString(value);
            }
        }

        internal static NotificationService? CurrentToastNotificationService
        {
            get
            {
                // If toast notification service field is null, then initialize
                if (field != null)
                {
                    return field;
                }

                try
                {
                    // Get Icon location paths
                    (string iconLocationStartMenu, _)
                        = TaskSchedulerHelper.GetIconLocationPaths(
                                                                   out string? appAumIdNameAlternative,
                                                                   out _,
                                                                   out string? executablePath,
                                                                   out _);

                    // Register notification service
                    field = new NotificationService(ILoggerHelper.GetILogger("ToastCOM"));

                    // Get AumId to use
                    string? currentAumId = CurrentAumid ??= appAumIdNameAlternative;

                    // Get string for AumId registration
                    if (!string.IsNullOrEmpty(currentAumId))
                    {
                        // Initialize Toast Notification service
                        CurrentAumidInGuid = field.Initialize(
                                                              currentAumId,
                                                              executablePath ?? "",
                                                              iconLocationStartMenu,
                                                              asElevatedUser: true
                                                             );

                        // Subscribe ToastCallback
                        field.ToastCallback += Service_ToastNotificationCallback;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWriteLine($"Notification service initialization has failed, ignoring!\r\n{ex}", LogType.Error, true);
                    return null;
                }

                return field;
            }
        }

        internal static void RegisterWindow(this Window window)
        {
            // Uninstall existing drag area change
            UninstallDragAreaChangeMonitor();

            CurrentWindow = window;
            CurrentWindowPtr = WindowNative.GetWindowHandle(window);
            CurrentWindowId = Win32Interop.GetWindowIdFromWindow(CurrentWindowPtr);

            if (!CurrentWindowId.HasValue)
            {
                throw new NullReferenceException($"Window ID cannot be retrieved from pointer: 0x{CurrentWindowPtr:x8}");
            }

            CurrentAppWindow = AppWindow.GetFromWindowId(CurrentWindowId.Value);
            CurrentOverlappedPresenter = CurrentAppWindow.Presenter as OverlappedPresenter;

            // Install WndProc callback
            _oldMainWndProcPtr = InstallWndProcCallback(CurrentWindowPtr, MainWndProc);

            // Install Drag Area Change monitor
            InstallDragAreaChangeMonitor();

            // Apply fix for mouse event
            ApplyWindowTitlebarContextFix();

            // Apply fix for Window border on Windows 10
            ApplyWindowBorderFix();

            // Initialize FileDialogHandler
            FileDialogNative.InitHandlerPointer(CurrentWindowPtr);
        }

        #region Drag Area Handler

        private static void InstallDragAreaChangeMonitor()
        {
            DragAreaChangeEvent += WindowDragAreaChangeEventHandler;
        }

        private static void UninstallDragAreaChangeMonitor()
        {
            DragAreaChangeEvent -= WindowDragAreaChangeEventHandler;
        }

        private static void WindowDragAreaChangeEventHandler(object? sender, RectInt32[] dragArea)
        {
            if (sender is AppWindow appWindow)
            {
                appWindow.TitleBar.SetDragRectangles(dragArea);
            }
        }

        #endregion

        #region WndProc Handler
        private static IntPtr InstallWndProcCallback(IntPtr hwnd, WndProcDelegate wndProc)
        {
            // Install WndProc hook
            const int GWLP_WNDPROC = -4;
            WndProcDelegate mNewWndProcDelegate = wndProc;
            IntPtr pWndProc = Marshal.GetFunctionPointerForDelegate(mNewWndProcDelegate);
            return PInvoke.SetWindowLongPtr(hwnd, GWLP_WNDPROC, pWndProc);
        }

        private static IntPtr MainWndProc(IntPtr hwnd, uint msg, UIntPtr wParam, IntPtr lParam)
        {
            const uint WM_SYSCOMMAND = 0x0112;
            const uint WM_SHOWWINDOW = 0x0018;
            const uint WM_NCHITTEST = 0x0084;
            const uint WM_NCCALCSIZE = 0x0083;
            const uint WM_SETTINGCHANGE = 0x001A;
            const uint WM_ACTIVATE = 0x0006;

            switch (msg)
            {
                case WM_ACTIVATE:
                    {
                        switch (wParam)
                        {
                            case 1 when lParam == 0:
                                MainPage.CurrentBackgroundHandler?.WindowFocused();
                                break;
                            case 0 when lParam == 0:
                                MainPage.CurrentBackgroundHandler?.WindowUnfocused();
                                break;
                        }

                        break;
                    }
                case WM_SYSCOMMAND:
                    {
                        const uint SC_MAXIMIZE = 0xF030;
                        const uint SC_MINIMIZE = 0xF020;
                        const uint SC_RESTORE = 0xF120;
                        const uint SC_CLOSE = 0xF060;
                        switch (wParam)
                        {
                            case SC_CLOSE:
                                {
                                    // Deal with close message from system shell.
                                    if (CurrentWindow is MainWindow mainWindow)
                                    {
                                        mainWindow.CloseApp();
                                    }
                                    return 0;
                                }
                            case SC_MAXIMIZE:
                                {
                                    // TODO: Apply to force disable the "double-click to maximize" feature.
                                    //       The feature should expected to be disabled while m_presenter.IsResizable and IsMaximizable
                                    //       set to false. But the feature is still not respecting the changes in WindowsAppSDK 1.4.
                                    //
                                    //       Issues have been described here:
                                    //       https://github.com/microsoft/microsoft-ui-xaml/issues/8666
                                    //       https://github.com/microsoft/microsoft-ui-xaml/issues/8783

                                    // Ignore WM_SYSCOMMAND SC_MAXIMIZE message
                                    // Thank you Microsoft :)
                                    return 0;
                                }
                            case SC_MINIMIZE:
                                {
                                    MainPage.CurrentBackgroundHandler?.WindowUnfocused();
                                    if (LauncherConfig.GetAppConfigValue("MinimizeToTray").ToBool())
                                    {
                                        // Carousel is handled inside WM_SHOWWINDOW message for minimize to tray
                                        if (CurrentWindow is MainWindow mainWindow && mainWindow._TrayIcon != null)
                                        {
                                            mainWindow._TrayIcon.ToggleAllVisibility();
                                        }
                                        else TrayNullHandler("WindowUtility.MainWndProc");

                                        return 0;
                                    }

                                    InnerLauncherConfig.m_homePage?.CarouselStopScroll();
                                    break;
                                }
                            case SC_RESTORE:
                                {
                                    MainPage.CurrentBackgroundHandler?.WindowFocused();
                                    InnerLauncherConfig.m_homePage?.CarouselRestartScroll();
                                    break;
                                }
                        }

                        break;
                    }
                case WM_SHOWWINDOW:
                    {
                        if (wParam == 0)
                        {
                            InnerLauncherConfig.m_homePage?.CarouselStopScroll();
                        }
                        else
                        {
                            MainPage.CurrentBackgroundHandler?.WindowFocused();
                            InnerLauncherConfig.m_homePage?.CarouselRestartScroll();
                        }
                        break;
                    }
                case WM_NCHITTEST:
                    {
                        const int HTCLIENT = 1;
                        const int HTCAPTION = 2;
                        const int HTMINBUTTON = 8;
                        const int HTMAXBUTTON = 9;
                        const int HTRIGHT = 11;
                        const int HTTOP = 12;
                        const int HTTOPRIGHT = 14;
                        const int HTCLOSE = 20;

                        var result = PInvoke.CallWindowProc(_oldMainWndProcPtr, hwnd, msg, wParam, lParam);
                        return result switch
                        {
                            // Hide all system buttons
                            HTMINBUTTON => HTCLIENT,
                            HTMAXBUTTON => HTCLIENT,
                            HTCLOSE => HTCLIENT,
                            // Fix "Caption Resize" issue
                            HTRIGHT => HTCAPTION,
                            HTTOP => HTCAPTION,
                            HTTOPRIGHT => HTCAPTION,
                            _ => result
                        };
                    }
                case WM_NCCALCSIZE:
                    {
                        if (!InnerLauncherConfig.m_isWindows11 && wParam != 0)
                        {
                            return 0;
                        }

                        break;
                    }
                case WM_SETTINGCHANGE:
                    {
                        var setting = Marshal.PtrToStringAnsi(lParam);
                        if (setting == "ImmersiveColorSet")
                        {
                            PInvoke.SetPreferredAppMode(PInvoke.ShouldAppsUseDarkMode()
                                                               ? PreferredAppMode.AllowDark
                                                               : PreferredAppMode.Default);
                        }

                        break;
                    }
            }

            return PInvoke.CallWindowProc(_oldMainWndProcPtr, hwnd, msg, wParam, lParam);
        }

        #endregion

        #region Titlebar Methods
        private static void ApplyWindowTitlebarContextFix()
        {
            if (!CurrentWindowId.HasValue)
            {
                return;
            }

            InputNonClientPointerSource incps = InputNonClientPointerSource.GetForWindowId(CurrentWindowId.Value);
            incps.SetRegionRects(NonClientRegionKind.Close, null);
            incps.SetRegionRects(NonClientRegionKind.Minimize, null);
            EnableWindowNonClientArea();
        }

        internal static void ApplyWindowTitlebarLegacyColor()
        {
            UIElementExtensions.SetApplicationResource("WindowCaptionForeground",
                                                       InnerLauncherConfig.IsAppThemeLight
                                                           ? new Color { A = 255, B = 0, G = 0, R = 0 }
                                                           : new Color { A = 255, B = 255, G = 255, R = 255 });
            UIElementExtensions.SetApplicationResource("WindowCaptionBackground",
                                                       new SolidColorBrush(new Color { A = 0, B = 0, G = 0, R = 0 }));
            UIElementExtensions.SetApplicationResource("WindowCaptionBackgroundDisabled",
                                                       new SolidColorBrush(new Color { A = 0, B = 0, G = 0, R = 0 }));
        }

        internal static void SetWindowBackdrop(WindowBackdropKind kind)
        {
            if (CurrentWindow == null)
            {
                return;
            }

            CurrentWindow.SystemBackdrop = kind switch
            {
                WindowBackdropKind.Acrylic => new DesktopAcrylicBackdrop(),
                WindowBackdropKind.Mica => !InnerLauncherConfig.m_isWindows11
                    ? new DesktopAcrylicBackdrop()
                    : new MicaBackdrop(),
                WindowBackdropKind.MicaAlt => !InnerLauncherConfig.m_isWindows11
                    ? new DesktopAcrylicBackdrop()
                    : new MicaBackdrop { Kind = MicaKind.BaseAlt },
                _ => null
            };
        }

        internal static void SetWindowTitlebarDefaultDragArea()
        {
            if (CurrentWindow == null)
            {
                return;
            }

            double scaleFactor = CurrentWindowMonitorScaleFactor;
            RectInt32[] dragRects =
                [new(0, 0, (int)(CurrentWindow.Bounds.Width * scaleFactor), (int)(48 * scaleFactor))];

            DragAreaChangeEvent?.Invoke(CurrentAppWindow, dragRects);
        }

        internal static void SetWindowTitlebarDragArea(RectInt32[] dragAreas)
        {
            DragAreaChangeEvent?.Invoke(CurrentAppWindow, dragAreas);
        }

        internal static void SetWindowTitlebarIcon(nint smallIconPtr, nint largeIconPtr)
        {
            if (smallIconPtr == nint.Zero || largeIconPtr == nint.Zero)
            {
                return;
            }

            Windowing.SetWindowIcon(CurrentWindowPtr, smallIconPtr, largeIconPtr);
        }
        #endregion

        #region Window state methods
        private static void ApplyWindowBorderFix()
        {
            // Hide window border but keep drop shadow
            if (!InnerLauncherConfig.m_isWindows11)
            {
                var margin = new MARGINS
                {
                    cyBottomHeight = 1
                };
                PInvoke.DwmExtendFrameIntoClientArea(CurrentWindowPtr, ref margin);

                const SetWindowPosFlags flags = SetWindowPosFlags.SWP_NOSIZE
                                                | SetWindowPosFlags.SWP_NOMOVE
                                                | SetWindowPosFlags.SWP_NOZORDER
                                                | SetWindowPosFlags.SWP_FRAMECHANGED;
                PInvoke.SetWindowPos(CurrentWindowPtr, 0, 0, 0, 0, 0, flags);
            }

            var desktopSiteBridgeHwnd = PInvoke.FindWindowEx(CurrentWindowPtr, 0, "Microsoft.UI.Content.DesktopChildSiteBridge", "");
            _oldDesktopSiteBridgeWndProcPtr = InstallWndProcCallback(desktopSiteBridgeHwnd, DesktopSiteBridgeWndProc);
        }

        private static IntPtr DesktopSiteBridgeWndProc(IntPtr hwnd, uint msg, UIntPtr wParam, IntPtr lParam)
        {
            const uint WM_WINDOWPOSCHANGING = 0x0046;

            switch (msg)
            {
                case WM_WINDOWPOSCHANGING:
                    {
                        // Fix the weird 1px offset
                        var windowPos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                        if (windowPos is { x: 0, y: 1 } &&
                            windowPos.cx == (int)(WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Width * CurrentWindowMonitorScaleFactor) &&
                            windowPos.cy == (int)(WindowSize.WindowSize.CurrentWindowSize.WindowBounds.Height * CurrentWindowMonitorScaleFactor) - 1)
                        {
                            windowPos.y = 0;
                            windowPos.cy += 1;
                            Marshal.StructureToPtr(windowPos, lParam, false);
                        }

                        break;
                    }
            }

            return PInvoke.CallWindowProc(_oldDesktopSiteBridgeWndProcPtr, hwnd, msg, wParam, lParam);
        }

        internal static void SetWindowSize(int width, int height)
        {
            if (CurrentWindowPtr == nint.Zero)
                return;

            // Get the scale factor and calculate the size and offset
            double scaleFactor = CurrentWindowMonitorScaleFactor;
            int lastWindowWidth = (int)(width * scaleFactor);
            int lastWindowHeight = (int)(height * scaleFactor);

            Size desktopSize = ScreenProp.CurrentResolution;
            int xOff = desktopSize.Width / 2 - lastWindowWidth / 2;
            int yOff = desktopSize.Height / 2 - lastWindowHeight / 2;

            // Use CurrentWindowPosition to change the size and position
            CurrentWindowPosition = new Rect
            { Height = lastWindowHeight, Width = lastWindowWidth, X = xOff, Y = yOff };
        }

        internal static void WindowMinimize()
        {
            if (CurrentWindowPtr == nint.Zero)
            {
                return;
            }

            const uint WM_SYSCOMMAND = 0x0112;
            const uint SC_MINIMIZE = 0xF020;
            PInvoke.SendMessage(CurrentWindowPtr, WM_SYSCOMMAND, SC_MINIMIZE, 0);
        }

        internal static void WindowRestore()
        {
            if (CurrentWindowPtr == nint.Zero)
            {
                return;
            }

            const uint WM_SYSCOMMAND = 0x0112;
            const uint SC_RESTORE = 0xF120;
            PInvoke.SendMessage(CurrentWindowPtr, WM_SYSCOMMAND, SC_RESTORE, 0);
        }

        internal static void EnableWindowNonClientArea()
        {
            if (!CurrentWindowId.HasValue || CurrentAppWindow == null)
            {
                return;
            }

            double scaleFactor = CurrentWindowMonitorScaleFactor;
            var incps = InputNonClientPointerSource.GetForWindowId(CurrentWindowId.Value);
            RectInt32[] safeArea =
            [
                new(CurrentAppWindow.Size.Width - (int)((144 + 12) * scaleFactor), 0, (int)((144 + 12) * scaleFactor),
                    (int)(48 * scaleFactor))
            ];
            incps.SetRegionRects(NonClientRegionKind.Passthrough, safeArea);
        }

        internal static void DisableWindowNonClientArea()
        {
            if (!CurrentWindowId.HasValue || CurrentAppWindow == null)
            {
                return;
            }

            double       scaleFactor = CurrentWindowMonitorScaleFactor;
            var          incps = InputNonClientPointerSource.GetForWindowId(CurrentWindowId.Value);
            RectInt32[] safeArea = [new(0, 0, CurrentAppWindow.Size.Width, (int)(48 * scaleFactor))];
            incps.SetRegionRects(NonClientRegionKind.Passthrough, safeArea);
        }

        internal static bool IsCurrentWindowInFocus()
        {
            IntPtr currentForegroundWindow = PInvoke.GetForegroundWindow();
            return CurrentWindowPtr == currentForegroundWindow;
        }
        #endregion

        #region Tray Icon Invoker
        /// <summary>
        ///     <inheritdoc cref="TrayIcon.ToggleMainVisibility" />
        /// </summary>
        public static void ToggleToTray_MainWindow()
        {
            if (CurrentWindow is not MainWindow window) return;

            if (window._TrayIcon != null) window._TrayIcon.ToggleMainVisibility();
            else TrayNullHandler(nameof(Tray_ShowNotification));
        }

        /// <summary>
        ///     <inheritdoc cref="TrayIcon.ToggleAllVisibility" />
        /// </summary>
        public static void ToggleToTray_AllWindow()
        {
            if (CurrentWindow is not MainWindow window) return;

            if (window._TrayIcon != null) window._TrayIcon.ToggleAllVisibility();
            else TrayNullHandler(nameof(Tray_ShowNotification));
        }

        /// <summary>
        ///  <inheritdoc cref="TrayIcon.ShowNotification"/>
        /// </summary>
        /// <param name="title">The title to display on the balloon tip.</param>
        /// <param name="message">The text to display on the balloon tip.</param>
        /// <param name="icon">A symbol that indicates the severity.</param>
        /// <param name="customIconHandle">A custom icon.</param>
        /// <param name="largeIcon">True to allow large icons (Windows Vista and later).</param>
        /// <param name="sound">If false do not play the associated sound.</param>
        /// <param name="respectQuietTime">
        /// Do not display the balloon notification if the current user is in "quiet time", 
        /// which is the first hour after a new user logs into his or her account for the first time. 
        /// During this time, most notifications should not be sent or shown. 
        /// This lets a user become accustomed to a new computer system without those distractions. 
        /// Quiet time also occurs for each user after an operating system upgrade or clean installation. 
        /// A notification sent with this flag during quiet time is not queued; 
        /// it is simply dismissed unshown. The application can resend the notification later 
        /// if it is still valid at that time. <br/>
        /// Because an application cannot predict when it might encounter quiet time, 
        /// we recommended that this flag always be set on all appropriate notifications 
        /// by any application that means to honor quiet time. <br/>
        /// During quiet time, certain notifications should still be sent because 
        /// they are expected by the user as feedback in response to a user action, 
        /// for instance when he or she plugs in a USB device or prints a document.<br/>
        /// If the current user is not in quiet time, this flag has no effect.
        /// </param>
        /// <param name="realtime">
        /// Windows Vista and later. <br/>
        /// If the balloon notification cannot be displayed immediately, discard it. 
        /// Use this flag for notifications that represent real-time information 
        /// which would be meaningless or misleading if displayed at a later time.  <br/>
        /// For example, a message that states "Your telephone is ringing."
        /// </param>
        // Taken from H.NotifyIcon.TrayIcon.ShowNotification docs
        // https://github.com/HavenDV/H.NotifyIcon/blob/89356c52bedae45b1fd451531e8ac8cfe8b13086/src/libs/H.NotifyIcon.Shared/TaskbarIcon.Notifications.cs#L14
        public static void Tray_ShowNotification(string title,
                                                 string message,
                                                 NotificationIcon icon = NotificationIcon.None,
                                                 IntPtr? customIconHandle = null,
                                                 bool largeIcon = false,
                                                 bool sound = true,
                                                 bool respectQuietTime = true,
                                                 bool realtime = false)
        {
            if (CurrentWindow is not MainWindow window) return;

            if (window._TrayIcon != null)
                window._TrayIcon.ShowNotification(title, message, icon, customIconHandle, largeIcon, sound,
                                                  respectQuietTime, realtime);
            else TrayNullHandler(nameof(Tray_ShowNotification));
        }

        private static void TrayNullHandler(string caller)
        {
            Logger.LogWriteLine($"TrayIcon is null/not initialized!\r\n\tCalled by: {caller}");
        }

        private static void Service_ToastNotificationCallback(string app, string arg, Dictionary<string, string?>? userInputData)
        {
            if (CurrentWindow is not MainWindow window) return;

            // If the MainWindow is currently active, then set the window
            // to foreground.
            window._TrayIcon?.ToggleAllVisibility();

            // TODO: Make the callback actually usable on elevated app
        }
        #endregion

        #region Taskbar Methods
        public static int SetTaskBarState(nint hwnd, TaskbarState state)
        {
            return Taskbar.SetProgressState(hwnd, state);
        }

        public static int SetProgressValue(nint hwnd, ulong completed, ulong total)
        {
            return Taskbar.SetProgressValue(hwnd, completed, total);
        }

        public static int SetTaskBarState(TaskbarState state)
        {
            return Taskbar.SetProgressState(CurrentWindowPtr, state);
        }

        public static int SetProgressValue(ulong completed, ulong total)
        {
            return Taskbar.SetProgressValue(CurrentWindowPtr, completed, total);
        }
        #endregion
    }
}
