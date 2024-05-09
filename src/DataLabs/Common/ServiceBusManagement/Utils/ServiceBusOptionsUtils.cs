namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceBusManagement
{
    using global::Azure.Messaging.ServiceBus.Administration;
    using global::Azure.Messaging.ServiceBus;
    using System;

    public static class ServiceBusOptionsUtils
    {
        public static ServiceBusClientOptions CreateServiceBusClientOptions(int maxRetry, int delayInMsec, int maxDelayInSec, int timeoutPerAttempInSec)
        {
            return new ServiceBusClientOptions
            {
                RetryOptions = new ServiceBusRetryOptions
                {
                    MaxRetries = maxRetry,
                    Delay = TimeSpan.FromMilliseconds(delayInMsec),
                    MaxDelay = TimeSpan.FromSeconds(maxDelayInSec),
                    TryTimeout = TimeSpan.FromSeconds(timeoutPerAttempInSec),
                    Mode = ServiceBusRetryMode.Exponential
                }
            };
        }

        public static ServiceBusProcessorOptions CreateServiceBusProcessorOptions(
            TimeSpan maxAutoRenewDuration,
            int concurrency,
            int prefetchCount)
        {
            return new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxAutoLockRenewalDuration = maxAutoRenewDuration,
                MaxConcurrentCalls = concurrency,
                PrefetchCount = prefetchCount,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            };
        }

        public static CreateQueueOptions CreateQueueOptions(string queueName, 
            int maxDeliveryCount = 10,
            int lockDurationInSec = 60,
            bool enableBatchedOperations = true,
            bool deadLetteringOnMessageExpiration = true,
            int ttlInDays = 7,
            long maxSizeInMegabytes = 81920)
        {
            /*
             * TODO
             * Check With Pooja, maxDeliveryCount 100??
             * 
                MaxDeliveryCount = 100,
                EnableBatchedOperations = true,
                LockDuration = TimeSpan.FromSeconds(60),
                DeadLetteringOnMessageExpiration = true,
                DefaultMessageTimeToLive = TimeSpan.FromDays(7),
                MaxSizeInMegabytes = 81920 //= 80Gb - max allowed size for all messages in queue. All add operations will be failing after it
            */

            return new CreateQueueOptions(queueName)
            {
                MaxDeliveryCount = maxDeliveryCount,
                EnableBatchedOperations = enableBatchedOperations,
                LockDuration = TimeSpan.FromSeconds(lockDurationInSec),
                DeadLetteringOnMessageExpiration = deadLetteringOnMessageExpiration,
                DefaultMessageTimeToLive = TimeSpan.FromDays(ttlInDays),
                MaxSizeInMegabytes = maxSizeInMegabytes
            };
        }

        /*
        private static ServiceBusSettings GetServiceBusSettings()
        {
            var getTimeout = TimeSpan.Parse(ServiceConfiguration.Current.Get(
                ConfigurationConstants.CommonSection,
                ConfigurationConstants.ServiceBusGetTimeout));
            var generalTimeout = TimeSpan.Parse(ServiceConfiguration.Current.Get(
                ConfigurationConstants.CommonSection,
                ConfigurationConstants.ServiceBusGeneralTimeout));
            var sdkPrefetchCount = ServiceConfiguration.Current.Get<int>(
                ConfigurationConstants.CommonSection,
                ConfigurationConstants.ServiceBusSdkPrefetchCount);
            var lenCachingInSec = ServiceConfiguration.Current.Get<int>(
                ConfigurationConstants.CommonSection,
                ConfigurationConstants.ServiceBusLenCachingInSeconds);
            var shallowSdkPrefetchMode = ServiceConfiguration.Current.Get<bool>(
                ConfigurationConstants.CommonSection,
                ConfigurationConstants.ServiceBusShallowSdkPrefetchMode);
            var returnedMessagesCounterLookBackInMinutes = ServiceConfiguration.Current.Get<int>(
                ConfigurationConstants.CommonSection,
                ConfigurationConstants.ServiceBusReturnedMessagesCounterLookBackInMinutes);
            var returnedMessagesCounterPercentage = ServiceConfiguration.Current.Get<int>(
                ConfigurationConstants.CommonSection,
                ConfigurationConstants.ServiceBusReturnedMessagesCounterPercentage);
            var clientPrefetchEnabled = ServiceConfiguration.Current.Get<bool>(
                ConfigurationConstants.CommonSection,
                ConfigurationConstants.ServiceBusClientPrefetchEnabled);

            return new ServiceBusSettings(getTimeout, generalTimeout, sdkPrefetchCount, lenCachingInSec, shallowSdkPrefetchMode,
                returnedMessagesCounterLookBackInMinutes: returnedMessagesCounterLookBackInMinutes, returnedMessagesCounterPercentage: returnedMessagesCounterPercentage,
                clientPrefetchEnabled: clientPrefetchEnabled);
        }
        */
        }
    }