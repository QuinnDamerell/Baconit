using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.Helpers
{
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

        public HashList(int maxSize)
        {
            m_dictonary = new Dictionary<Key, Value>();
            m_list = new List<Key>();
            m_maxSize = maxSize;
        }

        /// <summary>
        /// Returns or set the current value for the given key
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
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
        /// <param name="key"></param>
        /// <param name="value"></param>
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
        /// <param name="item"></param>
        public void Remove(Key item)
        {
            m_dictonary.Remove(item);
            m_list.Remove(item);
        }

        /// <summary>
        /// Returns if the collection contains the key.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool ContainsKey(Key item)
        {
            return m_dictonary.ContainsKey(item);
        }

        /// <summary>
        /// Gets the given position.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
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
