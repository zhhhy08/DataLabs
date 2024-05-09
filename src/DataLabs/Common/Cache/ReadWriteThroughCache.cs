namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Cache
{
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Extensions;
    using Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;

    /// <summary>
    /// ReadWriteThroughCache
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="T"></typeparam>
    public abstract class ReadWriteThroughCache<TKey, T> : IReadWriteThroughCache<TKey, T>
      where T : class
      where TKey : IEquatable<TKey>
    {
        #region Fields

        /// <summary>
        /// The _synclock
        /// </summary>
        private readonly object _synclock = new object();

        /// <summary>
        /// The _delegate map
        /// </summary>
        private readonly IDictionary<LambdaExpression, Delegate>
            _delegateMap = new Dictionary<LambdaExpression, Delegate>();



        #region ReadWrite Sync Delegates

        /// <summary>
        /// Gets or sets the entity key function.
        /// </summary>
        /// <value>
        /// The entity key function.
        /// </value>
        public readonly Func<T, TKey>? _entityKeyFunction;

        /// <summary>
        /// Gets or sets the fetch entity function.
        /// </summary>
        /// <value>
        /// The fetch entity function.
        /// </value>
        public readonly LambdaExpression? _fetchEntityFunction;

        /// <summary>
        /// Gets or sets the fetch entities function.
        /// </summary>
        /// <value>
        /// The fetch entities function.
        /// </value>
        public readonly LambdaExpression? _fetchEntitiesFunction;

        /// <summary>
        /// Gets or sets the post entities function.
        /// </summary>
        /// <value>
        /// The post entities function.
        /// </value>
        public readonly LambdaExpression? _postEntitiesFunction;

        /// <summary>
        /// Gets or sets the delete entities function.
        /// </summary>
        /// <value>
        /// The delete entities function.
        /// </value>
        public readonly LambdaExpression? _deleteEntitiesFunction;

        /// <summary>
        /// Gets or sets the fetch updated entities function.
        /// </summary>
        /// <value>
        /// The fetch updated entities function.
        /// </value>
        public readonly LambdaExpression? _fetchUpdatedEntitiesFunction;

        #endregion

        #region ReadWrite Async Delegates

        /// <summary>
        /// Gets or sets the fetch entity async function.
        /// </summary>
        /// <value>
        /// The fetch entity async function.
        /// </value>
        public readonly LambdaExpression? _fetchEntityAsyncFunction;

        /// <summary>
        /// Gets or sets the fetch entities async function.
        /// </summary>
        /// <value>
        /// The fetch entities async function.
        /// </value>
        public readonly LambdaExpression? _fetchEntitiesAsyncFunction;

        /// <summary>
        /// Gets or sets the post entities async function.
        /// </summary>
        /// <value>
        /// The post entities async function.
        /// </value>
        public readonly LambdaExpression? _postEntitiesAsyncFunction;

        /// <summary>
        /// Gets or sets the delete entities async function.
        /// </summary>
        /// <value>
        /// The delete entities async function.
        /// </value>
        public readonly LambdaExpression? _deleteEntitiesAsyncFunction;

        /// <summary>
        /// Gets or sets the fetch updated entities async function.
        /// </summary>
        /// <value>
        /// The fetch updated entities async function.
        /// </value>
        public readonly LambdaExpression? _fetchUpdatedEntitiesAsyncFunction;

        #endregion

        #endregion

        #region Constructor

        public ReadWriteThroughCache(Func<T, TKey>? entityKeyFunction = null, LambdaExpression? fetchEntityFunction = null,
            LambdaExpression? fetchEntitiesFunction = null, LambdaExpression? postEntitiesFunction = null, LambdaExpression? deleteEntitiesFunction = null,
            LambdaExpression? fetchUpdatedEntitiesFunction = null, LambdaExpression? fetchEntityAsyncFunction = null, LambdaExpression? fetchEntitiesAsyncFunction = null,
            LambdaExpression? postEntitiesAsyncFunction = null, LambdaExpression? deleteEntitiesAsyncFunction = null, LambdaExpression? fetchUpdatedEntitiesAsyncFunction = null)
        {
            this._entityKeyFunction = entityKeyFunction;
            this._fetchEntityFunction = fetchEntityFunction;
            this._fetchEntitiesFunction = fetchEntitiesFunction;
            this._postEntitiesFunction = postEntitiesFunction;
            this._deleteEntitiesFunction = deleteEntitiesFunction;
            this._fetchUpdatedEntitiesFunction = fetchUpdatedEntitiesFunction;
            this._fetchEntityAsyncFunction = fetchEntityAsyncFunction;
            this._fetchEntitiesAsyncFunction = fetchEntitiesAsyncFunction;
            this._postEntitiesAsyncFunction = postEntitiesAsyncFunction;
            this._deleteEntitiesAsyncFunction = deleteEntitiesAsyncFunction;
            this._fetchUpdatedEntitiesAsyncFunction = fetchUpdatedEntitiesAsyncFunction;
        }

        #endregion

        #region ICache Implementation

        /// <summary>
        /// Gets the count.
        /// </summary>
        /// <value>
        /// The count.
        /// </value>
        public abstract long Count
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
        public abstract T? this[TKey key]
        {
            get;
            set;
        }

        /// <summary>
        /// Clears this instance.
        /// </summary>
        public virtual void Clear()
        {
            this.Write(new KeyValuePair<TKey, T?>[0], false, true);
        }

        /// <summary>
        /// Clears the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        public virtual void Clear(TKey key)
        {
            this.Write(new KeyValuePair<TKey, T?>(key, null), true);
        }

        /// <summary>
        /// Writes the specified entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="delete">if set to <c>true</c> [delete].</param>
        public virtual void Write(KeyValuePair<TKey, T?> entity, bool delete = false)
        {
            this.Write(new[] { entity }, delete);
        }

        /// <summary>
        /// Writes the specified entities.
        /// </summary>
        /// <param name="entities">The entities.</param>
        /// <param name="delete">if set to <c>true</c> [delete].</param>
        /// <param name="snapshot">if set to <c>true</c> [snapshot].</param>
        public abstract void Write(IEnumerable<
            KeyValuePair<TKey, T?>> entities, bool delete = false, bool snapshot = false);

        #endregion

        #region IDispose Implementation

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        [SuppressMessage("Microsoft.Design",
            "CA1063:ImplementIDisposableCorrectly",
            Justification = "Current implementation is enough.")]
        public void Dispose()
        {
            this.HandleDispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Handles the dispose.
        /// </summary>
        protected virtual void HandleDispose()
        {
            this._delegateMap.Clear();
        }

        #endregion

        #region IReadWriteThroughCache Implementation

        #region Sync Methods

        /// <summary>
        /// Gets the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="args">The args.</param>
        public T? Get(TKey key, params object[] args)
        {
            var cachedEntity = this[key];
            return cachedEntity ?? this.Refresh(key, args);
        }

        /// <summary>
        /// Refreshes the specified parent activity.
        /// </summary>
        /// <param name="args">The args.</param>
        public IList<T> Refresh(params object[] args)
        {
            GuardHelper.ArgumentNotNull(this._entityKeyFunction);
            GuardHelper.ArgumentNotNull(this._fetchEntitiesFunction);

            var entities = this.InvokeDelegate(
                this._fetchEntitiesFunction, args) as IList<T>;

            entities = entities ?? Array.Empty<T>();
            this.Write(entities.Where(e => e != null).Select(e => new
                KeyValuePair<TKey, T?>(this._entityKeyFunction(e), e)), false, true);
            return entities;
        }

        /// <summary>
        /// Refreshes the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="args">The args.</param>
        public T? Refresh(TKey key, params object[] args)
        {
            GuardHelper.ArgumentNotNull(key);
            GuardHelper.ArgumentNotNull(this._fetchEntityFunction);

            var entity = this.InvokeDelegate(
                this._fetchEntityFunction, key, args) as T;
            this.Write(new KeyValuePair<TKey, T?>(key, entity));
            return entity;
        }

        /// <summary>
        /// Deletes the specified entities.
        /// </summary>
        /// <param name="entities">The entities.</param>
        /// <param name="args">The args.</param>
        public void Delete(IList<T> entities, params object[] args)
        {
            GuardHelper.ArgumentNotNull(this._entityKeyFunction);
            GuardHelper.ArgumentNotNull(this._deleteEntitiesFunction);

            this.InvokeDelegate(this._deleteEntitiesFunction, entities, args);

            this.Write(entities.Where(e => e != null).Select(e => new
                KeyValuePair<TKey, T?>(this._entityKeyFunction(e), e)), true);
        }

        /// <summary>
        /// Posts the specified entities.
        /// </summary>
        /// <param name="entities">The entities.</param>
        /// <param name="snapshot">if set to <c>true</c> [snapshot].</param>
        /// <param name="args">The args.</param>
        public void Post(
            IList<T> entities, bool snapshot = false, params object[] args)
        {
            GuardHelper.ArgumentNotNull(this._entityKeyFunction);
            GuardHelper.ArgumentNotNull(this._postEntitiesFunction);

            this.InvokeDelegate(this._postEntitiesFunction, entities, args);

            this.Write(entities.Where(e => e != null).Select(e => new
               KeyValuePair<TKey, T?>(this._entityKeyFunction(e), e)), false, snapshot);
        }

        /// <summary>
        /// Refreshes the specified watermark.
        /// </summary>
        /// <param name="watermark">The watermark.</param>
        /// <param name="currentTime">The current time.</param>
        /// <param name="args">The args.</param>
        public IList<T> Refresh(DateTime watermark, DateTime currentTime, params object[] args)
        {
            GuardHelper.ArgumentNotNull(this._entityKeyFunction);
            GuardHelper.ArgumentNotNull(this._fetchUpdatedEntitiesFunction);

            var entities = this.InvokeDelegate(
                this._fetchUpdatedEntitiesFunction,
                watermark, currentTime, args) as IList<T>;

            entities = entities ?? Array.Empty<T>();
            this.Write(entities.Where(e => e != null).Select(e =>
                new KeyValuePair<TKey, T?>(this._entityKeyFunction(e), e)));
            return entities;
        }

        #endregion

        #region Async Methods

        /// <summary>
        /// Gets the async.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="args">The args.</param>
        public async Task<T?> GetAsync(TKey key, params object[] args)
        {
            var cachedEntity = this[key];
            return cachedEntity ?? await this.RefreshAsync(key, args).IgnoreContext();
        }

        /// <summary>
        /// Refreshes the async.
        /// </summary>
        /// <param name="args">The args.</param>
        /// <exception cref="System.ArgumentException">fetchEntitiesAsyncFunction should return Task<IList<T>></exception>
        public async Task<IList<T>> RefreshAsync(params object[] args)
        {
            GuardHelper.ArgumentNotNull(this._entityKeyFunction);
            GuardHelper.ArgumentNotNull(this._fetchEntitiesAsyncFunction);

            if (!(this.InvokeDelegate(
                this._fetchEntitiesAsyncFunction, args) is Task<IList<T>> entitiesTask))
            {
                throw new ArgumentException("fetchEntitiesAsyncFunction");
            }

            var entities = await entitiesTask.IgnoreContext() ?? Array.Empty<T>();
            this.Write(entities.Where(e => e != null).Select(e => new
                KeyValuePair<TKey, T?>(this._entityKeyFunction(e), e)), false, true);
            return entities;
        }

        /// <summary>
        /// Refreshes the specified key async.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="args">The args.</param>
        /// <exception cref="System.ArgumentException">FetchEntityAsyncFunction should return Task<T></exception>
        public async Task<T?> RefreshAsync(TKey key, params object[] args)
        {
            GuardHelper.ArgumentNotNull(key);
            GuardHelper.ArgumentNotNull(this._fetchEntityAsyncFunction);

            if (!(this.InvokeDelegate(
               this._fetchEntityAsyncFunction, key, args) is Task<T> entityTask))
            {
                throw new ArgumentException("fetchEntityAsyncFunction");
            }

            var entity = await entityTask.IgnoreContext();
            this.Write(new KeyValuePair<TKey, T?>(key, entity));
            return entity;
        }

        /// <summary>
        /// Deletes async.
        /// </summary>
        /// <param name="entities">The entities.</param>
        /// <param name="args">The args.</param>
        public async Task DeleteAsync(IList<T> entities, params object[] args)
        {
            GuardHelper.ArgumentNotNull(this._entityKeyFunction);
            GuardHelper.ArgumentNotNull(this._deleteEntitiesAsyncFunction);

            if (!(this.InvokeDelegate(
                this._deleteEntitiesAsyncFunction, entities, args) is Task deleteTask))
            {
                throw new ArgumentException("deleteEntitiesAsyncFunction");
            }

            await deleteTask.IgnoreContext();
            this.Write(entities.Where(e => e != null).Select(e => new
               KeyValuePair<TKey, T?>(this._entityKeyFunction(e), e)), true);
        }

        /// <summary>
        /// Posts the async.
        /// </summary>
        /// <param name="entities">The entities.</param>
        /// <param name="snapshot">if set to <c>true</c> [snapshot].</param>
        /// <param name="args">The args.</param>
        public async Task PostAsync(
            IList<T> entities, bool snapshot = false, params object[] args)
        {
            GuardHelper.ArgumentNotNull(this._entityKeyFunction);
            GuardHelper.ArgumentNotNull(this._postEntitiesAsyncFunction);

            if (!(this.InvokeDelegate(
                this._postEntitiesAsyncFunction, entities, args) is Task postTask))
            {
                throw new ArgumentException("postEntitiesAsyncFunction");
            }

            await postTask.IgnoreContext();
            this.Write(entities.Where(e => e != null).Select(e => new
               KeyValuePair<TKey, T?>(this._entityKeyFunction(e), e)), false, snapshot);
        }

        /// <summary>
        /// Refreshes the async.
        /// </summary>
        /// <param name="watermark">The watermark.</param>
        /// <param name="currentTime">The current time.</param>
        /// <param name="args">The args.</param>
        /// <exception cref="System.ArgumentException">fetchEntitiesAsyncFunction</exception>
        public async Task<IList<T>> RefreshAsync(DateTime watermark, DateTime currentTime, params object[] args)
        {
            GuardHelper.ArgumentNotNull(this._entityKeyFunction);
            GuardHelper.ArgumentNotNull(this._fetchUpdatedEntitiesAsyncFunction);

            if (!(this.InvokeDelegate(
                this._fetchUpdatedEntitiesAsyncFunction,
                watermark, currentTime, args) is Task<IList<T>> entitiesTask))
            {
                throw new ArgumentException("FetchUpdatedEntitiesAsyncFunction");
            }

            var entities = await entitiesTask.IgnoreContext() ?? Array.Empty<T>();
            this.Write(entities.Where(e => e != null).Select(e =>
                new KeyValuePair<TKey, T?>(this._entityKeyFunction(e), e)));
            return entities;
        }

        #endregion

        #endregion

        /// <summary>
        /// Invokes the delegate.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="args">The args.</param>
        private object? InvokeDelegate(LambdaExpression expression, params object[] args)
        {
            GuardHelper.ArgumentNotNull(expression);

            if (!this._delegateMap.TryGetValue(
                expression, out var delegateToInvoke))
            {
                lock (this._synclock)
                {
                    if (!this._delegateMap.TryGetValue(
                        expression, out delegateToInvoke))
                    {
                        delegateToInvoke = expression.Compile();
                        this._delegateMap[expression] = delegateToInvoke;
                    }
                }
            }

            return delegateToInvoke.DynamicInvoke(args);
        }

        /// <summary>
        /// Invokes the delegate.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="arg">The arg.</param>
        /// <param name="args">The args.</param>
        private object? InvokeDelegate(LambdaExpression expression, object arg, params object[] args)
        {
            GuardHelper.ArgumentNotNull(arg);
            var finalArgs = new object[args != null ? args.Length + 1 : 1];
            finalArgs[0] = arg;
            for (var index = 1; index < finalArgs.Length; index++)
            {
                finalArgs[index] = args![index - 1];
            }
            return this.InvokeDelegate(expression, finalArgs);
        }

        /// <summary>
        /// Invokes the delegate.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="arg1">The arg1.</param>
        /// <param name="arg2">The arg2.</param>
        /// <param name="args">The args.</param>
        protected object? InvokeDelegate(LambdaExpression expression, object arg1, object arg2, params object[] args)
        {
            GuardHelper.ArgumentNotNull(arg1);
            GuardHelper.ArgumentNotNull(arg2);
            var finalArgs = new object[args != null ? args.Length + 2 : 2];
            finalArgs[0] = arg1;
            finalArgs[1] = arg2;
            for (var index = 2; index < finalArgs.Length; index++)
            {
                finalArgs[index] = args![index - 2];
            }
            return this.InvokeDelegate(expression, finalArgs);
        }
    }
}