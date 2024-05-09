namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConcurrencyManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    //using Microsoft.WindowsAzure.Governance.ResourcesCache.ActivityTracing;
    //using Microsoft.WindowsAzure.Governance.ResourcesCache.Shared.Utilities;
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// SemaphoreSlimGuard
    /// NOTE: This class needs to be disposed after using, or otherwise will throw an unhandled exception in the finalizer
    /// and crash the entire process.
    /// </summary>
    /// <seealso cref="IDisposable" />
    public class SemaphoreSlimGuard : IDisposable
    {
        #region Tracing

        private static readonly ActivityMonitorFactory SemaphoreSlimGuardReleaseSemaphoreFailedFactory =
            new ActivityMonitorFactory("SemaphoreSlimGuard.ReleaseSemaphoreFailed");

        private static readonly ActivityMonitorFactory SemaphoreSlimGuardSemaphoreLeftUnreleasedFactory =
            new ActivityMonitorFactory("SemaphoreSlimGuard.SemaphoreLeftUnreleased");

        private static readonly ActivityMonitorFactory SemaphoreSlimGuardSempahoreDisposedFactory =
            new ActivityMonitorFactory("SemaphoreSlimGuard.SempahoreDisposed");

        private static readonly ActivityMonitorFactory SemaphoreSlimGuardCreateAsyncFactory =
            new ActivityMonitorFactory("SemaphoreSlimGuard.CreateAsync");

        private static readonly ActivityMonitorFactory SemaphoreSlimGuardCreateFactory =
            new ActivityMonitorFactory("SemaphoreSlimGuard.Create");

        #endregion

        #region Fields

        private SemaphoreSlim? _semaphoreSlim;

        private readonly IActivity _parentActivity;

        #endregion

        #region Internal

        internal static event UnhandledExceptionEventHandler? UnhandledExceptionHandler;

        internal bool ThrowUnhandledExceptions = true;

        #endregion

        #region Contructors

        private SemaphoreSlimGuard(SemaphoreSlim semaphoreSlim, IActivity parentActivity)
        {
            this._semaphoreSlim = semaphoreSlim;
            this._parentActivity = parentActivity;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1065:DoNotRaiseExceptionsInUnexpectedLocations",
            Justification = "Intentional so that potential deadlock issues can be captured as early as possible.")]
        ~SemaphoreSlimGuard()
        {
            try
            {
                if (this._semaphoreSlim != null)
                {
                    throw new LockNotReleasedException("The semaphore slim is acquired, but never released.");
                }
            }
            catch (Exception ex)
            {
                var monitor = SemaphoreSlimGuardSemaphoreLeftUnreleasedFactory.ToMonitor(this._parentActivity);
                monitor.OnError(ex);
                if (UnhandledExceptionHandler != null)
                {
                    UnhandledExceptionHandler.Invoke(this, new UnhandledExceptionEventArgs(ex, true));
                }
                // Give a little bit of time for the activity event to propagate properly before crashing the process
                Thread.Sleep(500);
                if (ThrowUnhandledExceptions)
                {
                    throw;
                }
            }
        }

        #endregion

        #region IDisposable Implementations

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    _semaphoreSlim?.Release();
                }
                catch (ObjectDisposedException objectDiposedEx)
                {
                    // Since the semaphore slim guard does not assume ownership, if the semaphore has been disposed already,
                    // We can safely assume the lock has been released and there is no dead lock. We are only logging a warning here.
                    var monitor = SemaphoreSlimGuardSempahoreDisposedFactory.ToMonitor(this._parentActivity);
                    monitor.OnError(objectDiposedEx);
                }
                catch (Exception ex)
                {
                    var monitor = SemaphoreSlimGuardReleaseSemaphoreFailedFactory.ToMonitor(this._parentActivity);
                    monitor.OnError(ex);
                    throw;
                }
            }

            _semaphoreSlim = null;
        }

        #endregion

        #region Public Static Methods

        public static async Task<SemaphoreSlimGuard> CreateAsync(SemaphoreSlim semaphoreSlim,
            CancellationToken cancellationToken,
            Guid? contextId = null,
            bool logWaitDuration = false,
            [CallerFilePath] string? callerFilePath = null, [CallerMemberName] string? callerName = null,
            [CallerLineNumber] int callerLineNumber = 0)
        {
            GuardHelper.ArgumentNotNull(semaphoreSlim);

            var methodMonitor = SemaphoreSlimGuardCreateAsyncFactory.ToMonitor(correlationId: contextId.ToString());
            methodMonitor.Activity.Properties["CallerFilePath"] = callerFilePath;
            methodMonitor.Activity.Properties["CallerName"] = callerName;
            methodMonitor.Activity.Properties["CallerLineNumber"] = callerLineNumber;
            methodMonitor.OnStart(false);

            try
            {
                await AcquireSemaphoreSlimAsync(semaphoreSlim, cancellationToken,
                    logWaitDuration,
                    methodMonitor.Activity).IgnoreContext();

                methodMonitor.OnCompleted(logging: false);
                return new SemaphoreSlimGuard(semaphoreSlim, methodMonitor.Activity);
            }
            catch (Exception ex)
            {
                methodMonitor.OnError(ex);
                throw;
            }
        }

        public static async Task<SemaphoreSlimGuard> CreateAsync(SemaphoreSlim semaphoreSlim,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            Guid? contextId = null,
            bool logWaitDuration = false,
            [CallerFilePath] string? callerFilePath = null, [CallerMemberName] string? callerName = null,
            [CallerLineNumber] int callerLineNumber = 0)
        {
            GuardHelper.ArgumentNotNull(semaphoreSlim);
            GuardHelper.IsArgumentGreaterThan(timeout, TimeSpan.Zero);

            var methodMonitor = SemaphoreSlimGuardCreateAsyncFactory.ToMonitor(correlationId: contextId.ToString());
            methodMonitor.Activity.Properties["CallerFilePath"] = callerFilePath;
            methodMonitor.Activity.Properties["CallerName"] = callerName;
            methodMonitor.Activity.Properties["CallerLineNumber"] = callerLineNumber;
            methodMonitor.Activity.Properties["TimeoutInMs"] = timeout.RoundedMilliseconds();
            methodMonitor.OnStart(false);

            try
            {
                await AcquireSemaphoreSlimAsync(semaphoreSlim, timeout, cancellationToken,
                    logWaitDuration, methodMonitor.Activity).IgnoreContext();

                methodMonitor.OnCompleted(logging: false);
                return new SemaphoreSlimGuard(semaphoreSlim, methodMonitor.Activity);
            }
            catch (Exception ex)
            {
                methodMonitor.OnError(ex);
                throw;
            }
        }

        public static async Task<SemaphoreSlimGuard> CreateAsync(SemaphoreSlim semaphoreSlim,
            CancellationToken cancellationToken,
            IActivity parentActivity,
            bool logWaitDuration = false)
        {
            GuardHelper.ArgumentNotNull(semaphoreSlim);

            await AcquireSemaphoreSlimAsync(semaphoreSlim, cancellationToken,
                logWaitDuration,
                parentActivity).IgnoreContext();

            return new SemaphoreSlimGuard(semaphoreSlim, parentActivity);
        }

        public static async Task<SemaphoreSlimGuard> CreateAsync(SemaphoreSlim semaphoreSlim,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            IActivity parentActivity,
            bool logWaitDuration = false)
        {
            GuardHelper.ArgumentNotNull(semaphoreSlim);

            await AcquireSemaphoreSlimAsync(
                semaphoreSlim, timeout, cancellationToken, logWaitDuration, parentActivity).IgnoreContext();

            return new SemaphoreSlimGuard(semaphoreSlim, parentActivity);
        }

        public static SemaphoreSlimGuard Create(SemaphoreSlim semaphoreSlim,
            CancellationToken cancellationToken,
            Guid? contextId = null,
            bool logWaitDuration = false,
            [CallerFilePath] string? callerFilePath = null, [CallerMemberName] string? callerName = null,
            [CallerLineNumber] int callerLineNumber = 0)
        {
            GuardHelper.ArgumentNotNull(semaphoreSlim);

            var methodMonitor = SemaphoreSlimGuardCreateAsyncFactory.ToMonitor(correlationId: contextId.ToString());
            methodMonitor.Activity.Properties["CallerFilePath"] = callerFilePath;
            methodMonitor.Activity.Properties["CallerName"] = callerName;
            methodMonitor.Activity.Properties["CallerLineNumber"] = callerLineNumber;
            methodMonitor.OnStart(false);

            try
            {
                AcquireSemaphoreSlim(semaphoreSlim, cancellationToken,
                    logWaitDuration, methodMonitor.Activity);

                methodMonitor.OnCompleted(logging: false);
                return new SemaphoreSlimGuard(semaphoreSlim, methodMonitor.Activity);
            }
            catch (Exception ex)
            {
                methodMonitor.OnError(ex);
                throw;
            }
        }

        public static SemaphoreSlimGuard Create(SemaphoreSlim semaphoreSlim,
            TimeSpan timeout, CancellationToken cancellationToken,
            Guid? contextId = null,
            bool logWaitDuration = false,
            [CallerFilePath] string? callerFilePath = null, [CallerMemberName] string? callerName = null,
            [CallerLineNumber] int callerLineNumber = 0)
        {
            GuardHelper.ArgumentNotNull(semaphoreSlim);
            GuardHelper.IsArgumentGreaterThan(timeout, TimeSpan.Zero);

            var methodMonitor = SemaphoreSlimGuardCreateAsyncFactory.ToMonitor(correlationId: contextId.ToString());
            methodMonitor.Activity.Properties["CallerFilePath"] = callerFilePath;
            methodMonitor.Activity.Properties["CallerName"] = callerName;
            methodMonitor.Activity.Properties["CallerLineNumber"] = callerLineNumber;
            methodMonitor.Activity.Properties["TimeoutInMs"] = timeout.RoundedMilliseconds();
            methodMonitor.OnStart(false);

            try
            {
                AcquireSemaphoreSlim(semaphoreSlim, timeout, cancellationToken,
                    logWaitDuration, methodMonitor.Activity);

                methodMonitor.OnCompleted(logging: false);
                return new SemaphoreSlimGuard(semaphoreSlim, methodMonitor.Activity);
            }
            catch (Exception ex)
            {
                methodMonitor.OnError(ex);
                throw;
            }
        }

        #endregion

        #region Private Helper Methods

        private static async Task AcquireSemaphoreSlimAsync(SemaphoreSlim semaphoreSlim,
            CancellationToken cancellationToken, bool logWaitDuration, IActivity parentActivity)
        {
            if (logWaitDuration)
            {
                var stopWatch = Stopwatch.StartNew();

                await semaphoreSlim.WaitAsync(cancellationToken).IgnoreContext();

                if (parentActivity != null)
                {
                    parentActivity.Properties["SemaphoreWaitDuration"] =
                        stopWatch.ElapsedMilliseconds;
                }
            }
            else
            {
                await semaphoreSlim.WaitAsync(cancellationToken).IgnoreContext();
            }
        }

        private static async Task AcquireSemaphoreSlimAsync(SemaphoreSlim semaphoreSlim,
            TimeSpan timeout, CancellationToken cancellationToken, bool logWaitDuration, IActivity parentActivity)
        {
            if (logWaitDuration)
            {
                var stopWatch = Stopwatch.StartNew();

                if (!await semaphoreSlim.WaitAsync(timeout, cancellationToken).IgnoreContext())
                {
                    throw new TimeoutException($"Semaphore wait timed out after {timeout.TotalMilliseconds} ms.");
                }

                parentActivity.Properties["SemaphoreWaitDuration"] = stopWatch.ElapsedMilliseconds;
            }
            else
            {
                if (!await semaphoreSlim.WaitAsync(timeout, cancellationToken).IgnoreContext())
                {
                    throw new TimeoutException($"Semaphore wait timed out after {timeout.TotalMilliseconds} ms.");
                }
            }
        }

        private static void AcquireSemaphoreSlim(SemaphoreSlim semaphoreSlim,
            CancellationToken cancellationToken, bool logWaitDuration, IActivity parentActivity)
        {
            if (logWaitDuration)
            {
                var stopWatch = Stopwatch.StartNew();

                semaphoreSlim.Wait(cancellationToken);

                parentActivity.Properties["SemaphoreWaitDuration"] = stopWatch.ElapsedMilliseconds;
            }
            else
            {
                semaphoreSlim.Wait(cancellationToken);
            }
        }

        private static void AcquireSemaphoreSlim(SemaphoreSlim semaphoreSlim,
            TimeSpan timeout, CancellationToken cancellationToken, bool logWaitDuration, IActivity parentActivity)
        {
            if (logWaitDuration)
            {
                var stopWatch = Stopwatch.StartNew();

                if (!semaphoreSlim.Wait(timeout, cancellationToken))
                {
                    throw new TimeoutException($"Semaphore wait timed out after {timeout.TotalMilliseconds} ms.");
                }

                parentActivity.Properties["SemaphoreWaitDuration"] = stopWatch.ElapsedMilliseconds;
            }
            else
            {
                if (!semaphoreSlim.Wait(timeout, cancellationToken))
                {
                    throw new TimeoutException($"Semaphore wait timed out after {timeout.TotalMilliseconds} ms.");
                }
            }
        }

        #endregion
    }
}
