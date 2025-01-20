using Microsoft.UI.Xaml.Controls;
// ReSharper disable RedundantExtendsListEntry

namespace CollapseLauncher.Pages
{
    public sealed partial class UnavailablePage : Page
    {
        public UnavailablePage()
        {
            BackgroundImgChanger.ToggleBackground(true);
            InitializeComponent();
        }
    }
}
