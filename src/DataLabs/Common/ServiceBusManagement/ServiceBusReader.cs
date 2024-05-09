namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceBusManagement
{
    using global::Azure.Messaging.ServiceBus;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    
    [ExcludeFromCodeCoverage]
    public class ServiceBusReader : IDisposable
    {
        private static readonly ILogger<ServiceBusReader> Logger =
            DataLabLoggerFactory.CreateLogger<ServiceBusReader>();

        private ServiceBusProcessor _processor;
        private IServiceBusTaskManager _serviceBusTaskManager;

        private bool _disposed;

        public ServiceBusReader(ServiceBusProcessor processor, 
            IServiceBusTaskManager serviceBusTaskManager)
        {
            _processor = processor;
            _serviceBusTaskManager = serviceBusTaskManager;
        }

        public void UpdateConcurrency(int maxConcurrentCalls)
        {
            _processor.UpdateConcurrency(maxConcurrentCalls);
        }

        public void UpdatePrefetchCount(int prefetchCount)
        {
            _processor.UpdatePrefetchCount(prefetchCount);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _processor.ProcessMessageAsync += ProcessEventHandler;
                _processor.ProcessErrorAsync += ProcessErrorHandler;

                await _processor.StartProcessingAsync(cancellationToken).ConfigureAwait(false);

            }
            catch(Exception ex)
            {
                Logger.LogCritical(ex, "StartProcessingAsync got Exception. {exception}", ex.ToString());
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _processor.StopProcessingAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "StopProcessingAsync got Exception. {exception}", ex.ToString());
            }
            finally
            {
                _processor.ProcessMessageAsync -= ProcessEventHandler;
                _processor.ProcessErrorAsync -= ProcessErrorHandler;

                Dispose();
            }
            
        }

        private async Task ProcessEventHandler(ProcessMessageEventArgs args)
        {
            await _serviceBusTaskManager.ProcessMessageAsync(args).ConfigureAwait(false);
        }

        private Task ProcessErrorHandler(ProcessErrorEventArgs args)
        {
            try
            {
                Logger.LogError("ProcessErrorHandler. ServiceBusPath: {entityPath}, " +
                    "FullyQualifiedNamespace: {Namespace}, " +
                    "ErrorSource: {ErrorSource}, " +
                    "Exception: {Exception}",
                    _processor.EntityPath,
                    _processor.FullyQualifiedNamespace,
                    args.ErrorSource,
                    args.Exception);
            }
            catch (Exception ex)
            {
                // It is very important that you always guard against
                // exceptions in your handler code; the processor does
                // not have enough understanding of your code to
                // determine the correct action to take.  Any
                // exceptions from your handlers go uncaught by
                // the processor and will NOT be handled in any
                // way.
                Logger.LogCritical(ex, "ProcessErrorHandler got Exception. {exception}", ex.ToString());
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _processor.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }
}
