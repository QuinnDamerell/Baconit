using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.DataObjects
{
    /// <summary>
    /// A Reddit user, with a unique username.
    /// </summary>
    [JsonObject(MemberSerialization.OptOut)]
    public class User
    {
        /// <summary>
        /// The user name of the account
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// The Id of the user account
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }        

        /// <summary>
        /// The creation time of the account
        /// </summary>
        [JsonProperty(PropertyName = "created_utc")]
        public double CreatedUtc { get; set; }

        /// <summary>
        /// Indicates if the user has mail
        /// </summary>
        [JsonProperty(PropertyName = "has_mail")]
        public bool HasMail { get; set; }
        
        /// <summary>
        /// The number of unread messages in this user's inbox.
        /// </summary>
        [JsonProperty(PropertyName = "inbox_count")]
        public int InboxCount { get; set; }

        /// <summary>
        /// The number of gold creddits the logged in user currently has.
        /// </summary>
        [JsonProperty(PropertyName = "gold_creddits")]
        public int GoldCredits { get; set; }

        /// <summary>
        /// If the user has access to modmail.
        /// </summary>
        [JsonProperty(PropertyName = "has_mod_mail")]
        public bool HasModMail { get; set; }

        /// <summary>
        /// How much karma the user has gained from links.
        /// </summary>
        [JsonProperty(PropertyName = "link_karma")]
        public int LinkKarma { get; set; }

        /// <summary>
        /// How much karma the user has gained from comments.
        /// </summary>
        [JsonProperty(PropertyName = "comment_karma")]
        public int CommentKarma { get; set; }

        /// <summary>
        /// If the user is over 18 years old.
        /// </summary>
        [JsonProperty(PropertyName = "over_18")]
        public bool IsOver18 { get; set; }

        /// <summary>
        /// If the user has gold creddits.
        /// </summary>
        [JsonProperty(PropertyName = "is_gold")]
        public bool IsGold { get; set; }

        /// <summary>
        /// If the user is a moderator of a subreddit.
        /// </summary>
        [JsonProperty(PropertyName = "is_mod")]
        public bool IsMod { get; set; }
    }
}
