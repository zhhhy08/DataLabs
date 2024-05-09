namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts
{
    using System;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using System.Diagnostics;

    public interface IInputMessage
    {
        public bool HasSerializedObject { get; }
        public bool HasDeserializedObject { get; }

        public BinaryData SerializedData { get; set; }

        public object DeserializedObject { get; set; }

        public DateTimeOffset EventTime { get; set; }

        public string CorrelationId { get; set; }

        public string ResourceId { get; set; } // This might be null for rawInput Message

        public bool HasCompressed { get; set; }

        public void AddCommonTags(OpenTelemetryActivityWrapper taskActivity);

        public void AddRetryProperties(ref TagList tagList);
    }
}
