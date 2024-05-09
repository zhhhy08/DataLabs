namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ArnPublishClient.Interfaces
{
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Clients.Contracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Notifications.Contracts;
    using Microsoft.WindowsAzure.Governance.NotificationsClient.Notifications.Contracts.Data;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IArnNotificationClient
    {
        Task PublishToArn(
            IList<ResourceOperationBase> resourceOperations,
            DataBoundary? dataBoundary,
            FieldOverrides fieldOverrides,
            bool publishToPairedRegionClient,
            CancellationToken cancellationToken,
            AdditionalGroupingProperties additionalGroupingProperties);

        Task<StorageRemovalResult?> RemoveExpiredBlobContainers(
            CancellationToken cancellationToken);

        bool IsInitialized { get; }

        bool IsBackupClientInitialized { get; }

    }
}
