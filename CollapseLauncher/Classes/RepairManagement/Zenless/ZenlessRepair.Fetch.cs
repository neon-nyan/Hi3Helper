using CollapseLauncher.GameSettings.Zenless;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.EncTool.Parser.Sleepy;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

namespace CollapseLauncher
{
    internal partial class ZenlessRepair
    {
        #region Main Fetch Routine
        private async Task Fetch(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            // Set total activity string as "Loading Indexes..."
            Status.ActivityStatus = Locale.Lang._GameRepairPage.Status2;
            Status.IsProgressAllIndetermined = true;
            UpdateStatus();

            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder()
                .UseLauncherConfig(DownloadThreadCount + DownloadThreadCountReserved)
                .SetUserAgent(UserAgent)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Initialize the new DownloadClient
            DownloadClient downloadClient = DownloadClient.CreateInstance(client);

            // Create a hash set to overwrite local files
            Dictionary<string, FilePropertiesRemote> hashSet = new(StringComparer.OrdinalIgnoreCase);

            // If not in cache mode, then fetch main package
            if (!IsCacheUpdateMode)
            {
                // Get the primary manifest
                await GetPrimaryManifest(hashSet, assetIndex, token);
            }

            // Execute on non-recover main mode
            if (!IsOnlyRecoverMain)
            {
                // Get the in-game res manifest
                await GetResManifest(downloadClient, hashSet, assetIndex,
#if DEBUG
                    true
#else
                    false
#endif
                  , token);

                // Execute plugin things if not in cache mode only
                if (!IsCacheUpdateMode)
                {
                    // Force-Fetch the Bilibili SDK (if exist :pepehands:)
                    await FetchBilibiliSdk(token);

                    // Remove plugin from assetIndex
                    // Skip the removal for Delta-Patch
                    EliminatePluginAssetIndex(assetIndex);
                }
            }
        }
        #endregion

        #region PrimaryManifest
        private async Task GetPrimaryManifest(Dictionary<string, FilePropertiesRemote> hashSet, List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            // If it's using cache update mode, then return since we don't need to add manifest
            // from pkg_version on cache update mode.
            if (IsCacheUpdateMode)
                return;

            // Initialize pkgVersion list
            List<PkgVersionProperties> pkgVersion = [];

            // Initialize repo metadata
            try
            {
                // Get the metadata
                Dictionary<string, string> repoMetadata = await FetchMetadata(token);

                // Check for manifest. If it doesn't exist, then throw and warn the user
                if (!repoMetadata.TryGetValue(GameVersion.VersionString, out var value))
                {
                    // If version override is on, then throw
                    if (IsVersionOverride)
                        throw new VersionNotFoundException($"Manifest for {GameVersionManager.GamePreset.ZoneName} (version: {GameVersion.VersionString}) doesn't exist! Please contact @neon-nyan or open an issue for this!");

                    // Otherwise, fallback to the launcher's pkg_version
                    RegionResourceVersion latestZipApi = GameVersionManagerCast?.GetGameLatestZip(GameInstallStateEnum.Installed).FirstOrDefault();
                    string latestZipApiUrl = latestZipApi?.decompressed_path;

                    // Throw if latest zip api URL returns null
                    if (string.IsNullOrEmpty(latestZipApiUrl))
                        throw new NullReferenceException("Cannot find latest zip api url while failing back to pkg_version");

                    // Assign the URL based on the version
                    GameRepoURL = latestZipApiUrl;

                    // Combine pkg_version url
                    latestZipApiUrl = ConverterTool.CombineURLFromString(latestZipApiUrl, "pkg_version");

                    // Read pkg_version stream response
                    await using Stream stream = await FallbackCDNUtil.GetHttpStreamFromResponse(latestZipApiUrl, token);
                    await foreach (FilePropertiesRemote asset in stream
                        .EnumerateStreamToPkgVersionPropertiesAsync(token)
                        .RegisterMainCategorizedAssetsToHashSetAsync(assetIndex, hashSet, GamePath, GameRepoURL, token))
                    {
                        // If entry is null (means, an existing entry has been overwritten), then next
                        if (asset == null)
                            continue;

                        assetIndex.Add(asset);
                    }
                }

                // Assign the URL based on the version
                GameRepoURL = value;
            }
            // If the base._isVersionOverride is true, then throw. This sanity check is required if the delta patch is being performed.
            catch when (IsVersionOverride) { throw; }

            // Fetch the asset index from CDN
            // Set asset index URL
            string urlIndex = string.Format(LauncherConfig.AppGameRepairIndexURLPrefix, GameVersionManager.GamePreset.ProfileName, GameVersion.VersionString) + ".binv2";

            // Start downloading asset index using FallbackCDNUtil and return its stream
            await Task.Run(async () =>
            {
                await using BridgedNetworkStream stream = await FallbackCDNUtil.TryGetCDNFallbackStream(urlIndex, token);
                if (stream != null)
                {
                    // Deserialize asset index and set it to list
                    AssetIndexV2 parserTool = new AssetIndexV2();
                    pkgVersion = parserTool.Deserialize(stream, out DateTime timestamp);
                    Logger.LogWriteLine($"Asset index timestamp: {timestamp}", LogType.Default, true);
                }

                // Convert the pkg version list to asset index
                foreach (FilePropertiesRemote entry in pkgVersion.RegisterMainCategorizedAssetsToHashSet(assetIndex, hashSet, GamePath, GameRepoURL))
                {
                    // If entry is null (means, an existing entry has been overwritten), then next
                    if (entry == null)
                        continue;

                    assetIndex.Add(entry);
                }

                // Clear the pkg version list
                pkgVersion.Clear();
            }, token).ConfigureAwait(false);
        }

        private async Task<Dictionary<string, string>> FetchMetadata(CancellationToken token)
        {
            // Set metadata URL
            string urlMetadata = string.Format(LauncherConfig.AppGameRepoIndexURLPrefix, GameVersionManagerCast!.GamePreset.ProfileName);

            // Start downloading metadata using FallbackCDNUtil
            await using BridgedNetworkStream stream = await FallbackCDNUtil.TryGetCDNFallbackStream(urlMetadata, token);
            return await stream.DeserializeAsync(CoreLibraryJsonContext.Default.DictionaryStringString, token: token);
        }
        #endregion

        #region ResManifest
        private async Task GetResManifest(
            DownloadClient                           downloadClient,
            Dictionary<string, FilePropertiesRemote> hashSet,
            List<FilePropertiesRemote>               assetIndex,
            bool                                     throwIfError,
            CancellationToken                        token)
        {
            try
            {
                // Create sleepy property
                PresetConfig gamePreset = GameVersionManagerCast!.GamePreset;
                HttpClient client = downloadClient.GetHttpClient();

                // Get sleepy info
                SleepyInfo sleepyInfo = await TryGetSleepyInfo(
                    client,
                    gamePreset,
                    (GameSettings as ZenlessSettings)?.GeneralData.SelectedServerName,
                    gamePreset.GameDispatchDefaultName,
                    token);

                // Get persistent path
                string persistentPath = GameDataPersistentPath;

                // Get Sleepy Info
                SleepyFileInfoResult infoKindSilence = sleepyInfo.GetFileInfoResult(FileInfoKind.Silence);
                SleepyFileInfoResult infoKindData = sleepyInfo.GetFileInfoResult(FileInfoKind.Data);
                SleepyFileInfoResult infoKindRes = sleepyInfo.GetFileInfoResult(FileInfoKind.Res);
                SleepyFileInfoResult infoKindAudio = sleepyInfo.GetFileInfoResult(FileInfoKind.Audio);
                SleepyFileInfoResult infoKindBase = sleepyInfo.GetFileInfoResult(FileInfoKind.Base);

                // Create non-patch URL
                string baseResUrl = GetBaseResUrl(infoKindBase.BaseUrl, infoKindBase.RevisionStamp);

                // Fetch cache files
                if (IsCacheUpdateMode)
                {
                    IAsyncEnumerable<FilePropertiesRemote> infoSilenceEnumerable = EnumerateResManifestToAssetIndexAsync(
                        infoKindSilence.RegisterSleepyFileInfoToManifest(client, assetIndex, true, persistentPath, token),
                        assetIndex,
                        hashSet,
                        Path.Combine(GamePath, $@"{ExecutableName}_Data\"),
                        infoKindSilence.BaseUrl, baseResUrl);

                    IAsyncEnumerable<FilePropertiesRemote> infoDataEnumerable = EnumerateResManifestToAssetIndexAsync(
                        infoKindData.RegisterSleepyFileInfoToManifest(client, assetIndex, true, persistentPath, token),
                        assetIndex,
                        hashSet,
                        Path.Combine(GamePath, $@"{ExecutableName}_Data\"),
                        infoKindData.BaseUrl, baseResUrl);

                    await foreach (FilePropertiesRemote asset in ZenlessRepairExtensions
                                                                .MergeAsyncEnumerable(infoSilenceEnumerable, infoDataEnumerable).WithCancellation(token))
                    {
                        assetIndex.Add(asset);
                    }

                    // Create base revision file
                    string baseRevisionFile = Path.Combine(persistentPath, "base_revision");
                    await File.WriteAllTextAsync(EnsureCreationOfDirectory(baseRevisionFile), infoKindBase.RevisionStamp, token);
                }
                // Fetch repair files
                else
                {
                    IAsyncEnumerable<FilePropertiesRemote> infoResEnumerable = EnumerateResManifestToAssetIndexAsync(
                        infoKindRes.RegisterSleepyFileInfoToManifest(client, assetIndex, true, persistentPath, token),
                        assetIndex,
                        hashSet,
                        Path.Combine(GamePath, $@"{ExecutableName}_Data\"),
                        infoKindRes.BaseUrl, baseResUrl);

                    IAsyncEnumerable<FilePropertiesRemote> infoAudioEnumerable = GetOnlyInstalledAudioPack(
                            EnumerateResManifestToAssetIndexAsync(
                                                                  infoKindAudio.RegisterSleepyFileInfoToManifest(client, assetIndex, true, persistentPath, token),
                                                                  assetIndex,
                                                                  hashSet,
                                                                  Path.Combine(GamePath, $@"{ExecutableName}_Data\"),
                                                                  infoKindAudio.BaseUrl, baseResUrl)
                        );

                    await foreach (FilePropertiesRemote asset in ZenlessRepairExtensions
                                                                .MergeAsyncEnumerable(infoResEnumerable, infoAudioEnumerable).WithCancellation(token))
                    {
                        assetIndex.Add(asset);
                    }
                }
            }
            catch (Exception ex)
            {
                if (throwIfError)
                    throw;

                Logger.LogWriteLine($"An error has occurred while trying to fetch Sleepy's res manifest\r\n{ex}", LogType.Error, true);
            }
        }

        private static string GetBaseResUrl(string baseUrl, string stampRevision)
        {
            const string startMark = "output_";
            const string endMark = "/client";

            ReadOnlySpan<char> baseUrlSpan = baseUrl;

            int startMarkOffset = baseUrlSpan.IndexOf(startMark);
            int endMarkOffset = baseUrlSpan.IndexOf(endMark);

            if (startMarkOffset < 0 || endMarkOffset < 0)
                throw new IndexOutOfRangeException($"Start mark offset or End mark offset was not found! Start: {startMarkOffset} End: {endMarkOffset}");

            ReadOnlySpan<char> startMarkSpan = baseUrlSpan[..startMarkOffset];
            ReadOnlySpan<char> endMarkSpan   = baseUrlSpan[endMarkOffset..];

            return ConverterTool.CombineURLFromString(startMarkSpan, $"output_{stampRevision}", endMarkSpan.ToString());
        }

        private async Task<SleepyInfo> TryGetSleepyInfo(HttpClient client, PresetConfig gamePreset, string targetServerName, string fallbackServerName = null, CancellationToken token = default)
        {
            try
            {
                // Get re-associated channel ids
                string dispatchUrlTemplate = ('.' + gamePreset.GameDispatchURLTemplate).AssociateGameAndLauncherId(
                    "channel_id",
                    "sub_channel_id",
                    $"{gamePreset.ChannelID}",
                    $"{gamePreset.SubChannelID}")!.TrimStart('.');
                string gatewayUrlTemplate = ('.' + gamePreset.GameGatewayURLTemplate)!.AssociateGameAndLauncherId(
                    "channel_id",
                    "sub_channel_id",
                    $"{gamePreset.ChannelID}",
                    $"{gamePreset.SubChannelID}")!.TrimStart('.');

                // Initialize property
                SleepyProperty sleepyProperty = SleepyProperty.Create(
                    GameVersionManagerCast!.GetGameExistingVersion()?.VersionString,
                    gamePreset.GameDispatchChannelName,
                    gamePreset.GameDispatchArrayURL![Random.Shared.Next(0, gamePreset.GameDispatchArrayURL.Count - 1)],
                    dispatchUrlTemplate,
                    targetServerName ?? fallbackServerName,
                    gatewayUrlTemplate,
                    gamePreset.ProtoDispatchKey,
                    SleepyBuildProperty.Create(GameVersionManagerCast.SleepyIdentity, GameVersionManagerCast.SleepyArea)
                    );

                // Create sleepy instance
                SleepyInfo sleepyInfoReturn = SleepyInfo.CreateSleepyInfo(
                    client,
                    GameVersionManagerCast.SleepyInstance,
                    sleepyProperty
                    );

                // Initialize sleepy instance before using
                await sleepyInfoReturn.Initialize(token);

                // Return SleepyInfo
                return sleepyInfoReturn;
            }
            catch (TaskCanceledException) { throw; }
            catch (OperationCanceledException) { throw; }
            catch (Exception) when (!string.IsNullOrEmpty(fallbackServerName))
            {
                // If it fails, try to get the SleepyInfo from fallback server name
                return await TryGetSleepyInfo(client, gamePreset, fallbackServerName, null, token);
            }
        }

        private async IAsyncEnumerable<FilePropertiesRemote> GetOnlyInstalledAudioPack(IAsyncEnumerable<FilePropertiesRemote> enumerable)
        {
            const string windowsFullPath = @"Audio\Windows\Full";
            string[] audioList = GetCurrentAudioLangList("Jp");
            SearchValues<string> searchExclude = SearchValues.Create(audioList, StringComparison.OrdinalIgnoreCase);

            await foreach (FilePropertiesRemote asset in enumerable)
            {
                if (IsAudioFileIncluded(windowsFullPath, searchExclude, asset))
                    yield return asset;
            }
        }

        private static bool IsAudioFileIncluded(string windowsFullPath, SearchValues<string> searchInclude, FilePropertiesRemote asset)
        {
            // If it's not audio file, then return
            ReadOnlySpan<char> relPath = asset.GetAssetRelativePath(out RepairAssetType assetType);
            if (relPath.IsEmpty || assetType != RepairAssetType.Audio)
                return true;

            ReadOnlySpan<char> dirRelPath = Path.GetDirectoryName(relPath);
            // If non language audio file, then return
            if (dirRelPath.EndsWith(windowsFullPath))
                return true;

            // Check if non-full path, then return
            int indexOf = dirRelPath.LastIndexOf(windowsFullPath);
            if (indexOf < 0)
                return true;

            // If the index is more than WindowsFullPath.Length (included), then return
            ReadOnlySpan<char> lastSequence = Path.GetFileName(dirRelPath);
            int indexOfAny = lastSequence.IndexOfAny(searchInclude);

            // Otherwise, return false
            return indexOfAny == 0;
        }
        #endregion

        #region Utilities
#nullable enable
        private static async IAsyncEnumerable<FilePropertiesRemote> EnumerateResManifestToAssetIndexAsync(
            IAsyncEnumerable<PkgVersionProperties> pkgVersion,
            List<FilePropertiesRemote> assetIndex,
            Dictionary<string, FilePropertiesRemote> hashSet,
            string baseLocalPath,
            string basePatchUrl,
            string baseResUrl)
        {
            await foreach (FilePropertiesRemote? entry in pkgVersion.RegisterResCategorizedAssetsToHashSetAsync(assetIndex, hashSet, baseLocalPath, basePatchUrl, baseResUrl))
            {
                // If entry is null (means, an existing entry has been overwritten), then next
                if (entry == null)
                    continue;

                yield return entry;
            }
        }
#nullable restore

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
                          Logger.LogWriteLine($"[EliminatePluginAssetIndex] Removed: {asset.N}", LogType.Warning, true);
                      }
                      return r ?? false;
                  });
              });
        }

        private string[] GetCurrentAudioLangList(string fallbackCurrentLangName)
        {
            // Initialize the variable.
            string audioLangListPath = GameAudioLangListPath;
            string audioLangListPathStatic = GameAudioLangListPathStatic;
            string audioLangListPathAlternative = GameAudioLangListPathAlternate;
            string audioLangListPathAlternativeStatic = GameAudioLangListPathAlternateStatic;

            string[] returnValue;
            string fallbackCurrentLangNameNative = fallbackCurrentLangName switch
            {
                "Jp" => "Japanese",
                "En" => "English(US)",
                "Cn" => "Chinese",
                "Kr" => "Korean",
                _ => throw new NotSupportedException()
            };

            // Check if the audioLangListPath is null or the file is not exist,
            // then create a new one from the fallback value
            if (audioLangListPath == null || !File.Exists(audioLangListPathStatic))
            {
                // Try check if the folder is existed. If not, create one.
                string audioLangPathDir = Path.GetDirectoryName(audioLangListPathStatic);
                if (Directory.Exists(audioLangPathDir))
                    Directory.CreateDirectory(audioLangPathDir);

                // Assign the default value and write to the file, then return.
                returnValue = [fallbackCurrentLangName];
                File.WriteAllLines(audioLangListPathStatic, returnValue);
                return returnValue;
            }

            // Read all the lines. If empty, then assign the default value and rewrite it
            returnValue = File.ReadAllLines(audioLangListPathStatic);
            if (returnValue.Length == 0)
            {
                returnValue = [fallbackCurrentLangName];
                File.WriteAllLines(audioLangListPathStatic, returnValue);
            }

            string[] returnValueAlternate;

            // Check if the audioLangListPathAlternative is null or the file is not exist,
            // then create a new one from the fallback value
            if (audioLangListPathAlternative == null || !File.Exists(audioLangListPathAlternativeStatic))
            {
                // Try check if the folder is existed. If not, create one.
                string audioLangPathDir = Path.GetDirectoryName(audioLangListPathAlternativeStatic);
                if (Directory.Exists(audioLangPathDir))
                    Directory.CreateDirectory(audioLangPathDir);

                // Assign the default value and write to the file, then return.
                returnValueAlternate = [fallbackCurrentLangNameNative];
                File.WriteAllLines(audioLangListPathAlternativeStatic, returnValueAlternate);
                return returnValueAlternate;
            }

            // Read all the lines. If empty, then assign the default value and rewrite it
            returnValueAlternate = File.ReadAllLines(audioLangListPathAlternativeStatic);
            if (returnValueAlternate.Length != 0)
            {
                return returnValueAlternate;
            }

            returnValueAlternate = [fallbackCurrentLangNameNative];
            File.WriteAllLines(audioLangListPathAlternativeStatic, returnValueAlternate);

            // Return the value
            return returnValueAlternate;
        }

        private void CountAssetIndex(List<FilePropertiesRemote> assetIndex)
        {
            // Sum the assetIndex size and assign to _progressAllSize
            ProgressAllSizeTotal = assetIndex.Sum(x => x.S);

            // Assign the assetIndex count to _progressAllCount
            ProgressAllCountTotal = assetIndex.Count;
        }
        #endregion
    }
}
