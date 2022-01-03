using BaconBackend.DataObjects;
using System;
using System.Linq;
using Newtonsoft.Json;

namespace Baconit.ContentPanels
{
    /// <summary>
    /// Represents the content that the panel will show.
    /// </summary>
    public class ContentPanelSource
    {
        public const string CGenericIdBase = "generic_";

        public string Id;
        public string Url;
        public string SelfText;
        public string Subreddit;
        public bool IsNsfw;
        public bool IsSelf;
        public bool ForceWeb = false;

        public bool IsVideo = false;
        public bool IsRedditVideo = false;
        public Uri VideoUrl;
        public bool IsImageGallery = false;

        // Make a private constructor so they can be only created by
        // this class internally.
        private ContentPanelSource()
        { }

        public static ContentPanelSource CreateFromPost(Post post)
        {
            var source = new ContentPanelSource
            {
                Id = post.Id,
                Url = post.Url,
                SelfText = post.Selftext,
                Subreddit = post.Subreddit,
                IsNsfw = post.IsOver18,
                IsSelf = post.IsSelf,
                IsVideo = post.IsVideo,
                IsRedditVideo = post.SecureMedia?.RedditVideo?.Url != null,
                IsImageGallery = post.GalleryData?.Items?.Count() > 0
            };

            if (source.IsRedditVideo)
            {
                source.VideoUrl = new Uri(post.SecureMedia.RedditVideo.Url);
            }

            return source;
        }

        public static ContentPanelSource CreateFromUrl(string url)
        {
            var source = new ContentPanelSource {Id = CGenericIdBase + DateTime.Now.Ticks, Url = url};
            return source;
        }

        public override string ToString()
        {
#if DEBUG
            return JsonConvert.SerializeObject(this);
#else  
            return base.ToString();
#endif
        }
    }
}
