namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TrafficTuner
{
    public enum TrafficTunerNotAllowedReason
    {
        None,
        StopAllTenants,
        TenantId,
        SubscriptionId,
        ResourceType,
        MessageRetryCount,
        Region,
        ResourceId
    }
}
