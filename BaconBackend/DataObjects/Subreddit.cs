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
    public class Subreddit
    {
        /// <summary>
        /// The reddit defined display name for the subreddit
        /// </summary>
        [JsonProperty(PropertyName = "display_name")]
        public string DisplayName { get; set; }

        /// <summary>
        /// The id of the subreddit.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        /// <summary>
        /// The id of the subreddit.
        /// </summary>
        [JsonProperty(PropertyName = "public_description")]
        public string PublicDescription { get; set; }

        /// <summary>
        /// The title of the subreddit
        /// </summary>
        [JsonProperty(PropertyName = "title")]
        public string Title { get; set; }

        /// <summary>
        /// Indicates if this is a favorite or not.
        /// </summary>
        [JsonProperty(PropertyName = "isFavorite")]
        public bool IsFavorite { get; set; }

        /// <summary>
        /// The public markdown description for the subreddit.
        /// </summary>
        [JsonProperty(PropertyName = "description")]
        public string Description { get; set; }

        /// <summary>
        /// The number of subs this subreddit has
        /// </summary>
        [JsonProperty(PropertyName = "subscribers")]
        public int? SubscriberCount { get; set; } = null;
        
        /// <summary>
        /// The type of subreddit this is
        /// </summary>
        [JsonProperty(PropertyName = "subreddit_type")]
        public string SubredditType { get; set; }

        /// <summary>
        /// Indicates this is not a real subreddit, we made it up.
        /// </summary>
        [JsonProperty(PropertyName = "isArtifical")]
        public bool IsArtifical { get; set; } = false;        

        /// <summary>
        /// Uri to the favorite icon
        /// </summary>
        [JsonIgnore]
        public string FavIconUri { get; set; }

        /// <summary>
        /// Uri to the favorite icon
        /// </summary>
        [JsonIgnore]
        public SolidColorBrush SubTextBrush
        {
            get
            {
                if(IsFavorite)
                {
                    SolidColorBrush accentBrush = (SolidColorBrush)Application.Current.Resources["SystemControlBackgroundAccentBrush"];
                    Color accentcolor = accentBrush.Color;
                    accentcolor.B = (byte)Math.Min(255, accentBrush.Color.B + 50);
                    accentcolor.R = (byte)Math.Min(255, accentBrush.Color.R + 50);
                    accentcolor.G = (byte)Math.Min(255, accentBrush.Color.G + 50);
                    return accentBrush;
                }
                else
                {
                    return new SolidColorBrush(Color.FromArgb(153, 255, 255, 255));
                }
            }
        }

        public string GetNavigationUniqueId(SortTypes type, SortTimeTypes sortType)
        {
            return DisplayName + type + sortType;
        }
    }
}
