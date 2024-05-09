namespace ResourceAliasService
{
    using System.Diagnostics;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.ResourceProxyClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    /// <summary>
    /// This service agent is calling into external IdMapping service through resourceProxyClient, not part of the process of generating the IdMappings.
    /// </summary>
    public class IdMappingServiceAgent: IIdMappingServiceAgent
    {
        private readonly IResourceProxyClient resourceProxyClient;

        public IdMappingServiceAgent(IResourceProxyClient resourceProxyClient)
        {
            this.resourceProxyClient = resourceProxyClient;
        }

        public async Task<IdMappingsDto> GetArmIdsFromIdMapping(IEnumerable<string> resourceAliases, string resourceType, string? correlationId, int retryCount, IActivity activity, bool skipCacheRead, CancellationToken cancellationToken)
        {
            GuardHelper.ArgumentNotNull(resourceAliases, nameof(resourceAliases));
            GuardHelper.ArgumentNotNullOrEmpty(resourceType, nameof(resourceType));
            GuardHelper.ArgumentNotNull(activity, nameof(activity));

            if (!resourceAliases.Any())
            {
                return new IdMappingsDto
                {
                    StatusCode = ActivityStatusCode.Ok.ToString(),
                    IdMappings = Enumerable.Empty<IdMappingRecord>()
                };
            }

            var idMappingRequest = new DataLabsIdMappingRequest(activity[SolutionConstants.PartnerTraceId]!.ToString()!, retryCount, correlationId, resourceType, new IdMappingRequestBody { AliasResourceIds = resourceAliases });
            var idMappingResponse = await resourceProxyClient.GetIdMappingsAsync(idMappingRequest, cancellationToken, skipCacheRead: skipCacheRead).IgnoreContext();

            if (idMappingResponse.ErrorResponse != null)
            {
                ResourceAliasSolutionService.IdMappingCallErrorCounter.Add(1, new KeyValuePair<string, object?>("statusCode", idMappingResponse.ErrorResponse.HttpStatusCode));
                activity.Properties["IdMappingCallStatusCode"] = idMappingResponse.ErrorResponse.HttpStatusCode;
                activity.Properties["IdMappingCallErrorMessage"] = idMappingResponse.ErrorResponse.ErrorDescription;

                return new IdMappingsDto
                {
                    StatusCode = ActivityStatusCode.Error.ToString(),
                    ErrorMessage = idMappingResponse.ErrorResponse.ErrorDescription,
                    IdMappings = new List<IdMappingRecord>()
                };
            }

            var idMappings = idMappingResponse.SuccessResponse.Select(x =>
                new IdMappingRecord
                {
                    ArmIds = x.ArmIds,
                    AliasResourceId = x.AliasResourceId,
                    StatusCode = x.StatusCode,
                    ErrorMessage = x.ErrorMessage
                });

            return new IdMappingsDto
            {
                StatusCode = ActivityStatusCode.Ok.ToString(),
                IdMappings = idMappings
            };
        }
    }
}
