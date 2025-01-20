using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;

namespace CollapseLauncher.Interfaces
{
    internal interface IBackgroundActivity
    {
        event EventHandler<TotalPerFileProgress> ProgressChanged;
        event EventHandler<TotalPerFileStatus> StatusChanged;
        event EventHandler FlushingTrigger;

        bool IsRunning { get; }
        UIElement ParentUI { get; }
        void CancelRoutine();
        void Dispatch(DispatcherQueueHandler handler, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal);
        Task DispatchAsync(Action handler, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal);
    }
}
