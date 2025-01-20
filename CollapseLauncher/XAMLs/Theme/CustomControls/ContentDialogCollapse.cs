// using Hi3Helper.Shared.ClassStruct;

using CollapseLauncher.Extension;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using NColor = Windows.UI.Color;
// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher.CustomControls
{
    public enum ContentDialogTheme { Informational, Warning, Error, Success }

    public partial class ContentDialogCollapse : ContentDialog
    {
        public string ThemeTitleGlyph { get; set; }
        public ContentDialogTheme Theme { get; set; }
        public ContentDialogCollapse()
            : this(ContentDialogTheme.Warning) { }

        public ContentDialogCollapse(ContentDialogTheme theme = ContentDialogTheme.Warning)
        {
            Theme = theme;
            NColor titleColor = (Theme switch
            {
                ContentDialogTheme.Success => UIElementExtensions.GetApplicationResource<SolidColorBrush>("SystemFillColorSuccessBrush"),
                ContentDialogTheme.Warning => UIElementExtensions.GetApplicationResource<SolidColorBrush>("SystemFillColorCautionBrush"),
                ContentDialogTheme.Error => UIElementExtensions.GetApplicationResource<SolidColorBrush>("SystemFillColorCriticalBrush"),
                _ => UIElementExtensions.GetApplicationResource<SolidColorBrush>("SystemFillColorAttentionBrush")
            }).Color;
            titleColor.A                                                                          = 255;
            UIElementExtensions.GetApplicationResource<SolidColorBrush>("DialogTitleBrush").Color = titleColor;

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
