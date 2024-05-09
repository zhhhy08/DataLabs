namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.BlobClient
{
    using global::Azure.Storage.Blobs;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;

    [ExcludeFromCodeCoverage]
    public class FixedBlobContainerProviderFactory : IBlobContainerProviderFactory
    {
        private static readonly List<string> ContainerNames;
        static FixedBlobContainerProviderFactory()
        {
            const int NUM_CONTAINERS = 200;
            ContainerNames = new List<string>(NUM_CONTAINERS);

            const string alphabets = "abcdefghijklmnopqrstuvwxyz";
            /*
             * Container Name Restriction from Azure Blob
             * 
             * 1. All letters in a container name must be lowercase.
             * 2. Container names must be from 3 through 63 characters long.
             */
            for (int i = 0; i < 10; i++) 
            {
                for (int j = 0; j < 100; j++)
                {
                    char padding = alphabets[j % alphabets.Length];
                    var containerName = padding + j.ToString("D2") + i;
                    ContainerNames.Add(containerName);
                    if (ContainerNames.Count == NUM_CONTAINERS)
                    {
                        return;
                    }
                }
            }
        }

        public async Task<IBlobContainerProvider> CreateBlobContainerProviderAsync(BlobServiceClient blobServiceClient, CancellationToken cancellationToken)
        {
            var blobContainerProvider = new FixedBlobContainerProvider(blobServiceClient);
            await blobContainerProvider.CheckInitConnections(cancellationToken).ConfigureAwait(false);
            return blobContainerProvider;
        }

        private class FixedBlobContainerProvider : IBlobContainerProvider
        {
            private static readonly ActivityMonitorFactory FixedBlobContainerProviderCreateContainerIfNotExistsAsync = 
                new("FixedBlobContainerProvider.CreateContainerIfNotExistsAsync");

            private static readonly SemaphoreSlim _containerCreationSemaphoreSlim;

            private readonly BlobServiceClient _blobServiceClient;
            private readonly BlobContainerClient[] _blobContainerClients;

            static FixedBlobContainerProvider()
            {
                var maxConcurrentBlobContainerCreation = ConfigMapUtil.Configuration.GetValue<int>(SolutionConstants.MaxConcurrentBlobContainerCreation, 100);
                _containerCreationSemaphoreSlim = new SemaphoreSlim(maxConcurrentBlobContainerCreation, maxConcurrentBlobContainerCreation);
            }

            public FixedBlobContainerProvider(BlobServiceClient blobServiceClient)
            {
                _blobServiceClient = blobServiceClient;
                _blobContainerClients = new BlobContainerClient[ContainerNames.Count];
            }

            public async Task CheckInitConnections(CancellationToken cancellationToken)
            {
                // Let's create two random containers to validate during initialization
                // For other containers, Let's do create in a lazy way

                var index1 = ThreadSafeRandom.Next(ContainerNames.Count);
                var index2 = ThreadSafeRandom.Next(ContainerNames.Count);

                // During initialization, let's try to create two containers with cancellation Token. 
                // If it has issue, it will throw exception and initialization will fail
                await CreateContainerIfNotExistsAsync(index1, cancellationToken).ConfigureAwait(false);
                await CreateContainerIfNotExistsAsync(index2, cancellationToken).ConfigureAwait(false);
            }

            public async ValueTask<BlobContainerClient> GetBlobContainerClientAsync(string resourceId, string? tenantId, string blobName, uint hash1, ulong hash2, ulong hash3, CancellationToken cancellationToken)
            {
                // Use hash1 to select Container
                var idx = BlobUtils.GetBlobContainerIndex(hash1, _blobContainerClients.Length);
                var blobContainerClient = _blobContainerClients[idx];
                if (blobContainerClient != null)
                {
                    return blobContainerClient;
                }

                return await CreateContainerIfNotExistsAsync(idx, cancellationToken).ConfigureAwait(false);
            }

            private async Task<BlobContainerClient> CreateContainerIfNotExistsAsync(int idx, CancellationToken cancellationToken)
            {
                if (_blobContainerClients[idx] != null)
                {
                    return _blobContainerClients[idx];
                }

                var hasSemaphore = false;

                using var monitor = FixedBlobContainerProviderCreateContainerIfNotExistsAsync.ToMonitor();

                try
                {
                    monitor.OnStart(true);

                    await _containerCreationSemaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
                    hasSemaphore = true;

                    // Let's one more check if BlobContainerClient is already created by other thread
                    if (_blobContainerClients[idx] != null)
                    {
                        return _blobContainerClients[idx];
                    }

                    var containerName = ContainerNames[idx];
                    var blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                    await blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                    Interlocked.CompareExchange(ref _blobContainerClients[idx], blobContainerClient, null);

                    monitor.OnCompleted();
                    return _blobContainerClients[idx];
                }
                catch (Exception ex)
                {
                    monitor.OnError(ex);
                    throw;
                }
                finally
                {
                    if (hasSemaphore)
                    {
                        _containerCreationSemaphoreSlim.Release();
                    }
                }
            }
        }
    }
}
