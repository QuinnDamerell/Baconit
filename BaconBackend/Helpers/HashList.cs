using Newtonsoft.Json;
using System.Collections.Generic;

namespace BaconBackend.Helpers
{
    /// <summary>
    /// A length-capped hashtable that is ordered by the time keys were added, earliest first.
    /// When length-capping must happen, earlier added keys are deleted first.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class HashList<TKey, TValue>
    {
        /// <summary>
        /// The map used to hold the values
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        private Dictionary<TKey, TValue> _dictionary;

        /// <summary>
        /// The list used to preserve order
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public List<TKey> List;

        /// <summary>
        /// The max size the list can be
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public int MaxSize;

        /// <summary>
        /// Initializes a new, empty hash-list with capped length.
        /// </summary>
        /// <param name="maxSize">The maximum number of keys that can be in the hash-list at once.</param>
        public HashList(int maxSize)
        {
            _dictionary = new Dictionary<TKey, TValue>();
            List = new List<TKey>();
            MaxSize = maxSize;
        }

        /// <summary>
        /// Gets or sets the current value for the given key
        /// </summary>
        /// <param name="i">The key of the value to get or set.</param>
        /// <returns>The value associated with the specified key. If the specified key is not found, a get operation throws a KeyNotFoundException, and a set operation creates a new element with the specified key.</returns>
        public TValue this[TKey i]
        {
            get => _dictionary[i];
            set => Add(i, value);
        }

        /// <summary>
        /// Adds or updates the new element into the list and hash
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">The value of the element to add. The value can be null for reference types.</param>
        public void Add(TKey key, TValue value)
        {
            if (!_dictionary.ContainsKey(key))
            {
                List.Add(key);
                _dictionary.Add(key, value);

                while (List.Count > MaxSize)
                {
                    var remove = List[0];
                    _dictionary.Remove(remove);
                    List.Remove(remove);
                }
            }
            else
            {
                _dictionary[key] = value;
            }
        }

        /// <summary>
        /// Clears the hash-list
        /// </summary>
        public void Clear()
        {
            List.Clear();
            _dictionary.Clear();
        }
        
        /// <summary>
        /// Removes an item.
        /// </summary>
        /// <param name="item">The key of the element to remove.</param>
        public void Remove(TKey item)
        {
            _dictionary.Remove(item);
            List.Remove(item);
        }

        /// <summary>
        /// Returns if the collection contains the key.
        /// </summary>
        /// <param name="item">The key to locate in the hashtable.</param>
        /// <returns>If the hashtable contains an element with the specified key.</returns>
        public bool ContainsKey(TKey item)
        {
            return _dictionary.ContainsKey(item);
        }

        /// <summary>
        /// Gets the given position.
        /// </summary>
        /// <param name="pos">The position of the key in the list.</param>
        /// <returns>The key and value of the item at the specified position.</returns>
        public KeyValuePair<TKey, TValue> Get(int pos)
        {
            var item = List[pos];
            var value = _dictionary[item];
            return new KeyValuePair<TKey, TValue>(item, value);
        }

        /// <summary>
        /// Returns the count.
        /// </summary>
        public int Count => List.Count;
    }
}
