using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.DataObjects
{
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
        
        [JsonProperty(PropertyName = "inbox_count")]
        public int InboxCount { get; set; }

        [JsonProperty(PropertyName = "gold_creddits")]
        public int GoldCredits { get; set; }

        [JsonProperty(PropertyName = "has_mod_mail")]
        public bool HasModMail { get; set; }

        [JsonProperty(PropertyName = "link_karma")]
        public int LinkKarma { get; set; }

        [JsonProperty(PropertyName = "comment_karma")]
        public int CommentKarma { get; set; }

        [JsonProperty(PropertyName = "over_18")]
        public bool IsOver18 { get; set; }

        [JsonProperty(PropertyName = "is_gold")]
        public bool IsGold { get; set; }

        [JsonProperty(PropertyName = "is_mod")]
        public bool IsMod { get; set; }
    }
}
