using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Image;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.Native.Enums;
using Hi3Helper.Win32.Native.LibraryImport;
using Microsoft.UI;
using Microsoft.UI.Private.Media;
using Microsoft.UI.Xaml;
using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Libwebp;
using System;
using Windows.UI;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Logger;
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher
{
    public partial class App
    {
        public static bool IsAppKilled { get; set; } = false;

        public App()
        {
            if (DebugSettings != null)
            {
            #if ENABLEFRAMECOUNTER
                DebugSettings.EnableFrameRateCounter = true;
            #endif
#if DEBUG
                DebugSettings.LayoutCycleDebugBreakLevel = LayoutCycleDebugBreakLevel.High;
                DebugSettings.LayoutCycleTracingLevel = LayoutCycleTracingLevel.High;
                DebugSettings.IsXamlResourceReferenceTracingEnabled = true;
                DebugSettings.IsBindingTracingEnabled = true;
#endif
                DebugSettings.XamlResourceReferenceFailed += static (sender, args) =>
                {
                    LogWriteLine($"[XAML_RES_REFERENCE] Sender: {sender}\r\n{args!.Message}", LogType.Error, true);
                    SentryHelper.ExceptionHandler(new Exception($"{args.Message}"), SentryHelper.ExceptionType.UnhandledXaml);
                #if !DEBUG
                    MainEntryPoint.SpawnFatalErrorConsole(new Exception(args!.Message));
                #endif
                    
                };
                DebugSettings.BindingFailed += static (sender, args) =>
                {
                    LogWriteLine($"[XAML_BINDING] Sender: {sender}\r\n{args!.Message}", LogType.Error, true);
                    SentryHelper.ExceptionHandler(new Exception($"{args.Message}"), SentryHelper.ExceptionType.UnhandledXaml);
                #if !DEBUG
                    MainEntryPoint.SpawnFatalErrorConsole(new Exception(args!.Message));
                #endif
                };
                UnhandledException += static (sender, e) =>
                {
                    LogWriteLine($"[XAML_OTHER] Sender: {sender}\r\n{e!.Exception} {e.Exception!.InnerException}", LogType.Error, true);
                    var ex = e.Exception;
                    if (ex != null)
                    {
                        SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledXaml);
                    }
#if !DEBUG
                    MainEntryPoint.SpawnFatalErrorConsole(e!.Exception);
#endif
                };
            }

            RequestedTheme = IsAppThemeLight ? ApplicationTheme.Light : ApplicationTheme.Dark;
            PInvoke.SetPreferredAppMode(PInvoke.ShouldAppsUseDarkMode() ? PreferredAppMode.AllowDark : PreferredAppMode.Default);

            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                ThemeChangerInvoker.ThemeEvent += (_, _) => 
                  {
                      WindowUtility.ApplyWindowTitlebarLegacyColor();
                      bool  isThemeLight = IsAppThemeLight;
                      Color color        = isThemeLight ? Colors.Black : Colors.White;
                      Current!.Resources!["WindowCaptionForeground"] = color;

                      WindowUtility.CurrentAppWindow!.TitleBar!.ButtonForegroundColor = color;
                      WindowUtility.CurrentAppWindow!.TitleBar!.ButtonInactiveBackgroundColor = color;

                      if (WindowUtility.CurrentWindow!.Content is not null and FrameworkElement frameworkElement)
                          frameworkElement.RequestedTheme = isThemeLight ? ElementTheme.Light : ElementTheme.Dark;
                  };

                Window toInitializeWindow = null;
                switch (m_appMode)
                {
                    case AppMode.Updater:
                        toInitializeWindow = new UpdaterWindow();
                        break;
                    case AppMode.Hi3CacheUpdater:
                    case AppMode.Launcher:
                        toInitializeWindow = new MainWindow();
                        ((MainWindow)toInitializeWindow).InitializeWindowProperties();
                        break;
                    case AppMode.OOBEState:
                        toInitializeWindow = new MainWindow();
                        ((MainWindow)toInitializeWindow).InitializeWindowProperties(true);
                        break;
                    case AppMode.StartOnTray:
                        toInitializeWindow = new MainWindow();
                        ((MainWindow)toInitializeWindow).InitializeWindowProperties();
                        LogWriteLine("Running Collapse in Tray Mode!", LogType.Scheme);
                        break;
                }

                // Disable AppUserModelId for now as Windows doesn't respect it on non UWP apps
                //string appUserModelId = "Collapse.CollapseLauncher";
                //int setAUMIDResult = SetCurrentProcessExplicitAppUserModelID(appUserModelId);
                //if (setAUMIDResult != 0) LogWriteLine($"Error when setting AppUserModelId to {appUserModelId}. Error code: {setAUMIDResult}", LogType.Error, true);
                //else LogWriteLine($"Successfully set AppUserModelId to {appUserModelId}", LogType.Default, true);

                toInitializeWindow!.Activate();
                
                bool isAcrylicEnabled = LauncherConfig.GetAppConfigValue("EnableAcrylicEffect").ToBool();
                if (!isAcrylicEnabled) ToggleBlurBackdrop(false);
                if (m_appMode == AppMode.StartOnTray)
                {
                    WindowUtility.ToggleToTray_AllWindow();
                }

                if (m_appMode != AppMode.Updater && LauncherConfig.GetAppConfigValue("EnableWaifu2X").ToBool())
                {
                    ImageLoaderHelper.InitWaifu2X();
                }

                // Initialize support for MagicScaler's WebP decoding
                CodecManager.Configure(codecs =>
                {
                    codecs.UseLibwebp();
                });
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL ERROR ON APP INITIALIZER LEVEL!!!\r\n{ex}", LogType.Error, true);
                LogWriteLine("\r\nIf this is not intended, please report it to: https://github.com/CollapseLauncher/Collapse/issues\r\nPress any key to exit...");
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                //Console.ReadLine();
                throw;
            }
        }

        public static void ToggleBlurBackdrop(bool useBackdrop = true)
        {
            MaterialHelperTestApi.SimulateDisabledByPolicy = !useBackdrop;
        }
    }
}
