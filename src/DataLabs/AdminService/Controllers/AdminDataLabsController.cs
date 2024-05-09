namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Controllers
{
    using k8s.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Acis;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
    using Swashbuckle.AspNetCore.Annotations;
    using System.Net.Http;
    using System.ServiceModel;
    using System.Text;

    [ApiController]
    [Route("admin/datalabsoperations/[action]")]
    public class AdminDataLabsController : ControllerBase
    {
        #region Tracing

        private static readonly ILogger<AdminDataLabsController> Logger = DataLabLoggerFactory.CreateLogger<AdminDataLabsController>();
        private static readonly ActivityMonitorFactory AdminDataLabsControllerGetConfiguration =
            new("AdminDataLabsController.GetConfiguration");
        private static readonly ActivityMonitorFactory AdminDataLabsControllerDeleteAndRecreateServiceBusQueue =
            new("AdminDataLabsController.DeleteAndRecreateServiceBusQueue");
        private static readonly ActivityMonitorFactory AdminDataLabsControllerDeleteDeadLetterMessages =
            new("AdminDataLabsController.DeleteDeadLetterMessages");
        private static readonly ActivityMonitorFactory AdminDataLabsControllerReplayDeadLetterMessages =
            new("AdminDataLabsController.ReplayDeadLetterMessages");

        #endregion

        #region Fields

        public enum DLService
        {
            IOService,
            ResourceProxy,
            ResourceFetcherService
        }

        private IConfiguration _configuration;
        private HttpClient _httpClient;
        private IKubernetesProvider _kubernetesProvider;
        private Dictionary<DLService, string> _serviceEndpoints;
        private const string SuccessResponseIndicator = "has started";
        private const string SolutionNamespace = "solution-namespace";
        private const string SolutionNamespaceNamePrefix = "solution-io";

        #endregion

        public AdminDataLabsController(IConfigurationWithCallBack configuration, HttpClient httpClient, IKubernetesProvider kubernetesProvider)
        {
            _httpClient = httpClient;
            _kubernetesProvider = kubernetesProvider;
            _configuration = configuration;

            _serviceEndpoints = new Dictionary<DLService, string>();
            InitializeEndpoint(DLService.IOService, AdminConstants.IOServiceAdminEndpoint);
            InitializeEndpoint(DLService.ResourceProxy, AdminConstants.ResourceProxyAdminEndpoint);
            InitializeEndpoint(DLService.ResourceFetcherService, AdminConstants.ResourceFetcherAdminEndpoint);
        }

        #region Endpoint Initializers and Getters (private methods)

        private void InitializeEndpoint(DLService service, string endpointKey)
        {
            var endpointValue = _configuration.GetValue<string>(endpointKey, "") ?? "";

            if (!string.IsNullOrEmpty(endpointValue))
            {
                _serviceEndpoints.Add(service, endpointValue);
            }
        }

        private string GetServiceEndpoint(DLService service)
        {
            if (_serviceEndpoints.TryGetValue(service, out var endpointUri))
            {
                return endpointUri;
            }
            throw new EndpointNotFoundException("No endpoint known");
        }

        #endregion

        [HttpGet]
        [ActionName(AdminConstants.GetConfiguration)]
        [SwaggerOperation(Tags = new[] { AcisOperationGroups.DataLabsOperationGroup, AcisClaims.ReadOperationClaim })]
        [AcisDataClassification(AcisDataClassificationLevel.NoCustomerContent)]
        public async Task<ActionResult<string>> GetConfiguration(
            [FromQuery(Name = SolutionConstants.SERVICE)] string service,
            [FromQuery(Name = AdminConstants.ConfigKey)] string configKey)
        {
            using var monitor = AdminDataLabsControllerGetConfiguration.ToMonitor();
            try
            {
                monitor.Activity[SolutionConstants.SERVICE] = service;
                monitor.Activity[AdminConstants.ConfigKey] = configKey;
                monitor.OnStart();

                if (!Enum.TryParse<DLService>(service, out var dlService))
                {
                    monitor.Activity["ServiceDoesNotExist"] = true;
                    var ex = new EndpointNotFoundException("ServiceDoesNotExist: Please check your parameters (e.g. IOService)");
                    monitor.OnError(ex);
                    return new ObjectResult(ex);
                }

                var requestUri = $"admin/common/{AdminConstants.GetConfiguration}";
                var endpoint = GetServiceEndpoint(dlService);
                var query = $"?{AdminConstants.ConfigKey}={configKey}";

                var uriBuilder = new UriBuilder(endpoint);
                uriBuilder.Path = requestUri;
                uriBuilder.Query = query;

                var response = await _httpClient.GetAsync(uriBuilder.ToString()).ConfigureAwait(false);
                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                monitor.Activity["responseContent"] = responseContent;

                monitor.OnCompleted();
                return Ok(responseContent);
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                return new ObjectResult(ex);
            }
        }

        [HttpPatch]
        [ActionName(AdminConstants.DeleteAndRecreateServiceBusQueue)]
        [SwaggerOperation(Tags = new[] { AcisOperationGroups.DataLabsOperationGroup, AcisClaims.WriteOperationClaim })]
        [AcisDataClassification(AcisDataClassificationLevel.NoCustomerContent)]
        public async Task<ActionResult<string>> DeleteAndRecreateServiceBusQueue(
            [FromQuery(Name = AdminConstants.QueueName)] string queueName)
        {
            using var monitor = AdminDataLabsControllerDeleteAndRecreateServiceBusQueue.ToMonitor();
            try
            {
                monitor.OnStart();

                var query = $"?{AdminConstants.QueueName}={queueName}";
                var responseContent = await GetResponseFromIOServiceAdminController(AdminConstants.DeleteAndRecreateServiceBusQueue, query).ConfigureAwait(false);

                monitor.Activity["Response content"] = responseContent;

                monitor.OnCompleted();
                return Ok(responseContent);
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                return new ObjectResult(ex);
            }
        }

        [HttpPatch]
        [ActionName(AdminConstants.DeleteDeadLetterMessages)]
        [SwaggerOperation(Tags = new[] { AcisOperationGroups.DataLabsOperationGroup, AcisClaims.WriteOperationClaim })]
        [AcisDataClassification(AcisDataClassificationLevel.NoCustomerContent)]
        public async Task<ActionResult<string>> DeleteDeadLetterMessages(
            [FromQuery(Name = AdminConstants.QueueName)] string queueName,
            [FromQuery(Name = AdminConstants.DeleteLookBackHours)] int deleteLookBackHours,
            [FromQuery(Name = AdminConstants.NumberOfNodesInParallel)] int numberOfNodesInParallel)
        {
            using var monitor = AdminDataLabsControllerDeleteDeadLetterMessages.ToMonitor();
            try
            {
                monitor.OnStart();

                var solutionIoEndpointSlice = await GetSolutionIOEndpointSlice().ConfigureAwait(false);
                var query = $"?{AdminConstants.QueueName}={queueName}&{AdminConstants.DeleteLookBackHours}={deleteLookBackHours}";
                var (aggregatedResponseContent, responseContentToDisplay) = await GetAggregatedResponseFromMultipleIOControllerCalls(solutionIoEndpointSlice, AdminConstants.DeleteDeadLetterMessages, query, numberOfNodesInParallel).ConfigureAwait(false);

                monitor.Activity["Aggregated response content"] = aggregatedResponseContent;

                monitor.OnCompleted();
                return Ok(responseContentToDisplay);
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                return new ObjectResult(ex);
            }
        }

        [HttpPatch]
        [ActionName(AdminConstants.ReplayDeadLetterMessages)]
        [SwaggerOperation(Tags = new[] { AcisOperationGroups.DataLabsOperationGroup, AcisClaims.WriteOperationClaim })]
        [AcisDataClassification(AcisDataClassificationLevel.NoCustomerContent)]
        public async Task<ActionResult<string>> ReplayDeadLetterMessages(
            [FromQuery(Name = AdminConstants.QueueName)] string queueName,
            [FromQuery(Name = AdminConstants.ReplayLookBackHours)] int replayLookBackHours,
            [FromQuery(Name = AdminConstants.NumberOfNodesInParallel)] int numberOfNodesInParallel,
            [FromQuery(Name = AdminConstants.NeedDelete)] bool needDelete=false,
            [FromQuery(Name = AdminConstants.DeleteLookBackHours)] int deleteLookBackHours=48)
        {
            using var monitor = AdminDataLabsControllerReplayDeadLetterMessages.ToMonitor();
            try
            {
                monitor.OnStart();

                var utcNowFileTime = DateTime.UtcNow.ToFileTime();
                var solutionIoEndpointSlice = await GetSolutionIOEndpointSlice().ConfigureAwait(false);
                var query = $"?{AdminConstants.QueueName}={queueName}&{AdminConstants.ReplayLookBackHours}={replayLookBackHours}&{AdminConstants.UtcNowFileTime}={utcNowFileTime}&{AdminConstants.NeedDelete}={needDelete}&{AdminConstants.DeleteLookBackHours}={deleteLookBackHours}";
                var (aggregatedResponseContent, responseContentToDisplay) = await GetAggregatedResponseFromMultipleIOControllerCalls(solutionIoEndpointSlice, AdminConstants.ReplayDeadLetterMessages, query, numberOfNodesInParallel).ConfigureAwait(false);

                monitor.Activity["Aggregated response content"] = aggregatedResponseContent;

                monitor.OnCompleted();
                return Ok(responseContentToDisplay);
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                return new ObjectResult(ex);
            }
        }
        private async Task<V1EndpointSlice?> GetSolutionIOEndpointSlice()
        {
            var client = _kubernetesProvider.GetKubernetesClient();
            var slices = await client.ListNamespacedEndpointSliceAsync(SolutionNamespace).ConfigureAwait(false);
            var solutionIoEndpointSlice = slices.Items.SingleOrDefault(slice => slice.Metadata.Name.Contains(SolutionNamespaceNamePrefix));

            return solutionIoEndpointSlice;
        }

        private async Task<string> GetResponseFromIOServiceAdminController(string uriAffix, string query, string? ip=null)
        {
            var requestUri = $"{AdminConstants.BaseIOServiceRoute}/{uriAffix}";
            var endpoint = GetServiceEndpoint(DLService.IOService);

            var uriBuilder = new UriBuilder(endpoint);
            uriBuilder.Path = requestUri;
            uriBuilder.Query = query;
            if (ip != null)
            {
                uriBuilder.Host = ip;
            }

            var response = await _httpClient.PatchAsync(uriBuilder.ToString(), null).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            
            return responseContent;
        }

        private async Task<(string, string)> GetAggregatedResponseFromMultipleIOControllerCalls(V1EndpointSlice? solutionIoEndpointSlice, string uriAffix, string query, int numberOfNodesInParallel)
        {
            if (solutionIoEndpointSlice == null)
            {
                throw new Exception("Solution IO endpoint slice not found");
            }

            var nodeCount = solutionIoEndpointSlice.Endpoints.Count;
            if (numberOfNodesInParallel != -1 && numberOfNodesInParallel < 0)
            {
                throw new Exception("NumberOfNodesInParallel value is invalid. Please enter -1 for all nodes or a positive number.");
            }
            else if (numberOfNodesInParallel == -1 || numberOfNodesInParallel > nodeCount)
            {
                numberOfNodesInParallel = nodeCount;
            }

            var aggregatedResponseContentBuilder = new StringBuilder();
            var responseContentToDisplay = "";
            var traversedNodes = new HashSet<int>();
            Random random = new Random(Guid.NewGuid().GetHashCode());

            while (traversedNodes.Count < numberOfNodesInParallel)
            {
                var randomNodeIndex = random.Next(0, nodeCount);
                if (!traversedNodes.Add(randomNodeIndex))
                {
                    continue;
                }

                var ip = solutionIoEndpointSlice.Endpoints[randomNodeIndex].Addresses?[0];

                var responseContent = await GetResponseFromIOServiceAdminController(uriAffix, query, ip).ConfigureAwait(false);
                aggregatedResponseContentBuilder.AppendLine($"Response for node {traversedNodes.Count}: " + responseContent);

                // There is length limit for jarvis response. We will return success response if a request to any one of the nodes succeeds,
                // and return the first failure message if requests to all nodes fail.
                if (traversedNodes.Count == 1)
                {
                    responseContentToDisplay = responseContent;
                }
                if (responseContent.Contains(SuccessResponseIndicator))
                {
                    responseContentToDisplay = responseContent;
                }
            }
            var aggregatedResponseContent = aggregatedResponseContentBuilder.ToString().TrimEnd();

            return (aggregatedResponseContent, responseContentToDisplay);
        }
    }
}
