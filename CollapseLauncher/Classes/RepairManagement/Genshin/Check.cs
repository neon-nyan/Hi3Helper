using CollapseLauncher.GameVersioning;
using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.SentryHelper;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    internal partial class GenshinRepair
    {
        private async Task Check(List<PkgVersionProperties> assetIndex, CancellationToken token)
        {
            List<PkgVersionProperties> brokenAssetIndex = new List<PkgVersionProperties>();

            // Set Indetermined status as false
            _status!.IsProgressAllIndetermined = false;
            _status.IsProgressPerFileIndetermined = false;

            // Show the asset entry panel
            _status.IsAssetEntryPanelShow = true;

            // Reset stopwatch
            RestartStopwatch();

            // Ensure to delete the DXSetup files
            EnsureDeleteDXSetupFiles();

            // Try to move persistent files to StreamingAssets
            if (_isParsePersistentManifestSuccess) TryMovePersistentToStreamingAssets(assetIndex);

            // Check for any redundant files
            await CheckRedundantFiles(brokenAssetIndex, token);

            // Await the task for parallel processing
            try
            {
                var threadCount = _threadCount;
                var isSsd = Hi3Helper.Win32.Native.PInvoke.IsDriveSsd(_gameStreamingAssetsPath,
                                                                      ILoggerHelper.GetILogger());
                if (!isSsd)
                {
                    threadCount = 1;
                    LogWriteLine($"The drive is not SSD, the repair process will be slower!.\r\n\t" +
                                 $"Thread count set to {threadCount}.", LogType.Warning, true);
                }
                
                var runningTask = new ConcurrentDictionary<PkgVersionProperties, byte>();
                // Await the task for parallel processing
                // and iterate assetIndex and check it using different method for each type and run it in parallel
                await Parallel.ForEachAsync(assetIndex, new ParallelOptions { MaxDegreeOfParallelism = threadCount, CancellationToken = token }, async (asset, threadToken) =>
                {
                    if (!runningTask.TryAdd(asset, 0))
                    {
                        LogWriteLine($"Found duplicated task for {asset.remoteURL}! Skipping...", LogType.Warning, true);
                        return;
                    }
                    await CheckAssetAllType(asset, brokenAssetIndex, threadToken);
                    runningTask.Remove(asset, out _);
                });
            }
            catch (AggregateException ex)
            {
                var innerExceptionsFirst = ex.Flatten().InnerExceptions.First();
                SentryHelper.ExceptionHandler(innerExceptionsFirst, SentryHelper.ExceptionType.UnhandledOther);
                throw innerExceptionsFirst;
            }

            // Re-add the asset index with a broken asset index
            assetIndex.Clear();
            assetIndex.AddRange(brokenAssetIndex);
        }

        private void EnsureDeleteDXSetupFiles()
        {
            // Check if the DXSETUP file is exist, then delete it.
            // The DXSETUP files causes some false positive detection of data modification
            // for some games (like Genshin, which causes 4302-x error for some reason)
            string dxSetupDir = Path.Combine(_gamePath, "DXSETUP");
            TryDeleteReadOnlyDir(dxSetupDir);
        }

        private void TryMovePersistentToStreamingAssets(IEnumerable<PkgVersionProperties> assetIndex)
        {
            if (!Directory.Exists(_gamePersistentPath)) return;
            TryMoveAudioPersistent(assetIndex);
            TryMoveVideoPersistent();
        }

        private void TryMoveAudioPersistent(IEnumerable<PkgVersionProperties> assetIndex)
        {
            // Try to get the exclusion list of the audio (language specific) files
            string[] exclusionList = assetIndex
                .Where(x => x.isForceStoreInPersistent && x.remoteName
                    .AsSpan()
                    .EndsWith(".pck"))
                .Select(x => x.remoteNamePersistent
                    .Replace('/', '\\'))
                .ToArray();

            // Get the audio directory paths and create if it doesn't exist
            string audioAsbPath = Path.Combine(_gameStreamingAssetsPath, "AudioAssets");
            string audioPersistentPath = Path.Combine(_gamePersistentPath, "AudioAssets");
            if (!Directory.Exists(audioPersistentPath)) return;
            if (!Directory.Exists(audioAsbPath)) Directory.CreateDirectory(audioAsbPath);

            // Get the list of audio language names from _gameVersionManager
            List<string> audioLangList = ((GameTypeGenshinVersion)_gameVersionManager)._audioVoiceLanguageList;

            // Enumerate the content of audio persistent directory
            foreach (string path in Directory.EnumerateDirectories(audioPersistentPath, "*", SearchOption.TopDirectoryOnly))
            {
                // Get the last path section as language name to compare
                string langName = Path.GetFileName(path);

                // If the path section matches the name in language list, then continue
                if (audioLangList.Contains(langName))
                {
                    // Enumerate the files that's exist in the persistent path of each language
                    // except the one that's included in the exclusion list
                    foreach (string filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                        .Where(x => !exclusionList.Any(y => x.AsSpan().EndsWith(y.AsSpan()))))
                    {
                        // Trim the path name to get the generic languageName/filename form
                        string pathName = filePath.AsSpan().Slice(audioPersistentPath.Length + 1).ToString();
                        // Combine the generic name with audioAsbPath
                        string newPath = EnsureCreationOfDirectory(Path.Combine(audioAsbPath, pathName));

                        // Try to move the file to the asb path
                        File.Move(filePath, newPath, true);
                    }
                }
            }
        }

        private void TryMoveVideoPersistent()
        {
            string videoAsbPath = Path.Combine(_gameStreamingAssetsPath, "VideoAssets");
            string videoPersistentPath = Path.Combine(_gamePersistentPath, "VideoAssets");
            if (!Directory.Exists(videoPersistentPath)) return;
            if (!Directory.Exists(videoAsbPath)) Directory.CreateDirectory(videoAsbPath);
            MoveFolderContent(videoPersistentPath, videoAsbPath);
        }

#nullable enable
        private async ValueTask CheckAssetAllType(PkgVersionProperties asset, List<PkgVersionProperties> targetAssetIndex, CancellationToken token)
        {
            // Update activity status
            _status!.ActivityStatus = string.Format(Lang._GameRepairPage.Status6, asset.remoteName);

            // Increment current total count
            _progressAllCountCurrent++;

            // Reset per file size counter
            _progressPerFileSizeTotal = asset.fileSize;
            _progressPerFileSizeCurrent = 0;

            // Get file path
            string filePathStreaming = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.remoteName));
            string filePathPersistent = Path.Combine(_gamePath, ConverterTool.NormalizePath(asset.remoteNamePersistent));

            // Get persistent and streaming paths
            FileInfo fileInfoStreaming  = new FileInfo(filePathStreaming).EnsureNoReadOnly();
            FileInfo fileInfoPersistent = new FileInfo(filePathPersistent).EnsureNoReadOnly();

            // Decide file state
            bool isUsePersistent   = asset.isForceStoreInPersistent;
            bool isPatch           = asset.isPatch;
            bool isPersistentExist = fileInfoPersistent.Exists;
            bool isStreamingExist  = fileInfoStreaming.Exists;

            // Try to get hash buffer
            bool   isUseXxh64Hash = !string.IsNullOrEmpty(asset.xxh64hash);
            int    hashBufferLen  = (isUseXxh64Hash ? asset.xxh64hash.Length : asset.md5.Length) / 2;
            byte[] hashBuffer     = ArrayPool<byte>.Shared.Rent(hashBufferLen);

            try
            {
                if (!HexTool.TryHexToBytesUnsafe(asset.md5, hashBuffer))
                {
#if DEBUG
                    throw new InvalidOperationException();
#endif
                }

                // Get the file info to use
                bool isUnmatchedFileFound = !await IsFileMatched(
                                             a =>
                                             {
                                                 asset.isForceStoreInStreaming = a;
                                                 if (a)
                                                 {
                                                     asset.localName = filePathStreaming;
                                                 }
                                             },
                                             b =>
                                             {
                                                 asset.isForceStoreInPersistent = b;
                                                 if (b)
                                                 {
                                                     asset.localName = filePathPersistent;
                                                 }
                                             },
                                             (c, d) =>
                                             {
                                                 // Add the count and asset. Mark the type as "RepairAssetType.Unused"
                                                 _progressAllCountFound++;

                                                 Dispatch(() => AssetEntry.Add(
                                                                               new AssetProperty<RepairAssetType>(
                                                                                    Path.GetFileName(c),
                                                                                    RepairAssetType.Unused,
                                                                                    Path.GetDirectoryName(c),
                                                                                    asset.fileSize,
                                                                                    null,
                                                                                    null
                                                                                   )
                                                                              ));

                                                 asset.type      = "Unused";
                                                 asset.localName = d;
                                                 targetAssetIndex.Add(asset);

                                                 LogWriteLine($"File [T: {asset.type}]: {c} is redundant (exist both in persistent and streaming)", LogType.Warning, true);
                                             }
                                             );

                FileInfo fileInfoToUse = asset.isForceStoreInPersistent ? fileInfoPersistent : fileInfoStreaming;

                if (isUnmatchedFileFound)
                {
                    AddNotFoundOrMismatchAsset(asset, fileInfoToUse);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(hashBuffer);
            }

            return;

            async ValueTask<bool> IsFileMatched(Action<bool> storeAsStreaming, Action<bool> storeAsPersistent, Action<string, string> storeAsUnused)
            {
                if (!isUsePersistent)
                {
                    if (!isStreamingExist || !await IsFileHashMatch(fileInfoStreaming, hashBuffer, token))
                    {
                        if (isPersistentExist)
                        {
                            storeAsUnused(asset.remoteNamePersistent, fileInfoPersistent.FullName);
                        }

                        storeAsStreaming(true);
                        storeAsPersistent(false);
                        return false;
                    }

                    return true;
                }

                bool isStreamingMatch = isStreamingExist && await IsFileHashMatch(fileInfoStreaming, hashBuffer, token);
                bool isPersistentMatch = isPersistentExist && await IsFileHashMatch(fileInfoPersistent, hashBuffer, token);

                if (isStreamingMatch && isPersistentMatch && isPatch)
                {
                    storeAsUnused(asset.remoteName, fileInfoStreaming.FullName);
                }

                if (!isStreamingMatch && !isPersistentMatch)
                {
                    storeAsStreaming(false);
                    storeAsPersistent(true);
                    return false;
                }

                return true;
            }

            async ValueTask<bool> IsFileHashMatch(FileInfo fileInfo, ReadOnlyMemory<byte> hashToCompare, CancellationToken cancelToken)
            {
                if (_useFastMethod) return true; // Skip the hash calculation if the fast method is enabled
                // Refresh the fileInfo
                fileInfo.Refresh();

                // Try to get filestream
                await using FileStream fileStream = await fileInfo.NaivelyOpenFileStreamAsync(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                // If pass the check above, then do hash calculation
                // Additional: the total file size progress is disabled and will be incremented after this
                byte[] localHash = hashToCompare.Length == 8 ?
                    await CheckNonCryptoHashAsync(fileStream, new XxHash64(), cancelToken) :
                    await CheckHashAsync(fileStream, MD5.Create(), token);

                // Check if the hash is equal
                bool isMatch = IsArrayMatch(hashToCompare.Span, localHash);
                return isMatch;
            }

            void AddNotFoundOrMismatchAsset(PkgVersionProperties assetInner, FileInfo repairFile)
            {
                // Update the total progress and found counter
                _progressAllSizeFound += assetInner.fileSize;
                _progressAllCountFound++;

                // Set the per size progress
                _progressPerFileSizeCurrent = assetInner.fileSize;

                // Increment the total current progress
                _progressAllSizeCurrent += assetInner.fileSize;

                Dispatch(() => AssetEntry.Add(
                                              new AssetProperty<RepairAssetType>(
                                                   repairFile.Name,
                                                   RepairAssetType.Generic,
                                                   Path.GetDirectoryName(assetInner.isForceStoreInPersistent ? assetInner.remoteNamePersistent : assetInner.remoteName),
                                                   assetInner.fileSize,
                                                   repairFile.Exists ? hashBuffer : null,
                                                   HexTool.HexToBytesUnsafe(string.IsNullOrEmpty(assetInner.xxh64hash) ? assetInner.md5 : assetInner.xxh64hash)
                                                  )
                                             ));
                targetAssetIndex.Add(assetInner);

                LogWriteLine($"File [T: {RepairAssetType.Generic}]: {assetInner.localName} is not found or has unmatched size",
                             LogType.Warning, true);
            }
        }
#nullable restore

        #region UnusedFiles
        private async Task CheckRedundantFiles(List<PkgVersionProperties> targetAssetIndex, CancellationToken token)
        {
            // Initialize FilePath and FileInfo
            string FilePath;
            FileInfo fInfo;

            // Iterate the available deletefiles files
            DirectoryInfo directoryInfo = new DirectoryInfo(_gamePath);
            foreach (FileInfo listFile in directoryInfo
                .EnumerateFiles("*deletefiles*", SearchOption.TopDirectoryOnly)
                .EnumerateNoReadOnly())
            {
                LogWriteLine($"deletefiles file list path: {listFile}", LogType.Default, true);

                // Use deletefiles files to get the list of the redundant file
                await using Stream fs         = await listFile.NaivelyOpenFileStreamAsync(FileMode.Open, FileAccess.Read, FileShare.None, FileOptions.DeleteOnClose);
                using StreamReader listReader = new StreamReader(fs);
                while (!listReader.EndOfStream)
                {
                    // Get the File name and FileInfo
                    FilePath = Path.Combine(_gamePath, ConverterTool.NormalizePath(await listReader.ReadLineAsync(token)));
                    fInfo    = new FileInfo(FilePath).EnsureNoReadOnly();

                    // If the file doesn't exist, then continue
                    if (!fInfo.Exists)
                        continue;

                    // Update total found progress
                    _progressAllCountFound++;

                    // Get the stripped relative name
                    string strippedName = fInfo.FullName.AsSpan()[(_gamePath.Length + 1)..].ToString();

                    // Assign the asset before adding to targetAssetIndex
                    PkgVersionProperties asset = new PkgVersionProperties
                    {
                        localName = strippedName,
                        fileSize  = fInfo.Length,
                        type      = RepairAssetType.Unused.ToString()
                    };
                    Dispatch(() => AssetEntry.Add(
                                                  new AssetProperty<RepairAssetType>(
                                                       Path.GetFileName(asset.localName),
                                                       RepairAssetType.Unused,
                                                       Path.GetDirectoryName(asset.localName),
                                                       asset.fileSize,
                                                       null,
                                                       null
                                                      )
                                                 ));

                    // Add the asset into targetAssetIndex
                    targetAssetIndex.Add(asset);
                    LogWriteLine($"Redundant file has been found: {strippedName}", LogType.Default, true);
                }
            }

            // Iterate redundant diff and temporary files
            foreach (FileInfo fileInfo in directoryInfo.EnumerateFiles("*.*", SearchOption.AllDirectories)
                .EnumerateNoReadOnly()
                .Where(x => x.Name.EndsWith(".diff",  StringComparison.OrdinalIgnoreCase)
                                 || x.Name.EndsWith("_tmp",   StringComparison.OrdinalIgnoreCase)
                                 || x.Name.EndsWith(".hdiff", StringComparison.OrdinalIgnoreCase)))
            {
                // Update total found progress
                _progressAllCountFound++;

                // Get the stripped relative name
                string strippedName = fileInfo.FullName.AsSpan()[(_gamePath.Length + 1)..].ToString();

                // Assign the asset before adding to targetAssetIndex
                PkgVersionProperties asset = new PkgVersionProperties
                {
                    localName = strippedName,
                    fileSize  = fileInfo.Length,
                    type      = RepairAssetType.Unused.ToString()
                };
                Dispatch(() => AssetEntry.Add(
                    new AssetProperty<RepairAssetType>(
                        Path.GetFileName(asset.localName),
                        RepairAssetType.Unused,
                        Path.GetDirectoryName(asset.localName),
                        asset.fileSize,
                        null,
                        null
                    )
                ));

                // Add the asset into targetAssetIndex
                targetAssetIndex.Add(asset);
                LogWriteLine($"Redundant file has been found: {strippedName}", LogType.Default, true);
            }
        }
        #endregion
    }
}
