namespace Microsoft.WindowsAzure.Governance.DataLabs.Common.Core.Utils
{
    using System.Collections;
    using System.Collections.Generic;

    /* At this time, below doesn't implement full IDictionary interfaces
     * We can implement it later if we need full IDictionary interface */

    public class SimpleOrderedDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> where TKey : notnull
    {
        private readonly List<KeyValuePair<TKey, TValue?>> _keyValueList;
        private readonly Dictionary<TKey, ValueIndexPair<TValue?>> _keyValueMap;
        private readonly IEqualityComparer<TKey>? _keyComparer;

        public SimpleOrderedDictionary(int capacity, IEqualityComparer<TKey>? keyComparer)
        {
            _keyValueMap = new Dictionary<TKey, ValueIndexPair<TValue?>>(capacity, keyComparer);
            _keyValueList = new List<KeyValuePair<TKey, TValue?>>(capacity);
            _keyComparer = keyComparer;
        }

        public void Clear()
        {
            _keyValueMap.Clear();
            _keyValueList.Clear();
        }

        public ICollection<TKey> Keys => _keyValueMap.Keys;

        public int Count => _keyValueList.Count;

        public bool IsReadOnly => false;

        public void Add(TKey key, TValue? value)
        {
            _keyValueMap.Add(key, new ValueIndexPair<TValue?>(value, _keyValueList.Count));
            _keyValueList.Add(new KeyValuePair<TKey, TValue?>(key, value));
        }

        public void Add(KeyValuePair<TKey, TValue?> item)
        {
            _keyValueMap.Add(item.Key, new ValueIndexPair<TValue?>(item.Value, _keyValueList.Count));
            _keyValueList.Add(item);
        }

        public bool ContainsKey(TKey key) => _keyValueMap.ContainsKey(key);

        public bool Contains(KeyValuePair<TKey, TValue?> item)
        {
            return TryGetValue(item.Key, out TValue? value) && Equals(item.Value, value);
        }

        public bool Remove(TKey key)
        {
            if (_keyValueMap.TryGetValue(key, out var vip))
            {
                _keyValueMap.Remove(key);
                _keyValueList.RemoveAt(vip.Index);
                return true;
            }
            return false;
        }
        public bool TryGetValue(TKey key, out TValue? value)
        {

            if (_keyValueMap.TryGetValue(key, out var vip))
            {
                value = vip.Value;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _keyValueList.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _keyValueList.GetEnumerator();
        }

        internal List<KeyValuePair<TKey, TValue?>> GetInternalKeyValueList => _keyValueList;

        public TValue? this[TKey key]
        {
            get
            {
                if (_keyValueMap.TryGetValue(key, out var vip))
                {
                    return vip.Value;
                }else
                {
                    throw new System.Collections.Generic.KeyNotFoundException();
                }
            }
            set
            {
                if (_keyValueMap.TryGetValue(key, out var vip))
                {
                    // Preserve original index
                    vip.Value = value;
                    _keyValueList[vip.Index] = new KeyValuePair<TKey, TValue?>(key, value);
                }
                else
                {
                    Add(key, value);
                }
            }
        }
    }

    internal class ValueIndexPair<TValue>
    {
        internal TValue? Value;
        internal readonly int Index;

        public ValueIndexPair(TValue? value, int index)
        {
            this.Value = value;
            this.Index = index;
        }
    }
}
