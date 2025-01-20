using Hi3Helper;
using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Extension
{
    public delegate Task<TResult?> ActionTimeoutValueTaskCallback<TResult>(CancellationToken token);
    public delegate void ActionOnTimeOutRetry(int retryAttemptCount, int retryAttemptTotal, int timeOutSecond, int timeOutStep);
    internal static class TaskExtensions
    {
        internal const int DefaultTimeoutSec = 10;
        internal const int DefaultRetryAttempt = 5;

        internal static async Task AsTaskAndDoAction<TResult>(this Task<TResult?> taskResult, Action<TResult?> doAction)
        {
            TResult? result = await taskResult;
            doAction(result);
        }

        internal static async Task<TResult?> WaitForRetryAsync<TResult>(this ActionTimeoutValueTaskCallback<TResult?> funcCallback, int? timeout = null,
            int? timeoutStep = null, int? retryAttempt = null, ActionOnTimeOutRetry? actionOnRetry = null, CancellationToken fromToken = default)
            => await WaitForRetryAsync(() => funcCallback, timeout, timeoutStep, retryAttempt, actionOnRetry, fromToken);

        internal static async Task<TResult?> WaitForRetryAsync<TResult>(Func<ActionTimeoutValueTaskCallback<TResult?>> funcCallback, int? timeout = null,
            int? timeoutStep = null, int? retryAttempt = null, ActionOnTimeOutRetry? actionOnRetry = null, CancellationToken fromToken = default)
        {
            timeout ??= DefaultTimeoutSec;
            timeoutStep ??= 0;

            retryAttempt ??= DefaultRetryAttempt;

            int retryAttemptCurrent = 1;
            Exception? lastException = null;
            while (retryAttemptCurrent < retryAttempt)
            {
                fromToken.ThrowIfCancellationRequested();
                CancellationTokenSource? innerCancellationToken = null;
                CancellationTokenSource? consolidatedToken = null;

                try
                {
                    innerCancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(timeout ?? DefaultTimeoutSec));
                    consolidatedToken = CancellationTokenSource.CreateLinkedTokenSource(innerCancellationToken.Token, fromToken);

                    ActionTimeoutValueTaskCallback<TResult?> delegateCallback = funcCallback();
                    return await delegateCallback(consolidatedToken.Token);
                }
                catch (OperationCanceledException) when (fromToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    lastException = ex;
                    actionOnRetry?.Invoke(retryAttemptCurrent, (int)retryAttempt, timeout ?? 0, timeoutStep ?? 0);

                    if (ex is TimeoutException)
                    {
                        string msg = $"The operation has timed out! Retrying attempt left: {retryAttemptCurrent}/{retryAttempt}";
                        Logger.LogWriteLine(msg, LogType.Warning, true);
                    }
                    else
                    {
                        string msg = $"The operation has thrown an exception! Retrying attempt left: {retryAttemptCurrent}/{retryAttempt}\r\n{ex}";
                        Logger.LogWriteLine(msg, LogType.Error, true);
                    }

                    retryAttemptCurrent++;
                    timeout += timeoutStep;
                }
                finally
                {
                    innerCancellationToken?.Dispose();
                    consolidatedToken?.Dispose();
                }
            }

            if (lastException is not null
                && !fromToken.IsCancellationRequested)
                throw lastException is TaskCanceledException ? 
                    new TimeoutException("The operation has timed out with inner exception!", lastException) :
                    lastException;

            throw new TimeoutException("The operation has timed out!");
        }
    }
}
