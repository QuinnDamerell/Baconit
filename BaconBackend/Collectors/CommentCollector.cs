using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.Collectors
{
    /// <summary>
    /// The args for the OnCommentCollectionUpdatedArgs event.
    /// </summary>
    public class OnCommentCollectionUpdatedArgs : EventArgs
    {
        public int StartingPosition;
        public List<Comment> ChangedComment;
    }

    public class CommentCollector : Collector<Comment>
    {
        public class CommentCollectorContext
        {
            public Post post;
            public User user;
            public SortTypes userSort;
            public string forceComment;
            public string UniqueId;
        }

        /// <summary>
        /// Returns a collector for the given type. If the collector doesn't exist one will be created.
        /// </summary>
        /// <param name="subreddit"></param>
        /// <returns></returns>
        public static CommentCollector GetCollector(Post post, BaconManager baconMan, string forceComment = null)
        {
            CommentCollectorContext context = new CommentCollectorContext() { forceComment = forceComment, post = post };
            context.UniqueId = post.Id + (String.IsNullOrWhiteSpace(forceComment) ? String.Empty : forceComment) + post.CommentSortType;
            return (CommentCollector)Collector<Comment>.GetCollector(typeof(CommentCollector), context.UniqueId, context, baconMan);
        }

        /// <summary>
        /// Returns a collector for the given type. If the collector doesn't exist one will be created.
        /// </summary>
        /// <param name="subreddit"></param>
        /// <returns></returns>
        public static CommentCollector GetCollector(User user, BaconManager baconMan, SortTypes sort = SortTypes.New)
        {
            CommentCollectorContext context = new CommentCollectorContext() { user = user, userSort = sort };
            context.UniqueId = "t2_"+user.Id + sort;
            return (CommentCollector)Collector<Comment>.GetCollector(typeof(CommentCollector), context.UniqueId, context, baconMan);
        }

        //
        // Private vars
        //
        Post m_post = null;
        User m_user = null;
        BaconManager m_baconMan;

        public CommentCollector(CommentCollectorContext context, BaconManager baconMan)
            : base(baconMan, context.UniqueId)
        {
            // Set the vars
            m_baconMan = baconMan;
            m_post = context.post;
            m_user = context.user;

            string commentBaseUrl = "";
            string optionalParams = null;
            bool hasEmptyRoot = true;
            bool takeFirstArray = false;

            if (m_post != null)
            {
                // See if it a force comment
                if (!String.IsNullOrWhiteSpace(context.forceComment))
                {
                    // Set a unique id for this request
                    SetUniqueId(m_post.Id + context.forceComment);

                    // Make the url
                    commentBaseUrl = $"{context.post.Permalink}{context.forceComment}.json";
                    optionalParams = "context=3";
                }
                else
                {
                    // Get the post url
                    commentBaseUrl = $"/r/{context.post.Subreddit}/comments/{context.post.Id}.json";

                    optionalParams = $"sort={ConvertSortToUrl(m_post.CommentSortType)}";
                }

                hasEmptyRoot = true;
                takeFirstArray = false;
            }
            else
            {
                commentBaseUrl = $"user/{m_user.Name}/comments/.json";
                hasEmptyRoot = false;
                takeFirstArray = true;

                switch (context.userSort)
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
                }
            }

            // Make the helper, we need to ask it to make a fake root and not to take the
            // first element, the second element is the comment tree.
            InitListHelper(commentBaseUrl, hasEmptyRoot, takeFirstArray, optionalParams);
        }

        /// <summary>
        /// Converts an element list to a post list.
        /// </summary>
        /// <param name="elements"></param>
        /// <returns></returns>
        override protected List<Comment> ParseElementList(List<Element<Comment>> elements)
        {
            // Converts the elements into a list.
            List<Comment> flatList = new List<Comment>();
            ParseElementListRecur(ref flatList, elements, 0);
            return flatList;
        }

        private void ParseElementListRecur(ref List<Comment> flatList, List<Element<Comment>> node, int level)
        {
            foreach(Element<Comment> element in node)
            {
                // Ignore elements that don't have an author, these are meta text
                if (!String.IsNullOrWhiteSpace(element.Data.Author))
                {
                    // Add the comment
                    element.Data.CommentDepth = level;
                    flatList.Add(element.Data);
                }

                // Check for children
                if (element.Data.Replies != null && element.Data.Replies.Data.Children.Count > 0)
                {
                    ParseElementListRecur(ref flatList, element.Data.Replies.Data.Children, level + 1);
                }
            }
        }

        /// <summary>
        /// Applies any common formatting to the comments.
        /// </summary>
        /// <param name="comments">Commetns to be formatted</param>
        override protected void ApplyCommonFormatting(ref List<Comment> comments)
        {
            // #Todo
            foreach(Comment comment in comments)
            {
                // Set The time
                DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                DateTime postTime = origin.AddSeconds(comment.CreatedUtc).ToLocalTime();
                comment.TimeString = TimeToTextHelper.TimeElapseToText(postTime) + " ago";

                // Decode the body
                comment.Body = WebUtility.HtmlDecode(comment.Body);
                comment.AuthorFlairText = WebUtility.HtmlDecode(comment.AuthorFlairText);

                // Set if this post is from the op
                comment.IsCommentFromOp = m_post != null && comment.Author.Equals(m_post.Author);
            }
        }

        /// <summary>
        /// Used to add a comment that was added by the user
        /// </summary>
        /// <param name="parentId"></param>
        /// <param name="comment"></param>
        private void InjectComment(string parentId, Comment comment)
        {
            // First get the guts of the list helper.
            List<Element<Comment>> currentElements = GetListHelperElements();

            // This is a reply to a post, just add it to the bottom of the comment list
            if(parentId.StartsWith("t3_"))
            {
                // Get the current count.
                int listLength = GetCurrentPostsInternal().Count;

                // Add the new comment to the end of the list.
                currentElements.Add(new Element<Comment>() { Data = comment, Kind = "t1" });

                // Fire the updated event to show on the UI.
                FireCollectionUpdated(listLength + 1, new List<Comment> { comment }, false, false);

                // Fire the collection state changed
                FireStateChanged(1);
            }
            else
            {
                // Here is a hard one. To inject the comment in to the comment tree we need to run through
                // the current elements and figure out where it goes.
                int insertedPos = 0;
                string searchingId = parentId.Substring(3);
                bool wasSuccess = InjectCommentRecursive(ref currentElements, searchingId, comment, ref insertedPos);

                // Fire the updated event to show on the UI.
                FireCollectionUpdated(insertedPos, new List<Comment> { comment }, false, true);

                // Fire collection state changed
                FireStateChanged(1);
            }
        }

        private bool InjectCommentRecursive(ref List<Element<Comment>> elementList, string searchingParent, Comment comment, ref int insertedPos)
        {
            // Search through this tear
            for(int i = 0; i < elementList.Count; i++)
            {
                // Increment insert pos
                insertedPos++;

                // Try to match
                Element<Comment> element = elementList[i];
                if (element.Data.Id.Equals(searchingParent))
                {
                    // We found you dad! (or mom, why not)

                    // Make sure it can have children
                    if(element.Data.Replies == null)
                    {
                        element.Data.Replies = new RootElement<Comment>();
                        element.Data.Replies.Data = new ElementList<Comment>();
                        element.Data.Replies.Data.Children = new List<Element<Comment>>();
                    }

                    // Set the comment depth
                    comment.CommentDepth = element.Data.CommentDepth + 1;

                    // Add it!
                    element.Data.Replies.Data.Children.Insert(0, new Element<Comment>() { Data = comment, Kind = "t1" });

                    // Return success!
                    return true;
                }

                // If no match ask his kids.
                if (element.Data.Replies != null && InjectCommentRecursive(ref element.Data.Replies.Data.Children, searchingParent, comment, ref insertedPos))
                {
                    return true;
                }
            }

            // We failed here
            return false;
        }

        #region Comment Modifiers

        /// <summary>
        /// Called by the consumer when a comment vote should be changed
        /// </summary>
        /// <param name="comment">The post to be actioned</param>
        /// <param name="postPosition">A hint to the position of the post</param>
        public void ChangeCommentVote(Comment comment, PostVoteAction action, int postPosition = 0)
        {
            // Ensure we are signed in.
            if (!m_baconMan.UserMan.IsUserSignedIn)
            {
                m_baconMan.MessageMan.ShowSigninMessage("vote");
                return;
            }

            // Using the post and suggested index, find the real post and index
            Comment collectionComment = comment;
            FindCommentInCurrentCollection(ref collectionComment, ref postPosition);

            if (collectionComment == null || postPosition == -1)
            {
                // We didn't find it.
                return;
            }

            // Update the like status
            bool likesAction = action == PostVoteAction.UpVote;
            int voteMultiplier = action == PostVoteAction.UpVote ? 1 : -1;
            if(collectionComment.Likes.HasValue)
            {
                if(collectionComment.Likes.Value == likesAction)
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
            FireCollectionUpdated(postPosition, new List<Comment>() { collectionComment }, false, false);

            // Start a task to make the vote
            new Task(async () =>
            {
                try
                {
                    // Build the data
                    string voteDir = collectionComment.Likes.HasValue ? collectionComment.Likes.Value ? "1" : "-1" : "0";
                    List<KeyValuePair<string, string>> postData = new List<KeyValuePair<string, string>>();
                    postData.Add(new KeyValuePair<string, string>("id", "t1_" + collectionComment.Id));
                    postData.Add(new KeyValuePair<string, string>("dir", voteDir));

                    // Make the call
                    string str = await m_baconMan.NetworkMan.MakeRedditPostRequestAsString("api/vote", postData);

                    // Do some super simple validation
                    if (str != "{}")
                    {
                        throw new Exception("Failed to set vote! The response indicated a failure");
                    }
                }
                catch (Exception ex)
                {
                    m_baconMan.MessageMan.DebugDia("failed to vote!", ex);
                    m_baconMan.MessageMan.ShowMessageSimple("That's Not Right", "Something went wrong while trying to cast your vote, try again later.");
                }
            }).Start();
        }

        /// <summary>
        /// Called when the user is trying to comment on something.
        /// </summary>
        /// <returns></returns>
        public bool AddNewUserComment(string jsonResponse, string redditIdCommentingOn)
        {
            try
            {
                // Assume if we can find author we are successful. Not sure if that is safe or not... :)
                if(jsonResponse.Contains("\"author\""))
                {
                    // Do the next part in a try catch so if we fail we will still report success since the
                    // message was sent to reddit.
                    try
                    {
                        // Try to parse out the new comment
                        int dataPos = jsonResponse.IndexOf("\"data\":");
                        int dataStartPos = jsonResponse.IndexOf('{', dataPos + 7);
                        int dataEndPos = jsonResponse.IndexOf("}", dataStartPos);
                        string commentData = jsonResponse.Substring(dataStartPos, (dataEndPos - dataStartPos +1));

                        // Parse the new comment
                        Comment newComment = JsonConvert.DeserializeObject<Comment>(commentData);

                        // Inject the new comment
                        InjectComment(redditIdCommentingOn, newComment);
                    }
                    catch(Exception e)
                    {
                        // We fucked up adding the comment to the UI.
                        m_baconMan.MessageMan.DebugDia("Failed injecting comment", e);
                        m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "AddCommentSuccessButAddUiFailed");
                    }
                }
                else
                {
                    // Reddit returned something wrong
                    m_baconMan.MessageMan.ShowMessageSimple("That's not right", "Sorry we can't post your comment right now, reddit returned and unexpected message.");
                    m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "CommentPostReturnedUnexpectedMessage");
                    return false;
                }
            }
            catch (Exception e)
            {
                // Networking fail
                m_baconMan.MessageMan.ShowMessageSimple("That's not right", "Sorry we can't post your comment right now, we can't seem to make a network connection.");
                m_baconMan.MessageMan.DebugDia("failed to send comment", e);
                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToSendComment");
                return false;
            }
            return true;
        }

        #endregion

        /// <summary>
        /// Given a comment and a starting index this function will return the collection post
        /// object and the true index of the item. If not found it will return null and -1
        /// </summary>
        /// <param name="comment"></param>
        /// <param name="index"></param>
        private void FindCommentInCurrentCollection(ref Comment comment, ref int index)
        {
            // Get the current list
            List<Comment> comments = GetCurrentPostsInternal();

            // Find the post starting at the possible index
            for (; index < comments.Count; index++)
            {
                if (comments[index].Id.Equals(comment.Id))
                {
                    // Grab the post and break;
                    comment = comments[index];
                    return;
                }
            }

            // If we didn't find it kill them.
            index = -1;
            comments = null;
        }

        private string ConvertSortToUrl(CommentSortTypes types)
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
                case CommentSortTypes.QA:
                    return "qa";
                case CommentSortTypes.Top:
                    return "top";
            }
        }
    }
}
