using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.ShellLinkCOM;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable GrammarMistakeInComment

#nullable enable
namespace CollapseLauncher.Helper
{
    internal static class TaskSchedulerHelper
    {
        private const string CollapseStartupTaskName = "CollapseLauncherStartupTask";

        internal static bool IsInitialized;
        internal static bool CachedIsOnTrayEnabled;
        internal static bool CachedIsEnabled;

        internal static bool IsOnTrayEnabled()
        {
            if (!IsInitialized)
                InvokeGetStatusCommand().GetAwaiter().GetResult();

            return CachedIsOnTrayEnabled;
        }

        internal static bool IsEnabled()
        {
            if (!IsInitialized)
                InvokeGetStatusCommand().GetAwaiter().GetResult();

            return CachedIsEnabled;
        }

        private static async Task InvokeGetStatusCommand()
        {
            // Build the argument and mode to set
            var argumentBuilder = new StringBuilder();
            argumentBuilder.Append("IsEnabled");

            // Append task name and stub path
            AppendTaskNameAndPathArgument(argumentBuilder);

            // Store argument builder as string
            var argumentString = argumentBuilder.ToString();

            // Invoke command and get return code
            var returnCode = await GetInvokeCommandReturnCode(argumentString);

            (CachedIsEnabled, CachedIsOnTrayEnabled) = returnCode switch
            {
                // -1 means task is disabled with tray enabled
                -1 => (false, true),
                // 0 means task is disabled with tray disabled
                0  => (false, false),
                // 1 means task is enabled with tray disabled
                1  => (true, false),
                // 2 means task is enabled with tray enabled
                2  => (true, true),
                // Otherwise, return both disabled (due to failure)
                _ => (false, false)
            };

            // Print init determination
            CheckInitDetermination(returnCode);
        }

        private static void CheckInitDetermination(int returnCode)
        {
            // If the return code is within range, then set as initialized
            if (returnCode is > -2 and < 3)
            {
                // Set as initialized
                IsInitialized = true;
            }
            // Otherwise, log the return code
            else
            {
                string reason = returnCode switch
                {
                    int.MaxValue => "ARGUMENT_INVALID",
                    int.MinValue => "UNHANDLED_ERROR",
                    short.MaxValue => "INTERNALINVOKE_ERROR",
                    short.MinValue => "APPLET_NOTFOUND",
                    _ => $"UNKNOWN_{returnCode}"
                };
                Logger.LogWriteLine($"Error while getting task status from applet with reason: {reason}", LogType.Error, true);
            }
        }

        internal static void ToggleTrayEnabled(bool isEnabled)
        {
            CachedIsOnTrayEnabled = isEnabled;
            InvokeToggleCommand().GetAwaiter().GetResult();
        }

        internal static void ToggleEnabled(bool isEnabled)
        {
            CachedIsEnabled = isEnabled;
            InvokeToggleCommand().GetAwaiter().GetResult();
        }

        private static async Task InvokeToggleCommand()
        {
            // Build the argument and mode to set
            StringBuilder argumentBuilder = new StringBuilder();
            argumentBuilder.Append(CachedIsEnabled ? "Enable" : "Disable");

            // Append argument whether to toggle the tray or not
            if (CachedIsOnTrayEnabled)
                argumentBuilder.Append("ToTray");

            // Append task name and stub path
            AppendTaskNameAndPathArgument(argumentBuilder);

            // Store argument builder as string
            string argumentString = argumentBuilder.ToString();

            // Invoke applet
            int returnCode = await GetInvokeCommandReturnCode(argumentString);

            // Print init determination
            CheckInitDetermination(returnCode);
        }

        private static void AppendTaskNameAndPathArgument(StringBuilder argumentBuilder)
        {
            // Get current stub or main executable path
            string currentExecPath = MainEntryPoint.FindCollapseStubPath();

            // Build argument to the task name
            argumentBuilder.Append(" \"");
            argumentBuilder.Append(CollapseStartupTaskName);
            argumentBuilder.Append('"');

            // Build argument to the executable path
            argumentBuilder.Append(" \"");
            argumentBuilder.Append(currentExecPath);
            argumentBuilder.Append('"');
        }

        internal static void RecreateIconShortcuts()
        {
            // Get icons paths
            (string iconLocationStartMenu, string iconLocationDesktop)
                = GetIconLocationPaths(
                    out _,
                    out string? appDescription,
                    out string? executablePath,
                    out string? workingDirPath);

            // Create shell link instance and save the shortcut under Desktop and User's Start menu
            CreateShortcut(iconLocationStartMenu, appDescription, executablePath, workingDirPath);
            CreateShortcut(iconLocationDesktop, appDescription, executablePath, workingDirPath);
        }

        private static void CreateShortcut(
            string iconLocation,
            string? appDescription,
            string? executablePath,
            string? workingDirPath)
        {
            // Try create icon location directory
            string iconLocationDir = Path.GetDirectoryName(iconLocation) ?? "";

            // Try create directory
            Directory.CreateDirectory(iconLocationDir);
            
            // Create ShellLink instance
            using ShellLink shellLink = new ShellLink();

            // If existing icon exist, try open it
            try
            {
                if (File.Exists(iconLocation))
                    shellLink.Open(iconLocation);
            }
            catch (Exception ex)
            {
                string msg = $"An error occurred while opening existing icon file at: {iconLocation}";
                SentryHelper.ExceptionHandler(new Exception(msg, ex));
                Logger.LogWriteLine(msg + $"\r\n{ex}", LogType.Error, true);
            }
            
            // Set params on the shortcut instance
            shellLink.IconIndex         = 0;
            shellLink.IconPath          = executablePath ?? "";
            shellLink.DisplayMode       = LinkDisplayMode.edmNormal;
            shellLink.WorkingDirectory  = workingDirPath ?? "";
            shellLink.Target            = executablePath ?? "";
            shellLink.Description       = appDescription ?? "";

            // Save the icons
            shellLink.Save(iconLocation);
        }

        internal static (string IconStartMenu, string IconDesktop) GetIconLocationPaths(
            out string? appProductName,
            out string? appDescription,
            out string? executablePath,
            out string? workingDirPath)
        {
            // Get current executable path as its target.
            executablePath = LauncherConfig.AppExecutablePath;
            workingDirPath = Path.GetDirectoryName(executablePath);

            // Get exe's description
            FileVersionInfo currentExecVersionInfo = FileVersionInfo.GetVersionInfo(executablePath);
            appDescription = currentExecVersionInfo.FileDescription ?? "";

            // Get paths
            appProductName = currentExecVersionInfo.ProductName;
            string shortcutFilename = appProductName + ".lnk";
            string startMenuLocation = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            string desktopLocation = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string iconLocationStartMenu = Path.Combine(
                startMenuLocation,
                "Programs",
                currentExecVersionInfo.CompanyName ?? "",
                shortcutFilename);
            string iconLocationDesktop = Path.Combine(
                desktopLocation,
                shortcutFilename);

            return (iconLocationStartMenu, iconLocationDesktop);
        }

        private static async Task<int> GetInvokeCommandReturnCode(string argument)
        {
            const string retValMark = "RETURNVAL_";

            // Get the applet path and check if the file exist
            string appletPath = Path.Combine(LauncherConfig.AppExecutableDir, "Lib", "win-x64", "Hi3Helper.TaskScheduler.exe");
            if (!File.Exists(appletPath))
            {
                Logger.LogWriteLine($"Task Scheduler Applet does not exist in this path: {appletPath}", LogType.Error, true);
                return short.MinValue;
            }

            // Try to make process instance for the applet
            using Process process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName               = appletPath,
                Arguments              = argument,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                CreateNoWindow         = true
            };

#if DEBUG
            Logger.LogWriteLine("[TaskSchedulerHelper] Running TaskSchedulerHelper with command:\r\n" + appletPath + " " + argument, LogType.Debug, true);
#endif

            int lastErrCode = short.MaxValue;
            try
            {
                // Start the applet and wait until it exit.
                process.Start();
                while (!process.StandardOutput.EndOfStream)
                {
                    string? consoleStdOut = await process.StandardOutput.ReadLineAsync();
                    Logger.LogWriteLine("[TaskScheduler] " + consoleStdOut, LogType.Debug, true);

                    // Parse if it has RETURNVAL_
                    if (consoleStdOut == null || !consoleStdOut.StartsWith(retValMark))
                    {
                        continue;
                    }

                    ReadOnlySpan<char> span = consoleStdOut.AsSpan(retValMark.Length);
                    if (int.TryParse(span, null, out int resultReturnCode))
                    {
                        lastErrCode = resultReturnCode;
                    }
                }
                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                // If error happened, then return.
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                Logger.LogWriteLine($"An error has occurred while invoking Task Scheduler applet!\r\n{ex}", LogType.Error, true);
                return short.MaxValue;
            }

            // Get return code
            return lastErrCode;
        }
    }
}
