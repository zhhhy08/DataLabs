namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.ClientCertificateRestClient
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.SecretProviderManager;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class ClientCertificateRestClient : IRestClient, ICertificateListener
    {
        private static readonly ILogger<ClientCertificateRestClient> Logger = DataLabLoggerFactory.CreateLogger<ClientCertificateRestClient>();
        private static readonly ActivityMonitorFactory ClientCertificateRestClientCertificateChangedAsync = new ("ClientCertificateRestClient.CertificateChangedAsync");

        private readonly string _certificateName;
        private readonly RestClientOptions _restClientOptions;
        private RestClient _restClient;

        private readonly object _updateLock = new ();
        private volatile bool _disposed;

        public ClientCertificateRestClient(
            string certificateName,
            RestClientOptions restClientOptions)
        {
            _certificateName = certificateName;
            _restClientOptions = restClientOptions;

            // Get Certificate from SecretProviderManager
            var secretProviderManager = SecretProviderManager.Instance;
            var clientCertificate = secretProviderManager.GetCertificateWithListener(
                certificateName: _certificateName,
                listener: this,
                allowMultiListeners: true);
            GuardHelper.ArgumentNotNull(clientCertificate);

            var clientAuthOptions = CreateClientAuthenticationOptions(clientCertificate);

            _restClient = new RestClient(
                options: _restClientOptions,
                clientAuthenticationOptions: clientAuthOptions);
        }

        private SslClientAuthenticationOptions CreateClientAuthenticationOptions(X509Certificate2 certificate)
        {
            var clientAuthOptions = new SslClientAuthenticationOptions
            {
                ClientCertificates = new X509CertificateCollection
                {
                    certificate
                }
            };
            return clientAuthOptions;
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
                accessToken:accessToken,
                headers: headers,
                jsonRequestContent: jsonRequestContent,
                clientRequestId: clientRequestId,
                skipUriPathLogging: skipUriPathLogging,
                cancellationToken: cancellationToken);
        }

        public Task CertificateChangedAsync(X509Certificate2 newCertificate)
        {
            // Certificate is changed
            // we have to create new RestClient with new certifiate
            using var monitor = ClientCertificateRestClientCertificateChangedAsync.ToMonitor();

            try
            {
                var oldRestClient = _restClient;

                lock(_updateLock)
                {
                    if (_disposed)
                    {
                        // Already Disposed
                        return Task.CompletedTask;
                    }

                    monitor.OnStart(false);

                    var newClientAuthOptions = CreateClientAuthenticationOptions(newCertificate);
                    var newRestClient = new RestClient(
                        options: _restClientOptions,
                        clientAuthenticationOptions: newClientAuthOptions);

                    Interlocked.Exchange(ref _restClient, newRestClient);

                    // DO NOT log certificate content because it includes private key
                    Logger.LogWarning("{config} is changed", _certificateName);

                    // Dispose old ones
                    oldRestClient.Dispose();
                    monitor.OnCompleted();
                }
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
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
                    var restClient = _restClient;
                    restClient?.Dispose();
                    SecretProviderManager.Instance.RemoveListener(_certificateName, this);
                }
            }
        }
    }
}