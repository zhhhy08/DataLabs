namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TrafficTuner
{
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Runtime.CompilerServices;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TrafficTuner;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts.ARN;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;

    public class IOTrafficTuner
    {
        public const string ReasonDimension = "Reason";

        private readonly Counter<long> _trafficTunerCounter;
        private readonly ITrafficTuner _trafficTuner;
        
        public IOTrafficTuner(string configKey, string metricName)
        {
            _trafficTuner = new TrafficTuner(configKey);
            _trafficTunerCounter = IOServiceOpenTelemetry.IOServiceNameMeter.CreateCounter<long>(metricName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddFilteredReason(TrafficTunerNotAllowedReason tunerResult)
        {
            _trafficTunerCounter.Add(1, new KeyValuePair<string, object?>(ReasonDimension, tunerResult.FastEnumToString()));
        }

        public (TrafficTunerResult result, TrafficTunerNotAllowedReason reason) EvaluateTunerResult(ARNSingleInputMessage singleInputMessage, int retryCount)
        {
            // Just check null here instead of using SafeEvaluateTunerResult
            // so that we don't need to create uncessary TrafficTunerRequest
            if (_trafficTuner == null || !_trafficTuner.HasRule)
            {
                return (TrafficTunerResult.Allowed, TrafficTunerNotAllowedReason.None);
            }

            var trafficTunerRequest = new TrafficTunerRequest(
                subscriptionId: singleInputMessage.SubscriptionId,
                resourceType: singleInputMessage.ResourceType,
                tenantId: singleInputMessage.TenantId,
                resourceLocation: singleInputMessage.ResourceLocation,
                messageRetryCount: retryCount,
                singleInputMessage.ResourceId);

            var tunerResult = _trafficTuner.EvaluateTunerResult(trafficTunerRequest);
            if (tunerResult.result != TrafficTunerResult.Allowed)
            {
                AddFilteredReason(tunerResult.reason);
            }
            return tunerResult;
        }

        public void UpdateTrafficTunerRuleValue(string trafficTunerRuleString)
        {
            _trafficTuner.UpdateTrafficTunerRuleValue(trafficTunerRuleString);
        }
    }
}
