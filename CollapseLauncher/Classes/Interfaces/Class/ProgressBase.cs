using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using CommunityToolkit.WinUI;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Shared.Region;
using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Helper;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.TaskbarListCOM;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using CollapseUIExtension = CollapseLauncher.Extension.UIElementExtensions;
// ReSharper disable InconsistentlySynchronizedField
// ReSharper disable IdentifierTypo

#nullable enable
namespace CollapseLauncher.Interfaces
{
    internal class ProgressBase<T1> : GamePropertyBase<T1> where T1 : IAssetIndexSummary
    {
        public ProgressBase(UIElement parentUI, IGameVersionCheck? gameVersionManager, IGameSettings? gameSettings, string? gamePath, string? gameRepoURL, string? versionOverride)
            : base(parentUI, gameVersionManager, gameSettings, gamePath, gameRepoURL, versionOverride)
        {
            Status         = new TotalPerFileStatus { IsIncludePerFileIndicator = true };
            Progress       = new TotalPerFileProgress();
            SophonStatus   = new TotalPerFileStatus { IsIncludePerFileIndicator = true };
            SophonProgress = new TotalPerFileProgress();
            AssetIndex     = [];
        }

        public ProgressBase(UIElement parentUI, IGameVersionCheck? gameVersionManager, string? gamePath, string? gameRepoURL, string? versionOverride)
            : this(parentUI, gameVersionManager, null, gamePath, gameRepoURL, versionOverride) { }

        public event EventHandler<TotalPerFileProgress>? ProgressChanged;
        public event EventHandler<TotalPerFileStatus>?   StatusChanged;

        protected TotalPerFileStatus    SophonStatus;
        protected TotalPerFileProgress  SophonProgress;
        protected TotalPerFileStatus    Status;
        protected TotalPerFileProgress  Progress;
        protected int                   ProgressAllCountCurrent;
        protected int                   ProgressAllCountFound;
        protected int                   ProgressAllCountTotal;
        protected long                  ProgressAllSizeCurrent;
        protected long                  ProgressAllSizeFound;
        protected long                  ProgressAllSizeTotal;
        protected long                  ProgressPerFileSizeCurrent;
        protected long                  ProgressPerFileSizeTotal;

        // Extension for IGameInstallManager

        protected const int RefreshInterval = 100;

        public bool IsSophonInUpdateMode { get; set; }

        #region ProgressEventHandlers - Fetch
        protected void _innerObject_ProgressAdapter(object? sender, TotalPerFileProgress e) => ProgressChanged?.Invoke(sender, e);
        protected void _innerObject_StatusAdapter(object? sender, TotalPerFileStatus e) => StatusChanged?.Invoke(sender, e);

        protected virtual void _httpClient_FetchAssetProgress(int size, DownloadProgress downloadProgress)
        {
            // Calculate the speed
            double speedAll = CalculateSpeed(size);

            if (!CheckIfNeedRefreshStopwatch())
            {
                return;
            }

            // Calculate the clamped speed and timelapse
            double speedClamped = speedAll.ClampLimitedSpeedNumber();

            TimeSpan timeLeftSpan = ConverterTool.ToTimeSpanRemain(downloadProgress.BytesTotal, downloadProgress.BytesDownloaded, speedClamped);
            double   percentage   = ConverterTool.ToPercentage(downloadProgress.BytesTotal, downloadProgress.BytesDownloaded);

            lock (Status)
            {
                // Update fetch status
                Status.IsProgressPerFileIndetermined = false;
                Status.IsProgressAllIndetermined     = false;
                Status.ActivityPerFile               = string.Format(Lang!._GameRepairPage!.PerProgressSubtitle3!, ConverterTool.SummarizeSizeSimple(speedClamped));
            }

            lock (Progress)
            {
                // Update fetch progress
                Progress.ProgressPerFilePercentage = percentage;
                Progress.ProgressAllSizeCurrent    = downloadProgress.BytesDownloaded;
                Progress.ProgressAllSizeTotal      = downloadProgress.BytesTotal;
                Progress.ProgressAllSpeed          = speedClamped;
                Progress.ProgressAllTimeLeft       = timeLeftSpan;
            }

            // Push status and progress update
            UpdateStatus();
            UpdateProgress();
        }

        #endregion

        #region ProgressEventHandlers - Repair
        protected virtual void _httpClient_RepairAssetProgress(int size, DownloadProgress downloadProgress)
        {
            Interlocked.Add(ref ProgressAllSizeCurrent, size);

            // Calculate the speed
            double speedAll = CalculateSpeed(size);

            if (!CheckIfNeedRefreshStopwatch())
            {
                return;
            }

            // Calculate the clamped speed and timelapse
            double speedClamped = speedAll.ClampLimitedSpeedNumber();

            TimeSpan timeLeftSpan      = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal, ProgressAllSizeCurrent, speedClamped);
            double   percentagePerFile = ConverterTool.ToPercentage(downloadProgress.BytesTotal, downloadProgress.BytesDownloaded);

            lock (Progress)
            {
                Progress.ProgressPerFilePercentage  = percentagePerFile;
                Progress.ProgressPerFileSizeCurrent = downloadProgress.BytesDownloaded;
                Progress.ProgressPerFileSizeTotal   = downloadProgress.BytesTotal;
                Progress.ProgressAllSizeCurrent     = ProgressAllSizeCurrent;
                Progress.ProgressAllSizeTotal       = ProgressAllSizeTotal;

                // Calculate speed
                Progress.ProgressAllSpeed    = speedClamped;
                Progress.ProgressAllTimeLeft = timeLeftSpan;

                // Update current progress percentages
                Progress.ProgressAllPercentage = ProgressAllSizeCurrent != 0 ?
                    ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent) :
                    0;
            }

            lock (Status)
            {
                // Update current activity status
                Status.IsProgressAllIndetermined     = false;
                Status.IsProgressPerFileIndetermined = false;

                // Set time estimation string
                string timeLeftString = string.Format(Lang!._Misc!.TimeRemainHMSFormat!, Progress.ProgressAllTimeLeft);

                Status.ActivityPerFile = string.Format(Lang._Misc.Speed!, ConverterTool.SummarizeSizeSimple(Progress.ProgressAllSpeed));
                Status.ActivityAll     = string.Format(Lang._GameRepairPage!.PerProgressSubtitle2!,
                                                       ConverterTool.SummarizeSizeSimple(ProgressAllSizeCurrent),
                                                       ConverterTool.SummarizeSizeSimple(ProgressAllSizeTotal)) + $" | {timeLeftString}";

                // Trigger update
                UpdateAll();
            }
        }

        protected virtual void UpdateRepairStatus(string activityStatus, string activityAll, bool isPerFileIndetermined)
        {
            lock (Status)
            {
                // Set repair activity status
                Status.ActivityStatus = activityStatus;
                Status.ActivityAll = activityAll;
                Status.IsProgressPerFileIndetermined = isPerFileIndetermined;
            }

            // Update status
            UpdateStatus();
        }
        #endregion

        #region ProgressEventHandlers - UpdateCache
        protected virtual void _httpClient_UpdateAssetProgress(int size, DownloadProgress downloadProgress)
        {
            Interlocked.Add(ref ProgressAllSizeCurrent, size);

            // Calculate the speed
            double speedAll = CalculateSpeed(size);

            if (!CheckIfNeedRefreshStopwatch())
            {
                return;
            }

            // Calculate the clamped speed and timelapse
            double   speedClamped = speedAll.ClampLimitedSpeedNumber();
            TimeSpan timeLeftSpan = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal, ProgressAllSizeCurrent, speedClamped);
            double   percentage   = ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent);

            // Update current progress percentages and speed
            lock (Progress)
            {
                Progress.ProgressAllPercentage = percentage;
            }

            // Update current activity status
            Status.IsProgressAllIndetermined = false;
            string timeLeftString             = string.Format(Lang._Misc.TimeRemainHMSFormat, timeLeftSpan);
            Status.ActivityAll               = string.Format(Lang._Misc.Downloading + ": {0}/{1} ", ProgressAllCountCurrent,
                                                             ProgressAllCountTotal)
                                               + string.Format($"({Lang._Misc.SpeedPerSec})",
                                                               ConverterTool.SummarizeSizeSimple(speedClamped))
                                               + $" | {timeLeftString}";

            // Trigger update
            UpdateAll();
        }
        #endregion

        #region ProgressEventHandlers - Patch
        protected virtual void RepairTypeActionPatching_ProgressChanged(object? sender, BinaryPatchProgress e)
        {
            lock (Progress)
            {
                Progress.ProgressPerFilePercentage = e.ProgressPercentage;
                Progress.ProgressAllSpeed = e.Speed;

                // Update current progress percentages
                Progress.ProgressAllPercentage = ProgressAllSizeCurrent != 0 ?
                    ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent) :
                    0;
            }

            if (!CheckIfNeedRefreshStopwatch())
            {
                return;
            }

            lock (Status)
            {
                // Update current activity status
                Status.IsProgressAllIndetermined     = false;
                Status.IsProgressPerFileIndetermined = false;
                Status.ActivityPerFile               = string.Format(Lang!._GameRepairPage!.PerProgressSubtitle5!,
                                                                     ConverterTool.SummarizeSizeSimple(Progress.ProgressAllSpeed));
                Status.ActivityAll                   = string.Format(Lang._GameRepairPage.PerProgressSubtitle2!,
                                                                     ConverterTool.SummarizeSizeSimple(ProgressAllSizeCurrent),
                                                                     ConverterTool.SummarizeSizeSimple(ProgressAllSizeTotal));
            }

            // Trigger update
            UpdateAll();
        }
        #endregion

        #region ProgressEventHandlers - CRC/HashCheck
        protected virtual void UpdateProgressCrc(long read)
        {
            // Calculate speed
            double speedAll = CalculateSpeed(read);

            if (!CheckIfNeedRefreshStopwatch())
            {
                return;
            }

            lock (Progress)
            {
                // Update current progress percentages
                Progress.ProgressPerFilePercentage = ProgressPerFileSizeCurrent != 0 ?
                    ConverterTool.ToPercentage(ProgressPerFileSizeTotal, ProgressPerFileSizeCurrent) :
                    0;
                Progress.ProgressAllPercentage = ProgressAllSizeCurrent != 0 ?
                    ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent) :
                    0;

                // Update the progress of total size
                Progress.ProgressPerFileSizeCurrent = ProgressPerFileSizeCurrent;
                Progress.ProgressPerFileSizeTotal   = ProgressPerFileSizeTotal;
                Progress.ProgressAllSizeCurrent     = ProgressAllSizeCurrent;
                Progress.ProgressAllSizeTotal       = ProgressAllSizeTotal;

                // Calculate current speed and update the status and progress speed
                Progress.ProgressAllSpeed = speedAll;

                // Calculate the timelapse
                Progress.ProgressAllTimeLeft = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal, ProgressAllSizeCurrent, speedAll);
            }

            lock (Status)
            {
                // Set time estimation string
                string timeLeftString = string.Format(Lang!._Misc!.TimeRemainHMSFormat!, Progress.ProgressAllTimeLeft);

                // Update current activity status
                Status.ActivityPerFile = string.Format(Lang._Misc.Speed!, ConverterTool.SummarizeSizeSimple(Progress.ProgressAllSpeed));
                Status.ActivityAll = string.Format(Lang._GameRepairPage!.PerProgressSubtitle2!, 
                                                    ConverterTool.SummarizeSizeSimple(ProgressAllSizeCurrent), 
                                                    ConverterTool.SummarizeSizeSimple(ProgressAllSizeTotal)) + $" | {timeLeftString}";
            }

            // Trigger update
            UpdateAll();
        }
        #endregion

        #region ProgressEventHandlers - DoCopyStreamProgress
        protected virtual void UpdateProgressCopyStream(long currentPosition, int read, long totalReadSize)
        {
            // Calculate the speed
            double speedAll = CalculateSpeed(read);

            if (!CheckIfNeedRefreshStopwatch())
            {
                return;
            }

            lock (Progress)
            {
                // Update current progress percentages
                Progress.ProgressPerFilePercentage = ConverterTool.ToPercentage(totalReadSize, currentPosition);

                // Update the progress of total size
                Progress.ProgressPerFileSizeCurrent = currentPosition;
                Progress.ProgressPerFileSizeTotal   = totalReadSize;

                // Calculate current speed and update the status and progress speed
                Progress.ProgressAllSpeed = speedAll;

                // Calculate the timelapse
                Progress.ProgressAllTimeLeft = ConverterTool.ToTimeSpanRemain(totalReadSize, currentPosition, speedAll);
            }

            lock (Status)
            {
                // Set time estimation string
                string timeLeftString = string.Format(Lang!._Misc!.TimeRemainHMSFormat!, Progress.ProgressAllTimeLeft);

                // Update current activity status
                Status.ActivityPerFile = string.Format(Lang._Misc.Speed!, ConverterTool.SummarizeSizeSimple(Progress.ProgressAllSpeed));
                Status.ActivityAll     = string.Format(Lang._GameRepairPage!.PerProgressSubtitle2!, 
                                                       ConverterTool.SummarizeSizeSimple(currentPosition), 
                                                       ConverterTool.SummarizeSizeSimple(totalReadSize)) + $" | {timeLeftString}";
            }

            // Trigger update
            UpdateAll();
        }
        #endregion

        #region ProgressEventHandlers - SpeedCalculator and Refresh Interval Checker
        private const double ScOneSecond = 1000;
        private long _scLastTick = Environment.TickCount64;
        private long _scLastReceivedBytes;
        private double _scLastSpeed;

        protected double CalculateSpeed(long receivedBytes) => CalculateSpeed(receivedBytes, ref _scLastSpeed, ref _scLastReceivedBytes, ref _scLastTick);

        protected static double CalculateSpeed(long receivedBytes, ref double lastSpeedToUse, ref long lastReceivedBytesToUse, ref long lastTickToUse)
        {
            long   currentTick           = Environment.TickCount64 - lastTickToUse + 1;
            long   totalReceivedInSecond = Interlocked.Add(ref lastReceivedBytesToUse, receivedBytes);
            double speed                 = totalReceivedInSecond * ScOneSecond / currentTick;

            if (!(currentTick > ScOneSecond))
            {
                return lastSpeedToUse;
            }

            lastSpeedToUse = speed;
            _ = Interlocked.Exchange(ref lastSpeedToUse,         speed);
            _ = Interlocked.Exchange(ref lastReceivedBytesToUse, 0);
            _ = Interlocked.Exchange(ref lastTickToUse,          Environment.TickCount64);
            return lastSpeedToUse;
        }

        private int _riLastTick = Environment.TickCount;

        protected bool CheckIfNeedRefreshStopwatch()
        {
            int currentTick = Environment.TickCount - _riLastTick;
            if (currentTick <= RefreshInterval)
            {
                return false;
            }

            Interlocked.Exchange(ref _riLastTick, Environment.TickCount);
            return true;

        }
        #endregion

        #region ProgressEventHandlers - SophonInstaller
        private double _sophonDownloadOnlySpeed = 1;
        private double _sophonDownloadOnlyLastSpeed;
        private long   _sophonDownloadOnlyReceivedBytes;
        private long   _sophonDownloadOnlyLastTick = Environment.TickCount64;

        private long   _sophonDownloadOnlyCurrentDownloadedBytes;
        private long   _sophonDownloadOnlyLastDownloadedBytes;

        protected void UpdateSophonFileTotalProgress(long read)
        {
            _ = Interlocked.Add(ref ProgressAllSizeCurrent, read);

            // Calculate the speed
            double speedAll = CalculateSpeed(read);

            // Get last received bytes from download
            long lastReceivedDownloadBytes = _sophonDownloadOnlyCurrentDownloadedBytes - _sophonDownloadOnlyLastDownloadedBytes;
            _ = Interlocked.Exchange(ref _sophonDownloadOnlyLastDownloadedBytes, _sophonDownloadOnlyCurrentDownloadedBytes);

            // Calculate the speed for download (just use it for update only by setting receivedBytes to 0)
            _sophonDownloadOnlySpeed = CalculateSpeed(lastReceivedDownloadBytes, ref _sophonDownloadOnlyLastSpeed, ref _sophonDownloadOnlyReceivedBytes, ref _sophonDownloadOnlyLastTick);

            // Calculate the clamped speed for download and timelapse
            double speedDownloadClamped = _sophonDownloadOnlySpeed.ClampLimitedSpeedNumber();

            if (!CheckIfNeedRefreshStopwatch())
            {
                return;
            }

            // Assign local sizes to progress
            SophonProgress.ProgressAllSizeCurrent     = ProgressAllSizeCurrent;
            SophonProgress.ProgressAllSizeTotal       = ProgressAllSizeTotal;
            SophonProgress.ProgressPerFileSizeCurrent = ProgressPerFileSizeCurrent;
            SophonProgress.ProgressPerFileSizeTotal   = ProgressPerFileSizeTotal;

            SophonProgress.ProgressAllSpeed     = speedAll;
            SophonProgress.ProgressPerFileSpeed = speedDownloadClamped;

            // Calculate Count
            SophonProgress.ProgressAllEntryCountCurrent = ProgressAllCountCurrent;
            SophonProgress.ProgressAllEntryCountTotal   = ProgressAllCountTotal;

            // Always change the status progress to determined
            Status.IsProgressAllIndetermined     = false;
            Status.IsProgressPerFileIndetermined = false;
            StatusChanged?.Invoke(this, Status);

            // Calculate percentage
            SophonProgress.ProgressAllPercentage     = ConverterTool.ToPercentage(ProgressAllSizeTotal, ProgressAllSizeCurrent);
            SophonProgress.ProgressPerFilePercentage = ConverterTool.ToPercentage(ProgressPerFileSizeTotal, ProgressPerFileSizeCurrent);
            SophonProgress.ProgressAllTimeLeft       = ConverterTool.ToTimeSpanRemain(ProgressAllSizeTotal, ProgressAllSizeCurrent, speedAll);

            // Update progress
            ProgressChanged?.Invoke(this, SophonProgress);

            // Update taskbar progress
            if (Status.IsProgressAllIndetermined)
            {
                WindowUtility.SetTaskBarState(TaskbarState.Indeterminate);
            }
            else if (Status.IsRunning)
            {
                WindowUtility.SetTaskBarState(TaskbarState.Normal);
                WindowUtility.SetProgressValue((ulong)(SophonProgress.ProgressAllPercentage * 10), 1000);
            }
            else
            {
                WindowUtility.SetTaskBarState(TaskbarState.NoProgress);
            }
        }

        protected void UpdateSophonFileDownloadProgress(long downloadedWrite, long currentWrite)
        {
            _ = Interlocked.Add(ref ProgressPerFileSizeCurrent,               downloadedWrite);
            _ = Interlocked.Add(ref _sophonDownloadOnlyCurrentDownloadedBytes, currentWrite);
        }

        protected void UpdateSophonDownloadStatus(SophonAsset asset)
        {
            Interlocked.Add(ref ProgressAllCountCurrent, 1);
            Status.ActivityStatus = $"{(IsSophonInUpdateMode
                ? Lang._Misc.Updating
                : Lang._Misc.Downloading)}: {string.Format(Lang._Misc.PerFromTo, ProgressAllCountCurrent,
                                                           ProgressAllCountTotal)}";

            UpdateStatus();
        }

        protected static void UpdateSophonLogHandler(object? sender, LogStruct e)
        {
#if !DEBUG
            if (e.LogLevel == LogLevel.Debug) return;
#endif
            (bool isNeedWriteLog, LogType logType) logPair = e.LogLevel switch
            {
                LogLevel.Warning => (true, LogType.Warning),
                LogLevel.Debug => (true, LogType.Debug),
                LogLevel.Error => (true, LogType.Error),
                _ => (true, LogType.Default)
            };
            LogWriteLine(e.Message, logPair.logType, logPair.isNeedWriteLog);
        }
        #endregion

        #region BaseTools
        protected async Task DoCopyStreamProgress(Stream source, Stream target, long? estimatedSize = null, CancellationToken token = default)
        {
            // ReSharper disable once ConstantNullCoalescingCondition
            long inputSize = estimatedSize != null ? estimatedSize ?? 0 : source.Length;
            long currentPos = 0;

            bool isLastPerfileStateIndetermined = Status.IsProgressPerFileIndetermined;
            bool isLastTotalStateIndetermined = Status.IsProgressAllIndetermined;

            Status.IsProgressPerFileIndetermined = false;
            Status.IsProgressAllIndetermined = true;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(16 << 10);
            try
            {
                int read;
                while ((read = await source.ReadAsync(buffer, token)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), token);
                    currentPos += read;
                    UpdateProgressCopyStream(currentPos, read, inputSize);
                }
            }
            finally
            {
                Status.IsProgressPerFileIndetermined = isLastPerfileStateIndetermined;
                Status.IsProgressAllIndetermined = isLastTotalStateIndetermined;
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        protected static string EnsureCreationOfDirectory(string str)
            => StreamExtension.EnsureCreationOfDirectory(str).FullName;

        protected static string EnsureCreationOfDirectory(FileInfo str)
            => str.EnsureCreationOfDirectory().FullName;

        protected static void TryUnassignReadOnlyFiles(string dirPath)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(dirPath);
            if (!directoryInfo.Exists)
            {
                return;
            }

            // Iterate every file and set the read-only flag to false
            foreach (FileInfo file in directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                _ = file.EnsureNoReadOnly();
            }
        }

        protected static void TryUnassignReadOnlyFileSingle(string filePath)
        {
            FileInfo fileInfo = new FileInfo(filePath);
            _ = fileInfo.EnsureNoReadOnly();
        }

        protected static void TryDeleteReadOnlyDir(string dirPath)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
            if (!dirInfo.Exists)
            {
                return;
            }

            foreach (FileInfo files in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                .EnumerateNoReadOnly())
            {
                try
                {
                    files.Delete();
                }
                catch (Exception ex)
                {
                    SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                    LogWriteLine($"Failed while deleting file: {files.FullName}\r\n{ex}", LogType.Warning, true);
                } // Suppress errors
            }

            try
            {
                dirInfo.Refresh();
                dirInfo.Delete(true);
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed while deleting parent dir: {dirPath}\r\n{ex}", LogType.Warning, true);
            } // Suppress errors
        }

        protected static void TryDeleteReadOnlyFile(string filePath)
        {
            FileInfo fileInfo = new FileInfo(filePath)
                .EnsureNoReadOnly(out bool isFileExist);
            if (!isFileExist) return;

            try
            {
                fileInfo.Delete();
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed to delete file: {fileInfo.FullName}\r\n{ex}", LogType.Error, true);
            }
        }

        protected static void MoveFolderContent(string sourcePath, string destPath)
        {
            // Get the source folder path length + 1
            int dirLength = sourcePath.Length + 1;

            // Initialize paths and error status
            bool errorOccured = false;

            // Enumerate files inside of source path
            DirectoryInfo directoryInfoSource = new DirectoryInfo(sourcePath);
            if (!directoryInfoSource.Exists)
            {
                throw new DirectoryNotFoundException($"Cannot find source directory on this path: {sourcePath}");
            }
            foreach (FileInfo fileInfo in directoryInfoSource.EnumerateFiles("*", SearchOption.AllDirectories)
                .EnumerateNoReadOnly())
            {
                // Get the relative path of the file from source path
                ReadOnlySpan<char> relativePath = fileInfo.FullName.AsSpan()[dirLength..];
                // Get the absolute path for destination
                var destFilePath = Path.Combine(destPath, relativePath.ToString());
                // Get folder path for destination
                var destFolderPath = Path.GetDirectoryName(destFilePath);

                // Create the destination folder if not exist
                if (!string.IsNullOrEmpty(destFolderPath))
                    _ = Directory.CreateDirectory(destFolderPath);

                try
                {
                    // Try moving the file
                    LogWriteLine($"Moving \"{relativePath.ToString()}\" to \"{destFolderPath}\"", LogType.Default, true);
                    fileInfo.MoveTo(destFilePath, true);
                }
                catch (Exception ex)
                {
                    SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                    // If failed, flag ErrorOccured as true and skip to the next file 
                    LogWriteLine($"Error while moving \"{relativePath.ToString()}\" to \"{destFolderPath}\"\r\nException: {ex}", LogType.Error, true);
                    errorOccured = true;
                }
            }

            if (errorOccured)
            {
                return;
            }

            try
            {
                // If no error occurred, then delete the source folder
                directoryInfoSource.Refresh();
                directoryInfoSource.Delete(true);
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                // If failed, flag ErrorOccured as true and skip the source directory deletion 
                LogWriteLine($"Error while deleting source directory \"{directoryInfoSource.FullName}\"\r\nException: {ex}", LogType.Error, true);
            }
        }

        protected virtual void ResetStatusAndProgress()
        {
            // Reset the cancellation token
            Token = new CancellationTokenSourceWrapper();

            // Reset RepairAssetProperty list
            AssetEntry!.Clear();

            // Reset status and progress properties
            ResetStatusAndProgressProperty();

            // Update the status and progress
            UpdateAll();
        }
        
        protected void ResetStatusAndProgressProperty()
        {
            // Reset cancellation token
            Token = new CancellationTokenSourceWrapper();

            lock (Status)
            {
                // Show the asset entry panel
                Status.IsAssetEntryPanelShow = false;

                // Reset all total activity status
                Status.ActivityStatus            = Lang!._GameRepairPage!.StatusNone;
                Status.ActivityAll               = Lang._GameRepairPage.StatusNone;
                Status.IsProgressAllIndetermined = false;

                // Reset all per-file activity status
                Status.ActivityPerFile               = Lang._GameRepairPage.StatusNone;
                Status.IsProgressPerFileIndetermined = false;

                // Reset all status indicators
                Status.IsAssetEntryPanelShow = false;
                Status.IsCompleted           = false;
                Status.IsCanceled            = false;

                // Reset all total activity progress
                lock (Progress)
                {
                    Progress.ProgressPerFilePercentage = 0;
                    Progress.ProgressAllPercentage     = 0;
                    Progress.ProgressPerFileSpeed      = 0;
                    Progress.ProgressAllSpeed          = 0;

                    Progress.ProgressAllEntryCountCurrent     = 0;
                    Progress.ProgressAllEntryCountTotal       = 0;
                    Progress.ProgressPerFileEntryCountCurrent = 0;
                    Progress.ProgressPerFileEntryCountTotal   = 0;
                }
                // Reset all inner counter
                ProgressAllCountCurrent      = 0;
                ProgressAllCountTotal        = 0;
                ProgressAllSizeCurrent       = 0;
                ProgressAllSizeTotal         = 0;
                ProgressPerFileSizeCurrent   = 0;
                ProgressPerFileSizeTotal     = 0;
            }
        }

        protected async Task<MemoryStream> BufferSourceStreamToMemoryStream(Stream input, CancellationToken token)
        {
            // Initialize buffer and return stream
            int read;
            byte[] buffer = new byte[16 << 10];
            MemoryStream stream = new MemoryStream();

            // Initialize length and Stopwatch
            double sizeToDownload = input.Length;
            double downloaded = 0;
            Stopwatch sw = Stopwatch.StartNew();

            // Do read the stream
            while ((read = await input.ReadAsync(buffer, token)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, read), token);

                // Update the read status
                downloaded += read;
                lock (Progress)
                {
                    Progress.ProgressPerFilePercentage = Math.Round(downloaded / sizeToDownload * 100, 2);
                }
                lock (Status)
                {
                    Status.ActivityPerFile = string.Format(Lang!._GameRepairPage!.PerProgressSubtitle3!, ConverterTool.SummarizeSizeSimple(downloaded / sw.Elapsed.TotalSeconds));
                }
                UpdateAll();
            }

            // Reset the stream position and stop the stopwatch
            stream.Position = 0;
            sw.Stop();

            // Return the return stream
            return stream;
        }

        protected async Task FetchBilibiliSdk(CancellationToken token)
        {
            // Check whether the sdk is not null, 
            if (GameVersionManager.GameApiProp.data?.sdk == null) return;

            // Set total activity string as "Loading Indexes..."
            Status.ActivityStatus = Lang!._GameRepairPage!.Status2;
            UpdateStatus();

            // Get the URL and get the remote stream of the zip file
            // Also buffer the stream to memory
            string?                          url            = GameVersionManager.GameApiProp.data.sdk.path;
            if (url == null) throw new NullReferenceException();

            HttpResponseMessage httpResponse = await FallbackCDNUtil.GetURLHttpResponse(url, token);
            await using BridgedNetworkStream httpStream     = await FallbackCDNUtil.GetHttpStreamFromResponse(httpResponse, token);
            await using MemoryStream         bufferedStream = await BufferSourceStreamToMemoryStream(httpStream, token);
            using ZipArchive                 zip            = new ZipArchive(bufferedStream, ZipArchiveMode.Read, true);
            // Iterate the Zip Entry
            foreach (var entry in zip.Entries)
            {
                // Get the filename of the entry without ext.
                string fileName = Path.GetFileNameWithoutExtension(entry.FullName);

                // If the entry is the "sdk_pkg_version", then override the info to sdk_pkg_version
                string sdkDllPath;
                switch (fileName)
                {
                    case "PCGameSDK":
                        // Set the SDK DLL path
                        sdkDllPath = Path.Combine(GamePath!, $"{Path.GetFileNameWithoutExtension(GameVersionManager!.GamePreset!.GameExecutableName)}_Data", "Plugins", "PCGameSDK.dll");
                        break;
                    case "sdk_pkg_version":
                        // Set the SDK DLL path to be used for sdk_pkg_version
                        sdkDllPath = Path.Combine(GamePath!, "sdk_pkg_version");
                        break;
                    default:
                        continue;
                }

                // Assign FileInfo to sdkDllPath
                FileInfo sdkDllFile = new FileInfo(sdkDllPath).EnsureCreationOfDirectory().EnsureNoReadOnly();

                // Do check if sdkDllFile is not null
                // Try to create the file if not exist or open an existing one
                await using Stream sdkDllStream = sdkDllFile.Open(!sdkDllFile.Exists || entry.Length < sdkDllFile.Length ? FileMode.Create : FileMode.OpenOrCreate);
                // Get the hash from the stream
                byte[] hashByte = await Hash.GetHashAsync<Crc32>(sdkDllStream, null, token);
                uint   hashInt  = BitConverter.ToUInt32(hashByte);

                // If the hash is the same, then skip
                if (hashInt == entry.Crc32) continue;
                await using Stream entryStream = entry.Open();
                // Reset the SDK DLL stream pos and write the data
                sdkDllStream.Position = 0;
                await entryStream.CopyToAsync(sdkDllStream, token);
            }
        }

        protected IEnumerable<(T1 AssetIndex, T2 AssetProperty)> PairEnumeratePropertyAndAssetIndexPackage<T2>
            (IEnumerable<T1> assetIndex, IEnumerable<T2> assetProperty)
            where T2 : IAssetProperty
        {
            using IEnumerator<T1> assetIndexEnumerator = assetIndex.GetEnumerator();
            using IEnumerator<T2> assetPropertyEnumerator = assetProperty.GetEnumerator();

            while (assetIndexEnumerator.MoveNext()
                && assetPropertyEnumerator.MoveNext())
            {
                yield return (assetIndexEnumerator.Current, assetPropertyEnumerator.Current);
            }
        }

        protected static IEnumerable<T1> EnforceHttpSchemeToAssetIndex(IEnumerable<T1> assetIndex)
        {
            const string httpsScheme = "https://";
            const string httpScheme = "http://";
            // Get the check if HTTP override is enabled
            bool isUseHttpOverride = LauncherConfig.GetAppConfigValue("EnableHTTPRepairOverride").ToBool();

            // Iterate the IAssetIndexSummary asset
            foreach (T1 asset in assetIndex)
            {
                // If the HTTP override is enabled, then start override the HTTPS scheme
                if (isUseHttpOverride)
                {
                    // Get the remote url as span
                    ReadOnlySpan<char> url = asset.GetRemoteURL().AsSpan();
                    // If the url starts with HTTPS scheme, then...
                    if (url.StartsWith(httpsScheme))
                    {
                        // Get the trimmed URL without HTTPS scheme as span
                        ReadOnlySpan<char> trimmedURL = url.Slice(httpsScheme.Length);
                        // Set the trimmed URL
                        asset.SetRemoteURL(string.Concat(httpScheme, trimmedURL));
                    }

                    // Yield it and continue to the next entry
                    yield return asset;
                    continue;
                }

                // If override not enabled, then just return the asset as is
                yield return asset;
            }
        }

        protected async Task<bool> TryRunExamineThrow(Task<bool> action)
        {
            try
            {
                // Define if the status is still running
                Status.IsRunning = true;

                // Run the task
                return await action;
            }
            catch (TaskCanceledException)
            {
                // If a cancellation was thrown, then set IsCanceled as true
                Status.IsCompleted = false;
                Status.IsCanceled = true;
                throw;
            }
            catch (OperationCanceledException)
            {
                // If a cancellation was thrown, then set IsCanceled as true
                Status.IsCompleted = false;
                Status.IsCanceled = true;
                throw;
            }
            catch (Exception)
            {
                // Except, if the other exception was thrown, then set both IsCompleted
                // and IsCanceled as false.
                Status.IsCompleted = false;
                Status.IsCanceled = false;
                throw;
            }
            finally
            {
                // Clear the _assetIndex after that
                if (Status is { IsCompleted: false })
                {
                    AssetIndex.Clear();
                }

                Status.IsRunning = false;
            }
        }

        protected void SetFoundToTotalValue()
        {
            // Assign found count and size to total count and size
            ProgressAllCountTotal = ProgressAllCountFound;
            ProgressAllSizeTotal = ProgressAllSizeFound;

            // Reset found count and size
            ProgressAllCountFound = 0;
            ProgressAllSizeFound = 0;
        }

        protected bool SummarizeStatusAndProgress(List<T1> assetIndex, string msgIfFound, string msgIfClear)
        {
            // Reset status and progress properties
            ResetStatusAndProgressProperty();

            // Assign found value to total value
            SetFoundToTotalValue();

            // Set check if broken asset is found or not
            bool isBrokenFound = assetIndex.Count > 0;

            // Set status
            Status.IsAssetEntryPanelShow = isBrokenFound;
            Status.IsCompleted = true;
            Status.IsCanceled = false;
            Status.ActivityStatus = isBrokenFound ? msgIfFound : msgIfClear;

            // Update status and progress
            UpdateAll();

            // Return broken asset check
            return isBrokenFound;
        }

        protected virtual bool IsArrayMatch(ReadOnlySpan<byte> source, ReadOnlySpan<byte> target) => source.SequenceEqual(target);

        protected virtual async Task RunDownloadTask(long assetSize, FileInfo assetPath, string assetURL,
            DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, CancellationToken token, bool isOverwrite = true)
        {
            // For any instances that uses Burst Download and if the speed limiter is null when
            // _isBurstDownloadEnabled set to false, then create the speed limiter instance
            bool isUseSelfSpeedLimiter = !IsBurstDownloadEnabled;
            DownloadSpeedLimiter? downloadSpeedLimiter = null;
            if (isUseSelfSpeedLimiter)
            {
                // Create the speed limiter instance and register the listener
                downloadSpeedLimiter = DownloadSpeedLimiter.CreateInstance(LauncherConfig.DownloadSpeedLimitCached);
                LauncherConfig.DownloadSpeedLimitChanged += downloadSpeedLimiter.GetListener();
            }

            try
            {
                // Always do multi-session download with the new DownloadClient regardless of any sizes (if applicable)
                await downloadClient.DownloadAsync(
                    assetURL,
                    assetPath,
                    isOverwrite,
                    sessionChunkSize: LauncherConfig.DownloadChunkSize,
                    progressDelegateAsync: downloadProgress,
                    cancelToken: token,
                    downloadSpeedLimiter: downloadSpeedLimiter
                    );
            }
            finally
            {
                // If the self speed listener is used, then unregister the listener
                if (isUseSelfSpeedLimiter && downloadSpeedLimiter != null)
                {
                    LauncherConfig.DownloadSpeedLimitChanged -= downloadSpeedLimiter.GetListener();
                }
            }
        }

        /// <summary>
        /// <inheritdoc cref="StreamExtension.NaivelyOpenFileStreamAsync(FileInfo, FileMode, FileAccess, FileShare, FileOptions)"/>
        /// </summary>
        internal static ValueTask<FileStream> NaivelyOpenFileStreamAsync(FileInfo info, FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
            => info.NaivelyOpenFileStreamAsync(fileMode, fileAccess, fileShare);
        #endregion

        #region HashTools
        protected virtual ConfiguredTaskAwaitable<byte[]> GetCryptoHashAsync<T>(
            string            filePath,
            byte[]?           hmacKey             = null,
            bool              updateProgress      = true,
            bool              updateTotalProgress = true,
            CancellationToken token               = default)
            where T : HashAlgorithm =>
            Hash.GetCryptoHashAsync<T>(filePath,
                                       hmacKey,
                                       read => UpdateHashReadProgress(read, updateProgress, updateTotalProgress),
                                       token);

        protected virtual ConfiguredTaskAwaitable<byte[]> GetCryptoHashAsync<T>(
            FileInfo          fileInfo,
            byte[]?           hmacKey             = null,
            bool              updateProgress      = true,
            bool              updateTotalProgress = true,
            CancellationToken token               = default)
            where T : HashAlgorithm =>
            Hash.GetCryptoHashAsync<T>(fileInfo,
                                       hmacKey,
                                       read => UpdateHashReadProgress(read, updateProgress, updateTotalProgress),
                                       token);

        protected virtual ConfiguredTaskAwaitable<byte[]> GetCryptoHashAsync<T>(
            Stream            stream,
            byte[]?           hmacKey             = null,
            bool              updateProgress      = true,
            bool              updateTotalProgress = true,
            CancellationToken token               = default)
            where T : HashAlgorithm =>
            Hash.GetCryptoHashAsync<T>(stream,
                                       hmacKey,
                                       read => UpdateHashReadProgress(read, updateProgress, updateTotalProgress),
                                       token);

        protected virtual byte[] GetCryptoHash<T>(
            string            filePath,
            byte[]?           hmacKey             = null,
            bool              updateProgress      = true,
            bool              updateTotalProgress = true,
            CancellationToken token               = default)
            where T : HashAlgorithm =>
            Hash.GetCryptoHash<T>(filePath,
                                  hmacKey,
                                  read => UpdateHashReadProgress(read, updateProgress, updateTotalProgress),
                                  token);

        protected virtual byte[] GetCryptoHash<T>(
            FileInfo          fileInfo,
            byte[]?           hmacKey             = null,
            bool              updateProgress      = true,
            bool              updateTotalProgress = true,
            CancellationToken token               = default)
            where T : HashAlgorithm =>
            Hash.GetCryptoHash<T>(fileInfo,
                                  hmacKey,
                                  read => UpdateHashReadProgress(read, updateProgress, updateTotalProgress),
                                  token);

        protected virtual byte[] GetCryptoHash<T>(
            Stream            stream,
            byte[]?           hmacKey             = null,
            bool              updateProgress      = true,
            bool              updateTotalProgress = true,
            CancellationToken token               = default)
            where T : HashAlgorithm =>
            Hash.GetCryptoHash<T>(stream,
                                  hmacKey,
                                  read => UpdateHashReadProgress(read, updateProgress, updateTotalProgress),
                                  token);

        protected virtual ConfiguredTaskAwaitable<byte[]> GetHashAsync<T>(
            string            filePath,
            bool              updateProgress      = true,
            bool              updateTotalProgress = true,
            CancellationToken token               = default)
            where T : NonCryptographicHashAlgorithm, new() =>
            Hash.GetHashAsync<T>(filePath,
                                 read => UpdateHashReadProgress(read, updateProgress, updateTotalProgress),
                                 token);

        protected virtual ConfiguredTaskAwaitable<byte[]> GetHashAsync<T>(
            FileInfo          fileInfo,
            bool              updateProgress      = true,
            bool              updateTotalProgress = true,
            CancellationToken token               = default)
            where T : NonCryptographicHashAlgorithm, new() =>
            Hash.GetHashAsync<T>(fileInfo,
                                 read => UpdateHashReadProgress(read, updateProgress, updateTotalProgress),
                                 token);

        protected virtual ConfiguredTaskAwaitable<byte[]> GetHashAsync<T>(
            Stream            stream,
            bool              updateProgress      = true,
            bool              updateTotalProgress = true,
            CancellationToken token               = default)
            where T : NonCryptographicHashAlgorithm, new() =>
            Hash.GetHashAsync<T>(stream,
                                 read => UpdateHashReadProgress(read, updateProgress, updateTotalProgress),
                                 token);

        protected virtual byte[] GetHash<T>(
            string            filePath,
            bool              updateProgress      = true,
            bool              updateTotalProgress = true,
            CancellationToken token               = default)
            where T : NonCryptographicHashAlgorithm, new() =>
            Hash.GetHash<T>(filePath,
                            read => UpdateHashReadProgress(read, updateProgress, updateTotalProgress),
                            token);

        protected virtual byte[] GetHash<T>(
            FileInfo          fileInfo,
            bool              updateProgress      = true,
            bool              updateTotalProgress = true,
            CancellationToken token               = default)
            where T : NonCryptographicHashAlgorithm, new() =>
            Hash.GetHash<T>(fileInfo,
                            read => UpdateHashReadProgress(read, updateProgress, updateTotalProgress),
                            token);

        protected virtual byte[] GetHash<T>(
            Stream            stream,
            bool              updateProgress      = true,
            bool              updateTotalProgress = true,
            CancellationToken token               = default)
            where T : NonCryptographicHashAlgorithm, new() =>
            Hash.GetHash<T>(stream,
                            read => UpdateHashReadProgress(read, updateProgress, updateTotalProgress),
                            token);

        private void UpdateHashReadProgress(int read, bool updateProgress, bool updateTotalProgress)
        {
            // If progress update is not allowed, then return
            if (!updateProgress)
            {
                return;
            }

            // Otherwise, perform the progress update
            // Increment total size counter if allowed
            if (updateTotalProgress)
                Interlocked.Add(ref ProgressAllSizeCurrent, read);

            // Increment per file size counter
            Interlocked.Add(ref ProgressPerFileSizeCurrent, read);

            // Update status and progress for Xxh64 calculation
            UpdateProgressCrc(read);
        }
        #endregion

        #region PatchTools
        protected virtual async ValueTask RunPatchTask(DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, long patchSize, Memory<byte> patchHash,
                                                       string patchURL, string patchOutputFile, string inputFile, string outputFile, bool isNeedRename = false, CancellationToken token = default)
            => await RunPatchTask(downloadClient, downloadProgress, patchSize,
                patchHash, patchURL, new FileInfo(patchOutputFile).EnsureNoReadOnly(), new FileInfo(inputFile).EnsureNoReadOnly(),
                new FileInfo(outputFile).EnsureCreationOfDirectory().EnsureNoReadOnly(), isNeedRename, token);

        protected virtual async ValueTask RunPatchTask(DownloadClient downloadClient, DownloadProgressDelegate downloadProgress, long patchSize, Memory<byte> patchHash,
                                                       string patchURL, FileInfo patchOutputFile, FileInfo inputFile, FileInfo outputFile, bool isNeedRename = false, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(patchOutputFile);
            ArgumentNullException.ThrowIfNull(inputFile);
            ArgumentNullException.ThrowIfNull(outputFile);

            // If file doesn't exist, then download the patch first
            if (!patchOutputFile.Exists || patchOutputFile.Length != patchSize)
            {
                // Download patch File first
                await RunDownloadTask(patchSize, patchOutputFile, patchURL, downloadClient, downloadProgress, token);
            }

            // Always do loop if patch doesn't get downloaded properly
            while (true)
            {
                await using FileStream patchFileStream = await patchOutputFile.NaivelyOpenFileStreamAsync(FileMode.Open, FileAccess.Read, FileShare.None);
                // Verify the patch file and if it doesn't match, then re-download it
                byte[] patchCrc = await GetCryptoHashAsync<MD5>(patchFileStream, null, true, false, token);
                if (!IsArrayMatch(patchCrc, patchHash.Span))
                {
                    // Revert back the total size
                    Interlocked.Add(ref ProgressAllSizeCurrent, -patchSize);

                    // Re-download the patch file
                    await RunDownloadTask(patchSize, patchOutputFile, patchURL, downloadClient, downloadProgress, token);
                    continue;
                }

                // else, break and quit from loop
                break;
            }

            // Start patching process
            BinaryPatchUtility patchUtil = new BinaryPatchUtility();
            try
            {
                string inputFilePath = inputFile.FullName;
                string patchFilePath = patchOutputFile.FullName;
                string outputFilePath = outputFile.FullName;

                // Subscribe patching progress and start applying patch
                patchUtil.ProgressChanged += RepairTypeActionPatching_ProgressChanged;
                patchUtil.Initialize(inputFilePath, patchFilePath, outputFilePath);
                await Task.Run(() => patchUtil.Apply(token), token);

                // Delete old block
                inputFile.Refresh();
                inputFile.Delete();
                if (isNeedRename)
                {
                    // Rename to the original filename
                    outputFile.Refresh();
                    outputFile.MoveTo(inputFile.FullName, true);
                }
            }
            finally
            {
                // Delete the patch file and unsubscribe the patching progress
                patchOutputFile.Refresh();
                if (patchOutputFile.Exists)
                    patchOutputFile.Delete();

                patchUtil.ProgressChanged -= RepairTypeActionPatching_ProgressChanged;
            }
        }
        #endregion

        #region DialogTools
        protected async Task SpawnRepairDialog(List<T1> assetIndex, Action? actionIfInteractiveCancel)
        {
            ArgumentNullException.ThrowIfNull(assetIndex);
            long totalSize = assetIndex.Sum(x => x.GetAssetSize());
            StackPanel content = CollapseUIExtension.CreateStackPanel();

            content.AddElementToStackPanel(new TextBlock
            {
                Text = string.Format(Lang._InstallMgmt.RepairFilesRequiredSubtitle!, assetIndex.Count, ConverterTool.SummarizeSizeSimple(totalSize)),
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap
            });
            Button showBrokenFilesButton = content.AddElementToStackPanel(
                CollapseUIExtension.CreateButtonWithIcon<Button>(
                    Lang._InstallMgmt!.RepairFilesRequiredShowFilesBtn,
                    "\uf550",
                    "FontAwesomeSolid",
                    "AccentButtonStyle"
                )
                .WithHorizontalAlignment(HorizontalAlignment.Center));

            showBrokenFilesButton.Click += async (_, _) =>
            {
                string tempPath = Path.GetTempFileName() + ".log";

                await using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                {
                    await using (StreamWriter sw = new StreamWriter(fs))
                    {
                        await sw.WriteLineAsync($"Original Path: {GamePath}");
                        await sw.WriteLineAsync($"Total size to download: {ConverterTool.SummarizeSizeSimple(totalSize)} ({totalSize} bytes)");
                        await sw.WriteLineAsync();

                        foreach (T1 fileList in assetIndex)
                        {
                            await sw.WriteLineAsync(fileList.PrintSummary());
                        }
                    }
                }

                Process proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = tempPath,
                        UseShellExecute = true
                    }
                };
                proc.Start();
                await proc.WaitForExitAsync();

                try
                {
                    File.Delete(tempPath);
                }
                catch
                { 
                    // piped to parent
                }
            };

            if (totalSize == 0) return;

            ContentDialogResult result = await SimpleDialogs.SpawnDialog(
                string.Format(Lang._InstallMgmt.RepairFilesRequiredTitle!, assetIndex.Count),
                content,
                ParentUI,
                Lang._Misc!.Cancel,
                Lang._Misc.YesResume,
                null,
                ContentDialogButton.Primary,
                ContentDialogTheme.Warning);

            if (result == ContentDialogResult.None)
            {
                actionIfInteractiveCancel?.Invoke();
                throw new OperationCanceledException();
            }
        }
        #endregion

        #region HandlerUpdaters
        public void Dispatch(DispatcherQueueHandler handler, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            EnsureParentUINotNull();
            ParentUI.DispatcherQueue.TryEnqueue(priority, handler);
        }

        public Task DispatchAsync(Action handler, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            EnsureParentUINotNull();
            return ParentUI.DispatcherQueue.EnqueueAsync(handler, priority);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnsureParentUINotNull()
        {
            if (ParentUI == null)
            {
                throw new NullReferenceException("_parentUI cannot be null when the method is being called!");
            }
        }

        #nullable enable
        protected virtual void PopRepairAssetEntry(IAssetProperty? assetProperty = null)
        {
            try
            {
                if (ParentUI!.DispatcherQueue!.HasThreadAccess)
                {
                    if (assetProperty == null)
                    {
                        if (AssetEntry!.Count > 0) AssetEntry.RemoveAt(0);
                    }
                    else
                    {
                        AssetEntry.Remove(assetProperty);
                    }
                    return;
                }

                Dispatch(() =>
                {
                    if (assetProperty == null)
                    {
                        if (AssetEntry!.Count > 0) AssetEntry.RemoveAt(0);
                    }
                    else
                    {
                        AssetEntry.Remove(assetProperty);
                    }
                });
            }
            catch
            {
                // pipe to parent
            }
        }
        #nullable restore

        protected void UpdateAll()
        {
            UpdateStatus();
            UpdateProgress();
        }

        protected virtual void UpdateProgress()
        {
            ProgressChanged?.Invoke(this, Progress);

            if (Status is {IsProgressAllIndetermined: false, IsRunning: true})
            {
                WindowUtility.SetProgressValue((ulong)(Progress.ProgressAllPercentage * 10), 1000);
            }
        }

        protected virtual void UpdateStatus()
        {
            StatusChanged?.Invoke(this, Status);

            if (Status.IsProgressAllIndetermined)
            {
                WindowUtility.SetTaskBarState(TaskbarState.Indeterminate);
            }
            else if (Status.IsRunning)
            {
                WindowUtility.SetTaskBarState(TaskbarState.Normal);
            }
            else
            {
                WindowUtility.SetTaskBarState(TaskbarState.NoProgress);
            }
        }

        #endregion
    }
}
