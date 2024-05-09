// <copyright file="ICollectionExtensions.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// ICollection extensions class.
    /// </summary>
    public static class ICollectionExtensions
    {
        /// <summary>
        /// Method to add range of values to a collection.
        /// </summary>
        /// <typeparam name="T">Type of the source collection.</typeparam>
        /// <param name="source">Source collection.</param>
        /// <param name="values">Values to add to source.</param>
        public static void AddRange<T>(this ICollection<T> source, IEnumerable<T> values)
        {
            foreach (T value in values)
            {
                source.Add(value);
            }
        }

        public static bool SafeFastEmpty<T>(this ICollection<T>? collection) => collection == null || collection.Count == 0;

        public static bool IsNullOrEmpty<T>(this IEnumerable<T> enumerable)
        {
            if (enumerable != null)
            {
                return !enumerable.Any();
            }

            return true;
        }
    }
}
