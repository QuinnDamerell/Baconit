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
    [JsonObject(MemberSerialization.OptOut)]
    public class SubredditAbout
    {

        /// <summary>
        /// The reddit's "About JSON" contains the Subreddit property as a
        /// object labeled "data"
        /// </summary>
        [JsonProperty(PropertyName = "data")]
        public Subreddit SubredditInfo { get; set; }

        /// <summary>
        /// The reddit json kind string
        /// </summary>
        [JsonProperty(PropertyName = "kind")]
        public string Kind { get; set; }
    }
}