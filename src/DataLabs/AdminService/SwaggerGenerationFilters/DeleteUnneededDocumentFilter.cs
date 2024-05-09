namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.SwaggerGenerationFilters
{
    using Microsoft.OpenApi.Models;
    using Swashbuckle.AspNetCore.SwaggerGen;
    using System.Diagnostics.CodeAnalysis;

    [ExcludeFromCodeCoverage]
    internal class DeleteUnneededDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            try
            {
                foreach (var servers in swaggerDoc.Servers)
                {
                    servers.Url = null;
                }

                foreach (var apiDescription in context.ApiDescriptions.ToList())
                {
                    if (apiDescription.RelativePath != null && apiDescription.RelativePath.StartsWith("admin/common/"))
                    {
                        var pathToRemove = "/" + apiDescription.RelativePath;
                        swaggerDoc.Paths.Remove(pathToRemove);
                    }
                }
            }
            catch(Exception ex) {
                Console.WriteLine(ex.ToString());
            }
            
        }
    }
}