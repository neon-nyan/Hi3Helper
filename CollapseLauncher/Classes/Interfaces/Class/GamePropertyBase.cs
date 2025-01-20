using CollapseLauncher.Extension;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Interfaces
{
    internal class GamePropertyBase<T1> : IAssetEntry
    {
        private string      GamePathField       { get; }
        private GameVersion GameVersionOverride { get; }

#nullable enable
        public GamePropertyBase(UIElement parentUI, IGameVersionCheck? gameVersionManager, IGameSettings? gameSettings, string? gamePath, string? gameRepoURL, string? versionOverride)
        {
            GameSettings = gameSettings;
            GameVersionManager = gameVersionManager;
            ParentUI = parentUI;
            GamePathField = gamePath;
            GameRepoURL = gameRepoURL;
            Token = new CancellationTokenSourceWrapper();
            IsVersionOverride = versionOverride != null;

            // If the version override is not null, then assign the override value
            if (IsVersionOverride)
            {
                GameVersionOverride = new GameVersion(versionOverride);
            }

            AssetEntry = [];
        }

        public GamePropertyBase(UIElement parentUI, IGameVersionCheck? gameVersionManager, string? gamePath, string? gameRepoURL, string? versionOverride)
            : this(parentUI, gameVersionManager, null, gamePath, gameRepoURL, versionOverride) { }
#nullable restore

        protected const int BufferLength = 4 << 10; // 4 KiB
        protected const int BufferMediumLength = 1 << 20; // 1 MiB
        protected const int BufferBigLength = 2 << 20; // 2 MiB
        protected const int SizeForMultiDownload = 10 << 20;
        protected const int DownloadThreadCountReserved = 16;
        protected virtual string UserAgent { get; set; } = "UnityPlayer/2017.4.18f1 (UnityWebRequest/1.0, libcurl/7.51.0-DEV)";

        protected bool IsVersionOverride { get; init; }
        protected bool IsBurstDownloadEnabled { get => IsBurstDownloadModeEnabled; }
        protected byte DownloadThreadCount { get => (byte)AppCurrentDownloadThread; }
        protected byte ThreadCount { get => (byte)AppCurrentThread; }
        protected int DownloadThreadCountSqrt { get => (int)Math.Max(Math.Sqrt(DownloadThreadCount), 4); }
        protected CancellationTokenSourceWrapper Token { get; set; }
        protected GameVersion GameVersion
        {
            get
            {
                if (GameVersionManager != null && IsVersionOverride)
                {
                    return GameVersionOverride;
                }
                return GameVersionManager?.GetGameExistingVersion() ?? throw new NullReferenceException();
            }
        }

        protected IGameVersionCheck GameVersionManager { get; set; }
        protected IGameSettings GameSettings { get; set; }
        protected string GamePath { get => string.IsNullOrEmpty(GamePathField) ? GameVersionManager.GameDirPath : GamePathField; }
        protected string GameRepoURL { get; set; }
        protected List<T1> AssetIndex { get; set; }
        protected bool UseFastMethod { get; set; }

        public ObservableCollection<IAssetProperty> AssetEntry { get; set; }
        public UIElement ParentUI { get; init; }
    }
}
