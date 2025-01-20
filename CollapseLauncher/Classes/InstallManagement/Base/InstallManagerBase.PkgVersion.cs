using CollapseLauncher.Helper;
using CollapseLauncher.Helper.JsonConverter;
using CollapseLauncher.Helper.Loading;
using CollapseLauncher.Pages;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.SentryHelper;
using static Hi3Helper.Logger;

// ReSharper disable GrammarMistakeInComment
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace
// ReSharper disable ArrangeObjectCreationWhenTypeEvident

#nullable enable
namespace CollapseLauncher.InstallManager.Base
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(LocalFileInfo))]
    internal sealed partial class LocalFileInfoJsonContext : JsonSerializerContext;

    public sealed class LocalFileInfo
    {
        [JsonPropertyName("remoteName")]
        [JsonConverter(typeof(SlashToBackslashConverter))]
        public string RelativePath { get; set; }

        [JsonPropertyName("md5")]
        [JsonConverter(typeof(HexStringToBytesConverter))]
        public byte[] MD5Hash { get; set; }

        [JsonPropertyName("hash")]
        [JsonConverter(typeof(HexStringToBytesConverter))]
        public byte[] Xxh64Hash { get; set; }

        [JsonPropertyName("fileSize")] public long FileSize { get; set; }

        [JsonIgnore] public string FullPath { get; set; }

        [JsonIgnore] public string FileName { get; set; }

        [JsonIgnore] public bool IsFileExist { get; set; }

    #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public LocalFileInfo()
        {
        }

        public LocalFileInfo(FileSystemInfo fileInfo, string basePath)
        {
            FullPath     = fileInfo.FullName;
            FileName     = Path.GetFileName(fileInfo.FullName);
            RelativePath = GetRelativePath(FullPath, basePath);
            Update();
        }
    #pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        private static string GetRelativePath(string fullPath, string basePath)
        {
            ReadOnlySpan<char> relativePath = fullPath.AsSpan(basePath.Length).TrimStart('\\');
            return relativePath.ToString();
        }

        public void Update()
        {
            FileInfo fileInfo = new FileInfo(FullPath);
            IsFileExist = fileInfo.Exists;
            if (fileInfo.Exists)
            {
                FileSize = fileInfo.Length;
            }
        }

        public FileInfo ToFileInfo()
        {
            return new FileInfo(string.IsNullOrEmpty(FullPath) ? RelativePath : FullPath);
        }

        public override string ToString()
        {
            return RelativePath;
        }
    }

    internal partial class InstallManagerBase
    {
        [StringSyntax("Regex")]
        protected const string NonGameFileRegexPattern = @"(\.\d\d\d|(zip|7z)|patch)|\.$";

        [GeneratedRegex(NonGameFileRegexPattern, RegexOptions.NonBacktracking)]
        private static partial Regex GetNonGameFileRegex();
        private static readonly Regex NonGameFileRegex = GetNonGameFileRegex();

        public virtual async ValueTask CleanUpGameFiles(bool withDialog = true)
        {
            // Get the unused file info asynchronously
            (List<LocalFileInfo>, long) unusedFileInfo = await GetUnusedFileInfoList(withDialog);
            
            // Spawn dialog if used
            if (withDialog)
            {
                if (WindowUtility.CurrentWindow is MainWindow mainWindow)
                {
                    mainWindow.overlayFrame.BackStack?.Clear();
                    mainWindow.overlayFrame.Navigate(typeof(NullPage));
                    mainWindow.overlayFrame.Navigate(typeof(FileCleanupPage), null,
                                                     new DrillInNavigationTransitionInfo());
                }
                
                if (FileCleanupPage.Current == null)
                    return;
                await FileCleanupPage.Current.InjectFileInfoSource(unusedFileInfo.Item1, unusedFileInfo.Item2);
                
                LoadingMessageHelper.HideLoadingFrame();
                
                FileCleanupPage.Current.MenuExitButton.Click   += ExitFromOverlay;
                FileCleanupPage.Current.MenuReScanButton.Click += ExitFromOverlay;
                FileCleanupPage.Current.MenuReScanButton.Click += async (_, _) =>
                                                                  {
                                                                      await Task.Delay(250);
                                                                      await CleanUpGameFiles();
                                                                  };

                return;
            }

            // Delete the file straight forward if dialog is not used
            foreach (LocalFileInfo fileInfo in unusedFileInfo.Item1)
            {
                TryDeleteReadOnlyFile(fileInfo.FullPath);
            }

            return;

            static void ExitFromOverlay(object? sender, RoutedEventArgs args)
            {
                if (WindowUtility.CurrentWindow is not MainWindow mainWindow)
                    return;

                mainWindow.overlayFrame.GoBack();
                mainWindow.overlayFrame.BackStack?.Clear();
            }
        }

        protected virtual async Task<(List<LocalFileInfo>, long)> GetUnusedFileInfoList(bool includeZipCheck)
        {
            LoadingMessageHelper.ShowLoadingFrame();
            try
            {
                // Reset token
                ResetStatusAndProgress();

                // Initialize uninstall game property
                _uninstallGameProperty ??= AssignUninstallFolders();
                if (!_uninstallGameProperty.HasValue)
                {
                    throw new NotSupportedException("Clean-up feature for this game is not yet supported!");
                }

                // Get game state
                GameInstallStateEnum gameStateEnum = await GameVersionManager.GetGameState();

                // Do pkg_version check if Zip Check is used
                if (includeZipCheck)
                {
                    // Initialize new proxy-aware HttpClient
                    using HttpClient httpClient = new HttpClientBuilder()
                                                 .UseLauncherConfig(DownloadThreadCount + DownloadThreadCountReserved)
                                                 .SetAllowedDecompression(DecompressionMethods.None)
                                                 .Create();

                    // Initialize and get game state, then get the latest package info
                    LoadingMessageHelper.SetMessage(
                                                    Locale.Lang._FileCleanupPage.LoadingTitle,
                                                    Locale.Lang._FileCleanupPage.LoadingSubtitle2);

                    DownloadClient downloadClient = DownloadClient.CreateInstance(httpClient);
                    RegionResourceVersion? packageLatestBase = GameVersionManager
                                                              .GetGameLatestZip(gameStateEnum).FirstOrDefault();
                    string? packageExtractBasePath = packageLatestBase?.decompressed_path;

                    // Check Fail-safe: Download pkg_version files if not exist
                    string pkgVersionPath = Path.Combine(GamePath, "pkg_version");
                    if (!string.IsNullOrEmpty(packageExtractBasePath))
                    {
                        // Check Fail-safe: Download main pkg_version file
                        string mainPkgVersionUrl = ConverterTool.CombineURLFromString(packageExtractBasePath,
                            "pkg_version");
                        await downloadClient.DownloadAsync(mainPkgVersionUrl, pkgVersionPath, true);

                        // Check Fail-safe: Download audio pkg_version files
                        if (!string.IsNullOrEmpty(_gameAudioLangListPathStatic) &&
                            !string.IsNullOrEmpty(packageExtractBasePath))
                        {
                            if (!File.Exists(_gameAudioLangListPathStatic))
                            {
                                throw new
                                    FileNotFoundException("Game does have audio lang index file but does not exist!"
                                                          + $" Expecting location: {_gameAudioLangListPathStatic}");
                            }

                            await DownloadOtherAudioPkgVersion(_gameAudioLangListPathStatic,
                                                               packageExtractBasePath,
                                                               downloadClient);
                        }
                    }

                    // Check Fail-safe: If the main pkg_version still not exist, throw!
                    bool isMainPkgVersionExist = File.Exists(pkgVersionPath);
                    if (!isMainPkgVersionExist)
                    {
                        throw new FileNotFoundException("Cannot get the file list due to pkg_version file not exist!");
                    }
                }

                // Try parse the pkg_versions (including the audio one)
                List<LocalFileInfo> pkgFileInfo        = [];
                HashSet<string>     pkgFileInfoHashSet = [];
                await ParsePkgVersions2FileInfo(pkgFileInfo, pkgFileInfoHashSet, Token.Token);

                string[] ignoredFiles = [];
                if (File.Exists(Path.Combine(GamePath, "@IgnoredFiles")))
                {
                    try
                    {
                        ignoredFiles = await File.ReadAllLinesAsync(Path.Combine(GamePath, "@IgnoredFiles"));
                        LogWriteLine("Found ignore file settings!");
                    }
                    catch (Exception ex)
                    {
                        await SentryHelper.ExceptionHandlerAsync(ex);
                        LogWriteLine($"Failed when reading ignore file setting! Ignoring...\r\n{ex}", LogType.Error,
                                     true);
                    }
                }

                // Add pre-download zips into the ignored list 
                RegionResourceVersion? packagePreDownloadList =
                    GameVersionManager.GetGamePreloadZip()?.FirstOrDefault();
                if (packagePreDownloadList != null)
                {
                    List<string> preDownloadZips = [];
                    var pkg = new GameInstallPackage(packagePreDownloadList, GamePath)
                        { PackageType = GameInstallPackageType.General };
                    if (!string.IsNullOrEmpty(pkg.Name)) preDownloadZips.Add($"{pkg.Name}*");

                    if (packagePreDownloadList.voice_packs?.Count > 0)
                    {
                        preDownloadZips.AddRange(packagePreDownloadList.voice_packs
                                                                       .Select(audioRes =>
                                                                                   new GameInstallPackage(audioRes,
                                                                                       GamePath)
                                                                                   {
                                                                                       PackageType =
                                                                                           GameInstallPackageType.Audio
                                                                                   })
                                                                       .Where(pkgAudio =>
                                                                                  !string.IsNullOrEmpty(pkgAudio.Name))
                                                                       .Select(pkgAudio => $"{pkgAudio.Name}*"));
                    }

                    if (preDownloadZips.Count > 0)
                    {
                        ignoredFiles = ignoredFiles.Concat(preDownloadZips).ToArray();
                    }
                }

                if (ignoredFiles.Length > 0)
                    LogWriteLine($"[GetUnusedFileInfoList] Final ignored file list:\r\n{string.Join(", ", ignoredFiles)}",
                                 LogType.Scheme, true);

                // Get the list of the local file paths
                List<LocalFileInfo> localFileInfo = [];
                await GetRelativeLocalFilePaths(localFileInfo, includeZipCheck, gameStateEnum, Token.Token);

                // Get and filter the unused file from the pkg_versions and ignoredFiles
                List<LocalFileInfo> unusedFileInfo = [];
                long unusedFileSize = 0;
                await Task.Run(() =>
                                   Parallel.ForEach(localFileInfo,
                                                    new ParallelOptions { CancellationToken = Token.Token },
                                                    (asset, _) =>
                                                    {
                                                        if (pkgFileInfoHashSet.Contains(asset.RelativePath) ||
                                                            PatternMatcher
                                                               .MatchesAnyPattern(asset.ToFileInfo().Name,
                                                                    ignoredFiles.ToList()))
                                                            return;

                                                        lock (unusedFileInfo)
                                                        {
                                                            Interlocked.Add(ref unusedFileSize, asset.FileSize);
                                                            unusedFileInfo.Add(asset);
                                                        }
                                                    }));

                return (unusedFileInfo, unusedFileSize);
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex);
                return ([], 0);
            }
        }

        protected virtual async ValueTask DownloadOtherAudioPkgVersion(string audioListFilePath, string baseExtractUrl,
                                                                       DownloadClient downloadClient)
        {
            // Initialize reader
            using StreamReader reader = new StreamReader(audioListFilePath);
            // Read until EOF
            while (!reader.EndOfStream)
            {
                // Read the line and skip if it's empty
                string? line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                // Get the pkg_version filename, url and then download it
                string pkgFileName = $"Audio_{line.Trim()}_pkg_version";
                string pkgPath     = Path.Combine(GamePath, pkgFileName);
                string pkgUrl      = ConverterTool.CombineURLFromString(baseExtractUrl, pkgFileName);

                // Skip if URL is not found
                if ((await FallbackCDNUtil.GetURLStatusCode(pkgUrl, default)).StatusCode == HttpStatusCode.NotFound)
                {
                    continue;
                }

                // Download the file
                await downloadClient.DownloadAsync(pkgUrl, pkgPath, true);
            }
        }

        protected virtual async ValueTask ParsePkgVersions2FileInfo(List<LocalFileInfo> pkgFileInfo,
                                                                    HashSet<string>     pkgFileInfoHashSet,
                                                                    CancellationToken   token)
        {
            string gamePath = GamePath;

            // Iterate the pkg_version file paths
            foreach (string pkgPath in
                     Directory.EnumerateFiles(gamePath, "*pkg_version", SearchOption.TopDirectoryOnly))
            {
                // Parse and add the entries to the list
                await InnerParsePkgVersion2FileInfo(gamePath, pkgPath, pkgFileInfo, pkgFileInfoHashSet, token);
            }
        }

        protected virtual async ValueTask InnerParsePkgVersion2FileInfo(string              gamePath, string path,
                                                                        List<LocalFileInfo> pkgFileInfo,
                                                                        HashSet<string>     pkgFileInfoHashSet,
                                                                        CancellationToken   token)
        {
            // Assign path to reader
            using StreamReader reader = new StreamReader(path, true);
            // Do loop until EOF
            while (!reader.EndOfStream)
            {
                // Read line and deserialize
                string?        line          = await reader.ReadLineAsync(token);
                LocalFileInfo? localFileInfo = line?.Deserialize(LocalFileInfoJsonContext.Default.LocalFileInfo);

                // Assign the values
                if (localFileInfo == null)
                    continue;

                localFileInfo.FullPath    = Path.Combine(gamePath, localFileInfo.RelativePath);
                localFileInfo.FileName    = Path.GetFileName(localFileInfo.RelativePath);
                localFileInfo.IsFileExist = File.Exists(localFileInfo.FullPath);

                // Add it to the list and hashset
                pkgFileInfo.Add(localFileInfo);
                pkgFileInfoHashSet.Add(localFileInfo.RelativePath);
            }
        }

        protected virtual bool IsCategorizedAsGameFile(FileInfo fileInfo, string gamePath, bool includeZipCheck,
                                                       GameInstallStateEnum gameState, out LocalFileInfo localFileInfo)
        {
            // Convert to LocalFileInfo and get the relative path
            localFileInfo = new LocalFileInfo(fileInfo, gamePath);
            string             relativePath     = localFileInfo.RelativePath;
            ReadOnlySpan<char> relativePathSpan = relativePath;
            string             fileName         = localFileInfo.FileName;
            string             gameFolder       = _uninstallGameProperty?.gameDataFolderName ?? string.Empty;
            string             persistentPath   = Path.Combine(gameFolder, "Persistent");

            // 1st check: Ensure that the file is not a persistent file
            if (relativePathSpan.StartsWith(persistentPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 2nd check: Ensure that the file is not a config or pkg_version file
            if (relativePathSpan.EndsWith("config.ini",     StringComparison.OrdinalIgnoreCase)
                || relativePathSpan.EndsWith("pkg_version", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 3rd check: Ensure that the file is not a web cache file
            if (relativePathSpan.Contains("webCache",    StringComparison.OrdinalIgnoreCase)
                || relativePathSpan.Contains("SDKCache", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 4th check: Ensure that the file isn't in excluded list
            if (_uninstallGameProperty?.foldersToKeepInData
                                       .Any(x => relativePath
                                                .AsSpan() // As Span<T> since StartsWith() in it is typically faster
                                                 // than the one from String primitive
                                                .Contains(x.AsSpan(), StringComparison.OrdinalIgnoreCase)) ?? false)
            {
                return false; // Return false if it's not actually in excluded list
            }

            // 5th check: Ensure if the path includes the folder name at start
            if (!string.IsNullOrEmpty(gameFolder) && relativePathSpan
                   .StartsWith(gameFolder, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 6th check: Ensure if the path includes the DXSETUP folder
            if (Path.GetDirectoryName(relativePathSpan)
                    .EndsWith("DXSETUP", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 7th check: Ensure that the file is one of the files included
            //            in the Regex pattern list
            if (_uninstallGameProperty?.filesToDelete
                                       .Any(pattern => Regex.IsMatch(fileName,
                                                                     pattern,
                                                                     RegexOptions.Compiled |
                                                                     RegexOptions.NonBacktracking
                                                                    )) ?? false)
            {
                return true;
            }

            // 8th check: Ensure that the file is one of package files
            if (includeZipCheck && NonGameFileRegex.IsMatch(fileName))
            {
                return true;
            }

            // 9th check: Ensure that the file is Sophon Chunk file
            // if game state is installed.
            // If all those matches failed, then return them as a non-game file
            return gameState == GameInstallStateEnum.Installed
                   && localFileInfo.RelativePath.StartsWith("chunk_collapse", StringComparison.OrdinalIgnoreCase);
        }

        protected virtual async Task GetRelativeLocalFilePaths(List<LocalFileInfo> localFileInfoList,
                                                               bool includeZipCheck, GameInstallStateEnum gameState,
                                                               CancellationToken token)
        {
            await Task.Run(() =>
                           {
                               int           count          = 0;
                               long          totalSize      = 0;
                               string        gamePath       = GamePath;
                               DirectoryInfo dirInfo        = new DirectoryInfo(gamePath);
                               int           updateInterval = 100; // Update UI every 100 files
                               int           processedCount = 0;

                               // Do the do in parallel since it will be a really CPU expensive task due to janky checks here and there.
                               Parallel.ForEach(dirInfo.EnumerateFiles("*", SearchOption.AllDirectories),
                                                new ParallelOptions { CancellationToken = token },
                                                (fileInfo, _) =>
                                                {
                                                    // Throw if token is cancelled
                                                    token.ThrowIfCancellationRequested();

                                                    // Do the check within the lambda function to possibly check the file
                                                    // condition in multithread
                                                    if (!IsCategorizedAsGameFile(fileInfo, gamePath, includeZipCheck, gameState, out LocalFileInfo localFileInfo))
                                                        return;

                                                    Interlocked.Add(ref totalSize, fileInfo.Exists ? fileInfo.Length : 0);
                                                    Interlocked.Increment(ref count);
                                                    int currentCount = Interlocked.Increment(ref processedCount);

                                                    if (currentCount % updateInterval == 0)
                                                    {
                                                        ParentUI.DispatcherQueue.TryEnqueue(() =>
                                                        {
                                                            LoadingMessageHelper.SetMessage(
                                                                 Locale.Lang._FileCleanupPage.LoadingTitle,
                                                                 string.Format(Locale.Lang._FileCleanupPage.LoadingSubtitle1,
                                                                               count,
                                                                               ConverterTool.SummarizeSizeSimple(totalSize))
                                                                );
                                                        });
                                                    }

                                                    lock (localFileInfoList)
                                                    {
                                                        localFileInfoList.Add(localFileInfo);
                                                    }
                                                });
                           }, token);
        }
    }
}