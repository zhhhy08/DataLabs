namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerBlobClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common.Core.Constants;
    using System.Net;
    using System.Runtime.Serialization;
    using System.Threading;

    public class TestPartnerBlobClient : IPartnerBlobClient
    {
        public bool ReturnSerialziationError;
        public bool ThrowNetworkException;
        public HttpStatusCode StatusCode = HttpStatusCode.OK;

        public Task<List<TResponse>> GetResourcesAsync<TResponse>(string uri, int retryFlowCount, CancellationToken cancellationToken) where TResponse : class
        {
            if (ThrowNetworkException)
            {
                throw new HttpRequestException(null, null, StatusCode);
            }

            if (ReturnSerialziationError)
            {
                using var stream = ResourcesConstants.InvalidMultiResourcesBinaryData.ToStream();
                try
                {
                    var result = SerializationHelper.ReadListFromStream<TResponse>(stream);
                    return Task.FromResult(result);
                }
                catch (Exception ex)
                {
                    throw new SerializationException(ex.Message, ex);
                }
            }
            else
            {
                using var stream = ResourcesConstants.MultiResourcesBinaryData.ToStream();
                try
                {
                    var result = SerializationHelper.ReadListFromStream<TResponse>(stream);
                    return Task.FromResult(result);
                }
                catch (Exception ex)
                {
                    throw new SerializationException(ex.Message, ex);
                }
            }
        }

        public void Clear()
        {
            ReturnSerialziationError = false;
            ThrowNetworkException = false;
            StatusCode = HttpStatusCode.OK;
        }
    }
}