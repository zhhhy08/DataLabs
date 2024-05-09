namespace Microsoft.WindowsAzure.Governance.DataLabs.IOService.Contracts
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;

    public abstract class AbstractInputMessage<T> : IInputMessage where T : class
    {
        private BinaryData _serializedData;
        private T _deserializedObject;

        public abstract DateTimeOffset EventTime { get; set; }
        public abstract string CorrelationId { get; set; }
        public abstract string ResourceId { get; set; }

        public bool HasCompressed { get; set; }

        public bool HasSerializedObject => _serializedData != null;
        public bool HasDeserializedObject => _deserializedObject != null;

        public abstract SolutionDataFormat DataFormat { get; }

        public abstract Func<BinaryData, bool, T> Deserializer { get; }

        public abstract Func<T, BinaryData> Serializer { get; }

        protected abstract Counter<long>? DeserializerCounter { get; }

        public BinaryData SerializedData
        {
            get
            {
                if (_serializedData == null && _deserializedObject != null)
                {
                    lock(this)
                    {
                        // Same InputMessage could be shared between multiple tasks. So we need lock here
                        if (_serializedData == null)
                        {
                            _serializedData = Serializer(_deserializedObject);
                            HasCompressed = false;
                        }
                    }
                    
                }
                return _serializedData;
            }
            set
            {
                _serializedData = value;
            }
        }

        public T DeserializedObject
        {
            get
            {
                if (_deserializedObject == null && _serializedData != null && Deserializer != null)
                {
                    lock(this)
                    {
                        // Same InputMessage could be shared between multiple tasks. So we need lock here
                        if (_deserializedObject == null)
                        {
                            DeserializerCounter?.Add(1);
                            _deserializedObject = Deserializer(_serializedData, HasCompressed);
                            if (_deserializedObject != null)
                            {
                                FillInfoWithDeserializedObject(_deserializedObject);
                            }
                        }
                    }
                }
                return _deserializedObject;
            }
            set
            {
                _deserializedObject = value;
            }
        }

        object IInputMessage.DeserializedObject
        {
            get
            {
                return DeserializedObject;
            }
            set
            {
                DeserializedObject = (T)value;
            }
        }

        public abstract void AddCommonTags(OpenTelemetryActivityWrapper taskActivity);
        
        public abstract void AddRetryProperties(ref TagList tagList);

        protected abstract void FillInfoWithDeserializedObject(T deserializedObject);
    }
}
