namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.CacheClient
{
    using StackExchange.Redis;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using System.Diagnostics.CodeAnalysis;

    [ExcludeFromCodeCoverage]
    public class ConnectionMultiplexerWrapperFactory : IConnectionMultiplexerWrapperFactory
    {
        public IConnectionMultiplexerWrapper CreateConnectionMultiplexerWrapper(DataLabCacheNode dataLabCacheNode)
        {
            return new ConnectionMultiplexerWrapper(dataLabCacheNode);
        }

        public class ConnectionMultiplexerWrapper : IConnectionMultiplexerWrapper
        {
            private static readonly ILogger<ConnectionMultiplexerWrapper> Logger = DataLabLoggerFactory.CreateLogger<ConnectionMultiplexerWrapper>();

            private static readonly ActivityMonitorFactory ConnectionMultiplexerWrapperCreateConnectionMultiplexerAsync =
                new("ConnectionMultiplexerWrapper.CreateConnectionMultiplexerAsync", useDataLabsEndpoint: true);

            private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
            private readonly DataLabCacheNode _dataLabCacheNode;
            private IConnectionMultiplexer? _connectionMultiplexer;
            private volatile bool _disposed;

            public ConnectionMultiplexerWrapper(DataLabCacheNode dataLabCacheNode)
            {
                _dataLabCacheNode = dataLabCacheNode;
            }

            public async ValueTask<IConnectionMultiplexer> CreateConnectionMultiplexerAsync(IActivity? activity, CancellationToken cancellationToken)
            {
                if (_connectionMultiplexer != null)
                {
                    return _connectionMultiplexer;
                }

                // Connections doesn't exist yet
                // Let's create it

                var hasSemaphore = false;

                try
                {
                    // Let's set semaphore timeout as well
                    using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    var connectTimeout = _dataLabCacheNode.ConfigurationOptions.ConnectTimeout + 2000; // Add 2 seconds to the connect timeout
                    tokenSource.CancelAfter(connectTimeout);
                    cancellationToken = tokenSource.Token;

                    await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                    hasSemaphore = true;

                    await CreateConnectionMultiplexerAsync().ConfigureAwait(false);
                    return _connectionMultiplexer!;
                }
                finally
                {
                    if (hasSemaphore)
                    {
                        _connectionLock.Release();
                    }
                }
            }

            private async Task CreateConnectionMultiplexerAsync()
            {
                if (_connectionMultiplexer != null)
                {
                    return;
                }

                var monitor = ConnectionMultiplexerWrapperCreateConnectionMultiplexerAsync.ToMonitor();

                try
                {
                    monitor.OnStart(false);
                    var configurationOptions = _dataLabCacheNode.ConfigurationOptions;
                    monitor.Activity[SolutionConstants.EndPoint] = EndPointCollection.ToString(configurationOptions.EndPoints[0]);

                    var multiplexer = await ConnectionMultiplexer.ConnectAsync(configurationOptions).ConfigureAwait(false);
                    multiplexer.ConnectionFailed += _dataLabCacheNode.ConnectionFailedHandler;
                    multiplexer.ConnectionRestored += _dataLabCacheNode.ConnectionRestoredHandler;

                    Interlocked.Exchange(ref _connectionMultiplexer, multiplexer);

                    CacheClientMetricProvider.AddConnectionCreationSuccessCounter(_dataLabCacheNode.CachePoolName, _dataLabCacheNode.CacheNodeName);
                    monitor.OnCompleted();
                }
                catch (Exception ex)
                {
                    CacheClientMetricProvider.AddConnectionCreationErrorCounter(_dataLabCacheNode.CachePoolName, _dataLabCacheNode.CacheNodeName);
                    monitor.OnError(ex);
                    throw;
                }
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                try
                {
                    var connectionMultiplexer = _connectionMultiplexer;
                    if (connectionMultiplexer != null)
                    {
                        try
                        {
                            connectionMultiplexer.ConnectionFailed -= _dataLabCacheNode.ConnectionFailedHandler;
                            connectionMultiplexer.ConnectionRestored -= _dataLabCacheNode.ConnectionRestoredHandler;
                            connectionMultiplexer.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Failed to dispose old connection");
                        }
                    }

                    _connectionLock.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to dispose old connection");
                }

            }
        }

    }
}
