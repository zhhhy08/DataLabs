namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient
{
    using StackExchange.Redis;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;

    public interface IConnectionMultiplexerWrapper : IDisposable
    {
        public ValueTask<IConnectionMultiplexer> CreateConnectionMultiplexerAsync(IActivity? activity, CancellationToken cancellationToken);
    }
}
