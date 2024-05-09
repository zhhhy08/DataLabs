namespace IdMappingService.CustomResourceTypeHandling.Contracts
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.IdMappingService.Services.Contracts;
    using System.Collections.Generic;

    /// <summary>
    /// Defines the contract for a custom handler that can be used to extract identifiers from a resource
    /// </summary>
    public interface IIdentifierCreationHandler
    {
        public IdentifierCreationHandlerType HandlerType { get; }

        public List<Identifier> CreateIdentifiers(NotificationResourceDataV3<GenericResource> resourceData, InternalIdSpecification internalIdSpecification, IActivity parentActivity);

    }
}
