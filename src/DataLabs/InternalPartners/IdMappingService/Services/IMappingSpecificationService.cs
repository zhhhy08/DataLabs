namespace Microsoft.WindowsAzure.IdMappingService.Services
{
    using Microsoft.WindowsAzure.IdMappingService.Services.Contracts;

    public interface IMappingSpecificationService
    {
        InternalIdSpecification GetInternalIdSpecification(string ResourceType);
    }
}
