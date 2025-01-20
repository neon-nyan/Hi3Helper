using System.Collections.Generic;

namespace CollapseLauncher.GameSettings.Genshin.Enums
{
    internal static class DictionaryCategory
    {
        // HoYoooooooo :wogreeee:
        internal static Dictionary<double, int> RenderResolutionOption = new()
        {
            { 0.6d, 0 },
            { 0.8d, 1 },
            { 0.9d, 8 },
            { 1.0d, 2 },
            { 1.1d, 3 },
            { 1.2d, 4 },
            { 1.3d, 5 },
            { 1.4d, 6 },
            { 1.5d, 7 }
        };
    }

    enum FPSOption
    {
        f30,
        f60,
        f45
    }

    enum ShadowQualityOption
    {
        Lowest,
        Low,
        Medium,
        High
    }

    enum VisualEffectsOption
    {
        Lowest,
        Low,
        Medium,
        High
    }

    enum SFXQualityOption
    {
        Lowest,
        Low,
        Medium,
        High
    }

    enum EnvironmentDetailOption
    {
        Lowest,
        Low,
        Medium,
        High,
        Highest
    }

    enum VerticalSyncOption
    {
        Off,
        On
    }

    enum AntialiasingOption
    {
        Off,
        FSR2,
        SMAA
    }

    enum VolumetricFogOption
    {
        Off,
        On
    }

    enum ReflectionsOption
    {
        Off,
        On
    }

    enum MotionBlurOption
    {
        Off,
        Low,
        High,
        Extreme
    }

    enum BloomOption
    {
        Off,
        On
    }

    enum CrowdDensityOption
    {
        Low,
        High
    }

    enum SubsurfaceScatteringOption
    {
        Off,
        Medium,
        High
    }

    enum CoOpTeammateEffectsOption
    {
        Off,
        PartiallyOff,
        On
    }

    enum AnisotropicFilteringOption
    {
        x1,
        x2,
        x4,
        x8,
        x16
    }

    enum GraphicsQualityOption
    {
        Lowest,
        Low,
        Medium,
        High
    }

    enum GlobalIlluminationOption
    {
        Off,
        Medium,
        High,
        Extreme
    }

    enum DynamicCharacterResolutionOption
    {
        Off,
        On
    }
}
