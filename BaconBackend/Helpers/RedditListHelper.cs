using BaconBackend.Managers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Web.Http;

namespace BaconBackend.Helpers
{
    /// <summary>
    /// Helper classes used to help parse the Json.
    /// </summary>
    public class Element<TEt>
    {
        /// <summary>
        /// Reddit item.
        /// </summary>
        [JsonProperty(PropertyName = "data")]
        public TEt Data;

        /// <summary>
        /// Type prefix of Data object type.
        /// </summary>
        [JsonProperty(PropertyName = "kind")]
        public string Kind;
    }

    /// <summary>
    /// Helper class that holds the list
    /// </summary>
    public class ElementList<TEt>
    {
        /// <summary>
        /// List of elements.
        /// </summary>
        [JsonProperty(PropertyName = "children")]
        public List<Element<TEt>> Children;

        /// <summary>
        /// The fullname of the last element in the list, or null if this is the end of the list.
        /// </summary>
        [JsonProperty(PropertyName = "after")]
        public string After;

        /// <summary>
        /// The fullname of the first element in the list, or null if there are no elements
        /// earlier in this list.
        /// </summary>
        [JsonProperty(PropertyName = "before")]
        public string Before;
    }

    /// <summary>
    /// Holds the root information
    /// </summary>
    public class RootElement<TEt>
    {
        /// <summary>
        /// List of elements.
        /// </summary>
        [JsonProperty(PropertyName = "data")]
        public ElementList<TEt> Data;

        /// <summary>
        /// Type prefix of the objects in the Data list.
        /// </summary>
        [JsonProperty(PropertyName = "kind")]
        public string Kind;
    }

    /// <summary>
    /// Used for special case roots that have a nameless array.
    /// </summary>
    public class ArrayRoot<TEt>
    {
        /// <summary>
        /// List of all the elements under the root.
        /// </summary>
        [JsonProperty(PropertyName = "root")]
        public List<RootElement<TEt>> Root;
    }


    /// <summary>
    /// This is a helper class designed to help with returning long
    /// reddit lists. Most lists on reddit have a limit (like subreddits and such)
    /// and if you want the entire thing it might take multiple calls.
    /// </summary>
    internal class RedditListHelper<T>
    {
        //
        // Private vars
        //
        private readonly string _baseUrl;
        private readonly string _optionalGetArgs;
        private readonly NetworkManager _networkMan;
        private int _lastTopGet;
        private readonly ElementList<T> _currentElementList = new ElementList<T>();

        /// <summary>
        /// Indicates if the root of this object is an array, if so we will treat it as a list.
        /// </summary>
        private readonly bool _isArrayRoot;

        /// <summary>
        /// If we are creating an empty root as above, this tells us which element in the created root to use.
        /// </summary>
        private readonly bool _takeFirstArrayRoot;

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
            _baseUrl = baseUrl;
            _optionalGetArgs = optionalGetArgs;
            _currentElementList.Children = new List<Element<T>>();
            _networkMan = netMan;
            _isArrayRoot = isArrayRoot;
            _takeFirstArrayRoot = takeFirstArrayRoot;
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
            return await FetchElements(_lastTopGet, _lastTopGet + count);
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

            var sanityCheckCount = 0;
            while (true)
            {
                // See if we now have what they asked for, OR the list has elements but we don't have an after.
                // (this is the case when we have hit the end of the list)
                // #bug!?!? At some point I changed the children count in the after check to sanityCheckCount == 0, but I can't remember why
                // and it breaks lists that have ends. There is some bug where something doesn't try to refresh or something...
                if (_currentElementList.Children.Count >= top
                    || (_currentElementList.Children.Count != 0 && _currentElementList.After == null)
                    || (sanityCheckCount > 25))
                {
                    // Return what they asked for capped at the list size
                    var length = top - bottom;
                    var listLength = _currentElementList.Children.Count - bottom;
                    length = Math.Min(length, listLength);

                    // Set what the top was we returned.
                    _lastTopGet = bottom + length;
                    return _currentElementList.Children.GetRange(bottom, length);
                }

                // Figure out how many we need still.
                var numberNeeded = top - _currentElementList.Children.Count;

                // Make the request.
                var webResult = await MakeRequest(numberNeeded, _currentElementList.After);

                // This will hold the root
                RootElement<T> root;

                // Get the input stream and json reader.
                // NOTE!! We are really careful not to use a string here so we don't have to allocate a huge string.
                var inputStream = await webResult.ReadAsInputStreamAsync();
                using (var reader = new StreamReader(inputStream.AsStreamForRead()))
                using (JsonReader jsonReader = new JsonTextReader(reader))
                {
                    // Check if we have an array root or a object root
                    if (_isArrayRoot)
                    {
                        // Parse the Json as an object
                        var serializer = new JsonSerializer();
                        var arrayRoot = await Task.Run(() => serializer.Deserialize<List<RootElement<T>>>(jsonReader));

                        // Use which ever list element we want.
                        root = _takeFirstArrayRoot ? arrayRoot[0] : arrayRoot[1];
                    }
                    else
                    {
                        // Parse the Json as an object
                        var serializer = new JsonSerializer();
                        //var val = await jsonReader.ReadAsStringAsync();
                        root = await Task.Run(() => serializer.Deserialize<RootElement<T>>(jsonReader));
                    }
                }

             
                // Copy the new contents into the current cache
                _currentElementList.Children.AddRange(root.Data.Children);

                // Update the before and after
                _currentElementList.After = root.Data.After;
                _currentElementList.Before = root.Data.Before;
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
            return _currentElementList.Children;
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
            _currentElementList.After = "";
            _currentElementList.Before = "";
            _currentElementList.Children.Clear();
        }

        private async Task<IHttpContent> MakeRequest(int limit, string after)
        {
            var optionalEnding = string.IsNullOrWhiteSpace(_optionalGetArgs) ? string.Empty : "&"+ _optionalGetArgs;
            var url = _baseUrl + $"?limit={limit}&raw_json=1" + (string.IsNullOrWhiteSpace(after) ? string.Empty : $"&after={after}") + optionalEnding;
            return await _networkMan.MakeRedditGetRequest(url);
        }
    }
}
