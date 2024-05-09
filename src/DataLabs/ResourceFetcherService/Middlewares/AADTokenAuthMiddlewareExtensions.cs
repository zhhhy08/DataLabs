namespace Microsoft.WindowsAzure.Governance.DataLabs.ResourceFetcherService.Middlewares
{
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.AspNetCore.Builder;

    /// <summary>
    /// AAD token authentication middleware extensions class.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class AADTokenAuthMiddlewareExtensions
    {
        public static IApplicationBuilder UseAADTokenAuthMiddleware(
            this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AADTokenAuthMiddleware>();
        }
    }
}
