namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public static class TaskExtensions
    {
        public static ConfiguredTaskAwaitable IgnoreContext(this Task task) => task.ConfigureAwait(false);

        public static ConfiguredTaskAwaitable<T> IgnoreContext<T>(this Task<T> task) => task.ConfigureAwait(false);

        public static ConfiguredValueTaskAwaitable IgnoreContext(this ValueTask task) => task.ConfigureAwait(false);

        public static ConfiguredValueTaskAwaitable<T> IgnoreContext<T>(this ValueTask<T> task) => task.ConfigureAwait(false);

        public static ConfiguredCancelableAsyncEnumerable<T> IgnoreContext<T>(this ConfiguredCancelableAsyncEnumerable<T> task) => task.ConfigureAwait(false);

        /// <summary>
        /// NOTE: Activity TaskExtensions.TraceOriginalException should be opted out to avoid clutter in ActivityStarted logs
        /// </summary>
        public static Task WithTimeout(this Task task, TimeSpan timeout)
        {
            GuardHelper.ArgumentNotNull(task);

            return task.ToGenericTask<object>().WithTimeout(timeout);
        }

        /// <summary>
        /// NOTE: Activity TaskExtensions.TraceOriginalException should be opted out to avoid clutter in ActivityStarted logs
        /// </summary>
        public static async Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout)
        {
            GuardHelper.ArgumentNotNull(task);

            using (var timeoutTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None))
            {
                var timeoutTask = Task.Delay(timeout, timeoutTokenSource.Token);
                var completedTask = await Task.WhenAny(task, timeoutTask).IgnoreContext();

                if (completedTask == task)
                {
                    // Cancel the timeout task if the actual task completed first!
                    timeoutTokenSource.Cancel();

                    return await task.IgnoreContext();
                }
                throw new TaskTimeoutException();
            }
        }

        public static Task WithTimeout(
            this Task task, TimeSpan? timeout, CancellationTokenSource cancellationTokenSource)
        {
            GuardHelper.ArgumentNotNull(task);

            return task.ToGenericTask<object>().WithTimeout(timeout, cancellationTokenSource);
        }

        public static async Task<T> WithTimeout<T>(
            this Task<T> task, TimeSpan? timeout, CancellationTokenSource cancellationTokenSource)
        {
            GuardHelper.ArgumentNotNull(task);

            if (!timeout.HasValue)
            {
                return await task.IgnoreContext();
            }

            using (var timeoutTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token))
            {
                var timeoutTask = Task.Delay(timeout.Value, timeoutTokenSource.Token);
                var completedTask = await Task.WhenAny(timeoutTask, task).IgnoreContext();

                if (completedTask == task || cancellationTokenSource.IsCancellationRequested)
                {
                    timeoutTokenSource.Cancel();
                    return await task.IgnoreContext();
                }

                cancellationTokenSource.Cancel();

                throw new TaskTimeoutException();
            }
        }

        public static async Task<T> WithTimeoutResult<T>(
            this Task<T> task, TimeSpan? timeout, T timeoutResult,
            CancellationTokenSource cancellationTokenSource)
        {
            GuardHelper.ArgumentNotNull(task);

            if (!timeout.HasValue)
            {
                return await task.IgnoreContext();
            }

            using (var timeoutTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token))
            {
                var timeoutTask = Task.Delay(timeout.Value, timeoutTokenSource.Token);
                var completedTask = await Task.WhenAny(timeoutTask, task).IgnoreContext();

                if (completedTask == task || cancellationTokenSource.IsCancellationRequested)
                {
                    timeoutTokenSource.Cancel();
                    return await task.IgnoreContext();
                }

                cancellationTokenSource.Cancel();
                return timeoutResult;
            }
        }

        public static async Task<T?> ToGenericTask<T>(this Task task)
        {
            await task.IgnoreContext();
            return default;
        }
    }
}
