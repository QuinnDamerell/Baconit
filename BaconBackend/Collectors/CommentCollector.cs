using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using BaconBackend.Managers;

namespace BaconBackend.Collectors
{
    /// <summary>
    ///     The args for the OnCommentCollectionUpdatedArgs event.
    /// </summary>
    public class CommentCollectionUpdatedArgs : EventArgs
    {
        public List<Comment> ChangedComment;
        public int StartingPosition;
    }

    public class CommentCollector : Collector<Comment>
    {
        private readonly BaconManager _baconMan;

        //
        // Private vars
        //
        private readonly Post _post;

        public CommentCollector(CommentCollectorContext context, BaconManager baconMan)
            : base(baconMan, context.UniqueId)
        {
            // Set the vars
            _baconMan = baconMan;
            _post = context.Post;
            var user = context.User;

            string commentBaseUrl;
            string optionalParams = null;
            bool hasEmptyRoot;
            bool takeFirstArray;

            if (_post != null)
            {
                // See if it a force comment
                if (!string.IsNullOrWhiteSpace(context.ForceComment))
                {
                    // Set a unique id for this request
                    SetUniqueId(_post.Id + context.ForceComment);

                    // Make the url
                    commentBaseUrl = $"{context.Post.Permalink}{context.ForceComment}.json";
                    optionalParams = "context=3";
                }
                else
                {
                    // Get the post url
                    commentBaseUrl = $"/r/{context.Post.Subreddit}/comments/{context.Post.Id}.json";

                    optionalParams = $"sort={ConvertSortToUrl(_post.CommentSortType)}";
                }

                hasEmptyRoot = true;
                takeFirstArray = false;
            }
            else
            {
                commentBaseUrl = $"user/{user.Name}/comments/.json";
                hasEmptyRoot = false;
                takeFirstArray = true;

                switch (context.UserSort)
                {
                    case SortTypes.Controversial:
                        optionalParams = "sort=controversial";
                        break;
                    case SortTypes.Hot:
                        optionalParams = "sort=hot";
                        break;
                    case SortTypes.New:
                        optionalParams = "sort=new";
                        break;
                    case SortTypes.Top:
                        optionalParams = "sort=top";
                        break;
                    case SortTypes.Rising:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // Make the helper, we need to ask it to make a fake root and not to take the
            // first element, the second element is the comment tree.
            InitListHelper(commentBaseUrl, hasEmptyRoot, takeFirstArray, optionalParams);

            _baconMan.UserMan.OnUserUpdated += OnUserUpdated;
        }

        /// <summary>
        ///     Returns a collector for the given type. If the collector doesn't exist one will be created.
        /// </summary>
        /// <param name="post"></param>
        /// <param name="baconMan"></param>
        /// <param name="forceComment"></param>
        /// <returns></returns>
        public static CommentCollector GetCollector(Post post, BaconManager baconMan, string forceComment = null)
        {
            var context = new CommentCollectorContext
            {
                ForceComment = forceComment,
                Post = post,
                UniqueId = post.Id + (string.IsNullOrWhiteSpace(forceComment) ? string.Empty : forceComment) +
                           post.CommentSortType
            };
            return (CommentCollector) GetCollector(typeof(CommentCollector), context.UniqueId,
                context, baconMan);
        }

        /// <summary>
        ///     Returns a collector for the given type. If the collector doesn't exist one will be created.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="baconMan"></param>
        /// <param name="sort"></param>
        /// <returns></returns>
        public static CommentCollector GetCollector(User user, BaconManager baconMan, SortTypes sort = SortTypes.New)
        {
            var context = new CommentCollectorContext {User = user, UserSort = sort};
            context.UniqueId = "t2_" + user.Id + sort;
            return (CommentCollector) GetCollector(typeof(CommentCollector), context.UniqueId,
                context, baconMan);
        }

        /// <summary>
        ///     Fired when the current user is updated.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnUserUpdated(object sender, UserUpdatedArgs args)
        {
            // If a user is added or removed update the subreddit to reflect the new user.
            if (args.Action != UserCallbackAction.Updated) Update(true);
        }

        /// <summary>
        ///     Converts an element list to a post list.
        /// </summary>
        /// <param name="elements"></param>
        /// <returns></returns>
        protected override List<Comment> ParseElementList(List<Element<Comment>> elements)
        {
            // Converts the elements into a list.
            var flatList = new List<Comment>();
            ParseElementListRecur(ref flatList, elements, 0);
            return flatList;
        }

        private static void ParseElementListRecur(ref List<Comment> flatList, IEnumerable<Element<Comment>> node, int level)
        {
            foreach (var element in node)
            {
                // Ignore elements that don't have an author, these are meta text
                if (!string.IsNullOrWhiteSpace(element.Data.Author))
                {
                    // Add the comment
                    element.Data.CommentDepth = level;
                    flatList.Add(element.Data);
                }

                // Check for children
                if (element.Data.Replies != null && element.Data.Replies.Data.Children.Count > 0)
                    ParseElementListRecur(ref flatList, element.Data.Replies.Data.Children, level + 1);
            }
        }

        /// <summary>
        ///     Applies any common formatting to the comments.
        /// </summary>
        /// <param name="comments">Comments to be formatted</param>
        protected override void ApplyCommonFormatting(ref List<Comment> comments)
        {
            foreach (var comment in comments)
            {
                // Set The time
                var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                var postTime = origin.AddSeconds(comment.CreatedUtc).ToLocalTime();
                comment.TimeString = TimeToTextHelper.TimeElapseToText(postTime) + " ago";

                // Set if this post is from the op
                comment.IsCommentFromOp = _post != null && comment.Author.Equals(_post.Author);

                // Set if this comment is from the current user
                if (_baconMan.UserMan.IsUserSignedIn && _baconMan.UserMan.CurrentUser != null)
                    comment.IsCommentOwnedByUser =
                        _baconMan.UserMan.CurrentUser.Name.Equals(comment.Author, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        ///     Used to add a comment is edited by the user.
        /// </summary>
        /// <param name="newComment"></param>
        private void UpdateComment(Comment newComment)
        {
            // First get the guts of the list helper.
            var currentElements = GetListHelperElements();

            // Here is a hard one. To edit the comment in to the comment tree we need to run through
            // the current elements and figure out where it goes.
            var insertedPos = 0;
            var wasSuccess =
                InjectCommentRecursive(ref currentElements, newComment.Id, newComment, true, ref insertedPos);

            if (!wasSuccess) return;
            // Fire the updated event to show on the UI.
            FireCollectionUpdated(insertedPos, new List<Comment> {newComment}, false, false);

            // Fire collection state changed
            FireStateChanged(1);
        }

        /// <summary>
        ///     Used to add a comment that was added by the user
        /// </summary>
        /// <param name="parentId"></param>
        /// <param name="comment"></param>
        private void InjectComment(string parentId, Comment comment)
        {
            // First get the guts of the list helper.
            var currentElements = GetListHelperElements();

            // This is a reply to a post, just add it to the bottom of the comment list
            if (parentId.StartsWith("t3_"))
            {
                // Get the current count.
                var listLength = GetCurrentPostsInternal().Count;

                // Add the new comment to the end of the list.
                currentElements.Add(new Element<Comment> {Data = comment, Kind = "t1"});

                // Fire the updated event to show on the UI.
                FireCollectionUpdated(listLength + 1, new List<Comment> {comment}, false, false);

                // Fire the collection state changed
                FireStateChanged(1);
            }
            else
            {
                // Here is a hard one. To inject the comment in to the comment tree we need to run through
                // the current elements and figure out where it goes.
                var insertedPos = 0;
                var parentIdWithoutType = parentId.Substring(3);
                var wasSuccess = InjectCommentRecursive(ref currentElements, parentIdWithoutType, comment, false,
                    ref insertedPos);

                if (!wasSuccess) return;
                // Fire the updated event to show on the UI.
                FireCollectionUpdated(insertedPos, new List<Comment> {comment}, false, true);

                // Fire collection state changed
                FireStateChanged(1);
            }
        }

        private bool InjectCommentRecursive(ref List<Element<Comment>> elementList, string searchingParent,
            Comment comment, bool isEdit, ref int insertedPos)
        {
            // Search through this tear
            foreach (var t in elementList)
            {
                // Increment insert pos
                insertedPos++;

                // Try to match
                var element = t;
                if (element.Data.Id.Equals(searchingParent))
                {
                    if (isEdit)
                    {
                        // Give this comment the replies of the old one.
                        comment.Replies = element.Data.Replies;
                        comment.CommentDepth = element.Data.CommentDepth;

                        // Update the comment
                        element.Data = comment;

                        // Subtract one since we are replacing not inserting after.
                        insertedPos--;
                    }
                    else
                    {
                        // We found you dad! (or mom, why not)

                        // Make sure it can have children
                        if (element.Data.Replies == null)
                        {
                            element.Data.Replies = new RootElement<Comment>
                            {
                                Data = new ElementList<Comment> {Children = new List<Element<Comment>>()}
                            };
                        }

                        // Set the comment depth
                        comment.CommentDepth = element.Data.CommentDepth + 1;

                        // Add it!
                        element.Data.Replies.Data.Children.Insert(0,
                            new Element<Comment> {Data = comment, Kind = "t1"});
                    }

                    // Return success!
                    return true;
                }

                // If no match ask his kids.
                if (element.Data.Replies != null && InjectCommentRecursive(ref element.Data.Replies.Data.Children,
                    searchingParent, comment, isEdit, ref insertedPos)) return true;
            }

            // We failed here
            return false;
        }

        /// <summary>
        ///     Given a comment and a starting index this function will return the collection post
        ///     object and the true index of the item. If not found it will return null and -1
        /// </summary>
        /// <param name="comment"></param>
        /// <param name="index"></param>
        private void FindCommentInCurrentCollection(ref Comment comment, ref int index)
        {
            // Get the current list
            var comments = GetCurrentPostsInternal();

            // Find the post starting at the possible index
            for (; index < comments.Count; index++)
                if (comments[index].Id.Equals(comment.Id))
                {
                    // Grab the post and break;
                    comment = comments[index];
                    return;
                }

            // If we didn't find it kill them.
            index = -1;
            comments = null;
        }

        private static string ConvertSortToUrl(CommentSortTypes types)
        {
            switch (types)
            {
                default:
                case CommentSortTypes.Best:
                    return "confidence";
                case CommentSortTypes.Controversial:
                    return "controversial";
                case CommentSortTypes.New:
                    return "new";
                case CommentSortTypes.Old:
                    return "old";
                case CommentSortTypes.Qa:
                    return "qa";
                case CommentSortTypes.Top:
                    return "top";
            }
        }

        public class CommentCollectorContext
        {
            public string ForceComment;
            public Post Post;
            public string UniqueId;
            public User User;
            public SortTypes UserSort;
        }

        #region Comment Modifiers

        /// <summary>
        ///     Called by the consumer when a comment vote should be changed
        /// </summary>
        /// <param name="comment">The post to be action</param>
        /// <param name="action"></param>
        /// <param name="postPosition">A hint to the position of the post</param>
        public void ChangeCommentVote(Comment comment, PostVoteAction action, int postPosition = 0)
        {
            // Ensure we are signed in.
            if (!_baconMan.UserMan.IsUserSignedIn)
            {
                _baconMan.MessageMan.ShowSigninMessage("vote");
                return;
            }

            // Using the post and suggested index, find the real post and index
            var collectionComment = comment;
            FindCommentInCurrentCollection(ref collectionComment, ref postPosition);

            if (collectionComment == null || postPosition == -1)
                // We didn't find it.
                return;

            // Update the like status
            var likesAction = action == PostVoteAction.UpVote;
            var voteMultiplier = action == PostVoteAction.UpVote ? 1 : -1;
            if (collectionComment.Likes.HasValue)
            {
                if (collectionComment.Likes.Value == likesAction)
                {
                    // duplicate vote would undo the action
                    collectionComment.Likes = null;
                    collectionComment.Score -= voteMultiplier;
                }
                else
                {
                    // opposite vote, takes into account previous vote
                    collectionComment.Likes = likesAction;
                    collectionComment.Score += 2 * voteMultiplier;
                }
            }
            else
            {
                // first vote
                collectionComment.Likes = likesAction;
                collectionComment.Score += voteMultiplier;
            }

            // Fire off that a update happened.
            FireCollectionUpdated(postPosition, new List<Comment> {collectionComment}, false, false);

            // Start a task to make the vote
            new Task(async () =>
            {
                try
                {
                    // Build the data
                    var voteDir = collectionComment.Likes.HasValue ? collectionComment.Likes.Value ? "1" : "-1" : "0";
                    var postData = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("id", "t1_" + collectionComment.Id),
                        new KeyValuePair<string, string>("dir", voteDir)
                    };

                    // Make the call
                    var str = await _baconMan.NetworkMan.MakeRedditPostRequestAsString("api/vote", postData);

                    // Do some super simple validation
                    if (str != "{}") throw new Exception("Failed to set vote! The response indicated a failure");
                }
                catch (Exception ex)
                {
                    _baconMan.MessageMan.DebugDia("failed to vote!", ex);
                    _baconMan.MessageMan.ShowMessageSimple("That's Not Right",
                        "Something went wrong while trying to cast your vote, try again later.");
                }
            }).Start();
        }

        /// <summary>
        ///     Called when the user added a comment or edit an existing comment.
        /// </summary>
        /// <returns></returns>
        public bool CommentAddedOrEdited(string parentOrOrgionalId, string serverResponse, bool isEdit)
        {
            // Assume if we can find author we are successful. Not sure if that is safe or not... :)
            if (!string.IsNullOrWhiteSpace(serverResponse) && serverResponse.Contains("\"author\""))
            {
                // Do the next part in a try catch so if we fail we will still report success since the
                // message was sent to reddit.
                try
                {
                    // Parse the new comment
                    var newComment = MiscellaneousHelper.ParseOutRedditDataElement<Comment>(_baconMan, serverResponse)
                        .Result;

                    if (isEdit)
                        UpdateComment(newComment);
                    else
                        // Inject the new comment
                        InjectComment(parentOrOrgionalId, newComment);
                }
                catch (Exception e)
                {
                    // We fucked up adding the comment to the UI.
                    _baconMan.MessageMan.DebugDia("Failed injecting comment", e);
                    TelemetryManager.ReportUnexpectedEvent(this, "AddCommentSuccessButAddUiFailed");
                }

                // If we get to adding to the UI return true because reddit has the comment.
                return true;
            }

            // Reddit returned something wrong
            _baconMan.MessageMan.ShowMessageSimple("That's not right",
                "Sorry we can't post your comment right now, reddit returned and unexpected message.");
            TelemetryManager.ReportUnexpectedEvent(this, "CommentPostReturnedUnexpectedMessage");
            return false;
        }

        /// <summary>
        ///     Called when a comment should be deleted
        /// </summary>
        /// <param name="comment"></param>
        public void CommentDeleteRequest(Comment comment)
        {
            // Start a task to delete the comment
            new Task(async () =>
            {
                try
                {
                    // Build the data
                    var postData = new List<KeyValuePair<string, string>>();
                    postData.Add(new KeyValuePair<string, string>("id", "t1_" + comment.Id));

                    // Make the call
                    var str = await _baconMan.NetworkMan.MakeRedditPostRequestAsString("/api/del", postData);

                    // Do some super simple validation
                    if (str != "{}") throw new Exception("Failed to delete comment! The response indicated a failure");

                    // If successful, mark it as deleted in the UI.
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        comment.Body = "[deleted]";
                        comment.IsDeleted = true;
                        UpdateComment(comment);
                    });
                }
                catch (Exception ex)
                {
                    _baconMan.MessageMan.DebugDia("failed to vote!", ex);
                    _baconMan.MessageMan.ShowMessageSimple("That's Not Right",
                        "Something went wrong while trying to delete your comment, check your internet connection.");
                }
            }).Start();
        }

        #endregion
    }
}