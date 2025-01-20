using CollapseLauncher.Helper;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace CollapseLauncher.WindowSize
{
    internal static class WindowSize
    {
        internal static
#if !DEBUG
            readonly
#endif
            Dictionary<string, WindowSizeProp> WindowSizeProfiles
#if DEBUG
            =>
#else
            =
#endif
            new()
            {
            {
                "Normal",
                new WindowSizeProp
                {
                    WindowBounds                  = new Size(1280, 720),
                    PostEventPanelScaleFactor     = 1.35f,
                    SidePanel1Width               = new GridLength(340, GridUnitType.Pixel),
                    EventPostCarouselBounds       = new Size(340,  158),
                    PostPanelBounds               = new Size(340,  84),
                    PostPanelBottomMargin         = new Thickness(0, 0, 0, 20),
                    PostPanelPaimonHeight         = 110,
                    PostPanelPaimonMargin         = new Thickness(0, -48, -56, 0),
                    PostPanelPaimonInnerMargin    = new Thickness(0, 0,   0,   0),
                    PostPanelPaimonTextMargin     = new Thickness(0, 0,   0,   18),
                    PostPanelPaimonTextSize       = 11,
                    BannerIconWidth               = 136,
                    BannerIconWidthHYP            = 111,
                    BannerIconMargin              = new Thickness(0, 0, 94, 84),
                    BannerIconMarginHYP           = new Thickness(48, 252, 0, 0),
                    BannerIconAlignHorizontal     = HorizontalAlignment.Right,
                    BannerIconAlignHorizontalHYP  = HorizontalAlignment.Left,
                    BannerIconAlignVertical       = VerticalAlignment.Bottom,
                    BannerIconAlignVerticalHYP    = VerticalAlignment.Top,
                    SettingsPanelWidth            = 676
                }
            },
            {
                "Small",
                new WindowSizeProp
                {
                    WindowBounds                  = new Size(1024, 576),
                    PostEventPanelScaleFactor     = 1.25f,
                    SidePanel1Width               = new GridLength(280, GridUnitType.Pixel),
                    EventPostCarouselBounds       = new Size(280,  130),
                    PostPanelBounds               = new Size(280,  82),
                    PostPanelBottomMargin         = new Thickness(0, 0, 0, 12),
                    PostPanelPaimonHeight         = 110,
                    PostPanelPaimonMargin         = new Thickness(0, -48, -56, 0),
                    PostPanelPaimonInnerMargin    = new Thickness(0, 0,   0,   0),
                    PostPanelPaimonTextMargin     = new Thickness(0, 0,   0,   18),
                    PostPanelPaimonTextSize       = 11,
                    BannerIconWidth               = 100,
                    BannerIconWidthHYP            = 86,
                    BannerIconMargin              = new Thickness(0, 0, 70, 52),
                    BannerIconMarginHYP           = new Thickness(22, 186, 0, 0),
                    BannerIconAlignHorizontal     = HorizontalAlignment.Right,
                    BannerIconAlignHorizontalHYP  = HorizontalAlignment.Left,
                    BannerIconAlignVertical       = VerticalAlignment.Bottom,
                    BannerIconAlignVerticalHYP    = VerticalAlignment.Top,
                    SettingsPanelWidth            = 464
                }
            }
        };

        internal static string CurrentWindowSizeName
        {
            get
            {
                string val = GetAppConfigValue("WindowSizeProfile").ToString();
                return !WindowSizeProfiles.ContainsKey(val ?? "Normal") ? WindowSizeProfiles.Keys.FirstOrDefault() : val;
            }
            set
            {
                SetAppConfigValue("WindowSizeProfile", value);
                WindowUtility.SetWindowSize(CurrentWindowSize.WindowBounds.Width, CurrentWindowSize.WindowBounds.Height);
            }
        }

        internal static WindowSizeProp CurrentWindowSize      { get => WindowSizeProfiles[CurrentWindowSizeName]; }
    }

    internal class WindowSizeProp
    {
        public Size                WindowBounds                 { get; set; }
        public GridLength          SidePanel1Width              { get; set; }
        public float               PostEventPanelScaleFactor    { get; set; }
        public Size                EventPostCarouselBounds      { get; set; }
        public Size                PostPanelBounds              { get; set; }
        public Thickness           PostPanelBottomMargin        { get; set; }
        public double              PostPanelPaimonTextSize      { get; set; }
        public int                 PostPanelPaimonHeight        { get; set; }
        public Thickness           PostPanelPaimonMargin        { get; set; }
        public Thickness           PostPanelPaimonInnerMargin   { get; set; }
        public Thickness           PostPanelPaimonTextMargin    { get; set; }
        public int                 BannerIconWidth              { get; set; }
        public int                 BannerIconWidthHYP           { get; set; }
        public Thickness           BannerIconMargin             { get; set; }
        public Thickness           BannerIconMarginHYP          { get; set; }
        public HorizontalAlignment BannerIconAlignHorizontal    { get; set; }
        public HorizontalAlignment BannerIconAlignHorizontalHYP { get; set; }
        public VerticalAlignment   BannerIconAlignVertical      { get; set; }
        public VerticalAlignment   BannerIconAlignVerticalHYP   { get; set; }
        public int                 SettingsPanelWidth           { get; set; }
    }
}
