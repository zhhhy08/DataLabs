namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.RegionConfig;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.OpenTelemetry;

    public class CacheTaskContext : AbstractEventTaskContext<CacheTaskContext>
    {
        public enum CacheCommand
        {
            SET,
            DELETE
        }

        public override CancellationToken TaskCancellationToken { get; }
        public override CacheTaskContext TaskContext => this;
        public override string Scenario { get; set; }
        public override EventTaskFinalStage EventFinalStage { get; set; }

        public string CorrelationId { get; }
        public string TenantId { get; }
        public string ResourceId { get; }
        public string ResourceType { get; }
        public BinaryData Data { get; }
        public long TimeStamp { get; }
        public string ETag { get; }
        public CacheCommand Command { get; }

        // Region data related, specifically logging in Common.Core
        public RegionConfig RegionConfigData { get; private set; }

        public CacheTaskContext(
            string tenantId, 
            string resourceId, 
            string resourceType,
            string correlationId, 
            BinaryData data, 
            CacheCommand cacheCommand,
            long timeStamp, 
            string etag, 
            int retryFlowCount,
            ActivityContext parentActivityContext,
            DateTimeOffset topActivityStartTime,
            CancellationToken taskCancellationToken,
            RegionConfig regionConfigData) :
            base(IOServiceOpenTelemetry.IOActivitySource, "CacheTaskContext", parentActivityContext, false, retryFlowCount, topActivityStartTime)
        {
            TenantId = tenantId;
            ResourceId = resourceId;
            if (string.IsNullOrWhiteSpace(resourceType)) {
                resourceType = ArmUtils.GetResourceType(resourceId);
            }
            ResourceType = resourceType;
            CorrelationId = correlationId;
            Data = data;
            Command = cacheCommand;
            TimeStamp = timeStamp;
            ETag = etag;
            TaskCancellationToken = taskCancellationToken;
            RegionConfigData = regionConfigData;

            EventTaskActivity.SetTag(SolutionConstants.TenantId, TenantId);
            EventTaskActivity.SetTag(SolutionConstants.InputResourceId, ResourceId);
            EventTaskActivity.SetTag(SolutionConstants.ResourceType, ResourceType);
            EventTaskActivity.SetTag(SolutionConstants.CorrelationId, CorrelationId);
            EventTaskActivity.SetTag(SolutionConstants.TimeStamp, timeStamp);
            EventTaskActivity.SetTag(SolutionConstants.ETag, etag);
            EventTaskActivity.SetTag(SolutionConstants.RetryCount, retryFlowCount);
            EventTaskActivity.SetTag(SolutionConstants.RegionName, regionConfigData.RegionLocationName);
        }

        public override void TaskMovingToPoison(string poisonReason, string reasonDetails, string component, Exception ex)
        {
         
        }

        public override void TaskMovingToRetry(string retryReason, string reasonDetails, int retryDelayMs, string component, Exception ex)
        {
        }

        protected override void Dispose(bool disposing)
        {
        }

        public override void CancelTask()
        {
        }

        public override bool IsAlreadyTaskCancelled()
        {
            return false;
        }
    }
}
