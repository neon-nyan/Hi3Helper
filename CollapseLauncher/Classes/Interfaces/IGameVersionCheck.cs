using CollapseLauncher.GameVersioning;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Threading.Tasks;
// ReSharper disable CommentTypo
// ReSharper disable GrammarMistakeInComment
// ReSharper disable IdentifierTypo

namespace CollapseLauncher.Interfaces
{
    internal static class GameVersionCheckExtension
    {
        /// <summary>
        /// Casting the IGameVersionCheck as its origin or another version class.
        /// </summary>
        /// <typeparam name="TCast">The type of version class to cast</typeparam>
        /// <returns>The version class to get casted</returns>
        internal static TCast CastAs<TCast>(this IGameVersionCheck versionCheck)
            where TCast : GameVersionBase, IGameVersionCheck => (TCast)versionCheck;
    }

    internal interface IGameVersionCheck
    {
        /// <summary>
        /// Get the game name
        /// </summary>
        string GameName { get; }

        /// <summary>
        /// Get the region name of the game
        /// </summary>
        string GameRegion { get; }

#nullable enable
        /// <summary>
        /// Get the version section of the game INI's configuration
        /// </summary>
        IniSection? GameIniVersionSection { get; }

        /// <summary>
        /// Get the profile section of the game INI's configuration
        /// </summary>
        IniSection? GameIniProfileSection { get; }
#nullable restore

        /// <summary>
        /// Get the base of the instance
        /// </summary>
        GameVersionBase AsVersionBase { get; }

        /// <summary>
        /// Returns or sets the path of the game.<br/>
        /// If you set the value from this property, the value won't be saved until you execute <c>UpdateGamePath()</c>
        /// </summary>
        string GameDirPath { get; set; }

        /// <summary>
        /// Returns the app data path of the game
        /// </summary>
        string GameDirAppDataPath { get; }

        /// <summary>
        /// Returns or sets the game preset
        /// </summary>
        PresetConfig GamePreset { get; }

        /// <summary>
        /// Returns or set the API properties
        /// </summary>
        RegionResourceProp GameApiProp { get; set; }

        /// <summary>
        /// Returns the type of the game
        /// </summary>
        GameNameType GameType { get; }

        /// <summary>
        /// Returns the name of the engine output log file
        /// </summary>
        string GameOutputLogName { get; }

        /// <summary>
        /// Returns the game vendor type property and the game name based on <c>app.info</c> file
        /// </summary>
        GameVendorProp VendorTypeProp { get; }

        /// <summary>
        /// Returns the current version of the game as provided by miHoYo's API.
        /// </summary>
        /// <returns>The current version of the game</returns>
        GameVersion? GetGameVersionApi();

        /// <summary>
        /// Returns the preload version of the game as provided by miHoYo's API.
        /// </summary>
        /// <returns>The preload version of the game</returns>
        GameVersion? GetGameVersionApiPreload();

        /// <summary>
        /// Returns the version of the game installed.<br/>
        /// It will return a <c>null</c> if the game doesn't installed.
        /// </summary>
        /// <returns>The version of the game installed</returns>
        GameVersion? GetGameExistingVersion();

        /// <summary>
        /// Checks if the game version is installed or matches the version provided from miHoYo's API.
        /// </summary>
        bool IsGameVersionMatch();

        /// <summary>
        /// Checks if the plugin version is installed or matches the version provided from miHoYo's API.
        /// </summary>
        ValueTask<bool> IsPluginVersionsMatch();

        /// <summary>
        /// Checks if the sdk version is installed or matches the version provided from miHoYo's API.
        /// This is used to obtain the status of the SDK .dlls for certain builds (for example: Bilibili version)
        /// </summary>
        ValueTask<bool> IsSdkVersionsMatch();

        /// <summary>
        /// Check if the game version is installed.
        /// </summary>
        bool IsGameInstalled();

        /// <summary>
        /// Check if the game has a pre-load.
        /// </summary>
        bool IsGameHasPreload();

        /// <summary>
        /// Check if the game has a delta-patch. (For Honkai only)
        /// </summary>
        bool IsGameHasDeltaPatch();

        /// <summary>
        /// Returns the state of the game.
        /// </summary>
        ValueTask<GameInstallStateEnum> GetGameState();

        /// <summary>
        /// Returns the Delta-patch file property.
        /// If the Delta-patch file doesn't exist, then it will return a null.<br/><br/>
        /// This method is only available for Honkai.
        /// </summary>
        DeltaPatchProperty GetDeltaPatchInfo();

        /// <summary>
        /// Returns the <c>List</c> of the Resource Version for the Latest Zip based on the game state
        /// </summary>
        /// <param name="gameState">The state of the game</param>
        List<RegionResourceVersion> GetGameLatestZip(GameInstallStateEnum gameState);

        #nullable enable
        /// <summary>
        /// Returns the <c>List</c> of the Resource Version for the Pre-load Zip.
        /// If the Pre-load doesn't exist, then it will return a null.
        /// </summary>
        List<RegionResourceVersion>? GetGamePreloadZip();
        
        /// <summary>
        /// Returns the <c>List</c> of the Resource Version for the Plugins
        /// </summary>
        List<RegionResourcePlugin>? GetGamePluginZip();

        /// <summary>
        /// Returns the <c>List</c> of the Resource Version for the SDKs
        /// </summary>
        List<RegionResourcePlugin>? GetGameSdkZip();

        /// <summary>
        /// Ensure the validity of the game's config.ini file.
        /// This method also being used to check + prompt the user to whether
        /// repair the game's config.ini or ignore it.
        /// </summary>
        /// <param name="uiParentElement">The parent UI element where the dialog will be spawned (if needed)</param>
        /// <returns><c>True</c> means the values within config.ini is valid or action is cancelled. <c>False</c> means config.ini has been repaired or changed.</returns>
        ValueTask<bool> EnsureGameConfigIniCorrectiveness(UIElement uiParentElement);

        /// <summary>
        /// Try to find game installation path from the given path.
        /// If it returns null, then there's no game installation found.
        /// </summary>
        string? FindGameInstallationPath(string path);
#nullable restore

        /// <summary>
        /// Update the location of the game folder and also save it to the Game Profile's Ini file.
        /// </summary>
        /// <param name="path">The path of the game folder</param>
        /// <param name="saveValue">Save the config file</param>
        void UpdateGamePath(string path, bool saveValue = true);

        /// <summary>
        /// Update the version of the game to the latest one provided by miHoYo's API and also save it to the Game Profile's Ini file.
        /// </summary>
        /// <param name="saveValue">Save the config file</param>
        void UpdateGameVersionToLatest(bool saveValue = true);

        /// <summary>
        /// Update the version of the game to the given value and also save it to the Game Profile's Ini file.
        /// </summary>
        /// <param name="version">The version to change</param>
        /// <param name="saveValue">Save the config file</param>
        void UpdateGameVersion(GameVersion? version, bool saveValue = true);

        /// <summary>
        /// Update the game channel and save it to the config.
        /// </summary>
        /// <param name="saveValue">Save the config file</param>
        void UpdateGameChannels(bool saveValue = true);

        /// <summary>
        /// Update the game plugin versions and save it to the config.
        /// </summary>
        /// <param name="versions">The dictionary collection of the plugins</param>
        /// <param name="saveValue">Save the config file</param>
        void UpdatePluginVersions(Dictionary<string, GameVersion> versions, bool saveValue = true);

        /// <summary>
        /// Update the game SDK version and save it to the config.
        /// </summary>
        /// <param name="version">The version of the SDK</param>
        /// <param name="saveValue">Save the config file</param>
        void UpdateSdkVersion(GameVersion? version, bool saveValue = true);

        /// <summary>
        /// Reinitialize the game version configs, including the INIs.
        /// </summary>
        void Reinitialize();
    }
}
