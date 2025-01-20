using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.EncTool.Proto.StarRail;
using Microsoft.UI.Xaml;
using System;
using System.Text;

namespace CollapseLauncher.GameVersioning
{
    internal sealed class GameTypeStarRailVersion : GameVersionBase
    {
        #region Properties
        public SRMetadata StarRailMetadataTool { get; set; }
        #endregion

        public GameTypeStarRailVersion(UIElement parentUIElement, RegionResourceProp gameRegionProp, string gameName, string gameRegion)
            : base(parentUIElement, gameRegionProp, gameName, gameRegion)
        {
            // Try check for reinitializing game version.
            TryReinitializeGameVersion();

            // Initialize Star Rail metadata tool
            if (GamePreset.ProtoDispatchKey != null)
            {
                string dispatchUrlTemplate = ('.' + GamePreset.GameDispatchURLTemplate).AssociateGameAndLauncherId(
                    "channel_id",
                    "sub_channel_id",
                    $"{GamePreset.ChannelID}",
                    $"{GamePreset.SubChannelID}")?.TrimStart('.');
                string gatewayUrlTemplate = ('.' + GamePreset.GameGatewayURLTemplate).AssociateGameAndLauncherId(
                    "channel_id",
                    "sub_channel_id",
                    $"{GamePreset.ChannelID}",
                    $"{GamePreset.SubChannelID}")?.TrimStart('.');

                if (GamePreset.GameDispatchArrayURL == null)
                {
                    throw new NullReferenceException(GamePreset.GameDispatchArrayURL + " is null!");
                }
                StarRailMetadataTool = new SRMetadata(
                                                      GamePreset.GameDispatchArrayURL[0],
                                                      GamePreset.ProtoDispatchKey,
                                                      dispatchUrlTemplate,
                                                      gatewayUrlTemplate,
                                                      GamePreset.GameDispatchChannelName,
                                                      GameVersionAPI?.VersionString);
            }

            // Initialize Proto ID for static StarRailGateway
            InitializeProtoId();
        }

        ~GameTypeStarRailVersion() => StarRailMetadataTool?.Dispose();

        public override bool IsGameHasDeltaPatch() => GameDeltaPatchProp != null;

        public override DeltaPatchProperty GetDeltaPatchInfo() => GameDeltaPatchProp == null ? null : GameDeltaPatchProp;

#nullable enable
        public void InitializeProtoId()
        {
            if (GamePreset.GameDataTemplates != null && GamePreset.GameDataTemplates.Count != 0)
            {
                byte[]? data = GamePreset.GetGameDataTemplate("MagicSpell", [2, 3, 0, 0]);
                if (data == null)
                {
                    Logger.LogWriteLine("[IGameVersionCheck:InitializeProtoId] data is null!", LogType.Error, true);
                    return;
                }

                string jsonResponse = Encoding.UTF8.GetString(data);
                StarRailDispatchGatewayProps.Deserialize(jsonResponse);
            }
        }
    }
}
