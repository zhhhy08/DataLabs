namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.BlobClient
{
    using global::Azure.Identity;
    using global::Azure.Storage.Blobs;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using System.Threading;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    [ExcludeFromCodeCoverage]
    public class HashBasedStorageAccountSelector : IStorageAccountSelector
    {
        private readonly List<string> _storageAccountNames;
        private readonly List<IBlobContainerProvider> _blobContainerProviders;

        public HashBasedStorageAccountSelector(IBlobContainerProviderFactory blobContainerProviderFactory, string storageAccountNames, CancellationToken cancellationToken)
        {
            GuardHelper.ArgumentNotNull(storageAccountNames);

            _storageAccountNames = storageAccountNames.ConvertToList();

            _blobContainerProviders = new List<IBlobContainerProvider>(_storageAccountNames.Count);

            var blobStorageLogsEnabled = ConfigMapUtil.Configuration.GetValue(SolutionConstants.BlobStorageLogsEnabled, false);
            var blobStorageTraceEnabled = ConfigMapUtil.Configuration.GetValue(SolutionConstants.BlobStorageTraceEnabled, false);

            var blobClientOptions = new BlobClientOptions();
            blobClientOptions.Diagnostics.IsLoggingEnabled = blobStorageLogsEnabled;
            blobClientOptions.Diagnostics.IsDistributedTracingEnabled = blobStorageTraceEnabled;

            int parallelism = 5;
            List<Task<IBlobContainerProvider>> tasks = new(parallelism);
            for (int i = 0; i < _storageAccountNames.Count; i++)
            {
                var stroageAccountName = _storageAccountNames[i];

                tasks.Add(CreateBlobContainerProvider(
                    blobContainerProviderFactory,
                    blobClientOptions,
                    stroageAccountName, 
                    cancellationToken));

                if (i % parallelism == 0)
                {
                    Task.WhenAll(tasks).Wait(cancellationToken);

                    foreach (var task in tasks)
                    {
                        _blobContainerProviders.Add(task.Result);
                    }
                    tasks.Clear();
                }
            }

            if (tasks.Count > 0)
            {
                Task.WhenAll(tasks).Wait(cancellationToken);
                foreach (var task in tasks)
                {
                    _blobContainerProviders.Add(task.Result);
                }
                tasks.Clear();
            }

            GuardHelper.IsArgumentEqual(_blobContainerProviders.Count, _storageAccountNames.Count);
            for (int i = 0; i < _blobContainerProviders.Count; i++)
            {
                GuardHelper.ArgumentNotNull(_blobContainerProviders[i]);
            }
        }

        private Task<IBlobContainerProvider> CreateBlobContainerProvider(
            IBlobContainerProviderFactory blobContainerProviderFactory,
            BlobClientOptions blobClientOptions,
            string storageAccountName, 
            CancellationToken cancellationToken)
        {
            var blobServiceClient = new BlobServiceClient(
                     new Uri(string.Format("https://{0}.blob.core.windows.net", storageAccountName)),
                     new DefaultAzureCredential(),
                     blobClientOptions);

            return blobContainerProviderFactory.CreateBlobContainerProviderAsync(blobServiceClient, cancellationToken);
        }

        public IBlobContainerProvider GetBlobContainerProvider(string resourceId, string? tenantId, uint hash1, ulong hash2, ulong hash3)
        {
            // TODO
            // consider Consistent Hashing using virtual nodes for better balancing so that we can dynamically add more storage accounts

            var idx = BlobUtils.GetStorageAccountIndex(hash1, _storageAccountNames.Count);
            return _blobContainerProviders[idx];
        }
    }
}
