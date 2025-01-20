using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.Http.Legacy;
using Hi3Helper.Preset;
using Hi3Helper.SentryHelper;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

#pragma warning disable CS0618 // Type or member is obsolete
namespace CollapseLauncher.InstallManager
{
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    internal class GameInstallPackage : IAssetIndexSummary
    {
        #region Properties
        public string                   URL                     { get; set; }
        public string                   DecompressedURL         { get; set; }
        public string                   Name                    { get; set; }
        public string                   PathOutput              { get; set; }
        public GameInstallPackageType   PackageType             { get; set; }
        public long                     Size                    { get; set; }
        public long                     SizeRequired            { get; set; }
        public long                     SizeDownloaded          { get; set; }
        public GameVersion              Version                 { get; set; }
        public byte[]                   Hash                    { get; set; }
        public string                   HashString              { get => HexTool.BytesToHexUnsafe(Hash); }
        public string                   LanguageID              { get; set; }
        public List<GameInstallPackage> Segments                { get; set; }
        public string                   RunCommand              { get; set; }
        public string                   PluginId                { get; set; }
        public bool                     IsUseLegacyDownloader   { get; set; }
        #endregion

        public GameInstallPackage(RegionResourcePlugin packageProperty, string pathOutput)
            : this(packageProperty.package, pathOutput)
        {
            PluginId = packageProperty.plugin_id;
            RunCommand = packageProperty.package?.run_command;

            if (packageProperty.version != null)
            {
                Version = new GameVersion(packageProperty.version);
            }
            PackageType = GameInstallPackageType.Plugin;
        }

        public GameInstallPackage(RegionResourceVersion packageProperty, string pathOutput, string overrideVersion = null)
        {
            if (packageProperty == null || pathOutput == null) throw new NullReferenceException();
            
            if (packageProperty.path != null)
            {
                URL = packageProperty.path;
                Name = Path.GetFileName(packageProperty.path);
                PathOutput = Path.Combine(pathOutput, Name);
            }
            else if (packageProperty.url != null)
            {
                URL = packageProperty.url;
                Name = Path.GetFileName(packageProperty.url);
                PathOutput = Path.Combine(pathOutput, Name);
            }

            DecompressedURL = packageProperty.decompressed_path;
            SizeRequired = packageProperty.size;

            if (packageProperty.version != null || overrideVersion != null)
            {
                Version = new GameVersion(overrideVersion ?? packageProperty.version);
            }

            if (!string.IsNullOrEmpty(packageProperty.md5))
            {
                Hash = HexTool.HexToBytesUnsafe(packageProperty.md5);
            }
            if (packageProperty.language != null)
            {
                LanguageID = packageProperty.language;
            }

            if (packageProperty.segments is not { Count: > 0 })
            {
                return;
            }

            Name       = Path.GetFileName(packageProperty.segments.FirstOrDefault()?.path);
            PathOutput = Path.Combine(pathOutput, Name ?? "");
            Segments   = [];

            foreach (RegionResourceVersion segment in packageProperty.segments)
            {
                Segments.Add(new GameInstallPackage(segment, pathOutput));
            }
        }

        public bool IsReadStreamExist(int count)
        {
            if (PathOutput == null) return false;
            // Check if the single file exist or not
            FileInfo fileInfo = new FileInfo(PathOutput);
            if (fileInfo.Exists)
                return true;

            // Check for the chunk files
            return Enumerable.Range(0, count).All(chunkID =>
            {
                // Get the hash number
                long id = Http.GetHashNumber(count, chunkID);
                // Append the hash number to the path
                string pathLegacy = $"{PathOutput}.{id}";
                string path       = PathOutput + $".{chunkID + 1:000}";
                // Get the file info
                FileInfo fileInfoLegacy = new FileInfo(pathLegacy);
                FileInfo fileInfoLocal = new FileInfo(path);
                // Check if the file exist
                return fileInfoLegacy.Exists || fileInfoLocal.Exists;
            });
        }

        public Stream GetReadStream(int count)
        {
            // Get the file info of the single file
            FileInfo fileInfo = new FileInfo(PathOutput!);
            // Check if the file exist and the length is equal to the size
            if (fileInfo.Exists && fileInfo.Length == Size)
            {
                // Return the stream for read
                return fileInfo.Open(new FileStreamOptions
                {
                    Access = FileAccess.Read,
                    BufferSize = 4 << 10,
                    Mode = FileMode.Open,
                    Options = FileOptions.None,
                    Share = FileShare.Read
                });
            }

            // If the single file doesn't exist, then try getting chunk stream
            return GetCombinedStreamFromPackageAsset(count);
        }

        public long GetStreamLength(int count)
        {
            // Get the file info of the single file
            FileInfo fileInfo = new FileInfo(PathOutput!);
            // Check if the file exist and the length is equal to the size
            if (fileInfo.Exists && fileInfo.Length == Size)
            {
                // Return the stream for read
                return fileInfo.Length;
            }

            // If the single file doesn't exist, then try getting chunk stream
            return GetCombinedLengthFromPackageAsset(count);
        }

        private CombinedStream GetCombinedStreamFromPackageAsset(int count)
        {
            // Set the array
            FileStream[] streamList = new FileStream[count];
            // Enumerate the ID
            for (int i = 0; i < streamList.Length; i++)
            {
                // Get the hash ID
                long id = Http.GetHashNumber(count, i);
                // Append hash ID to the path
                string path       = PathOutput + $".{i + 1:000}";
                string pathLegacy = $"{PathOutput}.{id}";
                // Get the file info and check if the file exist
                FileInfo fileInfo = new FileInfo(path);
                FileInfo fileInfoLegacy = new FileInfo(pathLegacy);
                if (!fileInfo.Exists && !fileInfoLegacy.Exists)
                {
                    // If not found, then throw
                    throw new FileNotFoundException($"File chunk doesn't exist in this path! -> {path}");
                }

                // Allocate to the array and open the stream
                FileStreamOptions opt = new FileStreamOptions
                {
                    Access     = FileAccess.Read,
                    BufferSize = 4 << 10,
                    Mode       = FileMode.Open,
                    Options    = FileOptions.None,
                    Share      = FileShare.Read
                };
                if (fileInfo.Exists)
                    streamList[i] = fileInfo.Open(opt);
                else if (fileInfoLegacy.Exists)
                    streamList[i] = fileInfoLegacy.Open(opt);
            }

            // Assign the array and initiate it as a combined stream
            return new CombinedStream(streamList);
        }

        private long GetCombinedLengthFromPackageAsset(int count)
        {
            // Initialize length
            long length = 0;
            // Enumerate the ID
            for (int i = 0; i < count; i++)
            {
                // Get the hash ID
                long id = Http.GetHashNumber(count, i);
                // Append hash ID to the path
                string path       = PathOutput + $".{i + 1:000}";
                string pathLegacy = $"{PathOutput}.{id}";
                // Get the file info and check if the file exist
                FileInfo fileInfo = new FileInfo(path);
                FileInfo fileInfoLegacy = new FileInfo(pathLegacy);
                switch (fileInfo.Exists)
                {
                    case false when !fileInfoLegacy.Exists:
                        continue;
                    // Add length to the existing one
                    case true:
                        length += fileInfo.Length;
                        break;
                    default:
                    {
                        if (fileInfoLegacy.Exists)
                            length += fileInfoLegacy.Length;
                        break;
                    }
                }

                // Then go back to the loop routine
                // ReSharper disable once RedundantJumpStatement
                continue;
            }

            // Return the length
            return length;
        }

        public void DeleteFile(int count)
        {
            string lastFile = PathOutput;
            try
            {
                FileInfo fileInfo = new FileInfo(PathOutput!);
                if (fileInfo.Exists && fileInfo.Length == Size)
                {
                    fileInfo.Delete();
                }

                for (int i = 0; i < count; i++)
                {
                    long   id          = Http.GetHashNumber(count, i);
                    string path        = PathOutput + $".{i + 1:000}";
                    string pathLegacy  = $"{PathOutput}.{id}";
                    bool   isUseLegacy = File.Exists(pathLegacy);

                    lastFile = isUseLegacy ? pathLegacy : path;
                    fileInfo = new FileInfo(path);
                    FileInfo fileInfoLegacy = new FileInfo(pathLegacy);
                    if (fileInfo.Exists)
                    {
                        fileInfo.Delete();
                    }

                    if (fileInfoLegacy.Exists)
                    {
                        fileInfoLegacy.Delete();
                    }
                }
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                Logger.LogWriteLine($"Failed while deleting file: {lastFile}. Skipping!\r\n{ex}", LogType.Warning, true);
            }
        }

        public string PrintSummary() => $"File [T: {PackageType}]: {URL}\t{ConverterTool.SummarizeSizeSimple(Size)} ({Size} bytes)";
        public long GetAssetSize() => Size;
        public string GetRemoteURL() => URL;
        public void SetRemoteURL(string url) => URL = url;
    }
}
