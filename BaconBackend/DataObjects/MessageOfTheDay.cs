using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.DataObjects
{
    [JsonObject(MemberSerialization.OptOut)]
    public class MessageOfTheDay
    {
        [JsonProperty(PropertyName = "title")]
        public string Title { get; set; }

        [JsonProperty(PropertyName = "markdown_content")]
        public string MarkdownContent { get; set; }

        [JsonProperty(PropertyName = "critical")]
        public bool isCritical { get; set; }

        [JsonProperty(PropertyName = "ignore")]
        public bool isIngore { get; set; }

        [JsonProperty(PropertyName = "min_open_times")]
        public int MinOpenTimes { get; set; }

        [JsonProperty(PropertyName = "unique_id")]
        public string UniqueId { get; set; }
    }
}
