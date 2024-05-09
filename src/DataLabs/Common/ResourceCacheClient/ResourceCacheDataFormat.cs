namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient
{
    public enum ResourceCacheDataFormat : byte
    {
       // Notice!!!!
       // For backward compatibility, do not change the order of the enum values
       // Otherwise, it will break the existing cache
       ARN = 0,
       ARM = 1,
       CAS = 2,
       ARMAdmin = 3,
       SubscriptionArmReadLimit = 4,
       PacificCollection = 5,
       NotFoundEntry = 6,
       IdMapping = 7,
    }
}