namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Cache
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// IReadWriteThroughCache
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="T"></typeparam>
    public interface IReadWriteThroughCache<TKey, T> : ICache<TKey, T>
        where T : class
        where TKey : IEquatable<TKey>
    {
        #region Sync Methods

        /// <summary>
        /// Gets the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="args">The args.</param>
        T? Get(TKey key, params object[] args);

        /// <summary>
        /// Refreshes the entire cache.
        /// </summary>
        /// <param name="args">The args.</param>
        IList<T> Refresh(params object[] args);

        /// <summary>
        /// Refreshes the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="args">The args.</param>
        T? Refresh(TKey key, params object[] args);

        /// <summary>
        /// Deletes the specified entities.
        /// </summary>
        /// <param name="entities">The entities.</param>
        /// <param name="args">The args.</param>
        void Delete(IList<T> entities, params object[] args);

        /// <summary>
        /// Posts the specified entities.
        /// </summary>
        /// <param name="entities">The entities.</param>
        /// <param name="snapshot">if set to <c>true</c> [snapshot].</param>
        /// <param name="args">The args.</param>
        void Post(IList<T>
            entities, bool snapshot = false, params object[] args);

        /// <summary>
        /// Refreshes the specified watermark.
        /// </summary>
        /// <param name="watermark">The watermark.</param>
        /// <param name="currentTime">The current time.</param>
        /// <param name="args">The args.</param>
        IList<T> Refresh(DateTime watermark, DateTime currentTime, params object[] args);

        #endregion

        #region Async Methods

        /// <summary>
        /// Gets async.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="args">The args.</param>
        Task<T?> GetAsync(TKey key, params object[] args);

        /// <summary>
        /// Refreshes async.
        /// </summary>
        /// <param name="args">The args.</param>
        Task<IList<T>> RefreshAsync(params object[] args);

        /// <summary>
        /// Refreshes async.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="args">The args.</param>
        Task<T?> RefreshAsync(TKey key, params object[] args);

        /// <summary>
        /// Deletes async.
        /// </summary>
        /// <param name="entities">The entities.</param>
        /// <param name="args">The args.</param>
        Task DeleteAsync(IList<T> entities, params object[] args);

        /// <summary>
        /// Posts async.
        /// </summary>
        /// <param name="entities">The entities.</param>
        /// <param name="snapshot">if set to <c>true</c> [snapshot].</param>
        /// <param name="args">The args.</param>
        Task PostAsync(IList<T>
            entities, bool snapshot = false, params object[] args);

        /// <summary>
        /// Refreshes the async.
        /// </summary>
        /// <param name="watermark">The watermark.</param>
        /// <param name="currentTime">The current time.</param>
        /// <param name="args">The args.</param>
        Task<IList<T>> RefreshAsync(DateTime watermark, DateTime currentTime, params object[] args);

        #endregion
    }
}