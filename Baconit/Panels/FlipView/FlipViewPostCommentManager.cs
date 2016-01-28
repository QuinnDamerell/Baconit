﻿using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using Baconit.HelperControls;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        /// <summary>
        /// The comment collector for this
        /// </summary>
        DeferredCollector<Comment> m_commentCollector;

        /// <summary>
        /// A lock object for the comment collector.
        /// </summary>
        object m_commentCollectorLock = new object();

        /// <summary>
        /// The current post for the comment manager
        /// </summary>
        Post m_post;

        /// <summary>
        /// The current comments.
        /// </summary>
        public ObservableCollection<Comment> Comments
        {
            get
            {
                return m_comments;
            }
        }
        ObservableCollection<Comment> m_comments = new ObservableCollection<Comment>();

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
            Comments.Clear();

            if (!m_post.HaveCommentDefaultsBeenSet)
            {
                // Set the default count and sort for comments
                post.CurrentCommentShowingCount = App.BaconMan.UiSettingsMan.Comments_DefaultCount;
                post.CommentSortType = App.BaconMan.UiSettingsMan.Comments_DefaultSortType;
                m_post.HaveCommentDefaultsBeenSet = true;
            }
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
                // Ensure we have a collector
                DeferredCollector<Comment> collector = EnsureCollector();

                // We have to ask for all the comments here bc we can't extend.
                if (!collector.PreLoadItems(false, m_post.CurrentCommentShowingCount))
                {
                    // If update returns false it isn't going to update because it has a cache. So just show the
                    // cache.
                    OnCollectionUpdatedArgs<Comment> args = new OnCollectionUpdatedArgs<Comment>()
                    {
                        ChangedItems = collector.GetCurrentItems(false),
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
                // Delete the collector
                DeleteCollector();

                // Make a new one.
                DeferredCollector<Comment> collector = EnsureCollector();

                // Force our collector to update.
                if (!collector.LoadAllItems(true, m_post.CurrentCommentShowingCount))
                {
                    // If update returns false it isn't going to update because it has a cache. So just show the
                    // cache.
                    OnCollectionUpdatedArgs<Comment> args = new OnCollectionUpdatedArgs<Comment>()
                    {
                        ChangedItems = collector.GetCurrentItems(true),
                        IsFreshUpdate = true,
                        IsInsert = false,
                        StartingPosition = 0
                    };
                    CommentCollector_OnCollectionUpdated(null, args);
                }
            });
        }

        /// <summary>
        /// Ensures a collector exists
        /// </summary>
        private DeferredCollector<Comment> EnsureCollector()
        {
            lock(m_commentCollectorLock)
            {
                if (m_commentCollector == null)
                {
                    // Get the comment collector, if we don't want to show a subset don't give it the target comment
                    m_commentCollector = new DeferredCollector<Comment>(CommentCollector.GetCollector(m_post, App.BaconMan, m_showThreadSubset ? m_targetComment : null));

                    // Sub to collection callbacks for the comments.
                    m_commentCollector.OnCollectionUpdated += CommentCollector_OnCollectionUpdated;
                    m_commentCollector.OnCollectorStateChange += CommentCollector_OnCollectorStateChange;
                }
                return m_commentCollector;
            }
        }


        /// <summary>
        /// Deletes the current collector
        /// </summary>
        private void DeleteCollector()
        {
            lock (m_commentCollectorLock)
            {
                // Kill the current collector
                if (m_commentCollector != null)
                {
                    m_commentCollector.OnCollectionUpdated -= CommentCollector_OnCollectionUpdated;
                    m_commentCollector.OnCollectorStateChange -= CommentCollector_OnCollectorStateChange;
                }
                m_commentCollector = null;

            }
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
                DeferredCollector<Comment> collector = EnsureCollector();
                if (collector != null)
                {
                    collector.LoadAllItems(true, m_post.CurrentCommentShowingCount);
                }
            });
        }

        /// <summary>
        /// Returns if we are only showing a subset of the comments.
        /// </summary>
        /// <returns></returns>
        public bool IsOnlyShowingSubset()
        {
            DeferredCollector<Comment> collector = EnsureCollector();
            return collector != null ? collector.GetState() == DeferredLoadState.Subset : true;
        }

        /// <summary>
        /// Called when we need more posts because we are scrolling down.
        /// </summary>
        public async void RequestMorePosts()
        {
            // Ensure we have a collector.
            DeferredCollector<Comment> collector = EnsureCollector();

            // As the deferred collector to load all items, if we are doing work show loading.
            if (collector.LoadAllItems())
            {
                // Dispatch to the UI thread
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    // Only show the loading if the current number of comments is 0
                    if(Comments.Count == 0)
                    {
                        m_post.ShowCommentLoadingMessage = Visibility.Visible;
                    }
                });
            }
        }

        public void PrepareForDeletion()
        {
            // Clear out, we are going to be deleted
            Comments.Clear();

            if(m_fullCommentList != null)
            {
                m_fullCommentList.Clear();
            }

            // Kill the collector
            DeleteCollector();
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
                    if(e.NewPostCount == 0 && Comments.Count == 0)
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
                lock (Comments)
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
                                Comment currentComment = i >= Comments.Count ? null : Comments[i];

                                // Check for highlight
                                if (!String.IsNullOrWhiteSpace(m_targetComment) && newComment.Id.Equals(m_targetComment))
                                {
                                    newComment.IsHighlighted = true;
                                }

                                if (currentComment == null)
                                {
                                    Comments.Add(newComment);
                                }
                                else
                                {
                                    if (newComment.Id.Equals(currentComment.Id))
                                    {
                                        // Update the comment
                                        Comments[i].Author = newComment.Author;
                                        Comments[i].Score = newComment.Score;
                                        Comments[i].TimeString = newComment.TimeString;
                                        Comments[i].CollapsedCommentCount = newComment.CollapsedCommentCount;
                                        Comments[i].Body = newComment.Body;
                                        Comments[i].Likes = newComment.Likes;
                                        Comments[i].ShowFullComment = true;
                                    }
                                    else
                                    {
                                        // Replace it
                                        Comments[i] = newComment;
                                    }
                                }

                                // Always add to the full list
                                m_fullCommentList.Add(newComment);
                            }

                            // Trim off anything that shouldn't be here anymore
                            while (Comments.Count > e.ChangedItems.Count)
                            {
                                Comments.RemoveAt(Comments.Count - 1);
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
                                if (!String.IsNullOrWhiteSpace(m_targetComment) && comment.Id.Equals(m_targetComment))
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
                                    Comments.Add(comment);
                                }
                                insertIndex++;
                            }

                            // Now deal with the insert list.
                            foreach (KeyValuePair<string, Comment> insertPair in insertList)
                            {
                                // If the key is empty string we are inserting into the head
                                if (String.IsNullOrWhiteSpace(insertPair.Key))
                                {
                                    Comments.Insert(0, insertPair.Value);
                                }
                                else
                                {
                                    // Try to find the parent comment.
                                    for (int i = 0; i < Comments.Count; i++)
                                    {
                                        Comment comment = Comments[i];
                                        if (comment.Id.Equals(insertPair.Key))
                                        {
                                            // We found the parent, it is not collapsed we should insert this comment after it.
                                            if (comment.ShowFullComment)
                                            {
                                                Comments.Insert(i + 1, insertPair.Value);
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
                                for (int i = replaceCount; i < Comments.Count; i++)
                                {
                                    Comment comment = Comments[i];
                                    if (comment.Id.Equals(replacePair.Key))
                                    {
                                        // If the id is the same we are updating. If we replace the comment the UI will freak out,
                                        // so just update the UI values
                                        if (comment.Id.Equals(replacePair.Value.Id))
                                        {
                                            Comments[i].Author = replacePair.Value.Author;
                                            Comments[i].Score = replacePair.Value.Score;
                                            Comments[i].TimeString = replacePair.Value.TimeString;
                                            Comments[i].CollapsedCommentCount = replacePair.Value.CollapsedCommentCount;
                                            Comments[i].Body = replacePair.Value.Body;
                                            Comments[i].Likes = replacePair.Value.Likes;
                                        }
                                        else
                                        {
                                            // Replace the comment with this one
                                            Comments[i] = replacePair.Value;
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
            DeferredCollector<Comment> collector = EnsureCollector();
            ((CommentCollector)collector.GetCollector()).ChangeCommentVote(comment, PostVoteAction.UpVote);
        }

        public void DownVote_Tapped(Comment comment)
        {
            DeferredCollector<Comment> collector = EnsureCollector();
            ((CommentCollector)collector.GetCollector()).ChangeCommentVote(comment, PostVoteAction.DownVote);
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

        /// <summary>
        /// Called when is added or edited
        /// </summary>
        public bool CommentAddedOrEdited(string parentOrOrgionalId, OnCommentSubmittedArgs args)
        {
            DeferredCollector<Comment> collector = EnsureCollector();
            return ((CommentCollector)collector.GetCollector()).CommentAddedOrEdited(parentOrOrgionalId, args.Response, args.IsEdit);
        }

        /// <summary>
        /// Called when the comment should be deleted
        /// </summary>
        public void CommentDeleteRequest(Comment comment)
        {
            DeferredCollector<Comment> collector = EnsureCollector();
            ((CommentCollector)collector.GetCollector()).CommentDeleteRequest(comment);
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
                args.Request.Data.SetText($" \r\n\r\n{shareBody}\r\n\r\n{commentLink}");
                m_shareComment = null;
                App.BaconMan.TelemetryMan.ReportEvent(this, "CommentShared");
            }
            else
            {
                args.Request.FailWithDisplayText("Baconit doesn't have anything to share!");
                App.BaconMan.TelemetryMan.ReportUnexpectedEvent(this, "FailedToShareCommentHelperCommentNoShareComment");
            }
        }

        #endregion

        #region Collapse Logic

        public void CollapseCommentsFromComment(Comment comment)
        {
            int foundLevel = -1;
            int killedComment = 0;

            // Lock the list
            lock (Comments)
            {
                // Go through all of the comments
                for (int i = 0; i < Comments.Count; i++)
                {
                    // If found level is set we have already seen the comment
                    if (foundLevel != -1)
                    {
                        // If this comment is higher than the root collapse kill it
                        if (Comments[i].CommentDepth > foundLevel)
                        {
                            // Remove it
                            Comments.RemoveAt(i);
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
                    else if (Comments[i].Id.Equals(comment.Id))
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
            lock (Comments)
            {
                lock (m_fullCommentList)
                {
                    // First, we need to find where in the UI we need to add comments
                    int inserationPoint = -1;
                    int expandRootLevel = 0;
                    for (int i = 0; i < Comments.Count; i++)
                    {
                        if (Comments[i].Id.Equals(comment.Id))
                        {
                            // We found it!
                            inserationPoint = i;
                            expandRootLevel = Comments[i].CommentDepth;
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
                        Comments.Insert(inserationPoint, m_fullCommentList[i]);
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
