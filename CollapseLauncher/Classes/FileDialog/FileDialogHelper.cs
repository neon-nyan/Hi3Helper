using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.FileDialogCOM;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.FileDialogCOM
{
    internal static class FileDialogHelper
    {
        /// <summary>
        /// Get only folder paths that's allowed by Collapse Launcher
        /// </summary>
        /// <param name="title">The title of the folder dialog</param>
        /// <param name="checkOverride">Override path check and returns the actual string</param>
        /// <returns>If <seealso cref="string"/> is empty, then it's cancelled. Otherwise, returns a selected path</returns>
        internal static async Task<string> GetRestrictedFolderPathDialog(string? title = null, Func<string, string>? checkOverride = null)
        {
        StartGet:
            string dirPath = await FileDialogNative.GetFolderPicker(title);

            if (string.IsNullOrEmpty(dirPath))
                return dirPath;

            string? existingCheckOverride;
            if (checkOverride != null && !string.IsNullOrEmpty(existingCheckOverride = checkOverride(dirPath)))
            {
                return existingCheckOverride;
            }

            if (!ConverterTool.IsUserHasPermission(dirPath))
            {
                await SpawnInvalidDialog(
                    Locale.Lang._Dialogs.InvalidGameDirNew2Title,
                    Locale.Lang._Dialogs.InvalidGameDirNew2Subtitle,
                    dirPath);
                goto StartGet;
            }

            if (IsRootPath(dirPath))
            {
                await SpawnInvalidDialog(
                    Locale.Lang._Dialogs.InvalidGameDirNew3Title,
                    Locale.Lang._Dialogs.InvalidGameDirNew3Subtitle,
                    dirPath);
                goto StartGet;
            }

            if (IsSystemDirPath(dirPath))
            {
                await SpawnInvalidDialog(
                    Locale.Lang._Dialogs.InvalidGameDirNew4Title,
                    Locale.Lang._Dialogs.InvalidGameDirNew4Subtitle,
                    dirPath);
                goto StartGet;
            }

            if (IsProgramDataPath(dirPath))
            {
                await SpawnInvalidDialog(
                    Locale.Lang._Dialogs.InvalidGameDirNew5Title,
                    Locale.Lang._Dialogs.InvalidGameDirNew5Subtitle,
                    dirPath);
                goto StartGet;
            }

            if (IsCollapseProgramPath(dirPath) || !CheckIfFolderIsValidLegacy(dirPath))
            {
                await SpawnInvalidDialog(
                    Locale.Lang._Dialogs.CannotUseAppLocationForGameDirTitle,
                    Locale.Lang._Dialogs.CannotUseAppLocationForGameDirSubtitle,
                    dirPath,
                    true);
                goto StartGet;
            }

            if (!IsProgramFilesPath(dirPath))
            {
                return dirPath;
            }

            await SpawnInvalidDialog(
                                     Locale.Lang._Dialogs.InvalidGameDirNew6Title,
                                     Locale.Lang._Dialogs.InvalidGameDirNew6Subtitle,
                                     dirPath);
            goto StartGet;


            async Task SpawnInvalidDialog(string dialogTitle, string message, string selectedPath, bool isUseLegacyFormatting = false)
            {
                TextBlock textBlock = new TextBlock
                {
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                };
                textBlock.AddTextBlockLine(message)
                .AddTextBlockNewLine(2)
                .AddTextBlockLine(Locale.Lang._Dialogs.InvalidGameDirNewSubtitleSelectedPath, weight: FontWeights.Bold)
                .AddTextBlockNewLine()
                .AddTextBlockLine(selectedPath);
                if (!isUseLegacyFormatting)
                {
                    textBlock.AddTextBlockNewLine(2)
                             .AddTextBlockLine(Locale.Lang._Dialogs.InvalidGameDirNewSubtitleSelectOther);
                }

                await SimpleDialogs.SpawnDialog(
                    isUseLegacyFormatting ? dialogTitle : string.Format(Locale.Lang._Dialogs.InvalidGameDirNewTitleFormat, dialogTitle),
                    textBlock,
                    (WindowUtility.CurrentWindow as MainWindow)?.Content,
                    Locale.Lang._Misc.Okay,
                    defaultButton: ContentDialogButton.Close,
                    dialogTheme: CustomControls.ContentDialogTheme.Error);
            }
        }

        private static bool IsSystemDirPath(ReadOnlySpan<char> path)
        {
            string? systemRootPath = Environment.GetEnvironmentVariable("SystemRoot");
            return path.StartsWith(systemRootPath);
        }

        private static bool IsProgramDataPath(ReadOnlySpan<char> path)
        {
            string? programDataPath = Environment.GetEnvironmentVariable("ProgramData");
            return path.StartsWith(programDataPath);
        }

        private static bool IsProgramFilesPath(ReadOnlySpan<char> path)
        {
            string? programFilesPath = Environment.GetEnvironmentVariable("ProgramFiles");
            return path.StartsWith(programFilesPath);
        }

        private static bool IsCollapseProgramPath(ReadOnlySpan<char> path)
        {
            string collapseProgramPath = LauncherConfig.AppExecutableDir;
            return path.StartsWith(collapseProgramPath);
        }

        /// <summary>
        /// Determines if the path given is a drive root
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <returns>True if path is root of the drive</returns>
        public static bool IsRootPath(ReadOnlySpan<char> path)
        {
            ReadOnlySpan<char> rootPath = Path.GetPathRoot(path);
            return rootPath.SequenceEqual(path);
        }

        private static bool CheckIfFolderIsValidLegacy(string basePath)
        {
            bool isInAppFolderExist = File.Exists(Path.Combine(basePath, LauncherConfig.AppExecutableName))
                                      || File.Exists(Path.Combine($@"{basePath}\..\", LauncherConfig.AppExecutableName))
                                      || File.Exists(Path.Combine($@"{basePath}\..\..\", LauncherConfig.AppExecutableName))
                                      || File.Exists(Path.Combine($@"{basePath}\..\..\..\", LauncherConfig.AppExecutableName));

            string? driveLetter = Path.GetPathRoot(basePath);
            if (string.IsNullOrEmpty(driveLetter))
            {
                return false;
            }

            bool isInAppFolderExist2 = basePath.EndsWith("Collapse Launcher")
                                       || basePath.StartsWith(Path.Combine(driveLetter, "Program Files"))
                                       || basePath.StartsWith(Path.Combine(driveLetter, "Program Files (x86)"))
                                       || basePath.StartsWith(Path.Combine(driveLetter, "Windows"));

            return !(isInAppFolderExist || isInAppFolderExist2);
        }

    }
}
