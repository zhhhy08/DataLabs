namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.DstsClient
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.AccessTokenProvider;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    /// <summary>
    /// This Authentication Handler is based on MSAL library
    /// Inbuilt token cache is used at the App level which reduces
    /// no.of tokens generated overall
    /// https://dev.azure.com/msazure/One/_git/EngSys-Security-dSTS?path=/Security/Samples/JWTSamples/WebAPIClientJWT_MSAL_DotNetFramework/AuthenticationHandler.cs
    /// </summary>

    //Adding exclude from code coverage as class handles initialization of IConfidentialClientApplication and Client Certificate
    //and passing these objects (mocks) via constructor based injection is anti-pattern 
    [ExcludeFromCodeCoverage]
    public class DstsV2TokenGenerationHandler : DelegatingHandler
    {
        private static readonly ActivityMonitorFactory DstsV2TokenGenerationHandlerConstructor =
            new("DstsV2TokenGenerationHandler.Constructor");

        private static readonly ActivityMonitorFactory DstsV2TokenGenerationHandlerSendAsync =
            new("DstsV2TokenGenerationHandler.SendAsync");

        private static readonly ActivityMonitorFactory DstsV2TokenGenerationHandlerConfigureRequestWithDSTSTokenAsync =
            new("DstsV2TokenGenerationHandler.ConfigureRequestWithDSTSTokenAsync");

        private readonly DstsAccessTokenProvider _dstsAccessTokenProvider;
        private volatile bool _disposed;

        public DstsV2TokenGenerationHandler(
            DstsConfigValues dstsConfigValues,
            X509Certificate2 clientCertificate)
        {
            using var monitor = DstsV2TokenGenerationHandlerConstructor.ToMonitor();
            monitor.OnStart(false);

            try
            {
                _dstsAccessTokenProvider = new DstsAccessTokenProvider(dstsConfigValues, clientCertificate);
                monitor.OnCompleted();
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            using var monitor = DstsV2TokenGenerationHandlerSendAsync.ToMonitor();
            monitor.OnStart(false);

            try
            {
                monitor.Activity[SolutionConstants.RequestURI] = request.RequestUri;

                await ConfigureRequestWithDSTSTokenAsync(request, cancellationToken).IgnoreContext();
                var responseMessage = await base.SendAsync(request, cancellationToken).IgnoreContext();

                monitor.Activity.Properties[SolutionConstants.HttpStatusCode] = responseMessage.StatusCode;

                monitor.OnCompleted();
                return responseMessage;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        private async Task ConfigureRequestWithDSTSTokenAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                var token = await _dstsAccessTokenProvider.GetAccessTokenAsync(tenantId: null, scopes: null, cancellationToken: cancellationToken).ConfigureAwait(false);
                SetDSTSV2AuthHeaders(request, token);
            }
            catch (Exception ex)
            {
                using var methodMonitor = DstsV2TokenGenerationHandlerConfigureRequestWithDSTSTokenAsync.ToMonitor();
                methodMonitor.OnError(ex);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetDSTSV2AuthHeaders(HttpRequestMessage request, string accessToken)
        {
            GuardHelper.ArgumentNotNull(accessToken);

            var headerStr = new AuthenticationHeaderValue(SolutionConstants.AuthHeaderBearerScheme, accessToken).ToString();

            //update the Authorization header, remove old unauthorized dsts header
            request.Headers.Remove(CommonHttpHeaders.AuthorizationDstsV2);
            request.Headers.TryAddWithoutValidation(CommonHttpHeaders.AuthorizationDstsV2, headerStr);

            //CAS expects token in Authorization header
            request.Headers.Remove(CommonHttpHeaders.Authorization);
            request.Headers.TryAddWithoutValidation(CommonHttpHeaders.Authorization, headerStr);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                var tokenProvider = _dstsAccessTokenProvider;
                tokenProvider?.Dispose();
            }

            base.Dispose(disposing);
        }

    }
}