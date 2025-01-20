using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Image;
using CommunityToolkit.WinUI.Animations;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ImageUI = Microsoft.UI.Xaml.Controls.Image;
// ReSharper disable PartialTypeWithSinglePart

#nullable enable
namespace CollapseLauncher.Helper.Background.Loaders
{
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    internal sealed partial class StillImageLoader : IBackgroundMediaLoader
    {
        private FrameworkElement ParentUI            { get; }
        private Compositor       CurrentCompositor   { get; }
        private ImageUI?         ImageBackCurrent    { get; }
        private ImageUI?         ImageBackLast       { get; }
        private Grid?            ImageBackParentGrid { get; }
        private Grid             AcrylicMask         { get; }
        private Grid             OverlayTitleBar     { get; }
        private double           AnimationDuration   { get; }

        public bool IsBackgroundDimm
        {
            get; 
            set;
        }

        internal StillImageLoader(
            FrameworkElement parentUI,
            Grid             acrylicMask, Grid overlayTitleBar,
            Grid             imageBackParentGrid,
            ImageUI?         imageBackCurrent, ImageUI? imageBackLast,
            double           animationDuration = BackgroundMediaUtility.TransitionDuration)
        {
            GC.SuppressFinalize(this);
            ParentUI          = parentUI;
            CurrentCompositor = parentUI.GetElementCompositor();

            AcrylicMask     = acrylicMask;
            OverlayTitleBar = overlayTitleBar;

            ImageBackCurrent    = imageBackCurrent;
            ImageBackLast       = imageBackLast;
            ImageBackParentGrid = imageBackParentGrid;

            AnimationDuration = animationDuration;
        }

        ~StillImageLoader() => Dispose();

        public void Dispose()
        {
            GC.Collect();
            GC.SuppressFinalize(this);
        }

        public async Task LoadAsync(string filePath,      bool              isImageLoadForFirstTime,
                                    bool   isRequestInit, CancellationToken token)
        {
            try
            {
                // Get the image stream
                token.ThrowIfCancellationRequested();
                await using FileStream? imageStream = BackgroundMediaUtility.GetAlternativeFileStream() ??
                                                      await ImageLoaderHelper.LoadImage(filePath, false,
                                                               isImageLoadForFirstTime);
                // Return if the stream is null due to cancellation or an error.
                if (imageStream == null)
                {
                    return;
                }

                BitmapImage bitmapImage =
                    await ImageLoaderHelper.Stream2BitmapImage(imageStream.AsRandomAccessStream());

                await Task.WhenAll([
                    ApplyAndSwitchImage(AnimationDuration, bitmapImage),
                    ColorPaletteUtility.ApplyAccentColor(ParentUI,
                                                         imageStream.AsRandomAccessStream(),
                                                         filePath,
                                                         isImageLoadForFirstTime, false)
                    ]);

            }
            finally
            {
                GC.Collect();
            }
        }

        private async Task ApplyAndSwitchImage(double duration, BitmapImage imageToApply)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(duration);

            ImageBackLast!.Source = ImageBackCurrent!.Source;

            ImageBackCurrent!.Opacity = 0;

            ImageBackLast!.Opacity   = 1;
            ImageBackCurrent!.Source = imageToApply;

            await Task.WhenAll(
                               ImageBackCurrent.StartAnimation(timeSpan,
                                                               CurrentCompositor
                                                                  .CreateScalarKeyFrameAnimation("Opacity",
                                                                       1, 0)),
                               ImageBackLast.StartAnimation(timeSpan,
                                                            CurrentCompositor
                                                               .CreateScalarKeyFrameAnimation("Opacity",
                                                                0, 1, timeSpan * 0.5))
                              );
        }

        public void Dimm()
        {
            BackgroundMediaUtility.SharedActionBlockQueue?.Post(ToggleImageVisibility(true));
        }

        public void Undimm()
        {
            BackgroundMediaUtility.SharedActionBlockQueue?.Post(ToggleImageVisibility(false));
        }

        private async Task ToggleImageVisibility(bool hideImage, bool completeInvisible = false, bool isForceShow = false)
        {
            if (isForceShow)
            {
                hideImage = false;
                completeInvisible = false;
            }
            else
            {
                if (IsBackgroundDimm == hideImage) return;
                IsBackgroundDimm = hideImage;
            }

            TimeSpan duration = TimeSpan.FromSeconds(hideImage
                                                         ? BackgroundMediaUtility.TransitionDuration
                                                         : BackgroundMediaUtility.TransitionDurationSlow);

            const float fromScale = 1f;
            Vector3 fromTranslate =
                new Vector3(-((float)(ImageBackParentGrid?.ActualWidth ?? 0) * (fromScale - 1f) / 2),
                    -((float)(ImageBackParentGrid?.ActualHeight ?? 0) * (fromScale - 1f) / 2), 0);
            const float toScale = 1.07f;
            Vector3 toTranslate = new Vector3(-((float)(ImageBackParentGrid?.ActualWidth ?? 0) * (toScale - 1f) / 2),
                -((float)(ImageBackParentGrid?.ActualHeight ?? 0) * (toScale - 1f) / 2), 0);

            if (isForceShow)
            {
                await Task.WhenAll(
                    AcrylicMask.StartAnimation(
                        duration,
                        CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity", hideImage ? 1f : 0f, hideImage ? 0f : 1f)
                        ),
                    OverlayTitleBar.StartAnimation(
                         duration,
                         CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity", hideImage ? 0f : 1f, hideImage ? 1f : 0f)
                        ),
                    ImageBackParentGrid.StartAnimation(
                        duration,
                        CurrentCompositor.CreateVector3KeyFrameAnimation("Scale", new Vector3(hideImage ? toScale : fromScale), new Vector3(!hideImage ? toScale : fromScale)),
                        CurrentCompositor.CreateVector3KeyFrameAnimation("Translation", hideImage ? toTranslate : fromTranslate, !hideImage ? toTranslate : fromTranslate),
                        CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity", 1f, 0f)
                        )
                );
            }
            else if (completeInvisible)
            {
                await Task.WhenAll(
                    ImageBackParentGrid.StartAnimation(
                        duration,
                        CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity", hideImage ? completeInvisible ? 0f : 0.4f : 1f, hideImage ? 1f : 0f)
                    )
                );
            }
            else
            {
                await Task.WhenAll(
                    AcrylicMask.StartAnimation(
                        duration,
                        CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity", hideImage ? 1f : 0f, hideImage ? 0f : 1f)
                        ),
                    OverlayTitleBar.StartAnimation(
                         duration,
                         CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity", hideImage ? 0f : 1f, hideImage ? 1f : 0f)
                        ),
                    ImageBackParentGrid.StartAnimation(
                        duration,
                        CurrentCompositor.CreateVector3KeyFrameAnimation("Scale", new Vector3(hideImage ? toScale : fromScale), new Vector3(!hideImage ? toScale : fromScale)),
                        CurrentCompositor.CreateVector3KeyFrameAnimation("Translation", hideImage ? toTranslate : fromTranslate, !hideImage ? toTranslate : fromTranslate)
                        )
                );
            }
        }

        public void Show(bool isForceShow = false)
        {
            if (ImageBackParentGrid?.Opacity > 0f) return;
            BackgroundMediaUtility.SharedActionBlockQueue?.Post(ToggleImageVisibility(false, true, isForceShow));
        }

        public void Hide()
        {
            if (ImageBackParentGrid?.Opacity < 1f) return;
            BackgroundMediaUtility.SharedActionBlockQueue?.Post(ToggleImageVisibility(true, true));
        }

        public void Mute()
        {
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioIsMute", true);
        }

        public void Unmute()
        {
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioIsMute", false);
        }

        public void SetVolume(double value)
        {
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioVolume", value);
        }

        public void WindowFocused()
        {
        }

        public void WindowUnfocused()
        {
        }

        public void Play()
        {
        }

        public void Pause()
        {
        }
    }
}