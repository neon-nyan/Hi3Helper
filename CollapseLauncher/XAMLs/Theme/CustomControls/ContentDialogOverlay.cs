#nullable enable
    using CollapseLauncher.Extension;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Windows.Foundation;
    using NColor = Windows.UI.Color;

    namespace CollapseLauncher.CustomControls
{
    // ReSharper disable once PartialTypeWithSinglePart
    public partial class ContentDialogOverlay : ContentDialog
    {
        public string ThemeTitleGlyph { get; set; }
        public ContentDialogTheme Theme { get; set; }
        public ContentDialogOverlay()
            : this(ContentDialogTheme.Warning) { }

        public ContentDialogOverlay(ContentDialogTheme theme = ContentDialogTheme.Warning)
        {
            Theme = theme;
            object brushObj = Theme switch
                              {
                                  ContentDialogTheme.Success => UIElementExtensions.GetApplicationResource<object>("SystemFillColorSuccessBrush"),
                                  ContentDialogTheme.Warning => UIElementExtensions.GetApplicationResource<object>("SystemFillColorCautionBrush"),
                                  ContentDialogTheme.Error => UIElementExtensions.GetApplicationResource<object>("SystemFillColorCriticalBrush"),
                                  _ => UIElementExtensions.GetApplicationResource<object>("SystemFillColorAttentionBrush")
                              };

            if (brushObj is not null and SolidColorBrush brush)
            {
                NColor titleColor = brush.Color;
                titleColor.A = 255;

                if (UIElementExtensions.GetApplicationResource<object>("DialogTitleBrush") is not null and SolidColorBrush brushTitle)
                    brushTitle.Color = titleColor;
            }

            ThemeTitleGlyph = Theme switch
            {
                ContentDialogTheme.Success => "",
                ContentDialogTheme.Warning => "",
                ContentDialogTheme.Error => "",
                _ => ""
            };
        }

        public new IAsyncOperation<ContentDialogResult> ShowAsync()
        {
            if (Title is string titleString && Theme != ContentDialogTheme.Informational)
            {
                Grid titleStack = UIElementExtensions.CreateIconTextGrid(
                        text: titleString,
                        iconGlyph: ThemeTitleGlyph,
                        iconSize: 20,
                        iconFontFamily: "FontAwesomeSolid"
                    ).WithPadding(-8d, 0d, 0d, 0d);
                Title = titleStack;
            }
            return base.ShowAsync();
        }
    }
}
