namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerSolutionClient
{
    using global::Grpc.Core;
    using global::Grpc.Net.Client;
    
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Grpc;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerService.V1;
    using static Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerService.V1.PartnerService;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PodHealth;

    [ExcludeFromCodeCoverage]
    public class PartnerSolutionClient : IDisposable
    {
        private static readonly ILogger<PartnerSolutionClient> Logger = DataLabLoggerFactory.CreateLogger<PartnerSolutionClient>();

        private static readonly ActivityMonitorFactory PartnerSolutionClientSendRequestAsync =
            new ActivityMonitorFactory("PartnerSolutionClient.SendRequestAsync");

        public GrpcClientOption GrpcClientOption { get; }

        private readonly GrpcChannel _channel;
        private readonly PartnerServiceClient _client;
        private readonly PodHealthManager _podHealthManager;
        private volatile bool _disposed;

        public PartnerSolutionClient(
            string addr,
            GrpcClientOption grpcClientOption)
        {
            GuardHelper.ArgumentNotNullOrEmpty(addr, nameof(addr));

            grpcClientOption.LBPolicy = GrpcLBPolicy.ROUND_ROBIN;

            GrpcClientOption = grpcClientOption;

            _podHealthManager = new PodHealthManager(
                serviceName: addr,
                denyListConfigKey: SolutionConstants.PartnerDenyList);

            _channel = GrpcUtils.CreateGrpcChannel(
                addr: addr,
                grpcClientOption: grpcClientOption,
                podHealthManager: _podHealthManager);

            _client = new PartnerServiceClient(_channel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddRequestProperties(PartnerRequest solutionRequest, IActivity activity, OpenTelemetryActivityWrapper? taskActivity)
        {
            var inputCorrelationId = solutionRequest.Correlationid;
            var sentSize = solutionRequest.InputData.Length;

            activity[SolutionConstants.PartnerTraceId] = solutionRequest.TraceId;
            activity[SolutionConstants.PartnerRetryCount] = solutionRequest.RetryCount;
            activity[SolutionConstants.PartnerReqSize] = sentSize;
            activity[SolutionConstants.PartnerInputCorrelationId] = inputCorrelationId;
            activity[SolutionConstants.RegionName] = solutionRequest.RegionName;

            if (taskActivity != null)
            {
                taskActivity.SetTag(SolutionConstants.PartnerTraceId, solutionRequest.TraceId);
                taskActivity.SetTag(SolutionConstants.PartnerRetryCount, solutionRequest.RetryCount);
                taskActivity.SetTag(SolutionConstants.PartnerReqSize, sentSize);
                taskActivity.SetTag(SolutionConstants.PartnerInputCorrelationId, inputCorrelationId);
                taskActivity.SetTag(SolutionConstants.RegionName, solutionRequest.RegionName);
            }
        }

        public async Task<PartnerResponse> SendRequestAsync(PartnerRequest solutionRequest, CancellationToken cancellationToken, DateTime? deadline = null, string? scenario = null, string? component = null)
        {
            using var methodMonitor = PartnerSolutionClientSendRequestAsync.ToMonitor(scenario: scenario, component: component);
            var taskActivity = OpenTelemetryActivityWrapper.Current;

            try
            {
                var inputCorrelationId = solutionRequest.Correlationid;
                var hasInputCorrelationId = !string.IsNullOrEmpty(inputCorrelationId);

                AddRequestProperties(solutionRequest, methodMonitor.Activity, taskActivity);
                
                if (hasInputCorrelationId)
                {
                    methodMonitor.Activity.CorrelationId = inputCorrelationId;
                }

                methodMonitor.OnStart(false);

                var reqScenario = methodMonitor.Activity.Scenario;
                if (!string.IsNullOrEmpty(scenario))
                {
                    solutionRequest.ReqAttributes.Add(BasicActivityMonitor.Scenario, reqScenario);
                }

                var response = await _client.ProcessMessageAsync(solutionRequest, deadline: deadline, cancellationToken: cancellationToken).ConfigureAwait(false);

                var partnerRoundTripInMilli = methodMonitor.Activity.Elapsed.TotalMilliseconds;

                methodMonitor.Activity[SolutionConstants.OutputCorrelationId] = response.Correlationid;
                taskActivity?.SetTag(SolutionConstants.OutputCorrelationId, response.Correlationid);

                if (!hasInputCorrelationId && !string.IsNullOrEmpty(response.Correlationid))
                {
                    // Set CorrelationId
                    methodMonitor.Activity.CorrelationId = response.Correlationid;
                }

                long clientSendToServerDoneTime = response.RespEpochtime - solutionRequest.ReqEpochtime;
                if (clientSendToServerDoneTime < 0)
                {
                    clientSendToServerDoneTime = 1;
                }

                long serverDoneToClientReceiveTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - response.RespEpochtime;
                if (serverDoneToClientReceiveTime < 0)
                {
                    serverDoneToClientReceiveTime = 1;
                }

                methodMonitor.Activity[SolutionConstants.ClientSendToServerDoneTime] = clientSendToServerDoneTime;
                methodMonitor.Activity[SolutionConstants.ServerDoneToClientReceiveTime] = serverDoneToClientReceiveTime;
                methodMonitor.Activity[SolutionConstants.PartnerRoundTrip] = partnerRoundTripInMilli;

                if (taskActivity != null)
                {
                    taskActivity.SetTag(SolutionConstants.ClientSendToServerDoneTime, clientSendToServerDoneTime);
                    taskActivity.SetTag(SolutionConstants.ServerDoneToClientReceiveTime, serverDoneToClientReceiveTime);
                    taskActivity.SetTag(SolutionConstants.PartnerRoundTrip, partnerRoundTripInMilli);
                    taskActivity.SetTag(SolutionConstants.PartnerRoundSuccess, true);
                }

                methodMonitor.OnCompleted();
                return response;
            }
            catch (Exception ex)
            {
                var partnerRoundTripInMilli = methodMonitor.Activity.Elapsed.TotalMilliseconds;

                if (taskActivity != null)
                {
                    taskActivity.SetTag(SolutionConstants.PartnerRoundTrip, partnerRoundTripInMilli);
                    taskActivity.SetTag(SolutionConstants.PartnerRoundSuccess, false);
                }

                methodMonitor.OnError(ex);
                throw;
            }
        }

        public AsyncServerStreamingCall<PartnerResponse> SendRequestAndStreamResponseAsync(PartnerRequest solutionRequest, CancellationToken cancellationToken, DateTime? deadline, string? scenario = null, string? component = null)
        {
            using var methodMonitor = PartnerSolutionClientSendRequestAsync.ToMonitor(scenario: scenario, component: component);
            var taskActivity = OpenTelemetryActivityWrapper.Current;

            try
            {
                var inputCorrelationId = solutionRequest.Correlationid;
                var hasInputCorrelationId = !string.IsNullOrEmpty(inputCorrelationId);

                AddRequestProperties(solutionRequest, methodMonitor.Activity, taskActivity);

                methodMonitor.Activity[SolutionConstants.PartnerResponseStream] = true;
                taskActivity?.SetTag(SolutionConstants.PartnerResponseStream, true);

                if (hasInputCorrelationId)
                {
                    methodMonitor.Activity.CorrelationId = inputCorrelationId;
                }

                methodMonitor.OnStart(false);

                var reqScenario = methodMonitor.Activity.Scenario;
                if (!string.IsNullOrEmpty(scenario))
                {
                    solutionRequest.ReqAttributes.Add(BasicActivityMonitor.Scenario, reqScenario);
                }

                var streamCall = _client.ProcessStreamMessages(solutionRequest, deadline: deadline, cancellationToken: cancellationToken);

                methodMonitor.OnCompleted();
                return streamCall;
            }
            catch (Exception ex)
            {
                var partnerRoundTripInMilli = methodMonitor.Activity.Elapsed.TotalMilliseconds;

                if (taskActivity != null)
                {
                    taskActivity.SetTag(SolutionConstants.PartnerRoundTrip, partnerRoundTripInMilli);
                    taskActivity.SetTag(SolutionConstants.PartnerRoundSuccess, false);
                }

                methodMonitor.OnError(ex);
                throw;
            }
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                try
                {
                    _channel?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Dispose Failed. {exception}", ex.ToString());
                }
            }
        }

    }
}
