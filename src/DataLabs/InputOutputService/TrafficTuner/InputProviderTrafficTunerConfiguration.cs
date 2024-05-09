namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TrafficTuner
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.TrafficTuner;

    public class InputProviderTrafficTunerConfiguration
    {
        public IOTrafficTuner InputTrafficTuner;
        public IOTrafficTuner PartnerTrafficTuner;

        public InputProviderTrafficTunerConfiguration(string inputTrafficTunerRuleKey, string inputTrafficTunerMetricName, string partnerTrafficTunerRuleKey, string partnerTrafficTunerRuleMetricName)
        {
            GuardHelper.ArgumentNotNullOrEmpty(inputTrafficTunerMetricName, nameof(inputTrafficTunerMetricName));
            GuardHelper.ArgumentNotNullOrEmpty(inputTrafficTunerMetricName, nameof(inputTrafficTunerMetricName));
            GuardHelper.ArgumentNotNullOrEmpty(partnerTrafficTunerRuleKey, nameof(partnerTrafficTunerRuleKey));
            GuardHelper.ArgumentNotNullOrEmpty(partnerTrafficTunerRuleMetricName, nameof(partnerTrafficTunerRuleMetricName));

            this.InputTrafficTuner = new IOTrafficTuner(inputTrafficTunerRuleKey,inputTrafficTunerMetricName);
            this.PartnerTrafficTuner = new IOTrafficTuner(partnerTrafficTunerRuleKey, partnerTrafficTunerRuleMetricName);
        }
    }
}
