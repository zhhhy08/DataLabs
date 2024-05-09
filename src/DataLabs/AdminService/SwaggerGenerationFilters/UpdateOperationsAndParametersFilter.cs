namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.SwaggerGenerationFilters
{
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.OpenApi.Any;
    using Microsoft.OpenApi.Models;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Acis;
    using Swashbuckle.AspNetCore.SwaggerGen;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;

    [ExcludeFromCodeCoverage]
    internal class UpdateOperationsAndParametersFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {

            // Update the operationId so different actions across controllers have different ids.
            operation.OperationId = $"{context.ApiDescription.ActionDescriptor.RouteValues["controller"]}_{context.ApiDescription.ActionDescriptor.RouteValues["action"]}";

            // Force 200 response type.
            operation.Responses["200"].Description = "OK";
            //If we have empty response for the 200 populate the schema object.
            var contents = operation.Responses["200"].Content;

            if (contents.Count > 0)
            {
                //Remove the text/plain content type if any.
                contents.Remove("text/plain");
            }
            else
            {
                //This has empty contents type for 200 response.
                //We need to add schema as object.
                var mediatype = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = "object" }
                };
                contents.Add("application/json", mediatype);
            }

            if (operation?.Parameters == null)
            {
                return;
            }

            var paramsDescriptors = context.ApiDescription.ParameterDescriptions.ToDictionary(
                param => param.Name.ToLowerInvariant(), param => param);

            // Update parameters
            for (var i = 0; i < operation.Parameters.Count; i++)
            {
                if (paramsDescriptors.TryGetValue(operation.Parameters[i].Name.ToLowerInvariant(), out var paramDescriptors))
                {
                    if (paramDescriptors.ParameterDescriptor is ControllerParameterDescriptor controllerParameterDescriptor)
                    {
                        // Update if the parameter is required and the description from the AcisParamDescription attribute.
                        operation.Parameters[i].Required = !controllerParameterDescriptor.ParameterInfo.Attributes.HasFlag(ParameterAttributes.HasDefault);
                        var paramAttr = controllerParameterDescriptor.ParameterInfo.GetCustomAttributes(typeof(AcisParamDescriptionAttribute), false)?.OfType<AcisParamDescriptionAttribute>().FirstOrDefault();
                        if (!string.IsNullOrEmpty(paramAttr?.Description))
                        {
                            operation.Parameters[i].Description = paramAttr.Description;
                        }
                    }
                }
            }


            if (operation.RequestBody != null)
            {
                var requestBodyName = operation.RequestBody.Extensions.TryGetValue(OpenApiConstants.BodyName, out var bodyNameValue) &&
                    bodyNameValue is OpenApiString bodyName ?
                    bodyName.Value :
                    "body";
                if (paramsDescriptors.TryGetValue(requestBodyName, out var paramDescriptors))
                {
                    if (paramDescriptors.ParameterDescriptor is ControllerParameterDescriptor controllerParameterDescriptor)
                    {
                        // Update if the parameter is required and the description from the AcisParamDescription attribute.
                        operation.RequestBody.Required = !controllerParameterDescriptor.ParameterInfo.Attributes.HasFlag(ParameterAttributes.HasDefault);
                        var paramAttr = controllerParameterDescriptor.ParameterInfo.GetCustomAttributes(typeof(AcisParamDescriptionAttribute), false)?.OfType<AcisParamDescriptionAttribute>().FirstOrDefault();
                        if (!string.IsNullOrEmpty(paramAttr?.Description))
                        {
                            operation.RequestBody.Description = paramAttr.Description;
                        }
                    }
                }

                // If request body is not null, we will transform it into OpenApiParameter early, so that we can control its position
                // TODO: Remove this logic once ADS/Support Center fixes their bug
                // OpenApiBodyParameter is an internal class
                var bodyParameter = (typeof(OpenApiParameter).Assembly.CreateInstance("Microsoft.OpenApi.Models.OpenApiBodyParameter") as OpenApiParameter)!;
                bodyParameter.Description = operation.RequestBody.Description;
                bodyParameter.Name = requestBodyName;
                var schema = operation.RequestBody.Content.Values.FirstOrDefault()?.Schema;
                bodyParameter.Schema = schema?.Type != null || schema?.Reference != null ? schema : new OpenApiSchema { Type = "object" };
                bodyParameter.Required = operation.RequestBody.Required;
                bodyParameter.Extensions = operation.RequestBody.Extensions
                    .Where(ex => !ex.Key.Equals(OpenApiConstants.BodyName, StringComparison.Ordinal))
                    .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

                var parameters = new List<OpenApiParameter>(operation.Parameters.Count + 1) { bodyParameter };
                parameters.AddRange(operation.Parameters);

                operation.Parameters = parameters;
                operation.RequestBody = null;
            }
        }
    }
}