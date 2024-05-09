namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AccessTokenProvider
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Metrics;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Identity.Client;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.DstsClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    /// <summary>
    /// Dsts token provider class. When certificate is rotated, new instance need to be created
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class DstsAccessTokenProvider : IAccessTokenProvider
    {
        private static readonly ActivityMonitorFactory DstsAccessTokenProviderConstructor =
            new("DstsAccessTokenProvider.Constructor");

        private static readonly ActivityMonitorFactory DstsAccessTokenProviderGetAccessTokenAsync = 
            new("DstsAccessTokenProvider.GetAccessTokenAsync");

        public const string DstsAccessTokenProviderMetric = "DstsAccessTokenProviderMetric";

        private static readonly Histogram<long> DstsAccessTokenProviderMetricDuration = MetricLogger.CommonMeter.CreateHistogram<long>(DstsAccessTokenProviderMetric);

        private string? _serverRealmAccessToken;
        private IConfidentialClientApplication? _realmApplication;

        private readonly string[] _dstsScopes;
        private readonly string[]? _serverRealmScopes;
        
        private readonly IConfidentialClientApplication _clientApplication;
        private readonly DstsConfigValues _dstsConfigValues;
        private readonly bool _needCrossRegionAuth;

        public DstsAccessTokenProvider(DstsConfigValues dstsConfigValues, X509Certificate2 certificate)
        {
            using var monitor = DstsAccessTokenProviderConstructor.ToMonitor();
            monitor.OnStart(false);

            try
            {
                _dstsConfigValues = dstsConfigValues;

                GuardHelper.ArgumentNotNullOrEmpty(_dstsConfigValues.ClientId);
                GuardHelper.ArgumentNotNullOrEmpty(_dstsConfigValues.ServerId);
                GuardHelper.ArgumentNotNullOrEmpty(_dstsConfigValues.ClientHome);

                var serverId = _dstsConfigValues.ServerId.TrimEnd('/');
                _dstsScopes = new string[] { $"{serverId}/.default" };

                monitor.Activity.Properties[SolutionConstants.DstsClientId] = _dstsConfigValues.ClientId;
                monitor.Activity.Properties[SolutionConstants.DstsServerId] = _dstsConfigValues.ServerId;
                monitor.Activity.Properties[SolutionConstants.DstsClientHome] = _dstsConfigValues.ClientHome;
                monitor.Activity.Properties[SolutionConstants.DstsServerHome] = _dstsConfigValues.ServerHome;
                monitor.Activity.Properties[SolutionConstants.DstsServerRealm] = _dstsConfigValues.ServerRealm;

                if (!string.IsNullOrWhiteSpace(_dstsConfigValues.ServerHome) && 
                    !_dstsConfigValues.ServerHome.Equals(_dstsConfigValues.ClientHome, StringComparison.OrdinalIgnoreCase))
                {
                    GuardHelper.ArgumentNotNullOrEmpty(_dstsConfigValues.ServerRealm);
                    _needCrossRegionAuth = true;
                }

                _serverRealmScopes = _needCrossRegionAuth ? new string[] { $"{_dstsConfigValues.ServerRealm}/.default" } : null;

                // Refer to this MSAL document
                // https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/web-apps-apis/client-credential-flows

                // this object will cache tokens in-memory - keep it as a singleton
                _clientApplication = ConfidentialClientApplicationBuilder.Create(_dstsConfigValues.ClientId)
                                    .WithCertificate(certificate, true)  //SendX5c is necessary for Dsts
                                    .WithAuthority(_dstsConfigValues.ClientHome) // For dsts, let's add authority here because it is same across requests
                                    .WithLegacyCacheCompatibility(false)
                                    .Build();

                // Don't add any token cache serializer so that we will use built-in MSAL memory cache without serializer
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
            string[]? scopes,
            CancellationToken cancellationToken)
        {
            try
            {
                AuthenticationResult authResult;

                if (_needCrossRegionAuth)
                {
                    /* 
                     * When the client and the server exists on different regions with different home dSTS instances the authentication process is called cross region authentication.
                     * This process requires two requests to acquire tokens:
                     * - The first token is acquired using any client credential, the client home dSTS instance and the target server realm as the scope.
                     * - The second token is acquired using the first token as credential, the target service home dSTS instance and using the target server application id as the scope. 
                     */
                    var realmAuthResult = await _clientApplication.AcquireTokenForClient(_serverRealmScopes)
                            .ExecuteAsync(cancellationToken)
                            .ConfigureAwait(false);

                    TagList realmTagList = default;
                    realmTagList.Add(SolutionConstants.AppIdDimension, _dstsConfigValues.ServerRealm);
                    realmTagList.Add(SolutionConstants.TokenSourceDimension, realmAuthResult.AuthenticationResultMetadata.TokenSource);
                    realmTagList.Add(SolutionConstants.CacheRefreshReasonDimension, realmAuthResult.AuthenticationResultMetadata.CacheRefreshReason);
                    realmTagList.Add(SolutionConstants.CrossRegionDimension, true);
                    DstsAccessTokenProviderMetricDuration.Record(realmAuthResult.AuthenticationResultMetadata.DurationTotalInMs, realmTagList);

                    var realmApplication = CreateRealmApplication(realmAuthResult.AccessToken);

                    authResult = await realmApplication.AcquireTokenForClient(_dstsScopes) // For dsts, dstsScoped is predefined like ApplicationId/.default
                        .ExecuteAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    authResult = await _clientApplication.AcquireTokenForClient(_dstsScopes) // For dsts, dstsScoped is predefined like ApplicationId/.default
                        .ExecuteAsync(cancellationToken)
                        .ConfigureAwait(false);
                }

                TagList tagList = default;
                tagList.Add(SolutionConstants.AppIdDimension, _dstsConfigValues.ServerId);
                tagList.Add(SolutionConstants.TokenSourceDimension, authResult.AuthenticationResultMetadata.TokenSource);
                tagList.Add(SolutionConstants.CacheRefreshReasonDimension, authResult.AuthenticationResultMetadata.CacheRefreshReason);
                tagList.Add(SolutionConstants.CrossRegionDimension, _needCrossRegionAuth);
                DstsAccessTokenProviderMetricDuration.Record(authResult.AuthenticationResultMetadata.DurationTotalInMs, tagList);

                return authResult.AccessToken;
            }
            catch (Exception ex)
            {
                using var monitor = DstsAccessTokenProviderGetAccessTokenAsync.ToMonitor();
                monitor.OnError(ex);
                throw;
            }
        }

        private IConfidentialClientApplication CreateRealmApplication(string realmAccessToken)
        {
            lock (this)
            {
                if (_realmApplication == null || _serverRealmAccessToken == null || !_serverRealmAccessToken.Equals(realmAccessToken))
                {
                    _realmApplication = ConfidentialClientApplicationBuilder.Create(_dstsConfigValues.ServerRealm)
                        .WithClientAssertion(realmAccessToken)
                        .WithAuthority(_dstsConfigValues.ServerHome)
                        .WithLegacyCacheCompatibility(false)
                        .Build();

                    // Don't add any token cache serializer so that we will use built-in MSAL memory cache without serializer

                    _serverRealmAccessToken = realmAccessToken;
                }

                return _realmApplication;
            }
        }

        public void Dispose()
        {
        }
    }
}
