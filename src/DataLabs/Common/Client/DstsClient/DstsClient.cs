namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.DstsClient
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.SecretProviderManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class DstsClient : IRestClient, ICertificateListener
    {
        private static readonly ILogger<DstsClient> Logger = DataLabLoggerFactory.CreateLogger<DstsClient>();
        private static readonly ActivityMonitorFactory DstsClientCertificateChangedAsync = new ("DstsClient.CertificateChangedAsync");

        private readonly DstsConfigValues _dstsConfigValues;
        private readonly RestClientOptions _restClientOptions;

        private DstsV2TokenGenerationHandler _dstsDelegatingHandler;
        private RestClient _restClient;

        private readonly object _updateLock = new ();
        private volatile bool _disposed;

        public DstsClient(
            DstsConfigValues dstsConfigValues, 
            RestClientOptions restClientOptions)
        {
            _dstsConfigValues = dstsConfigValues;
            _restClientOptions = restClientOptions;

            GuardHelper.ArgumentNotNullOrEmpty(_dstsConfigValues.ClientId);
            GuardHelper.ArgumentNotNullOrEmpty(_dstsConfigValues.ServerId);
            GuardHelper.ArgumentNotNullOrEmpty(_dstsConfigValues.ClientHome);
            GuardHelper.ArgumentNotNullOrEmpty(_dstsConfigValues.CertificateName);

            // Get Certificate from SecretProviderManager
            var secretProviderManager = SecretProviderManager.Instance;
            var clientCertificate = secretProviderManager.GetCertificateWithListener(
                certificateName: _dstsConfigValues.CertificateName,
                listener: this,
                allowMultiListeners: true);
            GuardHelper.ArgumentNotNull(clientCertificate);

            var clientAuthOptions = RestClient.CreateClientAuthenticationOptions(clientCertificate, _dstsConfigValues.SkipCertificateValidation);

            _dstsDelegatingHandler = new DstsV2TokenGenerationHandler(
                dstsConfigValues: _dstsConfigValues,
                clientCertificate: clientCertificate);

            _restClient = new RestClient(
                options: _restClientOptions,
                clientAuthenticationOptions: clientAuthOptions,
                delegatingHandler: _dstsDelegatingHandler);
        }

        public Task<HttpResponseMessage> CallRestApiAsync<TRequest>(
            IEndPointSelector endPointSelector,
            string requestUri,
            HttpMethod httpMethod, 
            string? accessToken, 
            IEnumerable<KeyValuePair<string, string>>? headers, 
            TRequest? jsonRequestContent,
            string? clientRequestId,
            bool skipUriPathLogging,
            CancellationToken cancellationToken) where TRequest : class
        {
            return _restClient.CallRestApiAsync(
                endPointSelector: endPointSelector,
                requestUri: requestUri,
                httpMethod: httpMethod,
                accessToken: accessToken,
                headers: headers,
                jsonRequestContent: jsonRequestContent,
                clientRequestId: clientRequestId,
                skipUriPathLogging: skipUriPathLogging,
                cancellationToken);
        }

        public Task CertificateChangedAsync(X509Certificate2 newCertificate)
        {
            // Certificate is changed
            // we have to create new RestClient and DstsV2TokenGenerationHandler with new certifiate
            using var monitor = DstsClientCertificateChangedAsync.ToMonitor();

            DstsV2TokenGenerationHandler? newDstsDelegatingHandler = null;
            RestClient? newRestClient = null;
            try
            {
                var oldDstsDelegatingHandler = _dstsDelegatingHandler;
                var oldRestClient = _restClient;
                
                lock(_updateLock)
                {
                    if (_disposed)
                    {
                        // Already Disposed
                        return Task.CompletedTask;
                    }

                    monitor.OnStart(false);

                    var newClientAuthOptions = RestClient.CreateClientAuthenticationOptions(newCertificate, _dstsConfigValues.SkipCertificateValidation);

                    newDstsDelegatingHandler = new DstsV2TokenGenerationHandler(
                        dstsConfigValues: _dstsConfigValues,
                        clientCertificate: newCertificate);

                    newRestClient = new RestClient(
                        options: _restClientOptions,
                        clientAuthenticationOptions: newClientAuthOptions,
                        delegatingHandler: newDstsDelegatingHandler);

                    Interlocked.Exchange(ref _dstsDelegatingHandler, newDstsDelegatingHandler);
                    Interlocked.Exchange(ref _restClient, newRestClient);

                    // Set local variable for new to null to avoid dispose(inside catch)
                    newDstsDelegatingHandler = null;
                    newRestClient = null;

                    // DO NOT log certificate content because it includes private key
                    Logger.LogWarning("{config} is changed", _dstsConfigValues.CertificateName);

                    // Dispose old ones
                    oldRestClient.Dispose();
                    oldDstsDelegatingHandler.Dispose();

                    monitor.OnCompleted();
                }
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);

                if (newRestClient != null)
                {
                    newRestClient.Dispose();
                    newRestClient = null;
                }

                if (newDstsDelegatingHandler != null)
                {
                    newDstsDelegatingHandler.Dispose();
                    newDstsDelegatingHandler = null;
                }
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            lock(_updateLock)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    var delegatingHandler = _dstsDelegatingHandler;
                    var restClient = _restClient;

                    delegatingHandler?.Dispose();
                    restClient?.Dispose();
                    SecretProviderManager.Instance.RemoveListener(_dstsConfigValues.CertificateName, this);
                }
            }
        }
    }
}