// <copyright file="PartnerBlobClient.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerBlobClient
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Client.RestClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public class PartnerBlobClient : IPartnerBlobClient
    {
        private static readonly ActivityMonitorFactory PartnerBlobClientGetResourcesAsync =
            new ActivityMonitorFactory("PartnerBlobClient.GetResourcesAsync");

        public static readonly string UserAgent = RestClient.CreateUserAgent("PartnerBlobClient");

        private const string PartnerBlobDefaultTimeOutString = "5/20";

        private readonly RestClient _restClient;
        private readonly PartnerBlobClientOptions _clientOptions;
        private readonly TimeOutConfigInfo _timeOutConfigInfo;

        public PartnerBlobClient(IConfiguration configuration)
        {
            _clientOptions = new PartnerBlobClientOptions(configuration);
            _restClient = new RestClient(options: _clientOptions);
            _timeOutConfigInfo = new TimeOutConfigInfo(SolutionConstants.PartnerBlobCallMaxTimeOutInSec, PartnerBlobDefaultTimeOutString, configuration);
        }

        public async Task<List<TResponse>> GetResourcesAsync<TResponse>(string uri, int retryFlowCount, CancellationToken cancellationToken) where TResponse : class
        {
            using var monitor = PartnerBlobClientGetResourcesAsync.ToMonitor();

            Exception? serializationException = null;

            try
            {
                var timeOut = _timeOutConfigInfo.GetTimeOut(retryFlowCount);
                monitor.Activity[SolutionConstants.TimeOutValue] = timeOut;

                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tokenSource.CancelAfter(timeOut);
                cancellationToken = tokenSource.Token;

                monitor.OnStart(false);

                monitor.Activity[SolutionConstants.PartnerBlobURI] = SolutionLoggingUtils.HideSigFromBlobUri(uri);

                using HttpResponseMessage response = await _restClient.GetAsync(
                    fullUri: uri,
                    headers: null,
                    clientRequestId: null,
                    activity: monitor.Activity,
                    cancellationToken).ConfigureAwait(false);

                response.EnsureSuccessStatusCode(); // this will throw exception

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    var result = SerializationHelper.ReadListFromStream<TResponse>(stream);
                    monitor.OnCompleted();
                    return result;
                }
                catch(Exception ex)
                {
                    serializationException = ex;
                }
            }
            catch (Exception ex)
            {
                if (ex is HttpRequestErrorException httpEx)
                {
                    monitor.Activity[SolutionConstants.ResponseBody] = httpEx.ResponseBody;
                }

                if (ex is OperationCanceledException && ex.InnerException is TimeoutException)
                {
                    ex = ex.InnerException;
                }

                monitor.OnError(ex);
                throw;
            }

            monitor.OnError(serializationException);
            throw new SerializationException(serializationException.Message, serializationException);
        }
    }
}
