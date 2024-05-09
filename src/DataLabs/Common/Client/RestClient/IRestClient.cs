namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IRestClient : IDisposable
    {
        /// <summary>
        /// Call an API and gets a response.
        /// </summary>
        /// <typeparam name="TRequest">
        /// Type of the request if there is a request content provided.
        /// </typeparam>
        /// <param name="endPointSelector">Endpoint</param>
        /// <param name="requestUri">Request Path</param>
        /// <param name="httpMethod">Http method.</param>
        /// <param name="accessToken">AAD access token.</param>
        /// <param name="headers">Headers dictionary.</param>
        /// <param name="jsonRequestContent">Request content.</param>
        /// <param name="clientRequestId">Client Request Id (usually Guid)</param>
        /// <param name="skipUriPathLogging">Whether to log full URI in logging</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Http response message.</returns>
        public Task<HttpResponseMessage> CallRestApiAsync<TRequest>(
            IEndPointSelector endPointSelector,
            string requestUri,
            HttpMethod httpMethod,
            string? accessToken,
            IEnumerable<KeyValuePair<string, string>>? headers,
            TRequest? jsonRequestContent,
            string? clientRequestId,
            bool skipUriPathLogging,
            CancellationToken cancellationToken) where TRequest : class;
    }
}