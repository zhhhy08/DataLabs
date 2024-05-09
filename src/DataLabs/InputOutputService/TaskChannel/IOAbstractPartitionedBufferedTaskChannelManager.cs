namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.TaskChannel
{
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.TaskChannel;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts;

    public abstract class IOAbstractPartitionedBufferedTaskChannelManager<TInput> : AbstractPartitionedBufferedTaskChannelManager<IOEventTaskContext<TInput>> where TInput : IInputMessage
    {
        private static readonly ILogger<IOAbstractPartitionedBufferedTaskChannelManager<TInput>> Logger =
            DataLabLoggerFactory.CreateLogger<IOAbstractPartitionedBufferedTaskChannelManager<TInput>>();

        private IOTaskChannelPartitionKey _partitionKey = IOTaskChannelPartitionKey.NONE;

        private readonly string _configKey;
        private readonly object _lock = new object();

        public IOAbstractPartitionedBufferedTaskChannelManager(string channelName, string contextTypeName, int initMaxBoundSize = 1000, int intNumQueue = 5, int initDelayInMilli = 0, int initXaxBufferedSize = 500) : 
            base(channelName: channelName, contextTypeName: contextTypeName, initMaxBoundSize: initMaxBoundSize, intNumQueue: intNumQueue, initDelayInMilli: initDelayInMilli, initXaxBufferedSize: initXaxBufferedSize)
        {
            _configKey = channelName + InputOutputConstants.BufferedChannelPartitionKeySuffix;
            var partitionKeyStr = ConfigMapUtil.Configuration.GetValueWithCallBack<string>(
                _configKey,
                UpdatePartitionKey, IOTaskChannelPartitionKey.NONE.FastEnumToString(), allowMultiCallBacks: true);

            if (!string.IsNullOrWhiteSpace(partitionKeyStr))
            {
                // If given string is not compatible with enum, throw exception
                _partitionKey = StringEnumCache.GetEnumIgnoreCase<IOTaskChannelPartitionKey>(partitionKeyStr);
            }
        }

        protected override long GetConsumerPartitionId(AbstractEventTaskContext<IOEventTaskContext<TInput>> eventTaskContext)
        {
            if (_partitionKey != IOTaskChannelPartitionKey.NONE)
            {
                var ioTaskContext = eventTaskContext.TaskContext;
                var outputMessage = ioTaskContext.OutputMessage;

                if (outputMessage != null && outputMessage.ResourceId != null)
                {
                    var outputResourceId = outputMessage.ResourceId;
                    var tenantId = outputMessage.TenantId;

                    string key = null;
                    switch (_partitionKey)
                    {
                        case IOTaskChannelPartitionKey.SUBSCRIPTIONID:
                            key = outputResourceId.GetSubscriptionIdOrNull();
                            break;
                        case IOTaskChannelPartitionKey.TENANTID:
                            key = tenantId;
                            break;
                        case IOTaskChannelPartitionKey.SCOPEID:
                            key = outputResourceId.GetSubscriptionIdOrNull() ?? tenantId;
                            // TODO, do we need to consider management group here??
                            break;
                        default:
                            break;
                    }

                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        var bytes = Encoding.UTF8.GetBytes(key);
                        return HashUtils.Murmur32(bytes);
                    }
                }
            }

            // -1 or 0: round robin distribution to all consumers
            return -1;
        }

        private Task UpdatePartitionKey(string newVal)
        {
            if (string.IsNullOrWhiteSpace(newVal))
            {
                Logger.LogError("{config} must be non empty", _configKey);
                return Task.CompletedTask;
            }

            var oldVal = _partitionKey.FastEnumToString();

            lock (_lock)
            {
                if (StringEnumCache.TryGetEnumIgnoreCase<IOTaskChannelPartitionKey>(newVal, out var newPartitionKey))
                {
                    _partitionKey = newPartitionKey;
                    Logger.LogWarning("{config} is changed, Old: {oldVal}, New: {newVal}", _configKey, oldVal, newVal);
                }
            }

            return Task.CompletedTask;
        }
    }
}