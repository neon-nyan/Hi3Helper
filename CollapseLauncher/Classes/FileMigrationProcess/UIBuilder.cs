using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.FileDialogCOM;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Win32.FileDialogCOM;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    internal partial class FileMigrationProcess
    {
        private static async ValueTask<string> BuildCheckOutputPathUI(UIElement parentUI, string dialogTitle, string inputPath, string outputPath, bool isFileTransfer)
        {
            ContentDialogCollapse mainDialogWindow = new ContentDialogCollapse(ContentDialogTheme.Informational)
            {
                Title = dialogTitle,
                CloseButtonText = Locale.Lang!._Misc!.Cancel,
                PrimaryButtonText = null,
                SecondaryButtonText = null,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = parentUI!.XamlRoot
            };

            Grid mainGrid = UIElementExtensions.CreateGrid()
                .WithRows(new GridLength(1.0,    GridUnitType.Star), new GridLength(1.0, GridUnitType.Star), new GridLength(1.0, GridUnitType.Star))
                .WithColumns(new GridLength(1.0, GridUnitType.Star), GridLength.Auto);

            // ReSharper disable once UnusedVariable
            TextBlock locateFolderSubtitle = mainGrid.AddElementToGridColumn(new TextBlock
            {
                FontSize = 14d,
                TextWrapping = TextWrapping.Wrap,
                Text = Locale.Lang._FileMigrationProcess!.LocateFolderSubtitle
            }, 0, 2).WithHorizontalAlignment(HorizontalAlignment.Stretch);

            TextBox choosePathTextBox = mainGrid.AddElementToGridRow(new TextBox
            {
                IsSpellCheckEnabled = false,
                IsRightTapEnabled = false,
                PlaceholderText = Locale.Lang._FileMigrationProcess.ChoosePathTextBoxPlaceholder,
                Text = string.IsNullOrEmpty(outputPath) ? null : outputPath
            }, 1).WithMargin(0d, 12d, 0d, 0d)
            .WithMinWidth(256d)
            .WithHorizontalAlignment(HorizontalAlignment.Stretch)
            .WithHorizontalContentAlignment(HorizontalAlignment.Stretch);

            Button choosePathButton = mainGrid
                .AddElementToGridRowColumn(UIElementExtensions
                    .CreateButtonWithIcon<Button>(Locale.Lang._FileMigrationProcess.ChoosePathButton, "", "FontAwesome", "AccentButtonStyle"),
                    1, 1).WithMargin(8d, 12d, 0d, 0d);

            InfoBar warningTextInfoBar = mainGrid.AddElementToGridRowColumn(new InfoBar
            {
                IsClosable = true,
                IsOpen = false
            }, 2, 0, 0, 2).WithMargin(0, 16, 0, 0);

            mainDialogWindow.Content = mainGrid;

            if (!string.IsNullOrEmpty(outputPath))
                ToggleOrCheckPathWarning(outputPath);

            choosePathButton.Click += async (_, _) =>
            {
                string pathResult = isFileTransfer ? await FileDialogNative.GetFileSavePicker(null, dialogTitle) :
                                                       await FileDialogHelper.GetRestrictedFolderPathDialog(dialogTitle);

                choosePathTextBox!.Text = string.IsNullOrEmpty(pathResult) ? null : pathResult;
            };
            choosePathTextBox!.TextChanged += (sender, _) => ToggleOrCheckPathWarning(((TextBox)sender!).Text);

            ContentDialogResult mainDialogWindowResult = await mainDialogWindow.QueueAndSpawnDialog();
            return mainDialogWindowResult == ContentDialogResult.Primary ? choosePathTextBox.Text : null;

            void ToggleWarningText(string text = null)
            {
                bool canContinue = string.IsNullOrEmpty(text);
                mainDialogWindow.PrimaryButtonText = canContinue ? Locale.Lang!._Misc!.Next : null;

                warningTextInfoBar.Title    = Locale.Lang!._FileMigrationProcess!.ChoosePathErrorTitle;
                warningTextInfoBar.Severity = canContinue ? InfoBarSeverity.Success : InfoBarSeverity.Error;
                warningTextInfoBar.IsOpen   = !canContinue;
                warningTextInfoBar.Message  = text;
            }

            void ToggleOrCheckPathWarning(string path)
            {
                string parentPath              = path;
                if (isFileTransfer) parentPath = Path.GetDirectoryName(path);

                if (string.IsNullOrEmpty(parentPath))
                {
                    ToggleWarningText(Locale.Lang!._FileMigrationProcess!.ChoosePathErrorPathUnselected);
                    return;
                }
                if (parentPath.StartsWith(inputPath, StringComparison.OrdinalIgnoreCase) || IsOutputPathSameAsInput(inputPath, path, isFileTransfer))
                {
                    ToggleWarningText(Locale.Lang!._FileMigrationProcess!.ChoosePathErrorPathIdentical);
                    return;
                }
                if (!(File.Exists(parentPath) || Directory.Exists(parentPath)))
                {
                    ToggleWarningText(Locale.Lang!._FileMigrationProcess!.ChoosePathErrorPathNotExist);
                    return;
                }
                if (!ConverterTool.IsUserHasPermission(parentPath))
                {
                    ToggleWarningText(Locale.Lang!._FileMigrationProcess!.ChoosePathErrorPathNoPermission);
                    return;
                }
                ToggleWarningText();
            }
        }

        private FileMigrationProcessUIRef BuildMainMigrationUI()
        {
            ContentDialogCollapse mainDialogWindow = new ContentDialogCollapse(ContentDialogTheme.Informational)
            {
                Title = DialogTitle,
                CloseButtonText = null,
                PrimaryButtonText = null,
                SecondaryButtonText = null,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = ParentUI!.XamlRoot
            };

            Grid mainGrid = UIElementExtensions.CreateGrid()
                .WithWidth(500d)
                .WithColumns(new GridLength(1.0d, GridUnitType.Star), new GridLength(1.0d, GridUnitType.Star))
                .WithRows(new GridLength(1.0d,    GridUnitType.Auto), new GridLength(20d,  GridUnitType.Pixel), new GridLength(20d, GridUnitType.Pixel), new GridLength(20d, GridUnitType.Pixel));

            // Build path indicator
            StackPanel pathActivityPanel = mainGrid.AddElementToGridRowColumn(
                UIElementExtensions.CreateStackPanel().WithMargin(0d, 0d, 0d, 8d),
                0, 0, 0, 2
                );
            _ = pathActivityPanel.AddElementToStackPanel(
                new TextBlock
                {
                    FontWeight = FontWeights.Bold,
                    Text = Locale.Lang!._FileMigrationProcess!.PathActivityPanelTitle
                });
            TextBlock pathActivitySubtitle = pathActivityPanel.AddElementToStackPanel(
                new TextBlock
                {
                    Text = Locale.Lang._Misc!.Idle,
                    FontSize = 18d,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

            // Build speed indicator
            TextBlock speedIndicator = mainGrid.AddElementToGridRow(
                new TextBlock { FontWeight = FontWeights.Bold },
                1);
            Run speedIndicatorTitle = new Run { Text = Locale.Lang._FileMigrationProcess.SpeedIndicatorTitle, FontWeight = FontWeights.Medium };
            Run speedIndicatorSubtitle = new Run { Text = "-" };
            speedIndicator!.Inlines!.Add(speedIndicatorTitle);
            speedIndicator!.Inlines!.Add(speedIndicatorSubtitle);

            // Build file count indicator
            TextBlock fileCountIndicator = mainGrid.AddElementToGridRow(
                new TextBlock { FontWeight = FontWeights.Bold },
                2);
            Run fileCountIndicatorTitle = new Run { Text = Locale.Lang._FileMigrationProcess.FileCountIndicatorTitle, FontWeight = FontWeights.Medium };
            Run fileCountIndicatorSubtitle = new Run { Text = Locale.Lang._Misc.PerFromToPlaceholder };
            fileCountIndicator!.Inlines!.Add(fileCountIndicatorTitle);
            fileCountIndicator!.Inlines!.Add(fileCountIndicatorSubtitle);

            // Build file size indicator
            TextBlock fileSizeIndicator = mainGrid.AddElementToGridRowColumn(
                new TextBlock
                {
                    FontWeight = FontWeights.Bold,
                    HorizontalTextAlignment = TextAlignment.Right
                },
                1, 1).WithHorizontalAlignment(HorizontalAlignment.Right);
            Run fileSizeIndicatorSubtitle = new Run { Text = Locale.Lang._Misc.PerFromToPlaceholder };
            fileSizeIndicator!.Inlines!.Add(fileSizeIndicatorSubtitle);

            // Build progress percentage indicator
            StackPanel progressTextIndicator = mainGrid.AddElementToGridRowColumn(
                UIElementExtensions.CreateStackPanel(Orientation.Horizontal).WithHorizontalAlignment(HorizontalAlignment.Right),
                2, 1);
            TextBlock progressTextIndicatorSubtitle = progressTextIndicator.AddElementToStackPanel(
                new TextBlock { Text = "0", FontWeight = FontWeights.Bold });
            _ = progressTextIndicator.AddElementToStackPanel(
                new TextBlock { Text = "%", FontWeight = FontWeights.Bold });

            // Build progress bar indicator
            ProgressBar progressBarIndicator = mainGrid.AddElementToGridRowColumn(
                new ProgressBar
                {
                    Value = 0d,
                    Maximum = 100d,
                    IsIndeterminate = true
                }.WithHorizontalAlignment(HorizontalAlignment.Stretch)
                .WithVerticalAlignment(VerticalAlignment.Bottom),
                3, 0, 0, 2);

            // Set progress percentage indicator subtitle with progress bar value
            BindingOperations.SetBinding(progressTextIndicatorSubtitle, TextBlock.TextProperty, new Binding
            {
                Source = progressBarIndicator,
                Path = new PropertyPath("Value"),
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            // Set the main dialog content and queue the dialog spawn
            mainDialogWindow.Content = mainGrid;
        #pragma warning disable CA2012
            _ = mainDialogWindow.QueueAndSpawnDialog();
        #pragma warning restore CA2012

            // Return the migration process UI ref struct 
            return new FileMigrationProcessUIRef
            {
                MainDialogWindow = mainDialogWindow,
                PathActivitySubtitle = pathActivitySubtitle,
                FileCountIndicatorSubtitle = fileCountIndicatorSubtitle,
                FileSizeIndicatorSubtitle = fileSizeIndicatorSubtitle,
                ProgressBarIndicator = progressBarIndicator,
                SpeedIndicatorSubtitle = speedIndicatorSubtitle
            };
        }
    }
}
