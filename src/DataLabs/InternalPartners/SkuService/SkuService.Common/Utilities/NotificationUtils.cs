namespace SkuService.Main
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Partner.DataLabsInterface;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using System;
    using System.Collections.Generic;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring.Constants;
    using SkuService.Common.Utilities;
    using SkuService.Common;
    using System.Threading;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using System.Runtime.CompilerServices;

    public static class NotificationUtils
    {
        public static DataLabsARNV3Response BuildDataLabsOutputResponse(IList<NotificationResourceDataV3<GenericResource>> notifications, DataLabsARNV3Request request, string eventType)
        {
            var notificationData = new NotificationDataV3<GenericResource>(publisherInfo: Constants.PublisherInfo, resources: notifications, resourceLocation: MonitoringConstants.REGION);
            var egNotification = new EventGridNotification<NotificationDataV3<GenericResource>>(
                id: notifications?.FirstOrDefault()?.ResourceId,
                topic: Constants.Topic,
                subject: notifications?.FirstOrDefault()?.ResourceId,
                eventType: eventType,
                eventTime: DateTimeOffset.UtcNow,
                data: notificationData);
            var successResponse = new DataLabsARNV3SuccessResponse(egNotification, DateTimeOffset.UtcNow, null);
            return new DataLabsARNV3Response(DateTimeOffset.UtcNow, request.CorrelationId, successResponse, null, null);
        }


        public static DataLabsARNV3Response BuildDataLabsErrorResponse(DataLabsARNV3Request request, Exception exception)
        {
            if (exception is SkuPartnerException dlException)
            {
                var dlErrorResponse = new DataLabsErrorResponse(DataLabsErrorType.RETRY, request.RetryCount * 5000, dlException.ErrorResponse.HttpStatusCode.ToString(), dlException.ErrorResponse.ErrorDescription, dlException.ErrorResponse.FailedComponent.ToString());
                return new DataLabsARNV3Response(DateTimeOffset.UtcNow, request.CorrelationId, null, dlErrorResponse, null);
            }

            var errorResponse = new DataLabsErrorResponse(DataLabsErrorType.POISON, request.RetryCount * 5000, "Unknown", exception.Message, "NotSpecified");
            return new DataLabsARNV3Response(DateTimeOffset.UtcNow, request.CorrelationId, null, errorResponse, null);
        }

        public static async IAsyncEnumerable<DataLabsARNV3Response> GenerateArnOutputResponseAsync(DataLabsARNV3Request request, IAsyncEnumerator<List<NotificationResourceDataV3<GenericResource>>> enumerator, string action, IActivityMonitor monitor, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            DataLabsARNV3Response? errorResponse;
            List<NotificationResourceDataV3<GenericResource>> output = null!;
            while (true)
            {
                try
                {
                    if (!await enumerator.MoveNextAsync(cancellationToken))
                    {
                        break;
                    }

                    output = enumerator.Current;
                    errorResponse = null;
                }
                catch (Exception e)
                {
                    monitor.OnError(e);
                    errorResponse = BuildDataLabsErrorResponse(request, e);
                }

                if (errorResponse != null)
                {
                    yield return errorResponse;
                }
                else
                {
                    yield return BuildDataLabsOutputResponse(output, request, $"{Constants.SubscriptionSkuResourceType}/{action}");
                }
            }
        }
    }
}
