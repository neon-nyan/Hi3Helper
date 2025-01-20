using CollapseLauncher.Helper;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.Native.LibraryImport;
using Microsoft.UI.Xaml;
using System;
using System.Drawing;
using System.Threading.Tasks;
using Hi3Helper.SentryHelper;
using Microsoft.Extensions.Logging;
using static CollapseLauncher.InnerLauncherConfig;
using static CollapseLauncher.Pages.HomePage;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
#pragma warning disable CA1822

// Resharper disable all

namespace CollapseLauncher
{
    public sealed partial class TrayIcon
    {
        internal static TrayIcon Current { get; private set; }

        internal bool IsCreated
        {
            get 
            { 
                var v = CollapseTaskbar.IsCreated;
                if (!v)
                {
                    var s = new System.Diagnostics.StackTrace();
                    var c = System.Diagnostics.DiagnosticMethodInfo.Create(s.GetFrame(1));
                    LogWriteLine($"[TrayIcon] {c.Name} called when TrayIcon is not created!", LogType.Error, true);
                }
                return v;
            }
        }

        #region Locales

        private static string _popupHelp1  => Lang._Misc.Taskbar_PopupHelp1;
        private static string _popupHelp2  => Lang._Misc.Taskbar_PopupHelp2;
        private static string _showApp     => Lang._Misc.Taskbar_ShowApp;
        private static string _hideApp     => Lang._Misc.Taskbar_HideApp;
        private static string _showConsole => Lang._Misc.Taskbar_ShowConsole;
        private static string _hideConsole => Lang._Misc.Taskbar_HideConsole;
        private static string _exitApp     => Lang._Misc.Taskbar_ExitApp;

        // ReSharper disable UnusedMember.Local
        private readonly string _preview = Lang._Misc.BuildChannelPreview;
        private readonly string _stable = Lang._Misc.BuildChannelStable;
        // ReSharper restore UnusedMember.Local
        #endregion

        #region Main
        public TrayIcon()
        {
            InitializeComponent();
            
            CollapseTaskbar.Logger = LoggerInstance;
            CollapseTaskbar.SetValue(TaskbarIcon.LoggerProp, LoggerInstance);

            var instanceIndicator = "";
            var instanceCount     = MainEntryPoint.InstanceCount;
            var guid              = LauncherConfig.GetGuid(instanceCount);
            if (Environment.OSVersion.Version >= new Version(10, 0, 22621))
            {
                LogWriteLine("[TrayIcon] Initializing Tray with parameters:\r\n\t" +
                             $"GUID: {guid}\r\n\t" +
                             $"Instance Count: {instanceCount}", LogType.Scheme, true);
                CollapseTaskbar.SetValue(TaskbarIcon.IdProperty, guid);
            }
            else // Do not use static GUID on W10 and W11 21H2
            {
                var guidGet = CollapseTaskbar.GetValue(TaskbarIcon.IdProperty).ToString();
                LogWriteLine("[TrayIcon] Initializing Tray with parameters:\r\n\t" +
                             $"GUID: {guidGet}\r\n\t" +
                             $"Instance Count: {instanceCount}", LogType.Scheme, true);
            }

            if (instanceCount > 1)
            {
                instanceIndicator = $" - #{instanceCount}";
                CollapseTaskbar.SetValue(TaskbarIcon.CustomNameProperty, $"Collapse Launcher{instanceIndicator}");
            }
            
            // Bug in W10: Weird border issue
            // Verdict: disable SecondWindow ContextMenuMode, use classic.
            // if (isPreview) CollapseTaskbar.ContextMenuMode = ContextMenuMode.SecondWindow;
#if DEBUG
            CollapseTaskbar.ToolTipText =
                $"Collapse Launcher{instanceIndicator}\r\n" +
                $"{_popupHelp1}\r\n" +
                $"{_popupHelp2}";  
#else
            CollapseTaskbar.ToolTipText = 
                $"Collapse Launcher{instanceIndicator}\r\n" +
                $"{_popupHelp1}\r\n" +
                $"{_popupHelp2}";
#endif
            CloseButton.Text = _exitApp;

            // Switch toggle text to see if it's started with Start
            MainTaskbarToggle.Text = (m_appMode == AppMode.StartOnTray) ? _showApp : _hideApp;
            ConsoleTaskbarToggle.Text = (m_appMode == AppMode.StartOnTray) ? _showConsole : _hideConsole;

            CollapseTaskbar.Icon = Icon.FromHandle(LauncherConfig.AppIconSmall);
            CollapseTaskbar.Visibility = Visibility.Visible;
            
            CollapseTaskbar.TrayIcon.MessageWindow.BalloonToolTipChanged += BalloonChangedEvent;

            Current   = this;
        }

        public void Dispose()
        {
            CollapseTaskbar.Dispose();
        }
        
        private ILogger LoggerInstance => ILoggerHelper.GetILogger("TrayIcon");

        private void BalloonChangedEvent(object o, MessageWindow.BalloonToolTipChangedEventArgs args)
        {
            // Subscribe to the event when the Balloon is visible, and unsub when it's not.
            // Due to bug, this MouseEvent is not available when the notification already went to the tray.
            if (args.IsVisible)
                CollapseTaskbar.TrayIcon.MessageWindow.MouseEventReceived += NotificationOnMouseEventReceived;
            else
                CollapseTaskbar.TrayIcon.MessageWindow.MouseEventReceived -= NotificationOnMouseEventReceived;
        }

        private bool _mouseEventProcessing;
        private async void NotificationOnMouseEventReceived(object o, MessageWindow.MouseEventReceivedEventArgs args)
        {
            if (_mouseEventProcessing) return;
            if (args.MouseEvent != MouseEvent.BalloonToolTipClicked) return;
            
            _mouseEventProcessing = true;
            BringToForeground();
            await Task.Delay(250);
            _mouseEventProcessing = false;
        }
        #endregion

        #region Visibility Toggler
        /// <summary>
        /// Using H.NotifyIcon's WindowExtension to toggle visibility of main window (m_window)
        /// </summary>
        [RelayCommand]
        private void ToggleMainVisibilityButton() => ToggleMainVisibility();

        /// <summary>
        /// Toggle console visibility using LoggerConsole's DisposeConsole//AllocateConsole
        /// </summary>
        [RelayCommand]
        private void ToggleConsoleVisibilityButton() => ToggleConsoleVisibility();

        /// <summary>
        /// Toggle both main and console visibility while avoiding flip-flop condition
        /// </summary>
        [RelayCommand]
        private void ToggleAllVisibilityInvoke() => ToggleAllVisibility();

        /// <summary>
        /// Using user32's SetForegroundWindow to pull both windows to foreground
        /// </summary>
        [RelayCommand]
        private void BringToForegroundInvoke() => BringToForeground();

        /// <summary>
        /// Update tray context menu
        /// </summary>
        [RelayCommand]
        private void UpdateContextMenuInvoke() => UpdateContextMenu();

        /// <summary>
        /// Close app
        /// </summary>
        [RelayCommand]
        private void CloseApp() => (WindowUtility.CurrentWindow as MainWindow)?.CloseApp();
        #endregion

        #region Taskbar Public Methods
        /// <summary>
        /// Toggle console window visibility
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public void ToggleConsoleVisibility(bool forceShow = false)
        {
            if (LauncherConfig.GetAppConfigValue("EnableConsole").ToBool())
            {
                IntPtr consoleWindowHandle = PInvoke.GetConsoleWindow();
                if (LoggerConsole.ConsoleHandle == IntPtr.Zero) return;
                if (PInvoke.IsWindowVisible(consoleWindowHandle) && !forceShow)
                {
                    LoggerConsole.DisposeConsole();
                    ConsoleTaskbarToggle.Text = _showConsole;
                    LogWriteLine("Console is hidden!");
                }
                else
                {
                    LoggerConsole.AllocateConsole();
                    PInvoke.SetForegroundWindow(PInvoke.GetConsoleWindow());
                    ConsoleTaskbarToggle.Text = _hideConsole;
                    LogWriteLine("Console is visible!");
                }
            }
        }

        /// <summary>
        /// Toggle main window visibility
        /// </summary>
        public void ToggleMainVisibility(bool forceShow = false)
        {
            IntPtr mainWindowHandle = WindowUtility.CurrentWindowPtr;
            var    isVisible        = PInvoke.IsWindowVisible(mainWindowHandle);

            if (isVisible && !forceShow)
            {
                WindowUtility.CurrentWindow?.Hide(false);
                EfficiencyModeWrapper(true);
                MainTaskbarToggle.Text     = _showApp;
                // Increase refresh rate to 1000ms when main window is hidden
                RefreshRate = RefreshRateSlow;
                LogWriteLine("Main window is hidden!");

                // Spawn the hidden to tray toast notification
                ShowNotification(
                    Lang._NotificationToast.WindowHiddenToTray_Title,
                    Lang._NotificationToast.WindowHiddenToTray_Subtitle, NotificationIcon.None,
                    null, false, false
                    );
            }
            else
            {
                WindowUtility.CurrentWindow?.Show(false);
                EfficiencyModeWrapper(false);
                PInvoke.SetForegroundWindow(mainWindowHandle);
                MainTaskbarToggle.Text = _hideApp;
                // Revert refresh rate to its default
                RefreshRate = RefreshRateDefault;
                LogWriteLine("Main window is shown!");
            }
        }

        /// <summary>
        /// Toggle both main and console windows visibility
        /// </summary>
        public void ToggleAllVisibility()
        {
            IntPtr consoleWindowHandle = PInvoke.GetConsoleWindow();
            IntPtr mainWindowHandle    = WindowUtility.CurrentWindowPtr;
            bool   isMainWindowVisible = PInvoke.IsWindowVisible(mainWindowHandle);

            bool isConsoleVisible = LauncherConfig.GetAppConfigValue("EnableConsole").ToBool() && PInvoke.IsWindowVisible(consoleWindowHandle);

            if (isMainWindowVisible && !isConsoleVisible)
            {
                ToggleConsoleVisibility();
            }
            if (!isMainWindowVisible && isConsoleVisible)
            {
                ToggleMainVisibility();
            }
            else
            {
                ToggleConsoleVisibility();
                ToggleMainVisibility();
            }
        }

        /// <summary>
        /// Bring all windows into foreground, also brought all windows if they were hidden in taskbar.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public void BringToForeground()
        {
            IntPtr mainWindowHandle    = WindowUtility.CurrentWindowPtr;
            IntPtr consoleWindowHandle = PInvoke.GetConsoleWindow();

            bool isMainWindowVisible = PInvoke.IsWindowVisible(mainWindowHandle);

            if (LauncherConfig.GetAppConfigValue("EnableConsole").ToBool())
            {
                if (!PInvoke.IsWindowVisible(consoleWindowHandle))
                {
                    ToggleConsoleVisibility(true);
                }
                //Stupid workaround for console window not showing up using SetForegroundWindow
                //Basically do minimize then maximize action using ShowWindow 6->9 (nice)
                PInvoke.ShowWindow(consoleWindowHandle, 6);
                PInvoke.ShowWindow(consoleWindowHandle, 9);
                //SetForegroundWindow(consoleWindowHandle);
            }

            if (!isMainWindowVisible)
                ToggleMainVisibility(true);
            PInvoke.ShowWindow(mainWindowHandle, 9);
            PInvoke.SetForegroundWindow(mainWindowHandle);
        }

        /// <summary>
        /// Update tray context menu
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public void UpdateContextMenu()
        {
            if (!IsCreated) return;
            // Enable visibility toggle for console if the console is enabled
            if (LauncherConfig.GetAppConfigValue("EnableConsole").ToBool())
            {
                ConsoleTaskbarToggle.IsEnabled = true;
            }
            else
            {
                ConsoleTaskbarToggle.IsEnabled = false;
                ConsoleTaskbarToggle.Text = _hideConsole;
            }
            
            // Force refresh all text based on their respective window state
            IntPtr consoleWindowHandle = PInvoke.GetConsoleWindow();
            IntPtr mainWindowHandle    = WindowUtility.CurrentWindowPtr;
            
            bool isMainWindowVisible = PInvoke.IsWindowVisible(mainWindowHandle);
            bool isConsoleVisible    = LauncherConfig.GetAppConfigValue("EnableConsole").ToBool() && PInvoke.IsWindowVisible(consoleWindowHandle);

            ConsoleTaskbarToggle.Text = isConsoleVisible ? _hideConsole : _showConsole;
            MainTaskbarToggle.Text    = isMainWindowVisible ? _hideApp : _showApp;
        }

        /// <summary>
        /// Displays a balloon notification with the specified title,
        /// text, and predefined icon or custom icon in the taskbar for the specified time period.
        /// <remarks>This method currently does not support callback. Clicking notification will not do anything.</remarks>
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
        public void ShowNotification(string           title,
                                     string           message,
                                     NotificationIcon icon             = NotificationIcon.None,
                                     IntPtr?          customIconHandle = null,
                                     bool             largeIcon        = false,
                                     bool             sound            = true,
                                     bool             respectQuietTime = true,
                                     bool             realtime         = false)
        {
            try
            {
                if (!IsCreated) return;
                CollapseTaskbar.ShowNotification(title, message, icon, customIconHandle, largeIcon, sound, respectQuietTime, realtime);
            }
            catch (Exception e) //Just write a log if it throws an error, not that important anyway o((⊙﹏⊙))o.
            {
                LogWriteLine($"Failed when trying to send notification!\r\n\tTitle: {title}\r\n\t{message}\r\n{e}",
                             LogType.Error, true);
            }
        }
        
        #endregion

        #region Static Methods
        private static void EfficiencyModeWrapper(bool enableEfficiency)
        {
            try
            {
                H.NotifyIcon.EfficiencyMode.EfficiencyModeUtilities.SetEfficiencyMode(enableEfficiency);
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed when trying to toggle Efficiency Mode!\r\n{ex}", LogType.Error, true);
            }
        }

        #endregion
    }
}
