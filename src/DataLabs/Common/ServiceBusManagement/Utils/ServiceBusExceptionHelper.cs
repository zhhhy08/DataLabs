namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceBusManagement
{
    using global::Azure.Messaging.ServiceBus;
    using System;
    using System.Net;

    public class ServiceBusExceptionHelper
    {
        public static bool IsAzureServiceBusEntityNotFound(Exception ex)
        {
            var serviceBusException = ex as ServiceBusException ?? ex?.InnerException as ServiceBusException;
            return serviceBusException != null && serviceBusException.Reason == ServiceBusFailureReason.MessagingEntityNotFound;
        }

        public static bool IsAzureServiceBusEntityAlreadyExists(Exception ex)
        {
            var serviceBusException = ex as ServiceBusException ?? ex?.InnerException as ServiceBusException;
            return serviceBusException != null && serviceBusException.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists;
        }

        public static bool IsAzureServiceBusMessageLockLost(Exception ex)
        {
            var serviceBusException = ex as ServiceBusException ?? ex?.InnerException as ServiceBusException;
            return serviceBusException != null && serviceBusException.Reason == ServiceBusFailureReason.MessageLockLost;
        }
    }
}
