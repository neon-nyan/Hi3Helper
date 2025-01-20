using Microsoft.UI.Xaml;
using System;

namespace CollapseLauncher.GameVersioning
{
    // ReSharper disable once RedundantExtendsListEntry
    internal sealed class GameTypeHonkaiVersion : GameVersionBase
    {
        #region Statics
        private static readonly Version senadinaVersion = new(7, 3, 0);
        #endregion

        #region Public properties
        public bool IsCurrentSenadinaVersion { get => GameVersionAPI?.ToVersion() >= senadinaVersion; }
        public bool IsPreloadSenadinaVersion { get => GameVersionAPIPreload.HasValue ? GameVersionAPIPreload.Value.ToVersion() >= senadinaVersion : false; }
        #endregion

        public GameTypeHonkaiVersion(UIElement parentUIElement, RegionResourceProp gameRegionProp, string gameName, string gameRegion)
            : base(parentUIElement, gameRegionProp, gameName, gameRegion)
        {
            // Try check for reinitializing game version from XMF file.
            TryReinitializeGameVersion();
        }

        public override bool IsGameHasDeltaPatch() => GameDeltaPatchProp != null;

        public override DeltaPatchProperty GetDeltaPatchInfo() => GameDeltaPatchProp == null ? null : GameDeltaPatchProp;

        public override void Reinitialize()
        {
            // Do base reinitialization first
            base.Reinitialize();

            // Then try Reinitialize game version provided by XMF
            TryReinitializeGameVersion();
        }
    }
}
