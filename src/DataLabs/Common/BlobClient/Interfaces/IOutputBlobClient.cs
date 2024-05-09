namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.BlobClient
{
    using global::Azure;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IOutputBlobClient
    {
        public Task<(ETag? etag, bool conflict)> UploadContentAsync(string resourceId, string? tenantId, BinaryData binaryData, ETag? etag,
            long outputTimeStamp, bool useOutputTimeStampTagCondition, int retryFlowCount, CancellationToken cancellationToken);
        public Task<(BinaryData? binaryData, ETag? etag)> DownloadContentAsync(string resourceId, string? tenantId, bool exceptionOnNotFound, int retryFlowCount,
            CancellationToken cancellationToken);
    }
}
