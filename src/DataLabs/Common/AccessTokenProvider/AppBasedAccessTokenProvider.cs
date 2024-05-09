namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AccessTokenProvider
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Metrics;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Identity.Client;
    using Microsoft.Identity.Web;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    /// <summary>
    /// App based access token provider class. When certificate is rotated, new instance need to be created
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class AppBasedAccessTokenProvider : IAccessTokenProvider
    {
        public const string AppBasedAccessTokenProviderMetric = "AppBasedAccessTokenProviderMetric";
        
        private static readonly ActivityMonitorFactory AppBasedAccessTokenProviderConstructor =
            new("AppBasedAccessTokenProvider.Constructor");

        private static readonly ActivityMonitorFactory AppBasedAccessTokenProviderGetAccessTokenAsync = 
            new("AppBasedAccessTokenProvider.GetAccessTokenAsync");

        private static readonly Histogram<long> AppBasedAccessTokenProviderMetricDuration = MetricLogger.CommonMeter.CreateHistogram<long>(AppBasedAccessTokenProviderMetric);

        private readonly IConfidentialClientApplication _clientApplication;
        private readonly string _applicationId;
        private readonly string _aadAuthority;

        /*
         * Refer to public enum AzureCloudInstance
        /// <summary>
        /// Microsoft Azure public cloud. Maps to https://login.microsoftonline.com
        /// </summary>
        AzurePublic,

        /// <summary>
        /// Microsoft Azure China cloud. Maps to https://login.chinacloudapi.cn
        /// </summary>
        AzureChina,

        /// <summary>
        /// Microsoft Azure German cloud ("Black Forest"). Maps to https://login.microsoftonline.de
        /// </summary>
        AzureGermany,

        /// <summary>
        /// US Government cloud. Maps to https://login.microsoftonline.us
        /// </summary>
        AzureUsGovernment
        */

        public AppBasedAccessTokenProvider(
            string applicationId, 
            X509Certificate2 certificate,
            string aadAuthority,
            int inMemoryCacheLimit = 100 * 1024 * 1024) //An app token is about 2-3 KB in size, so 100MB can cache 30000 ~ 50000 tokens)
        {
            using var monitor = AppBasedAccessTokenProviderConstructor.ToMonitor();
            monitor.OnStart(false);

            try
            {
                _applicationId = applicationId;
                _aadAuthority = aadAuthority;
                if (!_aadAuthority.EndsWith("/"))
                {
                    _aadAuthority += "/";
                }

                monitor.Activity.Properties["ApplicationId"] = _applicationId;
                monitor.Activity.Properties["AADAuthority"] = _aadAuthority;
                monitor.Activity.Properties["CacheSizeLimit"] = inMemoryCacheLimit;

                // Refer to this MSAL document
                // https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/web-apps-apis/client-credential-flows

                // this object will cache tokens in-memory - keep it as a singleton
                _clientApplication = ConfidentialClientApplicationBuilder.Create(applicationId)
                                    // don't specify authority here, we'll do it on the request 
                                    .WithCertificate(certificate, true)    // SendX5c is necessary for first party App
                                    .WithLegacyCacheCompatibility(false)
                                    .Build();

                // Add an in-memory serializer token cache with options
                _clientApplication.AddInMemoryTokenCache(services =>
                {
                    // Configure the memory cache options
                    services.Configure<MemoryCacheOptions>(options =>
                    {
                        options.SizeLimit = inMemoryCacheLimit;
                    });
                });

                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public async Task<string> GetAccessTokenAsync(
            string? tenantId,
            string[]? scopes, // e.g. { "resourceForScope/.default" }
            CancellationToken cancellationToken)
        {
            try
            {
                // AppBaseAccessToken requires scopes and tenantId
                GuardHelper.ArgumentNotNullOrEmpty(tenantId, nameof(tenantId));
                GuardHelper.ArgumentNotNull(scopes, nameof(scopes));

                var builder = _clientApplication.AcquireTokenForClient(scopes);
                var authority = _aadAuthority + tenantId;

#pragma warning disable CS0618 // Type or member is obsolete
                // MSAL token cache guideline web site still show this way to use token cache for multi-tenant app
                // https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/web-apps-apis/client-credential-flows
                builder = builder.WithAuthority(authority);
#pragma warning restore CS0618 // Type or member is obsolete

                var authResult = await builder.ExecuteAsync(cancellationToken).ConfigureAwait(false);

                TagList tagList = default;
                tagList.Add(SolutionConstants.AppIdDimension, _applicationId);
                tagList.Add(SolutionConstants.TokenSourceDimension, authResult.AuthenticationResultMetadata.TokenSource);
                tagList.Add(SolutionConstants.CacheRefreshReasonDimension, authResult.AuthenticationResultMetadata.CacheRefreshReason);
                AppBasedAccessTokenProviderMetricDuration.Record(authResult.AuthenticationResultMetadata.DurationTotalInMs, tagList);

                return authResult.AccessToken;
            }
            catch (Exception ex)
            {
                using var monitor = AppBasedAccessTokenProviderGetAccessTokenAsync.ToMonitor();
                monitor.OnError(ex);
                throw;
            }
        }

        public void Dispose()
        {
        }
    }
}
