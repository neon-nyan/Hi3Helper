using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo

namespace CollapseLauncher.Helper.Metadata
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(GeneralDataProp))]
    internal sealed partial class GeneralDataPropJsonContext : JsonSerializerContext;

    // WARNING!!!
    // This feature is only available for Genshin.
    [SuppressMessage("ReSharper", "RedundantDefaultMemberInitializer")]
    public class GeneralDataProp
    {
        public string deviceUUID              { get; set; } = "";
        public string userLocalDataVersionId  { get; set; } = "0.0.1";
        public int    deviceLanguageType      { get; set; } = 1;
        public int    deviceVoiceLanguageType { get; set; } = 2;

        [JsonPropertyName("selectedServerName")]
        public string _selectedServerName { get; set; } = "os_asia";

        [JsonIgnore]
        public ServerRegionID selectedServerName
        {
            get
            {
                string valueFromReg = _selectedServerName;
                // ReSharper disable once RedundantTypeArgumentsOfMethod
                if (!Enum.TryParse<ServerRegionID>(valueFromReg, true, out ServerRegionID result))
                {
                    return ServerRegionID.os_asia;
                }

                return result;
            }
            set => _selectedServerName = $"{value}";
        }

        public int          localLevelIndex                               { get; set; } = 0;
        public string       deviceID                                      { get; set; } = "";
        public string       targetUID                                     { get; set; } = "";
        public string       curAccountName                                { get; set; } = "";
        public string       uiSaveData                                    { get; set; } = "";
        public string       inputData                                     { get; set; } = "";
        public string       graphicsData                                  { get; set; } = "";
        public string       globalPerfData                                { get; set; } = "";
        public int          miniMapConfig                                 { get; set; } = 1;
        public bool         enableCameraSlope                             { get; set; } = true;
        public bool         enableCameraCombatLock                        { get; set; } = true;
        public bool         completionPkg                                 { get; set; } = false;
        public bool         completionPlayGoPkg                           { get; set; } = false;
        public bool         onlyPlayWithPSPlayer                          { get; set; } = false;
        public bool         needPlayGoFullPkgPatch                        { get; set; } = false;
        public bool         resinNotification                             { get; set; } = true;
        public bool         exploreNotification                           { get; set; } = true;
        public int          volumeGlobal                                  { get; set; } = 10;
        public int          volumeSFX                                     { get; set; } = 10;
        public int          volumeMusic                                   { get; set; } = 10;
        public int          volumeVoice                                   { get; set; } = 10;
        public int          audioAPI                                      { get; set; } = -1;
        public int          audioDynamicRange                             { get; set; } = 0;
        public int          audioOutput                                   { get; set; } = 1;
        public bool         _audioSuccessInit                             { get; set; } = true;
        public bool         enableAudioChangeAndroidMinimumBufferCapacity { get; set; } = true;
        public int          audioAndroidMiniumBufferCapacity              { get; set; } = 2 << 10;
        public bool         motionBlur                                    { get; set; } = true;
        public bool         gyroAiming                                    { get; set; } = false;
        public bool         firstHDRSetting                               { get; set; } = true;
        public double       maxLuminosity                                 { get; set; } = 0.0f;
        public double       uiPaperWhite                                  { get; set; } = 0.0f;
        public double       scenePaperWhite                               { get; set; } = 0.0f;
        public double       gammaValue                                    { get; set; } = 2.200000047683716;
        public List<string> _overrideControllerMapKeyList                 { get; set; } = [];
        public List<string> _overrideControllerMapValueList               { get; set; } = [];
        public bool         rewiredDisableKeyboard                        { get; set; } = false;
        public bool         rewiredEnableKeyboard                         { get; set; } = false;
        public bool         rewiredEnableEDS                              { get; set; } = false;
        public bool         disableRewiredDelayInit                       { get; set; } = false;
        public bool         disableRewiredInitProtection                  { get; set; } = false;
        public int          lastSeenPreDownloadTime                       { get; set; } = 0;
        public bool         enableEffectAssembleInEditor                  { get; set; } = true;
        public bool         forceDisableQuestResourceManagement           { get; set; } = false;
        public bool         needReportQuestResourceDeleteStatusFiles      { get; set; } = false;
        public bool         mtrCached                                     { get; set; } = true;
        public bool         mtrIsOpen                                     { get; set; } = true;
        public int          mtrMaxTTL                                     { get; set; } = 32;
        public int          mtrTimeOut                                    { get; set; } = 5000;
        public int          mtrTraceCount                                 { get; set; } = 5;
        public int          mtrAbortTimeOutCount                          { get; set; } = 3;
        public int          mtrAutoTraceInterval                          { get; set; } = 3600;
        public int          mtrTraceCDEachReason                          { get; set; } = 600;
        public int          mtrTimeInterval                               { get; set; } = 1000;
        public List<string> mtrBanReasons                                 { get; set; } = [];
        public List<string> _customDataKeyList                            { get; set; } = [];
        public List<string> _customDataValueList                          { get; set; } = [];
        public List<int>    _serializedCodeSwitches                       { get; set; } = [];
        public bool         urlCheckCached                                { get; set; } = false;
        public bool         urlCheckIsOpen                                { get; set; } = false;
        public bool         urlCheckAllIP                                 { get; set; } = false;
        public int          urlCheckTimeOut                               { get; set; } = 5000;
        public int          urlCheckSueecssTraceCount                     { get; set; } = 5;
        public int          urlCheckErrorTraceCount                       { get; set; } = 30;
        public int          urlCheckAbortTimeOutCount                     { get; set; } = 3;
        public int          urlCheckTimeInterval                          { get; set; } = 1000;
        public int          urlCheckCDEachReason                          { get; set; } = 600;
        public List<string> urlCheckBanReasons                            { get; set; } = [];
        public bool         mtrUseOldWinVersion                           { get; set; } = false;
    }
}