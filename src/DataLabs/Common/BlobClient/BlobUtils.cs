namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.BlobClient
{
    using global::Azure;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Blobs.Models;
    using System;
    using System.IO;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Diagnostics.CodeAnalysis;
    using System.Collections.Generic;
    using Microsoft.Extensions.Logging;
    using System.Linq;

    [ExcludeFromCodeCoverage]
    public static class BlobUtils
    {
        public const string BlobTag_OutputTimeStamp = "outputtime";

        // TODO some testing
        /*
        private static StorageTransferOptions _defaultStorageTransferOptions = new StorageTransferOptions
        {
            // TODO make hotconfigurable below parameters

            // Set the maximum number of parallel transfer workers
            MaximumConcurrency = 2,
            // Set the initial transfer length to 4 MiB
            InitialTransferSize = 4 * 1024 * 1024,
            // Set the maximum length of a transfer to 2 MiB
            MaximumTransferSize = 2 * 1024 * 1024
        };
        */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetStorageAccountIndex(uint hash1, int numStorages)
        {
            return (int)(hash1 % (uint)numStorages);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetBlobContainerIndex(uint hash1, int numBlobContainers)
        {
            return (int)(hash1 % (uint)numBlobContainers);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] GetBytesForHash(string resourceId, string? tenantId)
        {
            var key = string.IsNullOrEmpty(tenantId) ? resourceId : tenantId + resourceId;
            return Encoding.UTF8.GetBytes(key.ToLowerInvariant());
        }

        /* 
         * When ETag is not null, we will use Etag for precondition
         * Otherwise, we will use outputTimeStamp only if newer content is uploaded
         */
        public static async Task<(ETag? etag, bool conflict)> UploadContentAsync(
            BlobContainerClient container, 
            string blobName, 
            BinaryData binaryData,
            ETag? etag,
            long outputTimestamp,
            bool useOutputTimeStampTagCondition,
            CancellationToken cancellationToken)
        {
            // TODO
            // Use compressed data. Cache is already using compressed Data. Reuse the compressed data for blob as well. 
            // Check if Jing could support the compressed EventHub data
            try
            {
                BlobUploadOptions blobUploadOptions = new BlobUploadOptions();

                // TODO, need some experiment with defaultTransferOption
                //blobUploadOptions.TransferOptions = _defaultStorageTransferOptions;

                if (etag != null)
                {
                    blobUploadOptions.Conditions = new BlobRequestConditions()
                    {
                        IfMatch = etag
                    };
                }
                else if (useOutputTimeStampTagCondition && outputTimestamp > 0)
                {
                    string outputTimeStr = outputTimestamp.ToString("x16");
                    // Add OutputTimeStamp as Tag
                    var tags = new Dictionary<string, string>
                    {
                        { BlobTag_OutputTimeStamp, outputTimeStr }
                    };
                    blobUploadOptions.Tags = tags;

                    blobUploadOptions.Conditions = new BlobRequestConditions()
                    {
                        TagConditions = $"{BlobTag_OutputTimeStamp} < '{outputTimeStr}'"
                    };
                }

                var blobClient = container.GetBlobClient(blobName);
                var blobContentInfo = await blobClient.UploadAsync(binaryData, blobUploadOptions, cancellationToken).ConfigureAwait(false);
                return (blobContentInfo.Value.ETag, false);
            }
            catch (Exception ex)
            {
                if (ex.IsAzureBlobPreConditionFailed())
                {
                    return (null, true);
                }
                throw;
            }
        }

        public static async Task<(BinaryData? binaryData, ETag? etag)> DownloadContentAsync(BlobContainerClient container, string blobName, bool exceptionOnNotFound, CancellationToken cancellationToken)
        {
            try
            {
                var blobClient = container.GetBlobClient(blobName);
                using (var stream = new MemoryStream())
                {
                    var response = await blobClient.DownloadToAsync(stream,
                            conditions: default,
                            transferOptions: default,
                            cancellationToken).ConfigureAwait(false);

                    var etag = response.Headers.ETag;
                    var content = new BinaryData(stream.GetBuffer().AsMemory(0, (int) stream.Position));
                    return (content, etag);
                }
            }
            catch (Exception ex)
            {
                if (!exceptionOnNotFound && ex.IsAzureBlobNotFound())
                {
                    return (null, null);
                }
                throw;
            }
        }

        public static async Task<bool> DeleteBlobsWithPrefixAsync(BlobContainerClient container, string blobPrefix, bool exceptionOnDeleteFailed, CancellationToken cancellationToken, ILogger? logger = null)
        {
            try
            {
                var blobs = container.GetBlobsAsync(prefix: blobPrefix, cancellationToken: cancellationToken).AsPages(default);
                // Enumerate the blobs returned for each page.
                await foreach (Page<BlobItem> blobPage in blobs)
                {
                    foreach (BlobItem blobItem in blobPage.Values)
                    {
                        await container.DeleteBlobIfExistsAsync(blobItem.Name, cancellationToken: cancellationToken);
                    }
                    if(logger != null)
                    {
                        var blobNamesString = blobPage.Values.Select((x)=> x.Name);
                        logger.LogInformation("Deleted " + string.Join(",", blobNamesString));
                    }
                }
                return true;
            }
            catch (Exception)
            {
                if (!exceptionOnDeleteFailed)
                {
                    return false;
                }
                throw;
            }
        }


        /// <summary>
        /// Determines whether [is azure blob not found] [the specified ex].
        /// </summary>
        public static bool IsAzureBlobNotFound(this Exception ex)
        {
            return HasInnerExceptionWithHttpAndErrorCodes(ex,
                HttpStatusCode.NotFound, BlobErrorCode.BlobNotFound.ToString());
        }

        /// <summary>
        /// Determines whether [is azure blob container Etag condition not matched] [the specified ex].
        /// </summary>
        public static bool IsAzureBlobPreConditionFailed(this Exception ex, BlobErrorCode? blobErrorCode = null)
        {
            return HasInnerExceptionWithHttpAndErrorCodes(ex,
                HttpStatusCode.PreconditionFailed, blobErrorCode?.ToString());
        }

        /// <summary>
        /// Determines whether [is azure blob container not found] [the specified ex].
        /// </summary>
        public static bool IsAzureBlobContainerNotFound(this Exception ex)
        {
            return HasInnerExceptionWithHttpAndErrorCodes(ex,
                HttpStatusCode.NotFound, BlobErrorCode.ContainerNotFound.ToString());
        }

        public static bool IsAzureBlobFileAlreadyExist(this Exception ex)
        {
            RequestFailedException? requestFailedException =
                ex as RequestFailedException ?? ex.InnerException as RequestFailedException;
            return requestFailedException != null && String.Equals(BlobErrorCode.BlobAlreadyExists.ToString(), requestFailedException.ErrorCode, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasInnerExceptionWithHttpAndErrorCodes(
           this Exception ex, HttpStatusCode httpStatus, string? extendedErrorCode)
        {
            var unifiedException = ex as RequestFailedException ?? ex?.InnerException as RequestFailedException;
            if (unifiedException == null)
            {
                return false;
            }

            return unifiedException.Status == (int)httpStatus &&
                (extendedErrorCode == null || string.Equals(unifiedException.ErrorCode, extendedErrorCode, StringComparison.OrdinalIgnoreCase));
        }
        

    }
}
