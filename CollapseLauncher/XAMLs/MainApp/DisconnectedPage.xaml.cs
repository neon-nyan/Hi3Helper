using CollapseLauncher.Dialogs;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Linq;
using static Hi3Helper.Logger;
// ReSharper disable IdentifierTypo
// ReSharper disable AsyncVoidMethod

namespace CollapseLauncher
{
    public sealed partial class DisconnectedPage
    {
        public DisconnectedPage()
        {
            try
            {
                InitializeComponent();
                WindowUtility.SetWindowBackdrop(WindowBackdropKind.Mica);
                OverlayFrame.Navigate(typeof(Pages.NullPage));
                InitializeRegionComboBox();
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL CRASH!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void InitializeRegionComboBox()
        {
            ComboBoxGameCategory.ItemsSource = InnerLauncherConfig.BuildGameTitleListUI();
#nullable enable
            string? gameName = LauncherConfig.GetAppConfigValue("GameCategory");

            List<string>? gameCollection = LauncherMetadataHelper.GetGameNameCollection()!;
            List<string?>? regionCollection = LauncherMetadataHelper.GetGameRegionCollection(gameName ?? "");

            if (regionCollection == null)
                gameName = LauncherMetadataHelper.LauncherGameNameRegionCollection?.Keys.FirstOrDefault();

            ComboBoxGameRegion.ItemsSource = InnerLauncherConfig.BuildGameRegionListUI(gameName);

            var indexCategory = gameCollection.IndexOf(gameName!);
            if (indexCategory < 0) indexCategory = 0;

            var indexRegion = LauncherMetadataHelper.GetPreviousGameRegion(gameName);

            ComboBoxGameCategory.SelectedIndex = indexCategory;
            ComboBoxGameRegion.SelectedIndex = indexRegion;
        }

        private void PaimonClicked(object sender, PointerRoutedEventArgs e)
        {
            WindowUtility.SetWindowBackdrop(WindowBackdropKind.None);
            MainFrameChanger.ChangeWindowFrame(typeof(MainPage), new DrillInNavigationTransitionInfo());
        }

        private async void ShowError(object sender, RoutedEventArgs e)
        {
            await SimpleDialogs.Dialog_ShowUnhandledExceptionMenu(this);
            // MainFrameChanger.ChangeWindowFrame(typeof(UnhandledExceptionPage));
        }

        private void GoToAppSettings(object sender, RoutedEventArgs e)
        {
            OverlayFrame.Navigate(typeof(Pages.SettingsPage));
            GoBackOverlayFrame.Visibility = Visibility.Visible;
            OverlayFrameBg.Visibility = Visibility.Visible;
            FrontGrid.Visibility = Visibility.Collapsed;
        }

        private void GoBackFromOverlayFrame(object sender, RoutedEventArgs e)
        {
            if (OverlayFrame?.CanGoBack ?? false)
                OverlayFrame?.GoBack();

            if ((OverlayFrame?.CanGoBack ?? false) || sender is not Button btn)
            {
                return;
            }

            btn.Visibility            = Visibility.Collapsed;
            OverlayFrameBg.Visibility = Visibility.Collapsed;
            FrontGrid.Visibility      = Visibility.Visible;
        }

        private void SetGameCategoryChange(object sender, SelectionChangedEventArgs e)
        {
            object? selectedItem = ((ComboBox)sender).SelectedItem;
            if (selectedItem == null) return;
            string? selectedCategoryString = InnerLauncherConfig.GetComboBoxGameRegionValue(selectedItem);

            ComboBoxGameRegion.ItemsSource = InnerLauncherConfig.BuildGameRegionListUI(selectedCategoryString);
            ComboBoxGameRegion.SelectedIndex = InnerLauncherConfig.GetIndexOfRegionStringOrDefault(selectedCategoryString);
        }

        private async void SetGameRegionChange(object sender, SelectionChangedEventArgs e)
        {
            object? selValue = ((ComboBox)sender).SelectedValue;
            if (selValue == null) return;

            string? category = InnerLauncherConfig.GetComboBoxGameRegionValue(ComboBoxGameCategory.SelectedValue);
            string? region = InnerLauncherConfig.GetComboBoxGameRegionValue(selValue);
            _ = await LauncherMetadataHelper.GetMetadataConfig(category, region);

            // Set and Save CurrentRegion in AppConfig
            LauncherConfig.SetAndSaveConfigValue("GameCategory", category);
            LauncherMetadataHelper.SetPreviousGameRegion(category, region);
        }
    }
}