using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Win32.Native.LibraryImport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault

namespace CollapseLauncher
{
    internal static partial class StarRailRepairExtension
    {
        internal static string ReplaceStreamingToPersistentPath(string inputPath, string execName, FileType type)
        {
            string parentStreamingRelativePath = string.Format(type switch
            {
                FileType.Block => StarRailRepair.AssetGameBlocksStreamingPath,
                FileType.Audio => StarRailRepair.AssetGameAudioStreamingPath,
                FileType.Video => StarRailRepair.AssetGameVideoStreamingPath,
                _ => string.Empty
            }, execName);
            string parentPersistentRelativePath = string.Format(type switch
            {
                FileType.Block => StarRailRepair.AssetGameBlocksPersistentPath,
                FileType.Audio => StarRailRepair.AssetGameAudioPersistentPath,
                FileType.Video => StarRailRepair.AssetGameVideoPersistentPath,
                _ => string.Empty
            }, execName);

            int indexOfStart = inputPath.IndexOf(parentStreamingRelativePath, StringComparison.Ordinal);
            int indexOfEnd   = indexOfStart + parentStreamingRelativePath.Length;

            if (indexOfStart == -1) return inputPath;

            ReadOnlySpan<char> startOfPath = inputPath.AsSpan(0, indexOfStart).TrimEnd('\\');
            ReadOnlySpan<char> endOfPath = inputPath.AsSpan(indexOfEnd, inputPath.Length - indexOfEnd).TrimStart('\\');

            string returnPath = Path.Join(startOfPath, parentPersistentRelativePath, endOfPath);
            return returnPath;
        }

        internal static string GetFileRelativePath(string inputPath, string parentPath) => inputPath.AsSpan(parentPath.Length).ToString();
    }

    internal partial class StarRailRepair
    {
        private async Task Check(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            // Try to find "badlist.byte" files in the game folder and delete it
            foreach (string badListFile in Directory.EnumerateFiles(GamePath, "*badlist*.byte*", SearchOption.AllDirectories))
            {
                LogWriteLine($"Removing bad list mark at: {badListFile}", LogType.Warning, true);
                TryDeleteReadOnlyFile(badListFile);
            }

            // Try to find "verify.fail" files in the game folder and delete it
            foreach (string verifyFail in Directory.EnumerateFiles(GamePath, "*verify*.fail*", SearchOption.AllDirectories))
            {
                LogWriteLine($"Removing verify.fail mark at: {verifyFail}", LogType.Warning, true);
                TryDeleteReadOnlyFile(verifyFail);
            }

            List<FilePropertiesRemote> brokenAssetIndex = [];

            // Set Indetermined status as false
            Status.IsProgressAllIndetermined     = false;
            Status.IsProgressPerFileIndetermined = false;

            // Show the asset entry panel
            Status.IsAssetEntryPanelShow = true;

            // Await the task for parallel processing
            try
            {
                // Iterate assetIndex and check it using different method for each type and run it in parallel
                await Parallel.ForEachAsync(assetIndex, new ParallelOptions { MaxDegreeOfParallelism = ThreadCount, CancellationToken = token }, async (asset, threadToken) =>
                {
                    // Assign a task depends on the asset type
                    switch (asset.FT)
                    {
                        case FileType.Generic:
                            await CheckGenericAssetType(asset, brokenAssetIndex, threadToken);
                            break;
                        case FileType.Block:
                        case FileType.Audio:
                        case FileType.Video:
                            await CheckAssetType(asset, brokenAssetIndex, threadToken);
                            break;
                    }
                });
            }
            catch (AggregateException ex)
            {
                var innerExceptionsFirst = ex.Flatten().InnerExceptions.First();
                await SentryHelper.ExceptionHandlerAsync(innerExceptionsFirst, SentryHelper.ExceptionType.UnhandledOther);
                throw innerExceptionsFirst;
            }

            // Re-add the asset index with a broken asset index
            assetIndex.Clear();
            assetIndex.AddRange(brokenAssetIndex);
        }

        #region AssetTypeCheck
        private async Task CheckGenericAssetType(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
        {
            // Update activity status
            Status.ActivityStatus = string.Format(Lang._GameRepairPage.Status6,
                                                   StarRailRepairExtension.GetFileRelativePath(asset.N, GamePath));

            // Increment current total count
            ProgressAllCountCurrent++;

            // Reset per file size counter
            ProgressPerFileSizeTotal = asset.S;
            ProgressPerFileSizeCurrent = 0;

            // Get the file info
            FileInfo fileInfo = new FileInfo(asset.N);

            // Check if the file exist or has unmatched size
            if (!fileInfo.Exists)
            {
                AddIndex();
                LogWriteLine($"File [T: {asset.FT}]: {asset.N} is not found", LogType.Warning, true);
                return;
            }

            if (fileInfo.Length != asset.S)
            {
                if (fileInfo.Name.Contains("pkg_version")) return;
                AddIndex();
                LogWriteLine($"File [T: {asset.FT}]: {asset.N} has unmatched size " +
                             $"(Local: {fileInfo.Length} <=> Remote: {asset.S}",
                             LogType.Warning, true);
                return;
            }

            // Skip CRC check if fast method is used
            if (UseFastMethod)
            {
                return;
            }

            // Open and read fileInfo as FileStream 
            await using FileStream fileStream = await NaivelyOpenFileStreamAsync(fileInfo, FileMode.Open, FileAccess.Read, FileShare.Read);
            // If pass the check above, then do CRC calculation
            // Additional: the total file size progress is disabled and will be incremented after this
            byte[] localCrc = await GetCryptoHashAsync<MD5>(fileStream, null, true, true, token);

            // If local and asset CRC doesn't match, then add the asset
            if (IsArrayMatch(localCrc, asset.CRCArray))
            {
                return;
            }

            ProgressAllSizeFound += asset.S;
            ProgressAllCountFound++;

            Dispatch(() => AssetEntry.Add(
                                          new AssetProperty<RepairAssetType>(
                                                                             Path.GetFileName(asset.N),
                                                                             ConvertRepairAssetTypeEnum(asset.FT),
                                                                             Path.GetDirectoryName(asset.N),
                                                                             asset.S,
                                                                             localCrc,
                                                                             asset.CRCArray
                                                                            )
                                         ));

            // Mark the main block as "need to be repaired"
            asset.IsBlockNeedRepair = true;
            targetAssetIndex.Add(asset);

            LogWriteLine($"File [T: {asset.FT}]: {asset.N} is broken! Index CRC: {asset.CRC} <--> File CRC: {HexTool.BytesToHexUnsafe(localCrc)}", LogType.Warning, true);
            return;

            void AddIndex()
            {
                // Update the total progress and found counter
                ProgressAllSizeFound += asset.S;
                ProgressAllCountFound++;

                // Set the per size progress
                ProgressPerFileSizeCurrent = asset.S;

                // Increment the total current progress
                ProgressAllSizeCurrent += asset.S;

                Dispatch(() => AssetEntry.Add(
                                              new AssetProperty<RepairAssetType>(
                                                   Path.GetFileName(asset.N),
                                                   ConvertRepairAssetTypeEnum(asset.FT),
                                                   Path.GetDirectoryName(asset.N),
                                                   asset.S,
                                                   null,
                                                   null
                                                  )
                                             ));
                targetAssetIndex.Add(asset);
            }
        }

        private async Task CheckAssetType(FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
        {
            // Update activity status
            Status.ActivityStatus = string.Format(Lang._GameRepairPage.Status6,
                                                   StarRailRepairExtension.GetFileRelativePath(asset.N, GamePath));

            // Increment current total count
            ProgressAllCountCurrent++;

            // Reset per file size counter
            ProgressPerFileSizeTotal = asset.S;
            ProgressPerFileSizeCurrent = 0;

            // Get persistent and streaming paths
            FileInfo fileInfoPersistent = new FileInfo(StarRailRepairExtension.ReplaceStreamingToPersistentPath(asset.N, ExecName, asset.FT));
            FileInfo fileInfoStreaming = new FileInfo(asset.N);

            bool usePersistent = asset.IsPatchApplicable || !fileInfoStreaming.Exists;
            bool isHasMark = asset.IsHasHashMark || usePersistent;
            bool isPersistentExist = fileInfoPersistent.Exists && fileInfoPersistent.Length == asset.S;
            bool isStreamingExist = fileInfoStreaming.Exists && fileInfoStreaming.Length == asset.S;

            // Update the local path to full persistent or streaming path and add asset for missing/unmatched size file
            asset.N = usePersistent ? fileInfoPersistent.FullName : fileInfoStreaming.FullName;

            // Check if the file exist on both persistent and streaming path for non-patch file, then mark the
            // persistent path as redundant (unused)
            bool isNonPatchHasRedundantPersistent = !asset.IsPatchApplicable && isPersistentExist && isStreamingExist && fileInfoStreaming.Length == asset.S;

            if (isNonPatchHasRedundantPersistent)
            {
                // Add the count and asset. Mark the type as "RepairAssetType.Unused"
                ProgressAllCountFound++;

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(fileInfoPersistent.FullName),
                        RepairAssetType.Unused,
                        Path.GetDirectoryName(fileInfoPersistent.FullName),
                        asset.S,
                        null,
                        null
                    )
                ));

                // Create a new instance as unused one
                FilePropertiesRemote unusedAsset = new FilePropertiesRemote
                {
                    N = fileInfoPersistent.FullName,
                    FT = FileType.Unused,
                    RN = asset.RN,
                    CRC = asset.CRC,
                    S = asset.S
                };
                targetAssetIndex.Add(unusedAsset);

                LogWriteLine($"File [T: {asset.FT}]: {unusedAsset.N} is redundant (exist both on persistent and streaming)", LogType.Warning, true);
            }

            // If the file has Hash Mark or is persistent, then create the hash mark file
            if (isHasMark) CreateHashMarkFile(asset.N, asset.CRC);

            // Check if both location has the file exist or has the size right
            if ((usePersistent && !isPersistentExist && !isStreamingExist)
             || (usePersistent && !isPersistentExist))
            {
                // Update the total progress and found counter
                ProgressAllSizeFound += asset.S;
                ProgressAllCountFound++;

                // Set the per size progress
                ProgressPerFileSizeCurrent = asset.S;

                // Increment the total current progress
                ProgressAllSizeCurrent += asset.S;

                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(asset.N),
                        ConvertRepairAssetTypeEnum(asset.FT),
                        Path.GetDirectoryName(asset.N),
                        asset.S,
                        null,
                        null
                    )
                ));
                targetAssetIndex.Add(asset);
                LogWriteLine($"File [T: {asset.FT}]: {asset.N} is not found or has unmatched size", LogType.Warning, true);

                return;
            }

            // Skip CRC check if fast method is used
            if (UseFastMethod)
            {
                return;
            }

            // Open and read fileInfo as FileStream
            string fileNameToOpen = usePersistent ? fileInfoPersistent.FullName : fileInfoStreaming.FullName;
            try
            {
                await CheckFile(fileNameToOpen, asset, targetAssetIndex, token);
            }
            catch (FileNotFoundException ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex);
                LogWriteLine($"File {fileNameToOpen} is not found while UsePersistent is {usePersistent}. " +
                             $"Creating hard link and retrying...", LogType.Warning, true);

                var targetFile = File.Exists(fileInfoPersistent.FullName) ? fileInfoPersistent.FullName : 
                    File.Exists(fileInfoStreaming.FullName)           ? fileInfoStreaming.FullName : 
                                                                        throw new FileNotFoundException(fileNameToOpen);

                PInvoke.CreateHardLink(fileNameToOpen, targetFile, IntPtr.Zero);
                await CheckFile(fileNameToOpen, asset, targetAssetIndex, token);
            }
        }

        private async Task CheckFile(string fileNameToOpen, FilePropertiesRemote asset, List<FilePropertiesRemote> targetAssetIndex, CancellationToken token)
        {
            await using FileStream fileStream = new FileStream(fileNameToOpen,
                                                           FileMode.Open,
                                                           FileAccess.Read,
                                                           FileShare.Read,
                                                           BufferBigLength);
            // If pass the check above, then do CRC calculation
            // Additional: the total file size progress is disabled and will be incremented after this
            byte[] localCrc = await GetCryptoHashAsync<MD5>(fileStream, null, true, true, token);

            // If local and asset CRC doesn't match, then add the asset
            if (!IsArrayMatch(localCrc, asset.CRCArray))
            {
                ProgressAllSizeFound += asset.S;
                ProgressAllCountFound++;

                Dispatch(() => AssetEntry.Add(
                                              new AssetProperty<RepairAssetType>(
                                                   Path.GetFileName(asset.N),
                                                   ConvertRepairAssetTypeEnum(asset.FT),
                                                   Path.GetDirectoryName(asset.N),
                                                   asset.S,
                                                   localCrc,
                                                   asset.CRCArray
                                                  )
                                             ));

                // Mark the main block as "need to be repaired"
                asset.IsBlockNeedRepair = true;
                targetAssetIndex.Add(asset);

                LogWriteLine($"File [T: {asset.FT}]: {asset.N} is broken! Index CRC: {asset.CRC} <--> File CRC: {HexTool.BytesToHexUnsafe(localCrc)}", LogType.Warning, true);
            }
        }

        private static void CreateHashMarkFile(string filePath, string hash)
        {
            RemoveHashMarkFile(filePath, out var basePath, out var baseName);

            // Create base path if not exist
            if (!string.IsNullOrEmpty(basePath) && !Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);

            // Re-create the hash file
            string toName = Path.Combine(basePath ?? "", $"{baseName}_{hash}.hash");
            if (File.Exists(toName)) return;
            File.Create(toName).Dispose();
        }

        private static void RemoveHashMarkFile(string filePath, out string basePath, out string baseName)
        {
            // Get the base path and name
            basePath = Path.GetDirectoryName(filePath);
            baseName = Path.GetFileNameWithoutExtension(filePath);

            // Get directory base info. If it doesn't exist, return
            if (string.IsNullOrEmpty(basePath))
            {
                return;
            }

            DirectoryInfo basePathDirInfo = new DirectoryInfo(basePath);
            if (!basePathDirInfo.Exists)
            {
                return;
            }

            // Enumerate any possible existing hash path and delete it
            foreach (FileInfo existingPath in basePathDirInfo.EnumerateFiles($"{baseName}_*.hash")
                .EnumerateNoReadOnly())
            {
                existingPath.Delete();
            }
        }
        #endregion
    }
}
