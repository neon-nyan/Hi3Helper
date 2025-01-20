using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.IO;

namespace CollapseLauncher.GameVersioning
{
    internal sealed class GameTypeGenshinVersion : GameVersionBase
    {
        #region Const
        private const string GlobalExecName = "GenshinImpact.exe";
        private const string AlternativeExecName = "YuanShen.exe";
        #endregion

        #region Properties
        public readonly List<string> _audioVoiceLanguageList = ["Chinese", "English(US)", "Japanese", "Korean"];
        #endregion

        public GameTypeGenshinVersion(UIElement parentUIElement, RegionResourceProp gameRegionProp, string gameName, string gamePreset)
            : base(parentUIElement, gameRegionProp, gameName, gamePreset)
        {
            // Try check for reinitializing game version.
            TryReinitializeGameVersion();
        }

        public override bool IsGameHasDeltaPatch() => false;

        public override DeltaPatchProperty GetDeltaPatchInfo() => null;

#nullable enable
        protected override string? TryFindGamePathFromExecutableAndConfig(string path, string? executableName)
        {
            string? basePath = base.TryFindGamePathFromExecutableAndConfig(path, executableName);

            // Try detect the CN and Bilibili client location by overriding the executable name argument
            if (string.IsNullOrEmpty(basePath))
            {
                executableName = AlternativeExecName;
                return base.TryFindGamePathFromExecutableAndConfig(path, executableName);
            }

            return basePath;
        }

        protected override bool IsExecutableFileExist(string? executableName)
        {
            bool isBaseExist = base.IsExecutableFileExist(executableName);
            if (!isBaseExist)
            {
                executableName = AlternativeExecName;
                isBaseExist = base.IsExecutableFileExist(executableName);
            }
            return isBaseExist;
        }

        protected override bool IsGameDataDirExist(string? executableName)
        {
            bool isBaseExist = base.IsGameDataDirExist(executableName);
            if (!isBaseExist)
            {
                executableName = AlternativeExecName;
                isBaseExist = base.IsGameDataDirExist(executableName);
            }
            return isBaseExist;
        }

        protected override bool IsGameVendorValid(string? executableName)
        {
            bool isBaseVendorValid = base.IsGameVendorValid(executableName);
            if (!isBaseVendorValid)
            {
                executableName = AlternativeExecName;
                isBaseVendorValid = base.IsGameVendorValid(executableName);
            }
            return isBaseVendorValid;
        }

        protected override bool IsGameExecDataDirValid(string? executableName)
        {
            string? fullExecPath, fullDirPath;

            // Phase 1: Check routine for CN & Bilibili
            if (GamePreset.GameExecutableName?.Equals(AlternativeExecName) ?? false)
            {
                // Check if the CN & Bilibili has Global files in it
                fullExecPath = Path.Combine(GameDirPath, GlobalExecName);
                fullDirPath = Path.Combine(GameDirPath, $"{Path.GetFileNameWithoutExtension(GlobalExecName)}");
                if (File.Exists(fullExecPath) || Directory.Exists(fullDirPath))
                    return false;
            }

            // Phase 2: Check routine for Global
            if (GamePreset.GameExecutableName?.Equals(GlobalExecName) ?? false)
            {
                // Check if the Global has CN & Bilibili files in it
                fullExecPath = Path.Combine(GameDirPath, AlternativeExecName);
                fullDirPath = Path.Combine(GameDirPath, $"{Path.GetFileNameWithoutExtension(AlternativeExecName)}");
                if (File.Exists(fullExecPath) || Directory.Exists(fullDirPath))
                    return false;
            }

            // Otherwise, return true
            return true;
        }

        protected override bool IsGameHasBilibiliStatus(string? executableName)
        {
            bool isBaseBilibiliValid = base.IsGameHasBilibiliStatus(executableName);
            if (!isBaseBilibiliValid)
            {
                executableName = AlternativeExecName;
                isBaseBilibiliValid = base.IsGameHasBilibiliStatus(executableName);
            }
            return isBaseBilibiliValid;
        }

        protected override void FixInvalidGameExecDataDir(string? executableName)
        {
            string? fullExecPath, fullDirPath;
            string? fullExecPathFrom = null, fullExecPathTo = null;
            string? fullDirPathFrom = null, fullDirPathTo = null;

            // Phase 1: Check routine for CN & Bilibili
            if (GamePreset.GameExecutableName?.Equals(AlternativeExecName) ?? false)
            {
                // Check if the CN & Bilibili has Global files in it
                fullExecPath = Path.Combine(GameDirPath, GlobalExecName);
                fullDirPath = Path.Combine(GameDirPath, $"{Path.GetFileNameWithoutExtension(GlobalExecName)}_Data");
                if (File.Exists(fullExecPath))
                {
                    fullExecPathFrom = fullExecPath;
                    fullExecPathTo = Path.Combine(GameDirPath, AlternativeExecName);
                }
                if (Directory.Exists(fullDirPath))
                {
                    fullDirPathFrom = fullDirPath;
                    fullDirPathTo = Path.Combine(GameDirPath, $"{Path.GetFileNameWithoutExtension(AlternativeExecName)}_Data");
                }
            }

            // Phase 2: Check routine for Global
            if (GamePreset.GameExecutableName?.Equals(GlobalExecName) ?? false)
            {
                // Check if the Global has CN & Bilibili files in it
                fullExecPath = Path.Combine(GameDirPath, AlternativeExecName);
                fullDirPath = Path.Combine(GameDirPath, $"{Path.GetFileNameWithoutExtension(AlternativeExecName)}_Data");
                if (File.Exists(fullExecPath))
                {
                    fullExecPathFrom = fullExecPath;
                    fullExecPathTo = Path.Combine(GameDirPath, GlobalExecName);

                    FileInfo oldFileInfo = new FileInfo(fullExecPathFrom);
                    FileInfo newFileInfo = new FileInfo(fullExecPathTo);
                    bool isOldFileSymbolLink = oldFileInfo.Exists && oldFileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
                    bool isNewFileSymbolLink = newFileInfo.Exists && newFileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);

                    if (isOldFileSymbolLink || isNewFileSymbolLink)
                        throw new InvalidDataException("Collapse cannot fix file with symbolic link. Please resolve the issue manually!");
                }
                if (Directory.Exists(fullDirPath))
                {
                    fullDirPathFrom = fullDirPath;
                    fullDirPathTo = Path.Combine(GameDirPath, $"{Path.GetFileNameWithoutExtension(GlobalExecName)}_Data");
                }
            }

            if (!string.IsNullOrEmpty(fullExecPathFrom) && !string.IsNullOrEmpty(fullExecPathTo))
                File.Move(fullExecPathFrom, fullExecPathTo, true);

            if (!string.IsNullOrEmpty(fullDirPathFrom) && !string.IsNullOrEmpty(fullDirPathTo))
            {
                DirectoryInfo oldDirInfo = new DirectoryInfo(fullDirPathFrom);
                DirectoryInfo newDirInfo = new DirectoryInfo(fullDirPathTo);
                bool isOldDirSymbolLink = oldDirInfo.Exists && oldDirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
                bool isNewDirSymbolLink = newDirInfo.Exists && newDirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);

                if (isOldDirSymbolLink || isNewDirSymbolLink)
                    throw new InvalidDataException("Collapse cannot fix directory with symbolic link. Please resolve the issue manually!");

                if (newDirInfo.Exists)
                {
                    foreach (FileInfo oldFileInfo in oldDirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        string oldDirPath = oldDirInfo.FullName;
                        string oldFilePathRel = oldFileInfo.FullName.Substring(oldDirPath.Length).Trim('\\');
                        string newFilePath = Path.Combine(fullDirPathTo, oldFilePathRel);
                        string? newFileDirPath = Path.GetDirectoryName(newFilePath);

                        if (!string.IsNullOrEmpty(newFileDirPath) && !Directory.Exists(newFileDirPath))
                            Directory.CreateDirectory(newFileDirPath);

                        oldFileInfo.MoveTo(newFilePath, true);
                    }

                    oldDirInfo.Delete(true);
                }
                else
                {
                    oldDirInfo.MoveTo(fullDirPathTo);
                }
            }
        }
    }
}
