using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Background.Loaders;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ImageUI = Microsoft.UI.Xaml.Controls.Image;
// ReSharper disable PartialTypeWithSinglePart

#nullable enable
namespace CollapseLauncher.Helper.Background
{
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    internal sealed partial class BackgroundMediaUtility : IDisposable
    {
        internal enum MediaType
        {
            Media,
            StillImage,
            Unknown
        }

        internal const double TransitionDuration     = 0.25d;
        internal const double TransitionDurationSlow = 0.5d;

        internal static readonly string[] SupportedImageExt =
            [".jpg", ".jpeg", ".jfif", ".png", ".bmp", ".tiff", ".tif", ".webp"];

        internal static readonly string[] SupportedMediaPlayerExt =
            [".mp4", ".mov", ".mkv", ".webm", ".avi", ".gif"];

        private static FrameworkElement?   _parentUI;
        private ImageUI?            _bgImageBackground;
        private ImageUI?            _bgImageBackgroundLast;
        private MediaPlayerElement? _bgMediaPlayerBackground;

        private Grid? _bgAcrylicMask;
        private Grid? _bgOverlayTitleBar;

        private Grid? _parentBgImageBackgroundGrid;
        private Grid? _parentBgMediaPlayerBackgroundGrid;

        internal static string?   CurrentAppliedMediaPath;
        internal static MediaType CurrentAppliedMediaType = MediaType.Unknown;

        private CancellationTokenSourceWrapper? _cancellationToken;
        private StillImageLoader?               _loaderStillImage;
        private MediaPlayerLoader?              _loaderMediaPlayer;

        private bool _isCurrentRegistered;

        private static FileStream? _alternativeFileStream;

        private   delegate ValueTask          AssignDefaultAction<in T>(T element) where T : class;
        internal  delegate void               ThrowExceptionAction(Exception element);
        internal  static   ActionBlock<Task>? SharedActionBlockQueue = new(async (action) =>
                                                                           {
                                                                               try
                                                                               {
                                                                                   await action;
                                                                               }
                                                                               catch (Exception ex)
                                                                               {
                                                                                   _parentUI?.DispatcherQueue.TryEnqueue(() =>
                                                                                                                             ErrorSender.SendException(ex));
                                                                               }
                                                                           },
                                                                           new ExecutionDataflowBlockOptions
                                                                           {
                                                                               EnsureOrdered = true,
                                                                               MaxMessagesPerTask = 1,
                                                                               MaxDegreeOfParallelism = 1,
                                                                               BoundedCapacity = 1,
                                                                               TaskScheduler = TaskScheduler.Current
                                                                           });
        internal ActionBlock<Action> SharedActionBlockQueueChange = new(static (action) =>
                                                                        {
                                                                            try
                                                                            {
                                                                                _parentUI?.DispatcherQueue.TryEnqueue(() => action());
                                                                            }
                                                                            catch (Exception ex)
                                                                            {
                                                                                _parentUI?.DispatcherQueue.TryEnqueue(() =>
                                                                                                                          ErrorSender.SendException(ex));
                                                                            }
                                                                        },
                                                                        new ExecutionDataflowBlockOptions
                                                                        {
                                                                            EnsureOrdered = true,
                                                                            MaxMessagesPerTask = 1,
                                                                            MaxDegreeOfParallelism = 1,
                                                                            BoundedCapacity = 1
                                                                        });

        /// <summary>
        ///     Attach and register the <see cref="Grid" /> of the page to be assigned with background utility.
        ///     The <see cref="Grid" /> must be empty or have existing instances of previously registered <see cref="Grid" />.
        /// </summary>
        /// <param name="parentUI">The parent UI to be assigned for the media elements.</param>
        /// <param name="bgAcrylicMask">The acrylic mask for Background Image.</param>
        /// <param name="bgOverlayTitleBar">The title bar shadow over Background Image.</param>
        /// <param name="bgImageGridBackground">The parent <see cref="Grid" /> for Background Image.</param>
        /// <param name="bgMediaPlayerGrid">The parent <see cref="Grid" /> for Background Media Player</param>
        internal static async Task<BackgroundMediaUtility> CreateInstanceAsync(FrameworkElement? parentUI,          Grid bgAcrylicMask,
                                                                               Grid              bgOverlayTitleBar, Grid bgImageGridBackground,
                                                                               Grid              bgMediaPlayerGrid)
        {
            CurrentAppliedMediaPath = null;
            CurrentAppliedMediaType = MediaType.Unknown;
            if (_alternativeFileStream != null)
            {
                await _alternativeFileStream.DisposeAsync();
                _alternativeFileStream = null;
            }

            // Set the parent UI
            FrameworkElement? ui = parentUI;

            // Initialize the background instances
            var (bgImageBackground, bgImageBackgroundLast) =
                await InitializeElementGrid<ImageUI>(bgImageGridBackground, "ImageBackground", AssignDefaultImage);
            var bgMediaPlayerBackground =
                (await TryGetFirstGridElement<MediaPlayerElement>(bgMediaPlayerGrid.WithOpacity(0), "MediaPlayer"))
               .WithHorizontalAlignment(HorizontalAlignment.Center)
               .WithVerticalAlignment(VerticalAlignment.Center)
               .WithStretch(Stretch.UniformToFill);

            return new BackgroundMediaUtility(ui, bgAcrylicMask, bgOverlayTitleBar,
                                              bgImageGridBackground, bgMediaPlayerGrid, bgImageBackground,
                                              bgImageBackgroundLast, bgMediaPlayerBackground);
        }

        private BackgroundMediaUtility(FrameworkElement? parentUI,              Grid bgAcrylicMask,
                                       Grid              bgOverlayTitleBar,     Grid bgImageGridBackground,
                                       Grid              bgMediaPlayerGrid,     ImageUI? bgImageBackground,
                                       ImageUI?          bgImageBackgroundLast, MediaPlayerElement? mediaPlayerElement)
        {
            _parentUI                          = parentUI;
            _bgAcrylicMask                     = bgAcrylicMask;
            _bgOverlayTitleBar                 = bgOverlayTitleBar;
            _parentBgImageBackgroundGrid       = bgImageGridBackground;
            _parentBgMediaPlayerBackgroundGrid = bgMediaPlayerGrid;

            _bgImageBackground       = bgImageBackground;
            _bgImageBackgroundLast   = bgImageBackgroundLast;
            _bgMediaPlayerBackground = mediaPlayerElement;

            // Set that the current page has been registered
            _isCurrentRegistered = true;
        }

        ~BackgroundMediaUtility() => Dispose();

        /// <summary>
        ///     Detach and dispose the current background utility from the previously attached <see cref="Grid" />.
        /// </summary>
        public void Dispose()
        {
            if (_cancellationToken is { IsCancellationRequested: false })
            {
                _cancellationToken.Cancel();
            }
            _cancellationToken?.Dispose();

            _bgImageBackground       = null;
            _bgImageBackgroundLast   = null;
            _bgMediaPlayerBackground = null;

            _parentBgImageBackgroundGrid       = null;
            _parentBgMediaPlayerBackgroundGrid = null;
            _bgAcrylicMask                     = null;
            _bgOverlayTitleBar                 = null;

            _loaderMediaPlayer?.Dispose();
            _loaderMediaPlayer = null;
            _loaderStillImage?.Dispose();
            _loaderStillImage  = null;

            _alternativeFileStream?.Dispose();
            _alternativeFileStream = null;

            _isCurrentRegistered = false;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Initialize or to find an existing element by its base tag and type.
        /// </summary>
        /// <typeparam name="TElement">
        ///     The type of the element need to be created (where <c>TElement</c> is a member of
        ///     <c>FrameworkElement</c>)
        /// </typeparam>
        /// <param name="elementGrid">The parent <c>Grid</c> where the element is going to be or has stored.</param>
        /// <param name="baseTagType">The base tag to determine the element type.</param>
        /// <param name="defaultAssignAction">The delegate to perform action after the new element is created.</param>
        /// <returns>The tuple of the new "current and last" instance of the element.</returns>
        private static async ValueTask<(TElement?, TElement?)> InitializeElementGrid<TElement>(Grid elementGrid,
                                                                                               string baseTagType, AssignDefaultAction<TElement>? defaultAssignAction = null)
            where TElement : FrameworkElement, new()
        {
            // Get the type name of the element
            string typeName = typeof(TElement).Name + '_';

            // Find or create the element from/to the parent grid.
            TElement? elementCurrent =
                (await TryGetFirstGridElement(elementGrid, typeName + baseTagType + "_Current", defaultAssignAction))
               .WithHorizontalAlignment(HorizontalAlignment.Center)
               .WithVerticalAlignment(VerticalAlignment.Bottom)
               .WithStretch(Stretch.UniformToFill);
            TElement? elementLast =
                (await TryGetFirstGridElement(elementGrid, typeName + baseTagType + "_Last", defaultAssignAction))
               .WithHorizontalAlignment(HorizontalAlignment.Center)
               .WithVerticalAlignment(VerticalAlignment.Bottom)
               .WithStretch(Stretch.UniformToFill);

            // Return the current and last element
            return (elementCurrent, elementLast);
        }

        /// <summary>
        ///     Try to get the first element or create a new one from the parent <c>Grid</c> based on its tag type.
        /// </summary>
        /// <typeparam name="T">The type of the element to get (where <c>T</c> is a member of <c>FrameworkElement</c>)</typeparam>
        /// <param name="elementGrid">The parent <c>Grid</c> to get the element from.</param>
        /// <param name="tagType">The exact tag to determine the element type.</param>
        /// <param name="defaultAssignAction">The delegate to perform action after the new element is created.</param>
        /// <returns>Returns <c>null</c> if the <c>Grid</c> is null. Returns the new or existing element from the <c>Grid</c>.</returns>
        private static async ValueTask<T?> TryGetFirstGridElement<T>(Grid? elementGrid, string tagType,
                                                                     AssignDefaultAction<T>? defaultAssignAction = null)
            where T : FrameworkElement, new()
        {
            // If the parent grid is null, then return null
            if (elementGrid == null) return null;

            // Try to find the existing element at the first pos of the
            // parent's grid with the corresponding tag. If not found,
            // assign it as null instead
            T? targetElement = elementGrid.Children
                                          .OfType<T>()
                                          .FirstOrDefault(x => x.Tag is string tagString && tagString == tagType);

            // If the existing element is not found, then starts
            // create a new one
            if (targetElement != null) return targetElement; // Return an existing instance from the parent grid

            // Create a new instance of the element and add it into parent grid
            T newElement = elementGrid.AddElementToGridRowColumn(new T());
            newElement.SetTag(tagType); // Set element tag

            // If an "assign action" delegate is defined, then execute the delegate
            if (defaultAssignAction != null)
            {
                await defaultAssignAction(newElement);
            }

            // Return a new instance of the element
            return newElement;
        }

        /// <summary>
        ///     Assign the default.png image to the new <c>Image</c> instance.
        /// </summary>
        /// <typeparam name="TElement">Type of <c>Image</c>.</typeparam>
        /// <param name="element">Instance of an <c>Image</c>.</param>
        internal static async ValueTask AssignDefaultImage<TElement>(TElement element)
        {
            // If the element type is an "Image" type, then proceed
            if (element is ImageUI image)
            {
                // Get the default.png path and check if it exists
                string filePath = LauncherConfig.AppDefaultBG;
                if (!File.Exists(filePath))
                {
                    return;
                }

                // Read the default.png image and load it to
                // the image element.
                await using FileStream fileStream =
                    new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 << 10, true);
                BitmapImage imageSource = new BitmapImage();
                await imageSource.SetSourceAsync(fileStream.AsRandomAccessStream());
                image.Source = imageSource;
            }
        }

        /// <summary>
        ///     Ensure that the <see cref="ImageUI" /> instance is already initialized
        /// </summary>
        /// <exception cref="ArgumentNullException">Throw if <see cref="ImageUI" /> instance is not registered</exception>
        private void EnsureCurrentImageRegistered()
        {
            if (_bgImageBackground == null || _bgImageBackgroundLast == null)
            {
                throw new NullReferenceException("bgImageGridBackground instance is null");
            }
        }

        /// <summary>
        ///     Ensure that the <see cref="MediaPlayerElement" /> instance is already initialized
        /// </summary>
        /// <exception cref="ArgumentNullException">Throw if <see cref="MediaPlayerElement" /> instance is not registered</exception>
        private void EnsureCurrentMediaPlayerRegistered()
        {
            if (_bgMediaPlayerBackground == null)
            {
                throw new NullReferenceException("bgMediaPlayerGrid instance is null");
            }
        }

        /// <summary>
        ///     Load Still Image or Video as a background.
        /// </summary>
        /// <param name="mediaPath">Path of the background file</param>
        /// <param name="isRequestInit">Request an initialization before processing the background file</param>
        /// <param name="isForceRecreateCache">Request a cache recreation if the background file properties have been cached</param>
        /// <param name="throwAction">Action to do after exception occurred</param>
        /// <param name="actionAfterLoaded">Action to do after background is loaded</param>
        /// <exception cref="FormatException">Throws if the background file is not supported</exception>
        /// <exception cref="NullReferenceException">Throws if some instances aren't yet initialized</exception>
        internal async void LoadBackground(string                mediaPath,
                                           bool                  isRequestInit        = false,
                                           bool                  isForceRecreateCache = false,
                                           ThrowExceptionAction? throwAction          = null,
                                           Action?               actionAfterLoaded    = null)
        {
            while (!await SharedActionBlockQueue?.SendAsync(LoadBackgroundInner(mediaPath, isRequestInit, isForceRecreateCache, throwAction, actionAfterLoaded))!)
            {
                // Delay the invoke 1/4 second and wait until the action can
                // be sent again.
                await Task.Delay(250);
            }
        }

        private async Task LoadBackgroundInner(string mediaPath,                  bool                  isRequestInit = false,
                                               bool isForceRecreateCache = false, ThrowExceptionAction? throwAction = null,
                                               Action? actionAfterLoaded = null)
        {
            if (mediaPath.Equals(CurrentAppliedMediaPath, StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                while (!_isCurrentRegistered)
                {
                    await Task.Delay(250, _cancellationToken?.Token ?? default);
                }

                EnsureCurrentImageRegistered();
                EnsureCurrentMediaPlayerRegistered();

                _loaderMediaPlayer ??= new MediaPlayerLoader(
                                                             _parentUI!,
                                                             _bgAcrylicMask!, _bgOverlayTitleBar!,
                                                             _parentBgMediaPlayerBackgroundGrid!,
                                                             _bgMediaPlayerBackground);

                _loaderStillImage ??= new StillImageLoader(
                                                           _parentUI!,
                                                           _bgAcrylicMask!, _bgOverlayTitleBar!,
                                                           _parentBgImageBackgroundGrid!,
                                                           _bgImageBackground, _bgImageBackgroundLast);

                MediaType mediaType = GetMediaType(mediaPath);

                if (_cancellationToken is { IsDisposed: false })
                {
                    if (!_cancellationToken.IsCancelled)
                    {
                        await _cancellationToken.CancelAsync();
                    }

                    _cancellationToken.Dispose();
                }

                _cancellationToken = new CancellationTokenSourceWrapper();
                await (mediaType switch
                {
                    MediaType.Media => _loaderMediaPlayer,
                    MediaType.StillImage => _loaderStillImage as IBackgroundMediaLoader,
                    _ => throw new InvalidCastException()
                }).LoadAsync(mediaPath, isForceRecreateCache, isRequestInit, _cancellationToken.Token);

                switch (mediaType)
                {
                    case MediaType.Media:
                        _loaderStillImage?.Hide();
                        _loaderMediaPlayer?.Show();
                        break;
                    case MediaType.StillImage:
                        _loaderStillImage?.Show(CurrentAppliedMediaType == MediaType.Media
                            || InnerLauncherConfig.m_appCurrentFrameName != "HomePage");
                        _loaderMediaPlayer?.Hide();
                        break;
                }

                if (InnerLauncherConfig.m_appCurrentFrameName != "HomePage")
                {
                    if (_loaderMediaPlayer != null) _loaderMediaPlayer.IsBackgroundDimm = true;
                    if (_loaderStillImage != null) _loaderStillImage.IsBackgroundDimm = true;
                }

                CurrentAppliedMediaType = mediaType;
                actionAfterLoaded?.Invoke();

                CurrentAppliedMediaPath = mediaPath;
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                throwAction?.Invoke(ex);
            }
        }

        /// <summary>
        ///     Dimming the current loaded background
        /// </summary>
        internal void Dimm()
        {
            _loaderMediaPlayer?.Dimm();
            _loaderStillImage?.Dimm();
        }

        /// <summary>
        ///     Undimming the current loaded background
        /// </summary>
        internal void Undimm()
        {
            _loaderMediaPlayer?.Undimm();
            _loaderStillImage?.Undimm();
        }

        /// <summary>
        ///     Mute the audio of the currently loaded background
        /// </summary>
        internal void Mute()
        {
            _loaderMediaPlayer?.Mute();
            _loaderStillImage?.Mute();
        }

        /// <summary>
        ///     Unmute the audio of the currently loaded background
        /// </summary>
        internal void Unmute()
        {
            _loaderMediaPlayer?.Unmute();
            _loaderStillImage?.Unmute();
        }

        /// <summary>
        ///     Set the volume of the audio from the currently loaded background
        /// </summary>
        internal void SetVolume(double value)
        {
            _loaderMediaPlayer?.SetVolume(value);
            _loaderStillImage?.SetVolume(value);
        }

        /// <summary>
        ///     Trigger the unfocused window event to the currently loaded background
        /// </summary>
        internal void WindowUnfocused()
        {
            _loaderMediaPlayer?.WindowUnfocused();
            _loaderStillImage?.WindowUnfocused();
        }

        /// <summary>
        ///     Trigger the focused window event to the currently loaded background
        /// </summary>
        internal void WindowFocused()
        {
            _loaderMediaPlayer?.WindowFocused();
            _loaderStillImage?.WindowFocused();
        }

        /// <summary>
        ///     Play/Resume the currently loaded background
        /// </summary>
        internal void Play()
        {
            _loaderMediaPlayer?.Play();
            _loaderStillImage?.Play();
        }

        /// <summary>
        ///     Pause the currently loaded background
        /// </summary>
        internal void Pause()
        {
            _loaderMediaPlayer?.Pause();
            _loaderStillImage?.Pause();
        }

        public static FileStream? GetAlternativeFileStream()
        {
            FileStream? returnStream = _alternativeFileStream;
            _alternativeFileStream = null;
            return returnStream;
        }

        public static void SetAlternativeFileStream(FileStream stream)
        {
            _alternativeFileStream = stream;
        }

        public static MediaType GetMediaType(string mediaPath)
        {
            string extension = Path.GetExtension(mediaPath);
            if (SupportedImageExt.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return MediaType.StillImage;
            }

            return SupportedMediaPlayerExt.Contains(extension, StringComparer.OrdinalIgnoreCase) ? MediaType.Media : MediaType.Unknown;
        }
    }
}
