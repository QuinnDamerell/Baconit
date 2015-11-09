using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using BaconBackend.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace BaconBackend.Collectors
{
    public enum PostVoteAction
    {
        UpVote,
        DownVote,
    }

    public enum SortTypes
    {
        Hot,
        New,
        Rising,
        Controversial,
        Top
    }

    public class SubredditCollector : Collector<Post>
    {
        /// <summary>
        /// Used the pass the subreddit and sort through
        /// </summary>
        public class SubredditContainer
        {
            public Subreddit subreddit;
            public SortTypes sortType;
            public string forcePostId;
        }

        /// <summary>
        /// Returns a collector for the given type. If the collector doesn't exist one will be created.
        /// </summary>
        /// <param name="subreddit"></param>
        /// <returns></returns>
        public static SubredditCollector GetCollector(Subreddit subreddit, BaconManager baconMan, SortTypes sort = SortTypes.Hot, string forcePostId = null)
        {
            SubredditContainer container = new SubredditContainer() { subreddit = subreddit, sortType = sort, forcePostId = forcePostId };
            // Make the uniqueId. If we have a force post add that also so we don't get an existing collector with the real subreddit.
            string uniqueId = subreddit.Id + sort + (String.IsNullOrWhiteSpace(forcePostId) ? String.Empty : forcePostId);
            return (SubredditCollector)Collector<Post>.GetCollector(typeof(SubredditCollector), uniqueId, container, baconMan);
        }

        //
        // Private vars
        //
        Subreddit m_subreddit = null;
        SortTypes m_sortType = SortTypes.Hot;
        BaconManager m_baconMan;

        public SubredditCollector(SubredditContainer subredditContainer, BaconManager baconMan)
            : base(baconMan, subredditContainer.subreddit.Id + subredditContainer.sortType)
        {
            // Set the vars
            m_subreddit = subredditContainer.subreddit;
            m_sortType = subredditContainer.sortType;
            m_baconMan = baconMan;

            string subredditUrl = "";
            bool hasEmptyRoot = false;
            if (m_subreddit.DisplayName.ToLower() == "frontpage")
            {
                // Special case for the front page
                subredditUrl = $"/{SortTypeToString(m_sortType)}/.json";
            }
            else if(m_subreddit.DisplayName.ToLower() == "saved")
            {
                // Special case for the saved posts
                subredditUrl = $"/user/{m_baconMan.UserMan.CurrentUser.Name}/saved/.json";
            }
            else if(!String.IsNullOrWhiteSpace(subredditContainer.forcePostId))
            {
                // We are only going to try to grab one specific post. This is used by search and inbox to 
                // link to a post. Since we are doing so, we need to make the unique id something unique for this post so we don't get
                // a cache. This should match the unique id we use to look up the subreddit above.
                SetUniqueId(m_subreddit.Id + m_sortType + subredditContainer.forcePostId);
                subredditUrl = $"/r/{m_subreddit.DisplayName}/comments/{subredditContainer.forcePostId}/.json";
                hasEmptyRoot = true;
            }
            else
            {
                subredditUrl = $"/r/{m_subreddit.DisplayName}/{SortTypeToString(m_sortType)}/.json";
            }

            InitListHelper(subredditUrl, hasEmptyRoot, true);
        }

        /// <summary>
        /// Returns the given post if found.
        /// </summary>
        /// <returns></returns>
        public Post GetPost(string postId)
        {
            List<Post> posts = GetCurrentPosts();

            foreach(Post post in posts)
            {
                if(post.Id == postId)
                {
                    return post;
                }
            }
            return null;
        }

        #region Post Modifiers

        /// <summary>
        /// Called by the consumer when a post should be marked as read.
        /// </summary>
        /// <param name="post">The post to be marked as read</param>
        /// <param name="postPosition">A hint to the position of the post</param>
        public void MarkPostRead(Post post, int postPosition = 0)
        {
            // If the story is already marked get out of here.
            if (ReadPostsList.ContainsKey(post.Id))
            {
                return;
            }

            // Using the post and suggested index, find the real post and index
            Post collectionPost = post;
            FindPostInCurrentCollection(ref collectionPost, ref postPosition);

            if (collectionPost == null)
            {
                // We didn't find it.
                return;
            }

            // Add it to the read list. We don't want to note the comments until it is read in comment view.
            ReadPostsList.Add(collectionPost.Id, -1);

            // Save the new list
            SaveSettings();

            // Fire off that a update happened.
            FireCollectionUpdated(postPosition, new List<Post>() { collectionPost }, false, false);
        }

        /// <summary>
        /// Called by the consumer when a post should be make the amount of comments.
        /// </summary>
        /// <param name="post">The post to have comments noted</param>
        /// <param name="postPosition">A hint to the position of the post</param>
        public void MarkPostCommentCount(Post post, int postPosition = 0)
        {
            // If the comments are already noted get out of here.
            if (ReadPostsList.ContainsKey(post.Id) && post.NumComments == ReadPostsList[post.Id])
            {
                return;
            }

            // Using the post and suggested index, find the real post and index
            Post collectionPost = post;
            FindPostInCurrentCollection(ref collectionPost, ref postPosition);

            if (collectionPost == null)
            {
                // We didn't find it.
                return;
            }

            // Add or update the comment count
            ReadPostsList[collectionPost.Id] = collectionPost.NumComments;

            // Save the new list
            SaveSettings();

            // Fire off that a update happened.
            FireCollectionUpdated(postPosition, new List<Post>() { collectionPost }, false, false);
        }

        /// <summary>
        /// Called by the consumer when a post vote should be changed
        /// </summary>
        /// <param name="post">The post to be actioned</param>
        /// <param name="postPosition">A hint to the position of the post</param>
        public void ChangePostVote(Post post, PostVoteAction action, int postPosition = 0)
        {
            // Ensure we are signed in.
            if(!m_baconMan.UserMan.IsUserSignedIn)
            {
                m_baconMan.MessageMan.ShowSigninMessage("vote");
                return;
            }

            // Using the post and suggested index, find the real post and index
            Post collectionPost = post;
            FindPostInCurrentCollection(ref collectionPost, ref postPosition);

            if (collectionPost == null || postPosition == -1)
            {
                // We didn't find it.
                return;
            }

            // Update the like status
            if(action == PostVoteAction.UpVote)
            {
                if(collectionPost.Likes.HasValue && collectionPost.Likes.Value)
                {
                    collectionPost.Likes = null;
                    collectionPost.Score--;
                }
                else
                {
                    collectionPost.Likes = true;
                    collectionPost.Score++;
                }
            }
            else
            {
                if (collectionPost.Likes.HasValue && !collectionPost.Likes.Value)
                {
                    collectionPost.Likes = null;
                    collectionPost.Score++;
                }
                else
                {
                    collectionPost.Likes = false;
                    collectionPost.Score--;
                }
            }

            // Fire off that a update happened.
            FireCollectionUpdated(postPosition, new List<Post>() { collectionPost }, false, false);

            // Start a task to make the vote
            Task.Run(async () =>
            {
                try
                {
                    // Build the data
                    string voteDir = collectionPost.Likes.HasValue ? collectionPost.Likes.Value ? "1" : "-1" : "0";
                    List<KeyValuePair<string, string>> postData = new List<KeyValuePair<string, string>>();
                    postData.Add(new KeyValuePair<string, string>("id", "t3_"+collectionPost.Id));
                    postData.Add(new KeyValuePair<string, string>("dir", voteDir));

                    // Make the call
                    string str = await m_baconMan.NetworkMan.MakeRedditPostRequest("api/vote", postData);

                    // Do some super simple validation
                    if(str != "{}")
                    {
                        throw new Exception("Failed to set vote! The response indicated a failure");
                    }
                }
                catch(Exception ex)
                {
                    m_baconMan.MessageMan.DebugDia("failed to vote!", ex);
                    m_baconMan.MessageMan.ShowMessageSimple("That's Not Right", "Something went wrong while trying to cast your vote, try again later.");
                }
            });
        }

        #endregion

        /// <summary>
        /// Given a post and a starting index this function will return the collection post
        /// object and the true index of the item. If not found it will return null and -1
        /// </summary>
        /// <param name="post"></param>
        /// <param name="index"></param>
        private void FindPostInCurrentCollection(ref Post post, ref int index)
        {
            // Get the current list
            List<Post> posts = GetCurrentPostsInternal();

            // Find the post starting at the possible index
            for (; index < posts.Count; index++)
            {
                if (posts[index].Id.Equals(post.Id))
                {
                    // Grab the post and break;
                    post = posts[index];
                    return;
                }
            }

            // If we didn't find it kill them.
            index = -1;
            post = null;
        }

        /// <summary>
        /// Converts an element list to a post list.
        /// </summary>
        /// <param name="elements"></param>
        /// <returns></returns>
        override protected List<Post> ParseElementList(List<Element<Post>> elements)
        {
            // Converts the elements into a list.
            List<Post> posts = new List<Post>();
            foreach (Element<Post> element in elements)
            {
                posts.Add(element.Data);
            }
            return posts;
        }

        /// <summary>
        /// Applies any common formatting to the posts.
        /// </summary>
        /// <param name="posts">Posts to be formatted</param>
        override protected void ApplyCommonFormatting(ref List<Post> posts)
        {
            bool isFrontPage = m_subreddit.IsArtifical;

            foreach(Post post in posts)
            {
                // Set the first line
                DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                DateTime postTime = origin.AddSeconds(post.CreatedUtc).ToLocalTime();
                post.SubTextLine1 = TimeToTextHelper.TimeElapseToText(postTime) + $" ago by {post.Author}";

                // Set the second line
                post.SubTextLine2PartOne = post.NumComments + (post.NumComments == 1 ? " comment " : " comments ");
                post.SubTextLine2PartTwo = (isFrontPage ? post.Subreddit.ToLower() : post.Domain);

                // Set the second line for flipview
                post.FlipViewSecondary = $"r/{post.Subreddit.ToLower()}";

                // Escape the title and selftext
                post.Title = WebUtility.HtmlDecode(post.Title);
                if (!String.IsNullOrEmpty(post.Selftext))
                {
                    post.Selftext = WebUtility.HtmlDecode(post.Selftext);
                }

                // Set the title size
                post.TitleMaxLines = m_baconMan.UiSettingsMan.SubredditList_ShowFullTitles ? 99 : 2;

                // Check if it has been read
                if (ReadPostsList.ContainsKey(post.Id))
                {
                    // Set the text color
                    post.TitleTextColor = Color.FromArgb(255, 152, 152, 152);

                    // Set the comments text if we want to.
                    int commentDiff = post.NumComments - ReadPostsList[post.Id];
                    post.NewCommentText = (ReadPostsList[post.Id] != -1 && commentDiff > 0) ? $"(+{commentDiff})" : String.Empty;
                }
            }
        }

        /// <summary>
        /// Converts a sort type to a string
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private string SortTypeToString(SortTypes type)
        {
            switch (type)
            {
                case SortTypes.Rising:
                    return "rising";
                case SortTypes.Hot:
                    return "hot";
                case SortTypes.Controversial:
                    return "controversial";
                case SortTypes.New:
                    return "new";
                default:
                case SortTypes.Top:
                    return "top";
            }
        }

        /// <summary>
        /// Force the settings to be set so they will be saved.
        /// </summary>
        private void SaveSettings()
        {
            ReadPostsList = m_readPostsList;
        }

        /// <summary>
        /// This is an auto length caped list that has fast look up because it is also a map.
        /// If a story exists in here it has been read, and the int indicates the amount of comments
        /// it has when last read. If the comment count is -1 it has been read but the comments weren't noted.
        /// </summary>
        private HashList<string, int> ReadPostsList
        {
            get
            {
                if (m_readPostsList == null)
                {
                    if (m_baconMan.SettingsMan.RoamingSettings.ContainsKey("SubredditPostCollector.ReadPostsList"))
                    {
                        m_readPostsList = m_baconMan.SettingsMan.ReadFromRoamingSettings<HashList<string, int>>("SubredditPostCollector.ReadPostsList");
                    }
                    else
                    {
                        m_readPostsList = new HashList<string, int>(150);
                    }
                }
                return m_readPostsList;
            }
            set
            {
                m_readPostsList = value;
                m_baconMan.SettingsMan.WriteToRoamingSettings<HashList<string, int>>("SubredditPostCollector.ReadPostsList", m_readPostsList);
            }
        }
        private HashList<string, int> m_readPostsList = null;
    }
}
