using Hi3Helper;
using Hi3Helper.Data;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    internal partial class FileMigrationProcess
    {
        private void UpdateCountProcessed(FileMigrationProcessUIRef uiRef, string currentPathProcessed)
        {
            Interlocked.Add(ref _currentFileCountMoved, 1);

            string fileCountProcessedString = string.Format(Locale.Lang!._Misc!.PerFromTo!,
            _currentFileCountMoved,
            _totalFileCount);

            ParentUI!.DispatcherQueue!.TryEnqueue(() =>
            {
                uiRef.FileCountIndicatorSubtitle.Text = fileCountProcessedString;
                uiRef.PathActivitySubtitle.Text = currentPathProcessed;
            });
        }

        private async void UpdateSizeProcessed(FileMigrationProcessUIRef uiRef, long currentRead)
        {
            Interlocked.Add(ref _currentSizeMoved, currentRead);

            if (!await CheckIfNeedRefreshStopwatch())
            {
                return;
            }

            double percentage = Math.Round((double)_currentSizeMoved / _totalFileSize * 100d, 2);
            double speed      = _currentSizeMoved / _processStopwatch!.Elapsed.TotalSeconds;

            lock (uiRef.ProgressBarIndicator)
            {
                ParentUI.DispatcherQueue.TryEnqueue(() =>
                                                      {
                                                          string speedString = string.Format(Locale.Lang!._Misc!.SpeedPerSec!, ConverterTool.SummarizeSizeSimple(speed));
                                                          string sizeProgressString = string.Format(Locale.Lang._Misc.PerFromTo!,
                                                                   ConverterTool.SummarizeSizeSimple(_currentSizeMoved),
                                                                   ConverterTool.SummarizeSizeSimple(_totalFileSize));

                                                          uiRef.SpeedIndicatorSubtitle.Text          = speedString;
                                                          uiRef.FileSizeIndicatorSubtitle.Text       = sizeProgressString;
                                                          uiRef.ProgressBarIndicator.Value           = percentage;
                                                          uiRef.ProgressBarIndicator.IsIndeterminate = false;
                                                      });
            }
        }

        private async ValueTask<bool> CheckIfNeedRefreshStopwatch()
        {
            if (_eventsStopwatch!.ElapsedMilliseconds > RefreshInterval)
            {
                _eventsStopwatch.Restart();
                return true;
            }

            await Task.Delay(RefreshInterval);
            return false;
        }
    }
}
