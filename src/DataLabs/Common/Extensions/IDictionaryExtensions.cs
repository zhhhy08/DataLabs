// <copyright file="IDictionaryExtensions.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions
{
    using System.Collections.Generic;

    /// <summary>
    /// IDictionary extension class.
    /// </summary>
    public static class IDictionaryExtensions
    {
        /// <summary>
        /// Method to convert a dictionary into a coalesce dictionary.
        /// </summary>
        /// <typeparam name="TKey">The key type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="source">The source dictionary.</param>
        /// <param name="comparer">The equality comparer.</param>
        /// <returns>Coalesce dictionary.</returns>
        public static Dictionary<TKey, TValue> CoalesceDictionary<TKey, TValue>(
            this Dictionary<TKey, TValue> source,
            IEqualityComparer<TKey>? comparer = null) where TKey : notnull
        {
            return source ?? new Dictionary<TKey, TValue>(comparer);
        }
    }
}
