using BaconBackend.Managers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace BaconBackend.Helpers
{
    /// <summary>
    /// Helper classes used to help parse the Json.
    /// </summary>
    public class Element<Et>
    {
        /// <summary>
        /// Reddit item.
        /// </summary>
        [JsonProperty(PropertyName = "data")]
        public Et Data;

        /// <summary>
        /// Type prefix of Data's object type.
        /// </summary>
        [JsonProperty(PropertyName = "kind")]
        public string Kind;
    }

    /// <summary>
    /// Helper class that holds the list
    /// </summary>
    public class ElementList<Et>
    {
        /// <summary>
        /// List of elements.
        /// </summary>
        [JsonProperty(PropertyName = "children")]
        public List<Element<Et>> Children;

        /// <summary>
        /// The fullname of the last element in the list, or null if this is the end of the list.
        /// </summary>
        [JsonProperty(PropertyName = "after")]
        public string After = null;

        /// <summary>
        /// The fullname of the first element in the list, or null if there are no elements
        /// earlier in this list.
        /// </summary>
        [JsonProperty(PropertyName = "before")]
        public string Before = null;
    }

    /// <summary>
    /// Holds the root information
    /// </summary>
    public class RootElement<Et>
    {
        /// <summary>
        /// List of elements.
        /// </summary>
        [JsonProperty(PropertyName = "data")]
        public ElementList<Et> Data;

        /// <summary>
        /// Type prefix of the objects in the Data list.
        /// </summary>
        [JsonProperty(PropertyName = "kind")]
        public string Kind;
    }

    /// <summary>
    /// Used for special case roots that have a nameless array.
    /// </summary>
    public class ArrayRoot<Et>
    {
        /// <summary>
        /// List of all the elements under the root.
        /// </summary>
        [JsonProperty(PropertyName = "root")]
        public List<RootElement<Et>> Root;
    }


    /// <summary>
    /// This is a helper class designed to help with returning long
    /// reddit lists. Most lists on reddit have a limit (like subreddits and such)
    /// and if you want the entire thing it might take multiple calls.
    /// </summary>
    class RedditListHelper<T>
    {
        //
        // Private vars
        //
        string m_baseUrl;
        string m_optionalGetArgs;
        NetworkManager m_networkMan;
        int m_lastTopGet = 0;
        ElementList<T> m_currentElementList = new ElementList<T>();

        /// <summary>
        /// Indicates if the root of this object is an array, if so we will treat it as a list.
        /// </summary>
        bool m_isArrayRoot = false;

        /// <summary>
        /// If we are creating an empty root as above, this tells us which element in the created root to use.
        /// </summary>
        bool m_takeFirstArrayRoot = false;

        /// <summary>
        /// Create a new object to help build reddit lists.
        /// </summary>
        /// <param name="baseUrl">The URL with the information to populate this list.</param>
        /// <param name="netMan">An object to help make web requests.</param>
        /// <param name="isArrayRoot">If the object returned from reddit will have no root element 
        /// (and will therefore need modification to be correct JSON).</param>
        /// <param name="takeFirstArrayRoot">If the first child under the root is the one with the data (rather than the second child).</param>
        /// <param name="optionalGetArgs">Additional GET arguments to send when making the request to populate this list.</param>
        public RedditListHelper(string baseUrl, NetworkManager netMan, bool isArrayRoot = false, bool takeFirstArrayRoot = false, string optionalGetArgs = "")
        {
            m_baseUrl = baseUrl;
            m_optionalGetArgs = optionalGetArgs;
            m_currentElementList.Children = new List<Element<T>>();
            m_networkMan = netMan;
            m_isArrayRoot = isArrayRoot;
            m_takeFirstArrayRoot = takeFirstArrayRoot;
        }

        /// <summary>
        /// Fetches the next n number of elements from the source. If there aren't n left it will return how every
        /// many it can get. If the amount requested is more than what it current has it will try to fetch more.
        /// THIS IS NOT THREAD SAFE
        /// </summary>
        /// <param name="count">The number to get</param>
        /// <returns></returns>
        public async Task<List<Element<T>>> FetchNext(int count)
        {
            return await FetchElements(m_lastTopGet, m_lastTopGet + count);
        }

        /// <summary>
        /// Returns a range of elements from the source, if the elements are not local it will fetch them from the interwebs
        /// This can take multiple web calls to get the list, so this can be slow. If there aren't enough elements remaining
        /// we will return as many as we can get.
        /// THIS IS NOT THREAD SAFE
        /// </summary>
        /// <param name="bottom">The bottom range, inclusive</param>
        /// <param name="top">The top of the range, exclusive</param>
        /// <returns></returns>
        public async Task<List<Element<T>>> FetchElements(int bottom, int top)
        {
            if(top <= bottom)
            {
                throw new Exception("top can't be larger than bottom!");
            }

            int sanityCheckCount = 0;
            while (true)
            {
                // See if we now have what they asked for, OR the list has elements but we don't have an after.
                // (this is the case when we have hit the end of the list)
                // #bug!?!? At some point I changed the children count in the after check to sanityCheckCount == 0, but I can't remember why
                // and it breaks lists that have ends. There is some bug where something doesn't try to refresh or something...
                if (m_currentElementList.Children.Count >= top
                    || (m_currentElementList.Children.Count != 0 && m_currentElementList.After == null)
                    || (sanityCheckCount > 25))
                {
                    // Return what they asked for capped at the list size
                    int length = top - bottom;
                    int listLength = m_currentElementList.Children.Count - bottom;
                    length = Math.Min(length, listLength);

                    // Set what the top was we returned.
                    m_lastTopGet = bottom + length;
                    return m_currentElementList.Children.GetRange(bottom, length);
                }

                // Figure out how many we need still.
                int numberNeeded = top - m_currentElementList.Children.Count;

                // Make the request.
                IHttpContent webResult = await MakeRequest(numberNeeded, m_currentElementList.After);

                // This will hold the root
                RootElement<T> root = null;

                // Get the input stream and json reader.
                // NOTE!! We are really careful not to use a string here so we don't have to allocate a huge string.
                IInputStream inputStream = await webResult.ReadAsInputStreamAsync();
                using (StreamReader reader = new StreamReader(inputStream.AsStreamForRead()))
                using (JsonReader jsonReader = new JsonTextReader(reader))
                {
                    // Check if we have an array root or a object root
                    if (m_isArrayRoot)
                    {
                        // Parse the Json as an object
                        JsonSerializer serializer = new JsonSerializer();
                        List<RootElement<T>> arrayRoot = await Task.Run(() => serializer.Deserialize<List<RootElement<T>>>(jsonReader));

                        // Use which ever list element we want.
                        if (m_takeFirstArrayRoot)
                        {
                            root = arrayRoot[0];
                        }
                        else
                        {
                            root = arrayRoot[1];
                        }
                    }
                    else
                    {
                        // Parse the Json as an object
                        JsonSerializer serializer = new JsonSerializer();
                        root = await Task.Run(() => serializer.Deserialize<RootElement<T>>(jsonReader));
                    }
                }     

                // Copy the new contents into the current cache
                m_currentElementList.Children.AddRange(root.Data.Children);

                // Update the before and after
                m_currentElementList.After = root.Data.After;
                m_currentElementList.Before = root.Data.Before;
                sanityCheckCount++;
            }
        }

        /// <summary>
        /// Returns what elements the helper currently has without fetching more.
        /// THIS IS NOT THREAD SAFE
        /// </summary>
        /// <returns>Returns the elements</returns>
        public List<Element<T>> GetCurrentElements()
        {
            return m_currentElementList.Children;
        }

        /// <summary>
        /// Adds a fake element to the collection such as a user comment reply.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="element"></param>
        public void AddFakeElement(int pos, T element)
        {
            //m_currentElementList.Children.Insert(pos, element);
        }

        /// <summary>
        /// Clears the current list.
        /// </summary>
        public void Clear()
        {
            m_currentElementList.After = "";
            m_currentElementList.Before = "";
            m_currentElementList.Children.Clear();
        }

        private async Task<IHttpContent> MakeRequest(int limit, string after)
        {
            string optionalEnding = String.IsNullOrWhiteSpace(m_optionalGetArgs) ? String.Empty : "&"+ m_optionalGetArgs;
            string url = m_baseUrl + $"?limit={limit}" + (String.IsNullOrWhiteSpace(after) ? "" : $"&after={after}") + optionalEnding;
            return await m_networkMan.MakeRedditGetRequest(url);
        }
    }
}
