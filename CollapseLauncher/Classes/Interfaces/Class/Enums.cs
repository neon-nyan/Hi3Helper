// ReSharper disable InconsistentNaming
namespace CollapseLauncher
{
    internal enum RepairAssetType
    {
        Generic,
        Block,
        BlockUpdate,
        Audio,
        AudioUpdate,
        Video,
        Chunk,

        Unused
    }

    internal enum CacheAssetType
    {
        Data,
        Event,
        AI,
        Unused,
        Dispatcher,
        Gateway,

        General,    // Additional for HSR
        IFix,       // Additional for HSR
        DesignData, // Additional for HSR
        Lua         // Additional for HSR
    }

    internal enum CacheAssetStatus
    {
        New,
        Obsolete,
        Unused
    }

    internal enum GameInstallPackageType
    {
        General,
        Audio,
        Plugin,
        Utilities
    }
}
