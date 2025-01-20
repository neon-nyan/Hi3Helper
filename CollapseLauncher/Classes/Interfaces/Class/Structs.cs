using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Data;
using System;
using System.IO;
// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace CollapseLauncher
{
    internal class TotalPerFileProgress
    {
        public double ProgressPerFilePercentage;
        public double ProgressPerFileSpeed;
        public double ProgressAllPercentage;
        public double ProgressAllSpeed;

        public long ProgressPerFileEntryCountCurrent;
        public long ProgressPerFileEntryCountTotal;
        public long ProgressAllEntryCountCurrent;
        public long ProgressAllEntryCountTotal;

        // Extension for IGameInstallManager
        public long ProgressPerFileSizeCurrent;
        public long ProgressPerFileSizeTotal;
        public long ProgressAllSizeCurrent;
        public long ProgressAllSizeTotal;
        public TimeSpan ProgressAllTimeLeft;
    }

    internal class TotalPerFileStatus
    {
        public string ActivityStatus { get; set; }

        public string ActivityAll { get; set; }
        public bool IsProgressAllIndetermined { get; set; }

        public string ActivityPerFile { get; set; }
        public bool IsProgressPerFileIndetermined { get; set; }

        public bool IsAssetEntryPanelShow { get; set; }

        public bool IsCompleted { get; set; }
        public bool IsCanceled { get; set; }
        public bool IsRunning { get; set; }

        public bool IsIncludePerFileIndicator { get; set; }
    }

    internal struct GameVendorProp
    {
#nullable enable
        public GameVendorProp(string gamePath, string execName, GameVendorType fallbackVendorType)
        {
            // Set the fallback value of the Vendor Type first (in case the game isn't yet installed)
            VendorType = fallbackVendorType;

            // Concat the vendor app info file and return if it doesn't exist
            string infoVendorPath = Path.Combine(gamePath, $"{execName}_Data\\app.info");
            if (!File.Exists(infoVendorPath))
            {
                Logger.LogWriteLine("app.info file is not found!", LogType.Error);
                return;
            }

            // If it does, then process the file
            string[] infoEntries = File.ReadAllLines(infoVendorPath);
            if (infoEntries.Length < 2)
            {
                Logger.LogWriteLine("app.info file is malformed!", LogType.Error);
                return;
            }

            // Try parse the first line. If parsing fail, then return
            if (!Enum.TryParse(infoEntries[0], out GameVendorType _VendorType))
            {
                Logger.LogWriteLine($"Failed when parsing app.info\r\n\t{infoEntries[0]}\r\n\t{infoEntries[1]}");
                return;
            }

            // Assign the values
            VendorType = _VendorType;
            GameName = infoEntries[1];
        }

        public GameVendorType? VendorType { get; set; }
        public string? GameName { get; set; }
#nullable disable
    }

    public static class GameVersionExt
    {
        public static bool Compare(this GameVersion? fromVersion, GameVersion? toVersion)
        {
            if (!fromVersion.HasValue || !toVersion.HasValue) return false;
            return fromVersion.Value.ToVersion() < toVersion.Value.ToVersion();
        }

        public static bool Equals(this GameVersion? fromVersion, GameVersion? toVersion)
        {
            if (!fromVersion.HasValue || !toVersion.HasValue) return false;
            return fromVersion.Value.ToVersion() == toVersion.Value.ToVersion();
        }
    }

    public readonly record struct GameVersion
    {
        public GameVersion(int major, int minor, int build, int revision = 0)
        {
            Major = major;
            Minor = minor;
            Build = build;
            Revision = revision;
        }

        public GameVersion(ReadOnlySpan<int> ver)
        {
            if (ver.Length is not (3 or 4))
            {
                throw new ArgumentException("Version array entered should have length of 3 or 4!");
            }

            Major = ver[0];
            Minor = ver[1];
            Build = ver[2];
            if (ver.Length == 4)
            {
                Revision = ver[3];
            }
        }

        public GameVersion(Version version)
        {
            Major = version.Major;
            Minor = version.Minor;
            Build = version.Build;
        }

        public GameVersion(string version)
        {
            string[] ver = version.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (ver.Length is not (3 or 4))
            {
                throw new ArgumentException($"Version in the config.ini should be in \"x.x.x\" or \"x.x.x.x\" format! (current value: \"{version}\")");
            }

            if (!int.TryParse(ver[0], out Major)) throw new ArgumentException($"Major version is not a number! (current value: {ver[0]}");
            if (!int.TryParse(ver[1], out Minor)) throw new ArgumentException($"Minor version is not a number! (current value: {ver[1]}");
            if (!int.TryParse(ver[2], out Build)) throw new ArgumentException($"Build version is not a number! (current value: {ver[2]}");
            if (ver.Length != 4)
            {
                return;
            }

            if (!int.TryParse(ver[3], out Revision)) throw new ArgumentException($"Revision version is not a number! (current value: {ver[3]}");
        }

        public static bool TryParse(string version, out GameVersion? result)
        {
            result = null;
            Span<Range> ranges = stackalloc Range[8];
            ReadOnlySpan<char> versionSpan = version.AsSpan();
            int splitRanges = versionSpan.Split(ranges, '.', StringSplitOptions.TrimEntries);

            if (splitRanges is not (3 or 4)) return false;

            Span<int> versionSplits = stackalloc int[4];
            for (int i = 0; i < splitRanges; i++)
            {
                if (!int.TryParse(versionSpan[ranges[i]], null, out int versionParsed))
                    return false;

                versionSplits[i] = versionParsed;
            }

            result = new GameVersion(versionSplits);
            return true;
        }

        public bool IsMatch(string versionToCompare)
        {
            GameVersion parsed = new GameVersion(versionToCompare);
            return IsMatch(parsed);
        }

        public bool IsMatch(GameVersion? versionToCompare)
        {
            if (versionToCompare == null) return false;
            return Major == versionToCompare.Value.Major && Minor == versionToCompare.Value.Minor && Build == versionToCompare.Value.Build && Revision == versionToCompare.Value.Revision;
        }

        public GameVersion GetIncrementedVersion()
        {
            int nextMajor = Major;
            int nextMinor = Minor;

            nextMinor++;
            if (nextMinor < 10)
            {
                return new GameVersion([nextMajor, nextMinor, Build, Revision]);
            }

            nextMinor = 0;
            nextMajor++;

            return new GameVersion([nextMajor, nextMinor, Build, Revision]);
        }

        public          Version ToVersion() => new(Major, Minor, Build, Revision);
        public override string  ToString()  => $"{Major}.{Minor}.{Build}";

        public          string VersionStringManifest { get => string.Join('.', VersionArrayManifest); }
        public          string VersionString         { get => string.Join('.', VersionArray); }
        public          int[]  VersionArrayManifest  { get => [Major, Minor, Build, Revision]; }
        public          int[]  VersionArray          { get => [Major, Minor, Build]; }
        public readonly int    Major;
        public readonly int    Minor;
        public readonly int    Build;
        public readonly int    Revision;
    }

    public interface IAssetProperty
    {
        public string Name { get; }
        public string AssetTypeString { get; }
        public string Source { get; }
        public long Size { get; }
        public string SizeStr { get; }
        public string LocalCRC { get; }
        public byte[] LocalCRCByte { get; }
        public string RemoteCRC { get; }
        public byte[] RemoteCRCByte { get; }
    }

    public class AssetProperty<T>(
        string name,
        T      assetType,
        string source,
        long   size,
        byte[] localCrcByte,
        byte[] remoteCrcByte)
        : IAssetProperty
    {
        public string AssetTypeString { get => Enum.GetName(typeof(T), AssetType); }
        public string Name            { get; } = name;
        public T      AssetType       { get; } = assetType;
        public string Source          { get; } = '\\' + source;
        public long   Size            { get; } = size;
        public string SizeStr         { get => ConverterTool.SummarizeSizeSimple(Size); }
        public string LocalCRC        { get => LocalCRCByte == null ? "-" : HexTool.BytesToHexUnsafe(LocalCRCByte); }
        public byte[] LocalCRCByte    { get; } = localCrcByte;
        public string RemoteCRC       { get => RemoteCRCByte == null ? "-" : HexTool.BytesToHexUnsafe(RemoteCRCByte); }
        public byte[] RemoteCRCByte   { get; } = remoteCrcByte;

        public IAssetProperty ToIAssetProperty() => this;
    }
}
