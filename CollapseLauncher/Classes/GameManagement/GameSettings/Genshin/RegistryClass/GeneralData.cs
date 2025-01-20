using CollapseLauncher.GameSettings.Genshin.Context;
using Hi3Helper;
using Hi3Helper.EncTool;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable PartialTypeWithSinglePart

#pragma warning disable CS0659
namespace CollapseLauncher.GameSettings.Genshin
{
    internal sealed partial class GeneralData
    {
        #region Fields
        private const string _ValueName = "GENERAL_DATA_h2389025596";
        
        // Default GENERAL_DATA_h2389025596 value taken from version 5.0.0
        // For the next person, if there is any addition/deletion to the GeneralData props, please update this :)
        // Just delete the key from registry (back it up first) then run the game. After server selection is set then you will see this registry appear. 
        // Open Collapse Genshin GSP in debug mode and you shall see the raw value. Paste them here.
        // Thank you.
        // Sincerely, bagel 🥯
        // (hoyo why you did this to us)
        private static readonly string _generalDataDefault = 
            "{\"deviceUUID\":\"\",\"userLocalDataVersionId\":\"\",\"deviceLanguageType\":1,\"deviceVoiceLanguageType\":1,\"selectedServerName\":\"\",\"localLevelIndex\":0,\"deviceID\":\"\",\"targetUID\":\"\",\"curAccountName\":\"\",\"uiSaveData\":\"\",\"inputData\":\"{\\\"scriptVersion\\\":\\\"OSRELWin5.0.0\\\",\\\"mouseSensitivity\\\":10.0,\\\"joypadSenseIndex\\\":2,\\\"joypadFocusSenseIndex\\\":2,\\\"joypadInvertCameraX\\\":false,\\\"joypadInvertCameraY\\\":false,\\\"joypadInvertFocusCameraX\\\":false,\\\"joypadInvertFocusCameraY\\\":false,\\\"mouseSenseIndex\\\":2,\\\"mouseFocusSenseIndex\\\":2,\\\"touchpadSenseIndex\\\":2,\\\"touchpadFocusSenseIndex\\\":5,\\\"enableTouchpadFocusAcceleration\\\":false,\\\"lastJoypadDefaultScale\\\":1.0,\\\"lastJoypadFocusScale\\\":1.0,\\\"lastPCDefaultScale\\\":0.75,\\\"lastPCFocusScale\\\":1.0,\\\"lastTouchDefaultScale\\\":1.0,\\\"lastTouchFcousScale\\\":1.0,\\\"switchWalkRunByBtn\\\":false,\\\"skiffCameraAutoFix\\\":true,\\\"skiffCameraAutoFixInCombat\\\":false,\\\"cameraDistanceRatio\\\":0.0,\\\"wwiseVibration\\\":true,\\\"isYInited\\\":true,\\\"joypadSenseIndexY\\\":2,\\\"joypadFocusSenseIndexY\\\":2,\\\"mouseSenseIndexY\\\":2,\\\"mouseFocusSenseIndexY\\\":2,\\\"touchpadSenseIndexY\\\":2,\\\"touchpadFocusSenseIndexY\\\":5,\\\"lastJoypadDefaultScaleY\\\":1.0,\\\"lastJoypadFocusScaleY\\\":1.0,\\\"lastPCDefaultScaleY\\\":0.75,\\\"lastPCFocusScaleY\\\":1.0,\\\"lastTouchDefaultScaleY\\\":1.0,\\\"lastTouchFcousScaleY\\\":1.0}\",\"graphicsData\":\"{\\\"currentVolatielGrade\\\":4,\\\"customVolatileGrades\\\":[],\\\"volatileVersion\\\":\\\"OSRELWin5.0.0\\\"}\",\"globalPerfData\":\"{\\\"saveItems\\\":[],\\\"truePortedFromGraphicData\\\":true,\\\"portedVersion\\\":\\\"OSRELWin5.0.0\\\",\\\"volatileUpgradeVersion\\\":0,\\\"portedFromGraphicData\\\":false}\",\"miniMapConfig\":1,\"enableCameraSlope\":true,\"enableCameraCombatLock\":true,\"completionPkg\":false,\"completionPlayGoPkg\":false,\"onlyPlayWithPSPlayer\":false,\"needPlayGoFullPkgPatch\":false,\"resinNotification\":true,\"exploreNotification\":true,\"volumeGlobal\":10,\"volumeSFX\":10,\"volumeMusic\":10,\"volumeVoice\":10,\"audioAPI\":-1,\"audioDynamicRange\":0,\"audioOutput\":0,\"_audioSuccessInit\":true,\"enableAudioChangeAndroidMinimumBufferCapacity\":true,\"audioAndroidMiniumBufferCapacity\":2048,\"vibrationLevel\":0,\"vibrationIntensity\":3,\"usingNewVibrationSetting\":true,\"motionBlur\":true,\"gyroAiming\":false,\"gyroHorMoveSpeedIndex\":2,\"gyroVerMoveSpeedIndex\":2,\"gyroHorReverse\":false,\"gyroVerReverse\":false,\"gyroRotateType\":0,\"gyroExcludeRightStickVerInput\":false,\"firstHDRSetting\":true,\"maxLuminosity\":0.0,\"uiPaperWhite\":0.0,\"scenePaperWhite\":0.0,\"gammaValue\":2.200000047683716,\"enableHDR\":false,\"_overrideControllerMapKeyList\":[],\"_overrideControllerMapValueList\":[],\"rewiredMapMigrateRecord\":[],\"rewiredDisableKeyboard\":false,\"rewiredEnableKeyboard\":false,\"rewiredEnableEDS\":false,\"disableRewiredDelayInit\":false,\"disableRewiredInitProtection\":false,\"conflictKeyBindingElementId\":[],\"conflictKeyBindingActionId\":[],\"lastSeenPreDownloadTime\":0,\"lastSeenSettingResourceTabScriptVersion\":\"\",\"enableEffectAssembleInEditor\":true,\"forceDisableQuestResourceManagement\":false,\"needReportQuestResourceDeleteStatusFiles\":false,\"disableTeamPageBackgroundSwitch\":false,\"disableHttpDns\":false,\"mtrCached\":false,\"mtrIsOpen\":false,\"mtrMaxTTL\":32,\"mtrTimeOut\":5000,\"mtrTraceCount\":5,\"mtrAbortTimeOutCount\":3,\"mtrAutoTraceInterval\":0,\"mtrTraceCDEachReason\":600,\"mtrTimeInterval\":1000,\"mtrBanReasons\":[],\"_customDataKeyList\":[],\"_customDataValueList\":[],\"_serializedCodeSwitches\":[],\"urlCheckCached\":false,\"urlCheckIsOpen\":false,\"urlCheckAllIP\":false,\"urlCheckTimeOut\":5000,\"urlCheckSueecssTraceCount\":5,\"urlCheckErrorTraceCount\":30,\"urlCheckAbortTimeOutCount\":3,\"urlCheckTimeInterval\":1000,\"urlCheckCDEachReason\":600,\"urlCheckBanReasons\":[],\"mtrUseOldWinVersion\":false,\"greyTestDeviceUniqueId\":\"\",\"muteAudioOnAppMinimized\":false,\"disableFallbackControllerType\":false,\"lastShowDoorProgress\":-1.0,\"globalPerfSettingVersion\":2}";
        #endregion

        #region Properties
        //in need of help,,
        //Using guide from https://github.com/Myp3a/GenshinConfigurator/wiki/Config-format
        //Thanks Myp3a!

        public string deviceUUID { get; set; } = "";
        public string userLocalDataVersionId { get; set; } = "";

        /// <summary>
        /// This define "<c>Text Language</c>" in-game settings. <br/>
        /// Valid values: 1-13 
        /// Default: 1
        /// English(1)
        /// Simplified Chinese(2)
        /// Traditional Chinese(3)
        /// French(4)
        /// German(5)
        /// Spanish(6)
        /// Portugese(7)
        /// Russian(8)
        /// Japanese(9)
        /// Korean(10)
        /// Thai(11)
        /// Vietnamese(12)
        /// Indonesian(13)
        /// Turkish(14)
        /// Italy(15)
        /// </summary>
        public int deviceLanguageType { get; set; } = 1;

        /// <summary>
        /// This define "<c>Voice Language</c>" in-game settings. <br/>
        /// Valid values: 0-3
        /// Default: 1
        /// Chinese(0)
        /// English(1)
        /// Japanese(2)
        /// Korean(3)
        /// </summary>
        public int deviceVoiceLanguageType { get; set; } = 1;

        /// <summary>
        /// This define "<c>Server Name</c>" last selected in loading menu. <br/>
        /// </summary>
        public string selectedServerName { get; set; } = "os_usa";

        public int localLevelIndex { get; set; } = 0;
        public string deviceID { get; set; } = "";
        public string targetUID { get; set; } = "";
        public string curAccountName { get; set; } = "";

        /// <summary>
        /// This define "<c>Resolution</c>" index selected in-game. <br/>
        /// Valid value: 0-?
        /// Default: 0
        /// </summary>
        public string uiSaveData { get; set; }

        /// <summary>
        /// This holds settings for input sens and stuff.<br/>
        /// Please look at https://github.com/Myp3a/GenshinConfigurator/wiki/Config-format#input-data-format
        /// </summary>
        public string inputData { get; set; }

        /// <summary>
        /// This holds settings that holds Graphics Settings settings. YEP!<br/>
        /// Needs to be serialized again with its keys and values in dictionary<br/>
        /// https://github.com/Myp3a/GenshinConfigurator/wiki/Config-format#graphics-data-format
        /// </summary>
        [JsonPropertyName("graphicsData")]
        public string _graphicsData { get; set; }

        [JsonIgnore]
        public GraphicsData graphicsData { get; set; }

        /// <summary>
        /// This is a dict that keeps track of graphics settings changes.<br/>
        /// Always port graphicsData to this also, if not then settings are not applied.
        /// </summary>
        [JsonPropertyName("globalPerfData")]
        public string _globalPerfData { get; set; }

        [JsonIgnore]
        public GlobalPerfData globalPerfData { get; set; }

        public int miniMapConfig { get; set; } = 1;

        /// <summary>
        /// This  defines "<c>Automatic View Height</c>" in-game settings.<br/>
        /// Default: true
        /// </summary>
        public bool enableCameraSlope { get; set; } = true;

        public bool enableCameraCombatLock { get; set; } = true;
        public bool completionPkg { get; set; } = false;
        public bool completionPlayGoPkg { get; set; } = false;
        public bool onlyPlayWithPSPlayer { get; set; } = false;
        public bool needPlayGoFullPkgPatch { get; set; } = false;
        public bool resinNotification { get; set; } = true;
        public bool exploreNotification { get; set; } = true;

        /// <summary>
        /// This define "<c>Global Volume</c>" slider in-game.<br/>
        /// Valid values: 0-10
        /// Default: 10
        /// </summary>
        public int volumeGlobal { get; set; } = 10;

        /// <summary>
        /// This define "<c>SFX Volume</c>" slider in-game.<br/>
        /// Valid values: 0-10
        /// Default: 10
        /// </summary>
        public int volumeSFX { get; set; } = 10;

        /// <summary>
        /// This define "<c>Music Volume</c>" slider in-game.<br/>
        /// Valid values: 0-10
        /// Default: 10
        /// </summary>
        public int volumeMusic { get; set; } = 10;

        /// <summary>
        /// This define "<c>Voice Volume</c>" slider in-game.<br/>
        /// Valid values: 0-10
        /// Default: 10
        /// </summary>
        public int volumeVoice { get; set; } = 10;

        /// <summary>
        /// Probably just leave it alone...
        /// </summary>
        public int audioAPI { get; set; } = -1;

        /// <summary>
        /// This defines "<c>Dynamic Range</c>" audio combo box in-game.
        /// Valid values: 0 (full), 1 (limited)
        /// Default: 0
        /// </summary>
        public int audioDynamicRange { get; set; } = 0;

        /// <summary>
        /// This defines "<c>Audio Output</c>" combo box in-game.
        /// Valid Values: 0 (stereo), 1 (surround)
        /// Default: 0
        /// </summary>
        public int audioOutput { get; set; } = 0;

        public bool _audioSuccessInit { get; set; } = true;
        public bool enableAudioChangeAndroidMinimumBufferCapacity { get; set; } = true;
        public int audioAndroidMiniumBufferCapacity { get; set; } = 2048;

        /// <summary>
        /// This define vibration level for certain controller probably ?
        /// Valid Values: 0 (Full), 1 (Partial), 2 (Off)
        /// Default: 0
        /// </summary>
        public int vibrationLevel { get; set; } = 0;

        /// <summary>
        /// This defines "<c>Vibration Intensity</c>" slider in-game. <br/>
        /// Valid Values: 1-5
        /// Default: 5
        /// </summary>
        public int vibrationIntensity { get; set; } = 5;

        public bool usingNewVibrationSetting { get; set; } = true;

        /// <summary>
        /// Is not an actual blur setting, look at GraphicsData for the real deal.
        /// </summary>
        public bool motionBlur { get; set; } = true;

        /// <summary>
        /// Gyro Aiming controls, used for those who use controller that has Gyro control.
        /// </summary>
        public bool gyroAiming { get; set; } = false;
        public int gyroHorMoveSpeedIndex { get; set; } = 2;
        public int gyroVerMoveSpeedIndex { get; set; } = 2;
        public bool gyroHorReverse { get; set; } = false;
        public bool gyroVerReverse { get; set; } = false;
        public int gyroRotateType { get; set; } = 0;
        public bool gyroExcludeRightStickVerInput { get; set; } = false;

        //unsure what these does, probably HDR stuff? doesn't have HDR monitor to test...
        //also doesn't seem to be tied into any actual game settings as Genshin doesn't have a native HDR

        /// <summary>
        /// This controls if HDR first-time Wizard in-game will be shown when game first starts. <br/>
        /// is a boolean, Default: true <br/>
        /// Reset this every time HDR settings is toggled on.
        /// </summary>
        public bool firstHDRSetting { get; set; } = true;

        /// <summary>
        /// This controls "<c>Adjust Display Brightness</c>" slider on "Adjust Brightness" settings screen with white logo. Controls maximum Luminosity allowed in nits.<br/>
        /// Accepted values: 300.0m-2000.0m 
        /// Default : 300.0m
        /// </summary>
        public decimal maxLuminosity { get; set; } = 300.0m;

        /// <summary>
        /// This controls "<c>Display Brightness</c>" slider in "Adjust Brightness" settings screen with scenery. Controls UI brightness.<br/>
        /// Accepted values: 150.0m-550.0m
        /// Default : 175.0m
        /// </summary>
        public decimal uiPaperWhite { get; set; } = 175.0m;

        /// <summary>
        /// This controls "<c>Scenery Brightness</c>" slider in "Adjust Brightness" settings screen with scenery. Controls overall scenery brightness.<br/>
        /// Accepted values : 100.0m-500.0m
        /// Default : 200.0m
        /// </summary>
        public decimal scenePaperWhite { get; set; } = 200.0m;

        /// <summary>
        /// This defines "<c>Gamma</c>" slider in-game. <br/>
        /// Accepted values : 1.4f - 3.0f <br/>
        /// <c>WARNING:</c> Value is inverted
        /// </summary>
        public double gammaValue { get; set; } = 2.2f;

        /// <summary>
        /// This controls if HDR is enabled or not. <br/>
        /// is a boolean, Default: false <br/>
        /// </summary>
        public bool enableHDR { get; set; } = false;

        /// <summary>
        /// This holds value for controllers input that has been customized.
        /// </summary>
        public List<string> _overrideControllerMapKeyList { get; set; }
        
        // unused field
        public List<string> rewiredMapMigrateRecord { get; set; }

        /// <summary>
        /// This holds controller mapping override for controller ID in _overrideControllerMapKeyList.
        /// </summary>
        // Temporary for fallback before the implementation is made
        public List<string> _overrideControllerMapValueList { get; set; }

        //[JsonPropertyName("_overrideControllerMapValueList")]
        //public List<string> __overrideControllerMapValueList { get; set; }

        //[JsonIgnore]
        //public Controllers _overrideControllerMapValueList { get; set; }

        public bool         rewiredDisableKeyboard                   { get; set; } = false;
        public bool         rewiredEnableKeyboard                    { get; set; } = false;
        public bool         rewiredEnableEDS                         { get; set; } = false;
        public bool         disableRewiredDelayInit                  { get; set; } = false;
        public bool         disableRewiredInitProtection             { get; set; } = false;
        public List<object> conflictKeyBindingElementId              { get; set; }
        public List<object> conflictKeyBindingActionId               { get; set; }
        public int          lastSeenPreDownloadTime                  { get; set; } = 0;
        public string       lastSeenSettingResourceTabScriptVersion  { get; set; } = "";
        public bool         enableEffectAssembleInEditor             { get; set; } = true;
        public bool         forceDisableQuestResourceManagement      { get; set; } = false;
        public bool         needReportQuestResourceDeleteStatusFiles { get; set; } = false;

        /// <summary>
        /// This defines "<c>The background of the Party Setup screen will change based on your current region</c>" in-game combo box <br/>
        /// Default: true
        /// </summary>
        public bool disableTeamPageBackgroundSwitch { get; set; } = false;

        public bool         disableHttpDns            { get; set; } = false;
        public bool         mtrCached                 { get; set; } = false;
        public bool         mtrIsOpen                 { get; set; } = true;
        public int          mtrMaxTTL                 { get; set; } = 32;
        public int          mtrTimeOut                { get; set; } = 5000;
        public int          mtrTraceCount             { get; set; } = 5;
        public int          mtrAbortTimeOutCount      { get; set; } = 3;
        public int          mtrAutoTraceInterval      { get; set; } = 0;
        public int          mtrTraceCDEachReason      { get; set; } = 600;
        public int          mtrTimeInterval           { get; set; } = 1000;
        public List<object> mtrBanReasons             { get; set; }
        public List<string> _customDataKeyList        { get; set; }
        public List<string> _customDataValueList      { get; set; }
        public List<int>    _serializedCodeSwitches   { get; set; }
        public bool         urlCheckCached            { get; set; } = false;
        public bool         urlCheckIsOpen            { get; set; } = false;
        public bool         urlCheckAllIP             { get; set; } = false;
        public int          urlCheckTimeOut           { get; set; } = 5000;
        public int          urlCheckSueecssTraceCount { get; set; } = 5;
        public int          urlCheckErrorTraceCount   { get; set; } = 30;
        public int          urlCheckAbortTimeOutCount { get; set; } = 3;
        public int          urlCheckTimeInterval      { get; set; } = 1000;
        public int          urlCheckCDEachReason      { get; set; } = 600;
        public List<object> urlCheckBanReasons        { get; set; }
        public bool         mtrUseOldWinVersion       { get; set; } = false;
        public string       greyTestDeviceUniqueId    { get; set; } = "";

        /// <summary>
        /// This controls if game audio should be disabled when main window is minimized. <br/>
        /// is a boolean, Default: false <br/>
        /// </summary>
        public bool muteAudioOnAppMinimized { get; set; } = false;

        // iunno
        public bool   disableFallbackControllerType { get; set; } = false;
        public double lastShowDoorProgress          { get; set; } = -1.0;
        public int    globalPerfSettingVersion      { get; set; } = 2;
        #endregion

        #region Methods
#nullable enable
        public static GeneralData Load()
        {
            try
            {
                if (RegistryRoot == null) throw new ArgumentNullException($"Cannot load {_ValueName} since RegistryKey is unexpectedly not initialized!");
                object value = RegistryRoot.GetValue(_ValueName) ?? throw new ArgumentNullException($"Cannot find registry key {_ValueName}");

                ReadOnlySpan<byte> byteStr = (byte[])value;
#if DUMPGIJSON
                // Dump GeneralData as raw string
                LogWriteLine($"RAW Genshin Settings: {_ValueName}\r\n" +
                             $"{Encoding.UTF8.GetString(byteStr.TrimEnd((byte)0))}", LogType.Debug, true);

                // Dump GeneralData as indented JSON output using GeneralData properties
                LogWriteLine($"Deserialized Genshin Settings: {_ValueName}\r\n{byteStr
                    .Deserialize(GenshinSettingsJsonContext.Default.GeneralData)
                    .Serialize(GenshinSettingsJsonContext.Default.GeneralData, false, true)}", LogType.Debug, true);
#endif
#if DEBUG
                LogWriteLine($"Loaded Genshin Settings: {_ValueName}", LogType.Debug, true);
#else
                LogWriteLine($"Loaded Genshin Settings", LogType.Default, true);
#endif
                GeneralData data = byteStr.Deserialize(GenshinSettingsJsonContext.Default.GeneralData) ?? new GeneralData();

                if (data._graphicsData != null) data.graphicsData = GraphicsData.Load(data._graphicsData);
                if (data._globalPerfData != null) data.globalPerfData   = GlobalPerfData.Load(data._globalPerfData, data.graphicsData)!;
                return data;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while reading {_ValueName}, Default fallback data is used!" +
                             $"\r\n  Please open the game and change any settings, then close normally. After that you can use this feature." +
                             $"\r\n  If the issue persist, please report it on GitHub" +
                             $"\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception(
                    $"Failed when reading game settings {_ValueName}!\r\nFallback default value is used.\r\n\r\n" +
                    $"Unless you have never opened the game (fresh installation), please open the game and change any settings, then safely close the game. If the problem persist, report the issue on our GitHub\r\n\r\n" +
                    $"{ex}", ex));

                GeneralData data = _generalDataDefault.Deserialize(GenshinSettingsJsonContext.Default.GeneralData) ?? new GeneralData();
                data.graphicsData   = GraphicsData.Load(data._graphicsData!);
                data.globalPerfData = GlobalPerfData.Load(data._globalPerfData!, data.graphicsData)!;
                return data;
            }
        }

        public void Save()
        {
            try
            {
                if (RegistryRoot == null) throw new NullReferenceException($"Cannot save {_ValueName} since RegistryKey is unexpectedly not initialized!");

                _graphicsData = graphicsData.Create(globalPerfData);
                _globalPerfData = globalPerfData.Save();

                string data = this.Serialize(GenshinSettingsJsonContext.Default.GeneralData);
                byte[] dataByte = Encoding.UTF8.GetBytes(data);

                RegistryRoot.SetValue(_ValueName, dataByte, RegistryValueKind.Binary);
#if DUMPGIJSON
                //Dump saved GeneralData JSON from Collapse as indented output
                LogWriteLine($"Saved Genshin Settings: {_ValueName}\r\n{this.Serialize(GenshinSettingsJsonContext.Default.GeneralData, false, true)}", LogType.Debug, true);
#endif
#if DEBUG
                LogWriteLine($"Saved Genshin Settings: {_ValueName}" +
                    $"\r\n      Text Language        : {deviceLanguageType}" +
                    $"\r\n      VO Language          : {deviceVoiceLanguageType}" +
                    $"\r\n      Audio - Master Volume: {volumeGlobal}" +
                    $"\r\n      Audio - Music Volume : {volumeMusic}" +
                    $"\r\n      Audio - SFX Volume   : {volumeSFX}" +
                    $"\r\n      Audio - Voice Volume : {volumeVoice}" +
                    $"\r\n      Audio - Dynamic Range: {audioDynamicRange}" +
                    $"\r\n      Audio - Surround     : {audioOutput}" +
                    $"\r\n      Gamma                : {gammaValue}" +
                    $"\r\n      HDR - MaxLuminosity  : {maxLuminosity}" +
                    $"\r\n      HDR - UIPaperWhite   : {uiPaperWhite}" +
                    $"\r\n      HDR - ScenePaperWhite: {scenePaperWhite}", LogType.Debug);
#else
                LogWriteLine($"Saved Genshin Game Settings", LogType.Default, true);
#endif
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed to save {_ValueName}!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception("Failed to save Genshin settings, please report them to GitHub issues!", ex));
            }
        }

        public override bool Equals(object? comparedTo) => comparedTo is GeneralData generalData && TypeExtensions.IsInstancePropertyEqual(this, generalData);
        #endregion
    }
}
