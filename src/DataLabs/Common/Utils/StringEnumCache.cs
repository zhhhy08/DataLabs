namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;

    /// <summary>
    /// A cache that converts between strings and enums
    /// </summary>
    internal static class StringEnumCache<T> where T : struct
    {
        #region Fields

        private static readonly IDictionary<string, T> EnumCache;

        private static readonly IDictionary<string, T>? EnumCacheIgnoreCase;

        private static readonly IDictionary<T, string> StringCache;

        #endregion

        #region Constructors

        static StringEnumCache()
        {
            EnumCache = new Dictionary<string, T>(StringComparer.Ordinal);
            StringCache = new Dictionary<T, string>();

            var enumStrings = Enum.GetNames(typeof(T));
            var enumStringPairs = enumStrings.Select(GetEnumStringPair).ToArray();

            foreach (var pair in enumStringPairs)
            {
                EnumCache[pair.Item2] = pair.Item1;
                StringCache[pair.Item1] = pair.Item2;
            }

            if (enumStrings.Select(s => s.ToLowerInvariant()).Distinct().Count() == enumStrings.Length)
            {
                EnumCacheIgnoreCase = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in enumStringPairs)
                {
                    EnumCacheIgnoreCase[pair.Item2] = pair.Item1;
                }
            }
        }

        #endregion

        #region Static Methods

        public static string GetString(T value)
        {
            return StringCache[value];
        }

        public static string? SafeGetString(T value)
        {
            return StringCache.TryGetValue(value, out var str) ? str : value.ToString();
        }

        public static T GetEnum(string str)
        {
            GuardHelper.ArgumentNotNullOrEmpty(str);

            if (!TryGetEnum(str, out var value))
            {
                throw new ArgumentOutOfRangeException($"Unknown enum string: '{str}'");
            }

            return value;
        }

        public static T GetEnumIgnoreCase(string str)
        {
            GuardHelper.ArgumentNotNullOrEmpty(str);

            if (!TryGetEnumIgnoreCase(str, out var value))
            {
                throw new ArgumentOutOfRangeException(nameof(str), $"Unknown enum string: '{str}'");
            }

            return value;
        }

        public static bool TryGetEnum(string str, out T value)
        {
            GuardHelper.ArgumentNotNullOrEmpty(str);

            return EnumCache.TryGetValue(str, out value);
        }

        public static bool TryGetEnumIgnoreCase(string str, out T value)
        {
            GuardHelper.ArgumentNotNullOrEmpty(str);

            if (EnumCacheIgnoreCase == null)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(str),
                    $"Case insensitive cache is not populated for enum type '{nameof(T)}'");
            }

            return EnumCacheIgnoreCase.TryGetValue(str, out value);
        }

        public static IEnumerable<T> GetValues()
        {
            return EnumCache.Values;
        }

        public static IEnumerable<string> GetStrings()
        {
            return EnumCache.Keys;
        }

        #endregion

        #region Private helper methods

        private static Tuple<T, string> GetEnumStringPair(string memberName)
        {
            var attr = typeof(T).GetRuntimeField(memberName)?
                .GetCustomAttribute<EnumMemberAttribute>(true);

            var enumMember = (T)Enum.Parse(typeof(T), memberName);
            var memberString = attr?.Value ?? memberName;
            return Tuple.Create(enumMember, memberString);
        }

        #endregion
    }

    public static class StringEnumCache
    {
        public static string GetString<T>(T enumValue) where T : struct => StringEnumCache<T>.GetString(enumValue);

        public static string? SafeGetString<T>(T enumValue) where T : struct => StringEnumCache<T>.SafeGetString(enumValue);

        public static T GetEnum<T>(string enumStr) where T : struct => StringEnumCache<T>.GetEnum(enumStr);

        public static T GetEnumIgnoreCase<T>(string enumStr) where T : struct => StringEnumCache<T>.GetEnumIgnoreCase(enumStr);

        public static bool TryGetEnum<T>(string str, out T value) where T : struct => StringEnumCache<T>.TryGetEnum(str, out value);

        public static bool TryGetEnumIgnoreCase<T>(string str, out T value) where T : struct => StringEnumCache<T>.TryGetEnumIgnoreCase(str, out value);

        public static string FastEnumToString<T>(this T enumValue) where T : struct => GetString(enumValue);

        public static string? SafeFastEnumToString<T>(this T enumValue) where T : struct => SafeGetString(enumValue);

        public static IEnumerable<T> GetValues<T>() where T : struct => StringEnumCache<T>.GetValues();

        public static IEnumerable<string> GetStrings<T>() where T : struct => StringEnumCache<T>.GetStrings();
    }
}