namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Controller
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.AdminService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceBusManagement;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.Configuration;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Services;

    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route($"{AdminConstants.BaseIOServiceRoute}/[action]")]
    public class AdminServiceBusController : ControllerBase
    {
        #region Tracing
        private static readonly ILogger<AdminServiceBusController> Logger = DataLabLoggerFactory.CreateLogger<AdminServiceBusController>();
        private static readonly ActivityMonitorFactory AdminServiceBusControllerDeleteAndRecreateServiceBusQueue =
            new ("AdminServiceBusController.DeleteAndRecreateServiceBusQueue");
        private static readonly ActivityMonitorFactory AdminServiceBusControllerDeleteDeadLetterMessages =
            new ("AdminServiceBusController.DeleteDeadLetterMessages");
        private static readonly ActivityMonitorFactory AdminServiceBusControllerReplayDeadLetterMessages =
            new ("AdminServiceBusController.ReplayDeadLetterMessages");
        
        #endregion

        #region Fields

        private List<ServiceBusAdminManager> _serviceBusAdminManagerList = SolutionInputOutputService.ServiceBusAdminManagers;

        #endregion

        #region Constants

        private int _delayBeforeRecreateQueueInMilliseconds;
        private int _queueOperationTimeOut;

        #endregion

        // For UT only
        public AdminServiceBusController(IConfigurationWithCallBack configuration, List<ServiceBusAdminManager> serviceBusAdminManagerList) : this(
            configuration)
        {
            _serviceBusAdminManagerList = serviceBusAdminManagerList;
        }

        public AdminServiceBusController(IConfigurationWithCallBack configuration)
        {
            _delayBeforeRecreateQueueInMilliseconds = configuration.GetValueWithCallBack<int>(
                InputOutputConstants.DelayBeforeRecreateQueueInMilliseconds, UpdateDelayBeforeRecreateQueueInMilliseconds, 5000);

            _queueOperationTimeOut = configuration.GetValueWithCallBack<int>(
                InputOutputConstants.QueueOperationTimeOut, UpdateQueueOperationTimeOut, 60*2);
        }

        [HttpPatch]
        [ActionName(AdminConstants.DeleteAndRecreateServiceBusQueue)]
        public async Task<string> DeleteAndRecreateServiceBusQueue(
            [FromQuery(Name = AdminConstants.QueueName)] string queueName)
        {
            using var monitor = AdminServiceBusControllerDeleteAndRecreateServiceBusQueue.ToMonitor();
            try
            {
                monitor.OnStart();

                var serviceBusAdminManager = _serviceBusAdminManagerList.SingleOrDefault(sb => sb.RetryQueueName == queueName);
                monitor.Activity["Found Service bus manager with retry queue name:"] = serviceBusAdminManager.RetryQueueName;

                using var cancellationSource = new CancellationTokenSource();
                cancellationSource.CancelAfter(TimeSpan.FromSeconds(_queueOperationTimeOut));
                var cancellationToken = cancellationSource.Token;

                await serviceBusAdminManager.DeleteIfExistsAsync(queueName, cancellationToken).ConfigureAwait(false);
                await Task.Delay(_delayBeforeRecreateQueueInMilliseconds).ConfigureAwait(false);
                await serviceBusAdminManager.CreateRetryQueueWriter(queueName, cancellationToken).ConfigureAwait(false);

                monitor.OnCompleted();

                return $"Service Bus Queue {queueName} was successfully deleted and recreated.";
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                return ex.Message;
            }
        }

        [HttpPatch]
        [ActionName(AdminConstants.DeleteDeadLetterMessages)]
        public string DeleteDeadLetterMessages(
            [FromQuery(Name = AdminConstants.QueueName)] string queueName,
            [FromQuery(Name = AdminConstants.DeleteLookBackHours)] int deleteLookBackHours)
        {
            using var monitor = AdminServiceBusControllerDeleteDeadLetterMessages.ToMonitor();
            try
            {
                monitor.OnStart();

                var serviceBusAdminManager = _serviceBusAdminManagerList.SingleOrDefault(sb => sb.RetryQueueName == queueName);
                monitor.Activity["Found Service bus manager with retry queue name:"] = serviceBusAdminManager.RetryQueueName;

                TaskCompletionSource<bool> taskStartedSignal = new TaskCompletionSource<bool>();
                var task = serviceBusAdminManager.DeleteDeadLetterMessagesAsync(queueName, deleteLookBackHours, taskStartedSignal).ConfigureAwait(false);

                if (!taskStartedSignal.Task.Result)
                {
                    throw new Exception("Failed to start task to delete dead letter messages");
                }

                monitor.OnCompleted();

                return $"Task for deleting dead letter messages older than {deleteLookBackHours} hours ago has started.";
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                return ex.Message;
            }
        }

        [HttpPatch]
        [ActionName(AdminConstants.ReplayDeadLetterMessages)]
        public string ReplayDeadLetterMessages(
            [FromQuery(Name = AdminConstants.QueueName)] string queueName,
            [FromQuery(Name = AdminConstants.ReplayLookBackHours)] int replayLookBackHours,
            [FromQuery(Name = AdminConstants.UtcNowFileTime)] long UtcNowFileTime,
            [FromQuery(Name = AdminConstants.NeedDelete)] bool needDelete=false,
            [FromQuery(Name = AdminConstants.DeleteLookBackHours)] int deleteLookBackHours=48)
        {
            using var monitor = AdminServiceBusControllerReplayDeadLetterMessages.ToMonitor();
            try
            {
                monitor.OnStart();

                var serviceBusAdminManager = _serviceBusAdminManagerList.SingleOrDefault(sb => sb.RetryQueueName == queueName);
                monitor.Activity["Found Service bus manager with retry queue name:"] = serviceBusAdminManager.RetryQueueName;
                
                TaskCompletionSource<bool> taskStartedSignal = new TaskCompletionSource<bool>();
                var task = serviceBusAdminManager.ReplayDeadLetterMessagesAsync(queueName, replayLookBackHours,
                    UtcNowFileTime, taskStartedSignal, needDelete, deleteLookBackHours).ConfigureAwait(false);

                if (!taskStartedSignal.Task.Result)
                {
                    throw new Exception("Failed to start task to replay/delete dead letter messages");
                }

                monitor.OnCompleted();

                var response = $"Task for replaying dead letter messages in the past {replayLookBackHours} hours has started.";
                if (needDelete){
                    response += $" Task for deleting dead letter messages older than {deleteLookBackHours} hours ago has started.";
                }
                
                return response;
            }
            catch (Exception ex)
            {
                monitor.OnError(ex);
                return ex.Message;
            }
        }

        private Task UpdateDelayBeforeRecreateQueueInMilliseconds(int newValue)
        {
            if (newValue < 0)
            {
                Logger.LogError("{config} must be equal or larger than 0", _delayBeforeRecreateQueueInMilliseconds);
                return Task.CompletedTask;
            }

            var oldValue = _delayBeforeRecreateQueueInMilliseconds;
            if (oldValue != newValue)
            {
                if (Interlocked.CompareExchange(ref _delayBeforeRecreateQueueInMilliseconds, newValue, oldValue) == oldValue)
                {
                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}", _delayBeforeRecreateQueueInMilliseconds, oldValue, newValue);
                }
            }

            return Task.CompletedTask;
        }

        private Task UpdateQueueOperationTimeOut(int newValue)
        {
            if (newValue < 0)
            {
                Logger.LogError("{config} must be equal or larger than 0", _queueOperationTimeOut);
                return Task.CompletedTask;
            }

            var oldValue = _queueOperationTimeOut;
            if (oldValue != newValue)
            {
                if (Interlocked.CompareExchange(ref _queueOperationTimeOut, newValue, oldValue) == oldValue)
                {
                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}", _queueOperationTimeOut, oldValue, newValue);
                }
            }

            return Task.CompletedTask;
        }
    }
}