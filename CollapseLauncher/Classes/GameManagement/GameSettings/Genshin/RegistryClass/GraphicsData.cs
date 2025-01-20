using CollapseLauncher.GameSettings.Genshin.Context;
using Hi3Helper;
using System.Collections.Generic;
using static Hi3Helper.Logger;

namespace CollapseLauncher.GameSettings.Genshin
{
    internal class GraphicsData
    {
        #region Properties
        public int                       currentVolatielGrade { get; set; } = -1;
        public List<GenshinKeyValuePair> customVolatileGrades { get; set; } = [];
        public string                    volatileVersion      { get; set; } = "";
        #endregion

        #region Methods
#nullable enable
        public static GraphicsData Load(string graphicsJson)
        {
            return graphicsJson.Deserialize(GenshinSettingsJsonContext.Default.GraphicsData) ?? new GraphicsData();
        }

        public string Create(GlobalPerfData globalPerf)
        {
            volatileVersion = globalPerf.portedVersion;
            customVolatileGrades =
            [
                new GenshinKeyValuePair(1,  (int)globalPerf.FPS + 1),
                new GenshinKeyValuePair(2,  globalPerf.RenderResolution + 1),
                new GenshinKeyValuePair(3,  (int)globalPerf.ShadowQuality + 1),
                new GenshinKeyValuePair(4,  (int)globalPerf.VisualEffects + 1),
                new GenshinKeyValuePair(5,  (int)globalPerf.SFXQuality + 1),
                new GenshinKeyValuePair(6,  (int)globalPerf.EnvironmentDetail + 1),
                new GenshinKeyValuePair(7,  (int)globalPerf.VerticalSync + 1),
                new GenshinKeyValuePair(8,  (int)globalPerf.Antialiasing + 1),
                new GenshinKeyValuePair(9,  (int)globalPerf.VolumetricFog + 1),
                new GenshinKeyValuePair(10, (int)globalPerf.Reflections + 1),
                new GenshinKeyValuePair(11, (int)globalPerf.MotionBlur + 1),
                new GenshinKeyValuePair(12, (int)globalPerf.Bloom + 1),
                new GenshinKeyValuePair(13, (int)globalPerf.CrowdDensity + 1),
                new GenshinKeyValuePair(16, (int)globalPerf.CoOpTeammateEffects + 1),
                new GenshinKeyValuePair(15, (int)globalPerf.SubsurfaceScattering + 1),
                new GenshinKeyValuePair(17, (int)globalPerf.AnisotropicFiltering + 1),
                new GenshinKeyValuePair(19, (int)globalPerf.GlobalIllumination + 1),
                new GenshinKeyValuePair(21, (int)globalPerf.DynamicCharacterResolution + 1)
            ];

            string data = this.Serialize(GenshinSettingsJsonContext.Default.GraphicsData, false);
#if DEBUG
            LogWriteLine($"Saved Genshin GraphicsData\r\n{data}", LogType.Debug, true);
#endif
            return data;
        }
        #endregion
    }
}
