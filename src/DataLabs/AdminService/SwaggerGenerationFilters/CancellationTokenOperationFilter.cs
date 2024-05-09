namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.SwaggerGenerationFilters
{
    using Microsoft.OpenApi.Models;
    using Microsoft.Win32.SafeHandles;
    using Swashbuckle.AspNetCore.SwaggerGen;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;

    [ExcludeFromCodeCoverage]
    public class CancellationTokenOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {

            var excludedParameters = context.ApiDescription.ParameterDescriptions
                .Where(p => (p.ModelMetadata.ContainerType == typeof(CancellationToken)
                || p.ModelMetadata.ContainerType == typeof(WaitHandle)
                || p.ModelMetadata.ContainerType == typeof(SafeWaitHandle)))
                .Select(p => operation.Parameters.FirstOrDefault(oparam => oparam.Name == p.Name));

            foreach (var parameter in excludedParameters)
            {
                operation.Parameters.Remove(parameter);
            }
        }
    }
}