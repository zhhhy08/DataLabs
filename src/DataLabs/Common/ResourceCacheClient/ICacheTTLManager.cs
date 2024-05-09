namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ResourceCacheClient
{
    using System;

    public interface ICacheTTLManager
    {
        public TimeSpan GetCacheTTL(string? resourceType, bool inputType);
        public TimeSpan GetCacheTTLForNotFoundEntry(string? resourceType);

        public void AddUpdateListener(ICacheTTLManagerUpdateListener updateListener);
    }

    public interface ICacheTTLManagerUpdateListener
    {
        public void NotifyUpdatedConfig(ICacheTTLManager cacheTTLManager);
    }
}