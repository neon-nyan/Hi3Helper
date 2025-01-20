using System;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Interfaces
{
    internal interface IGameSettings : IGameSettingsUniversal
    {
        Task<Exception?> ImportSettings(string? gameBasePath = null);
        Task<Exception?> ExportSettings(bool isCompressed = true, string? parentPathToImport = null, string[]? relativePathToImport = null);
        void InitializeSettings();
        void ReloadSettings();
        void SaveSettings();
        IGameSettingsUniversal AsIGameSettingsUniversal();
    }
}
