using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault

namespace CollapseLauncher
{
    internal static partial class StarRailRepairExtension
    {
        private static readonly Dictionary<string, int> Hashtable = new();

        internal static void ClearHashtable() => Hashtable.Clear();

        internal static void AddSanitize(this List<FilePropertiesRemote> assetIndex, FilePropertiesRemote assetProperty)
        {
            string key = assetProperty.N + assetProperty.IsPatchApplicable;

            // Check if the asset has the key
            // If yes (exist), then get the index of the asset from hashtable
            if (Hashtable.TryGetValue(key, out int index))
            {
                // Get the property of the asset based on index from hashtable
                FilePropertiesRemote oldAssetProperty = assetIndex[index];
                // If the hash is not equal, then replace the existing property from assetIndex
                if (oldAssetProperty.CRCArray
                                    .AsSpan()
                                    .SequenceEqual(assetProperty.CRCArray))
                {
                    return;
                }
            #if DEBUG
                LogWriteLine($"[StarRailRepairExtension::AddSanitize()] Replacing duplicate of: {assetProperty.N} from: {oldAssetProperty.CRC}|{oldAssetProperty.S} to {assetProperty.CRC}|{assetProperty.S}", LogType.Debug, true);
            #endif
                assetIndex[index] = assetProperty;
                return;
            }

            Hashtable.Add(key, assetIndex.Count);
            assetIndex.Add(assetProperty);
        }
    }

    internal partial class StarRailRepair
    {
        private async Task Fetch(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            // Set total activity string as "Loading Indexes..."
            Status.ActivityStatus            = Lang._GameRepairPage.Status2;
            Status.IsProgressAllIndetermined = true;

            UpdateStatus();
            StarRailRepairExtension.ClearHashtable();

            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder()
                .UseLauncherConfig(DownloadThreadCount + DownloadThreadCountReserved)
                .SetUserAgent(UserAgent)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Initialize the new DownloadClient
            DownloadClient downloadClient = DownloadClient.CreateInstance(client);

            try
            {
                // Get the primary manifest
                await GetPrimaryManifest(assetIndex, token);

                // If the this._isOnlyRecoverMain && base._isVersionOverride is true, copy the asset index into the _originAssetIndex
                if (IsOnlyRecoverMain && IsVersionOverride)
                {
                    OriginAssetIndex = [];
                    foreach (FilePropertiesRemote asset in assetIndex)
                    {
                        FilePropertiesRemote newAsset = asset.Copy();
                        ReadOnlyMemory<char> assetRelativePath = newAsset.N.AsMemory(GamePath.Length).TrimStart('\\');
                        newAsset.N = assetRelativePath.ToString();
                        OriginAssetIndex.Add(newAsset);
                    }
                }

                // Subscribe the fetching progress and subscribe StarRailMetadataTool progress to adapter
                // _innerGameVersionManager.StarRailMetadataTool.HttpEvent += _httpClient_FetchAssetProgress;

                // Initialize the metadata tool (including dispatcher and gateway).
                // Perform this if only base._isVersionOverride is false to indicate that the repair performed is
                // not for delta patch integrity check.
                if (!IsVersionOverride && !IsOnlyRecoverMain && await InnerGameVersionManager.StarRailMetadataTool.Initialize(token, downloadClient, _httpClient_FetchAssetProgress, GetExistingGameRegionID(), Path.Combine(GamePath, $"{Path.GetFileNameWithoutExtension(InnerGameVersionManager.GamePreset.GameExecutableName)}_Data\\Persistent")))
                {
                    await Task.WhenAll(
                        // Read Block metadata
                        InnerGameVersionManager.StarRailMetadataTool.ReadAsbMetadataInformation(downloadClient, _httpClient_FetchAssetProgress, token),
                        InnerGameVersionManager.StarRailMetadataTool.ReadBlockMetadataInformation(downloadClient, _httpClient_FetchAssetProgress, token),
                        // Read Audio metadata
                        InnerGameVersionManager.StarRailMetadataTool.ReadAudioMetadataInformation(downloadClient, _httpClient_FetchAssetProgress, token),
                        // Read Video metadata
                        InnerGameVersionManager.StarRailMetadataTool.ReadVideoMetadataInformation(downloadClient, _httpClient_FetchAssetProgress, token)
                        ).ConfigureAwait(false);

                    // Convert Block, Audio and Video metadata to FilePropertiesRemote
                    ConvertSrMetadataToAssetIndex(InnerGameVersionManager.StarRailMetadataTool.MetadataBlock, assetIndex);
                    ConvertSrMetadataToAssetIndex(InnerGameVersionManager.StarRailMetadataTool.MetadataAudio, assetIndex, true);
                    ConvertSrMetadataToAssetIndex(InnerGameVersionManager.StarRailMetadataTool.MetadataVideo, assetIndex);
                }

                // Force-Fetch the Bilibili SDK (if exist :pepehands:)
                await FetchBilibiliSdk(token);

                // Remove plugin from assetIndex
                // Skip the removal for Delta-Patch
                if (!IsOnlyRecoverMain)
                {
                    EliminatePluginAssetIndex(assetIndex);
                }
            }
            finally
            {
                // Clear the hashtable
                StarRailRepairExtension.ClearHashtable();
                // Unsubscribe the fetching progress and dispose it and unsubscribe cacheUtil progress to adapter
                // _innerGameVersionManager.StarRailMetadataTool.HttpEvent -= _httpClient_FetchAssetProgress;
            }
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

        #region PrimaryManifest
        private async Task GetPrimaryManifest(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
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
                    throw new VersionNotFoundException($"Manifest for {GameVersionManager.GamePreset.ZoneName} (version: {GameVersion.VersionString}) doesn't exist! Please contact @neon-nyan or open an issue for this!");
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
            await using BridgedNetworkStream stream = await FallbackCDNUtil.TryGetCDNFallbackStream(urlIndex, token);
            if (stream != null)
            {
                // Deserialize asset index and set it to list
                AssetIndexV2 parserTool = new AssetIndexV2();
                pkgVersion = parserTool.Deserialize(stream, out DateTime timestamp);
                LogWriteLine($"Asset index timestamp: {timestamp}", LogType.Default, true);
            }

            // Convert the pkg version list to asset index
            ConvertPkgVersionToAssetIndex(pkgVersion, assetIndex);

            // Clear the pkg version list
            pkgVersion.Clear();
        }

        private async Task<Dictionary<string, string>> FetchMetadata(CancellationToken token)
        {
            // Set metadata URL
            string urlMetadata = string.Format(LauncherConfig.AppGameRepoIndexURLPrefix, GameVersionManager.GamePreset.ProfileName);

            // Start downloading metadata using FallbackCDNUtil
            await using BridgedNetworkStream stream = await FallbackCDNUtil.TryGetCDNFallbackStream(urlMetadata, token);
            return await stream.DeserializeAsync(CoreLibraryJsonContext.Default.DictionaryStringString, token: token);
        }

        private void ConvertPkgVersionToAssetIndex(List<PkgVersionProperties> pkgVersion, List<FilePropertiesRemote> assetIndex)
        {
            for (var index = pkgVersion.Count - 1; index >= 0; index--)
            {
                var entry = pkgVersion[index];
                // Add the pkgVersion entry to asset index
                FilePropertiesRemote normalizedProperty = GetNormalizedFilePropertyTypeBased(
                     GameRepoURL,
                     entry.remoteName,
                     entry.fileSize,
                     entry.md5,
                     FileType.Generic,
                     true);
                assetIndex.AddSanitize(normalizedProperty);
            }
        }
        #endregion

        #region Utilities
        private FilePropertiesRemote GetNormalizedFilePropertyTypeBased(string remoteParentURL,
                                                                        string remoteRelativePath,
                                                                        long fileSize,
                                                                        string hash,
                                                                        FileType type = FileType.Generic,
                                                                        bool isPatchApplicable = false, 
                                                                        bool isHasHashMark = false)
        {
            string remoteAbsolutePath = type switch
                                        {
                                            FileType.Generic => CombineURLFromString(remoteParentURL, remoteRelativePath),
                                            _ => remoteParentURL
                                        },
                   typeAssetRelativeParentPath = string.Format(type switch
                                                               {
                                                                   FileType.Block => AssetGameBlocksStreamingPath,
                                                                   FileType.Audio => AssetGameAudioStreamingPath,
                                                                   FileType.Video => AssetGameVideoStreamingPath,
                                                                   _ => string.Empty
                                                               }, ExecName);

            var localAbsolutePath = Path.Combine(GamePath, typeAssetRelativeParentPath, NormalizePath(remoteRelativePath));

            return new FilePropertiesRemote
            {
                FT = type,
                CRC = hash,
                S = fileSize,
                N = localAbsolutePath,
                RN = remoteAbsolutePath,
                IsPatchApplicable = isPatchApplicable,
                IsHasHashMark = isHasHashMark
            };
        }

        private unsafe string GetExistingGameRegionID()
        {
            // Delegate the default return value
            string GetDefaultValue() => InnerGameVersionManager.GamePreset.GameDispatchDefaultName ?? throw new KeyNotFoundException("Default dispatcher name in metadata is not exist!");

#nullable enable
            // Try to get the value as nullable object
            object? value = RegistryRoot?.GetValue("App_LastServerName_h2577443795", null);
            // Check if the value is null, then return the default name
            // Return the dispatch default name. If none, then throw
            if (value == null) return GetDefaultValue();
#nullable disable

            // Cast the value as byte array
            byte[] valueBytes = (byte[])value;
            int count = valueBytes.Length;

            // If the registry is empty, then return the default value;
            if (valueBytes.Length == 0)
                return GetDefaultValue();

            // Get the pointer of the byte array
            fixed (byte* valuePtr = &valueBytes[0])
            {
                // Try check the byte value. If it's null, then continue the loop while
                // also decreasing the count as its index
                while (*(valuePtr + (count - 1)) == 0) { --count; }

                // Get the name from the span and trim the \0 character at the end
                string name = Encoding.UTF8.GetString(valuePtr, count);
                return name;
            }
        }

        private void ConvertSrMetadataToAssetIndex(SRMetadataBase metadata, List<FilePropertiesRemote> assetIndex, bool writeAudioLangReordered = false)
        {
            // Get the voice Lang ID
            int voLangID = InnerGameVersionManager.GamePreset.GetVoiceLanguageID();
            // Get the voice Lang name by ID
            string voLangName = PresetConfig.GetStarRailVoiceLanguageFullNameByID(voLangID);

            // If prompt to write Redord file
            if (writeAudioLangReordered)
            {
                // Get game executable name, directory and file path
                string execName = Path.GetFileNameWithoutExtension(InnerGameVersionManager.GamePreset.GameExecutableName);
                string audioReorderedDir = Path.Combine(GamePath, @$"{execName}_Data\Persistent\Audio\AudioPackage\Windows");
                string audioReorderedPath = EnsureCreationOfDirectory(Path.Combine(audioReorderedDir, "AudioLangRedord.txt"));

                // Then write the Redord file content
                File.WriteAllText(audioReorderedPath, "{\"AudioLang\":\"" + voLangName + "\"}");
            }

            // Get the audio lang list
            string[] audioLangList = GetCurrentAudioLangList(voLangName);

            // Enumerate the Asset List
            int lastAssetIndexCount = assetIndex.Count;
            foreach (SRAsset asset in metadata.EnumerateAssets())
            {
                // Get the hash by bytes
                string hash = HexTool.BytesToHexUnsafe(asset.Hash);

                // Filter only current audio language file and other assets
                if (!FilterCurrentAudioLangFile(asset, audioLangList, out bool isHasHashMark))
                {
                    continue;
                }

                // Convert and add the asset as FilePropertiesRemote to assetIndex
                FilePropertiesRemote assetProperty = GetNormalizedFilePropertyTypeBased(
                     asset.RemoteURL,
                     asset.LocalName,
                     asset.Size,
                     hash,
                     ConvertFileTypeEnum(asset.AssetType),
                     asset.IsPatch,
                     isHasHashMark
                    );
                assetIndex.AddSanitize(assetProperty);
            }

            int addedCount = assetIndex.Count - lastAssetIndexCount;
            long addedSize = 0;
            ReadOnlySpan<FilePropertiesRemote> assetIndexSpan = CollectionsMarshal.AsSpan(assetIndex)[lastAssetIndexCount..];
            for (int i = assetIndexSpan.Length - 1; i >= 0; i--) addedSize += assetIndexSpan[i].S;

            LogWriteLine($"Added additional {addedCount} assets with {SummarizeSizeSimple(addedSize)}/{addedSize} bytes in size", LogType.Default, true);
        }

        private string[] GetCurrentAudioLangList(string fallbackCurrentLangName)
        {
            // Initialize the variable.
            string audioLangListPath = GameAudioLangListPath;
            string audioLangListPathStatic = GameAudioLangListPathStatic;
            string[] returnValue;

            // Check if the audioLangListPath is null or the file is not exist,
            // then create a new one from the fallback value
            if (audioLangListPath == null || !File.Exists(audioLangListPathStatic))
            {
                // Try check if the folder exist. If not, create one.
                string audioLangPathDir = Path.GetDirectoryName(audioLangListPathStatic);
                if (Directory.Exists(audioLangPathDir))
                    Directory.CreateDirectory(audioLangPathDir);

                // Assign the default value and write to the file, then return.
                returnValue = [fallbackCurrentLangName];
                if (audioLangListPathStatic != null)
                {
                    File.WriteAllLines(audioLangListPathStatic, returnValue);
                }

                return returnValue;
            }

            // Read all the lines. If empty, then assign the default value and rewrite it
            returnValue = File.ReadAllLines(audioLangListPathStatic);
            if (returnValue.Length != 0)
            {
                return returnValue;
            }

            returnValue = [fallbackCurrentLangName];
            File.WriteAllLines(audioLangListPathStatic, returnValue);

            // Return the value
            return returnValue;
        }

        private static bool FilterCurrentAudioLangFile(SRAsset asset, string[] langNames, out bool isHasHashMark)
        {
            // Set output value as false
            isHasHashMark = false;
            switch (asset.AssetType)
            {
                // In case if the type is SRAssetType.Audio, then do filtering
                case SRAssetType.Audio:
                    // Set isHasHashMark to true
                    isHasHashMark = true;
                    // Split the name definition from LocalName
                    string[] nameDef = asset.LocalName.Split('/');
                    // If the name definition array length > 1, then start do filtering
                    if (nameDef.Length > 1)
                    {
                        // Compare if the first name definition is equal to target langName.
                        // Also return if the file is an audio language file if it is a SFX file or not.
                        return langNames.Contains(nameDef[0], StringComparer.OrdinalIgnoreCase) || nameDef[0] == "SFX";
                    }
                    // If it's not in criteria of name definition, then return true as "normal asset"
                    return true;
                default:
                    // return true as "normal asset"
                    return true;
            }
        }

        private void CountAssetIndex(List<FilePropertiesRemote> assetIndex)
        {
            // Sum the assetIndex size and assign to _progressAllSize
            ProgressAllSizeTotal = assetIndex.Sum(x => x.S);

            // Assign the assetIndex count to _progressAllCount
            ProgressAllCountTotal = assetIndex.Count;
        }

        private static FileType ConvertFileTypeEnum(SRAssetType assetType) => assetType switch
                                                                              {
                                                                                  SRAssetType.Asb => FileType.Block,
                                                                                  SRAssetType.Block => FileType.Block,
                                                                                  SRAssetType.Audio => FileType.Audio,
                                                                                  SRAssetType.Video => FileType.Video,
                                                                                  _ => FileType.Generic
                                                                              };

        private static RepairAssetType ConvertRepairAssetTypeEnum(FileType assetType) => assetType switch
                                                                                         {
                                                                                             FileType.Block => RepairAssetType.Block,
                                                                                             FileType.Audio => RepairAssetType.Audio,
                                                                                             FileType.Video => RepairAssetType.Video,
                                                                                             _ => RepairAssetType.Generic
                                                                                         };
        #endregion
    }
}
