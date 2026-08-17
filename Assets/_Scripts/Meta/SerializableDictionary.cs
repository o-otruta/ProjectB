using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectB.Meta
{
    /// <summary>
    /// Обертка для сериализации словаря в JSON средствами Unity (JsonUtility).
    /// </summary>
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<TKey> keys = new List<TKey>();
        [SerializeField] private List<TValue> values = new List<TValue>();

        private Dictionary<TKey, int> indexCache = new Dictionary<TKey, int>();

        public int Count => keys.Count;

        public void OnBeforeSerialize()
        {
            // Do nothing, lists are already synced during runtime operations
        }

        public void OnAfterDeserialize()
        {
            indexCache.Clear();
            for (int i = 0; i < keys.Count; i++)
            {
                if (!indexCache.ContainsKey(keys[i]))
                {
                    indexCache.Add(keys[i], i);
                }
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                if (indexCache.TryGetValue(key, out int index))
                {
                    return values[index];
                }
                throw new KeyNotFoundException($"Key '{key}' not found.");
            }
            set
            {
                if (indexCache.TryGetValue(key, out int index))
                {
                    values[index] = value;
                }
                else
                {
                    Add(key, value);
                }
            }
        }

        public void Add(TKey key, TValue value)
        {
            if (indexCache.ContainsKey(key))
            {
                throw new ArgumentException($"An element with the same key '{key}' already exists.");
            }
            keys.Add(key);
            values.Add(value);
            indexCache.Add(key, keys.Count - 1);
        }

        public bool ContainsKey(TKey key)
        {
            return indexCache.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (indexCache.TryGetValue(key, out int index))
            {
                value = values[index];
                return true;
            }
            value = default;
            return false;
        }

        public bool Remove(TKey key)
        {
            if (indexCache.TryGetValue(key, out int index))
            {
                keys.RemoveAt(index);
                values.RemoveAt(index);
                // Rebuild cache because indices have shifted
                OnAfterDeserialize();
                return true;
            }
            return false;
        }

        public void Clear()
        {
            keys.Clear();
            values.Clear();
            indexCache.Clear();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            for (int i = 0; i < keys.Count; i++)
            {
                yield return new KeyValuePair<TKey, TValue>(keys[i], values[i]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
