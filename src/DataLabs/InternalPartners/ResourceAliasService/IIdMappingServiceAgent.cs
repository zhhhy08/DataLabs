namespace ResourceAliasService
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;

    /// <summary>
    /// Defines the contract for the service agent calling into external IdMapping service through resourceProxyClient, not part of the process of generating the IdMappings.
    /// </summary>
    public interface IIdMappingServiceAgent
    {
        Task<IdMappingsDto> GetArmIdsFromIdMapping(IEnumerable<string> resourceAliases, string resourceType, string? correlationId, int retryCount, IActivity activity, bool skipCacheRead, CancellationToken cancellationToken);
    }

    public record IdMappingsDto() {
        public string StatusCode;
        public IEnumerable<IdMappingRecord> IdMappings;
        public string? ErrorMessage;
    }

    public record IdMappingRecord()
    {
        public IList<string>? ArmIds;
        public string AliasResourceId;
        public string StatusCode;
        public string? ErrorMessage;
    }
}
