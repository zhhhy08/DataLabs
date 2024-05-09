namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    

    public class RetryPolicy : IRetryPolicy
    {
        #region Properties

        public IRetryStrategy RetryStrategy
        {
            get;
        }

        public IErrorStrategy ErrorStrategy
        {
            get;
        }

        #endregion

        #region Constructors

        public RetryPolicy(IErrorStrategy errorStrategy, IRetryStrategy retryStrategy)
        {
            GuardHelper.ArgumentNotNull(errorStrategy);
            GuardHelper.ArgumentNotNull(retryStrategy);

            this.ErrorStrategy = errorStrategy;
            this.RetryStrategy = retryStrategy;
        }

        public RetryPolicy(IErrorStrategy errorStrategy, int retryCount, TimeSpan retryInterval)
            : this(errorStrategy, new FixedInterval(retryCount, retryInterval))
        {
        }

        public RetryPolicy(IErrorStrategy errorStrategy, int retryCount, TimeSpan initialInterval, TimeSpan increment)
            : this(errorStrategy, new Incremental(retryCount, initialInterval, increment))
        {
        }

        public RetryPolicy(IErrorStrategy errorStrategy, int retryCount, TimeSpan minBackoff, TimeSpan maxBackoff, TimeSpan deltaBackoff)
            : this(errorStrategy, new ExponentialBackoff(retryCount, minBackoff, maxBackoff, deltaBackoff))
        {
        }

        #endregion

        public void Execute(Action action)
        {
            GuardHelper.ArgumentNotNull(action);

            this.Execute<object?>(() =>
            {
                action();
                return null;
            });
        }

        public Task ExecuteAsync(Func<Task> action, Action<int, Exception, TimeSpan>? onRetryingAction = null)
        {
            GuardHelper.ArgumentNotNull(action);

            return this.ExecuteAsync(() =>
            {
                var tcs = new TaskCompletionSource<object>();
                action().ContinueWith(task =>
                {
                    if (task.IsCanceled)
                    {
                        tcs.TrySetCanceled();
                    }
                    else if (task.IsFaulted)
                    {
                        Debug.Assert(task.Exception != null);
                        tcs.TrySetException(task.Exception.InnerExceptions);
                    }
                    else
                    {
                        tcs.TrySetResult(new object());
                    }
                });
                return tcs.Task;
            }, onRetryingAction);
        }

        public TResult Execute<TResult>(Func<TResult> func, Action<int, Exception, TimeSpan>? onRetryingAction = null)
        {
            GuardHelper.ArgumentNotNull(func);

            var retryCount = 0;
            while (true)
            {
                TimeSpan retryAfter;
                Exception exception;
                try
                {
                    return func();
                }
                catch (Exception ex)
                {
                    exception = ex;
                    if (!this.ErrorStrategy.IsTransientError(ex) ||
                        !this.RetryStrategy.ShouldRetry(retryCount++, ex, out retryAfter))
                    {
                        throw;
                    }
                }

                if (retryAfter.TotalMilliseconds < 0.0)
                {
                    retryAfter = TimeSpan.Zero;
                }
                onRetryingAction?.Invoke(
                    retryCount, exception, retryAfter);

                Thread.Sleep(retryAfter);
            }
        }

        public async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> func, Action<int, Exception, TimeSpan>? onRetryingAction = null)
        {
            GuardHelper.ArgumentNotNull(func);

            var retryCount = 0;
            while (true)
            {
                TimeSpan retryAfter;
                Exception exception;
                try
                {
                    return await func().IgnoreContext();
                }
                catch (Exception ex)
                {
                    exception = ex;
                    if (!this.ErrorStrategy.IsTransientError(ex) ||
                        !this.RetryStrategy.ShouldRetry(retryCount++, ex, out retryAfter))
                    {
                        throw;
                    }
                }

                if (retryAfter.TotalMilliseconds < 0.0)
                {
                    retryAfter = TimeSpan.Zero;
                }
                onRetryingAction?.Invoke(
                    retryCount, exception, retryAfter);

                await Task.Delay(retryAfter).IgnoreContext();
            }
        }

        public async Task<TResult> ExecuteWithAsyncHandlerAsync<TResult>(Func<Task<TResult>> func, Func<int, Exception, TimeSpan, Task>? onRetryingAsyncAction = null)
        {
            GuardHelper.ArgumentNotNull(func);

            var retryCount = 0;
            while (true)
            {
                TimeSpan retryAfter;
                Exception exception;
                try
                {
                    return await func().IgnoreContext();
                }
                catch (Exception ex)
                {
                    exception = ex;
                    if (!this.ErrorStrategy.IsTransientError(ex) ||
                        !this.RetryStrategy.ShouldRetry(retryCount++, ex, out retryAfter))
                    {
                        throw;
                    }
                }

                if (retryAfter.TotalMilliseconds < 0.0)
                {
                    retryAfter = TimeSpan.Zero;
                }

                if (onRetryingAsyncAction != null)
                {
                    await onRetryingAsyncAction(
                        retryCount, exception, retryAfter).IgnoreContext();
                }

                await Task.Delay(retryAfter).IgnoreContext();
            }
        }
    }
}
