namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Serialization;
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The JSON Type formatter class
    /// </summary>
    public class JsonTypeFormatter : IDataFormatter
    {
        #region Members

        /// <summary>
        /// The default serializer settings
        /// </summary>
        public static readonly JsonSerializerSettings DefaultSerializerSettings =
            new JsonSerializerSettings
            {
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                DefaultValueHandling = DefaultValueHandling.Populate,
                DateFormatHandling = DateFormatHandling.MicrosoftDateFormat,
                MaxDepth = int.MaxValue,
                Formatting = Formatting.Indented,
                TypeNameHandling = TypeNameHandling.None,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            };

        /// <summary>
        /// The Minified serializer settings
        /// </summary>
        public static readonly JsonSerializerSettings MinifiedSerializerSettings =
            new JsonSerializerSettings
            {
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                DefaultValueHandling = DefaultValueHandling.Populate,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                MaxDepth = int.MaxValue,
                Formatting = Formatting.None,
                TypeNameHandling = TypeNameHandling.None,
                PreserveReferencesHandling = PreserveReferencesHandling.None
            };

        /// <summary>
        /// The Default Formatter
        /// </summary>
        public static readonly JsonTypeFormatter Formatter = new JsonTypeFormatter();

        public static readonly JsonTypeFormatter MinifiedFormatter = new JsonTypeFormatter(MinifiedSerializerSettings);

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the encoding to be used for JsonFormatter
        /// </summary>
        public Encoding Encoding
        {
            get;
        }

        /// <summary>
        /// The serializer settings
        /// </summary>
        public JsonSerializerSettings SerializerSettings
        {
            get;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonTypeFormatter" /> class.
        /// </summary>
        /// <param name="serializerSettings">The serializer settings.</param>
        /// <param name="encoding">The encoding.</param>
        public JsonTypeFormatter(JsonSerializerSettings?
            serializerSettings = null, Encoding? encoding = null)
        {
            this.Encoding = encoding ?? new UTF8Encoding(false, true);
            this.SerializerSettings = serializerSettings ?? DefaultSerializerSettings;
            this.SerializerSettings.ObjectCreationHandling = ObjectCreationHandling.Replace;
        }

        #endregion

        #region Read Methods

        /// <summary>
        /// Reads from text.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stringToRead">The string to read.</param>
        /// <exception cref="Newtonsoft.Json.JsonReaderException"></exception>
        public T? ReadFromText<T>(string stringToRead)
        {
            if (string.IsNullOrWhiteSpace(stringToRead))
            {
                return default;
            }

            var returnObject = SerializationHelper.DeSerializeObject<
                object>(typeof(T), stringToRead, this.SerializerSettings);
            if (returnObject == null)
            {
                return default;
            }
            return (T)returnObject;
        }

        /// <summary>
        /// Reads from stream.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="readStream">The read stream.</param>
        public T? ReadFromStream<T>(Stream readStream)
        {
            var serializeObject = ReadFromStream(typeof(T), readStream);
            if (serializeObject != null)
            {
                return (T)serializeObject;
            }
            return default;
        }

        /// <summary>
        /// Reads from a given byte array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="readBytes">The read array</param>
        /// <returns>Deserialized object</returns>
        public T? ReadFromByteArray<T>(byte[] readBytes)
        {
            using (var stream = new MemoryStream(readBytes))
            {
                return this.ReadFromStream<T>(stream);
            }
        }

        /// <summary>
        /// Reads from stream.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="readStream">The read stream.</param>
        /// <exception cref="Newtonsoft.Json.JsonReaderException"></exception>
        public object? ReadFromStream(Type type, Stream readStream)
        {
            GuardHelper.ArgumentNotNull(type, nameof(type));

            object? result;
            //Do not dispose this stream as it will close the underlying stream
            var streamReader = new StreamReader(readStream, this.Encoding);
            var serializer = JsonSerializer.Create(this.SerializerSettings);
            using (var textReader = new JsonTextReader(streamReader))
            {
                if (type.IsEnum)
                {
                    var value = textReader.ReadAsInt32();
                    if (!value.HasValue)
                    {
                        throw new JsonReaderException();
                    }

                    result = Enum.ToObject(type, value.Value);
                }
                else
                {
                    result = serializer.Deserialize(textReader, type);
                }
            }

            return result;
        }

        /// <summary>
        /// Reads from stream async.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="readStream">The read stream.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task<T?> ReadFromStreamAsync<T>(Stream readStream, CancellationToken cancellationToken)
        {
            var serializedResult = await ReadFromStreamAsync(typeof(T), readStream, cancellationToken).IgnoreContext();
            if (serializedResult != null)
            {
                return (T)serializedResult;
            }
            return default;
        }

        /// <summary>
        /// Called when [read from readStream async].
        /// </summary>
        /// <param name="type">The type to deserialize.</param>
        /// <param name="readStream">The stream to read from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// Async read task.
        /// </returns>
        /// <exception cref="JsonReaderException"></exception>
        public async Task<object?> ReadFromStreamAsync(Type type, Stream readStream, CancellationToken cancellationToken)
        {
            var serializer = JsonSerializer.Create(this.SerializerSettings);
            object? result;

            //Do not dispose this stream as it will close the underlying stream
            var streamReader = new StreamReader(readStream, this.Encoding);
            using (var textReader = new JsonTextReader(streamReader))
            {
                if (type.IsEnum)
                {
                    var value = await textReader.ReadAsInt32Async(cancellationToken).IgnoreContext();
                    if (!value.HasValue)
                    {
                        throw new JsonReaderException();
                    }

                    result = Enum.ToObject(type, value.Value);
                }
                else
                {
                    result = serializer.Deserialize(textReader, type);
                }
            }

            return result;
        }

        #endregion

        #region Write Methods

        /// <summary>
        /// Writes to text.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objectToStringify">The object to stringify.</param>
        public string? WriteToText<T>(T objectToStringify)
        {
            if (objectToStringify == null)
            {
                return null;
            }

            return SerializationHelper.SerializeObject(
                objectToStringify, settings: this.SerializerSettings);
        }

        /// <summary>
        /// Writes the given object to a byte array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objectToWrite">Object to be serialized</param>
        /// <returns>Byte array representing the serialized object</returns>
        public byte[] WriteToByteArray<T>(T objectToWrite)
        {
            using (var stream = new MemoryStream())
            {
                this.WriteToStream(objectToWrite, stream);
                stream.Position = 0;

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Writes to stream.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>
        /// <param name="writeStream">The write stream.</param>
        public void WriteToStream<T>(T value, Stream writeStream)
        {
            var streamWriter = new StreamWriter(writeStream, this.Encoding);
            var serializer = JsonSerializer.Create(this.SerializerSettings);
            using (var textWriter = new JsonTextWriter(streamWriter) { CloseOutput = false })
            {
                serializer.Serialize(textWriter, value);
                textWriter.Flush();
            }
        }

        public void WriteListItemToStream<T>(T value, Stream writeStream)
        {
            WriteToStream(value, writeStream);
        }

        public void CompressAndWriteToStream<T>(T value, Stream writeStream)
        {
            using (var compressionStream = new GZipStream(writeStream, CompressionMode.Compress, true))
            {
                Formatter.WriteToStream(value, compressionStream);
            }
        }

        /// <summary>
        /// Writes to stream async.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>stream
        /// <param name="writeStream">The write stream.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task WriteToStreamAsync<T>(T value, Stream writeStream, CancellationToken cancellationToken)
        {
            var serializer = JsonSerializer.Create(this.SerializerSettings);
            var streamWriter = new StreamWriter(writeStream, this.Encoding);
            using (var textWriter = new JsonTextWriter(streamWriter) { CloseOutput = false })
            {
                serializer.Serialize(textWriter, value);
                await textWriter.FlushAsync(cancellationToken).IgnoreContext();
            }
        }

        #endregion

        #region Static Methods

        /// <summary>
        /// Creates a new serializer settings to be used by WebAPI services.
        /// </summary>
        public static void UpdateApiSerializerSettings(JsonSerializerSettings settings)
        {
            settings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
            settings.DefaultValueHandling = DefaultValueHandling.Populate;
            settings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
            settings.TypeNameHandling = TypeNameHandling.None;
            settings.NullValueHandling = NullValueHandling.Ignore;
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();

            settings.Converters = new[]{
                new StringEnumConverter
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            };
        }

        #endregion
    }
}