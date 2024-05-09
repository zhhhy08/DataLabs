namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AccessTokenProvider
{
    using global::Azure.Core;
    using global::Azure.Identity;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    
    /// <summary>
    /// DefaultAzureCredential token provider class of the current application.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class DefaultAzureCredentialTokenProvider : IAccessTokenProvider
    {
        private static readonly ActivityMonitorFactory DefaultAzureCredentialTokenProviderGetAccessTokenAsync = 
            new("DefaultAzureCredentialTokenProvider.GetAccessTokenAsync");
        
        private readonly DefaultAzureCredential _tokenCredential;

        public DefaultAzureCredentialTokenProvider()
        {
            _tokenCredential = new DefaultAzureCredential();
        }

        public async Task<string> GetAccessTokenAsync(
            string? tenantId,
            string[]? scopes, // e.g. { "resourceForScope/.default" }
            CancellationToken cancellationToken)
        {
            try
            {
                GuardHelper.ArgumentNotNullOrEmpty(tenantId, nameof(tenantId));
                GuardHelper.ArgumentNotNull(scopes, nameof(scopes));

                var accessToken = await _tokenCredential.GetTokenAsync(new TokenRequestContext(scopes: scopes, tenantId: tenantId));
                return accessToken.Token;
            }
            catch (Exception ex)
            {
                using var monitor = DefaultAzureCredentialTokenProviderGetAccessTokenAsync.ToMonitor();
                monitor.OnError(ex);
                throw;
            }
        }

        public void Dispose()
        {
        }
    }
}
