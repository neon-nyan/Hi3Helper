using CollapseLauncher.Helper.Update;
using Microsoft.Win32;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.ApplicationModel.Activation;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    public static partial class AppActivation
    {
        [GeneratedRegex("""[\"].+?[\"]|[^ ]+""", RegexOptions.NonBacktracking)]
        internal static partial Regex MatchDoubleOrEscapedQuote();

        public static void Enable()
        {
            AppInstance.GetCurrent().Activated += App_Activated;

            const string protocolName = "collapse";
            RegistryKey  reg          = Registry.ClassesRoot.OpenSubKey(protocolName + @"\shell\open\command", true);

            if (reg != null)
            {
                if ((string)reg.GetValue("") == $"\"{AppExecutablePath}\" %1")
                {
                    LogWriteLine("The protocol is already activated.");
                    return;
                }
            }

            LogWriteLine("Protocol does not exist or paths are different. Activating protocol...");

            Registry.ClassesRoot.DeleteSubKeyTree(protocolName, false);

            RegistryKey protocol = Registry.ClassesRoot.CreateSubKey(protocolName, true);

            protocol.SetValue("", "CollapseLauncher protocol");
            protocol.SetValue("URL Protocol", "");
            protocol.SetValue("Version", LauncherUpdateHelper.LauncherCurrentVersionString);

            RegistryKey command = protocol.CreateSubKey(@"shell\open\command", true);

            command.SetValue("", $"\"{AppExecutablePath}\" %1");
        }

        private static void App_Activated(object sender, AppActivationArguments e)
        {
            if (IsMultipleInstanceEnabled)
                return;
            if (e.Kind != ExtendedActivationKind.Launch)
                return;
            if (e.Data == null)
                return;

            var args = e.Data as ILaunchActivatedEventArgs;
            ArgumentParser.ResetRootCommand();
            m_arguments = new Arguments();

            // Matches anything that is between two \" or " and anything that is not a space.
            IEnumerable<string> splitArgs = MatchDoubleOrEscapedQuote()
                .Matches(args!.Arguments)
                .Select(x => x.Value.Trim('"'));

            ArgumentParser.ParseArguments(splitArgs.Skip(1).ToArray());

            if (m_arguments.StartGame != null)
            {
                m_mainPage?.OpenAppActivation();
            }
        }

        public static bool DecideRedirection()
        {
            AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
            AppInstance keyInstance = AppInstance.FindOrRegisterForKey(m_appMode.ToString());

            if (keyInstance.IsCurrent || IsMultipleInstanceEnabled)
            {
                return false;
            }

            keyInstance.RedirectActivationToAsync(args).GetAwaiter().GetResult();
            return true;
        }

        public static void Disable()
        {
            AppInstance.GetCurrent().Activated -= App_Activated;
        }
    }
}
