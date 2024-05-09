namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AccessTokenProvider
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// IAccessTokenProvider interface.
    /// </summary>
    public interface IAccessTokenProvider : IDisposable
    {
        /// <summary>
        /// </summary>
        /// <param name="resource">Resource to get a token against.</param>
        /// <param name="tenantId">Tenant Id.</param>
        /// <param name="scopes">scopes</param>
        /// <returns>AAD access token.</returns>
        Task<string> GetAccessTokenAsync(
            string? tenantId,
            string[]? scopes, // e.g. { "resourceForScope/.default" }
            CancellationToken cancellationToken);
    }
}
