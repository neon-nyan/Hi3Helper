using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.StreamUtility;
using CommunityToolkit.WinUI.Animations;
#if USEFFMPEGFORVIDEOBG
using FFmpegInteropX;
#endif
using Hi3Helper;
using Hi3Helper.Shared.Region;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Windows.Graphics.Imaging;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;
using Hi3Helper.SentryHelper;
using ImageUI = Microsoft.UI.Xaml.Controls.Image;
using static Hi3Helper.Logger;
// ReSharper disable PartialTypeWithSinglePart

#nullable enable
namespace CollapseLauncher.Helper.Background.Loaders
{
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    internal sealed partial class MediaPlayerLoader : IBackgroundMediaLoader
    {
    #pragma warning disable CS0169 // Field is never used
        private bool _isFocusChangeRunning;
    #pragma warning restore CS0169 // Field is never used

        private FrameworkElement ParentUI          { get; }
        private Compositor       CurrentCompositor { get; }

        private        MediaPlayerElement? CurrentMediaPlayerFrame           { get; }
        private        Grid                CurrentMediaPlayerFrameParentGrid { get; }
        private static bool                IsUseVideoBgDynamicColorUpdate    { get => LauncherConfig.IsUseVideoBGDynamicColorUpdate && LauncherConfig.EnableAcrylicEffect; }

        private Grid AcrylicMask     { get; }
        private Grid OverlayTitleBar { get; }
        
        public   bool                            IsBackgroundDimm       { get; set; }
        private  FileStream?                     CurrentMediaStream     { get; set; }
        private  MediaPlayer?                    CurrentMediaPlayer     { get; set; }
#if USEFFMPEGFORVIDEOBG
        private  FFmpegMediaSource?              CurrentFFmpegMediaSource { get; set; }
#endif

        private ImageUI?           CurrentMediaImage        { get; }
        private SoftwareBitmap?    CurrentFrameBitmap       { get; set; }
        private CanvasImageSource? CurrentCanvasImageSource { get; set; }
        private CanvasDevice?      CanvasDevice             { get; set; }
        private CanvasBitmap?      CanvasBitmap             { get; set; }
        private bool               IsCanvasCurrentlyDrawing { get; set; }

        internal MediaPlayerLoader(
            FrameworkElement parentUI,
            Grid             acrylicMask,           Grid                overlayTitleBar,
            Grid             mediaPlayerParentGrid, MediaPlayerElement? mediaPlayerCurrent)
        {
            ParentUI          = parentUI;
            CurrentCompositor = parentUI.GetElementCompositor();

            AcrylicMask     = acrylicMask;
            OverlayTitleBar = overlayTitleBar;

            CurrentMediaPlayerFrameParentGrid = mediaPlayerParentGrid;
            CurrentMediaPlayerFrame           = mediaPlayerCurrent;

            CurrentMediaImage = mediaPlayerParentGrid.AddElementToGridRowColumn(new ImageUI()
                                                                                   .WithHorizontalAlignment(HorizontalAlignment
                                                                                       .Center)
                                                                                   .WithVerticalAlignment(VerticalAlignment
                                                                                       .Center)
                                                                                   .WithStretch(Stretch
                                                                                       .UniformToFill));
        }

        ~MediaPlayerLoader()
        {
            LogWriteLine("[~MediaPlayerLoader()] MediaPlayerLoader Deconstructor has been called!", LogType.Warning, true);
            Dispose();
        }

        public void Dispose()
        {
            try
            {
                DisposeMediaModules();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error disposing Media Modules: {ex.Message}", LogType.Error, true);
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
            }

            GC.SuppressFinalize(this);
        }

        public async Task LoadAsync(string filePath,      bool              isImageLoadForFirstTime,
                                    bool   isRequestInit, CancellationToken token)
        {
            try
            {
                DisposeMediaModules();

                int canvasWidth  = (int)CurrentMediaPlayerFrameParentGrid.ActualWidth;
                int canvasHeight = (int)CurrentMediaPlayerFrameParentGrid.ActualHeight;

                if (IsUseVideoBgDynamicColorUpdate)
                {
                    CanvasDevice ??= CanvasDevice.GetSharedDevice();
                    CurrentFrameBitmap ??= new SoftwareBitmap(BitmapPixelFormat.Rgba8, canvasWidth, canvasHeight,
                                                              BitmapAlphaMode.Ignore);
                    CurrentCanvasImageSource ??=
                            new CanvasImageSource(CanvasDevice, canvasWidth, canvasHeight, 96, CanvasAlphaMode.Ignore);

                    CurrentMediaImage!.Source = CurrentCanvasImageSource;
                    CurrentMediaImage.Visibility = Visibility.Visible;
                    App.ToggleBlurBackdrop();
                }
                else if (CurrentMediaImage != null)
                {
                    CurrentMediaImage.Visibility = Visibility.Collapsed;
                    
                }

                await GetPreviewAsColorPalette(filePath);

                CurrentMediaStream ??= BackgroundMediaUtility.GetAlternativeFileStream() ?? File.Open(filePath, StreamExtension.FileStreamOpenReadOpt);

#if !USEFFMPEGFORVIDEOBG
                EnsureIfFormatIsDashOrUnsupported(CurrentMediaStream);
#endif
                CurrentMediaPlayer ??= new MediaPlayer();

                if (WindowUtility.IsCurrentWindowInFocus())
                {
                    CurrentMediaPlayer.AutoPlay = true;
                }

                bool   isAudioMute     = LauncherConfig.GetAppConfigValue("BackgroundAudioIsMute").ToBool();
                double lastAudioVolume = LauncherConfig.GetAppConfigValue("BackgroundAudioVolume").ToDouble();

                CurrentMediaPlayer.IsMuted          = isAudioMute;
                CurrentMediaPlayer.Volume           = lastAudioVolume;
                CurrentMediaPlayer.IsLoopingEnabled = true;

#if !USEFFMPEGFORVIDEOBG
                CurrentMediaPlayer.SetStreamSource(CurrentMediaStream.AsRandomAccessStream());
#else

                CurrentFFmpegMediaSource ??= await FFmpegMediaSource.CreateFromStreamAsync(CurrentMediaStream.AsRandomAccessStream());

                await CurrentFFmpegMediaSource.OpenWithMediaPlayerAsync(CurrentMediaPlayer);
                const string MediaInfoStrFormat = @"Playing background video with FFmpeg!
    Media Duration: {0}
    Video Resolution: {9}x{10} px
    Video Codec: {1}
    Video Codec Decoding Method: {3}
    Video Decoder Engine: {11}
    Video Bitrate: {2} bps
    Video Bitdepth: {11} Bits
    Audio Codec: {4}
    Audio Bitrate: {5} bps
    Audio Channel: {6}
    Audio Sample: {7}Hz
    Audio Bitwide: {8} Bits
";
                Logger.LogWriteLine(
                    string.Format(MediaInfoStrFormat,
                    CurrentFFmpegMediaSource.Duration.ToString("c"),                                // 0
                    CurrentFFmpegMediaSource.CurrentVideoStream?.CodecName ?? "No Video Stream",    // 1
                    CurrentFFmpegMediaSource.CurrentVideoStream?.Bitrate ?? 0,                      // 2
                    CurrentFFmpegMediaSource.CurrentVideoStream?.DecoderEngine                      // 3
                    == DecoderEngine.FFmpegD3D11HardwareDecoder ? "Hardware" : "Software",
                    CurrentFFmpegMediaSource.CurrentAudioStream?.CodecName ?? "No Audio Stream",    // 4
                    CurrentFFmpegMediaSource.CurrentAudioStream?.Bitrate ?? 0,                      // 5
                    CurrentFFmpegMediaSource.CurrentAudioStream?.Channels ?? 0,                     // 6
                    CurrentFFmpegMediaSource.CurrentAudioStream?.SampleRate ?? 0,                   // 7
                    CurrentFFmpegMediaSource.CurrentAudioStream?.BitsPerSample ?? 0,                // 8
                    CurrentFFmpegMediaSource.CurrentVideoStream?.PixelWidth ?? 0,                   // 9
                    CurrentFFmpegMediaSource.CurrentVideoStream?.PixelHeight ?? 0,                  // 10
                    CurrentFFmpegMediaSource.CurrentVideoStream?.BitsPerSample ?? 0,                // 11
                    CurrentFFmpegMediaSource.CurrentVideoStream?.DecoderEngine ?? 0                 // 12
                    ), LogType.Debug, true);
#endif
                CurrentMediaPlayer.IsVideoFrameServerEnabled = IsUseVideoBgDynamicColorUpdate;
                if (IsUseVideoBgDynamicColorUpdate)
                {
                    CurrentMediaPlayer.VideoFrameAvailable += FrameGrabberEvent;
                }

                CurrentMediaPlayerFrame?.SetMediaPlayer(CurrentMediaPlayer);
                CurrentMediaPlayer.Play();
            }
            catch
            {
                DisposeMediaModules();
                await BackgroundMediaUtility.AssignDefaultImage(CurrentMediaImage);
                throw;
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        public void DisposeMediaModules()
        {
            if (CurrentMediaPlayer != null)
            {
                CurrentMediaPlayer.VideoFrameAvailable -= FrameGrabberEvent;
            }

            if (IsUseVideoBgDynamicColorUpdate)
            {
                while (IsCanvasCurrentlyDrawing)
                {
                    Thread.Sleep(100);
                }

                CanvasDevice?.Dispose();
                CanvasDevice = null;
                CurrentFrameBitmap?.Dispose();
                CurrentFrameBitmap = null;
                CanvasBitmap?.Dispose();
                CanvasBitmap = null;
            }

#if USEFFMPEGFORVIDEOBG
            CurrentFFmpegMediaSource?.Dispose();
            CurrentFFmpegMediaSource = null;
#endif
            CurrentMediaPlayer?.Dispose();
            CurrentMediaPlayer = null;
            CurrentCanvasImageSource = null;
            CurrentMediaStream?.Dispose();
            CurrentMediaStream = null;
        }

#if !USEFFMPEGFORVIDEOBG
        private void EnsureIfFormatIsDashOrUnsupported(Stream stream)
        {
            ReadOnlySpan<byte> dashSignature = "ftypdash"u8;

            byte[] buffer = new byte[64];
            stream.ReadExactly(buffer);

            try
            {
                if (buffer.AsSpan(4).StartsWith(dashSignature))
                    throw new FormatException("The video format is in \"MPEG-DASH\" format, which is unsupported.");
            }
            finally
            {
                stream.Position = 0;
            }
        }
#endif

        private async ValueTask GetPreviewAsColorPalette(string file)
        {
            StorageFile                storageFile = await GetFileAsStorageFile(file);
            using StorageItemThumbnail thumbnail   = await storageFile.GetThumbnailAsync(ThumbnailMode.VideosView);
            await using Stream         stream      = thumbnail.AsStream();

            await ColorPaletteUtility.ApplyAccentColor(ParentUI, stream.AsRandomAccessStream(), string.Empty, false,
                                                       true);
        }

        private static async ValueTask<StorageFile> GetFileAsStorageFile(string filePath)
            => await StorageFile.GetFileFromPathAsync(filePath);

        private void FrameGrabberEvent(MediaPlayer mediaPlayer, object args)
        {
            IsCanvasCurrentlyDrawing = true;

            if (CurrentCanvasImageSource == null)
            {
                IsCanvasCurrentlyDrawing = false;
                return;
            }

            lock (this)
            {
                CurrentCanvasImageSource?.DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        // Check one more time due to high possibility of thread-race issue.
                        if (CurrentCanvasImageSource == null)
                            return;

                        CanvasBitmap ??= CanvasBitmap.CreateFromSoftwareBitmap(CanvasDevice, CurrentFrameBitmap);
                        using CanvasDrawingSession canvasDrawingSession = CurrentCanvasImageSource.CreateDrawingSession(Color.FromArgb(0, 0, 0, 0));

                        mediaPlayer.CopyFrameToVideoSurface(CanvasBitmap);
                        canvasDrawingSession.DrawImage(CanvasBitmap);
                    }
                    catch (Exception e)
                    {
                        LogWriteLine($"[FrameGrabberEvent] Error drawing frame to canvas.\r\n{e}", LogType.Error, true);
                    }
                });
            }
            IsCanvasCurrentlyDrawing = false;
        }

        public void Dimm()
        {
            BackgroundMediaUtility.SharedActionBlockQueue?.Post(ToggleImageVisibility(true));
        }

        public void Undimm()
        {
            BackgroundMediaUtility.SharedActionBlockQueue?.Post(ToggleImageVisibility(false));
        }

        private async Task ToggleImageVisibility(bool hideImage)
        {
            if (IsBackgroundDimm == hideImage) return;
            IsBackgroundDimm = hideImage;

            TimeSpan duration = TimeSpan.FromSeconds(hideImage
                                                         ? BackgroundMediaUtility.TransitionDuration
                                                         : BackgroundMediaUtility.TransitionDurationSlow);
            await Task.WhenAll(
                AcrylicMask.StartAnimation(
                    duration,
                    CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity",
                        hideImage ? 1f : 0f,
                        hideImage ? 0f : 1f)
                    ),
                OverlayTitleBar.StartAnimation(
                    duration,
                    CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity",
                        hideImage ? 0f : 1f,
                        hideImage ? 1f : 0f)
                    )
                );
        }

        public void Show(bool isForceShow = false)
        {
            BackgroundMediaUtility.SharedActionBlockQueue?.Post(ShowInner());
        }

        private async Task ShowInner()
        {
            if (CurrentMediaPlayerFrameParentGrid.Opacity > 0f) return;

            if (!IsUseVideoBgDynamicColorUpdate)
            {
                App.ToggleBlurBackdrop(false);
            }
            TimeSpan duration = TimeSpan.FromSeconds(BackgroundMediaUtility.TransitionDuration);

            await CurrentMediaPlayerFrameParentGrid
               .StartAnimation(duration,
                               CurrentCompositor
                                  .CreateScalarKeyFrameAnimation("Opacity", 1f, 0f)
                              );
        }

        public void Hide()
        {
            BackgroundMediaUtility.SharedActionBlockQueue?.Post(HideInner());
        }

        private async Task HideInner()
        {
            bool isLastAcrylicEnabled = LauncherConfig.GetAppConfigValue("EnableAcrylicEffect").ToBool();

            if (!IsUseVideoBgDynamicColorUpdate)
            {
                App.ToggleBlurBackdrop(isLastAcrylicEnabled);
            }

            if (CurrentMediaPlayerFrameParentGrid.Opacity < 1f) return;
            TimeSpan duration = TimeSpan.FromSeconds(BackgroundMediaUtility.TransitionDuration);

            await CurrentMediaPlayerFrameParentGrid
               .StartAnimation(duration,
                               CurrentCompositor
                                  .CreateScalarKeyFrameAnimation("Opacity", 0f,
                                                                 (float)CurrentMediaPlayerFrameParentGrid
                                                                    .Opacity)
                              );

            DisposeMediaModules();
        }

        public async void WindowUnfocused()
        {
            try
            {
                await (BackgroundMediaUtility.SharedActionBlockQueue?.SendAsync(WindowUnfocusedInner()) ?? Task.CompletedTask);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private async Task WindowUnfocusedInner()
        {
            double currentAudioVolume = CurrentMediaPlayer?.Volume ?? 0;
            await InterpolateVolumeChange((float)currentAudioVolume, 0f, true);
            Pause();
        }

        public async void WindowFocused()
        {
            try
            {
                await (BackgroundMediaUtility.SharedActionBlockQueue?.SendAsync(WindowFocusedInner()) ?? Task.CompletedTask);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private async Task WindowFocusedInner()
        {
            double currentAudioVolume = LauncherConfig.GetAppConfigValue("BackgroundAudioVolume")
                                                      .ToDouble();
            Play();
            await InterpolateVolumeChange(0f, (float)currentAudioVolume, false);
        }

        public void Mute()
        {
            if (CurrentMediaPlayer == null) return;
            CurrentMediaPlayer.IsMuted = true;
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioIsMute", true);
        }

        public void Unmute()
        {
            if (CurrentMediaPlayer == null) return;

            CurrentMediaPlayer.IsMuted = false;
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioIsMute", false);
        }

        private async ValueTask InterpolateVolumeChange(float from, float to, bool isMute)
        {
            if (CurrentMediaPlayer == null) return;

            double tFrom = from;
            double tTo   = to;

            double current = tFrom;
            double inc     = isMute ? -0.05 : 0.05;

            Loops:
            current                   += inc;
            CurrentMediaPlayer.Volume =  current;

            await Task.Delay(10);
            switch (isMute)
            {
                case true when current > tTo - inc:
                case false when current < tTo - inc:
                    goto Loops;
            }

            CurrentMediaPlayer.Volume = tTo;
        }

        public void SetVolume(double value)
        {
            if (CurrentMediaPlayer != null)
                CurrentMediaPlayer.Volume = value;
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioVolume", value);
        }

        public void Play()
        {
            CurrentMediaPlayer?.Play();
        }

        public void Pause()
        {
            CurrentMediaPlayer?.Pause();
        }
    }
}