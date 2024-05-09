namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Cache
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// ICache
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="T"></typeparam>
    public interface ICache<TKey, T> : IDisposable
      where T : class
      where TKey : IEquatable<TKey>
    {
        /// <summary>
        /// Gets the count.
        /// </summary>
        /// <value>
        /// The count.
        /// </value>
        long Count
        {
            get;
        }

        /// <summary>
        /// Gets or sets the <see cref="`0"/> with the specified key.
        /// </summary>
        /// <value>
        /// The <see cref="`0"/>.
        /// </value>
        /// <param name="key">The key.</param>
        T? this[TKey key]
        {
            get;
            set;
        }

        /// <summary>
        /// Clears this instance.
        /// </summary>
        void Clear();

        /// <summary>
        /// Clears the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        void Clear(TKey key);

        /// <summary>
        /// Writes the specified entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="delete">if set to <c>true</c> [delete].</param>
        void Write(KeyValuePair<TKey, T?> entity, bool delete = false);

        /// <summary>
        /// Writes the specified entities.
        /// </summary>
        /// <param name="entities">The entities.</param>
        /// <param name="delete">if set to <c>true</c> [delete].</param>
        /// <param name="snapshot">if set to <c>true</c> [snapshot].</param>
        void Write(IEnumerable<KeyValuePair<
            TKey, T?>> entities, bool delete = false, bool snapshot = false);
    }
}