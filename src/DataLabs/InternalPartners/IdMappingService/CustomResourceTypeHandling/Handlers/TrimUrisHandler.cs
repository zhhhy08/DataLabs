namespace IdMappingService.CustomResourceTypeHandling.Handlers
{
    using IdMappingService.CustomResourceTypeHandling.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.IdMappingService.Services;
    using Microsoft.WindowsAzure.IdMappingService.Services.Contracts;

    /// <summary>
    /// Custom Handler for extracting base hostname from a URI
    /// Removes trailing slashes and port numbers and removes "leading https://"
    /// "https://test-sb.servicebus.windows.net:443/" -> "test-sb.servicebus.windows.net"
    /// </summary>
    internal class TrimUrisHandler : IIdentifierCreationHandler
    {
        public IdentifierCreationHandlerType HandlerType => IdentifierCreationHandlerType.Overwrite;

        public List<Identifier> CreateIdentifiers(NotificationResourceDataV3<GenericResource> resourceData, InternalIdSpecification internalIdSpecification, IActivity parentActivity)
        {
            var extractedIdentifiers = PropertyExtractionService.ExtractProperties(resourceData.ArmResource, internalIdSpecification, parentActivity);
            
            foreach (var identifier in extractedIdentifiers) {
                identifier.Value = TryTrimUri(identifier.Value);
            }
            return extractedIdentifiers.ToList();
        }

        private string TryTrimUri(string url)
        {
            var isUri = Uri.TryCreate(url, UriKind.Absolute,out var uri);

            return isUri ? uri.Host : url;
        }
    }
}
