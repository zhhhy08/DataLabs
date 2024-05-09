namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RetryPolicy
{
    using System;
    using System.Threading.Tasks;

    public interface IRetryPolicy
    {
        #region Properties

        IRetryStrategy RetryStrategy
        {
            get;
        }

        IErrorStrategy ErrorStrategy
        {
            get;
        }

        #endregion

        void Execute(Action action);

        Task ExecuteAsync(Func<Task> action,
            Action<int, Exception, TimeSpan>? onRetryingAction = null);

        TResult Execute<TResult>(
            Func<TResult> func,
            Action<int, Exception, TimeSpan>? onRetryingAction = null);

        Task<TResult> ExecuteAsync<TResult>(
            Func<Task<TResult>> func,
            Action<int, Exception, TimeSpan>? onRetryingAction = null);

        Task<TResult> ExecuteWithAsyncHandlerAsync<TResult>(
            Func<Task<TResult>> func,
            Func<int, Exception, TimeSpan, Task>? onRetryingAsyncAction = null);
    }
}