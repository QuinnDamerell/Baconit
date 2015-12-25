using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using Baconit.HelperControls;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace Baconit.Panels
{
    public class FlipViewPostCommentManager
    {
        CommentCollector m_commentCollector;

        /// <summary>
        /// Holds a full list of the comments, even collapsed comments
        /// </summary>
        List<Comment> m_fullCommentList = new List<Comment>();

        /// <summary>
        /// Indicates if we tried to load comments or not
        /// </summary>
        bool m_attemptedToLoadComments = false;

        /// <summary>
        /// Indicates we are trying to show a comment.
        /// </summary>
        string m_targetComment = null;

        /// <summary>
        /// Indicates if we should show a subset of comments or the entire thread.
        /// </summary>
        bool m_showThreadSubset = false;

        /// <summary>
        /// The current post for the comment manager
        /// </summary>
        public Post Post
        {
            get { return m_post; }
        }
        Post m_post;

        public FlipViewPostCommentManager(ref Post post, string targetComment, bool showThreadSubset)
        {
            m_post = post;
            m_targetComment = targetComment;
            m_showThreadSubset = showThreadSubset;

            // If we have a target comment and we want to show only a subset.
            if (showThreadSubset && !String.IsNullOrWhiteSpace(targetComment))
            {
                post.FlipViewShowEntireThreadMessage = Visibility.Visible;
            }
            else
            {
                post.FlipViewShowEntireThreadMessage = Visibility.Collapsed;
            }

            // This post might have comments if opened already in flip view. Clear them out if it does.
            m_post.Comments.Clear();
        }

        /// <summary>
        /// Attempts to prefetch comments. Returns true if it is going, or false it not.
        /// </summary>
        /// <returns></returns>
        public bool PreFetchComments()
        {
            // Only attempt to do this once.
            if(m_attemptedToLoadComments)
            {
                return false;
            }
            m_attemptedToLoadComments = true;

            // Do this in a background thread
            Task.Run(() =>
            {
                // Get the comment collector, if we don't want to show a subset don't give it the target comment
                m_commentCollector = CommentCollector.GetCollector(m_post, App.BaconMan, m_showThreadSubset ? m_targetComment : null);

                // Sub to collection callbacks for the comments.
                m_commentCollector.OnCollectionUpdated += CommentCollector_OnCollectionUpdated;
                m_commentCollector.OnCollectorStateChange += CommentCollector_OnCollectorStateChange;

                // We have to ask for all the comments here bc we can't extend.
                if (!m_commentCollector.Update(false, m_post.CurrentCommentShowingCount))
                {
                    // If update returns false it isn't going to update because it has a cache. So just show the
                    // cache.
                    OnCollectionUpdatedArgs<Comment> args = new OnCollectionUpdatedArgs<Comment>()
                    {
                        ChangedItems = m_commentCollector.GetCurrentPosts(),
                        IsFreshUpdate = true,
                        IsInsert = false,
                        StartingPosition = 0
                    };
                    CommentCollector_OnCollectionUpdated(null, args);
                }
            });

            return true;
        }

        /// <summary>
        /// Fired when the comment sort is changed.
        /// </summary>
        public void ChangeCommentSort()
        {
            // Show loading
            m_post.FlipViewShowLoadingMoreComments = true;

            // Do this in a background thread
            Task.Run(() =>
            {
                // Kill the current collector
                if (m_commentCollector != null)
                {
                    m_commentCollector.OnCollectionUpdated -= CommentCollector_OnCollectionUpdated;
                    m_commentCollector.OnCollectorStateChange -= CommentCollector_OnCollectorStateChange;
                }
                m_commentCollector = null;

                // Get a new collector with the new sort
                m_commentCollector = CommentCollector.GetCollector(m_post, App.BaconMan);

                // Sub to collection callbacks for the comments.
                m_commentCollector.OnCollectionUpdated += CommentCollector_OnCollectionUpdated;
                m_commentCollector.OnCollectorStateChange += CommentCollector_OnCollectorStateChange;

                // Force our collector to update.
                if (!m_commentCollector.Update(true, m_post.CurrentCommentShowingCount))
                {
                    // If update returns false it isn't going to update because it has a cache. So just show the
                    // cache.
                    OnCollectionUpdatedArgs<Comment> args = new OnCollectionUpdatedArgs<Comment>()
                    {
                        ChangedItems = m_commentCollector.GetCurrentPosts(),
                        IsFreshUpdate = true,
                        IsInsert = false,
                        StartingPosition = 0
                    };
                    CommentCollector_OnCollectionUpdated(null, args);
                }
            });
        }


        /// <summary>
        /// Updates the showing comment count and refreshes
        /// </summary>
        /// <param name="newCount"></param>
        public async void UpdateShowingCommentCount(int newCount)
        {
            // Show loading
            m_post.FlipViewShowLoadingMoreComments = true;

            // Dispatch to the UI thread so this will happen a bit later to give time for the animations to finish.
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                // Update the count
                m_post.CurrentCommentShowingCount = newCount;

                // Refresh
                Refresh();
            });
        }


        /// <summary>
        /// Kicks off a refresh of the comments.
        /// </summary>
        public void Refresh()
        {
            // Show loading
            m_post.FlipViewShowLoadingMoreComments = true;

            // Do this in a background thread
            Task.Run(() =>
            {
                // If we have a collector kick off an update.
                if (m_commentCollector != null)
                {
                    m_commentCollector.Update(true, m_post.CurrentCommentShowingCount);
                }
            });
        }

        /// <summary>
        /// Called when we need more posts because we are scrolling down.
        /// </summary>
        public async void RequestMorePosts()
        {
            bool showLoadingUi = false;

            // Call prefetch comments to ensure we have tried to load them. If the user has prefetch comments off
            // they will not be loaded. This call will be ignored if we are already trying.
            if(PreFetchComments())
            {
                // We are fetching comments. Show the loading UI.
                showLoadingUi = true;
            }
            else
            {
                // Show the loading IU if we are still updating.
                showLoadingUi = m_commentCollector.State == CollectorState.Extending || m_commentCollector.State == CollectorState.Updating;
            }

            // Show the loading footer if we should.
            if(showLoadingUi)
            {
                // Dispatch to the UI thread
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    m_post.ShowCommentLoadingMessage = Visibility.Visible;
                });
            }
        }

        public void PrepareForDeletion()
        {
            // Clear out, we are going to be deleted
            m_post.Comments.Clear();

            if(m_fullCommentList != null)
            {
                m_fullCommentList.Clear();
            }

            // Kill the collector
            if (m_commentCollector != null)
            {
                m_commentCollector.OnCollectionUpdated -= CommentCollector_OnCollectionUpdated;
                m_commentCollector.OnCollectorStateChange -= CommentCollector_OnCollectorStateChange;
            }
            m_commentCollector = null;
        }

        private async void CommentCollector_OnCollectorStateChange(object sender, OnCollectorStateChangeArgs e)
        {
            // #todo handle when there are no more
            if(e.State == CollectorState.Idle || e.State == CollectorState.FullyExtended)
            {
                // When we are idle hide the loading message.
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    // Check if we have any comments
                    if(e.NewPostCount == 0 && m_post.Comments.Count == 0)
                    {
                        m_post.ShowCommentLoadingMessage = Visibility.Visible;
                        m_post.ShowCommentsErrorMessage = "No Comments";
                        m_post.FlipViewShowLoadingMoreComments = false;
                    }
                    else
                    {
                        m_post.ShowCommentLoadingMessage = Visibility.Collapsed;
                        m_post.FlipViewShowLoadingMoreComments = false;
                    }               
                });
            }
            else if(e.State == CollectorState.Error)
            {
                // Show an error message if we error
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    m_post.ShowCommentsErrorMessage = "Error Loading Comments";
                    m_post.FlipViewShowLoadingMoreComments = false;
                });
            }
        }

        private async void CommentCollector_OnCollectionUpdated(object sender, OnCollectionUpdatedArgs<Comment> e)
        {
            // Dispatch to the UI thread
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Setup the insert
                int insertIndex = e.StartingPosition;

                // Lock the list
                lock (m_post.Comments)
                {
                    lock(m_fullCommentList)
                    {
                        if (e.IsFreshUpdate)
                        {
                            // Reset the full list
                            m_fullCommentList.Clear();


                            // For fresh updates we can just replace everything and expand anything that isn't.
                            for (int i = 0; i < e.ChangedItems.Count; i++)
                            {
                                Comment newComment = e.ChangedItems[i];
                                Comment currentComment = i >= m_post.Comments.Count ? null : m_post.Comments[i];

                                if (currentComment == null)
                                {
                                    m_post.Comments.Add(newComment);
                                }
                                else
                                {
                                    if (newComment.Id.Equals(currentComment.Id))
                                    {
                                        // Update the comment
                                        m_post.Comments[i].Author = newComment.Author;
                                        m_post.Comments[i].Score = newComment.Score;
                                        m_post.Comments[i].TimeString = newComment.TimeString;
                                        m_post.Comments[i].CollapsedCommentCount = newComment.CollapsedCommentCount;
                                        m_post.Comments[i].Body = newComment.Body;
                                        m_post.Comments[i].Likes = newComment.Likes;
                                        m_post.Comments[i].ShowFullComment = true;
                                    }
                                    else
                                    {
                                        // Replace it
                                        m_post.Comments[i] = newComment;
                                    }
                                }

                                // Always add to the full list
                                m_fullCommentList.Add(newComment);
                            }

                            // Trim off anything that shouldn't be here anymore
                            while (m_post.Comments.Count > e.ChangedItems.Count)
                            {
                                m_post.Comments.RemoveAt(m_post.Comments.Count - 1);
                            }
                        }
                        else
                        {
                            // This is tricky because the comment list in the post is a subset of the main list due to collapse.
                            // Thus items are missing or moved in that list.

                            // How we will do it is the following. We will make all of actions to the main list. If the operation is
                            // "add at the end" we will do it to both lists because it is safe no matter what the state of the comment list.
                            // If we have an insert or replace we will build two lists. Once the main list is updated we will then address updating
                            // the comment list properly.

                            // This list tracks any inserts we need to do. The key is the parent comment id, the value is the comment to
                            // be inserted.
                            List<KeyValuePair<string, Comment>> insertList = new List<KeyValuePair<string, Comment>>();

                            // This list tracks any replaces we need to do. The key is the parent comment id, the value is the comment to
                            // be inserted.
                            List<KeyValuePair<string, Comment>> replaceList = new List<KeyValuePair<string, Comment>>();

                            // First update the main list
                            foreach (Comment comment in e.ChangedItems)
                            {
                                if (m_targetComment != null && comment.Id.Equals(m_targetComment))
                                {
                                    comment.IsHighlighted = true;
                                }

                                // Check if this is a add or replace if not an insert
                                bool isReplace = insertIndex < m_fullCommentList.Count;

                                // If we are inserting just insert it where it should be.
                                if (e.IsInsert)
                                {
                                    m_fullCommentList.Insert(insertIndex, comment);

                                    // Make sure we have a parent, if not use empty string
                                    string parentComment = insertIndex > 0 ? m_fullCommentList[insertIndex - 1].Id : string.Empty;
                                    insertList.Add(new KeyValuePair<string, Comment>(parentComment, comment));
                                }
                                else if (isReplace)
                                {
                                    // Grab the id that we are replacing and the comment to replace it.
                                    replaceList.Add(new KeyValuePair<string, Comment>(m_fullCommentList[insertIndex].Id, comment));

                                    // Replace the current item
                                    m_fullCommentList[insertIndex] = comment;
                                }
                                else
                                {
                                    // Add it to the end of the main list
                                    m_fullCommentList.Add(comment);

                                    // If we are adding it to the end of the main list it is safe to add it to the end of the UI list.
                                    m_post.Comments.Add(comment);
                                }
                                insertIndex++;
                            }

                            // Now deal with the insert list.
                            foreach (KeyValuePair<string, Comment> insertPair in insertList)
                            {
                                // If the key is empty string we are inserting into the head
                                if (String.IsNullOrWhiteSpace(insertPair.Key))
                                {
                                    m_post.Comments.Insert(0, insertPair.Value);
                                }
                                else
                                {
                                    // Try to find the parent comment.
                                    for (int i = 0; i < m_post.Comments.Count; i++)
                                    {
                                        Comment comment = m_post.Comments[i];
                                        if (comment.Id.Equals(insertPair.Key))
                                        {
                                            // We found the parent, it is not collapsed we should insert this comment after it.
                                            if (comment.ShowFullComment)
                                            {
                                                m_post.Comments.Insert(i + 1, insertPair.Value);
                                            }

                                            // We are done, break out of this parent search.
                                            break;

                                        }
                                    }
                                }
                            }

                            // Now deal with the replace list.
                            for (int replaceCount = 0; replaceCount < replaceList.Count; replaceCount++)
                            {
                                KeyValuePair<string, Comment> replacePair = replaceList[replaceCount];

                                // Try to find the comment we will replace; Note if is very important that we start at the current replace point
                                // because this comment might have been added before this count already due to a perviouse replace. In that case
                                // we don't want to accidentally find that one instead of this one.
                                for (int i = replaceCount; i < m_post.Comments.Count; i++)
                                {
                                    Comment comment = m_post.Comments[i];
                                    if (comment.Id.Equals(replacePair.Key))
                                    {
                                        // If the id is the same we are updating. If we replace the comment the UI will freak out,
                                        // so just update the UI values
                                        if (comment.Id.Equals(replacePair.Value.Id))
                                        {
                                            m_post.Comments[i].Author = comment.Author;
                                            m_post.Comments[i].Score = comment.Score;
                                            m_post.Comments[i].TimeString = comment.TimeString;
                                            m_post.Comments[i].CollapsedCommentCount = comment.CollapsedCommentCount;
                                            m_post.Comments[i].Body = comment.Body;
                                            m_post.Comments[i].Likes = comment.Likes;
                                        }
                                        else
                                        {
                                            // Replace the comment with this one
                                            m_post.Comments[i] = replacePair.Value;
                                        }

                                        // We are done, break out of the search for the match.
                                        break;

                                    }
                                }
                            }
                        }
                    }
                }
            });
        }

        #region Comment Click Listeners

        public void UpVote_Tapped(Comment comment)
        {
            m_commentCollector.ChangeCommentVote(comment, PostVoteAction.UpVote);
        }

        public void DownVote_Tapped(Comment comment)
        {
            m_commentCollector.ChangeCommentVote(comment, PostVoteAction.DownVote);
        }

        public void Share_Tapped(Comment comment)
        {
            ShareComment(comment);
        }

        public async void Save_Tapped(Comment comment)
        {
            // Update the UI now
            comment.IsSaved = !comment.IsSaved;

            // Make the call
            bool success = await MiscellaneousHelper.SaveOrHideRedditItem(App.BaconMan, "t1_" + comment.Id, comment.IsSaved, null);

            // If we failed revert
            if (!success)
            {
                comment.IsSaved = !comment.IsSaved;
            }
        }

        public void CopyPermalink_Tapped(Comment comment)
        {
            // Get the link and copy the url into the clipboard
            string commentLink = "https://reddit.com" + m_post.Permalink + comment.Id;
            DataPackage data = new DataPackage();
            data.SetText(commentLink);
            Clipboard.SetContent(data);
        }

        public async void Collpase_Tapped(Comment comment)
        {
            // Kick delay to the UI thread so the animations can continue
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                CollapseCommentsFromComment(comment);
            });
        }

        public async void Expand_Tapped(Comment comment)
        {
            // Kick delay to the UI thread so the animations can continue
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                ExpandCommentsFromComment(comment);
            });
        }

        #endregion

        #region Share Logic

        // Again. Barh.
        Comment m_shareComment = null;
        private void ShareComment(Comment comment)
        {
            m_shareComment = comment;
            // Setup the share contract so we can share data
            DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
            dataTransferManager.DataRequested += DataTransferManager_DataRequested;
            DataTransferManager.ShowShareUI();
        }

        private void DataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            if (m_shareComment != null)
            {
                string commentLink = "https://reddit.com" + m_post.Permalink + m_shareComment.Id;
                // #todo use a markdown-less body text
                string shareBody = m_shareComment.Body.Length > 50 ? m_shareComment.Body.Substring(0, 50) + "..." : m_shareComment.Body;
                args.Request.Data.Properties.ApplicationName = "Baconit";
                args.Request.Data.Properties.ContentSourceWebLink = new Uri(commentLink, UriKind.Absolute);
                args.Request.Data.Properties.Title = "A Reddit Post Shared From Baconit";
                args.Request.Data.Properties.Description = shareBody;
                args.Request.Data.SetText($"Check this out! \r\n\r\n{shareBody}\r\n\r\n{commentLink}");
                m_shareComment = null;
                App.BaconMan.TelemetryMan.ReportEvent(this, "CommentShared");
            }
            else
            {
                args.Request.FailWithDisplayText("Baconit doesn't have anything to share!");
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToShareCommentHelperCommentNoShareComment");
            }
        }

        #endregion

        #region Collapse Logic

        public void CollapseCommentsFromComment(Comment comment)
        {
            int foundLevel = -1;
            int killedComment = 0;

            // Lock the list
            lock (m_post.Comments)
            {
                // Go through all of the comments
                for (int i = 0; i < m_post.Comments.Count; i++)
                {
                    // If found level is set we have already seen the comment
                    if (foundLevel != -1)
                    {
                        // If this comment is higher than the root collapse kill it
                        if (m_post.Comments[i].CommentDepth > foundLevel)
                        {
                            // Remove it
                            m_post.Comments.RemoveAt(i);
                            i--;

                            // Update kill count
                            killedComment++;

                            // Move on
                            continue;
                        }
                        else
                        {
                            // We have found the next comment at the same level or lower
                            // we are done.
                            break;
                        }
                    }

                    // If we are here we haven't found the comment yet.
                    else if (m_post.Comments[i].Id.Equals(comment.Id))
                    {
                        // We found it! Note the level
                        foundLevel = comment.CommentDepth;
                    }
                }

                // We are done, close the comment
                comment.CollapsedCommentCount = "+" + killedComment;
                comment.ShowFullComment = false;
            }
        }

        public void ExpandCommentsFromComment(Comment comment)
        {
            // Lock the list
            lock (m_post.Comments)
            {
                lock (m_fullCommentList)
                {
                    // First, we need to find where in the UI we need to add comments
                    int inserationPoint = -1;
                    int expandRootLevel = 0;
                    for (int i = 0; i < m_post.Comments.Count; i++)
                    {
                        if (m_post.Comments[i].Id.Equals(comment.Id))
                        {
                            // We found it!
                            inserationPoint = i;
                            expandRootLevel = m_post.Comments[i].CommentDepth;
                            break;
                        }
                    }
                    // Move past the comment
                    inserationPoint++;

                    // Now we have to find the comment in the full list
                    int fullListPoint = -1;
                    for (int i = 0; i < m_fullCommentList.Count; i++)
                    {
                        if (m_fullCommentList[i].Id.Equals(comment.Id))
                        {
                            // We found it!
                            fullListPoint = i;
                            break;
                        }
                    }
                    // Move past the comment
                    fullListPoint++;

                    // Next, Fill in the comments to the UI list until we hit
                    // a level lower than the original
                    for (int i = fullListPoint; i < m_fullCommentList.Count; i++)
                    {
                        if (m_fullCommentList[i].CommentDepth <= expandRootLevel)
                        {
                            // We found a lower level comment, leave now.
                            break;
                        }

                        // Insert this comment into the UI list
                        m_post.Comments.Insert(inserationPoint, m_fullCommentList[i]);
                        inserationPoint++;
                    }

                    // Set the original comment back to normal
                    comment.ShowFullComment = true;
                }
            }
        }
       
        #endregion
    }
}
