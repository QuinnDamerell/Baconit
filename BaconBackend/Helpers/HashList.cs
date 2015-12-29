using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.Helpers
{
    /// <summary>
    /// A length-capped hashtable that is ordered by the time keys were added, earliest first.
    /// When length-capping must happen, earlier added keys are deleted first.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class HashList<Key, Value>
    {
        /// <summary>
        /// The map used to hold the values
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        private Dictionary<Key, Value> m_dictonary = null;

        /// <summary>
        /// The list used to preserve order
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public List<Key> m_list = null;

        /// <summary>
        /// The max size the list can be
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public int m_maxSize = 0;

        /// <summary>
        /// Initializes a new, empty hashlist with capped length.
        /// </summary>
        /// <param name="maxSize">The maximum number of keys that can be in the hashlist at once.</param>
        public HashList(int maxSize)
        {
            m_dictonary = new Dictionary<Key, Value>();
            m_list = new List<Key>();
            m_maxSize = maxSize;
        }

        /// <summary>
        /// Gets or sets the current value for the given key
        /// </summary>
        /// <param name="i">The key of the value to get or set.</param>
        /// <returns>The value associated with the specified key. If the specified key is not found, a get operation throws a KeyNotFoundException, and a set operation creates a new element with the specified key.</returns>
        public Value this[Key i]
        {
            get { return m_dictonary[i]; }
            set
            {
                Add(i, value);
            }
        }

        /// <summary>
        /// Adds or updates the new element into the list and hash
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">The value of the element to add. The value can be null for reference types.</param>
        public void Add(Key key, Value value)
        {
            if (!m_dictonary.ContainsKey(key))
            {
                m_list.Add(key);
                m_dictonary.Add(key, value);

                while (m_list.Count > m_maxSize)
                {
                    Key remove = m_list[0];
                    m_dictonary.Remove(remove);
                    m_list.Remove(remove);
                }
            }
            else
            {
                m_dictonary[key] = value;
            }
        }

        /// <summary>
        /// Clears the hashlist
        /// </summary>
        public void Clear()
        {
            m_list.Clear();
            m_dictonary.Clear();
        }
        
        /// <summary>
        /// Removes an item.
        /// </summary>
        /// <param name="item">The key of the element to remove.</param>
        public void Remove(Key item)
        {
            m_dictonary.Remove(item);
            m_list.Remove(item);
        }

        /// <summary>
        /// Returns if the collection contains the key.
        /// </summary>
        /// <param name="item">The key to locate in the hashtable.</param>
        /// <returns>If the hashtable contains an element with the specified key.</returns>
        public bool ContainsKey(Key item)
        {
            return m_dictonary.ContainsKey(item);
        }

        /// <summary>
        /// Gets the given position.
        /// </summary>
        /// <param name="pos">The position of the key in the list.</param>
        /// <returns>The key and value of the item at the specified position.</returns>
        public KeyValuePair<Key, Value> Get(int pos)
        {
            Key item = m_list[pos];
            Value value = m_dictonary[item];
            return new KeyValuePair<Key, Value>(item, value);
        }

        /// <summary>
        /// Returns the count.
        /// </summary>
        public int Count
        {
            get
            {
                return m_list.Count; 
            }
        }
    }
}
