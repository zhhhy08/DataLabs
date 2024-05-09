namespace IdMappingService.CustomResourceTypeHandling
{
    using IdMappingService.CustomResourceTypeHandling.Contracts;
    using IdMappingService.CustomResourceTypeHandling.Handlers;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    [ExcludeFromCodeCoverage]
    public class CustomHandlerRegistration
    {
        private static TrimUrisHandler trimUrisHandler = new TrimUrisHandler();
        public static Dictionary<string, IIdentifierCreationHandler> Handlers = new Dictionary<string, IIdentifierCreationHandler>(StringComparer.OrdinalIgnoreCase) 
        { 
            {"Microsoft.Cache/redis", trimUrisHandler },
            {"Microsoft.ClassicCompute/DomainName", trimUrisHandler },
            {"Microsoft.DocumentDB/DatabaseAccounts", trimUrisHandler },
            {"Microsoft.Eventhub/Namespaces", trimUrisHandler },
            {"Microsoft.Keyvault/Vaults", trimUrisHandler },
            {"Microsoft.Kusto/clusters", trimUrisHandler },
            {"Microsoft.ServiceBus/Namespaces", trimUrisHandler },
            {"Microsoft.Storage/StorageAccounts", trimUrisHandler },
            {"Microsoft.ServiceFabric/Clusters", trimUrisHandler },
        };
    }
}
