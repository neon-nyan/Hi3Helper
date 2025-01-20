using CollapseLauncher.GameVersioning;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Parser;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.EncTool.Parser.Cache;
using Hi3Helper.EncTool.Parser.Senadina;
using Hi3Helper.Http;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.Win32;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable InconsistentNaming
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable GrammarMistakeInComment
// ReSharper disable StringLiteralTypo
#pragma warning disable CA1068

namespace CollapseLauncher
{
    internal struct HonkaiRepairAssetIgnore
    {
        internal static HonkaiRepairAssetIgnore CreateEmpty() => new()
        {
            IgnoredAudioPCKType       = [],
            IgnoredVideoCGSubCategory = []
        };

        internal AudioPCKType[] IgnoredAudioPCKType;
        internal int[] IgnoredVideoCGSubCategory;
    }

    #nullable enable
    internal partial class HonkaiRepair
    {
        private          string?      _mainMetaRepoUrl;
        private readonly byte[]       _collapseHeader        = "Collapse"u8.ToArray();
        private readonly List<string> _ignoredUnusedFileList = [];

        private async Task Fetch(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            // Set total activity string as "Loading Indexes..."
            Status.ActivityStatus = Lang!._GameRepairPage!.Status2!;
            Status.IsProgressAllIndetermined = true;
            UpdateStatus();

            // Initialize the Senadina File Identifier
            Dictionary<string, SenadinaFileIdentifier>? senadinaFileIdentifier = null;

            // Clear the _ignoredUnusedFileList
            _ignoredUnusedFileList.Clear();

            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder()
                .UseLauncherConfig(DownloadThreadCount + DownloadThreadCountReserved)
                .SetUserAgent(UserAgent)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Get the instance of a new DownloadClient
            DownloadClient downloadClient = DownloadClient.CreateInstance(client);

            try
            {
                // Subscribe the fetching progress and subscribe cacheUtil progress to adapter
                CacheUtil!.ProgressChanged += _innerObject_ProgressAdapter;
                CacheUtil!.StatusChanged += _innerObject_StatusAdapter;

                // Update the progress bar state
                Status.IsProgressPerFileIndetermined = true;
                UpdateStatus();

                // Region: XMFAndAssetIndex
                // Fetch metadata
                Dictionary<string, string> manifestDict = await FetchMetadata(token);

                // Check for manifest. If it doesn't exist, then throw and warn the user
                if (!manifestDict.TryGetValue(GameVersion.VersionString!, out string? gameRepoUrl))
                {
                    throw new VersionNotFoundException($"Manifest for {GameVersionManager!.GamePreset!.ZoneName} (version: {GameVersion.VersionString}) doesn't exist! Please contact @neon-nyan or open an issue for this!");
                }

                GameRepoURL = gameRepoUrl;

                // Initialize local audio manifest, blocks and patchConfig stream.
                SenadinaFileIdentifier? audioManifestSenadinaFileIdentifier = null;
                SenadinaFileIdentifier? blocksBaseManifestSenadinaFileIdentifier = null;
                SenadinaFileIdentifier? blocksPlatformManifestSenadinaFileIdentifier = null;
                SenadinaFileIdentifier? blocksCurrentManifestSenadinaFileIdentifier = null;
                SenadinaFileIdentifier? patchConfigManifestSenadinaFileIdentifier = null;
                _mainMetaRepoUrl = null;

                // Get the status if the current game is Senadina version.
                GameTypeHonkaiVersion gameVersionKind = GameVersionManager!.CastAs<GameTypeHonkaiVersion>()!;
                int[] versionArray = gameVersionKind.GetGameVersionApi()?.VersionArray!;
                bool IsSenadinaVersion = gameVersionKind.IsCurrentSenadinaVersion;

                // TODO: Use FallbackCDNUtil to fetch the stream.
                if (IsSenadinaVersion && !IsOnlyRecoverMain)
                {
                    _mainMetaRepoUrl = $"https://r2.bagelnl.my.id/cl-meta/pustaka/{GameVersionManager!.GamePreset!.ProfileName}/{string.Join('.', versionArray)}";

                    // Get the Senadina File Identifier Dictionary and its file references
                    senadinaFileIdentifier = await GetSenadinaIdentifierDictionary(client, _mainMetaRepoUrl, token);
                    audioManifestSenadinaFileIdentifier = await GetSenadinaIdentifierKind(client, senadinaFileIdentifier,
                                                                                          SenadinaKind.chiptunesCurrent, versionArray, _mainMetaRepoUrl, false, token);
                    blocksPlatformManifestSenadinaFileIdentifier = await GetSenadinaIdentifierKind(client, senadinaFileIdentifier,
                                                                                               SenadinaKind.platformBase, versionArray, _mainMetaRepoUrl, false, token);
                    blocksBaseManifestSenadinaFileIdentifier = await GetSenadinaIdentifierKind(client, senadinaFileIdentifier,
                                                                                               SenadinaKind.bricksBase, versionArray, _mainMetaRepoUrl, false, token);
                    blocksCurrentManifestSenadinaFileIdentifier = await GetSenadinaIdentifierKind(client, senadinaFileIdentifier,
                                                                                                  SenadinaKind.bricksCurrent, versionArray, _mainMetaRepoUrl, false, token);
                    patchConfigManifestSenadinaFileIdentifier = await GetSenadinaIdentifierKind(client, senadinaFileIdentifier,
                                                                                                SenadinaKind.wandCurrent, versionArray, _mainMetaRepoUrl, true, token);
                }

                if (!IsOnlyRecoverMain)
                {
                    // Get the list of ignored assets
                    HonkaiRepairAssetIgnore IgnoredAssetIDs = GetIgnoredAssetsProperty();

                    // Region: VideoIndex via External -> _cacheUtil: Data Fetch
                    // Fetch video index and also fetch the gateway URL
                    (string, string) gatewayURL = await FetchVideoAndGateway(downloadClient, assetIndex, IgnoredAssetIDs, token);
                    AssetBaseURL = "http://" + gatewayURL.Item1 + '/';
                    GameServer = CacheUtil?.GetCurrentGateway()!;

                    // Region: AudioIndex
                    // Try check audio manifest.m file and fetch it if it doesn't exist
                    await FetchAudioIndex(client, assetIndex, IgnoredAssetIDs, audioManifestSenadinaFileIdentifier!, token);
                }

                // Assign the URL based on the version
                GameRepoURL = manifestDict[GameVersion.VersionString!];

                // Region: XMFAndAssetIndex
                // Fetch asset index
                await FetchAssetIndex(assetIndex, token);

                if (!IsOnlyRecoverMain)
                {
                    // Region: XMFAndAssetIndex
                    // Try check XMF file and fetch it if it doesn't exist
                    await FetchXMFFile(client, assetIndex, blocksPlatformManifestSenadinaFileIdentifier,
                        blocksBaseManifestSenadinaFileIdentifier!, blocksCurrentManifestSenadinaFileIdentifier!,
                        patchConfigManifestSenadinaFileIdentifier!, manifestDict[GameVersion.VersionString!], token);

                    // Remove plugin from assetIndex
                    // Skip the removal for Delta-Patch
                    EliminatePluginAssetIndex(assetIndex);
                }
            }
            finally
            {
                // Unsubscribe the fetching progress and dispose it and unsubscribe cacheUtil progress to adapter
                CacheUtil!.ProgressChanged -= _innerObject_ProgressAdapter;
                CacheUtil.StatusChanged -= _innerObject_StatusAdapter;
                senadinaFileIdentifier?.Clear();
            }
        }
        
        private async Task<Dictionary<string, SenadinaFileIdentifier>?> GetSenadinaIdentifierDictionary(HttpClient client, string mainUrl, CancellationToken token)
        {
            string             identifierUrl               = CombineURLFromString(mainUrl, "daftar-pustaka")!;
            await using Stream fileIdentifierStream        = (await HttpResponseInputStream.CreateStreamAsync(client, identifierUrl, null, null, null, null, null, token))!;
            await using Stream fileIdentifierStreamDecoder = new BrotliStream(fileIdentifierStream, CompressionMode.Decompress, true);

            await ThrowIfFileIsNotSenadina(fileIdentifierStream, token);
#if DEBUG
            using StreamReader rd = new StreamReader(fileIdentifierStreamDecoder);
            string response = await rd.ReadToEndAsync(token);
            LogWriteLine($"[HonkaiRepair::GetSenadinaIdentifierDictionary() Dictionary Response:\r\n{response}", LogType.Debug, true);
            return response.Deserialize(SenadinaJsonContext.Default.DictionaryStringSenadinaFileIdentifier);
#else
            return await fileIdentifierStreamDecoder.DeserializeAsync(SenadinaJsonContext.Default.DictionaryStringSenadinaFileIdentifier, token: token);
#endif
        }

        private async Task<SenadinaFileIdentifier?> GetSenadinaIdentifierKind(HttpClient client, Dictionary<string, SenadinaFileIdentifier>? dict, SenadinaKind kind, int[] gameVersion, string mainUrl, bool skipThrow, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(dict);
            
            string origFileRelativePath = $"{gameVersion[0]}_{gameVersion[1]}_{kind.ToString().ToLower()}";
            string hashedRelativePath = SenadinaFileIdentifier.GetHashedString(origFileRelativePath);

            string fileUrl = CombineURLFromString(mainUrl, hashedRelativePath)!;
            if (!dict.TryGetValue(origFileRelativePath, out var identifier))
            {
                LogWriteLine($"Key reference to the pustaka file: {hashedRelativePath} is not found for game version: {string.Join('.', gameVersion)}. Please contact us on our Discord Server to report this issue.", LogType.Error, true);
                if (skipThrow) return null;
                throw new
                    FileNotFoundException("Assets reference for repair is not found. " +
                                          "Please contact us in GitHub issues or Discord to let us know about this issue.");
            }

            Stream networkStream = (await HttpResponseInputStream.CreateStreamAsync(client, fileUrl, null, null, null, null, null, token))!;

            await ThrowIfFileIsNotSenadina(networkStream, token);
            identifier.fileStream = SenadinaFileIdentifier.CreateKangBakso(networkStream, identifier.lastIdentifier!, origFileRelativePath, (int)identifier.fileTime);
            identifier.relativePath = origFileRelativePath;

            return identifier;
        }
        #nullable disable
        
        private async Task ThrowIfFileIsNotSenadina(Stream stream, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(stream);
            
            Memory<byte> header = new byte[_collapseHeader.Length];
            _ = await stream.ReadAsync(header, token)!;
            if (!header.Span.SequenceEqual(_collapseHeader))
                throw new InvalidDataException($"Daftar pustaka file is corrupted! Expecting header: 0x{BinaryPrimitives.ReadInt64LittleEndian(_collapseHeader):x8} but got: 0x{BinaryPrimitives.ReadInt64LittleEndian(header.Span):x8} instead!");
        }

        private void EliminatePluginAssetIndex(List<FilePropertiesRemote> assetIndex)
        {
            GameVersionManager.GameApiProp.data!.plugins?.ForEach(plugin =>
              {
                  if (plugin.package?.validate == null) return;
                  assetIndex.RemoveAll(asset =>
                  {
                      var r = plugin.package?.validate.Any(validate => validate.path != null &&
                                                                      (asset.N.Contains(validate.path)||asset.RN.Contains(validate.path)));
                      if (r ?? false)
                      {
                          LogWriteLine($"[EliminatePluginAssetIndex] Removed: {asset.N}", LogType.Warning, true);
                      }
                      return r ?? false;
                  });
              });
        }

        #region Registry Utils
#nullable enable
        private HonkaiRepairAssetIgnore GetIgnoredAssetsProperty()
        {
            // Try get the parent registry key
            RegistryKey? keys = Registry.CurrentUser.OpenSubKey(GameVersionManager!.GamePreset!.ConfigRegistryLocation);
            if (keys == null) return HonkaiRepairAssetIgnore.CreateEmpty(); // Return an empty property if the parent key doesn't exist

            // Initialize the property
            AudioPCKType[] IgnoredAudioPCKTypes      = [];
            int[]          IgnoredVideoCGSubCategory = [];

            // Try get the values of the registry key of the Audio ignored list
            object? objIgnoredAudioPCKTypes = keys.GetValue("GENERAL_DATA_V2_DeletedAudioTypes_h214176984");
            if (objIgnoredAudioPCKTypes != null)
            {
                ReadOnlySpan<byte> bytesIgnoredAudioPckTypes = (byte[])objIgnoredAudioPCKTypes;
                IgnoredAudioPCKTypes = bytesIgnoredAudioPckTypes.Deserialize(GenericJsonContext.Default.AudioPCKTypeArray) ?? IgnoredAudioPCKTypes;
            }

            // Try get the values of the registry key of the Video CG ignored list
            object? objIgnoredVideoCGSubCategory = keys.GetValue("GENERAL_DATA_V2_DeletedCGPackages_h2282700200");
            if (objIgnoredVideoCGSubCategory == null)
            {
                return new HonkaiRepairAssetIgnore
                {
                    IgnoredAudioPCKType = IgnoredAudioPCKTypes, IgnoredVideoCGSubCategory = IgnoredVideoCGSubCategory
                };
            }

            ReadOnlySpan<byte> bytesIgnoredVideoCGSubCategory = (byte[])objIgnoredVideoCGSubCategory;
            IgnoredVideoCGSubCategory = bytesIgnoredVideoCGSubCategory.Deserialize(GenericJsonContext.Default.Int32Array) ?? IgnoredVideoCGSubCategory;

            // Return the property value
            return new HonkaiRepairAssetIgnore { IgnoredAudioPCKType = IgnoredAudioPCKTypes, IgnoredVideoCGSubCategory = IgnoredVideoCGSubCategory };
        }
#nullable disable
        #endregion

        #region VideoIndex via External -> _cacheUtil: Data Fetch
        private async Task<(string, string)> FetchVideoAndGateway(DownloadClient downloadClient, List<FilePropertiesRemote> assetIndex, HonkaiRepairAssetIgnore ignoredAssetIDs, CancellationToken token)
        {
            // Fetch data cache file only and get the gateway
            (List<CacheAsset>, string, string, int) cacheProperty = await CacheUtil!.GetCacheAssetList(downloadClient, CacheAssetType.Data, token);

            if (IsOnlyRecoverMain)
            {
                return (cacheProperty.Item2, cacheProperty.Item3);
            }

            // Find the cache asset. If null, then return
            CacheAsset cacheAsset = cacheProperty.Item1.FirstOrDefault(x => x!.N!.EndsWith($"{HashID.CGMetadata}"));

            // Deserialize and build video index into asset index
            await BuildVideoIndex(downloadClient, cacheAsset, cacheProperty.Item2, assetIndex, ignoredAssetIDs, cacheProperty.Item4, token);

            // Return the gateway URL including asset bundle and asset cache
            return (cacheProperty.Item2, cacheProperty.Item3);
        }

        private async Task BuildVideoIndex(DownloadClient downloadClient, CacheAsset cacheAsset, string assetBundleURL, List<FilePropertiesRemote> assetIndex, HonkaiRepairAssetIgnore ignoredAssetIDs, int luckyNumber, CancellationToken token)
        {
            // Get the remote stream and use CacheStream
            await using MemoryStream memoryStream = new MemoryStream();
            if (downloadClient != null)
            {
                ArgumentNullException.ThrowIfNull(cacheAsset);
                // Download the cache and store it to MemoryStream
                await downloadClient.DownloadAsync(cacheAsset.ConcatURL, memoryStream, false, cancelToken: token);
                memoryStream.Position = 0;

                // Use CacheStream to decrypt and read it as Stream
                await using CacheStream cacheStream = new CacheStream(memoryStream, true, luckyNumber);
                // Enumerate and iterate the metadata to asset index
                await BuildAndEnumerateVideoVersioningFile(token,      CGMetadata.Enumerate(cacheStream, Encoding.UTF8),
                                                           assetIndex, ignoredAssetIDs, assetBundleURL);
            }
            else
            {
                throw new ObjectDisposedException("RepairManagement::Honkai::Fetch:BuildVideoIndex() error!" +
                                                  "\r\n downloadClient is unexpectedly disposed.");
            }
        }

        private async Task BuildAndEnumerateVideoVersioningFile(CancellationToken token, IEnumerable<CGMetadata> enumEntry, List<FilePropertiesRemote> assetIndex, HonkaiRepairAssetIgnore ignoredAssetIDs, string assetBundleURL)
        {
            ArgumentNullException.ThrowIfNull(assetIndex);
            
            // Get the base URL
            string baseURL = CombineURLFromString((GetAppConfigValue("EnableHTTPRepairOverride").ToBool() ? "http://" : "https://") + assetBundleURL, "/Video/");

            // Get FileInfo of the version.txt file
            FileInfo fileInfo = new FileInfo(Path.Combine(GamePath!, NormalizePath(VideoBaseLocalPath)!, "Version.txt"))
                .EnsureCreationOfDirectory()
                .EnsureNoReadOnly();

            // Build video versioning file
            await using StreamWriter sw = fileInfo.CreateText();

            // Initialize concurrent queue for video metadata
            ConcurrentDictionary<string, CGMetadata> videoMetadataQueue = new();

            // Iterate the metadata to be converted into asset index
            await Parallel.ForEachAsync(enumEntry!, new ParallelOptions
            {
                CancellationToken      = token,
                MaxDegreeOfParallelism = ThreadCount
            }, async (metadata, tokenInternal) =>
               {
                   // Only add remote available videos (not build-in) and check if the CG file is available in the server
                   // Edit: 2023-12-09
                   // Starting from 7.1, the CGs that have included in ignoredAssetIDs (which is marked as deleted) will be ignored.
                   bool isCGAvailable = await IsCGFileAvailable(metadata, baseURL, tokenInternal);
                   bool isCGIgnored   = ignoredAssetIDs.IgnoredVideoCGSubCategory.Contains(metadata!.CgSubCategory);

               #if DEBUG
                   if (isCGIgnored)
                       LogWriteLine($"Ignoring CG Category: {metadata.CgSubCategory} {(AudioLanguage == AudioLanguageType.Japanese ? metadata.CgPathHighBitrateJP : metadata.CgPathHighBitrateCN)}", LogType.Debug, true);
               #endif

                   if (!metadata.InStreamingAssets && isCGAvailable && !isCGIgnored)
                   {
                       string name = (AudioLanguage == AudioLanguageType.Japanese ? metadata.CgPathHighBitrateJP : metadata.CgPathHighBitrateCN) + ".usm";
                       _ = videoMetadataQueue.TryAdd(name, metadata);
                   }
               });

            foreach (KeyValuePair<string, CGMetadata> metadata in videoMetadataQueue)
            {
                assetIndex.Add(new FilePropertiesRemote
                {
                    N = CombineURLFromString(VideoBaseLocalPath, metadata.Key),
                    RN = CombineURLFromString(baseURL, metadata.Key),
                    S = AudioLanguage == AudioLanguageType.Japanese ? metadata.Value.FileSizeHighBitrateJP : metadata.Value.FileSizeHighBitrateCN,
                    FT = FileType.Video
                });

                if (!metadata.Value.InStreamingAssets)
                {
                    // Append the versioning list
                    await sw.WriteLineAsync("Video/" + (AudioLanguage == AudioLanguageType.Japanese ? metadata.Value.CgPathHighBitrateJP : metadata.Value.CgPathHighBitrateCN) + ".usm\t1");
                }
            }
        }

        private async ValueTask<bool> IsCGFileAvailable(CGMetadata cgInfo, string baseURL, CancellationToken token)
        {
            // If the file has no appoinment schedule (like non-birthday CG), then return true
            if (cgInfo!.AppointmentDownloadScheduleID == 0) return true;

            // Update the status
            Status.ActivityStatus = string.Format(Lang._GameRepairPage.Status14, cgInfo.CgExtraKey);
            Status.IsProgressAllIndetermined = true;
            Status.IsProgressPerFileIndetermined = true;
            UpdateStatus();

            // Set the URL and try get the status
            string cgURL = CombineURLFromString(baseURL, (AudioLanguage == AudioLanguageType.Japanese ? cgInfo.CgPathHighBitrateJP : cgInfo.CgPathHighBitrateCN) + ".usm");
            UrlStatus urlStatus = await FallbackCDNUtil.GetURLStatusCode(cgURL, token);

            LogWriteLine($"The CG asset: {(AudioLanguage == AudioLanguageType.Japanese ? cgInfo.CgPathHighBitrateJP : cgInfo.CgPathHighBitrateCN)} " + 
                         (urlStatus!.IsSuccessStatusCode ? "is" : "is not") + $" available (Status code: {urlStatus.StatusCode})", LogType.Default, true);

            return urlStatus.IsSuccessStatusCode;
        }

        #endregion

        #region AudioIndex
        private async Task FetchAudioIndex(HttpClient httpClient, List<FilePropertiesRemote> assetIndex, HonkaiRepairAssetIgnore ignoredAssetIDs, SenadinaFileIdentifier senadinaFileIdentifier, CancellationToken token)
        {
            // If the gameServer is null, then just leave
            if (GameServer == null)
            {
                LogWriteLine("We found that the Dispatch/GameServer has return a null. Please report this issue to Collapse's Contributor or submit an issue!", LogType.Warning, true);
                return;
            }

            // Set manifest.m local path and remote URL
            string manifestLocalPath = Path.Combine(GamePath!, NormalizePath(AudioBaseLocalPath)!, "manifest.m");
            string manifestRemotePath = string.Format(CombineURLFromString(AudioBaseRemotePath, GameServer.Manifest!.ManifestAudio!.ManifestAudioPlatform!.ManifestWindows!)!, 
                                                      $"{GameVersion.Major}_{GameVersion.Minor}", GameServer.Manifest.ManifestAudio.ManifestAudioRevision);

            try
            {
                // Try to get the audio manifest and deserialize it
                KianaAudioManifest manifest = await TryGetAudioManifest(httpClient, senadinaFileIdentifier, manifestLocalPath, manifestRemotePath, token);

                // Deserialize manifest and build Audio Index
                await BuildAudioIndex(manifest, assetIndex, ignoredAssetIDs, token);

                // Build audio version file
                BuildAudioVersioningFile(manifest, ignoredAssetIDs);
            }
            // If a throw was thrown, then try to redownload the manifest.m file and try deserialize it again
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Exception was thrown while reading audio manifest file!\r\n{ex}", LogType.Warning, true);
            }
        }

        private void BuildAudioVersioningFile(KianaAudioManifest audioManifest, HonkaiRepairAssetIgnore ignoredAssetIDs)
        {
            // Build audio versioning file
            using StreamWriter sw = new StreamWriter(Path.Combine(GamePath!, NormalizePath(AudioBaseLocalPath)!, "Version.txt"), false);
            // Edit: 2023-12-09
            // Starting from 7.1, the Audio Packages that have included in ignoredAssetIDs (which is marked as deleted) will be ignored.
            foreach (ManifestAssetInfo audioAsset in audioManifest!
                                                    .AudioAssets!
                                                    .Where(audioInfo => (audioInfo!.Language == AudioLanguageType.Common
                                                                         || audioInfo!.Language == AudioLanguage)
                                                                        && !ignoredAssetIDs.IgnoredAudioPCKType.Contains(audioInfo.PckType)))
            {
                // Only add common and language specific audio file
                sw.WriteLine($"{audioAsset!.Name}.pck\t{audioAsset.HashString}");
            }
        }

        private async Task BuildAudioIndex(KianaAudioManifest audioManifest, List<FilePropertiesRemote> audioIndex, HonkaiRepairAssetIgnore ignoredAssetIDs, CancellationToken token)
        {
            // Iterate the audioAsset to be added in audioIndex in parallel
            // Edit: 2023-12-09
            // Starting from 7.1, the Audio Packages that have included in ignoredAssetIDs (which is marked as deleted) will be ignored.
            await Parallel.ForEachAsync(audioManifest!
                .AudioAssets!
                .Where(audioInfo => (audioInfo!.Language == AudioLanguageType.Common
                                  || audioInfo!.Language == AudioLanguage)
                                  && !ignoredAssetIDs.IgnoredAudioPCKType.Contains(audioInfo.PckType)),
                new ParallelOptions
                {
                    CancellationToken = token,
                    MaxDegreeOfParallelism = ThreadCount
                }, async (audioInfo, _tokenInternal) =>
                {
                    // Try get the availability of the audio asset
                    if (await IsAudioFileAvailable(audioInfo, _tokenInternal))
                    {
                        // Skip AUDIO_Default since it's already been provided by base index
                        if (audioInfo!.Name != "AUDIO_Default")
                        {
                            lock (audioIndex!)
                            {
                                // Try add the audio file which has patch from unused file check
                                if (audioInfo.IsHasPatch)
                                {
                                    // Get the local file name and add it to ignored unused file list
                                    string localPath = Path.Combine(GamePath!, NormalizePath(AudioBaseLocalPath)!, audioInfo.Name + ".pck");
                                    _ignoredUnusedFileList.Add(localPath);
                                }

                                // Assign based on each values
                                FilePropertiesRemote audioAsset = new FilePropertiesRemote
                                {
                                    RN = audioInfo.Path,
                                    N = CombineURLFromString(AudioBaseLocalPath, audioInfo.Name + ".pck"),
                                    S = audioInfo.Size,
                                    FT = FileType.Audio,
                                    CRC = audioInfo.HashString,
                                    AudioPatchInfo = audioInfo.IsHasPatch ? audioInfo.PatchInfo : null,
                                };

                                // Add audioAsset to audioIndex
                                audioIndex.Add(audioAsset);
                            }
                        }
                    }
                });
        }

        private async ValueTask<bool> IsAudioFileAvailable(ManifestAssetInfo audioInfo, CancellationToken token)
        {
            // If the file is static (NeedMap == true), then pass
            if (audioInfo!.NeedMap) return true;

            // Update the status
            // TODO: Localize
            Status.ActivityStatus = string.Format(Lang._GameRepairPage.Status15, audioInfo.Path);
            Status.IsProgressAllIndetermined = true;
            Status.IsProgressPerFileIndetermined = true;
            UpdateStatus();

            // Set the URL and try get the status
            string audioURL = CombineURLFromString(string.Format(AudioBaseRemotePath!, $"{GameVersion.Major}_{GameVersion.Minor}", GameServer!.Manifest!.ManifestAudio!.ManifestAudioRevision), audioInfo.Path);
            UrlStatus urlStatus = await FallbackCDNUtil.GetURLStatusCode(audioURL, token);

            LogWriteLine($"The audio asset: {audioInfo.Path} " + (urlStatus!.IsSuccessStatusCode ? "is" : "is not") + $" available (Status code: {urlStatus.StatusCode})", LogType.Default, true);

            return urlStatus.IsSuccessStatusCode;
        }

        // ReSharper disable once UnusedParameter.Local
        private async Task<KianaAudioManifest> TryGetAudioManifest(HttpClient client, SenadinaFileIdentifier senadinaFileIdentifier, string manifestLocal, string manifestRemote, CancellationToken token)
        {
            await using Stream     originalFile = await senadinaFileIdentifier!.GetOriginalFileStream(client!, token);
            await using FileStream localFile    = new FileStream(EnsureCreationOfDirectory(manifestLocal!), FileMode.Create, FileAccess.Write, FileShare.ReadWrite);

            // Start downloading manifest.m
            if (originalFile != null)
            {
                await DoCopyStreamProgress(originalFile, localFile, token: token);
            }

            Stream networkStream = senadinaFileIdentifier.fileStream;

            // Deserialize the manifest
            return new KianaAudioManifest(networkStream, GameVersion.VersionArrayManifest, true);
        }
        #endregion

        #region XMFAndAssetIndex
        // ReSharper disable once UnusedParameter.Local
        private async Task<Dictionary<string, string>> FetchMetadata(CancellationToken token)
        {
            // Set metadata URL
            string urlMetadata = string.Format(AppGameRepoIndexURLPrefix, GameVersionManager!.GamePreset!.ProfileName);

            // Start downloading metadata using FallbackCDNUtil
            await using BridgedNetworkStream stream = await FallbackCDNUtil.TryGetCDNFallbackStream(urlMetadata, token);
            return await stream!.DeserializeAsync(CoreLibraryJsonContext.Default.DictionaryStringString, token: token);
        }

        private async Task FetchAssetIndex(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            // Set asset index URL
            string urlIndex = string.Format(AppGameRepairIndexURLPrefix, GameVersionManager!.GamePreset!.ProfileName, GameVersion.VersionString) + ".binv2";

            // Start downloading asset index using FallbackCDNUtil
            await using BridgedNetworkStream stream = await FallbackCDNUtil.TryGetCDNFallbackStream(urlIndex, token);

            // Deserialize asset index and return
            DeserializeAssetIndexV2(stream, assetIndex);
        }

        private void DeserializeAssetIndexV2(Stream stream, List<FilePropertiesRemote> assetIndexList)
        {
            AssetIndexV2 assetIndex = new AssetIndexV2();
            List<PkgVersionProperties> pkgVersionEntries = assetIndex.Deserialize(stream, out DateTime timestamp);
            LogWriteLine($"[HonkaiRepair::DeserializeAssetIndexV2()] Asset index V2 has been deserialized with: {pkgVersionEntries!.Count} assets found." +
                         $"Asset index was generated at: {timestamp} (UTC)", LogType.Default, true);

            bool isOnlyRecoverMain = IsOnlyRecoverMain;

            for (var index = pkgVersionEntries.Count - 1; index >= 0; index--)
            {
                var pkgVersionEntry = pkgVersionEntries[index];
                // Skip the .wmv file if main recovery check only is not performed
                if (!isOnlyRecoverMain && (Path.GetExtension(pkgVersionEntry!.remoteName)?
                       .Equals(".wmv", StringComparison.OrdinalIgnoreCase) ?? false))
                    continue;

                FilePropertiesRemote assetInfo = new FilePropertiesRemote
                {
                    FT  = FileType.Generic,
                    N   = pkgVersionEntry!.remoteName,
                    S   = pkgVersionEntry!.fileSize,
                    CRC = pkgVersionEntry.md5,
                    RN  = CombineURLFromString(GameRepoURL, pkgVersionEntry.remoteName)
                };

            #if DEBUG
                LogWriteLine($"[HonkaiRepair::DeserializeAssetIndexV2()] {assetInfo.PrintSummary()} found in manifest",
                             LogType.Default, true);
            #endif
                assetIndexList!.Add(assetInfo);
            }
        }

#nullable enable
        // ReSharper disable once UnusedParameter.Local
        private async Task FetchXMFFile(HttpClient _httpClient, List<FilePropertiesRemote> assetIndex,
                                        SenadinaFileIdentifier? xmfPlatformIdentifier,
                                        SenadinaFileIdentifier? xmfBaseIdentifier, SenadinaFileIdentifier? xmfCurrentIdentifier,
                                        SenadinaFileIdentifier? patchConfigIdentifier, string _repoURL, CancellationToken token)
        {
            // Set Primary XMF Path
            string xmfPriPath = Path.Combine(GamePath!, @"BH3_Data\StreamingAssets\Asb\pc\Blocks.xmf");
            // Set Secondary XMF Path
            string xmfSecPath = Path.Combine(GamePath!, $@"BH3_Data\StreamingAssets\Asb\pc\Blocks_{GameVersion.Major}_{GameVersion.Minor}.xmf");

            // Set Manifest Platform XMF Path
            string xmfPlatformPath = Path.Combine(GamePath!, @"BH3_Data\StreamingAssets\Asb\pc\BlockMeta.xmf");

            // Initialize patch config info variable
            BlockPatchManifest? patchConfigInfo = null;

            bool isPlatformXMFStreamExist = xmfPlatformIdentifier != null;
            bool isSecondaryXMFStreamExist = xmfCurrentIdentifier != null;
            bool isPatchConfigXMFStreamExist = patchConfigIdentifier != null;

            // Initialize temporary XMF stream
            using MemoryStream tempXMFStream = new();
            using MemoryStream tempXMFMetaStream = new();

            await using Stream? metaBaseXMFStream = !IsOnlyRecoverMain && isPlatformXMFStreamExist ?
                await xmfPlatformIdentifier!.GetOriginalFileStream(_httpClient, token) :
                null;
            if (xmfPlatformIdentifier != null)
            {
                await using Stream? metaDataXMFStream = !IsOnlyRecoverMain ? xmfPlatformIdentifier.fileStream : null;

                bool isEitherXMFExist = !(xmfBaseIdentifier == null && xmfCurrentIdentifier == null);

                if (isEitherXMFExist)
                {
                    await using Stream? baseXMFStream = !IsOnlyRecoverMain && isSecondaryXMFStreamExist ?
                        await xmfCurrentIdentifier!.GetOriginalFileStream(_httpClient, token) :
                        await xmfBaseIdentifier!.GetOriginalFileStream(_httpClient, token);
                    if (xmfCurrentIdentifier != null)
                    {
                        await using Stream? dataXMFStream = !IsOnlyRecoverMain ? xmfCurrentIdentifier.fileStream : xmfBaseIdentifier?.fileStream;

                        // Fetch only RecoverMain is disabled
                        await using (FileStream fs1 = new FileStream(EnsureCreationOfDirectory(!IsOnlyRecoverMain ? xmfSecPath : xmfPriPath), FileMode.Create, FileAccess.ReadWrite))
                        {
                            // Download the secondary XMF into MemoryStream
                            if (baseXMFStream != null)
                            {
                                await DoCopyStreamProgress(baseXMFStream, fs1, token: token);
                            }

                            // Copy the secondary XMF into primary XMF if _isOnlyRecoverMain == false
                            if (!IsOnlyRecoverMain)
                            {
                                await using FileStream fs2 = new FileStream(EnsureCreationOfDirectory(xmfPriPath), FileMode.Create, FileAccess.Write);
                                fs1.Position = 0;
                                await fs1.CopyToAsync(fs2, token);
                            }
                        }

                        // Get the estimated size of the local xmf size
                        FileInfo xmfFileInfoLocal = new FileInfo(IsOnlyRecoverMain ? xmfPriPath : xmfSecPath);
                        long?    estimatedXmfSize = !xmfFileInfoLocal.Exists ? null : xmfFileInfoLocal.Length;

                        // Copy the source stream into temporal stream
                        if (dataXMFStream != null)
                        {
                            await DoCopyStreamProgress(dataXMFStream, tempXMFStream, estimatedXmfSize, token);
                        }
                    }

                    tempXMFStream.Position = 0;
                }

                // Download the platform XMF file if exist
                if (!IsOnlyRecoverMain)
                {
                    // Create the filestream
                    await using FileStream fsMeta = new FileStream(EnsureCreationOfDirectory(xmfPlatformPath), FileMode.Create, FileAccess.Write);

                    // Download the platform XMF (RAW) into FileStream
                    if (metaBaseXMFStream != null)
                    {
                        await DoCopyStreamProgress(metaBaseXMFStream, fsMeta, token: token);
                    }

                    // Download the platform XMF (Data) into FileStream
                    await (metaDataXMFStream?.CopyToAsync(tempXMFMetaStream, token) ?? Task.CompletedTask);
                    tempXMFMetaStream.Position = 0;
                }

                // Fetch for PatchConfig.xmf file if available (Block patch metadata)
                if (!IsOnlyRecoverMain && isPatchConfigXMFStreamExist)
                {
                    patchConfigInfo = await FetchPatchConfigXMFFile(isEitherXMFExist ? tempXMFStream : tempXMFMetaStream, patchConfigIdentifier, _httpClient, token);
                }

                // After all completed, then Deserialize the XMF to build the asset index
                BuildBlockIndex(assetIndex, patchConfigInfo, IsOnlyRecoverMain ? xmfPriPath : xmfSecPath, isEitherXMFExist ? tempXMFStream : tempXMFMetaStream, !isEitherXMFExist);
            }

        #nullable restore
        }

        // ReSharper disable once UnusedParameter.Local
        private static async Task<BlockPatchManifest> FetchPatchConfigXMFFile(Stream xmfStream, SenadinaFileIdentifier patchConfigFileIdentifier, 
                                                                              HttpClient httpClient, CancellationToken token)
        {
            // Start downloading XMF and load it to MemoryStream first
            using MemoryStream mfs = new MemoryStream();
            // Copy the remote stream of Patch Config to temporal mfs
            await patchConfigFileIdentifier!.fileStream!.CopyToAsync(mfs, token);
            // Reset the MemoryStream position
            mfs.Position = 0;

        #nullable enable
            // Get the version provided by the XMF
            int[]? gameVersion = XMFUtility.GetXMFVersion(xmfStream);
            // Initialize and parse the manifest, then return the Patch Asset
            return gameVersion == null ? null : new BlockPatchManifest(mfs);
        }

        private void BuildBlockIndex(List<FilePropertiesRemote> assetIndex, BlockPatchManifest? patchInfo, string xmfPath, Stream xmfStream, bool isMeta)
        {
            // Reset the temporal stream pos.
            xmfStream.Position = 0;

            // Initialize and parse the XMF file
            XMFParser xmfParser = new XMFParser(xmfPath, xmfStream, isMeta);

            // Do loop and assign the block asset to asset index
            for (int i = 0; i < xmfParser.BlockCount; i++)
            {
                // Check if the patch info exist for current block, then assign blockPatchInfo
                BlockPatchInfo? blockPatchInfo = null;

                if (patchInfo != null && patchInfo.NewBlockCatalog!.TryGetValue(xmfParser.BlockEntry![i]!.HashString!, out int blockPatchInfoIndex))
                {
                    blockPatchInfo = patchInfo.PatchAsset![blockPatchInfoIndex];
                }

                // If the block has patch information, add the source block to the ignored list of unused assets
                if (blockPatchInfo.HasValue)
                {
                    // Enumerate the pairs to get the old file names
                    for (var index = blockPatchInfo.Value.PatchPairs.Count - 1; index >= 0; index--)
                    {
                        var pairs = blockPatchInfo.Value.PatchPairs[index];
                        // Get the local path and check if the file exists and does not listed into ignored list,
                        // then add the file into the ignored list
                        string localPath =
                            Path.Combine(GamePath!, NormalizePath(BlockBasePath)!, pairs.OldHashStr + ".wmv");
                        if (File.Exists(localPath) && !_ignoredUnusedFileList.Contains(localPath))
                            _ignoredUnusedFileList.Add(localPath);
                    }
                }

                // Assign as FilePropertiesRemote
                FilePropertiesRemote assetInfo = new FilePropertiesRemote
                {
                    N = CombineURLFromString(BlockBasePath, xmfParser.BlockEntry![i]!.HashString + ".wmv")!,
                    RN = CombineURLFromString(BlockAsbBaseURL, xmfParser.BlockEntry[i]!.HashString + ".wmv")!,
                    S = xmfParser.BlockEntry[i]!.Size,
                    CRC = xmfParser.BlockEntry[i]!.HashString!,
                    FT = FileType.Block,
                    BlockPatchInfo = blockPatchInfo
                };

                // Add the asset info
                assetIndex.Add(assetInfo);
            }

            // Write the blockVerifiedVersion based on secondary XMF
            File.WriteAllText(Path.Combine(GamePath!, NormalizePath(BlockBasePath)!, "blockVerifiedVersion.txt"), string.Join('_', xmfParser.Version!));
        }
#nullable disable

        private void CountAssetIndex(List<FilePropertiesRemote> assetIndex)
        {
            // Filter out video assets
            List<FilePropertiesRemote> assetIndexFiltered = assetIndex!.Where(x => x!.FT != FileType.Video).ToList();

            // Sum the assetIndex size and assign to _progressAllSize
            ProgressAllSizeTotal = assetIndexFiltered.Sum(x => x!.S);

            // Assign the assetIndex count to _progressAllCount
            ProgressAllCountTotal = assetIndexFiltered.Count;
        }
        #endregion
    }
}
