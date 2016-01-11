using BaconBackend.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baconit.ContentPanels
{
    /// <summary>
    /// Represents the content that the panel will show.
    /// </summary>
    public class ContentPanelSource
    {
        public const string c_genericIdBase = "generic_";

        public string Id;
        public string Url = null;
        public string SelfText = null;
        public string Subreddit = null;
        public bool IsNSFW = false;
        public bool IsSelf = false;
        public bool ForceWeb = false;

        // Make a private constructor so they can be only created by
        // this class internally.
        private ContentPanelSource()
        { }

        public static ContentPanelSource CreateFromPost(Post post)
        {
            ContentPanelSource source = new ContentPanelSource();
            source.Id = post.Id;
            source.Url = post.Url;
            source.SelfText = post.Selftext;
            source.Subreddit = post.Subreddit;
            source.IsNSFW = post.IsOver18;
            source.IsSelf = post.IsSelf;
            return source;
        }

        public static ContentPanelSource CreateFromUrl(string url)
        {
            ContentPanelSource source = new ContentPanelSource();
            source.Id = c_genericIdBase + DateTime.Now.Ticks;
            source.Url = url;
            return source;
        }
    }
}
