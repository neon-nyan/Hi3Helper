using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Background;
using CollapseLauncher.Helper.StreamUtility;
using CommunityToolkit.WinUI.Animations;
using CommunityToolkit.WinUI.Media;
using Hi3Helper;
using Hi3Helper.Data;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using PhotoSauce.MagicScaler;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Hi3Helper.SentryHelper;
using static CollapseLauncher.Helper.Image.Waifu2X;
using static Hi3Helper.Shared.Region.LauncherConfig;
using Orientation = Microsoft.UI.Xaml.Controls.Orientation;

using ImageBlendBrush = Hi3Helper.CommunityToolkit.WinUI.Media.ImageBlendBrush;
using ImageCropper = Hi3Helper.CommunityToolkit.WinUI.Controls.ImageCropper;
using CropShape = Hi3Helper.CommunityToolkit.WinUI.Controls.CropShape;
using ThumbPlacement = Hi3Helper.CommunityToolkit.WinUI.Controls.ThumbPlacement;
using BitmapFileFormat = Hi3Helper.CommunityToolkit.WinUI.Controls.BitmapFileFormat;
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher.Helper.Image
{
    internal static class ImageLoaderHelper
    {
        internal static readonly Dictionary<string, string> SupportedImageFormats =
            new() {
                { "All supported formats", string.Join(';', BackgroundMediaUtility.SupportedImageExt.Select(x => $"*{x}")) + ';' + string.Join(';', BackgroundMediaUtility.SupportedMediaPlayerExt.Select(x => $"*{x}")) },
                { "Image formats", string.Join(';', BackgroundMediaUtility.SupportedImageExt.Select(x => $"*{x}")) },
                { "Video formats", string.Join(';', BackgroundMediaUtility.SupportedMediaPlayerExt.Select(x => $"*{x}")) }
            };

        internal static readonly Dictionary<string, string> SupportedStaticImageFormats = new()
            {
                { "Image formats", string.Join(';', BackgroundMediaUtility.SupportedImageExt.Select(x => $"*{x}")) }
            };

        #region Waifu2X
        private static Waifu2X _waifu2X;
        private static Waifu2XStatus _cachedStatus = Waifu2XStatus.NotInitialized;

        public static Waifu2XStatus Waifu2XStatus
        {
            get
            {
                // Cache the status of waifu2x
                if (_cachedStatus == Waifu2XStatus.NotInitialized)
                {
                    _cachedStatus = VulkanTest();
                }
                return _cachedStatus;
            }
        }

        public static bool IsWaifu2XEnabled
        {
            get => GetAppConfigValue("EnableWaifu2X").ToBool() && IsWaifu2XUsable && _waifu2X != null;
            set
            {
                SetAndSaveConfigValue("EnableWaifu2X", value);
                if (value) InitWaifu2X();
                else DestroyWaifu2X();
            }
        }

        public static bool IsWaifu2XUsable => Waifu2XStatus < Waifu2XStatus.Error;

        private static Waifu2X CreateWaifu2X()
        {
            var waifu2X = new Waifu2X();
            if (waifu2X.Status >= Waifu2XStatus.Error)
            {
                return waifu2X;
            }

            waifu2X.SetParam(Param.Noise, -1);
            waifu2X.SetParam(Param.Scale, 2);
            waifu2X.Load(Path.Combine(AppExecutableDir, @"Assets\Waifu2X_Models\scale2.0x_model.param.bin"),
                         Path.Combine(AppExecutableDir, @"Assets\Waifu2X_Models\scale2.0x_model.bin"));
            _cachedStatus = waifu2X.Status;
            return waifu2X;
        }

        public static void InitWaifu2X()
        {
            _waifu2X ??= CreateWaifu2X();
        }

        public static void DestroyWaifu2X()
        {
            _waifu2X?.Dispose();
            _waifu2X = null;
        }
        #endregion

        internal static async Task<FileStream> LoadImage(string path, bool isUseImageCropper = false, bool overwriteCachedImage = false)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            double aspectRatioX = InnerLauncherConfig.m_actualMainFrameSize.Width;
            double aspectRatioY = InnerLauncherConfig.m_actualMainFrameSize.Height;
            double scaleFactor = WindowUtility.CurrentWindowMonitorScaleFactor;
            uint targetSourceImageWidth = (uint)(aspectRatioX * scaleFactor);
            uint targetSourceImageHeight = (uint)(aspectRatioY * scaleFactor);
            bool isError = false;

            if (!Directory.Exists(AppGameImgCachedFolder)) Directory.CreateDirectory(AppGameImgCachedFolder!);

            FileStream resizedImageFileStream = null;

            try
            {
                FileInfo inputFileInfo = new FileInfo(path);
                FileInfo resizedFileInfo = GetCacheFileInfo(inputFileInfo.FullName + inputFileInfo.Length);
                if (resizedFileInfo!.Exists && resizedFileInfo.Length > 1 << 15 && !overwriteCachedImage)
                {
                    resizedImageFileStream = resizedFileInfo.Open(StreamExtension.FileStreamOpenReadOpt);
                    return resizedImageFileStream;
                }

                if (isUseImageCropper)
                {
                    resizedImageFileStream = await SpawnImageCropperDialog(path, resizedFileInfo.FullName,
                                                                           targetSourceImageWidth, targetSourceImageHeight);
                    return resizedImageFileStream;
                }

                resizedImageFileStream = await GenerateCachedStream(inputFileInfo, targetSourceImageWidth,
                                                                    targetSourceImageHeight);
            }
            catch
            {
                isError = true;
                throw;
            }
            finally
            {
                if (isError && resizedImageFileStream != null)
                {
                    await resizedImageFileStream.DisposeAsync();
                }
            }

            return resizedImageFileStream;
        }

        private static async Task<FileStream> SpawnImageCropperDialog(string filePath, string cachedFilePath,
                                                                      uint toWidth, uint toHeight)
        {
            Grid parentGrid = new()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                CornerRadius = new CornerRadius(12)
                // Margin = new Thickness(-23, -19, -23, -19)
            };

            ImageCropper imageCropper = new ImageCropper
            {
                AspectRatio         = 16d / 9d,
                CropShape           = CropShape.Rectangular,
                ThumbPlacement      = ThumbPlacement.Corners,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment   = VerticalAlignment.Stretch,
                Opacity             = 0
            };

            // Path of image
            Uri overlayImageUri = new Uri(Path.Combine(AppExecutableDir, @"Assets\Images\ImageCropperOverlay",
                                                       GetAppConfigValue("WindowSizeProfile").ToString() == "Small" ? "small.png" : "normal.png"));

            // Why not use ImageBrush?
            // https://github.com/microsoft/microsoft-ui-xaml/issues/7809
            imageCropper.Overlay = new ImageBlendBrush
            {
                Opacity = 0.5,
                Stretch = Stretch.Fill,
                Mode = ImageBlendMode.Multiply,
                SourceUri = overlayImageUri
            };

            ContentDialogOverlay dialogOverlay = new ContentDialogOverlay(ContentDialogTheme.Informational)
            {
                Title = Locale.Lang!._Misc!.ImageCropperTitle,
                Content = parentGrid,
                SecondaryButtonText = Locale.Lang._Misc.Cancel,
                PrimaryButtonText = Locale.Lang._Misc.OkayHappy,
                DefaultButton = ContentDialogButton.Primary,
                IsPrimaryButtonEnabled = false,
                XamlRoot = (WindowUtility.CurrentWindow as MainWindow)?.Content!.XamlRoot
            };

            LoadImageCropperDetached(filePath, imageCropper, parentGrid, dialogOverlay);

            ContentDialogResult dialogResult = await dialogOverlay.QueueAndSpawnDialog();
            if (dialogResult == ContentDialogResult.Secondary) return null;

            try
            {
                await using (FileStream cachedFileStream =
                             new FileStream(cachedFilePath!, StreamExtension.FileStreamCreateReadWriteOpt))
                {
                    dialogOverlay.IsPrimaryButtonEnabled   = false;
                    dialogOverlay.IsSecondaryButtonEnabled = false;
                    await imageCropper.SaveAsync(cachedFileStream.AsRandomAccessStream()!, BitmapFileFormat.Png);
                }

                GC.WaitForPendingFinalizers();
                GC.WaitForFullGCComplete();
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"Exception caught at [ImageLoaderHelper::SpawnImageCropperDialog]\r\n{ex}", LogType.Error, true);
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
            }

            FileInfo cachedFileInfo = new FileInfo(cachedFilePath);
            return await GenerateCachedStream(cachedFileInfo, toWidth, toHeight, true);
        }

        private static async void LoadImageCropperDetached(string filePath, ImageCropper imageCropper,
                                                           Grid parentGrid, ContentDialogOverlay dialogOverlay)
        {
            try
            {
                StackPanel loadingMsgPanel = new()
                {
                    Orientation         = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                    Opacity             = 1d
                };

                loadingMsgPanel.AddElementToStackPanel(new ProgressRing
                {
                    IsIndeterminate   = true,
                    Width             = 16,
                    Height            = 16,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin            = new Thickness(0, 0, 8, 0)
                });
                loadingMsgPanel.AddElementToStackPanel(new TextBlock
                {
                    Text       = "Loading the Image",
                    FontWeight = FontWeights.SemiBold
                });

                parentGrid.AddElementToGridRowColumn(imageCropper);
                parentGrid.AddElementToGridRowColumn(loadingMsgPanel);

                StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
                await imageCropper!.LoadImageFromFile(file!);

                GC.WaitForPendingFinalizers();
                GC.WaitForFullGCComplete();

                Storyboard storyboardAnim = new();
                DoubleAnimation loadingMsgPanelAnim = loadingMsgPanel.CreateDoubleAnimation("Opacity", 0, 1, null,
                         TimeSpan.FromMilliseconds(500), EasingType.Cubic.ToEasingFunction());
                DoubleAnimation imageCropperAnim = imageCropper.CreateDoubleAnimation("Opacity", 1, 0, null,
                         TimeSpan.FromMilliseconds(500), EasingType.Cubic.ToEasingFunction());
                storyboardAnim.Children!.Add(loadingMsgPanelAnim);
                storyboardAnim.Children!.Add(imageCropperAnim);
                storyboardAnim.Begin();

                dialogOverlay!.IsPrimaryButtonEnabled = true;
            }
            catch
            {
                // ignored
            }
        }

        private static async Task<FileStream> GenerateCachedStream(FileInfo inputFileInfo,
                                                                   uint toWidth, uint toHeight,
                                                                   bool isFromCropProcess = false)
        {
            if (isFromCropProcess)
            {
                string inputFileName = inputFileInfo!.FullName;
                try
                {
                    inputFileInfo.MoveTo(inputFileInfo.FullName + "_old", true);
                    FileInfo newCachedFileInfo = new FileInfo(inputFileName);
                    await using (FileStream newCachedFileStream = newCachedFileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
                        await using (FileStream oldInputFileStream = inputFileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
                            await ResizeImageStream(oldInputFileStream, newCachedFileStream, toWidth, toHeight);

                    inputFileInfo.Delete();

                    return newCachedFileInfo.Open(StreamExtension.FileStreamOpenReadOpt);
                }
                catch (IOException ex)
                {
                    Logger.LogWriteLine($"[ImageLoaderHelper::GenerateCachedStream] IOException Caught! Opening InputFile instead...\r\n{ex}", LogType.Error, true);
                    await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                    return inputFileInfo.Open(StreamExtension.FileStreamOpenReadOpt);
                }
            }

            FileInfo cachedFileInfo = GetCacheFileInfo(inputFileInfo!.FullName + inputFileInfo.Length);
            bool isCachedFileExist = cachedFileInfo!.Exists && cachedFileInfo.Length > 1 << 15;
            if (isCachedFileExist) return cachedFileInfo.Open(StreamExtension.FileStreamOpenReadOpt);

            await using (FileStream cachedFileStream = cachedFileInfo.Create())
                await using (FileStream inputFileStream = inputFileInfo.Open(StreamExtension.FileStreamOpenReadOpt))
                    await ResizeImageStream(inputFileStream, cachedFileStream, toWidth, toHeight);

            return cachedFileInfo.Open(StreamExtension.FileStreamOpenReadOpt);
        }

        internal static FileInfo GetCacheFileInfo(string filePath)
        {
            string cachedFileHash = Hash.GetHashStringFromString<Crc32>(filePath);
            string cachedFilePath = Path.Combine(AppGameImgCachedFolder!, cachedFileHash!);
            if (IsWaifu2XEnabled)
                cachedFilePath += "_waifu2x";
            return new FileInfo(cachedFilePath);
        }

        public static async Task ResizeImageStream(Stream input, Stream output, uint toWidth, uint toHeight)
        {
            await Task.Run(() =>
            {
                ProcessImageSettings settings = new()
                {
                    Width = (int)toWidth,
                    Height = (int)toHeight,
                    HybridMode = HybridScaleMode.Off,
                    Interpolation = InterpolationSettings.CubicSmoother,
                    Anchor = CropAnchor.Bottom | CropAnchor.Center
                };
                settings.TrySetEncoderFormat(ImageMimeTypes.Png);

                var imageFileInfo = ImageFileInfo.Load(input!);
                var frame = imageFileInfo.Frames[0];
                input.Position = 0;

                bool isUseWaifu2X = IsWaifu2XEnabled && (frame.Width < toWidth || frame.Height < toHeight);
                using var pipeline = MagicImageProcessor.BuildPipeline(input, isUseWaifu2X ? ProcessImageSettings.Default : settings);

                if (isUseWaifu2X)
                    pipeline.AddTransform(new Waifu2XTransform(_waifu2X));

                pipeline.WriteOutput(output);
            });
        }

        public static async Task<(Bitmap, BitmapImage)> GetResizedBitmapNew(string filePath)
        {
            Bitmap bitmapRet;
            BitmapImage bitmapImageRet;

            FileStream cachedFileStream = await LoadImage(filePath);
            if (cachedFileStream == null) return (null, null);
            await using (cachedFileStream)
            {
                bitmapRet = await Task.Run(() => Stream2Bitmap(cachedFileStream.AsRandomAccessStream()));
                bitmapImageRet = await Stream2BitmapImage(cachedFileStream.AsRandomAccessStream());
            }

            return (bitmapRet, bitmapImageRet);
        }

        public static async Task<BitmapImage> Stream2BitmapImage(IRandomAccessStream image)
        {
            var ret = new BitmapImage();
            image!.Seek(0);
            await ret.SetSourceAsync(image);
            return ret;
        }

        public static Bitmap Stream2Bitmap(IRandomAccessStream image)
        {
            image!.Seek(0);
            return new Bitmap(image.AsStream()!);
        }

        /// <summary>
        /// Check if background image is downloaded
        /// </summary>
        /// <param name="fileInfo">FileInfo of the image to store</param>
        /// <param name="checkIsHashable">Is it hashed?</param>
        /// <returns>true if downloaded, false if not</returns>
        public static Task<bool> IsFileCompletelyDownloadedAsync(FileInfo fileInfo, bool checkIsHashable)
        {
            // Check if the file exist
            return Task<bool>.Factory.StartNew(
                () => IsFileCompletelyDownloaded(fileInfo, checkIsHashable),
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
        }

        /// <summary>
        /// Check if background image is downloaded
        /// </summary>
        /// <param name="fileInfo">FileInfo of the image to store</param>
        /// <param name="checkIsHashable">Is it hashed?</param>
        /// <returns>true if downloaded, false if not</returns>
        public static bool IsFileCompletelyDownloaded(FileInfo fileInfo, bool checkIsHashable)
        {
            // Get the parent path and file name
            string outputParentPath = Path.GetDirectoryName(fileInfo.FullName);
            string outputFileName = Path.GetFileName(fileInfo.FullName);

#nullable enable
            // Try get a hash from filename if the checkIsHashable set to true and the file does exist
            if (checkIsHashable && fileInfo.Exists && TryGetMd5HashFromFilename(outputFileName, out byte[]? hashFromFilename))
            {
                // Open the file and check for the hash
                using FileStream fileStream = fileInfo.OpenRead();
                ReadOnlySpan<byte> hashByte = MD5.HashData(fileStream);

                // Check if the hash matches then return the completeness
                bool isMatch = hashByte.SequenceEqual(hashFromFilename);
                return isMatch;
            }
#nullable restore

            // Try to get the prop file which includes the filename + the suggested size provided
            // by the network stream if it has been downloaded before
            if (outputParentPath == null)
            {
                return false;
            }

            string propFilePath = Directory.EnumerateFiles(outputParentPath, $"{outputFileName}#*", SearchOption.TopDirectoryOnly).FirstOrDefault();
            // Check if the file is found (not null), then try parse the information
            if (string.IsNullOrEmpty(propFilePath))
            {
                return false;
            }

            // Try split the filename into a segment by # char
            string[] propSegment = Path.GetFileName(propFilePath).Split('#');
            // Assign the check if the condition met and set the file existence status
            return propSegment.Length >= 2
                   && long.TryParse(propSegment[1], null, out long suggestedSize)
                   && fileInfo.Exists && fileInfo.Length == suggestedSize;

            // If the prop doesn't exist, then return false to assume that the file doesn't exist
        }

#nullable enable
        private static bool TryGetMd5HashFromFilename(string fileName, out byte[]? hash)
        {
            // Set default value for out
            hash = null;

            // If the filename is null, then return false
            if (string.IsNullOrEmpty(fileName))
                return false;

            // Assign range and try get the split
            Span<Range> range = stackalloc Range[4];
            ReadOnlySpan<char> fileNameSpan = fileName.AsSpan();
            int len = fileNameSpan.Split(range, '_', StringSplitOptions.RemoveEmptyEntries);

            // As per format should be "hash_number.ext", check that the range should have
            // expected to return 2. If not, then return false as non hashable.
            if (len != 2)
                return false;

            // Try get the span of the hash
            ReadOnlySpan<char> hashSpan = fileNameSpan[range[0]];

            // If the hashSpan is empty or the length is not even or it's not a MD5 Hex (32 chars), then return false
            if (hashSpan.IsEmpty || hashSpan.Length % 2 != 0 || hashSpan.Length != 32)
                return false;

            // Try decode hash hex to find out if the string is actually a hex
            Span<byte> dummy = stackalloc byte[16];
            if (!HexTool.TryHexToBytesUnsafe(hashSpan, dummy))
                return false;

            // Copy hash from stackalloc to output array
            hash = new byte[16];
            dummy.CopyTo(hash);

            // Return true as it's a valid MD5 hash
            return true;
        }

        private static readonly HashSet<FileInfo> ProcessingFiles = [];
        private static readonly HashSet<string>   ProcessingUrls  = [];

        public static async void TryDownloadToCompletenessDetached(string? url, HttpClient? useHttpClient, FileInfo fileInfo, CancellationToken token)
        {
            try
            {
                _ = await TryDownloadToCompletenessAsync(url, useHttpClient, fileInfo, token);
            }
            catch
            {
                // ignored
            }
        }

        public static async Task<bool> TryDownloadToCompletenessAsync(string? url, HttpClient? useHttpClient, FileInfo fileInfo, CancellationToken token)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            if (ProcessingFiles.Contains(fileInfo) || ProcessingUrls.Contains(url))
            {
                Logger.LogWriteLine("Found duplicate download request, skipping...\r\n\t" +
                                    $"URL : {url}", LogType.Warning, true);
                return false;
            }
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4 << 10);
            try
            {
                ProcessingFiles.Add(fileInfo);
                ProcessingUrls.Add(url);
                // Initialize file temporary name
                FileInfo fileInfoTemp = new FileInfo(fileInfo.FullName + "_temp");

                Logger.LogWriteLine($"Start downloading resource from: {url}", LogType.Default, true);

                if (fileInfo.Exists)
                    fileInfo.Delete();

                int writeAttempt = 5;

                while (writeAttempt > 0)
                {
                    // Try to get the remote stream and download the file
                    long fileLength;
                    await using (Stream netStream = await GetFallbackStreamUrl(useHttpClient, url, token))
                    {
                        await using (FileStream outStream = new FileStream(fileInfoTemp.FullName, FileMode.Create,
                                                                           FileAccess.ReadWrite, FileShare.ReadWrite))
                        {
                            // Get the file length
                            fileLength = netStream.Length;

                            // Create the prop file for download completeness checking
                            string? outputParentPath = Path.GetDirectoryName(fileInfoTemp.FullName);
                            string outputFilename = Path.GetFileName(fileInfoTemp.FullName);
                            if (outputParentPath != null)
                            {
                                string propFilePath = Path.Combine(outputParentPath, $"{outputFilename}#{netStream.Length}");
                                await using (FileStream _ = new FileStream(propFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Delete))
                                {
                                    // Just create the file
                                }
                            }

                            // Copy (and download) the remote streams to local
                            int read;
                            while ((read = await netStream.ReadAsync(buffer, token)) > 0)
                                await outStream.WriteAsync(buffer.AsMemory(0, read), token);
                        }
                    }

                    if (await IsFileCompletelyDownloadedAsync(fileInfoTemp, true))
                    {
                        // Move to its original filename
                        fileInfoTemp.Refresh();
                        fileInfoTemp.MoveTo(fileInfo.FullName, true);

                        Logger.LogWriteLine($"Resource download from: {url} has been completed and stored locally into:"
                            + $"\"{fileInfo.FullName}\" with size: {ConverterTool.SummarizeSizeSimple(fileLength)} ({fileLength} bytes)", LogType.Default, true);

                        // Break from the loop and return true
                        return true;
                    }

                    Logger.LogWriteLine($"Failed to download resource from: {url} while trying to store into:"
                        + $"\"{fileInfo.FullName}\" with size: {ConverterTool.SummarizeSizeSimple(fileLength)} ({fileLength} bytes)"
                        + $". Remained attempt: {writeAttempt}", LogType.Warning, true);

                    // Decrement the write attempt
                    writeAttempt--;
                }

                // Throw as timeout
                throw new TimeoutException($"The url: {url} keeps returning invalid data while trying to store into: {fileInfo.FullName}. Failing...");
            }
            // Ignore cancellation exceptions
            catch (TaskCanceledException)
            {
                // Return false as Cancelled
                return false;
            }
            catch (OperationCanceledException)
            {
                // Return false as Cancelled
                return false;
            }
            catch (Exception ex)
            {
                // ErrorSender.SendException(ex, ErrorType.Connection);
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                Logger.LogWriteLine($"Error has occured while downloading in background for: {url}\r\n{ex}", LogType.Error, true);

                // Return false as failed
                return false;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                ProcessingFiles.Remove(fileInfo);
                ProcessingUrls.Remove(url);
            }
        }

        private static async Task<Stream> GetFallbackStreamUrl(HttpClient? client, string urlLocal, CancellationToken tokenLocal)
        {
            if (client == null)
                return await FallbackCDNUtil.GetHttpStreamFromResponse(urlLocal, tokenLocal);

            return await BridgedNetworkStream.CreateStream(
                await client.GetAsync(urlLocal, HttpCompletionOption.ResponseHeadersRead, tokenLocal),
                tokenLocal);
        }

        public static string? GetCachedSprites(HttpClient? httpClient, string? url, CancellationToken token)
        {
            if (string.IsNullOrEmpty(url)) return url;
            if (token.IsCancellationRequested) return url;

            string cachePath = Path.Combine(AppGameImgCachedFolder, Path.GetFileNameWithoutExtension(url));
            if (!Directory.Exists(AppGameImgCachedFolder))
                Directory.CreateDirectory(AppGameImgCachedFolder);

            FileInfo fInfo = new FileInfo(cachePath);
            if (IsFileCompletelyDownloaded(fInfo, true))
            {
                return cachePath;
            }

            TryDownloadToCompletenessDetached(url, httpClient, fInfo, token);
            return url;

        }

        public static async Task<string?> GetCachedSpritesAsync(string? url, CancellationToken token)
            => await GetCachedSpritesAsync(null, url, token);

        public static async Task<string?> GetCachedSpritesAsync(HttpClient? httpClient, string? url, CancellationToken token)
        {
            if (string.IsNullOrEmpty(url)) return url;

            string cachePath = Path.Combine(AppGameImgCachedFolder, Path.GetFileNameWithoutExtension(url));
            if (!Directory.Exists(AppGameImgCachedFolder))
                Directory.CreateDirectory(AppGameImgCachedFolder);

            FileInfo fInfo = new FileInfo(cachePath);
            if (await IsFileCompletelyDownloadedAsync(fInfo, true))
            {
                return cachePath;
            }

            if (!await TryDownloadToCompletenessAsync(url, httpClient, fInfo, token))
            {
                return url;
            }
            return cachePath;
        }
    }
}
