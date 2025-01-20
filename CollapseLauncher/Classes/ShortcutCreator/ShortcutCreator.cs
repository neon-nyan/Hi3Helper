using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Loading;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Microsoft.Win32;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher.ShortcutUtils
{
    public static class ShortcutCreator
    {
        public static string GetIconName(GameNameType gameType)
        {
            return gameType switch
            {
                GameNameType.Genshin => "icon-genshin.ico",
                GameNameType.StarRail => "icon-starrail.ico",
                GameNameType.Zenless => "icon-zenless.ico",
                _ => "icon-honkai.ico"
            };
        }

        internal static void CreateShortcut(string path, PresetConfig preset, bool play = false)
        {
            var translatedGameTitle =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(preset.GameName,
                                                                        Lang._GameClientTitles)!;
            var translatedGameRegion =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(preset.ZoneName,
                                                                        Lang._GameClientRegions);
            var shortcutName = $"{translatedGameTitle} ({translatedGameRegion}) - Collapse Launcher.url".Replace(":", "");
            var url = $"collapse://open -g \"{preset.GameName}\" -r \"{preset.ZoneName}\"";

            if (play) url += " -p";

            var icon = Path.Combine(Path.GetDirectoryName(AppExecutablePath)!,
                                    $"Assets/Images/GameIcon/{GetIconName(preset.GameType)}");

            var fullPath = Path.Combine(path, shortcutName);

            using var writer = new StreamWriter(fullPath, false);
            writer.WriteLine($"[InternetShortcut]\nURL={url}\nIconIndex=0\nIconFile={icon}");
        }

        /// Heavily based on Heroic Games Launcher "Add to Steam" feature.
        /// 
        /// Source:
        /// https://github.com/Heroic-Games-Launcher/HeroicGamesLauncher/blob/8bdee1383446d3b81e240a4300baaf337d48ec92/src/backend/shortcuts/nonesteamgame/nonesteamgame.ts
        internal static async Task<bool> AddToSteam(PresetConfig preset, bool play)
        {
            var paths = GetShortcutsPath();

            if (paths == null || paths.Length == 0)
                return false;

            var tokenSource = new CancellationTokenSourceWrapper();

            LoadingMessageHelper.ShowActionButton(Lang._Misc.Cancel, "", (_, _) =>
            {
                tokenSource.Cancel();
                LogWriteLine("[ShortcutCreator::AddToSteam] Cancelled manually.");
            });

            List<SteamShortcut> shortcuts = [];

            foreach (var path in paths)
            {
                var parser = new SteamShortcutParser(path);

                var splitPath = path.Split('\\');
                var userId = splitPath[^3];

                shortcuts.Add(parser.Insert(preset, play));

                parser.Save();
                LogWriteLine($"[ShortcutCreator::AddToSteam] Added shortcut for {preset.GameName} - {preset.ZoneName} for Steam3ID {userId}");
            }

            foreach (var shortcut in shortcuts)
            {
                await shortcut.MoveImages(tokenSource.Token);
            }

            return true;
        }

        private static string[] GetShortcutsPath()
        {
            var reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam", false);

            if (reg == null)
                return null;

            var steamPath = (string)reg.GetValue("InstallPath", @"C:\Program Files (x86)\Steam");

            var steamUserData = Path.Combine(steamPath, "userdata");

            if (!Directory.Exists(steamUserData))
            {
                LogWriteLine("[ShortcutCreator::GetShortcutsPath] " + steamUserData + " is not a valid folder.", LogType.Error);
                return null;
            }

            var res = Directory.GetDirectories(steamUserData)
                .Where(x =>
                {
                    var y = x.Split("\\").Last();
                    return y != "ac" && y != "0" && y != "anonymous";
                }).ToArray();

            for (var i = 0; i < res.Length; i++)
            {
                res[i] = Path.Combine(res[i], "config", "shortcuts.vdf");
                LogWriteLine("[ShortcutCreator::GetShortcutsPath] Found profile: " + res[i], LogType.Debug);
            }

            return res;
        }
    }
}
