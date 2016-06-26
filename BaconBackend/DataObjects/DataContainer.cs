using BaconBackend.Collectors;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace BaconBackend.DataObjects 
{
    /// <summary>
    /// A generic Reddit json response containing a data object.
    /// </summary>
    [JsonObject(MemberSerialization.OptOut)]
    public class DataContainer<T>
    {

        /// <summary>
        /// Object labeled "data" from the response
        /// </summary>
        [JsonProperty(PropertyName = "data")]
        public T Data { get; set; }

        /// <summary>
        /// The reddit json kind string
        /// </summary>
        [JsonProperty(PropertyName = "kind")]
        public string Kind { get; set; }
    }
}