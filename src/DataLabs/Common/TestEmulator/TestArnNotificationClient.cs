namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TestEmulator
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArnPublishClient.Interfaces;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Clients.Contracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Notifications.Contracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Notifications.Contracts.Data;

    public class TestArnNotificationClient : IArnNotificationClient
    {
        public bool IsInitialized => true;
        public bool IsBackupClientInitialized => true;

        public bool ReturnException;

        public int NumPublishToArnCalls;
        public int NumExceptionCalls;
        public int NumPublishSuccess;

        public Task PublishToArn(
            IList<ResourceOperationBase> resourceOperations, 
            DataBoundary? dataBoundary, 
            FieldOverrides fieldOverrides, 
            bool publishToPairedRegionClient, 
            CancellationToken cancellationToken,
            AdditionalGroupingProperties additionalGroupingProperties = AdditionalGroupingProperties.None)
        {
            lock(this)
            {
                NumPublishToArnCalls++;

                if (ReturnException)
                {
                    NumExceptionCalls++;
                    throw new Exception("TestArnNotificationClient.PublishToArn exception");
                }

                NumPublishSuccess++;
                return Task.CompletedTask;
            }
        }

        public Task<StorageRemovalResult?> RemoveExpiredBlobContainers(CancellationToken cancellationToken)
        {
            return Task.FromResult((StorageRemovalResult?)null);
        }

        public void Clear()
        {
            ReturnException = false;
            NumExceptionCalls = 0;
            NumPublishToArnCalls = 0;
            NumPublishSuccess = 0;
        }
    }
}
