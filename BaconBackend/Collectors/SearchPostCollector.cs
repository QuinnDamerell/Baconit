using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

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
        public SearchPostCollector(BaconManager baconMan, string searchTerm, PostSearchSorts sort = PostSearchSorts.Relevance, PostSearchTimes time = PostSearchTimes.AllTime, string subreddit = null,
            string authorFilter = null, string websiteFilter = null, string selfTextFilter = null, string isSelfPost = null, string isNsfw = null) :
            base(baconMan, "SearchSubredditCollector")
        {
            // Add the subreddit if needed            
            if (!string.IsNullOrWhiteSpace(subreddit))
            {
                searchTerm += $" subreddit:{subreddit}";
            }
            // Add the author if needed            
            if (!string.IsNullOrWhiteSpace(authorFilter))
            {
                searchTerm += $" author:{authorFilter}";
            }
            // Add the website if needed            
            if (!string.IsNullOrWhiteSpace(websiteFilter))
            {
                searchTerm += $" site:{websiteFilter}";
            }
            // Add the selftext if needed            
            if (!string.IsNullOrWhiteSpace(selfTextFilter))
            {
                searchTerm += $" selftext:{selfTextFilter}";
            }
            // Add the is self if needed            
            if (!string.IsNullOrWhiteSpace(isSelfPost))
            {
                searchTerm += $" self:{isSelfPost}";
            }
            // Add the nsfw if needed            
            if (!string.IsNullOrWhiteSpace(isNsfw))
            {
                searchTerm += $" nsfw:{isNsfw}";
            }

            // Encode the query
            searchTerm = WebUtility.UrlEncode(searchTerm);

            var sortString = PostSortToString(sort);
            var timeString = PostTimeToString(time);

            // Set up the list helper
            InitListHelper("/search.json", false, false, $"q={searchTerm}&sort={sortString}&t={timeString}");
        }


        /// <summary>
        /// Fired when the posts should be formatted.
        /// </summary>
        /// <param name="posts"></param>
        protected override void ApplyCommonFormatting(ref List<Post> posts)
        {
            foreach (var post in posts)
            {
                // Set The time
                var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                var postTime = origin.AddSeconds(post.CreatedUtc).ToLocalTime();
                post.SubTextLine1 = TimeToTextHelper.TimeElapseToText(postTime) + " ago";
            }
        }

        protected override List<Post> ParseElementList(List<Element<Post>> elements)
        {
            // Converts the elements into a list.
            return elements.Select(element => element.Data).ToList();
        }

        private static string PostSortToString(PostSearchSorts sort)
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

        private static string PostTimeToString(PostSearchTimes time)
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
