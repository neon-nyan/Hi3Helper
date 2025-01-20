using CollapseLauncher.InstallManager;
using CollapseLauncher.InstallManager.Base;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable UnusedMemberInSuper.Global
// ReSharper disable IdentifierTypo

namespace CollapseLauncher.Interfaces
{
    internal interface IGameInstallManager : IBackgroundActivity, IDisposable
    {
        ValueTask<int> GetInstallationPath(bool isHasOnlyMigrateOption = false);
        Task StartPackageDownload(bool skipDialog = false);
        ValueTask<int> StartPackageVerification(List<GameInstallPackage> gamePackage = null);
        Task StartPackageInstallation();
        void ApplyGameConfig(bool forceUpdateToLatest = false);

        ValueTask<bool> MoveGameLocation();
        ValueTask<bool> UninstallGame();
        void Flush();
        ValueTask<bool> IsPreloadCompleted(CancellationToken token = default);

        ValueTask<bool> TryShowFailedDeltaPatchState();
        ValueTask<bool> TryShowFailedGameConversionState();
        ValueTask CleanUpGameFiles(bool withDialog = true);

        void UpdateCompletenessStatus(CompletenessStatus status);

        bool StartAfterInstall { get; set; }
        bool IsUseSophon { get; }
        bool IsSophonInUpdateMode { get; }
    }
}
