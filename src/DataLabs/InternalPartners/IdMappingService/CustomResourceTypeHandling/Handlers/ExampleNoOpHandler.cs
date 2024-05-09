namespace IdMappingService.CustomResourceTypeHandling.Handlers
{
    using IdMappingService.CustomResourceTypeHandling.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.IdMappingService.Services.Contracts;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    [ExcludeFromCodeCoverage]
    public class ExampleNoOpHandler : IIdentifierCreationHandler
    {
        public IdentifierCreationHandlerType HandlerType => IdentifierCreationHandlerType.Additive;

        public List<Identifier> CreateIdentifiers(NotificationResourceDataV3<GenericResource> resourceData, InternalIdSpecification internalIdSpecification, IActivity parentActivity)
        {
            return new List<Identifier>();
        }
    }
}
