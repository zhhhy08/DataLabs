namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConcurrencyManager
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IConcurrencyManager : IDisposable
    {
        public abstract int MaxConcurrency { get; }
        public abstract int NumAvailables { get; }
        public abstract int NumRunning { get; }

        public Task AcquireResourceAsync(CancellationToken cancellationToken);
        public Task<bool> AcquireResourceAsync(int millisecondsTimeout, CancellationToken cancellationToken);
        public int ReleaseResource();
        public Task<bool> SetNewMaxConcurrencyAsync(int maxConcurrency);
    }
}
