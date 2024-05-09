namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Cache
{
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading;

    /// <summary>
    /// A cache with expiration and size limitation, MemoryCache internally uses a ConcurrentDicitionary
    /// This cache should not be used for caches with millions of items.
    /// </summary>
    public class InMemoryCacheWithExpiration<T> : ReadWriteThroughCache<string, T> where T : class
    {
        #region Members

        private static readonly PropertyInfo? MemoryCacheSizeProperty = typeof(MemoryCache).GetProperty("Size", BindingFlags.NonPublic | BindingFlags.Instance);

        #endregion

        #region Fields

        /// <summary>
        /// The memory cache
        /// </summary>
        private MemoryCache _memoryCache;

        private readonly MemoryCacheOptions _memoryCacheConfig;

        private readonly Func<T, long>? _getSizeFunc;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the cached entity expiration.
        /// </summary>
        /// <value>
        /// The cached entity expiration.
        /// </value>
        public TimeSpan CachedEntityExpiration
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets the entity removed callback.
        /// </summary>
        /// <value>
        /// The entity removed callback.
        /// </value>
        public Action<T>? EntityRemovedCallback
        {
            get;
            set;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryCacheWithExpiration{T}" /> class.
        /// </summary>
        /// <param name="cacheExpiration">The cache expiration.</param>
        public InMemoryCacheWithExpiration(TimeSpan cacheExpiration, long? cacheSizeLimit = null, TimeSpan? pollingInterval = null, Func<T, long>? getSizeFunc = null,
            Func<T, string>? entityKeyFunction = null, LambdaExpression? fetchEntityFunction = null,
            LambdaExpression? fetchEntitiesFunction = null, LambdaExpression? postEntitiesFunction = null, LambdaExpression? deleteEntitiesFunction = null,
            LambdaExpression? fetchUpdatedEntitiesFunction = null, LambdaExpression? fetchEntityAsyncFunction = null, LambdaExpression? fetchEntitiesAsyncFunction = null,
            LambdaExpression? postEntitiesAsyncFunction = null, LambdaExpression? deleteEntitiesAsyncFunction = null, LambdaExpression? fetchUpdatedEntitiesAsyncFunction = null)
            : base(entityKeyFunction, fetchEntityFunction, fetchEntitiesFunction, postEntitiesFunction, deleteEntitiesFunction, fetchUpdatedEntitiesFunction,
                  fetchEntityAsyncFunction, fetchEntitiesAsyncFunction, postEntitiesAsyncFunction, deleteEntitiesAsyncFunction, fetchUpdatedEntitiesAsyncFunction)
        {
            GuardHelper.ArgumentConstraintCheck(ConstraintCheck(cacheSizeLimit, pollingInterval, getSizeFunc),
                "all arguments must be null or non-null");
            bool ConstraintCheck(long? cacheSizeLimitConstraint, TimeSpan? pollingIntervalConstraint, Func<T, long>? getSizeFuncConstraint) =>
                (cacheSizeLimitConstraint != null) == (pollingIntervalConstraint != null) &&
                    (pollingIntervalConstraint != null) == (getSizeFuncConstraint != null);

            this.CachedEntityExpiration = cacheExpiration;
            this._memoryCacheConfig = cacheSizeLimit == null || pollingInterval == null ? new MemoryCacheOptions() :
                new MemoryCacheOptions()
                {
                    ExpirationScanFrequency = pollingInterval.Value,
                    SizeLimit = cacheSizeLimit.Value
                };
            this._getSizeFunc = getSizeFunc;
            this._memoryCache = new MemoryCache(this._memoryCacheConfig);
        }

        #endregion

        #region ICache Implementation

        /// <summary>
        /// Gets the count.
        /// </summary>
        /// <value>
        /// The count.
        /// </value>
        public override long Count => this._memoryCache.Count;

        public long MemoryCacheSize
        {
            get
            {
                try
                {
                    // NOTE: Size property is intended for internal testing in CLR
                    var sizeValue = MemoryCacheSizeProperty?.GetValue(this._memoryCache);
                    if (sizeValue != null && sizeValue.GetType() == typeof(long))
                    {
                        return (long)sizeValue;
                    }
                    return 0L;
                }
                catch (Exception)
                {
                    return 0L;
                }
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="`0"/> with the specified key.
        /// </summary>
        /// <value>
        /// The <see cref="`0"/>.
        /// </value>
        /// <param name="key">The key.</param>
        public override T? this[string key]
        {
            get
            {
                GuardHelper.ArgumentNotNullOrEmpty(key);

                return this._memoryCache.Get<T>(key);
            }
            set
            {
                GuardHelper.ArgumentNotNullOrEmpty(key);

                this.Write(new KeyValuePair<string, T?>(key, value));
            }
        }

        /// <summary>
        /// Set the value for a key in the cache for a given expiration period
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="entityExpiration">The expiration timespan</param>
        /// <param name="value">The value to cache</param>
        public void Set(string key, TimeSpan entityExpiration, T? value)
        {
            GuardHelper.ArgumentNotNullOrEmpty(key);
            this.Write(new[] { new KeyValuePair<string, T?>(key, value) }, entityExpiration);
        }

        /// <summary>
        /// Writes the specified entities.
        /// </summary>
        /// <param name="entities">The entities.</param>
        /// <param name="delete">if set to <c>true</c> [delete].</param>
        /// <param name="snapshot">if set to <c>true</c> [snapshot].</param>
        public override void Write(IEnumerable<KeyValuePair<
            string, T?>> entities, bool delete = false, bool snapshot = false)
        {
            this.Write(entities, this.CachedEntityExpiration, delete, snapshot);
        }

        #endregion

        #region IDispose Implementation

        /// <summary>
        /// Handles the dispose.
        /// </summary>
        protected override void HandleDispose()
        {
            this._memoryCache.Dispose();

            base.HandleDispose();
        }

        #endregion

        /// <summary>
        /// Writes the specified entities.
        /// </summary>
        /// <param name="entities">The entities.</param>
        /// <param name="cachedEntityExpiration">The cached entity expiration.</param>
        /// <param name="delete">if set to <c>true</c> [delete].</param>
        /// <param name="snapshot">if set to <c>true</c> [snapshot].</param>
        private void Write(IEnumerable<KeyValuePair<string, T?>>
            entities, TimeSpan cachedEntityExpiration, bool delete = false, bool snapshot = false)
        {
            var memoryCache = this._memoryCache;
            if (snapshot)
            {
                memoryCache = new MemoryCache(this._memoryCacheConfig);
            }

            Write(memoryCache, entities, cachedEntityExpiration, delete,
                this.EntityRemovedCallback, this._getSizeFunc);
            if (snapshot)
            {
                var oldMemoryCache = this._memoryCache;
                // Swap read only and write caches
                Interlocked.Exchange(ref this._memoryCache, memoryCache);

                oldMemoryCache.Dispose();
            }
        }

        /// <summary>
        /// Writes the specified memory cache.
        /// </summary>
        /// <param name="memoryCache">The memory cache.</param>
        /// <param name="entities">The entities.</param>
        /// <param name="cachedEntityExpiration">The cached entity expiration.</param>
        /// <param name="delete">if set to <c>true</c> [delete].</param>
        /// <param name="entityRemovedCallback">The entity removed callback.</param>
        private static void Write(MemoryCache memoryCache,
            IEnumerable<KeyValuePair<string, T?>> entities, TimeSpan cachedEntityExpiration,
            bool delete, Action<T>? entityRemovedCallback, Func<T, long>? getSizeFunc)
        {
            if (!delete)
            {
                foreach (var entity in entities)
                {
                    if (entity.Value == null)
                    {
                        // Memory cache throws on adding null value
                        memoryCache.Remove(entity.Key);
                    }
                    else
                    {
                        var cacheItem = memoryCache.CreateEntry(entity.Key);
                        cacheItem.Value = entity.Value;
                        cacheItem.AbsoluteExpiration =
                                new DateTimeOffset(DateTime.Now.Add(cachedEntityExpiration));
                        if (entityRemovedCallback != null)
                        {
                            cacheItem.RegisterPostEvictionCallback((object key, object? value, EvictionReason reason, object? state) =>
                                entityRemovedCallback((value as T)!));
                        }
                        if (getSizeFunc != null)
                        {
                            cacheItem.Size = getSizeFunc(entity.Value);
                        }
                        cacheItem.Dispose();
                    }
                }
            }
            else
            {
                foreach (var kvp in entities)
                {
                    memoryCache.Remove(kvp.Key);
                }
            }
        }
    }
}