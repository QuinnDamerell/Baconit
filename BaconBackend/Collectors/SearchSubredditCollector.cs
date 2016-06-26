using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.Collectors
{
    public class SearchSubredditCollector : Collector<Subreddit>
    {
        private BaconManager m_baconMan;

        public SearchSubredditCollector(BaconManager baconMan, string searchTerm) :
            base(baconMan, "SearchSubredditCollector")
        {
            m_baconMan = baconMan;

            // Encode the query
            searchTerm = WebUtility.UrlEncode(searchTerm);

            // Set up the list helper
            InitListHelper("/search.json", false, false, $"q=" + searchTerm + "&restrict_sr=&sort=relevance&t=all&type=sr&count=50");
        }

        /// <summary>
        /// Fired when the subreddits should be formatted.
        /// </summary>
        /// <param name="subreddits"></param>
        protected override void ApplyCommonFormatting(ref List<Subreddit> subreddits)
        {
            foreach (Subreddit subreddit in subreddits)
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
            List<Subreddit> subreddits = new List<Subreddit>();
            foreach (Element<Subreddit> element in elements)
            {
                subreddits.Add(element.Data);
            }
            return subreddits;
        }
    }
}
