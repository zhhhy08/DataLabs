namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.SwaggerGenerationFilters
{
    using Microsoft.OpenApi.Any;
    using Microsoft.OpenApi.Models;
    using Swashbuckle.AspNetCore.SwaggerGen;
    using System.Diagnostics.CodeAnalysis;

    [ExcludeFromCodeCoverage]
    internal class SetBodyNameExtension : IRequestBodyFilter
    {
        public void Apply(OpenApiRequestBody requestBody, RequestBodyFilterContext context)
        {
            var parameterInfo = context.BodyParameterDescription?.ParameterInfo();

            if (parameterInfo != null)
            {
                // Use this[] instead Add to avoid ArgumentException "An item with the same key has already been added"

                requestBody.Extensions[OpenApiConstants.BodyName] = new OpenApiString(parameterInfo.Name);
            }
        }
    }
}