namespace Microsoft.WindowsAzure.Governance.DataLabs.Tests.Common
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public static class AssertUtils
    {
        public static T AssertThrows<T>(Action action) where T : Exception
        {
            GuardHelper.ArgumentNotNull(action);

            try
            {
                action();
            }
            catch (T ex)
            {
                return ex;
            }
            throw new AssertFailedException($"An exception of type {typeof(T)} was expected but was not thrown");
        }

        public static async Task<T> AssertThrowsAsync<T>(Task task) where T : Exception
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (T ex)
            {
                return ex;
            }
            catch (AggregateException ex)
            {
                var excpetion = ex.InnerExceptions.OfType<T>().FirstOrDefault();
                if (excpetion != null) return excpetion;
            }

            throw new AssertFailedException($"An exception of type {typeof(T)} was expected but was not thrown");
        }

        public static Task<T> AssertThrowsAsync<T>(ValueTask task) where T : Exception => AssertThrowsAsync<T>(task.AsTask());

        public static Task<T> AssertThrowsAsync<T, TOutput>(ValueTask<TOutput> task) where T : Exception => AssertThrowsAsync<T>(task.AsTask());

        public static void IsNullableTrue(bool? value)
        {
            Assert.AreEqual(true, value);
        }

        public static void IsNullableFalse(bool? value)
        {
            Assert.AreEqual(false, value);
        }

        public static void AreEqual(DateTimeOffset expected, DateTimeOffset actual, TimeSpan threshold)
        {
            GuardHelper.ArgumentNotNull(expected);
            GuardHelper.ArgumentNotNull(actual);

            if (Math.Abs((expected - actual).Ticks) > threshold.Ticks)
            {
                throw new AssertFailedException($"The difference between {expected} and {actual} was not within {threshold}");
            }
        }

        public static void AreNotEqual(DateTimeOffset expected, DateTimeOffset actual, TimeSpan threshold)
        {
            GuardHelper.ArgumentNotNull(expected);
            GuardHelper.ArgumentNotNull(actual);

            if (Math.Abs((expected - actual).Ticks) <= threshold.Ticks)
            {
                throw new AssertFailedException($"The difference between {expected} and {actual} was not outside {threshold}");
            }
        }

        public static void HasDuplicatedValue(Type type, HashSet<string> allowedDuplicateKeys)
        {
            var map = PrivateFunctionAccessHelper.GetFieldValues(type);
            var checkingMap = new Dictionary<string, KeyValuePair<string, object>>();
            foreach (var item in map)
            {
                //Console.WriteLine("Key: " + item.Key + ", Value: " + item.Value);

                var mapKey = item.Value.ToString();
                var success = checkingMap.TryAdd(mapKey, new KeyValuePair<string, object>(item.Key, item.Value));
                if (!success)
                {
                    if (!allowedDuplicateKeys.Contains(item.Key))
                    {
                        var previousKeyValue = checkingMap[mapKey];
                        Assert.IsTrue(success, $"Key: {item.Key}, Value: {item.Value} is defined with previous key: {previousKeyValue.Key}");
                    }
                }
            }
        }

        public static void HasDuplicatedValue(object obj, HashSet<string> allowedDuplicateKeys)
        {
            HasDuplicatedValue(obj.GetType(), allowedDuplicateKeys);
        }

    }
}
