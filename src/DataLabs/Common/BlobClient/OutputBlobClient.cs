namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.BlobClient
{
    using global::Azure;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    [ExcludeFromCodeCoverage]
    public class OutputBlobClient : IOutputBlobClient
    {
        private readonly IBlobNameProvider _blobNameProvider;
        private readonly IStorageAccountSelector _storageAccountSelector;

        private static readonly ActivityMonitorFactory OutputBlobClientDownloadContentAsync =
            new ActivityMonitorFactory("OutputBlobClient.DownloadContentAsync");

        private static readonly ActivityMonitorFactory OutputBlobClientUploadContentAsync =
            new ActivityMonitorFactory("OutputBlobClient.UploadContentAsync");

        private const string OutputBlobUploadDefaultTimeOutString = "5/15";
        private const string OutputBlobDownloadDefaultTimeOutString = "5/15";

        private readonly TimeOutConfigInfo _uploadTimeOutConfigInfo;
        private readonly TimeOutConfigInfo _downloadTimeOutConfigInfo;

        public OutputBlobClient(IStorageAccountSelector storageAccountSelector, IBlobNameProvider blobNameProvider, IConfiguration configuration)
        {
            _storageAccountSelector = storageAccountSelector;
            _blobNameProvider = blobNameProvider;

            // For paired region support, Two OutputBlobClient instances will be created. We need to allow multi callbacks
            _uploadTimeOutConfigInfo = new TimeOutConfigInfo(SolutionConstants.OutputBlobUploadMaxTimeOutInSec, OutputBlobUploadDefaultTimeOutString, configuration, allowMultiCallBacks: true);
            _downloadTimeOutConfigInfo = new TimeOutConfigInfo(SolutionConstants.OutputBlobDownloadMaxTimeOutInSec, OutputBlobDownloadDefaultTimeOutString, configuration, allowMultiCallBacks: true);
        }

        public async Task<(BinaryData? binaryData, ETag? etag)> DownloadContentAsync(string resourceId, string? tenantId, bool exceptionOnNotFound, int retryFlowCount, CancellationToken cancellationToken)
        {
            using var monitor = OutputBlobClientDownloadContentAsync.ToMonitor();
            var taskActivity = OpenTelemetryActivityWrapper.Current;

            try
            {
                monitor.OnStart(false);

                monitor.Activity[SolutionConstants.OutputResourceId] = resourceId;
                monitor.Activity[SolutionConstants.OutputTenantId] = tenantId;

                var timeOut = _downloadTimeOutConfigInfo.GetTimeOut(retryFlowCount);
                monitor.Activity[SolutionConstants.TimeOutValue] = timeOut;

                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tokenSource.CancelAfter(timeOut);
                cancellationToken = tokenSource.Token;

                var hash = _blobNameProvider.CalculateHash(resourceId, tenantId);
                var blobName = _blobNameProvider.GetBlobName(resourceId, tenantId, hash.Item1, hash.Item2, hash.Item3);
                var blobContainerProvider = _storageAccountSelector.GetBlobContainerProvider(resourceId, tenantId, hash.Item1, hash.Item2, hash.Item3);
                var blobContainerClient = await blobContainerProvider.GetBlobContainerClientAsync(resourceId, tenantId, blobName, hash.Item1, hash.Item2, hash.Item3, cancellationToken).ConfigureAwait(false);

                monitor.Activity[SolutionConstants.OutputContainerURI] = blobContainerClient.Uri;
                monitor.Activity[SolutionConstants.OutputBlobName] = blobName;

                if (taskActivity != null)
                {
                    taskActivity.SetTag(SolutionConstants.OutputContainerURI, blobContainerClient.Uri);
                    taskActivity.SetTag(SolutionConstants.OutputBlobName, blobName);
                    taskActivity.SetTag(SolutionConstants.OutputTenantId, tenantId);
                }

                var result = await BlobUtils.DownloadContentAsync(blobContainerClient, blobName, exceptionOnNotFound, cancellationToken).ConfigureAwait(false);

                monitor.Activity[SolutionConstants.ETag] = result.etag;

                if (result.binaryData == null && !exceptionOnNotFound)
                {
                    taskActivity?.SetTag(SolutionConstants.OutputBlobNotFound, true);
                }

                taskActivity?.SetTag(SolutionConstants.OutputBlobDownloadDuration, monitor.Activity.Elapsed.TotalMilliseconds);
                monitor.OnCompleted();
                return result;
            }
            catch (Exception ex)
            {
                taskActivity?.SetTag(SolutionConstants.OutputBlobDownloadDuration, monitor.Activity.Elapsed.TotalMilliseconds);
                monitor.OnError(ex);
                throw;
            }
        }

        public async Task<(ETag? etag, bool conflict)> UploadContentAsync(
            string resourceId, 
            string? tenantId, 
            BinaryData binaryData, 
            ETag? etag, 
            long outputTimeStamp,
            bool useOutputTimeStampTagCondition,
            int retryFlowCount,
            CancellationToken cancellationToken) 
        {
            using var monitor = OutputBlobClientUploadContentAsync.ToMonitor();
            var taskActivity = OpenTelemetryActivityWrapper.Current;

            try
            {
                monitor.OnStart(false);

                monitor.Activity[SolutionConstants.OutputResourceId] = resourceId;
                monitor.Activity[SolutionConstants.OutputTenantId] = tenantId;

                var timeOut = _uploadTimeOutConfigInfo.GetTimeOut(retryFlowCount);
                monitor.Activity[SolutionConstants.TimeOutValue] = timeOut;

                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tokenSource.CancelAfter(timeOut);
                cancellationToken = tokenSource.Token;

                var hash = _blobNameProvider.CalculateHash(resourceId, tenantId);
                var blobName = _blobNameProvider.GetBlobName(resourceId, tenantId, hash.Item1, hash.Item2, hash.Item3);
                var blobContainerProvider = _storageAccountSelector.GetBlobContainerProvider(resourceId, tenantId, hash.Item1, hash.Item2, hash.Item3);
                var blobContainerClient = await blobContainerProvider.GetBlobContainerClientAsync(resourceId, tenantId, blobName, hash.Item1, hash.Item2, hash.Item3, cancellationToken).ConfigureAwait(false);

                monitor.Activity[SolutionConstants.OutputContainerURI] = blobContainerClient.Uri;
                monitor.Activity[SolutionConstants.OutputBlobName] = blobName;
                monitor.Activity[SolutionConstants.OutputTimeStamp] = outputTimeStamp;
                monitor.Activity[SolutionConstants.ETag] = etag;

                if (taskActivity != null)
                {
                    taskActivity.SetTag(SolutionConstants.OutputContainerURI, blobContainerClient.Uri);
                    taskActivity.SetTag(SolutionConstants.OutputBlobName, blobName);
                    taskActivity.SetTag(SolutionConstants.OutputTenantId, tenantId);
                }

                var result = await BlobUtils.UploadContentAsync(blobContainerClient, blobName, binaryData, etag, outputTimeStamp, useOutputTimeStampTagCondition, cancellationToken).ConfigureAwait(false);

                if (result.conflict)
                {
                    monitor.Activity["ETagConflict"] = true;
                }
                monitor.Activity["NewETag"] = result.etag;

                taskActivity?.SetTag(SolutionConstants.OutputBlobUploadDuration, monitor.Activity.Elapsed.TotalMilliseconds);
                monitor.OnCompleted();
                return result;
            }
            catch (Exception ex)
            {
                taskActivity?.SetTag(SolutionConstants.OutputBlobUploadDuration, monitor.Activity.Elapsed.TotalMilliseconds);
                monitor.OnError(ex);
                throw;
            }
        }
    }
}
