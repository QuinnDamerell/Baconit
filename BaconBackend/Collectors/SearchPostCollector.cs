using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using BaconBackend.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.Collectors
{
    public enum PostSearchSorts
    {
        Relevance,
        Top,
        New,
        Comments
    }

    public enum PostSearchTimes
    {
        AllTime,
        PastHour,
        PastDay,
        PastWeek,
        PastMonth,
        PastYear
    }

    public class SearchPostCollector : Collector<Post>
    {
        private BaconManager m_baconMan;

        public SearchPostCollector(BaconManager baconMan, string searchTerm, PostSearchSorts sort = PostSearchSorts.Relevance, PostSearchTimes time = PostSearchTimes.AllTime, string subreddit = null,
            string authorFilter = null, string websiteFilter = null, string selftextFilter = null, string isSelfPost = null, string isNsfw = null) :
            base(baconMan, "SearchSubredditCollector")
        {
            m_baconMan = baconMan;

            // Add the subreddit if needed            
            if (!String.IsNullOrWhiteSpace(subreddit))
            {
                searchTerm += $" subreddit:{subreddit}";
            }
            // Add the author if needed            
            if (!String.IsNullOrWhiteSpace(authorFilter))
            {
                searchTerm += $" author:{authorFilter}";
            }
            // Add the website if needed            
            if (!String.IsNullOrWhiteSpace(websiteFilter))
            {
                searchTerm += $" site:{websiteFilter}";
            }
            // Add the selftext if needed            
            if (!String.IsNullOrWhiteSpace(selftextFilter))
            {
                searchTerm += $" selftext:{selftextFilter}";
            }
            // Add the is self if needed            
            if (!String.IsNullOrWhiteSpace(isSelfPost))
            {
                searchTerm += $" self:{isSelfPost}";
            }
            // Add the nsfw if needed            
            if (!String.IsNullOrWhiteSpace(isNsfw))
            {
                searchTerm += $" nsfw:{isNsfw}";
            }

            // Encode the query
            searchTerm = WebUtility.UrlEncode(searchTerm);

            string sortString = PostSortToString(sort);
            string timeString = PostTimeToString(time);

            // Set up the list helper
            InitListHelper($"/search.json", false, false, $"q={searchTerm}&sort={sortString}&t={timeString}");
        }


        /// <summary>
        /// Fired when the subreddits should be formatted.
        /// </summary>
        /// <param name="subreddits"></param>
        protected override void ApplyCommonFormatting(ref List<Post> posts)
        {
            foreach (Post post in posts)
            {
                // Set The time
                DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                DateTime postTime = origin.AddSeconds(post.CreatedUtc).ToLocalTime();
                post.SubTextLine1 = TimeToTextHelper.TimeElapseToText(postTime) + " ago";
            }
        }

        protected override List<Post> ParseElementList(List<Element<Post>> elements)
        {
            // Converts the elements into a list.
            List<Post> posts = new List<Post>();
            foreach (Element<Post> element in elements)
            {
                posts.Add(element.Data);
            }
            return posts;
        }

        public static string PostSortToString(PostSearchSorts sort)
        {
            switch(sort)
            {
                default:
                case PostSearchSorts.Relevance:
                    return "relevance";
                case PostSearchSorts.New:
                    return "new";
                case PostSearchSorts.Top:
                    return "top";
                case PostSearchSorts.Comments:
                    return "comments";

            }
        }

        public static string PostTimeToString(PostSearchTimes time)
        {
            switch (time)
            {
                default:
                case PostSearchTimes.AllTime:
                    return "all";
                case PostSearchTimes.PastHour:
                    return "hour";
                case PostSearchTimes.PastDay:
                    return "day";
                case PostSearchTimes.PastWeek:
                    return "week";
                case PostSearchTimes.PastMonth:
                    return "month";
                case PostSearchTimes.PastYear:
                    return "year";
            }
        }
    }
}
