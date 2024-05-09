namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.SwaggerGenerationFilters
{
    using Microsoft.OpenApi.Any;
    using Microsoft.OpenApi.Models;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Acis;
    using Swashbuckle.AspNetCore.SwaggerGen;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;

    [ExcludeFromCodeCoverage]
    internal class DataClassificationOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var acisDataClassificationAttribute = context.MethodInfo.GetCustomAttributes(false).OfType<AcisDataClassificationAttribute>().FirstOrDefault();
            if (acisDataClassificationAttribute != null)
            {
                operation.Extensions["x-ms-dataclassificationlevel"] = new OpenApiString(acisDataClassificationAttribute.ClassificationLevel.ToString());
            }
        }
    }
}