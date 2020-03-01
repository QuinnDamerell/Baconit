using System.Collections.Generic;
using Newtonsoft.Json;

namespace BaconBackend.Helpers.YouTube
{
    public class YouTubeVideoInfo
    {
        [JsonProperty(PropertyName = "streamingData")]
        public StreamingData StreamingData { get; set; }

        [JsonProperty(PropertyName = "videoDetails")]
        public VideoDetails VideoDetails { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public class Format
    {
        [JsonProperty(PropertyName = "itag")]
        public int ITag { get; set; }

        [JsonProperty(PropertyName = "url")]
        public string Url { get; set; }

        [JsonProperty(PropertyName = "mimeType")]
        public string MimeType { get; set; }

        [JsonProperty(PropertyName = "bitrate")]
        public int Bitrate { get; set; }

        [JsonProperty(PropertyName = "width")]
        public int Width { get; set; }

        [JsonProperty(PropertyName = "height")]
        public int Height { get; set; }

        [JsonProperty(PropertyName = "lastModified")]
        public string LastModified { get; set; }

        [JsonProperty(PropertyName = "contentLength")]
        public string ContentLength { get; set; }

        [JsonProperty(PropertyName = "quality")]
        public string Quality { get; set; }

        [JsonProperty(PropertyName = "qualityLabel")]
        public string QualityLabel { get; set; }

        [JsonProperty(PropertyName = "projectionType")]
        public string ProjectionType { get; set; }

        [JsonProperty(PropertyName = "averageBitrate")]
        public int AverageBitRate { get; set; }

        [JsonProperty(PropertyName = "audioQuality")]
        public string AudioQuality { get; set; }

        [JsonProperty(PropertyName = "approxDurationMs")]
        public string ApproxDurationMs { get; set; }

        [JsonProperty(PropertyName = "audioSampleRate")]
        public string AudioSampleRate { get; set; }

        [JsonProperty(PropertyName = "audioChannels")]
        public int AudioChannels { get; set; }

        [JsonProperty(PropertyName = "cipher")]
        public string Cipher { get; set; }
    }

    public class StreamingData
    {
        [JsonProperty(PropertyName = "expiresInSeconds")]
        public string ExpiresInSeconds { get; set; }

        [JsonProperty(PropertyName = "formats")]
        public List<Format> Formats { get; set; }
    }

    public class ThumbnailDetails
    {
        [JsonProperty(PropertyName = "url")]
        public string Url { get; set; }

        [JsonProperty(PropertyName = "width")]
        public int Width { get; set; }

        [JsonProperty(PropertyName = "height")]
        public int Height { get; set; }
    }

    public class Thumbnail
    {
        [JsonProperty(PropertyName = "thumbnails")]
        public List<ThumbnailDetails> Thumbnails { get; set; }
    }

    public class VideoDetails
    {
        [JsonProperty(PropertyName = "videoId")]
        public string VideoId { get; set; }

        [JsonProperty(PropertyName = "title")]
        public string Title { get; set; }

        [JsonProperty(PropertyName = "lengthSeconds")]
        public string LengthSeconds { get; set; }

        [JsonProperty(PropertyName = "keywords")]
        public List<string> Keywords { get; set; }

        [JsonProperty(PropertyName = "channelId")]
        public string ChannelId { get; set; }

        [JsonProperty(PropertyName = "isOwnerViewing")]
        public bool IsOwnerViewing { get; set; }

        [JsonProperty(PropertyName = "shortDescription")]
        public string ShortDescription { get; set; }

        [JsonProperty(PropertyName = "isCrawlable")]
        public bool IsCrawlable { get; set; }

        [JsonProperty(PropertyName = "thumbnail")]
        public Thumbnail Thumbnail { get; set; }

        [JsonProperty(PropertyName = "averageRating")]
        public double AverageRating { get; set; }

        [JsonProperty(PropertyName = "allowRatings")]
        public bool AllowRatings { get; set; }

        [JsonProperty(PropertyName = "viewCount")]
        public string ViewCount { get; set; }

        [JsonProperty(PropertyName = "author")]
        public string Author { get; set; }

        [JsonProperty(PropertyName = "isPrivate")]
        public bool IsPrivate { get; set; }

        [JsonProperty(PropertyName = "isUnpluggedCorpus")]
        public bool IsUnpluggedCorpus { get; set; }

        [JsonProperty(PropertyName = "isLiveContent")]
        public bool IsLiveContent { get; set; }
    }
}
