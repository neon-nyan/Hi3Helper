using CollapseLauncher.CustomControls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace CollapseLauncher
{
    internal struct FileMigrationProcessUIRef
    {
        internal ContentDialogCollapse MainDialogWindow;
        internal TextBlock PathActivitySubtitle;
        internal Run SpeedIndicatorSubtitle;
        internal Run FileCountIndicatorSubtitle;
        internal Run FileSizeIndicatorSubtitle;
        internal ProgressBar ProgressBarIndicator;
    }
}
