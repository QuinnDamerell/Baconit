using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using BaconBackend.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI;

namespace BaconBackend.Collectors
{
    public class PostCollector : Collector<Post>
    {
        /// <summary>
        /// Used the pass the subreddit and sort through
        /// </summary>
        public class PostCollectorContext
        {
            public User User;
            public Subreddit Subreddit;
            public SortTypes SortType;
            public SortTimeTypes SortTimeType;
            public string ForcePostId;
            public string UniqueId;
        }

        /// <summary>
        /// Returns a collector for the given type. If the collector doesn't exist one will be created.
        /// </summary>
        /// <param name="subreddit"></param>
        /// <param name="baconMan"></param>
        /// <param name="sort"></param>
        /// <param name="sortTime"></param>
        /// <returns></returns>
        public static PostCollector GetCollector(Subreddit subreddit, BaconManager baconMan, SortTypes sort = SortTypes.Hot, SortTimeTypes sortTime = SortTimeTypes.Week, string forcePostId = null)
        {
            var container = new PostCollectorContext
            {
                Subreddit = subreddit,
                SortType = sort,
                ForcePostId = forcePostId,
                SortTimeType = sortTime,
                UniqueId = subreddit.Id + sort + sortTime +
                           (string.IsNullOrWhiteSpace(forcePostId) ? string.Empty : forcePostId)
            };
            // Make the uniqueId. If we have a force post add that also so we don't get an existing collector with the real subreddit.
            return (PostCollector)GetCollector(typeof(PostCollector), container.UniqueId, container, baconMan);
        }

        /// <summary>
        /// Returns a collector for the given type. If the collector doesn't exist one will be created.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="baconMan"></param>
        /// <param name="sort"></param>
        /// <param name="sortTime"></param>
        /// <returns></returns>
        public static PostCollector GetCollector(User user, BaconManager baconMan, SortTypes sort = SortTypes.Hot, SortTimeTypes sortTime = SortTimeTypes.Week)
        {
            var container = new PostCollectorContext { User = user, SortType = sort, SortTimeType = sortTime };
            container.UniqueId = "t2_"+user.Id + sort + sortTime;
            return (PostCollector)GetCollector(typeof(PostCollector), container.UniqueId, container, baconMan);
        }

        //
        // Private vars
        //
        private readonly Subreddit _subreddit;
        private readonly BaconManager _baconMan;

        public PostCollector(PostCollectorContext collectorContext, BaconManager baconMan)
            : base(baconMan, collectorContext.UniqueId)
        {
            // Set the vars
            var user = collectorContext.User;
            _subreddit = collectorContext.Subreddit;
            var sortType = collectorContext.SortType;
            var sortTimeType = collectorContext.SortTimeType;
            _baconMan = baconMan;

            // If we are doing a top sort setup the sort time
            var optionalArgs = string.Empty;
            if(sortType == SortTypes.Top)
            {
                switch(sortTimeType)
                {
                    case SortTimeTypes.AllTime:
                        optionalArgs = "sort=top&t=all";
                        break;
                    case SortTimeTypes.Day:
                        optionalArgs = "sort=top&t=day";
                        break;
                    case SortTimeTypes.Hour:
                        optionalArgs = "sort=top&t=hour";
                        break;
                    case SortTimeTypes.Month:
                        optionalArgs = "sort=top&t=month";
                        break;
                    case SortTimeTypes.Week:
                        optionalArgs = "sort=top&t=week";
                        break;
                    case SortTimeTypes.Year:
                        optionalArgs = "sort=top&t=year";
                        break;
                }
            }

            var postCollectionUrl = "";
            var hasEmptyRoot = false;


            if (_subreddit != null)
            {
                if (_subreddit.DisplayName.ToLower() == "frontpage")
                {
                    // Special case for the front page
                    postCollectionUrl = $"/{SortTypeToString(sortType)}/.json";
                }
                else if (_subreddit.DisplayName.ToLower() == "saved")
                {
                    // Special case for the saved posts
                    postCollectionUrl = $"/user/{_baconMan.UserMan.CurrentUser.Name}/saved/.json";
                    optionalArgs = "type=links";
                }
                else if (!string.IsNullOrWhiteSpace(collectorContext.ForcePostId))
                {
                    // We are only going to try to grab one specific post. This is used by search and inbox to 
                    // link to a post. Since we are doing so, we need to make the unique id something unique for this post so we don't get
                    // a cache. This should match the unique id we use to look up the subreddit above.
                    SetUniqueId(_subreddit.Id + sortType + collectorContext.ForcePostId);
                    postCollectionUrl = $"/r/{_subreddit.DisplayName}/comments/{collectorContext.ForcePostId}/.json";
                    hasEmptyRoot = true;
                }
                else
                {
                    postCollectionUrl = $"/r/{_subreddit.DisplayName}/{SortTypeToString(sortType)}/.json";
                }
            }
            else
            {
                // Get posts for a user
                postCollectionUrl = $"user/{user.Name}/submitted/.json";

                switch(sortType)
                {
                    case SortTypes.Controversial:
                        optionalArgs = "sort=controversial";
                        break;
                    case SortTypes.Hot:
                        optionalArgs = "sort=hot";
                        break;
                    case SortTypes.New:
                        optionalArgs = "sort=new";
                        break;
                    case SortTypes.Top:
                        optionalArgs = "sort=top";
                        break;
                    case SortTypes.Rising:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            InitListHelper(postCollectionUrl, hasEmptyRoot, true, optionalArgs);

            // Listen to user changes so we will update the subreddits
            _baconMan.UserMan.OnUserUpdated += OnUserUpdated;
        }


        /// <summary>
        /// Fired when the current user is updated.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnUserUpdated(object sender, UserUpdatedArgs args)
        {
            // If a user is added or removed update the subreddit to reflect the new user.
            if (args.Action != UserCallbackAction.Updated)
            {
                Update(true);
            }
        }

        /// <summary>
        /// Returns the given post if found.
        /// </summary>
        /// <returns></returns>
        public Post GetPost(string postId)
        {
            var posts = GetCurrentPosts();

            return posts.FirstOrDefault(post => post.Id == postId);
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
            var collectionPost = post;
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
            FireCollectionUpdated(postPosition, new List<Post> { collectionPost }, false, false);
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
            var collectionPost = post;
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
            FireCollectionUpdated(postPosition, new List<Post> { collectionPost }, false, false);
        }

        /// <summary>
        /// Called by the consumer when a post vote should be changed
        /// </summary>
        /// <param name="post">The post to be actioned</param>
        /// <param name="postPosition">A hint to the position of the post</param>
        public void ChangePostVote(Post post, PostVoteAction action, int postPosition = 0)
        {
            // Ensure we are signed in.
            if(!_baconMan.UserMan.IsUserSignedIn)
            {
                _baconMan.MessageMan.ShowSigninMessage("vote");
                return;
            }

            // Using the post and suggested index, find the real post and index
            var collectionPost = post;
            FindPostInCurrentCollection(ref collectionPost, ref postPosition);

            if (collectionPost == null || postPosition == -1)
            {
                // We didn't find it.
                return;
            }

            // Update the like status
            var likesAction = action == PostVoteAction.UpVote;
            var voteMultiplier = action == PostVoteAction.UpVote ? 1 : -1;
            if(collectionPost.Likes.HasValue)
            {
                if(collectionPost.Likes.Value == likesAction)
                {
                    // duplicate vote would undo the action
                    collectionPost.Likes = null;
                    collectionPost.Score -= voteMultiplier;
                }
                else
                {
                    // opposite vote, takes into account previous vote
                    collectionPost.Likes = likesAction;
                    collectionPost.Score += 2 * voteMultiplier;
                }
            }
            else
            {
                // first vote
                collectionPost.Likes = likesAction;
                collectionPost.Score += voteMultiplier;
            }
            
            // Fire off that a update happened.
            FireCollectionUpdated(postPosition, new List<Post> { collectionPost }, false, false);

            // Start a task to make the vote
            Task.Run(async () =>
            {
                try
                {
                    // Build the data
                    var voteDir = collectionPost.Likes.HasValue ? collectionPost.Likes.Value ? "1" : "-1" : "0";
                    var postData = new List<KeyValuePair<string, string>>();
                    postData.Add(new KeyValuePair<string, string>("id", "t3_"+collectionPost.Id));
                    postData.Add(new KeyValuePair<string, string>("dir", voteDir));

                    // Make the call
                    var str = await _baconMan.NetworkMan.MakeRedditPostRequestAsString("api/vote", postData);

                    // Do some super simple validation
                    if(str != "{}")
                    {
                        throw new Exception("Failed to set vote! The response indicated a failure");
                    }
                }
                catch(Exception ex)
                {
                    _baconMan.MessageMan.DebugDia("failed to vote!", ex);
                    _baconMan.MessageMan.ShowMessageSimple("That's Not Right", "Something went wrong while trying to cast your vote, try again later.");
                }
            });
        }

        /// <summary>
        /// Called by the consumer when a post should be saved or hidden
        /// </summary>
        /// <param name="post">The post to be marked as read</param>
        /// <param name="postPosition">A hint to the position of the post</param>
        public async void SaveOrHidePost(Post post, bool? save, bool? hide, int postPosition = 0)
        {
            // Using the post and suggested index, find the real post and index
            var collectionPost = post;
            FindPostInCurrentCollection(ref collectionPost, ref postPosition);

            if (collectionPost == null)
            {
                // We didn't find it.
                return;
            }

            // Change the UI now
            if (save.HasValue)
            {
                collectionPost.IsSaved = save.Value;
            }
            else if (hide.HasValue)
            {
                collectionPost.IsHidden = hide.Value;
            }
            else
            {
                return;
            }

            // Make the call to save or hide the post
            var success = await Task.Run(() => MiscellaneousHelper.SaveOrHideRedditItem(_baconMan, "t3_" + collectionPost.Id, save, hide));

            if (!success)
            {
                // We failed, revert the UI.
                if (save.HasValue)
                {
                    collectionPost.IsSaved = save.Value;
                }
                else if (hide.HasValue)
                {
                    collectionPost.IsHidden = hide.Value;
                }
                else
                {
                    return;
                }
            }
            else
            {
                // Fire off that a update happened.
                FireCollectionUpdated(postPosition, new List<Post> { collectionPost }, false, false);
            }
        }

        /// <summary>
        /// Edits the text of a self post.
        /// </summary>
        /// <param name="post"></param>
        public bool EditSelfPost(Post post, string serverResponse)
        {
            // Assume if we can find author we are successful. Not sure if that is safe or not... :)
            if (!string.IsNullOrWhiteSpace(serverResponse) && serverResponse.Contains("\"author\""))
            {
                // Do the next part in a try catch so if we fail we will still report success since the
                // message was sent to reddit.
                // NOTE: YES THIS IS VERY BAD CODE.
                try
                {
                    // Try to parse out the new selftext.
                    var dataPos = serverResponse.IndexOf("\"selftext\":");
                    var dataStartPos = serverResponse.IndexOf('"', dataPos + 11);

                    // Find the end of the string. We must ignore any \\\" because those are escaped (\\) \" from the comment.
                    var dataEndPos = dataStartPos;
                    while(dataEndPos < serverResponse.Length)
                    {
                        // Find the next "
                        dataEndPos = serverResponse.IndexOf('"', dataEndPos + 1);

                        // Check that this wasn't after \
                        if(serverResponse[dataEndPos - 1] != '\\')
                        {
                            // We found a " without the \ before it!
                            break;
                        }
                    }

                    // Now get the string.
                    var newSelfText = serverResponse.Substring(dataStartPos, (dataEndPos - dataStartPos + 1));

                    // Remove the starting and ending "
                    newSelfText = newSelfText.Substring(1, newSelfText.Length - 2);

                    newSelfText = Regex.Unescape(newSelfText);   

                    // Using the post and suggested index, find the real post and index
                    var collectionPost = post;
                    var postPosition = 0;
                    FindPostInCurrentCollection(ref collectionPost, ref postPosition);

                    if (collectionPost == null)
                    {
                        // We didn't find it.
                        return true;
                    }

                    // Update the collection and post selftext. It is important to also update the post because
                    // we will use that to refresh the UI instantly.
                    post.Selftext = newSelfText;
                    collectionPost.Selftext = newSelfText;

                    // Fire off that a update happened.
                    FireCollectionUpdated(postPosition, new List<Post> { collectionPost }, false, false);                   
                }
                catch (Exception e)
                {
                    // We fucked up updating the UI for the post edit.
                    _baconMan.MessageMan.DebugDia("Failed updating selftext in UI", e);
                    TelemetryManager.ReportUnexpectedEvent(this, "FailedUpdatingSelftextInUI");
                }

                // If the response was ok always return true.
                return true;
            }

            // Reddit returned something wrong
            _baconMan.MessageMan.ShowMessageSimple("That's not right", "We can't edit your post right now, reddit returned and unexpected message.");
            TelemetryManager.ReportUnexpectedEvent(this, "CommentPostReturnedUnexpectedMessage");
            return false;
        }

        /// <summary>
        /// Called when we should delete a post from this user.
        /// </summary>
        public async void DeletePost(Post post)
        {
            // Try to delete it.
            var success = await Task.Run(()=> MiscellaneousHelper.DeletePost(_baconMan, post.Id));

            if(success)
            {
                _baconMan.MessageMan.ShowMessageSimple("Bye Bye", "Your post has been deleted.");
            }
            else
            {
                _baconMan.MessageMan.ShowMessageSimple("That's not right", "We can't edit your post right now, check your Internet connection.");
            }
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
            var posts = GetCurrentPostsInternal();

            // Find the post starting at the possible index
            for (; index < posts.Count; index++)
            {
                if (!posts[index].Id.Equals(post.Id)) continue;
                // Grab the post and break;
                post = posts[index];
                return;
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
        protected override List<Post> ParseElementList(List<Element<Post>> elements)
        {
            // Converts the elements into a list.
            return elements.Select(element => element.Data).ToList();
        }

        /// <summary>
        /// Applies any common formatting to the posts.
        /// </summary>
        /// <param name="posts">Posts to be formatted</param>
        protected override void ApplyCommonFormatting(ref List<Post> posts)
        {
            var isFrontPage = _subreddit != null && (_subreddit.IsArtificial || _subreddit.DisplayName.ToLower().Equals("frontpage"));
            var showSubreddit = isFrontPage || _subreddit == null;

            foreach(var post in posts)
            {
                // Set the first line
                var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                var postTime = origin.AddSeconds(post.CreatedUtc).ToLocalTime();

                // if this is going to a subreddit add the user, if this is going to a user add the domain
                if(_subreddit != null)
                {
                    post.SubTextLine1 = TimeToTextHelper.TimeElapseToText(postTime) + $" ago by {post.Author}";
                }
                else
                {
                    post.SubTextLine1 = TimeToTextHelper.TimeElapseToText(postTime) + $" ago to {post.Domain}";
                }

                // Set the second line
                post.SubTextLine2PartOne = post.NumComments + (post.NumComments == 1 ? " comment " : " comments ");
                post.SubTextLine2PartTwo = (showSubreddit ? post.Subreddit.ToLower() : post.Domain);

                // Set the second line for flipview
                post.FlipViewSecondary = showSubreddit ? $"r/{post.Subreddit.ToLower()}" : TimeToTextHelper.TimeElapseToText(postTime) + " ago";

                // Set the title size
                post.TitleMaxLines = _baconMan.UiSettingsMan.SubredditListShowFullTitles ? 99 : 2;

                // Set if we should show the save image or not
                post.ShowSaveImageMenu = string.IsNullOrWhiteSpace(ImageManager.GetImageUrl(post.Url)) || !post.IsGallery
                    ? Windows.UI.Xaml.Visibility.Collapsed 
                    : Windows.UI.Xaml.Visibility.Visible;

                // Set if this is owned by the current user
                if(_baconMan.UserMan.IsUserSignedIn && _baconMan.UserMan.CurrentUser != null)
                {
                    post.IsPostOwnedByUser = post.Author.Equals(_baconMan.UserMan.CurrentUser.Name, StringComparison.OrdinalIgnoreCase);
                }

                // Check if it has been read
                if (!ReadPostsList.ContainsKey(post.Id)) continue;
                // Set the text color
                post.TitleTextColor = Color.FromArgb(255, 152, 152, 152);

                // Set the comments text if we want to.
                var commentDiff = post.NumComments - ReadPostsList[post.Id];
                post.NewCommentText = (ReadPostsList[post.Id] != -1 && commentDiff > 0) ? $"(+{commentDiff})" : string.Empty;
            }
        }

        /// <summary>
        /// Converts a sort type to a string
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static string SortTypeToString(SortTypes type)
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
            ReadPostsList = _readPostsList;
        }

        /// <summary>
        /// This is an auto length caped list that has fast look up because it is also a map.
        /// If a story exists in here it has been read, and the int indicates the amount of comments
        /// it has when last read. If the comment count is -1 it has been read but the comments weren't noted.
        /// </summary>
        private HashList<string, int> ReadPostsList
        {
            get =>
                _readPostsList ?? (_readPostsList =
                    _baconMan.SettingsMan.RoamingSettings.ContainsKey("SubredditPostCollector.ReadPostsList")
                        ? _baconMan.SettingsMan.ReadFromRoamingSettings<HashList<string, int>>(
                            "SubredditPostCollector.ReadPostsList")
                        : new HashList<string, int>(150));
            set
            {
                _readPostsList = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("SubredditPostCollector.ReadPostsList", _readPostsList);
            }
        }
        private HashList<string, int> _readPostsList;
    }
}
