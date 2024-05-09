namespace Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Middlewares
{
    using System.Diagnostics.CodeAnalysis;
    /// <summary>
    /// AAD token authentication middleware extensions class.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class DSTSAuthMiddlewareExtensions
    {
        /// <summary>
        /// Method to add AAD token authentication middleware extension to application builder.
        /// </summary>
        /// <param name="builder">Application builder.</param>
        /// <returns>Application builder.</returns>
        public static IApplicationBuilder UseDSTSAuthMiddleware(
            this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<DSTSAuthMiddleware>();
        }
    }
}