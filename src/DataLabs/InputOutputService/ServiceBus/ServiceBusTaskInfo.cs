namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.ServiceBus
{
    using global::Azure.Messaging.ServiceBus;
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;

    [ExcludeFromCodeCoverage]
    internal class ServiceBusTaskInfo : IEventTaskCallBack
    {
        public bool IsCompleted => _taskCompleted == 1;
        public bool IsTaskSuccess => _taskSuccess == 1;
        public bool NeedToMoveDeadLetter => _needToMoveDeadLetter;

        public bool IsTaskCancelled => _isTaskCancelled;
        public bool HasParentTask => false;
        public string TaskCancelledReason => IsTaskCancelled ? SolutionConstants.IsCancelled : null;
        public long PartnerTotalSpentTime { get; set; }

        public string FailedReason { get; private set; }
        public string FailedDescription { get; private set; }
        
        private readonly ServiceBusReceivedMessage _message;

        private int _taskSuccess;
        private int _taskCompleted;

        private volatile bool _needToMoveDeadLetter;
        private volatile bool _isTaskCancelled;

        public ServiceBusTaskInfo(ProcessMessageEventArgs processMessageEventArgs)
        {
            _message = processMessageEventArgs.Message;
        }

        public void TaskStarted(IIOEventTaskContext eventTaskContext, ref TagList tagList)
        {
            tagList.Add(SolutionConstants.ServiceBusSequenceNumber, _message.SequenceNumber);
            tagList.Add(SolutionConstants.ServiceBusMessageId, _message.MessageId);
            tagList.Add(SolutionConstants.DeliveryCount, _message.DeliveryCount);
            tagList.Add(SolutionConstants.LockedUntil, _message.LockedUntil);
            tagList.Add(SolutionConstants.ExpiresAt, _message.ExpiresAt);
        }

        public void TaskCancelCalled(IIOEventTaskContext eventTaskContext)
        {
            _isTaskCancelled = true;
        }

        public void TaskTimeoutCalled(IIOEventTaskContext eventTaskContext)
        {
        }

        public void TaskErrorCalled(IIOEventTaskContext eventTaskContext, Exception ex)
        {
        }

        public void TaskSuccess(IIOEventTaskContext eventTaskContext)
        {
            SetFinalResult(true);
        }

        public void TaskMovedToRetry(IIOEventTaskContext eventTaskContext)
        {
            SetFinalResult(false);
        }

        public void TaskMovedToPoison(IIOEventTaskContext eventTaskContext)
        {
            MoveToDeadLetter(eventTaskContext);
        }

        public void TaskDropped(IIOEventTaskContext eventTaskContext)
        {
            SetFinalResult(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetFinalResult(bool success)
        {
            if(Interlocked.CompareExchange(ref _taskCompleted, 1, 0) != 0)
            {
                // Already finished
                return;
            }

            Interlocked.Exchange(ref _taskSuccess, success ? 1 : 0);
        }

        private void MoveToDeadLetter(IIOEventTaskContext taskContext)
        {
            if (Interlocked.CompareExchange(ref _taskCompleted, 1, 0) != 0)
            {
                // Already finished
                return;
            }

            Interlocked.Exchange(ref _taskSuccess, 0);

            _needToMoveDeadLetter = true;

            FailedReason = taskContext.FailedReason ?? "InternalError";
            FailedDescription = taskContext.FailedDescription;
        }

        public void FinalCleanup()
        {
        }

        internal void TryGetInputProperties(ref TagList tagList)
        {
            if (_message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_CorrrelationId, out var outVal) && outVal != null)
            {
                var correlationId = outVal.ToString();
                tagList.Add(InputOutputConstants.PropertyTag_Input_CorrrelationId, correlationId); // string
            }
            if (_message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_EventType, out outVal) && outVal != null)
            {
                var eventType = outVal.ToString();
                tagList.Add(InputOutputConstants.PropertyTag_Input_EventType, eventType); // string
            }
            if (_message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_Resource_Id, out outVal) && outVal != null)
            {
                var resourceId = outVal.ToString();
                tagList.Add(InputOutputConstants.PropertyTag_Input_Resource_Id, resourceId); // string
            }
            if (_message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_Tenant_Id, out outVal) && outVal != null)
            {
                var tenantId = outVal.ToString();
                tagList.Add(InputOutputConstants.PropertyTag_Input_Tenant_Id, tenantId); // string
            }
            if (_message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_ResourceLocation, out outVal) && outVal != null)
            {
                var resourceLocation = outVal.ToString();
                tagList.Add(InputOutputConstants.PropertyTag_Input_ResourceLocation, resourceLocation); // string
            }
            if (_message.ApplicationProperties.TryGetValue(InputOutputConstants.PropertyTag_Input_EventTime, out outVal) && outVal != null)
            {
                var eventTime = Convert.ToInt64(outVal);
                tagList.Add(InputOutputConstants.PropertyTag_Input_EventTime, eventTime); // long
            }
        }
    }
}