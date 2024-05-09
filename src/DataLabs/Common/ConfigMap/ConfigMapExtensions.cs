namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ConfigMap
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class ConfigMapExtensions
    {
        public const string LIST_DELIMITER = ";";

        public static List<string> ConvertToList(this string? value, string delimiter = LIST_DELIMITER, StringSplitOptions stringSplitOptions = StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        {
            if (string.IsNullOrEmpty(value))
            {
                return new List<string>(0);
            }
            return value.Split(delimiter, stringSplitOptions).ToList();
        }

        public static HashSet<string> ConvertToSet(this string? value, bool caseSensitive, string delimiter = LIST_DELIMITER, StringSplitOptions stringSplitOptions = StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        {
            if (string.IsNullOrEmpty(value))
            {
                return new HashSet<string>(0);
            }

            var comparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            return value.Split(delimiter, stringSplitOptions).ToHashSet(comparer);
        }

        public static HashSet<int> ConvertToIntSet(this string? value, string delimiter = LIST_DELIMITER, StringSplitOptions stringSplitOptions = StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        {
            if (string.IsNullOrEmpty(value))
            {
                return new HashSet<int>(0);
            }

            var splitValues = value.Split(delimiter, stringSplitOptions);
            var hashSet = new HashSet<int>(capacity: splitValues.Length);

            foreach(var splitValue in splitValues)
            {
                // Mostly this will be used for configMap. let's throw exception when format is incorrect
                hashSet.Add(int.Parse(splitValue));
            }

            return hashSet;
        }

        public static Dictionary<string, string> ConvertToDictionary(this string? value,
            bool caseSensitive,
            string delimiter = LIST_DELIMITER,
            StringSplitOptions stringSplitOptions = StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        {
            if (string.IsNullOrEmpty(value))
            {
                return new Dictionary<string, string>(0);
            }

            var comparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            var splitValues = value.Split(delimiter, stringSplitOptions);
            var dictionary = new Dictionary<string, string>(capacity: splitValues.Length, comparer: comparer);

            foreach (var splitValue in splitValues)
            {
                var keyValue = splitValue.Split('=');
                if (keyValue.Length != 2)
                {
                    throw new ArgumentException($"Invalid format for key value pair: {splitValue}");
                }

                var key = keyValue[0].Trim();
                if (key.Length == 0)
                {
                    throw new ArgumentException($"Invalid format for key value pair: {splitValue}");
                }

                dictionary.Add(keyValue[0].Trim(), keyValue[1].Trim());
            }

            return dictionary;
        }
    }
}