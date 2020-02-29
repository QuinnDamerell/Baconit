using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace BaconBackend.Collectors
{
    public class SearchSubredditCollector : Collector<Subreddit>
    {
        public SearchSubredditCollector(BaconManager baconMan, string searchTerm) :
            base(baconMan, "SearchSubredditCollector")
        {
            // Encode the query
            searchTerm = WebUtility.UrlEncode(searchTerm);

            // Set up the list helper
            InitListHelper("/search.json", false, false, "q=" + searchTerm + "&restrict_sr=&sort=relevance&t=all&type=sr&count=50");
        }

        /// <summary>
        /// Fired when the subreddits should be formatted.
        /// </summary>
        /// <param name="subreddits"></param>
        protected override void ApplyCommonFormatting(ref List<Subreddit> subreddits)
        {
            foreach (var subreddit in subreddits)
            {
                // Do some simple formatting
                subreddit.PublicDescription =subreddit.PublicDescription.Trim();

                // Do a quick and dirty replace for double new lines. This doesn't work for triple or tabs.
                subreddit.PublicDescription = subreddit.PublicDescription.Replace("\n\n", "\n");
            }
        }

        protected override List<Subreddit> ParseElementList(List<Element<Subreddit>> elements)
        {
            // Converts the elements into a list.
            return elements.Select(element => element.Data).ToList();
        }
    }
}
