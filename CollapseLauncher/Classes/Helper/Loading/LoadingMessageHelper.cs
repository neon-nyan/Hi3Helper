using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Animation;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace CollapseLauncher.Helper.Loading
{
    internal static class LoadingMessageHelper
    {
        internal static MainWindow CurrentMainWindow;
        internal static bool IsLoadingProgressIndeterminate = true;
        internal static bool IsCurrentlyShow;
        internal static List<RoutedEventHandler> CurrentActionButtonHandler = [];

        /// <summary>
        /// Initialize the necessary <c>MainWindow</c> for spawning the loading frame overlay.
        /// </summary>
        internal static void Initialize()
        {
            if (WindowUtility.CurrentWindow is MainWindow window)
                CurrentMainWindow = window;
        }

        /// <summary>
        /// Set the message of the loading frame.
        /// </summary>
        /// <param name="title">Set the title of the loading frame. You can set the value to <c>null</c> to ignore the change of the title or <c>string.Empty</c> to disable the title</param>
        /// <param name="subtitle">Set the subtitle of the loading frame. You can set the value to <c>null</c> to ignore the change of the subtitle or <c>string.Empty</c> to disable the subtitle</param>
        internal static void SetMessage(string title, string subtitle)
        {
            if (title != null) CurrentMainWindow!.LoadingStatusTextTitle!.Text = title;
            if (subtitle != null) CurrentMainWindow!.LoadingStatusTextSubtitle!.Text = subtitle;

            CurrentMainWindow!.LoadingStatusTextSeparator!.Visibility = title == string.Empty || subtitle == string.Empty ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>
        /// Set the value of the progress ring.
        /// <br/><br/>
        /// Note: If you would like to set the progress ring's value, make sure to set the <c>isProgressIndeterminate</c> to <c>true</c> with <c>SetProgressBarState()</c> method.
        /// </summary>
        /// <param name="value">Set the current value of the progress ring</param>
        internal static void SetProgressBarValue(double value) => CurrentMainWindow!.LoadingStatusProgressRing!.Value = value;

        /// <summary>
        /// Set the state and the maximum value of the progress ring.
        /// </summary>
        /// <param name="maxValue">Set the maximum value of the progress ring.</param>
        /// <param name="isProgressIndeterminate">Set the state of the progress ring's indeterminate.</param>
        internal static void SetProgressBarState(double maxValue = 100d, bool isProgressIndeterminate = true)
        {
            IsLoadingProgressIndeterminate = isProgressIndeterminate;
            CurrentMainWindow!.LoadingStatusProgressRing!.Maximum = maxValue;
            CurrentMainWindow!.LoadingStatusProgressRing!.IsIndeterminate = IsLoadingProgressIndeterminate;
        }

        /// <summary>
        /// Show the loading frame.
        /// </summary>
        internal static async void ShowLoadingFrame()
        {
            try
            {
                if (CurrentMainWindow.LoadingStatusBackgroundGrid.DispatcherQueue.HasThreadAccess)
                {
                    await ShowLoadingFrameInner();
                    return;
                }

                await CurrentMainWindow.LoadingStatusBackgroundGrid.DispatcherQueue.EnqueueAsync(ShowLoadingFrameInner);
            }
            catch
            {
                // ignored
            }
        }

        private static async Task ShowLoadingFrameInner()
        {
            if (IsCurrentlyShow) return;

            IsCurrentlyShow                                            = true;
            CurrentMainWindow!.LoadingStatusGrid!.Visibility           = Visibility.Visible;
            CurrentMainWindow!.LoadingStatusBackgroundGrid!.Visibility = Visibility.Visible;

            TimeSpan duration = TimeSpan.FromSeconds(0.25);

            await Task.WhenAll(
                               CurrentMainWindow.LoadingStatusBackgroundGrid.StartAnimation(duration,
                                                                                            CurrentMainWindow.LoadingStatusBackgroundGrid.GetElementCompositor()!.CreateScalarKeyFrameAnimation("Opacity", 1, 0)),
                               CurrentMainWindow.LoadingStatusGrid.StartAnimation(duration,
                                                                                  CurrentMainWindow.LoadingStatusGrid.GetElementCompositor()!.CreateVector3KeyFrameAnimation("Translation", new Vector3(0, 0, CurrentMainWindow.LoadingStatusGrid.Translation.Z), new Vector3(0, (float)(CurrentMainWindow.LoadingStatusGrid.ActualHeight + 16), CurrentMainWindow.LoadingStatusGrid.Translation.Z)),
                                                                                  CurrentMainWindow.LoadingStatusGrid.GetElementCompositor()!.CreateScalarKeyFrameAnimation("Opacity", 1, 0))
                              );
        }

        /// <summary>
        /// Hide the loading frame (also hide the action button).
        /// </summary>
        internal static async void HideLoadingFrame()
        {
            try
            {
                if (CurrentMainWindow.LoadingStatusBackgroundGrid.DispatcherQueue.HasThreadAccess)
                {
                    await HideLoadingFrameInner();
                    return;
                }

                await CurrentMainWindow.LoadingStatusBackgroundGrid.DispatcherQueue.EnqueueAsync(HideLoadingFrameInner);
            }
            catch
            {
                // ignored
            }
        }

        private static async Task HideLoadingFrameInner()
        {
            if (!IsCurrentlyShow) return;

            IsCurrentlyShow = false;

            TimeSpan duration = TimeSpan.FromSeconds(0.25);
            await Task.WhenAll(
                               CurrentMainWindow.LoadingStatusBackgroundGrid.StartAnimation(duration,
                                                                                            CurrentMainWindow.LoadingStatusBackgroundGrid.GetElementCompositor()!.CreateScalarKeyFrameAnimation("Opacity", 0, 1)),
                               CurrentMainWindow.LoadingStatusGrid.StartAnimation(duration,
                                                                                  CurrentMainWindow.LoadingStatusGrid.GetElementCompositor()!.CreateVector3KeyFrameAnimation("Translation", new Vector3(0, (float)(CurrentMainWindow.LoadingStatusGrid.ActualHeight + 16), CurrentMainWindow.LoadingStatusGrid.Translation.Z), new Vector3(0, 0, CurrentMainWindow.LoadingStatusGrid.Translation.Z)),
                                                                                  CurrentMainWindow.LoadingStatusGrid.GetElementCompositor()!.CreateScalarKeyFrameAnimation("Opacity", 0, 1))
                              );

            CurrentMainWindow.LoadingStatusGrid.Visibility            = Visibility.Collapsed;
            CurrentMainWindow.LoadingStatusBackgroundGrid!.Visibility = Visibility.Collapsed;
            HideActionButton();
        }

        /// <summary>
        /// Show the action button for the loading frame.
        /// </summary>
        /// <param name="buttonContent">Set the content of the button. <c>string</c> or <c>FrameworkElement</c> (UI element) is acceptable.</param>
        /// <param name="buttonIconGlyph">
        /// Set the glyph icon of the button. Set it to <c>null</c> or <c>string.Empty</c> to disable the icon.
        /// <br/><br/>
        /// Note: You can directly use the unicode character as an input or write it with "\u" escape (for example: "\uf00d" for X mark icon).
        /// <br/>Keep in mind that you can only use the glyph that's available in "Font Awesome Solid" font.<br/>
        /// </param>
        /// <param name="routedEvent">Set the event callback for the button's click event.</param>
        internal static void ShowActionButton(object buttonContent, string buttonIconGlyph = null, RoutedEventHandler routedEvent = null)
        {
            if (routedEvent != null)
            {
                CurrentActionButtonHandler!.Add(routedEvent);
                CurrentMainWindow!.LoadingStatusActionButton!.Click += routedEvent;
            }

            bool isHasIcon = !string.IsNullOrEmpty(buttonIconGlyph);
            CurrentMainWindow!.LoadingStatusActionButtonIcon!.Visibility = isHasIcon ? Visibility.Visible : Visibility.Collapsed;
            CurrentMainWindow!.LoadingStatusActionButton!.Visibility = Visibility.Visible;
            if (isHasIcon) CurrentMainWindow.LoadingStatusActionButtonIcon.Glyph = buttonIconGlyph;

            if (buttonContent is string buttonText)
            {
                TextBlock textBlock = new()
                {
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, isHasIcon ? -2 : 0, 0, 0)
                };
                textBlock.AddTextBlockLine(buttonText, FontWeights.SemiBold);
                CurrentMainWindow.LoadingStatusActionButtonContentContainer!.Children!.Clear();
                CurrentMainWindow.LoadingStatusActionButtonContentContainer!.AddElementToGridRowColumn(textBlock);
                return;
            }

            CurrentMainWindow.LoadingStatusActionButtonContentContainer!.Children!.Clear();
            CurrentMainWindow.LoadingStatusActionButtonContentContainer!.AddElementToGridRowColumn(buttonContent as FrameworkElement);
        }

        /// <summary>
        /// Hide the action button from the loading frame.
        /// </summary>
        internal static void HideActionButton()
        {
            if (CurrentActionButtonHandler!.Count > 0)
            {
                foreach (var t in CurrentActionButtonHandler)
                    CurrentMainWindow!.LoadingStatusActionButton!.Click -= t;

                CurrentActionButtonHandler.Clear();
            }

            CurrentMainWindow!.LoadingStatusActionButton!.Visibility = Visibility.Collapsed;
        }
    }
}
