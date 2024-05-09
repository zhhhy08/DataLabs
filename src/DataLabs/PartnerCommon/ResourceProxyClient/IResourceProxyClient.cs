namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;

    public interface IResourceProxyClient
    {
        /// <summary>
        /// Method to get resource from CAS using Resource Fetcher Proxy.
        /// Makes call to Resource Fetcher to retrieve resource needed given details in request body.
        /// </summary>
        /// <param name="request">Cas Request</param>
        /// <param name="cancellationToken">Cancellation Token</param>
        /// <param name="scenario">Scenario name, optional</param>
        /// <param name="component">Component name, optional</param>
        /// <returns></returns>
        Task<DataLabsCasResponse> GetCasResponseAsync(
            DataLabsCasRequest request,
            CancellationToken cancellationToken,
            bool skipCacheRead = false,
            bool skipCacheWrite = false,
            string? scenario = null,
            string? component = null);

        /// <summary>
        /// Method to send ARM Request using resource fetcher proxy.
        /// Makes call to Resource Fetcher to retrieve resource needed given details in request body.
        /// </summary>
        /// <param name="request">ARM request</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="scenario">Scenario name., optional.</param>
        /// <param name="component">Component name, optional.</param>
        /// <returns></returns>
        Task<DataLabsARMGenericResponse> CallARMGenericRequestAsync(
            DataLabsARMGenericRequest request,
            CancellationToken cancellationToken,
            bool skipCacheRead = false,
            bool skipCacheWrite = false,
            string? scenario = null,
            string? component = null);

        /// <summary>
        /// Method to get a resource using resource fetcher proxy.
        /// Resource fetcher proxy service has the logic to identify the resource type (Output/ Non-output)
        /// and subsequently the logic to fetch the resource from eith Cache/ Blob combination or from ARM.
        /// </summary>
        /// <param name="request">Resource request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="getDeletedResource">Flag which decides whether partner solution wants to get a deleted resource or a NotFound message.</param>
        /// <param name="scenario">Scenario name., optional.</param>
        /// <param name="component">Component name, optional.</param>
        /// <returns></returns>
        Task<DataLabsResourceResponse> GetResourceAsync(
            DataLabsResourceRequest request,
            CancellationToken cancellationToken,
            bool getDeletedResource,
            bool skipCacheRead = false,
            bool skipCacheWrite = false,
            string? scenario = null,
            string? component = null);

        /// <summary>
        /// Method to get resource using Resource Fetcher Proxy.
        /// Makes call to ARM Admin endpoint to get manifest config based on manifest provider
        /// </summary>
        /// <param name="request">Manifest request</param>
        /// <param name="cancellationToken">cancellation token</param>
        /// <param name="scenario">Scenario name, optional</param>
        /// <param name="component">Component name, optional</param>
        /// <returns></returns>
        Task<DataLabsARMAdminResponse> GetManifestConfigAsync(
            DataLabsManifestConfigRequest request,
            CancellationToken cancellationToken,
            bool skipCacheRead = false,
            bool skipCacheWrite = false,
            string? scenario = null,
            string? component = null);


        /// <summary>
        /// Method to get resource using Resource Fetcher Proxy.
        /// Makes call to ARM Admin endpoint to get Config Specs based on apiExtension passed in.
        /// Built to support variations to append relative path such as:
        /// /clouds/public
        /// /clouds/public/regions
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="scenario"></param>
        /// <param name="component"></param>
        /// <returns></returns>
        Task<DataLabsARMAdminResponse> GetConfigSpecsAsync(
           DataLabsConfigSpecsRequest request,
           CancellationToken cancellationToken,
           bool skipCacheRead = false,
           bool skipCacheWrite = false,
           string? scenario = null,
           string? component = null);

        /// <summary>
        /// Method to get pacific collection resource
        /// Makes call to Resource Fetcher to hit pacific to get collection resources
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="skipCacheRead"></param>
        /// <param name="skipCacheWrite"></param>
        /// <param name="scenario"></param>
        /// <param name="component"></param>
        /// <returns></returns>
        Task<DataLabsResourceCollectionResponse> GetCollectionAsync(
            DataLabsResourceRequest request,
            CancellationToken cancellationToken,
            bool skipCacheRead = false,
            bool skipCacheWrite = false,
            string? scenario = null,
            string? component = null);

        /// <summary>
        /// Method to get resource using Resource Fetcher Proxy.
        /// Makes call to IdMapping endpoint to get id mappings based on resource ids passed in.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="scenario"></param>
        /// <param name="component"></param>
        /// <returns></returns>
        Task<DataLabsIdMappingResponse> GetIdMappingsAsync(
            DataLabsIdMappingRequest request,
            CancellationToken cancellationToken,
            bool skipCacheRead = false,
            bool skipCacheWrite = false,
            string? scenario = null,
            string? component = null);
    }
}