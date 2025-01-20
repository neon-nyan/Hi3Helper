using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Database;
using CollapseLauncher.Helper.Update;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http.Legacy;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Win32.Native.LibraryImport;
using Hi3Helper.Win32.Native.ManagedTools;
using Hi3Helper.Win32.ShellLinkCOM;
using InnoSetupHelper;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using NuGet.Versioning;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using WinRT;
using static CollapseLauncher.ArgumentParser;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher
{
    public static partial class MainEntryPointExtension
    {
        [LibraryImport("Microsoft.ui.xaml.dll", EntryPoint = "XamlCheckProcessRequirements")]
        public static partial void XamlCheckProcessRequirements();
    }

    public static class MainEntryPoint
    {
    #nullable enable
        public static int  InstanceCount      { get; set; }
        public static App? CurrentAppInstance { get; set; }
    #nullable restore

        [STAThread]
        public static void Main(params string[] args)
        {
            AppCurrentArgument = args.ToList();
        #if PREVIEW
            IsPreview = true;
        #endif
            try
            {
                // Extract icons from the executable file
                string mainModulePath = AppExecutablePath;
                var    iconCount      = PInvoke.ExtractIconEx(mainModulePath, -1, null, null, 0);
                if (iconCount > 0)
                {
                    var largeIcons = new IntPtr[1];
                    var smallIcons = new IntPtr[1];
                    PInvoke.ExtractIconEx(mainModulePath, 0, largeIcons, smallIcons, 1);
                    AppIconLarge = largeIcons[0];
                    AppIconSmall = smallIcons[0];
                }

                // Decide AUMID string
                const string aumid = "Collapse";

                // Start Updater Hook
                StartUpdaterHook(aumid);

                // Set AUMID
                WindowUtility.CurrentAumid = aumid;

                InitAppPreset();
                var logPath = AppGameLogsFolder;
                CurrentLogger = IsConsoleEnabled
                    ? new LoggerConsole(logPath, Encoding.UTF8)
                    : new LoggerNull(logPath, Encoding.UTF8);

                if (Directory.GetCurrentDirectory() != AppExecutableDir)
                {
                    LogWriteLine(
                                 $"Force changing the working directory from {Directory.GetCurrentDirectory()} to {AppExecutableDir}!",
                                 LogType.Warning, true);
                    Directory.SetCurrentDirectory(AppExecutableDir);
                }

                InitializeAppSettings();

                SentryHelper.IsPreview = IsPreview;
            #pragma warning disable CS0618 // Type or member is obsolete
                SentryHelper.AppBuildCommit = ThisAssembly.Git.Sha;
                SentryHelper.AppBuildBranch = ThisAssembly.Git.Branch;
                SentryHelper.AppBuildRepo   = ThisAssembly.Git.RepositoryUrl;
                SentryHelper.AppCdnOption   = FallbackCDNUtil.GetPreferredCDN().URLPrefix;
            #pragma warning restore CS0618 // Type or member is obsolete
                if (SentryHelper.IsEnabled)
                {
                    try
                    {
                        // Sentry SDK Entry
                        LogWriteLine("Loading Sentry SDK...", LogType.Sentry, true);
                        SentryHelper.InitializeSentrySdk();
                        LogWriteLine("Setting up global exception handler redirection", LogType.Scheme, true);
                        SentryHelper.InitializeExceptionRedirect();
                    }
                    catch (Exception ex)
                    {
                        LogWriteLine($"Failed to load Sentry SDK.\r\n{ex}", LogType.Sentry, true);
                    }
                }

                LogWriteLine(string.Format("Running Collapse Launcher [{0}], [{3}], under {1}, as {2}",
                                           LauncherUpdateHelper.LauncherCurrentVersionString,
                                           GetVersionString(),
                                       #if DEBUG
                                           Environment.UserName,
                                       #else
                "[REDACTED]",
                                       #endif
                                           IsPreview ? "Preview" : "Stable"), LogType.Scheme, true);

            #pragma warning disable CS0618 // Type or member is obsolete
                LogWriteLine(
                             $"Runtime: {RuntimeInformation.FrameworkDescription} - WindowsAppSDK {WindowsAppSdkVersion}",
                             LogType.Scheme, true);
                LogWriteLine($"Built from repo {ThisAssembly.Git.RepositoryUrl}\r\n\t" +
                             $"Branch {ThisAssembly.Git.Branch} - Commit {ThisAssembly.Git.Commit} at {ThisAssembly.Git.CommitDate}",
                             LogType.Scheme, true);
            #pragma warning restore CS0618 // Type or member is obsolete

                Process.GetCurrentProcess().PriorityBoostEnabled = true;

                ParseArguments(args);

                // Initiate InnoSetupHelper's log event
                InnoSetupLogUpdate.LoggerEvent += InnoSetupLogUpdate_LoggerEvent;

                HttpLogInvoker.DownloadLog += HttpClientLogWatcher!;

                switch (m_appMode)
                {
                    case AppMode.ElevateUpdater:
                        RunElevateUpdate();
                        return;
                    case AppMode.InvokerTakeOwnership:
                        TakeOwnership.StartTakingOwnership(m_arguments.TakeOwnership.AppPath);
                        return;
                    case AppMode.InvokerMigrate:
                        if (m_arguments.Migrate.IsBhi3L)
                        {
                            new Migrate().DoMigrationBHI3L(
                                                           m_arguments.Migrate.GameVer,
                                                           m_arguments.Migrate.RegLoc,
                                                           m_arguments.Migrate.InputPath,
                                                           m_arguments.Migrate.OutputPath);
                        }
                        else
                        {
                            new Migrate().DoMigration(
                                                      m_arguments.Migrate.InputPath,
                                                      m_arguments.Migrate.OutputPath);
                        }

                        return;
                    case AppMode.InvokerMoveSteam:
                        new Migrate().DoMoveSteam(
                                                  m_arguments.Migrate.InputPath,
                                                  m_arguments.Migrate.OutputPath,
                                                  m_arguments.Migrate.GameVer,
                                                  m_arguments.Migrate.KeyName);
                        return;
                    case AppMode.GenerateVelopackMetadata:
                        GenerateVelopackMetadata(aumid);
                        return;
                }

            #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                // Reason: These are methods that either has its own error handling and/or not that important,
                // so the execution could continue without anything to worry about **technically**
                InitDatabaseHandler();
                CheckRuntimeFeatures();
            #pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                AppDomain.CurrentDomain.ProcessExit += OnProcessExit!;

                InstanceCount = ProcessChecker.EnumerateInstances(ILoggerHelper.GetILogger());

                AppActivation.Enable();
                if (AppActivation.DecideRedirection())
                {
                    return;
                }

                MainEntryPointExtension.XamlCheckProcessRequirements();
                ComWrappersSupport.InitializeComWrappers();

                StartMainApplication();
            }
        #if !DEBUG
        catch (Exception ex)
        {
            SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
            SpawnFatalErrorConsole(ex);
        }
        #else
            // ReSharper disable once RedundantCatchClause
            // Reason: warning shaddap-er
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                throw;
            }
        #endif
            finally
            {
                HttpLogInvoker.DownloadLog -= HttpClientLogWatcher!;
            }
        }

        private static async Task InitDatabaseHandler()
        {
            await DbHandler.Init();
        }

        private static void InnoSetupLogUpdate_LoggerEvent(object sender, InnoSetupLogStruct e)
        {
            LogWriteLine(
                         e.Message,
                         e.LogType switch
                         {
                             InnoSetupLogType.Warning => LogType.Warning,
                             InnoSetupLogType.Error => LogType.Error,
                             _ => LogType.Default
                         },
                         e.IsWriteToLog);
        }

        public static void SpawnFatalErrorConsole(Exception ex)
        {
            CurrentAppInstance?.Exit();
            LoggerConsole.AllocateConsole();
            Console.Error
                   .WriteLine($"FATAL ERROR ON APP MAIN() LEVEL AND THE MAIN THREAD HAS BEEN TERMINATED!!!\r\n{ex}");
            Console.Error.WriteLine("\r\nIf you are sure that this is not intended, " +
                                    "please report it to: https://github.com/CollapseLauncher/Collapse/issues\r\n" +
                                    "Press any key to exit or Press 'R' to restart the main thread app...");

        #if !DEBUG
            if (ConsoleKey.R == Console.ReadKey().Key)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = AppExecutablePath,
                    UseShellExecute = false
                };

                foreach (string arg in AppCurrentArgument)
                {
                    startInfo.ArgumentList.Add(arg);
                }
                Process process = new Process()
                {
                    StartInfo = startInfo
                };
                process.Start();
            }
        #endif
        }

        public static void StartMainApplication()
        {
            Application.Start(_ =>
                              {
                                  DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();

                                  var context = new DispatcherQueueSynchronizationContext(dispatcherQueue);
                                  SynchronizationContext.SetSynchronizationContext(context);

                                  // ReSharper disable once ObjectCreationAsStatement
                                  CurrentAppInstance = new App
                                  {
                                      HighContrastAdjustment = ApplicationHighContrastAdjustment.None
                                  };
                              });
        }

        private static void HttpClientLogWatcher(object sender, DownloadLogEvent e)
        {
            var severity = e.Severity switch
                           {
                               DownloadLogSeverity.Warning => LogType.Warning,
                               DownloadLogSeverity.Error => LogType.Error,
                               _ => LogType.Default
                           };

            LogWriteLine(e.Message, severity, true);
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            App.IsAppKilled = true;
        }

        private static void StartUpdaterHook(string aumid)
        {
        #if !USEVELOPACK
        // Add Squirrel Hooks
        SquirrelAwareApp.HandleEvents(
                                      // Add shortcut and uninstaller entry on first start-up
                                      // ReSharper disable UnusedParameter.Local
                                      (_, sqr) =>
                                      {
                                          Console
                                             .WriteLine("Please do not close this console window while Collapse is preparing the installation via Squirrel...");
                                      },
                                      (_, sqr) =>
                                      {
                                          Console
                                             .WriteLine("Please do not close this console window while Collapse is updating via Squirrel...");
                                      },
                                      onAppUninstall: (_, sqr) =>
                                      {
                                          Console
                                             .WriteLine("Uninstalling Collapse via Squirrel...\r\n" +
                                                        "Please do not close this console window while action is being performed!");
                                      },
                                      // ReSharper restore UnusedParameter.Local
                                      onEveryRun: (_, _, _) => { }
                                     );
        #else
            VelopackApp.Build()
                       .WithRestarted(TryCleanupFallbackUpdate)
                       .WithAfterUpdateFastCallback(TryCleanupFallbackUpdate)
                       .WithFirstRun(TryCleanupFallbackUpdate)
                       .Run(ILoggerHelper.GetILogger());

            _ = Task.Run(DeleteVelopackLock);

            GenerateVelopackMetadata(aumid);

            void DeleteVelopackLock()
            {
                // Get the current application directory
                string currentAppDir = AppDomain.CurrentDomain.BaseDirectory;

                // Construct the path to the .velopack_lock file
                string velopackLockPath = Path.Combine(currentAppDir, "..", "packages", ".velopack_lock");

                // Normalize the path
                velopackLockPath = Path.GetFullPath(velopackLockPath);

                // Check if the file exists
                if (!File.Exists(velopackLockPath))
                {
                    return;
                }

                // Delete the file
                File.Delete(velopackLockPath);
                LogWriteLine(".velopack_lock file deleted successfully.");
            }
        #endif
        }

    #if USEVELOPACK
        public static void TryCleanupFallbackUpdate(SemanticVersion newVersion)
        {
            string currentExecutedAppFolder = AppExecutableDir.TrimEnd('\\');
            string currentExecutedPath      = AppExecutablePath;

            // If the path is not actually running under "current" velopack folder, then return
        #if !DEBUG
        if (!currentExecutedAppFolder.EndsWith("current", StringComparison.OrdinalIgnoreCase)) // Expecting "current"
        {
            Logger.LogWriteLine("[TryCleanupFallbackUpdate] The launcher does not run from \"current\" folder");
            return;
        }
        #endif

            try
            {
                // Otherwise, start cleaning-up process
                string currentExecutedParentFolder = Path.GetDirectoryName(currentExecutedAppFolder);
                if (currentExecutedParentFolder != null)
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(currentExecutedParentFolder);
                    foreach (DirectoryInfo childLegacyAppSemVerFolder in
                             directoryInfo.EnumerateDirectories("app-*", SearchOption.TopDirectoryOnly))
                    {
                        // Removing the "app-*" folder
                        childLegacyAppSemVerFolder.Delete(true);
                        LogWriteLine($"[TryCleanupFallbackUpdate] Removed {childLegacyAppSemVerFolder.FullName} folder!",
                                     LogType.Default, true);
                    }

                    // Try to remove squirrel temp clowd folder
                    string squirrelTempPackagesFolder = Path.Combine(currentExecutedParentFolder, "SquirrelClowdTemp");
                    DirectoryInfo squirrelTempPackagesFolderInfo = new DirectoryInfo(squirrelTempPackagesFolder);
                    if (squirrelTempPackagesFolderInfo.Exists)
                    {
                        squirrelTempPackagesFolderInfo.Delete(true);
                        LogWriteLine($"[TryCleanupFallbackUpdate] Removed package temp folder: {squirrelTempPackagesFolder}!",
                                     LogType.Default, true);
                    }

                    // Try to remove stub executable
                    string squirrelLegacyStubPath = Path.Combine(currentExecutedParentFolder, "CollapseLauncher.exe");
                    RemoveSquirrelFilePath(squirrelLegacyStubPath);

                    // Try to remove createdump executable
                    string squirrelLegacyDumpPath = Path.Combine(currentExecutedParentFolder, "createdump.exe");
                    RemoveSquirrelFilePath(squirrelLegacyDumpPath);

                    // Try to remove RestartAgent executable
                    string squirrelLegacyRestartAgentPath =
                        Path.Combine(currentExecutedParentFolder, "RestartAgent.exe");
                    RemoveSquirrelFilePath(squirrelLegacyRestartAgentPath);
                }

                // Try to remove legacy shortcuts
                string currentWindowsPathDrive = Path.GetPathRoot(Environment.SystemDirectory);
                if (!string.IsNullOrEmpty(currentWindowsPathDrive))
                {
                    string squirrelLegacyStartMenuGlobal =
                        Path.Combine(currentWindowsPathDrive,
                                     @"ProgramData\Microsoft\Windows\Start Menu\Programs\Collapse\Collapse Launcher");
                    string squirrelLegacyStartMenuGlobalParent = Path.GetDirectoryName(squirrelLegacyStartMenuGlobal);
                    if (Directory.Exists(squirrelLegacyStartMenuGlobalParent) &&
                        Directory.Exists(squirrelLegacyStartMenuGlobal))
                    {
                        Directory.Delete(squirrelLegacyStartMenuGlobalParent, true);
                    }
                }

                // Try to delete all possible shortcuts on any users (since the shortcut used will be the global one)
                // Only do this if shortcut path is not same as current path tho... It pain to re-pin the shortcut again...
                string currentUsersDirPath = Path.Combine(currentWindowsPathDrive!, "Users");
                foreach (string userDirInfoPath in Directory
                                                  .EnumerateDirectories(currentUsersDirPath, "*",
                                                                        SearchOption.TopDirectoryOnly)
                                                  .Where(ConverterTool.IsUserHasPermission))
                {
                    // Get the shortcut file
                    string thisUserStartMenuShortcut = Path.Combine(userDirInfoPath,
                                                                    @"AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Collapse.lnk");
                    if (!File.Exists(thisUserStartMenuShortcut))
                    {
                        continue;
                    }

                    // Try open the shortcut and check whether this shortcut is actually pointing to
                    // CollapseLauncher.exe file
                    using ShellLink shellLink = new ShellLink(thisUserStartMenuShortcut);
                    // Try to get the target path and its filename
                    string shortcutTargetPath = shellLink.Target;
                    if (!shortcutTargetPath.Equals(currentExecutedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Compare if the filename is equal, then delete it.
                    File.Delete(thisUserStartMenuShortcut);
                    LogWriteLine($"[TryCleanupFallbackUpdate] Deleted old shortcut located at: " +
                                 $"{thisUserStartMenuShortcut} -> {shortcutTargetPath}",
                                 LogType.Default, true);
                }

                // Try to recreate shortcuts
                TaskSchedulerHelper.RecreateIconShortcuts();
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"[TryCleanupFallbackUpdate] Failed while operating clean-up routines...\r\n{ex}");
            }

            return;

            void RemoveSquirrelFilePath(string filePath)
            {
                if (!File.Exists(filePath))
                {
                    return;
                }

                File.Delete(filePath);
                LogWriteLine($"[TryCleanupFallbackUpdate] Removed old squirrel executables: {filePath}!",
                             LogType.Default, true);
            }
        }
    #endif

        public static string FindCollapseStubPath()
        {
            var collapseMainPath = AppExecutablePath;
            // var collapseExecName = "CollapseLauncher.exe";
            // var collapseStubPath = Path.Combine(Directory.GetParent(Path.GetDirectoryName(collapseMainPath)!)!.FullName,
            //                                     collapseExecName);
            // if (File.Exists(collapseStubPath))
            // {
            //     LogWriteLine($"Found stub at {collapseStubPath}", LogType.Default, true);
            //     return collapseStubPath;
            // }

            LogWriteLine($"Collapse stub is not used anymore, returning current executable path!\r\n\t{collapseMainPath}",
                         LogType.Default, true);
            return collapseMainPath;
        }

        private static async Task CheckRuntimeFeatures()
        {
            try
            {
                await Task.Run(() =>
                               {
                                   // RuntimeFeature docs https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.runtimefeature?view=net-9.0
                                   LogWriteLine($"Available Runtime Features:\r\n\t" +
                                                $"PortablePdb: {RuntimeFeature.IsSupported(RuntimeFeature.PortablePdb)}\r\n\t" +
                                                $"IsDynamicCodeCompiled:   {RuntimeFeature.IsDynamicCodeCompiled}\r\n\t" +
                                                $"IsDynamicCodeSupported:  {RuntimeFeature.IsDynamicCodeSupported}\r\n\t" +
                                                $"UnmanagedSignatureCallingConventions:    {RuntimeFeature.IsSupported(RuntimeFeature.UnmanagedSignatureCallingConvention)}",
                                                LogType.Debug, true);
                               }
                              );
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"[CheckRuntimeFeatures] Failed when enumerating available runtime features!\r\n{ex}",
                             LogType.Error, true);
            }
        }

        private static void InitializeAppSettings()
        {
            InitializeLocale();
            if (IsFirstInstall)
            {
                LoadLocale(CultureInfo.CurrentUICulture.Name);
                SetAppConfigValue("AppLanguage", Lang.LanguageID);
            }
            else
            {
                LoadLocale(GetAppConfigValue("AppLanguage").ToString());
            }

            var themeValue = GetAppConfigValue("ThemeMode").ToString();
            if (Enum.TryParse(themeValue, true, out CurrentAppTheme))
            {
                return;
            }

            CurrentAppTheme = AppThemeMode.Dark;
            LogWriteLine($"ThemeMode: {themeValue} is invalid! Falling back to Dark-mode (Valid values are: {string.Join(',', Enum.GetNames(typeof(AppThemeMode)))})",
                         LogType.Warning, true);
        }

        private static void RunElevateUpdate()
        {
            var elevatedProc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName         = UpdaterWindow.SourcePath,
                    WorkingDirectory = UpdaterWindow.WorkingDir,
                    Arguments =
                        $"update --input \"{m_arguments.Updater.AppPath}\" --channel {m_arguments.Updater.UpdateChannel}",
                    UseShellExecute = true,
                    Verb            = "runas"
                }
            };
            elevatedProc.Start();
        }

        private static void GenerateVelopackMetadata(string aumid)
        {
            const string xmlTemplate = """
                                       <?xml version="1.0" encoding="utf-8"?>
                                       <package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
                                       <metadata>
                                       <id>CollapseLauncher</id>
                                       <title>Collapse</title>
                                       <description>Collapse</description>
                                       <authors>Collapse Project Team</authors>
                                       <version>{0}</version>
                                       <channel>{1}</channel>
                                       <mainExe>CollapseLauncher.exe</mainExe>
                                       <os>win</os>
                                       <rid>win</rid>
                                       <shortcutLocations>Desktop,StartMenuRoot</shortcutLocations>
                                       <shortcutAmuid>{2}</shortcutAmuid>
                                       <shortcutAumid>{2}</shortcutAumid>
                                       </metadata>
                                       </package>
                                       """; // Adding shortcutAumid for future use, since they typo-ed the XML tag LMAO
            string currentVersion = LauncherUpdateHelper.LauncherCurrentVersionString;
            string xmlPath        = Path.Combine(AppExecutableDir, "sq.version");
            string xmlContent     = string.Format(xmlTemplate, currentVersion, IsPreview ? "preview" : "stable", aumid);
            File.WriteAllText(xmlPath, xmlContent.ReplaceLineEndings("\n"));
            LogWriteLine($"Velopack metadata has been successfully written!\r\n{xmlContent}", LogType.Default, true);
        }

        public static string GetVersionString()
        {
            var version = Environment.OSVersion.Version;
            m_isWindows11 = version.Build >= 22000;
            return m_isWindows11 ? $"Windows 11 (build: {version.Build}.{version.Revision})" : $"Windows {version.Major} (build: {version.Build}.{version.Revision})";
        }

        public static string MD5Hash(string path)
        {
            if (!File.Exists(path))
            {
                return "";
            }

            FileStream stream = File.OpenRead(path);
            var        hash   = Hash.GetCryptoHash<MD5>(stream);
            stream.Close();
            return Convert.ToHexStringLower(hash);
        }
    }
}