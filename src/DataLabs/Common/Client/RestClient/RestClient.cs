namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Polly;
    using Polly.Contrib.WaitAndRetry;
    using Polly.Extensions.Http;
    using Microsoft.Extensions.Http;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;

    /*
     * Like HttpClient, below RestClient should be reused
     * Don't create RestClient like HttpClient for each request, it will cause bad performance, socket starvation and resource issue.
     */
    public class RestClient : IRestClient
    {
        private static readonly ActivityMonitorFactory RestClientInternalCallRestApiAsync = new("RestClient.InternalCallRestApiAsync");

        public HttpClient Client { get; }
        public RestClientOptions Options { get; }

        private readonly bool _createdHttpClient;
        private volatile bool _disposed;

        public RestClient(
            RestClientOptions options,
            SslClientAuthenticationOptions? clientAuthenticationOptions = null,
            DelegatingHandler? delegatingHandler = null, 
            HttpClient? httpClient = null)
        {
            Options = options;

            if (httpClient == null)
            {
                // In DataLabs. all clients are long-lived client to connect either Resource Fetcher, Pacific, CAS, ARM
                // According to recommendation from https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines
                // Let's create HttpClient instance and configure it here
                // We still have other constructor to use IHttpClientFactory for some cases

                var socketsHandler = new SocketsHttpHandler
                {
                    PooledConnectionLifetime = Options.PooledConnectionLifetime,
                    PooledConnectionIdleTimeout = Options.PooledConnectionIdleTimeout,
                    ConnectTimeout = Options.ConnectTimeout,
                    MaxConnectionsPerServer = Options.MaxConnectionsPerServer,
                    EnableMultipleHttp2Connections = Options.EnableMultipleHttp2Connections,


                    /*
                     * Keep alive pings are sent when a period of inactivity exceeds the configured KeepAlivePingDelay value. 
                     * The client will close the connection if it doesn't receive any frames within the timeout.
                     * https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.keepalivepingtimeout?view=net-8.0
                     */
                    KeepAlivePingDelay = Options.SocketKeepAlivePingDelay,
                    KeepAlivePingTimeout = Options.SocketKeepAlivePingTimeout,

                    // Sends a keep alive ping for the whole lifetime of the connection.
                    // https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpkeepalivepingpolicy?view=net-8.0
                    KeepAlivePingPolicy = Options.KeepAlivePingPolicy,
                };

                if (clientAuthenticationOptions != null)
                {
                    socketsHandler.SslOptions = clientAuthenticationOptions;
                }

                if (delegatingHandler != null)
                {
                    delegatingHandler.InnerHandler = socketsHandler;
                }

                HttpMessageHandler messageHandler = delegatingHandler == null ? socketsHandler : delegatingHandler;

                if (Options.SameEndPointRetryCount > 0)
                {
                    // Refer to this recommendation
                    // https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-http-call-retries-exponential-backoff-polly
                    // https://github.com/App-vNext/Polly/wiki/Retry-with-jitter 

                    var delay = Backoff.DecorrelatedJitterBackoffV2(medianFirstRetryDelay: TimeSpan.FromSeconds(1), retryCount: Options.SameEndPointRetryCount);
                    var policyBuilder = HttpPolicyExtensions.HandleTransientHttpError();
                    if (Options.AdditionalHttpStatusCodesForRetry?.Count > 0)
                    {
                        policyBuilder = policyBuilder.OrResult(response =>
                            Options.AdditionalHttpStatusCodesForRetry!.Contains((int)response.StatusCode));
                    }

                    var retryWithBackoff = policyBuilder.WaitAndRetryAsync(delay);

                    IAsyncPolicy<HttpResponseMessage> policy = retryWithBackoff;
                    if (Options.RequestTimeoutForRetry != default)
                    {
                        var timeoutForPolicy = Policy.TimeoutAsync(Options.RequestTimeoutForRetry);
                        policy = timeoutForPolicy.WrapAsync(retryWithBackoff);
                    }

                    var policyHttpMessageHandler = new PolicyHttpMessageHandler(policy)
                    {
                        InnerHandler = messageHandler
                    };
                    messageHandler = policyHttpMessageHandler;
                }

                Client = new HttpClient(messageHandler);
                _createdHttpClient = true;
            }
            else
            {
                Client = httpClient;
            }

            // Set User-Agent
            if (!string.IsNullOrWhiteSpace(Options.UserAgent))
            {
                Client.DefaultRequestHeaders.Add("User-Agent", Options.UserAgent);
            }

            // Set HttpVersion
            Client.DefaultRequestVersion = Options.HttpRequestVersion;
            Client.DefaultVersionPolicy = Options.VersionPolicy;
            Client.Timeout = Options.RequestTimeout;
        }

        public async Task<HttpResponseMessage> GetAsync(
            string fullUri,
            IEnumerable<KeyValuePair<string, string>>? headers,
            string? clientRequestId,
            IActivity? activity,
            CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new InvalidOperationException("RestClient is already disposed");
            }

            var uri = new Uri(fullUri, UriKind.RelativeOrAbsolute);

            // We don't log requestUri here because it might have some sensitive data
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Version = Options.HttpRequestVersion;
            request.VersionPolicy = Options.VersionPolicy;

            // client Request Id
            if (string.IsNullOrWhiteSpace(clientRequestId))
            {
                clientRequestId = Guid.NewGuid().ToString();
            }

            // Set ClientRequest Id to clientRequestId and correlation Headers for tracking purpose of this Get request
            request.Headers.TryAddWithoutValidation(CommonHttpHeaders.ClientRequestId, clientRequestId);
            request.Headers.TryAddWithoutValidation(CommonHttpHeaders.CorrelationRequestId, clientRequestId);

            if (activity != null)
            {
                activity[CommonHttpHeaders.ClientRequestId] = clientRequestId;
                activity[CommonHttpHeaders.CorrelationRequestId] = clientRequestId;
            }

            // Let's not log given header value here just in case because it might have some sensitive data
            // Instead, each client (ARMClient, CasClient etc..) can log the headers in the client code because they know which header has sensitive data or not.
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (activity != null)
            {
                activity[SolutionConstants.HttpStatusCode] = (int)response.StatusCode;
                activity[SolutionConstants.HttpVersion] = SolutionUtils.GetHttpVersionString(response.Version);
            }

            return response;
        }

        public async Task<HttpResponseMessage> CallRestApiAsync<TRequest>(
            IEndPointSelector endPointSelector,
            string requestUri,
            HttpMethod httpMethod,
            string? accessToken,
            IEnumerable<KeyValuePair<string, string>>? headers,
            TRequest? jsonRequestContent,
            string? clientRequestId,
            bool skipUriPathLogging,
            CancellationToken cancellationToken)
            where TRequest : class
        {
            if (_disposed)
            {
                throw new InvalidOperationException("RestClient is already disposed");
            }

            var canRetyWithOtherEndpoint = false;
            Uri[]? primaryUris = null;
            Uri[]? backupUris = null;
            int primaryOutIndex = 0;
            int totalEndPoints = 0;

            try
            {
                var endPointUri = endPointSelector.GetEndPoint(out primaryOutIndex);

                // To prevent possible race condition during hotconfig, let's save to local variable
                primaryUris = endPointSelector.GetPrimaryEndPoints;
                backupUris = endPointSelector.GetBackupEndPoints;

                totalEndPoints = primaryUris!.Length + backupUris?.Length ?? 0;

                // PrimaryUris Length comparision is to prevent possible primaryUri hotconfig
                canRetyWithOtherEndpoint = Options?.MaxDifferentEndPointRetryCount > 0 &&
                    primaryUris.Length > primaryOutIndex &&
                    totalEndPoints > 1;

                var fullUri = new Uri(endPointUri, requestUri);

                var response = await InternalCallRestApiAsync(
                    fullUri: fullUri,
                    httpMethod: httpMethod,
                    accessToken: accessToken,
                    headers: headers,
                    jsonRequestContent: jsonRequestContent,
                    clientRequestId: clientRequestId,
                    skipUriPathLogging: skipUriPathLogging,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                var isTransientHttpStatusCode = SolutionUtils.IsTransientHttpStatusCode(response.StatusCode);
                if (!isTransientHttpStatusCode || !canRetyWithOtherEndpoint)
                {
                    return response;
                }
            }
            catch (Exception ex)
            {
                var needRetry = canRetyWithOtherEndpoint && ex is HttpRequestException;
                if (!needRetry)
                {
                    throw;
                }
            }

            int retryUriAddIndex = 0;
            Uri[] urisForRety = new Uri[totalEndPoints-1];

            var numPrimaryUris = primaryUris!.Length;
            for (int i = 1; i <= numPrimaryUris; i++)
            {
                int nextPrimaryIndex = (primaryOutIndex + i) % numPrimaryUris;
                if (nextPrimaryIndex != primaryOutIndex)
                {
                    urisForRety[retryUriAddIndex++] = primaryUris[i];
                }
            }

            if (backupUris?.Length > 0)
            {
                for (int i = 0; i < backupUris.Length; i++)
                {
                    urisForRety[retryUriAddIndex++] = backupUris[i];
                }
            }

            int maxRetryCount = Options!.MaxDifferentEndPointRetryCount;
            if (maxRetryCount > urisForRety.Length)
            {
                maxRetryCount = urisForRety.Length;
            }

            for (int i = 0; i < maxRetryCount-1; i++)
            {
                var nextEndPointUri = urisForRety[i];
                var nexFullUri = new Uri(nextEndPointUri, requestUri);
                try
                {
                    var nextResponse = await InternalCallRestApiAsync(
                        fullUri: nexFullUri,
                        httpMethod: httpMethod,
                        accessToken: accessToken,
                        headers: headers,
                        jsonRequestContent: jsonRequestContent,
                        clientRequestId: clientRequestId,
                        skipUriPathLogging: skipUriPathLogging,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (!SolutionUtils.IsTransientHttpStatusCode(nextResponse.StatusCode))
                    {
                        return nextResponse;
                    }
                }
                catch (HttpRequestException)
                {
                    // HttpRequestException will be retried
                }
            }

            // Last retry
            var lastEndPointUri = urisForRety[urisForRety.Length-1];
            var lastFullUri = new Uri(lastEndPointUri, requestUri);
            return await InternalCallRestApiAsync(
                       fullUri: lastFullUri,
                       httpMethod: httpMethod,
                       accessToken: accessToken,
                       headers: headers,
                       jsonRequestContent: jsonRequestContent,
                       clientRequestId: clientRequestId,
                       skipUriPathLogging: skipUriPathLogging,
                       cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private async Task<HttpResponseMessage> InternalCallRestApiAsync<TRequest>(
            Uri fullUri,
            HttpMethod httpMethod,
            string? accessToken,
            IEnumerable<KeyValuePair<string, string>>? headers,
            TRequest? jsonRequestContent,
            string? clientRequestId,
            bool skipUriPathLogging,
            CancellationToken cancellationToken)
            where TRequest : class
        {
            if (_disposed)
            {
                throw new InvalidOperationException("RestClient is already disposed");
            }

            using var monitor = RestClientInternalCallRestApiAsync.ToMonitor();

            try
            {
                GuardHelper.ArgumentNotNull(fullUri);
                GuardHelper.ArgumentNotNull(httpMethod, nameof(httpMethod));

                monitor.OnStart(false);

                if (skipUriPathLogging)
                {
                    monitor.Activity[SolutionConstants.Endpoint] = fullUri.Authority;
                }
                else
                {
                    monitor.Activity[SolutionConstants.RequestURI] = fullUri.ToString();
                }
                

                using var request = new HttpRequestMessage(httpMethod, fullUri);
                request.Version = Options.HttpRequestVersion;
                request.VersionPolicy = Options.VersionPolicy;
                
                // client Request Id
                if (string.IsNullOrWhiteSpace(clientRequestId))
                {
                    clientRequestId = Guid.NewGuid().ToString();
                }
                request.Headers.TryAddWithoutValidation(CommonHttpHeaders.ClientRequestId, clientRequestId);
                request.Headers.TryAddWithoutValidation(CommonHttpHeaders.CorrelationRequestId, clientRequestId);

                monitor.Activity[CommonHttpHeaders.ClientRequestId] = clientRequestId;
                monitor.Activity[CommonHttpHeaders.CorrelationRequestId] = clientRequestId;

                // Let's not log given header value here just in case because it might have some sensitive data
                // Instead, each client (ARMClient, CasClient etc..) can log the headers in the client code because they know which header has sensitive data or not.
                if (headers != null)
                {
                    foreach (KeyValuePair<string, string> header in headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                // Let's not log accessToken
                if (accessToken != null)
                {
                    request.WithBearerAuthorization(accessToken);
                }

                if (jsonRequestContent != null)
                {
                    request.WithJsonContent(jsonRequestContent);
                }

                var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                
                monitor.Activity[SolutionConstants.HttpStatusCode] = (int)response.StatusCode;
                monitor.Activity[SolutionConstants.HttpVersion] = SolutionUtils.GetHttpVersionString(response.Version);

                // Notice
                // Don't call here response.EnsureSuccessStatusCodeAsync()
                // How to interpret response is up to caller
                // Caller can call response.EnsureSuccessStatusCodeAsync in their code
                // RestClient should not throw unnecessary exception based on the response code here.

                monitor.OnCompleted();
                return response;
            }
            catch (HttpRequestErrorException ex)
            {
                monitor.Activity[SolutionConstants.ResponseBody] = ex.ResponseBody;
                monitor.OnError(ex);
                throw;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                throw;
            }
        }

        public void Dispose()
        {
            if (_createdHttpClient && !_disposed)
            {
                _disposed = true;
                Client.Dispose();
            }
        }

        public static string CreateUserAgent(string clientName)
        {
            // ARG format
            //AzureResourceGraph.PartialSyncIngestionService.global/1.24.1.39

            // AzureResourceBuilder.CasClient.westus3.abcsolution/2023.11.04.04
            // AzureResourceBuilder.PacificClient.westus3.idmapping/2023.11.04.04
            // AzureResourceBuilder.ArmClient.westus3.idmapping/2023.11.04.04
            // AzureResourceBuilder.ArmAdminClient.westus3.skusolution/2023.11.04.04
            return "AzureResourceBuilder" +
                '.' + clientName +
                '.' + MonitoringConstants.REGION +
                '.' + MonitoringConstants.SCALE_UNIT +
                '/' + MonitoringConstants.BUILD_VERSION;
        }

        public static SslClientAuthenticationOptions CreateClientAuthenticationOptions(X509Certificate2 certificate, bool skipCertificateValidation)
        {
            var clientAuthOptions = new SslClientAuthenticationOptions
            {
                ClientCertificates = new X509CertificateCollection
                {
                    certificate
                }
            };

            if (skipCertificateValidation)
            {
                // This should be used only for test dsts certificate
                clientAuthOptions.RemoteCertificateValidationCallback = (message, cert, chain, errors) => true;
            }

            return clientAuthOptions;
        }
    }
}