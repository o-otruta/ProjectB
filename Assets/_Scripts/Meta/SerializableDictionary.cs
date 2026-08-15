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
    public class SerializableDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        [SerializeField] private List<TKey> keys = new List<TKey>();
        [SerializeField] private List<TValue> values = new List<TValue>();

        public int Count => keys.Count;

        public TValue this[TKey key]
        {
            get
            {
                int index = keys.IndexOf(key);
                if (index >= 0)
                {
                    return values[index];
                }
                throw new KeyNotFoundException($"Key '{key}' not found.");
            }
            set
            {
                int index = keys.IndexOf(key);
                if (index >= 0)
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
            if (ContainsKey(key))
            {
                throw new ArgumentException($"An element with the same key '{key}' already exists.");
            }
            keys.Add(key);
            values.Add(value);
        }

        public bool ContainsKey(TKey key)
        {
            return keys.Contains(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            int index = keys.IndexOf(key);
            if (index >= 0)
            {
                value = values[index];
                return true;
            }
            value = default;
            return false;
        }

        public bool Remove(TKey key)
        {
            int index = keys.IndexOf(key);
            if (index >= 0)
            {
                keys.RemoveAt(index);
                values.RemoveAt(index);
                return true;
            }
            return false;
        }

        public void Clear()
        {
            keys.Clear();
            values.Clear();
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
