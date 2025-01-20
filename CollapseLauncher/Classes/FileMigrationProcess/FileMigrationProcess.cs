using CollapseLauncher.FileDialogCOM;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    internal partial class FileMigrationProcess
    {
        private const int RefreshInterval = 100; // 100ms UI refresh interval

        private string                  DialogTitle       { get; }
        private string                  InputPath         { get; }
        private string                  OutputPath        { get; }
        private bool                    IsFileTransfer    { get; }
        private UIElement               ParentUI          { get; }
        private CancellationTokenSource TokenSource       { get; }
        private bool                    IsSameOutputDrive { get; set; }

        private long _currentSizeMoved;
        private long _currentFileCountMoved;
        private long _totalFileSize;
        private long _totalFileCount;
        private Stopwatch _processStopwatch;
        private Stopwatch _eventsStopwatch;

        private FileMigrationProcess(UIElement parentUI, string dialogTitle, string inputPath, string outputPath, bool isFileTransfer, CancellationTokenSource tokenSource)
        {
            DialogTitle       = dialogTitle;
            InputPath         = inputPath;
            OutputPath        = outputPath;
            IsFileTransfer    = isFileTransfer;
            ParentUI          = parentUI;
            TokenSource       = tokenSource;
        }

        internal async Task<string> StartRoutine()
        {
            bool isSuccess = false;
            _currentSizeMoved = 0;
            _currentFileCountMoved = 0;
            _totalFileSize = 0;
            _totalFileCount = 0;
            FileMigrationProcessUIRef? uiRef = null;

            try
            {
                if (!await IsOutputPathSpaceSufficient(InputPath, OutputPath))
                    throw new OperationCanceledException("Disk space is not sufficient. Cancelling!");

                if (FileDialogHelper.IsRootPath(OutputPath))
                {
                    throw new NotSupportedException("Cannot move game to the root of the drive!");
                }

                uiRef = BuildMainMigrationUI();
                string outputPath = await StartRoutineInner(uiRef.Value);
                uiRef.Value.MainDialogWindow!.Hide();
                isSuccess = true;

                return outputPath;
            }
            catch when (!isSuccess) // Throw if the isSuccess is not set to true
            {
                if (!uiRef.HasValue || uiRef.Value.MainDialogWindow == null)
                {
                    throw;
                }

                uiRef.Value.MainDialogWindow.Hide();
                await Task.Delay(500); // Give artificial delay to give main dialog window thread to close first
                throw;
            }
            finally
            {
                _processStopwatch?.Stop();
                _eventsStopwatch?.Stop();
            }
        }

        private async Task<string> StartRoutineInner(FileMigrationProcessUIRef uiRef)
        {
            _processStopwatch = Stopwatch.StartNew();
            _eventsStopwatch = Stopwatch.StartNew();
            return IsFileTransfer ? await MoveFile(uiRef) : await MoveDirectory(uiRef);
        }

        private async Task<string> MoveFile(FileMigrationProcessUIRef uiRef)
        {
            FileInfo inputPathInfo = new FileInfo(InputPath);
            FileInfo outputPathInfo = new FileInfo(OutputPath);

            var inputPathDir = FileDialogHelper.IsRootPath(InputPath)
                ? Path.GetPathRoot(InputPath)
                : Path.GetDirectoryName(inputPathInfo.FullName);

            if (string.IsNullOrEmpty(inputPathDir))
                throw new InvalidOperationException(string.Format(Locale.Lang._Dialogs.InvalidGameDirNewTitleFormat,
                                                                  InputPath));
            
            DirectoryInfo outputPathDirInfo = new DirectoryInfo(inputPathDir);
            outputPathDirInfo.Create();

            // Update path display
            string inputFileBasePath = inputPathInfo.FullName[(inputPathDir!.Length + 1)..];
            UpdateCountProcessed(uiRef, inputFileBasePath);

            if (IsSameOutputDrive)
            {
                Logger.LogWriteLine($"[FileMigrationProcess::MoveFile()] Moving file in the same drive from: {inputPathInfo.FullName} to {outputPathInfo.FullName}", LogType.Default, true);
                inputPathInfo.MoveTo(outputPathInfo.FullName, true);
                UpdateSizeProcessed(uiRef, inputPathInfo.Length);
            }
            else
            {
                Logger.LogWriteLine($"[FileMigrationProcess::MoveFile()] Moving file across different drives from: {inputPathInfo.FullName} to {outputPathInfo.FullName}", LogType.Default, true);
                await MoveWriteFile(uiRef, inputPathInfo, outputPathInfo, TokenSource?.Token ?? default);
            }

            return outputPathInfo.FullName;
        }

        private async Task<string> MoveDirectory(FileMigrationProcessUIRef uiRef)
        {
            DirectoryInfo inputPathInfo = new DirectoryInfo(InputPath);
            DirectoryInfo outputPathInfo = new DirectoryInfo(OutputPath);
            outputPathInfo.Create();

            int parentInputPathLength = inputPathInfo.Parent!.FullName.Length + 1;
            string outputDirBaseNamePath = inputPathInfo.FullName.Substring(parentInputPathLength);
            string outputDirPath = Path.Combine(OutputPath, outputDirBaseNamePath);

            await Parallel.ForEachAsync(
                inputPathInfo.EnumerateFiles("*", SearchOption.AllDirectories),
                new ParallelOptions
                {
                    CancellationToken = TokenSource?.Token ?? default,
                    MaxDegreeOfParallelism = LauncherConfig.AppCurrentThread
                },
                async (inputFileInfo, cancellationToken) =>
                {
                    int parentInputPathLengthLocal = inputPathInfo.Parent!.FullName.Length + 1;
                    string inputFileBasePath = inputFileInfo!.FullName[parentInputPathLengthLocal..];

                    // Update path display
                    UpdateCountProcessed(uiRef, inputFileBasePath);

                    string outputTargetPath = Path.Combine(outputPathInfo.FullName, inputFileBasePath);
                    string outputTargetDirPath = Path.GetDirectoryName(outputTargetPath) ?? Path.GetPathRoot(outputTargetPath);

                    if (string.IsNullOrEmpty(outputTargetDirPath)) 
                        throw new InvalidOperationException(string.Format(Locale.Lang._Dialogs.InvalidGameDirNewTitleFormat,
                                                                          InputPath));
                    
                    DirectoryInfo outputTargetDirInfo = new DirectoryInfo(outputTargetDirPath);
                    outputTargetDirInfo.Create();

                    if (IsSameOutputDrive)
                    {
                        Logger.LogWriteLine($"[FileMigrationProcess::MoveDirectory()] Moving directory content in the same drive from: {inputFileInfo.FullName} to {outputTargetPath}", LogType.Default, true);
                        inputFileInfo.MoveTo(outputTargetPath, true);
                        UpdateSizeProcessed(uiRef, inputFileInfo.Length);
                    }
                    else
                    {
                        Logger.LogWriteLine($"[FileMigrationProcess::MoveDirectory()] Moving directory content across different drives from: {inputFileInfo.FullName} to {outputTargetPath}", LogType.Default, true);
                        FileInfo outputFileInfo = new FileInfo(outputTargetPath);
                        await MoveWriteFile(uiRef, inputFileInfo, outputFileInfo, cancellationToken);
                    }
                });

            inputPathInfo.Delete(true);
            return outputDirPath;
        }
    }
}
