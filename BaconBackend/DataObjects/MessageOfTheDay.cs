using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.DataObjects
{
    /// <summary>
    /// A message from the app developer that should be displayed to the client.
    /// </summary>
    [JsonObject(MemberSerialization.OptOut)]
    public class MessageOfTheDay
    {
        /// <summary>
        /// The title of the message.
        /// </summary>
        [JsonProperty(PropertyName = "title")]
        public string Title { get; set; }

        /// <summary>
        /// The body of the message, in Markdown.
        /// </summary>
        [JsonProperty(PropertyName = "markdown_content")]
        public string MarkdownContent { get; set; }

        /// <summary>
        /// If the message is critically important.
        /// </summary>
        [JsonProperty(PropertyName = "critical")]
        public bool isCritical { get; set; }

        /// <summary>
        /// If the app shouldn't display the message.
        /// </summary>
        [JsonProperty(PropertyName = "ignore")]
        public bool isIgnore { get; set; }

        /// <summary>
        /// The minimum number of times the user should have opened the app before seeing this message.
        /// </summary>
        [JsonProperty(PropertyName = "min_open_times")]
        public int MinOpenTimes { get; set; }

        /// <summary>
        /// A unique identifier for this message.
        /// </summary>
        [JsonProperty(PropertyName = "unique_id")]
        public string UniqueId { get; set; }

        /// <summary>
        /// The minimum minor version of the app the user should have before seeing this message,
        /// or 0 if there is no such minimum minor version number.
        /// </summary>
        [JsonProperty(PropertyName = "min_version_major")]
        public int MinVerMajor { get; set; }

        /// <summary>
        /// The minimum major version of the app the user should have before seeing this message,
        /// or 0 if there is no such minimum major version number.
        /// </summary>
        [JsonProperty(PropertyName = "min_version_minor")]
        public int MinVerMinor { get; set; }

        /// <summary>
        /// The minimum build of the app the user should have before seeing this message,
        /// or 0 if there is no such minimum build.
        /// </summary>
        [JsonProperty(PropertyName = "min_version_build")]
        public int MinVerBuild { get; set; }

        /// <summary>
        /// The minimum revision of the app the user should have before seeing this message,
        /// or 0 if there is no such minimum major revision.
        /// </summary>
        [JsonProperty(PropertyName = "min_version_rev")]
        public int MinVerRev { get; set; }
    }
}
