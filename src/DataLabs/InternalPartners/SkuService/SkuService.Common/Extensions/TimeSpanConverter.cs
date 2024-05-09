namespace SkuService.Common.Extensions
{
    using Newtonsoft.Json;
    using System;
    using System.Xml;

    internal class TimeSpanConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            var ts = (TimeSpan)value!;
            var tsString = XmlConvert.ToString(ts);
            serializer.Serialize(writer, tsString);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null!;
            }

            var value = serializer.Deserialize<string>(reader);
            return XmlConvert.ToTimeSpan(value!);
        }

        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert == typeof(TimeSpan) || typeToConvert == typeof(TimeSpan?);
        }
    }
}
