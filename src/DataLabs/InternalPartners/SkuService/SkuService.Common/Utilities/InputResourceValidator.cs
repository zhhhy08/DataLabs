namespace SkuService.Common.Utilities
{
    using System.Text;
    using System.Threading;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.CacheClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class InputResourceValidator
    {
        private readonly ICacheClient cacheClient;
        private static readonly ActivityMonitorFactory IsProcessingRequiredAsyncMonitorFactory = new("InputResourceValidator.IsProcessingRequiredForInputResourceAsync");
        private const string ResourceState = "State";
        private const string GlobalSkuEvaulationResult = "GlobalSkuEvaulationResult";
        public InputResourceValidator(ICacheClient cacheClient)
        {
            this.cacheClient = cacheClient;
        }

        public async Task<bool> IsProcessingRequiredForInputResourceAsync(DataLabsARNV3Request request, CancellationToken cancellationToken)
        {
            var monitor = IsProcessingRequiredAsyncMonitorFactory.ToMonitor();
            monitor.OnStart();
            if (request.InputResource.Id.StartsWith(Constants.Subjob, StringComparison.OrdinalIgnoreCase))
            {
                monitor.Activity["Subjobs"] = true;
                monitor.OnCompleted();
                return true;
            }

            var resource = request.InputResource.Data.Resources[0];
            monitor.Activity["ResourceType"] = resource.ArmResource.Type;
            if (resource.ArmResource.Type.EqualsOrdinalInsensitively(Constants.GlobalSkuResourceType))
            {
                var inputSku = JsonConvert.SerializeObject(resource.ArmResource);
                var existingData = await cacheClient.GetValueAsync(resource.ResourceId, cancellationToken);
                // remove eventTimePrefix and compare
                if (existingData == null || !inputSku.Equals(Encoding.UTF8.GetString(existingData, Constants.EventTimeBytes, existingData.Length - Constants.EventTimeBytes)))
                {
                    monitor.Activity[GlobalSkuEvaulationResult] = true;
                    monitor.OnCompleted();
                    return true;
                }

                var eventTimeInCache = BitConverter.ToInt64(existingData, 0);
                var inputEventTime = resource.ResourceEventTime != default ? resource.ResourceEventTime : DateTimeOffset.UtcNow;
                var diff = inputEventTime!.Value.ToUnixTimeMilliseconds() - eventTimeInCache;
                // cache event time within 2 minutes
                var result = Math.Abs(diff) <= 120000;
                monitor.Activity[GlobalSkuEvaulationResult] = result;
                monitor.OnCompleted();
                return result;
            }

            if (resource.ArmResource.Type.EqualsOrdinalInsensitively(Constants.SubscriptionInternalPropertiesResourceType))
            {
                var state = ((JObject)resource.ArmResource.Properties)[Constants.State]?.ToString();
                if (string.IsNullOrEmpty(state) || state.EqualsOrdinalInsensitively(Constants.Deleted))
                {
                    monitor.Activity[ResourceState] = state;
                    monitor.OnCompleted();
                    return false;
                }
            }
            if (resource.ArmResource.Type.EqualsOrdinalInsensitively(Constants.SubscriptionFeatureRegistrationType))
            {
                var state = ((JObject)resource.ArmResource.Properties)[Constants.State]?.ToString();
                if (string.IsNullOrEmpty(state) || state.EqualsOrdinalInsensitively(Constants.Pending))
                {
                    monitor.Activity[ResourceState] = state;
                    monitor.OnCompleted();
                    return false;
                }
            }

            monitor.OnCompleted();
            return true;
        }
    }
}
