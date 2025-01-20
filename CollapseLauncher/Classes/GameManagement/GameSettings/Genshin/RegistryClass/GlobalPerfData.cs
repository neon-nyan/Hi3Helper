using CollapseLauncher.GameSettings.Genshin.Context;
using CollapseLauncher.GameSettings.Genshin.Enums;
using Hi3Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using static Hi3Helper.Logger;
// ReSharper disable RedundantDefaultMemberInitializer

namespace CollapseLauncher.GameSettings.Genshin
{
    internal class PerfDataItem
    {
        #region Properties
        public int entryType { get; set; }
        public int index { get; set; }
        public string itemVersion { get; set; }
        #endregion

        #region Methods
        public PerfDataItem(int entryType, int index, string itemVersion)
        {
            this.entryType = entryType;
            this.index = index;
            this.itemVersion = itemVersion;
        }
        #endregion
    }

    internal class GenshinKeyValuePair
    {
        #region Properties
        public int key { get; set; }
        public int value { get; set; }
        #endregion

        #region Methods
        public GenshinKeyValuePair(int Key, int Value)
        {
            key = Key;
            value = Value;
        }
        #endregion
    }

    internal class GlobalPerfData
    {
        #region Fields
        private static readonly GlobalPerfData _LowestPreset = new()
        {
            FPS = FPSOption.f30,
            RenderResolution = 1,
            ShadowQuality = ShadowQualityOption.Lowest,
            VisualEffects = VisualEffectsOption.Lowest,
            SFXQuality = SFXQualityOption.Lowest,
            EnvironmentDetail = EnvironmentDetailOption.Lowest,
            VerticalSync = VerticalSyncOption.On,
            Antialiasing = AntialiasingOption.Off,
            VolumetricFog = VolumetricFogOption.Off,
            Reflections = ReflectionsOption.Off,
            MotionBlur = MotionBlurOption.Off,
            Bloom = BloomOption.On,
            CrowdDensity = CrowdDensityOption.High,
            SubsurfaceScattering = SubsurfaceScatteringOption.Off,
            CoOpTeammateEffects = CoOpTeammateEffectsOption.On,
            AnisotropicFiltering = AnisotropicFilteringOption.x1,
            GraphicsQuality = GraphicsQualityOption.Lowest,
            GlobalIllumination = GlobalIlluminationOption.Off,
            DynamicCharacterResolution = DynamicCharacterResolutionOption.On
        };

        private static readonly GlobalPerfData _LowPreset = new()
        {
            FPS = FPSOption.f30,
            RenderResolution = 8,
            ShadowQuality = ShadowQualityOption.Low,
            VisualEffects = VisualEffectsOption.Low,
            SFXQuality = SFXQualityOption.Low,
            EnvironmentDetail = EnvironmentDetailOption.Low,
            VerticalSync = VerticalSyncOption.On,
            Antialiasing = AntialiasingOption.FSR2,
            VolumetricFog = VolumetricFogOption.Off,
            Reflections = ReflectionsOption.Off,
            MotionBlur = MotionBlurOption.Off,
            Bloom = BloomOption.On,
            CrowdDensity = CrowdDensityOption.High,
            SubsurfaceScattering = SubsurfaceScatteringOption.Medium,
            CoOpTeammateEffects = CoOpTeammateEffectsOption.On,
            AnisotropicFiltering = AnisotropicFilteringOption.x2,
            GraphicsQuality = GraphicsQualityOption.Low,
            GlobalIllumination = GlobalIlluminationOption.Off,
            DynamicCharacterResolution = DynamicCharacterResolutionOption.On
        };

        private static readonly GlobalPerfData _MediumPreset = new()
        {
            FPS = FPSOption.f60,
            RenderResolution = 8,
            ShadowQuality = ShadowQualityOption.Medium,
            VisualEffects = VisualEffectsOption.Medium,
            SFXQuality = SFXQualityOption.Medium,
            EnvironmentDetail = EnvironmentDetailOption.Medium,
            VerticalSync = VerticalSyncOption.On,
            Antialiasing = AntialiasingOption.FSR2,
            VolumetricFog = VolumetricFogOption.Off,
            Reflections = ReflectionsOption.Off,
            MotionBlur = MotionBlurOption.High,
            Bloom = BloomOption.On,
            CrowdDensity = CrowdDensityOption.High,
            SubsurfaceScattering = SubsurfaceScatteringOption.Medium,
            CoOpTeammateEffects = CoOpTeammateEffectsOption.On,
            AnisotropicFiltering = AnisotropicFilteringOption.x4,
            GraphicsQuality = GraphicsQualityOption.Medium,
            GlobalIllumination = GlobalIlluminationOption.Medium,
            DynamicCharacterResolution = DynamicCharacterResolutionOption.On
        };
        #endregion

        #region Properties
        // Generate the list of the FPSOption value and order by ascending it.
        public static readonly FPSOption[] FPSOptionsList = Enum.GetValues<FPSOption>().OrderBy(GetFPSOptionNumber).ToArray();
        // Generate the list of the FPS number to be displayed on FPS Combobox
        // Queried in XAML, ReSharper fails to find it
        // ReSharper disable once CollectionNeverQueried.Global
        public static readonly int[] FPSIndex = FPSOptionsList.Select(GetFPSOptionNumber).ToArray();

        private static int GetFPSOptionNumber(FPSOption value)
        {
            // Get the string of the number by trimming the 'f' letter at the beginning
            string fpsStrNum = value.ToString().TrimStart('f');
            // Try parse the fpsStrNum as a number
            _ = int.TryParse(fpsStrNum, out int number);
            // Return the number
            return number;
        }
        
        // Queried in XAML, ReSharper fails to find it
        // ReSharper disable once CollectionNeverQueried.Global
        public static readonly string[] RenderScaleValuesStr = DictionaryCategory.RenderResolutionOption.Keys.Select(x => x.ToString("0.0")).ToArray();
        public static readonly List<double> RenderScaleValues = DictionaryCategory.RenderResolutionOption.Keys.ToList();
        public static readonly List<int> RenderScaleIndex = DictionaryCategory.RenderResolutionOption.Values.ToList();

        public List<PerfDataItem> saveItems                 { get; set; } = [];
        public bool               truePortedFromGraphicData { get; set; } = true;
        public string             portedVersion             { get; set; } = "OSRELWin4.2.0";
        public int                volatileUpgradeVersion    { get; set; } = 0;
        public bool               portedFromGraphicData     { get; set; } = false;
        #endregion

        #region Settings
        /// <summary>
        /// This defines "<c>FPS</c>" combobox In-game settings. <br/>
        /// Options: 30, 60, 45 <br/>
        /// Default: 60 [0]
        /// </summary>
        public FPSOption FPS = FPSOption.f60;

        /// <summary>
        /// This defines "<c>Render Resolution</c>" combobox In-game settings. <br/>
        /// Options: 0.6 [0], 0.8 [1], 0.9 [8], 1.0 [2], 1.1 [3], 1.2 [4], 1.3 [5], 1.4 [6], 1.5 [7]<br/>
        /// Default: 1.0 [2]
        /// </summary>
        public int RenderResolution = 2;

        /// <summary>
        /// This defines "<c>Shadow Quality</c>" combobox In-game settings. <br/>
        /// Options: Lowest, Low, Medium, High <br/>
        /// Default: High [3]
        /// </summary>
        public ShadowQualityOption ShadowQuality = ShadowQualityOption.High;

        /// <summary>
        /// This defines "<c>Visual Effects</c>" combobox In-game settings. <br/>
        /// Options: Lowest, Low, Medium, High <br/>
        /// Default: High [3]
        /// </summary>
        public VisualEffectsOption VisualEffects = VisualEffectsOption.High;

        /// <summary>
        /// This defines "<c>SFX Quality</c>" combobox In-game settings. <br/>
        /// Options: Lowest, Low, Medium, High <br/>
        /// Default: High [3]
        /// </summary>
        public SFXQualityOption SFXQuality = SFXQualityOption.High;

        /// <summary>
        /// This defines "<c>Environment Detail</c>" combobox In-game settings. <br/>
        /// Options: Lowest, Low, Medium, High, Highest <br/>
        /// Default: High [3]
        /// </summary>
        public EnvironmentDetailOption EnvironmentDetail = EnvironmentDetailOption.High;

        /// <summary>
        /// This defines "<c>Vertical Sync</c>" combobox In-game settings. <br/>
        /// Options: Off, On <br/>
        /// Default: On [1]
        /// </summary>
        public VerticalSyncOption VerticalSync = VerticalSyncOption.On;

        /// <summary>
        /// This defines "<c>Antialiasing</c>" combobox In-game settings. <br/>
        /// Options: Off, FSR 2, SMAA <br/>
        /// Default: FSR 2 [1]
        /// </summary>
        public AntialiasingOption Antialiasing = AntialiasingOption.FSR2;

        /// <summary>
        /// This defines "<c>Volumetric Fog</c>" combobox In-game settings. <br/>
        /// Options: Off, On <br/>
        /// Default: On [1]
        /// Game prohibits enabling this if "<c>Shadow Quality</c>" on Low or Lowest
        /// </summary>
        public VolumetricFogOption VolumetricFog = VolumetricFogOption.On;

        /// <summary>
        /// This defines "<c>Reflections</c>" combobox In-game settings. <br/>
        /// Options: Off, On <br/>
        /// Default: On [1]
        /// </summary>
        public ReflectionsOption Reflections = ReflectionsOption.On;

        /// <summary>
        /// This defines "<c>Motion Blur</c>" combobox In-game settings. <br/>
        /// Options: Off, Low, High, Extreme <br/>
        /// Default: Extreme [3]
        /// </summary>
        public MotionBlurOption MotionBlur = MotionBlurOption.Extreme;

        /// <summary>
        /// This defines "<c>Bloom</c>" combobox In-game settings. <br/>
        /// Options: Off, On <br/>
        /// Default: On [1]
        /// </summary>
        public BloomOption Bloom = BloomOption.On;

        /// <summary>
        /// This defines "<c>Crowd Density</c>" combobox In-game settings. <br/>
        /// Options: Low, High <br/>
        /// Default: High [1]
        /// </summary>
        public CrowdDensityOption CrowdDensity = CrowdDensityOption.High;

        /// <summary>
        /// This defines "<c>Subsurface Scattering</c>" combobox In-game settings. <br/>
        /// Options: Off, Medium, High <br/>
        /// Default: High [2]
        /// </summary>
        public SubsurfaceScatteringOption SubsurfaceScattering = SubsurfaceScatteringOption.High;

        /// <summary>
        /// This defines "<c>Co-Op Teammate Effects</c>" combobox In-game settings. <br/>
        /// Options: Off, Partially Off, On <br/>
        /// Default: On [2]
        /// </summary>
        public CoOpTeammateEffectsOption CoOpTeammateEffects = CoOpTeammateEffectsOption.On;

        /// <summary>
        /// This defines "<c>Anisotropic Filtering</c>" combobox In-game settings. <br/>
        /// Options: 1x, 2x, 4x, 8x, 16x <br/>
        /// Default: 8x [3]
        /// </summary>
        public AnisotropicFilteringOption AnisotropicFiltering = AnisotropicFilteringOption.x8;

        /// <summary>
        /// This defines "<c>Graphics Quality</c>" combobox In-game settings. <br/>
        /// Options: Lowest, Low, Medium, High <br/>
        /// Default: High [3]
        /// </summary>
        public GraphicsQualityOption GraphicsQuality = GraphicsQualityOption.High;

        /// <summary>
        /// This defines "<c>Global Illumination</c>" combobox In-game settings. <br/>
        /// Options: Off, Medium, High, Extreme <br/>
        /// Default: High [2] <br/>
        /// Notes: Only work for PC who meet the specs for Global Illumination, specified by HYV <br/>
        /// Further information: https://genshin.hoyoverse.com/en/news/detail/112690#:~:text=Minimum%20Specifications%20for%20Global%20Illumination
        /// </summary>
        public GlobalIlluminationOption GlobalIllumination = GlobalIlluminationOption.High;

        /// <summary>
        /// This defines "<c>Dynamic Character Resolution</c>" combobox In-game settings. <br/>
        /// Options: Off, On <br/>
        /// Default: On [1] <br/>
        /// Notes: Only work for PC who meet the specs for Dynamic Character Resolution, specified by HYV <br/>
        /// Further information: https://genshin.hoyoverse.com/en/news/detail/122141#:~:text=Dynamic%20Character%20Resolution
        /// </summary>
        public DynamicCharacterResolutionOption DynamicCharacterResolution = DynamicCharacterResolutionOption.On;
        #endregion

        #region Methods
        public static GlobalPerfData Load(string globalPerfJson, GraphicsData graphics)
        {
            GlobalPerfData tempData;
            // No GlobalPerfData object, import from GraphicsData
            // currentVolatielGrade => saveItems entryType 18
            // customVolatileGrades => saveItems
            // volatileVersion => portedVersion
            if (globalPerfJson == null && graphics != null)
            {
                tempData = new GlobalPerfData();
                var version = graphics.volatileVersion;
                tempData.portedVersion = version;
                tempData.saveItems.Add(new PerfDataItem(18, graphics.currentVolatielGrade - 1, version));
                if (graphics.currentVolatielGrade == -1)
                {
                    foreach (var setting in graphics.customVolatileGrades)
                    { 
                        tempData.saveItems.Add(new PerfDataItem(setting.key, setting.value - 1, version));
                    }
                }
            }
            else
            {
                tempData = globalPerfJson?.Deserialize(GenshinSettingsJsonContext.Default.GlobalPerfData) ?? new GlobalPerfData();
            }

            // Initialize globalPerf with a preset
            var graphicsQuality = (GraphicsQualityOption)(from setting in tempData.saveItems where setting.entryType == 18 select setting.index).FirstOrDefault(3);
            var globalPerf = graphicsQuality switch
            {
                GraphicsQualityOption.Lowest => _LowestPreset,
                GraphicsQualityOption.Low => _LowPreset,
                GraphicsQualityOption.Medium => _MediumPreset,
                _ => tempData
            };

            // Apply custom changes
            globalPerf.truePortedFromGraphicData = tempData.truePortedFromGraphicData;
            globalPerf.portedVersion = tempData.portedVersion;
            globalPerf.portedFromGraphicData = tempData.portedFromGraphicData;
            foreach (var setting in tempData.saveItems)
            {
                switch (setting.entryType)
                {
                    case 1:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - FPS: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.FPS = (FPSOption)setting.index;
                        break;

                    case 2:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - Render Resolution: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.RenderResolution = setting.index;
                        break;

                    case 3:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - Shadow Quality: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.ShadowQuality = (ShadowQualityOption)setting.index;
                        break;

                    case 4:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - Visual Effects: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.VisualEffects = (VisualEffectsOption)setting.index;
                        break;

                    case 5:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - SFX Quality: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.SFXQuality = (SFXQualityOption)setting.index;
                        break;

                    case 6:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - Environment Detail: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.EnvironmentDetail = (EnvironmentDetailOption)setting.index;
                        break;

                    case 7:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - Vertical Sync: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.VerticalSync = (VerticalSyncOption)setting.index;
                        break;

                    case 8:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - Antialiasing: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.Antialiasing = (AntialiasingOption)setting.index;
                        break;

                    case 9:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - Volumetric Fog: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.VolumetricFog = (VolumetricFogOption)setting.index;
                        break;

                    case 10:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - Reflections: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.Reflections = (ReflectionsOption)setting.index;
                        break;

                    case 11:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - Motion Blur: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.MotionBlur = (MotionBlurOption)setting.index;
                        break;

                    case 12:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - Bloom: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.Bloom = (BloomOption)setting.index;
                        break;

                    case 13:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - Crowd Density: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.CrowdDensity = (CrowdDensityOption)setting.index;
                        break;

                    // 14 is missing from settings
                    // And yes, do not reorder this unless the game order finally changes
                    // It is meant to be like this because miyoyo
                    case 16:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - Co-Op Teammate Effects: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.CoOpTeammateEffects = (CoOpTeammateEffectsOption)setting.index;
                        break;

                    case 15:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - Subsurface Scattering: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.SubsurfaceScattering = (SubsurfaceScatteringOption)setting.index;
                        break;

                    case 17:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - Anisotropic Filtering: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.AnisotropicFiltering = (AnisotropicFilteringOption)setting.index;
                        break;

                    case 18:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - Graphics Quality: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.GraphicsQuality = (GraphicsQualityOption)setting.index;
                        break;

                    case 19:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - Global Illumination: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.GlobalIllumination = (GlobalIlluminationOption)setting.index;
                        break;

                    case 21:
#if DEBUG
                        LogWriteLine($"Loaded Genshin Settings: Graphics - Dynamic Character Resolution: {setting.index}", LogType.Debug, true);
#endif
                        globalPerf.DynamicCharacterResolution = (DynamicCharacterResolutionOption)setting.index;
                        break;
                }
            }
            return globalPerf;
        }

        public string Save()
        {
            saveItems =
            [
                new PerfDataItem(1,  (int)FPS,                        portedVersion),
                new PerfDataItem(2,  RenderResolution,                portedVersion),
                new PerfDataItem(3,  (int)ShadowQuality,              portedVersion),
                new PerfDataItem(4,  (int)VisualEffects,              portedVersion),
                new PerfDataItem(5,  (int)SFXQuality,                 portedVersion),
                new PerfDataItem(6,  (int)EnvironmentDetail,          portedVersion),
                new PerfDataItem(7,  (int)VerticalSync,               portedVersion),
                new PerfDataItem(8,  (int)Antialiasing,               portedVersion),
                new PerfDataItem(9,  (int)VolumetricFog,              portedVersion),
                new PerfDataItem(10, (int)Reflections,                portedVersion),
                new PerfDataItem(11, (int)MotionBlur,                 portedVersion),
                new PerfDataItem(12, (int)Bloom,                      portedVersion),
                new PerfDataItem(13, (int)CrowdDensity,               portedVersion),
                new PerfDataItem(16, (int)CoOpTeammateEffects,        portedVersion),
                new PerfDataItem(15, (int)SubsurfaceScattering,       portedVersion),
                new PerfDataItem(17, (int)AnisotropicFiltering,       portedVersion),
                new PerfDataItem(18, (int)GraphicsQuality,            portedVersion),
                new PerfDataItem(19, (int)GlobalIllumination,         portedVersion),
                new PerfDataItem(21, (int)DynamicCharacterResolution, portedVersion)
            ];
            string data = this.Serialize(GenshinSettingsJsonContext.Default.GlobalPerfData, false);
#if DEBUG
            LogWriteLine($"Saved Genshin GlobalPerfData\r\n{data}", LogType.Debug, true);
#endif
            return data;
        }
        #endregion
    }
}
