using CollapseLauncher.Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using static Hi3Helper.Locale;
// ReSharper disable RedundantExtendsListEntry

namespace CollapseLauncher.Pages
{
    public sealed partial class UnhandledExceptionPage : Page
    {
        public UnhandledExceptionPage()
        {
            BackgroundImgChanger.ToggleBackground(true);
            InitializeComponent();
            ExceptionTextBox.Text = ErrorSender.ExceptionContent;
            Title.Text = ErrorSender.ExceptionTitle;
            Subtitle.Text = ErrorSender.ExceptionSubtitle;

            if (ErrorSender.ExceptionType == ErrorType.Connection && WindowUtility.CurrentWindow is MainWindow mainWindow && mainWindow.rootFrame.CanGoBack)
                BackToPreviousPage.Visibility = Visibility.Visible;
        }

        private void GoBackPreviousPage(object sender, RoutedEventArgs e) => (WindowUtility.CurrentWindow as MainWindow)?.rootFrame.GoBack();

        private void CopyTextToClipboard(object sender, RoutedEventArgs e)
        {
            DataPackage data = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            data.SetText(ErrorSender.ExceptionContent);
            Clipboard.SetContent(data);
            CopyThrow.Content = Lang._UnhandledExceptionPage.CopyClipboardBtn2;
            CopyThrow.IsEnabled = false;
        }
    }
}
