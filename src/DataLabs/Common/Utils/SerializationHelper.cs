namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using Google.Protobuf;
    using Microsoft.Extensions.Logging;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Constants;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.PartnerBlobClient;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ServiceMonitoring;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts;
    using Microsoft.WindowsAzure.Governance.Notifications.ArnContracts.ResourceContracts;
    using Newtonsoft.Json;

    /// <summary>
    /// SerializationHelpers
    /// </summary>
    public class SerializationHelper
    {
        #region Fields

        private static readonly ILogger<SerializationHelper> Logger =
            DataLabLoggerFactory.CreateLogger<SerializationHelper>();

        public static readonly JsonSerializerSettings DataLabsDefaultSerializationSettings =
           new JsonSerializerSettings
           {
               DateTimeZoneHandling = DateTimeZoneHandling.Utc,
               DefaultValueHandling = DefaultValueHandling.Populate,
               MaxDepth = int.MaxValue,
               Formatting = Formatting.None,
               TypeNameHandling = TypeNameHandling.None,
               PreserveReferencesHandling = PreserveReferencesHandling.None
           };

        private static JsonSerializer _serializer = JsonSerializer.Create(DataLabsDefaultSerializationSettings);
        private static ReadOnlySpan<byte> Utf8Bom => new byte[] { 0xEF, 0xBB, 0xBF };

        #endregion


        #region Public Methods

        public static string SerializeToString<T>(T data)
        {
            var memory = SerializeToMemory(data, false);
            return Encoding.UTF8.GetString(memory.Span);
        }

        public static void WriteToStream<T>(T value, Stream writeStream)
        {
            // we have to use default constructor. It will internally use UTF8 with some tolerance without UTF8BOM
            // If we provide "UTF8" encoding, StreamWrite will add UTF8BOM
            var streamWriter = new StreamWriter(writeStream); 
            using (var textWriter = new JsonTextWriter(streamWriter) { CloseOutput = false })
            {
                _serializer.Serialize(textWriter, value);
                textWriter.Flush();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CompressAndWriteToStream<T>(T value, Stream writeStream)
        {
            using (var compressionStream = new GZipStream(writeStream, CompressionMode.Compress, true))
            {
                WriteToStream(value, compressionStream);
            }
        }

        public static Memory<byte> SerializeToMemory<T>(T data, bool compress)
        {
            using var stream = new MemoryStream();

            if (compress)
            {
                CompressAndWriteToStream(data, stream);
            }
            else
            {
                WriteToStream(data, stream);
            }

            return stream.GetBuffer().AsMemory(0, (int)stream.Position);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ByteString SerializeToByteString<T>(T data, bool compress)
        {
            var memory = SerializeToMemory<T>(data, compress);
            return UnsafeByteOperations.UnsafeWrap(memory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Deserialize<T>(ByteString byteString, bool isCompressed)
        {
            var binaryData = BinaryData.FromBytes(byteString.Memory);
            return Deserialize<T>(binaryData, isCompressed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Deserialize<T>(string data)
        {
            using (var textReader = new StringReader(data)) // It is better to use default constructor. It will internally use UTF8 with some tolerance.
            {
                var result = (T?)_serializer.Deserialize(textReader, typeof(T));
                if (result == null)
                {
                    throw new JsonReaderException();
                }
                return result;
            }
        }

        public static T ReadFromStream<T>(Stream readStream)
        {
            //Do not dispose this stream as it will close the underlying stream
            var streamReader = new StreamReader(readStream); // It is better to use default constructor. It will internally use UTF8 with some tolerance.
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var result = _serializer.Deserialize<T>(jsonReader);
                if (result == null)
                {
                    throw new JsonReaderException();
                }
                return result;
            }
        }

        public static List<T> ReadListFromStream<T>(Stream readStream)
        {
            var resourcesList = new List<T>();
            var lineReadFailureCount = 0;

            try
            {
                //Do not dispose this stream as it will close the underlying stream
                var streamReader = new StreamReader(readStream);
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    jsonReader.SupportMultipleContent = true;

                    while (jsonReader.Read())
                    {
                        try
                        {
                            var result = _serializer.Deserialize<T>(jsonReader);
                            if (result != null)
                            {
                                resourcesList.Add(result);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Deserialize failed during reading List. {exception}", ex.ToString());
                            // TODO
                            // Metric
                            ++lineReadFailureCount;
                        }
                    }

                    return resourcesList;
                }
            }
            finally
            {
                // TODO
                if (lineReadFailureCount > 0)
                {
                    // Metric
                }

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Deserialize<T>(BinaryData binaryData, bool isCompressed)
        {
            using (var stream = binaryData.ToStream())
            {
                stream.Position = 0;

                if (isCompressed)
                {
                    using (var gzipStream = new GZipStream(stream, CompressionMode.Decompress, true))
                    {
                        return ReadFromStream<T>(gzipStream);
                    }
                }
                else
                {
                    return ReadFromStream<T>(stream);
                }
            }
        }

        public static T[]? DeserializeArnNotification<T>(BinaryData json, bool isCompressed)
        {
            GuardHelper.ArgumentNotNull(json);

            ReadOnlySpan<byte> span = json;
            if (span.Length == 0)
            {
                return null;
            }

            // To handle UTF8BOM,
            if (span.StartsWith(Utf8Bom))
            {
                span = span.Slice(Utf8Bom.Length);
            }

            char firstChar = Convert.ToChar(span[0]);

            T[]? egEvents = null;
            if (firstChar == '[')
            {
                egEvents = Deserialize<T[]>(json, isCompressed);
            }
            else
            {
                egEvents = new T[1];
                egEvents[0] = Deserialize<T>(json, isCompressed);
            }

            return egEvents;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EventGridNotification<NotificationDataV3<GenericResource>>[]? DeserializeArnV3Notification(BinaryData json, bool isCompressed)
        {
            return DeserializeArnNotification<EventGridNotification<NotificationDataV3<GenericResource>>>(json, isCompressed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EventGridNotification<NotificationDataV3<GenericResource>>[]? DeserializeArnV3Notification(ByteString byteString, bool isCompressed)
        {
            return DeserializeArnV3Notification(BinaryData.FromBytes(byteString.Memory), isCompressed);
        }

        public static async ValueTask<List<ValueTuple<EventGridNotification<NotificationDataV3<GenericResource>>, bool>>?> DeserializeArnV3ToEachResourceAsync(
            EventGridNotification<NotificationDataV3<GenericResource>>[] eventGridEvents,
            IPartnerBlobClient partnerBlobClient,
            int retryFlowCount,
            CancellationToken cancellationToken)
        {
            if (eventGridEvents == null || eventGridEvents.Length == 0)
            {
                return null;
            }

            var taskActivity = OpenTelemetryActivityWrapper.Current;
            var individualEventGridEvents = new List<ValueTuple<EventGridNotification<NotificationDataV3<GenericResource>>, bool>>();

            taskActivity?.SetTag(SolutionConstants.NumEventGridEvents, eventGridEvents.Length);

            // shortcut
            if (eventGridEvents.Length == 1 && eventGridEvents[0].Data?.Resources?.Count == 1)
            {
                individualEventGridEvents.Capacity = 1;
                individualEventGridEvents.Add((eventGridEvents[0], false));
                return individualEventGridEvents;
            }

            for (int i = 0, count = eventGridEvents.Length; i < count; i++)
            {
                var parentEventGridEvent = eventGridEvents[i];
                var parentDataV3 = parentEventGridEvent.Data;
                var isBobPayload = false;

                if (parentDataV3 == null)
                {
                    continue;
                }

                IList<NotificationResourceDataV3<GenericResource>> resources = parentDataV3.Resources;
                if (resources?.Count == 1)
                {
                    individualEventGridEvents.Add((parentEventGridEvent, isBobPayload));
                    continue;
                }

                if (resources == null || resources.Count == 0)
                {
                    var blobInfo = parentDataV3.ResourcesBlobInfo;
                    if (blobInfo != null)
                    {
                        if (!(blobInfo.BlobUri?.Length > 0))
                        {
                            throw new SerializationException("Blob URL is null");
                        }

                        if (taskActivity != null)
                        {
                            var maskedBlobUri = SolutionLoggingUtils.HideSigFromBlobUri(blobInfo.BlobUri);

                            if (i > 0)
                            {

                                taskActivity.SetTag(SolutionConstants.BlobURI + i, maskedBlobUri);
                                taskActivity.SetTag(SolutionConstants.BlobSize + i, blobInfo.BlobSize);
                            }
                            else
                            {
                                taskActivity.SetTag(SolutionConstants.BlobURI, maskedBlobUri);
                                taskActivity.SetTag(SolutionConstants.BlobSize, blobInfo.BlobSize);
                            }
                        }

                        resources = await partnerBlobClient.GetResourcesAsync<NotificationResourceDataV3<GenericResource>>(blobInfo.BlobUri, retryFlowCount, cancellationToken).ConfigureAwait(false);
                        if (resources == null || resources.Count == 0)
                        {
                            taskActivity?.SetTag(SolutionConstants.EmptyBlobResponse, true);
                            throw new SerializationException("Blob URL call returns empty response. URI: " +
                                SolutionLoggingUtils.HideSigFromBlobUri(blobInfo.BlobUri));
                        }

                        isBobPayload = true;
                    }
                    else
                    {
                        // there is NO blob URL
                        taskActivity?.SetTag(SolutionConstants.NOBlobURI, true);
                        return null;
                    }
                }

                var newCap = individualEventGridEvents.Count + resources.Count;
                if (individualEventGridEvents.Capacity < newCap)
                {
                    individualEventGridEvents.Capacity = newCap;
                }

                for (int r = 0, rcount = resources.Count; r < rcount; r++)
                {
                    var newDataV3 = CloneArnV3WithNewResource(parentDataV3, resources[r]);
                    var newEventGridEvent = CloneEventGridNotificationWithNewV3Data(parentEventGridEvent, newDataV3);
                    individualEventGridEvents.Add((newEventGridEvent, isBobPayload));
                }
            }

            return individualEventGridEvents;
        }

        /// <summary>
        /// Serializes the object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj">The obj.</param>
        /// <param name="jsonFormatting">The json formatting.</param>
        /// <param name="settings">The settings.</param>
        public static string SerializeObject<T>(
            T obj, Formatting jsonFormatting = Formatting.None, JsonSerializerSettings? settings = null)
        {
            GuardHelper.ArgumentNotNull(obj);

            var stringBuilder = new StringBuilder();
            var stringWriter = new StringWriter(stringBuilder, CultureInfo.InvariantCulture);
            try
            {
                SerializeJson(obj, stringWriter, jsonFormatting, settings);
            }
            finally
            {
                stringWriter?.Dispose();
            }

            return stringBuilder.ToString();
        }

        #endregion

        #region Private Methods

        private static void SerializeJson<T>(
            T obj,
            TextWriter textWriter,
            Formatting jsonFormatting = Formatting.None,
            JsonSerializerSettings? jsonSerializerSettings = null)
        {
            var serializer = JsonSerializer.Create(jsonSerializerSettings ?? JsonTypeFormatter.DefaultSerializerSettings);
            using (var jsonTextWriter = new JsonTextWriter(textWriter) { CloseOutput = false, Formatting = jsonFormatting })
            {
                serializer.Serialize(jsonTextWriter, obj);
                jsonTextWriter.Flush();
            }
        }

        public static T? DeSerializeObject<T>(string text, JsonSerializerSettings? settings = null) where T : class
        {
            GuardHelper.ArgumentNotNullOrEmpty(text, "text");
            return DeSerializeObject<T>(typeof(T), text, settings);
        }

        public static T? DeSerializeObject<T>(Type targetType, string text, JsonSerializerSettings? settings = null) where T : class
        {
            return DeSerializeObject<T>(targetType, null, text, settings);
        }

        public static T? DeSerializeObject<T>(Type targetType, IDictionary<Type, Type>? interfaceTypeToTargetTypeMap, string text, JsonSerializerSettings? settings = null) where T : class
        {
            GuardHelper.ArgumentNotNullOrEmpty(text, "text");
            GuardHelper.ArgumentNotNull(targetType, "targetType");
            Type typeFromHandle = typeof(T);
            if (!(targetType == typeFromHandle) && !targetType.IsSubclassOf(typeFromHandle))
            {
                throw new ArgumentException("targetType not of type T");
            }

            StringReader stringReader = new StringReader(text);
            try
            {
                JsonSerializer jsonSerializer = JsonSerializer.Create(settings ?? JsonTypeFormatter.DefaultSerializerSettings);
                if (interfaceTypeToTargetTypeMap != null && interfaceTypeToTargetTypeMap.Any())
                {
                    jsonSerializer.Converters.Add(InterfaceTypeConverter.CreateInterfaceConverter(interfaceTypeToTargetTypeMap));
                }

                return DeserializeJson<T>(targetType, stringReader, jsonSerializer, closeInput: true);
            }
            finally
            {
                stringReader?.Dispose();
            }
        }

        public static T? DeserializeJson<T>(TextReader textReader, JsonSerializerSettings? jsonSerializerSettings = null, bool closeInput = true)
            where T : class
        {
            return DeserializeJson<T>(typeof(T), textReader, JsonSerializer.CreateDefault(jsonSerializerSettings), closeInput);
        }


        private static T? DeserializeJson<T>(Type targetType, TextReader textReader, JsonSerializer serializer, bool closeInput)
            where T : class
        {
            using (var jsonTextReader = new JsonTextReader(textReader) { CloseInput = closeInput })
            {
                if (targetType.IsEnum)
                {
                    var value = jsonTextReader.ReadAsInt32();
                    if (value.HasValue == false)
                    {
                        throw new JsonReaderException();
                    }
                    return Enum.ToObject(targetType, value.Value) as T;
                }

                return serializer.Deserialize(jsonTextReader, targetType) as T;
            }
        }


        private static NotificationDataV3<GenericResource> CloneArnV3WithNewResource(NotificationDataV3<GenericResource> parent, NotificationResourceDataV3<GenericResource> resource)
        {
            GuardHelper.ArgumentNotNull(resource);

            var newResources = new List<NotificationResourceDataV3<GenericResource>>(1);
            newResources.Add(resource);

            Guid? homeTenantId = parent.HomeTenantId != null ? Guid.Parse(parent.HomeTenantId) : null;
            Guid? resourceHomeTenantId = parent.ResourceHomeTenantId != null ? Guid.Parse(parent.ResourceHomeTenantId) : null;

            return new NotificationDataV3<GenericResource>(
                publisherInfo: parent.PublisherInfo,
                resources: newResources,
                correlationId: null,
                resourceLocation: parent.ResourceLocation,
                frontdoorLocation: parent.FrontdoorLocation,
                homeTenantId: homeTenantId,
                resourceHomeTenantId: resourceHomeTenantId,
                apiVersion: parent.ApiVersion,
                additionalBatchProperties: parent.AdditionalBatchProperties,
                dataBoundary: null);
        }

        private static EventGridNotification<NotificationDataV3<GenericResource>> CloneEventGridNotificationWithNewV3Data(
            EventGridNotification<NotificationDataV3<GenericResource>> parentEventGrid,
            NotificationDataV3<GenericResource> newDataV3)
        {
            return new EventGridNotification<NotificationDataV3<GenericResource>>(
                id: parentEventGrid.Id,
                topic: parentEventGrid.Topic,
                subject: parentEventGrid.Subject,
                eventType: parentEventGrid.EventType,
                eventTime: parentEventGrid.EventTime,
                data: newDataV3);
        }

        private class InterfaceTypeConverter : JsonConverter
        {
            #region Privates

            /// <summary>
            /// The _interface type to target type map
            /// </summary>
            private readonly IDictionary<Type, Type> _interfaceTypeToTargetTypeMap;

            #endregion

            #region Constructors

            /// <summary>
            /// Prevents a default instance of the <see cref="InterfaceTypeConverter"/> class from being created.
            /// </summary>
            /// <param name="interfaceTypeToTargetTypeMap">The interface type to target type map.</param>
            private InterfaceTypeConverter(IDictionary<Type, Type> interfaceTypeToTargetTypeMap)
            {
                GuardHelper.ArgumentNotNullOrEmpty(interfaceTypeToTargetTypeMap, nameof(interfaceTypeToTargetTypeMap));

                this._interfaceTypeToTargetTypeMap = interfaceTypeToTargetTypeMap;
            }

            /// <summary>
            /// Creates the interface converter.
            /// </summary>
            /// <param name="interfaceTypeToTargetTypeMap">The interface type to target type map.</param>
            public static JsonConverter CreateInterfaceConverter(IDictionary<Type, Type> interfaceTypeToTargetTypeMap)
            {
                return new InterfaceTypeConverter(interfaceTypeToTargetTypeMap);
            }

            #endregion

            #region JsonConverter Overrides

            /// <summary>
            /// Checks if a type can be converted by looking for the type in our interface dictionary.
            /// </summary>
            /// <param name="objectType">The type to convert</param>
            /// <returns>If the converter can convert the type.</returns>
            public override bool CanConvert(Type objectType)
            {
                return this._interfaceTypeToTargetTypeMap.ContainsKey(objectType);
            }

            /// <summary>
            /// Executes default writing behavior
            /// </summary>
            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                GuardHelper.ArgumentNotNull(serializer, nameof(serializer));

                serializer.Serialize(writer, value);
            }

            /// <summary>
            /// Reads Json, passing the correct concrete type to instantiate.
            /// </summary>
            /// <exception cref="System.NotSupportedException">ReadJson executed on a type without a mapping.</exception>
            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                GuardHelper.ArgumentNotNull(serializer, nameof(serializer));

                if (this._interfaceTypeToTargetTypeMap.ContainsKey(objectType))
                {
                    return serializer.Deserialize(
                        reader, this._interfaceTypeToTargetTypeMap[objectType]);
                }

                throw new NotSupportedException(string.Format(CultureInfo.
                    InvariantCulture, "Concrete type not found for interface {0}", objectType));
            }

            #endregion
        }

        #endregion
    }
}
