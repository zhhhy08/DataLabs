using Azure;
using Azure.Storage.Blobs.Models;
using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.BlobClient;
using System.Net;

namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common
{
    public class TestOutputBlobClient : IOutputBlobClient
    {
        public Dictionary<string, BlobResult> _cache = new Dictionary<string, BlobResult>();
        public int NumUploadCall;
        public int NumDownloadCall;
        public int ReturnETagConflictAfterNum;
        public int NumTokenCancelled;

        public Task<(BinaryData binaryData, ETag? etag)> DownloadContentAsync(string resourceId, string tenantId, bool exceptionOnNotFound, int retryFlowCount, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) {
                Interlocked.Increment(ref NumTokenCancelled);
                cancellationToken.ThrowIfCancellationRequested();
            }

            Interlocked.Increment(ref NumDownloadCall);

            var key = GetKey(resourceId, tenantId);
            if (_cache.TryGetValue(key, out BlobResult value))
            {
                return Task.FromResult<(BinaryData binaryData, ETag? etag)>((value.Content, value.Etag));
            }

            if (exceptionOnNotFound)
            {
                throw new RequestFailedException((int)HttpStatusCode.NotFound, "BlobNotFound", BlobErrorCode.BlobNotFound.ToString(), null);
            }
            else
            {
                return Task.FromResult<(BinaryData binaryData, ETag? etag)>((null, null));
            }
        }

        public Task<(ETag? etag, bool conflict)> UploadContentAsync(string resourceId, string tenantId, BinaryData binaryData, ETag? etag, long outputTimeStamp, bool useOutputTimeStampTagCondition, int retryFlowCount, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Interlocked.Increment(ref NumTokenCancelled);
                cancellationToken.ThrowIfCancellationRequested();
            }

            Interlocked.Increment(ref NumUploadCall);

            var newEtag = new ETag(Guid.NewGuid().ToString());
            var key = GetKey(resourceId, tenantId);
            var blobResult = new BlobResult();
            blobResult.Content = binaryData;
            blobResult.Etag = newEtag;
            _cache[key] = blobResult;

            if (ReturnETagConflictAfterNum > 0 && ReturnETagConflictAfterNum == NumUploadCall)
            {
                return Task.FromResult<(ETag? etag, bool conflict)>((newEtag, true));
            }

            return Task.FromResult<(ETag? etag, bool conflict)>((newEtag, false));
        }

        public void Clear()
        {
            _cache.Clear();
            NumUploadCall = 0;
            NumDownloadCall = 0;
            ReturnETagConflictAfterNum = 0;
            NumTokenCancelled = 0;
        }

        private static string GetKey(string resourceId, string tenantId)
        {
            return $"{tenantId}_{resourceId}";
        }

        public class BlobResult
        {
            public BinaryData Content;
            public ETag? Etag;
        }
    }
}
