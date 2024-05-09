namespace SkuService.Common.Extensions
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;

    internal class CustomDictionaryConverter: JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Dictionary<string, string>);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null!;
            }

            if(reader.Value == null)
            {
                return null!;
            }

            return JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.Value.ToString()!)!;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
