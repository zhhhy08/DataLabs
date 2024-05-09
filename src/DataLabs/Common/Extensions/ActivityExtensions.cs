namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions
{
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.ActivityTracing.Contracts;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;

    public static class ActivityExtensions
    {
        public const string CountSuffix = "Count";

        public static void LogCollectionAndCount<T>(
            this IActivity activity, string propertyName, IEnumerable<T>? values,
            int maxLength = 1024, char delimiter = '|', bool ignoreCountForSingleValueProperty = false)
        {
            GuardHelper.ArgumentNotNull(activity);
            GuardHelper.ArgumentNotNullOrEmpty(propertyName);
            GuardHelper.IsArgumentPositive(maxLength);

            if (values == null)
            {
                if (!ignoreCountForSingleValueProperty)
                {
                    activity.Properties[propertyName + CountSuffix] = 0;
                }
                activity.Properties[propertyName] = null;
                return;
            }

            var count = 0;
            var sb = new StringBuilder();
            using (var enumerator = values.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    count++;

                    var str = enumerator.Current?.ToString() ?? "\"null\"";
                    if (sb.Length > 0 && sb.Length < maxLength)
                    {
                        sb.Append(delimiter);
                    }

                    if (sb.Length + str.Length > maxLength)
                    {
                        var remainingLen = maxLength - sb.Length;
                        if (remainingLen > 0)
                        {
                            sb.Append(str.Substring(0, remainingLen));
                        }

                        sb.Append("...");
                        break;
                    }

                    sb.Append(str);
                }

                switch (values)
                {
                    case ICollection<T> collection:
                        count = collection.Count;
                        break;
                    case IReadOnlyCollection<T> readOnlyCollection:
                        count = readOnlyCollection.Count;
                        break;
                    default:
                        while (enumerator.MoveNext()) count++;
                        break;
                }
            }

            if (!ignoreCountForSingleValueProperty || count > 1)
            {
                activity.Properties[propertyName + CountSuffix] = count;
            }
            activity.Properties[propertyName] = sb.ToString();
        }
    }
}