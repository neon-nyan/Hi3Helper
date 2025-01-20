using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Data;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Hi3Helper.SentryHelper;
// ReSharper disable InconsistentNaming

namespace CollapseLauncher
{
    public class Migrate : TakeOwnership
    {
        public void DoMoveSteam(string source, string target, string keyLoc, string keyName)
        {
            MoveOperation(source, target);
            StartTakingOwnership(target);

            Registry.SetValue(keyLoc, keyName, target, RegistryValueKind.String);
        }

        private static void MoveOperation(string source, string target)
        {
            Logger.LogWriteLine("Using \"Cross-disk\" method while moving to target", LogType.Default, true);
            string[] fileList = Directory.GetFiles(source, "*", SearchOption.AllDirectories);

            for (int i = 0; i < fileList.Length; i++)
            {
                var basePath   = fileList[i][(source.Length + 1)..];
                var targetPath = Path.Combine(target, basePath);

                if (!Directory.Exists(Path.GetDirectoryName(targetPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                if (File.Exists(targetPath))
                    File.Delete(targetPath);

                File.Move(fileList[i], targetPath);
                Logger.LogWriteLine($"\rMoving {i + 1}/{fileList.Length}: {basePath}", LogType.Default, true);
            }
        }

        internal void DoMigrationBHI3L(string version, string registryName, string sourceGame, string targetGame)
        {
            IniFile iniFile = new IniFile();

            if (sourceGame != targetGame)
            {
                try
                {
                    targetGame = Path.Combine(targetGame, "Games");
                    MoveOperation(sourceGame, targetGame);
                    BHI3LInfo info = new BHI3LInfo
                    {
                        game_info = new BHI3LInfo_GameInfo
                        {
                            installed = true,
                            install_path = targetGame,
                            version = version
                        }
                    };

                    Registry.CurrentUser.OpenSubKey(@"Software\Bp\Better HI3 Launcher", true)!
                        .SetValue(registryName,
                        Encoding.UTF8.GetBytes(info.Serialize(BHI3LInfoJsonContext.Default.BHI3LInfo)),
                        RegistryValueKind.Binary);
                }
                catch (Exception ex)
                {
                    Logger.LogWriteLine($"Failed when trying to move from BetterHi3Launcher {ex}", LogType.Error, true);
                    SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                }
            }

            if (!File.Exists(Path.Combine(targetGame, "config.ini")))
            {
                iniFile.Add("General", new Dictionary<string, IniValue>
                {
                    { "channel", new IniValue(1) },
                    { "cps", new IniValue() },
                    { "game_version", new IniValue(version) },
                    { "sub_channel", new IniValue(1) },
                    { "sdk_version", new IniValue() }
                });
                iniFile.Save(Path.Combine(targetGame, "config.ini"));
            }

            StartTakingOwnership(targetGame);
        }

        public void DoMigration(string source, string target)
        {
            string configFilePath = Path.Combine(source, "config.ini");

            if (!File.Exists(configFilePath))
            {
                return;
            }

            IniFile iniFile    = IniFile.LoadFrom(configFilePath);
            var     sourceGame = ConverterTool.NormalizePath(iniFile["launcher"]["game_install_path"].ToString());
            var     targetGame = Path.Combine(target, Path.GetFileName(sourceGame));
            Logger.LogWriteLine($"Moving From:\r\n\t{source}\r\nTo Destination:\r\n\t{target}", LogType.Default, true);
            try
            {
                MoveOperation(sourceGame, targetGame);
            }
            // Use move process if the method above throw a fucking nonsense reason even the folder is actually exist...
            catch (DirectoryNotFoundException)
            {
                Process move = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName        = "cmd.exe",
                        UseShellExecute = false,
                        Arguments       = $"/c move /Y \"{sourceGame}\" \"{targetGame}\""
                    }
                };

                move.Start();
                move.WaitForExit();
            }

            StartTakingOwnership(targetGame);

            iniFile["launcher"]["game_install_path"] = targetGame.Replace('\\', '/');
            iniFile.Save(Path.Combine(source, "config.ini"));
        }
    }
}
