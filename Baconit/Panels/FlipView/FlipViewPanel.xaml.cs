using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using Baconit.ContentPanels;
using Baconit.HelperControls;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using BaconBackend.Managers;

namespace Baconit.Panels.FlipView
{
    public sealed partial class FlipViewPanel : IPanel
    {
        //
        // Private Vars
        //

        /// <summary>
        /// The subreddit this flip view is representing.
        /// </summary>
        private Subreddit _subreddit;

        /// <summary>
        /// The current sort for this flip view instance
        /// </summary>
        private SortTypes _currentSort;

        /// <summary>
        /// The current sort time for this flip view instance
        /// </summary>
        private SortTimeTypes _currentSortTime;

        /// <summary>
        /// The collector backing this flip view
        /// </summary>
        private PostCollector _collector;

        /// <summary>
        /// A reference to the main panel host.
        /// </summary>
        private IPanelHost _host;

        /// <summary>
        /// The list of posts that back the flip view
        /// </summary>
        private readonly ObservableCollection<FlipViewPostItem> _postsLists = new ObservableCollection<FlipViewPostItem>();

        /// <summary>
        /// This list holds posts that we defer loaded if we have any.
        /// </summary>
        private readonly List<Post> _deferredPostList = new List<Post>();

        /// <summary>
        /// Indicates that there is a target post we are trying to get to.
        /// </summary>
        private string _targetPost = "";

        /// <summary>
        /// Indicates that there is a target comment we are trying to get to.
        /// </summary>
        private string _targetComment = "";

        /// <summary>
        /// Holds a reference to the loading overlay if there is one.
        /// </summary>
        private LoadingOverlay _loadingOverlay;

        /// <summary>
        /// Used to defer the first comments loading so we give the UI time to load before we start
        /// the intense work of loading comments.
        /// </summary>
        private bool _isFirstPostLoad = true;

        /// <summary>
        /// Holds a unique id for this flipview.
        /// </summary>
        private readonly string _uniqueId;

        public FlipViewPanel()
        {
            InitializeComponent();

            // Create a unique id for this
            _uniqueId = DateTime.Now.Ticks.ToString();
        }

        /// <summary>
        /// Fired when the panel is being created.
        /// </summary>
        /// <param name="host">A reference to the host.</param>
        /// <param name="arguments">Arguments for the panel</param>
        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            // Capture the host
            _host = host;

            // Check for the subreddit arg
            if (!arguments.ContainsKey(PanelManager.NavArgsSubredditName))
            {
                throw new Exception("No subreddit was given!");
            }
            var subredditName = (string)arguments[PanelManager.NavArgsSubredditName];

            // Kick off a background task to do the work
            Task.Run(async () =>
            {
                // Try to get the subreddit from the local cache.
                var subreddit = App.BaconMan.SubredditMan.GetSubredditByDisplayName(subredditName);

                // It is very rare that we can't get it from the cache because something
                // else usually request it from the web and then it will be cached.
                if (subreddit == null)
                {
                    // Since this can take some time, show the loading overlay
                    ShowFullScreenLoading();

                    // Try to get the subreddit from the web
                    subreddit = await App.BaconMan.SubredditMan.GetSubredditFromWebByDisplayName((string)arguments[PanelManager.NavArgsSubredditName]);
                }

                // Check again.
                if (subreddit == null)
                {
                    // Hmmmm. We can't load the subreddit. Show a message and go back
                    App.BaconMan.MessageMan.ShowMessageSimple("Hmmm, That's Not Right", "We can't load this subreddit right now, check your Internet connection.");

                    // We need to wait some time until the transition animation is done or we can't go back.
                    // If we call GoBack while we are still navigating it will be ignored.
                    await Task.Delay(1000);

                    // Now try to go back.
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        _host.GoBack();
                    });

                    // Get out of here.
                    return;
                }

                // Capture the subreddit
                _subreddit = subreddit;

                // Get the current sort
                _currentSort = arguments.ContainsKey(PanelManager.NavArgsSubredditSort) ? (SortTypes)arguments[PanelManager.NavArgsSubredditSort] : SortTypes.Hot;

                // Get the current sort time
                _currentSortTime = arguments.ContainsKey(PanelManager.NavArgsSubredditSortTime) ? (SortTimeTypes)arguments[PanelManager.NavArgsSubredditSortTime] : SortTimeTypes.Week;

                // Try to get the target post id
                if (arguments.ContainsKey(PanelManager.NavArgsPostId))
                {
                    _targetPost = (string)arguments[PanelManager.NavArgsPostId];
                }

                // Try to get the force post, this will make us show only one post for the subreddit,
                // which is the post given.
                string forcePostId = null;
                if (arguments.ContainsKey(PanelManager.NavArgsForcePostId))
                {
                    forcePostId = (string)arguments[PanelManager.NavArgsForcePostId];

                    // If the UI isn't already shown show the loading UI. Most of the time this post wont' be cached
                    // so it can take some time to load.
                    ShowFullScreenLoading();
                }

                // See if we are targeting a comment
                if (arguments.ContainsKey(PanelManager.NavArgsForceCommentId))
                {
                    _targetComment = (string)arguments[PanelManager.NavArgsForceCommentId];
                }

                // Get the collector and register for updates.
                _collector = PostCollector.GetCollector(_subreddit, App.BaconMan, _currentSort, _currentSortTime, forcePostId);
                _collector.OnCollectionUpdated += Collector_OnCollectionUpdated;

                // Kick off an update of the subreddits if needed.
                _collector.Update();

                // Set any posts that exist right now
                UpdatePosts(0, _collector.GetCurrentPosts());
            });
        }

        /// <summary>
        /// Fired when the panel is being navigated to.
        /// </summary>
        public void OnNavigatingTo()
        {
            // Set the task bar color
            _host.SetStatusBar(Color.FromArgb(255, 25, 25, 25));

            // If we have a current panel, set it visible.
            if(ui_flipView.SelectedItem != null)
            {
                ((FlipViewPostItem)ui_flipView.SelectedItem).IsVisible = true;
                ((FlipViewPostItem)ui_flipView.SelectedItem).LoadComments = true;
            }
        }

        /// <summary>
        /// Fired when the panel is being navigated from.
        /// </summary>
        public async void OnNavigatingFrom()
        {
            // Deffer the action so we don't mess up any animations.
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                // Tell all of the posts they are not visible.
                lock (_postsLists)
                {
                    foreach (var item in _postsLists)
                    {
                        item.IsVisible = false;
                    }
                }
            });
        }

        /// <summary>
        /// Fired when the panel should clear all memory.
        /// </summary>
        public async void OnCleanupPanel()
        {
            // If we have a collector unregister for updates.
            if(_collector != null)
            {
                _collector.OnCollectionUpdated -= Collector_OnCollectionUpdated;
            }

            // Kick to a background thread, remove all of our content.
            await Task.Run(() =>
            {
                // Remove all of our content
                ContentPanelMaster.Current.RemoveAllAllowedContent(_uniqueId);
            });

            // Deffer the action so we don't mess up any animations.
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                // Clear out all of the posts, and comments.
                lock (_postsLists)
                {
                    foreach (var item in _postsLists)
                    {
                        item.IsVisible = false;
                        item.LoadComments = false;
                    }
                    _postsLists.Clear();
                }
            });
        }

        /// <summary>
        /// Fired when the panel should try to reduce memory if possible. This will only be called
        /// while the panel isn't visible.
        /// </summary>
        public void OnReduceMemory()
        {
            // Kick to a background thread.
            Task.Run(() =>
            {
                // When we are asked to reduce memory unload all of our panels.
                // If the user comes back they will be reloaded as they view them.
                ContentPanelMaster.Current.UnloadContentForGroup(_uniqueId);
            });
        }

        /// <summary>
        /// Fired when the panel is already in the stack, but a new navigate has been made to it.
        /// Instead of creating a new panel, this same panel is used and given the navigation arguments.
        /// </summary>
        /// <param name="arguments">The argumetns passed when navigate was called</param>
        public async void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            // Do this logic here.
            OnNavigatingTo();

            if(!arguments.ContainsKey(PanelManager.NavArgsPostId))
            {
                return;
            }

            // Set the target post.
            _targetPost = (string)arguments[PanelManager.NavArgsPostId];

            // Kick off to the UI thread
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Lock the post list
                lock(_postsLists)
                {
                    // Make sure the string is still valid
                    if (string.IsNullOrWhiteSpace(_targetPost))
                    {
                        return;
                    }

                    // Set up the objects for the UI
                    for (var i = 0; i < _postsLists.Count; i++)
                    {
                        // Check if this post is it
                        if (_postsLists[i].Context.Post.Id == _targetPost)
                        {
                            // It is important we set the target post to empty string first!
                            _targetPost = string.Empty;

                            // Found it! Only set the index it we aren't already there.
                            if (ui_flipView.SelectedIndex != i)
                            {
                                ui_flipView.SelectedIndex = i;
                            }
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Fired when the collection list has been updated.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Collector_OnCollectionUpdated(object sender , CollectionUpdatedArgs<Post> args)
        {
            // Update the posts
            UpdatePosts(args.StartingPosition, args.ChangedItems);
        }

        /// <summary>
        /// Update the posts in flip view. Staring at the index given and going until the list is empty.
        /// </summary>
        /// <param name="startingPos"></param>
        /// <param name="newPosts"></param>
        private async void UpdatePosts(int startingPos, IReadOnlyList<Post> newPosts)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                var flipViewMenuVis = _host.CurrentScreenMode() == ScreenMode.Single ? Visibility.Visible : Visibility.Collapsed;

                // Grab the list lock
                lock (_postsLists)
                {
                    // If we are currently in a deferred scenario then we need to handle updates
                    // differently since the list won't match the list that is expected
                    if(_deferredPostList.Count != 0)
                    {
                        if (_postsLists.Count <= 0 || newPosts.Count <= 0 ||
                            !_postsLists[0].Context.Post.Id.Equals(newPosts[0].Id)) return;
                        // The current post is updated, so update it.
                        // We can't replace the time because flip view will freak out
                        // so just update whatever UI we need to update.
                        _postsLists[0].Context.Post.Likes = newPosts[0].Likes;
                        _postsLists[0].Context.Post.SubTextLine1 = newPosts[0].SubTextLine1;
                        _postsLists[0].Context.Post.SubTextLine2PartOne = newPosts[0].SubTextLine2PartOne;
                        _postsLists[0].Context.Post.SubTextLine2PartTwo = newPosts[0].SubTextLine2PartTwo;
                        _postsLists[0].Context.Post.Domain = newPosts[0].Domain;
                        _postsLists[0].Context.Post.Score = newPosts[0].Score;

                        // We have done all we want to do, leave now.
                        return;
                    }

                    // If the list is currently empty we want to only load the first element and defer the rest of the
                    // elements. If the target post is -1 we load the first element, if not we load it only.
                    var deferLoadPosts = _postsLists.Count == 0;
                    var deferTargetPost = _targetPost;
                    _targetPost = null;
                    if (deferLoadPosts)
                    {
                        // If we are doing a defer make sure we have a target
                        if (string.IsNullOrWhiteSpace(deferTargetPost) && newPosts.Count > 0)
                        {
                            deferTargetPost = newPosts[0].Id;
                        }
                    }

                    // Now setup the post update
                    var insertIndex = startingPos;

                    // Set up the objects for the UI
                    foreach (var post in newPosts)
                    {
                        if(post == null) continue;
                        
                        // Check if we are adding or inserting.
                        var isReplace = insertIndex < _postsLists.Count;

                        if (isReplace)
                        {
                            if (_postsLists[insertIndex].Context.Post.Id.Equals(post.Id))
                            {
                                // We can't replace the time because flip view will freak out
                                // so just update whatever UI we need to update.
                                _postsLists[insertIndex].Context.Post.Likes = post.Likes;
                                _postsLists[insertIndex].Context.Post.SubTextLine1 = post.SubTextLine1;
                                _postsLists[insertIndex].Context.Post.SubTextLine2PartOne = post.SubTextLine2PartOne;
                                _postsLists[insertIndex].Context.Post.SubTextLine2PartTwo = post.SubTextLine2PartTwo;
                                _postsLists[insertIndex].Context.Post.Domain = post.Domain;
                                _postsLists[insertIndex].Context.Post.Score = post.Score;
                            }
                            else
                            {
                                // Replace the entire post if it brand new
                                _postsLists[insertIndex].Context.Post = post;
                            }
                        }
                        else
                        {
                            // If we are deferring posts only add the target
                            if (deferLoadPosts)
                            {
                                if (post.Id.Equals(deferTargetPost))
                                {
                                    // Try catch is a work around for bug https://github.com/QuinnDamerell/Baconit/issues/53
                                    try
                                    {
                                        _postsLists.Add(new FlipViewPostItem(_host, _collector, post, _targetComment));
                                    }
                                    catch (Exception e)
                                    {
                                        TelemetryManager.ReportUnexpectedEvent(this, "UpdatePosts", e);
                                        App.BaconMan.MessageMan.DebugDia("Adding to postList failed! (deferLoadPosts)", e);
                                    }
                                }

                                // Add it to the deferred list, also add the deferred post so we know
                                // where it is in the list.
                                _deferredPostList.Add(post);
                            }
                            else
                            {
                                // Otherwise, just add it.
                                // Try catch is a work around for bug https://github.com/QuinnDamerell/Baconit/issues/53
                                try
                                {
                                    _postsLists.Add(new FlipViewPostItem(_host, _collector, post, _targetComment));
                                }
                                catch(Exception e)
                                {
                                    TelemetryManager.ReportUnexpectedEvent(this, "UpdatePosts", e);
                                    App.BaconMan.MessageMan.DebugDia("Adding to postList failed! (!deferLoadPosts)", e);
                                }
                            }
                        }

                        // Set the menu button
                        post.FlipViewMenuButton = flipViewMenuVis;

                        // Add one to the insert index
                        insertIndex++;
                    }

                    // If the item source hasn't been set yet do it now.
                    if (ui_flipView.ItemsSource == null)
                    {
                        ui_flipView.ItemsSource = _postsLists;
                    }
                }

                // Hide the loading overlay if it is visible
                HideFullScreenLoading();
            });
        }

        /// <summary>
        /// If we have deferred post this will add them
        /// </summary>
        public async void DoDeferredPostUpdate()
        {
            // Check if we have work to do.
            lock(_postsLists)
            {
                if(_deferredPostList.Count == 0)
                {
                    return;
                }
            }

            // Otherwise, sleep for a little to let the UI settle down.
            await Task.Delay(1000);

            // Now kick off a UI thread on low to do the update.
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
            {
                lock(_postsLists)
                {
                    // Ensure we sill have work do to.
                    if (_deferredPostList.Count == 0)
                    {
                        return;
                    }

                    var addBefore = true;
                    var deferredPostId = _postsLists.Count > 0 ? _postsLists[0].Context.Post.Id : string.Empty;
                    var insertList = new List<Post>();

                    foreach(var post in _deferredPostList)
                    {
                        // If this is the post don't do anything but indicate we should add after
                        if(post.Id.Equals(deferredPostId))
                        {
                            addBefore = false;
                        }
                        else
                        {
                            // If we are adding before add it before
                            if(addBefore)
                            {
                                // #todo BUG! This is a fun work around. If we insert posts here about 5% of the time the app will
                                // crash due to a bug in the platform. Since the exception is in the system we don't get a chance to handle it
                                // we just die.
                                // So, add these to another list and add them after.
                                insertList.Add(post);
                            }
                            else
                            {
                                // If not add it to the end.
                                _postsLists.Add(new FlipViewPostItem(_host, _collector, post, _targetComment));
                            }
                        }
                    }

                    // Now ad the inserts, but insert them from the element closest to the visible post to away.
                    // We want to do this so flipview doesn't keep changing which panels are virtualized as we add panels.
                    // as possible.
                    foreach (var post in insertList.Reverse<Post>())
                    {
                        _postsLists.Insert(0, new FlipViewPostItem(_host, _collector, post, _targetComment));
                    }

                    // Clear the deferrals
                    _deferredPostList.Clear();
                }

                // And preload the content for the next post, but again defer this since it is a background task.
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    UpdatePanelContent();
                });
            });
        }

        #region Flippping Logic

        /// <summary>
        /// Fired when the flip panel selection changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void FlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(ui_flipView.SelectedIndex == -1)
            {
                return;
            }

            // Mark the item read.
            _collector.MarkPostRead(((FlipViewPostItem)ui_flipView.SelectedItem)?.Context.Post, ui_flipView.SelectedIndex);

            // Hide the comment box if open
            HideCommentBoxIfOpen();

            // If the index is 0 we are most likely doing a first load, so we want to
            // set the panel content instantly so we get the loading UI as fast a possible.
            if (ui_flipView.SelectedIndex == 0)
            {
                // Update the posts
                UpdatePanelContent();
            }
            else
            {
                // Kick off the panel content update to the UI thread with idle pri to give the UI time to setup.
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, UpdatePanelContent);
            }

            // Now if we have deferred post add them
            DoDeferredPostUpdate();
        }

        /// <summary>
        /// Updates the content in all of the panels in flipview.
        /// </summary>
        private async void UpdatePanelContent()
        {
            // Create a list we need to set to the UI.
            var setToUiList = new List<Tuple<FlipViewPostItem, bool>>();
            var clearList = new List<FlipViewPostItem>();
            var extendCollection = false;

            // Get the min and max number of posts to load.
            var minContentLoad = ui_flipView.SelectedIndex;
            var maxContentLoad = ui_flipView.SelectedIndex;
            if (App.BaconMan.UiSettingsMan.FlipViewPreloadFutureContent)
            {
                maxContentLoad++;
            }

            // Lock the list
            lock (_postsLists)
            {
                for (var i = 0; i < _postsLists.Count; i++)
                {
                    var item = _postsLists[i];
                    if (i >= minContentLoad && i <= maxContentLoad)
                    {
                        // Add the post to the list of posts to set. We have to do this outside of the lock
                        // because we might delay while doing it.
                        setToUiList.Add(new Tuple<FlipViewPostItem, bool>(item, ui_flipView.SelectedIndex == i));
                    }
                    else
                    {
                        // Add the post to the list of posts to clear. We have to do this outside of the lock
                        // because we might delay while doing it.
                        clearList.Add(item);
                    }
                }

                // Check if we should load more posts. Note we want to check how many post the
                // collector has because this gets called when the m_postList is being built, thus
                // the count will be wrong.
                if(_postsLists.Count > 5 && _collector.GetCurrentPosts().Count < maxContentLoad + 4)
                {
                    extendCollection = true;
                }
            }

            // Extend if we should
            if(extendCollection)
            {
                _collector.ExtendCollection(25);
            }

            // Now that we are out of lock set the items we want to set.
            foreach(var tuple in setToUiList)
            {
                // We found an item to show or prelaod, do it.
                await SetPostContent(tuple.Item1, tuple.Item2);

                // If this is the first post to load delay for a while to give the UI time to setup.
                if (_isFirstPostLoad)
                {
                    _isFirstPostLoad = false;
                    await Task.Delay(1000);
                }

                // After the delay tell the post to prefetch the comments if we are visible.
                if (tuple.Item2)
                {
                    tuple.Item1.LoadComments = true;
                }
            }

            // Now set them all to not be visible and clear
            // the comments.
            foreach (var item in clearList)
            {
                item.IsVisible = false;
                item.LoadComments = false;
            }

            // Kick off a background thread to clear out what we don't want.
            await Task.Run(() =>
            {
                // Get all of our content.
                var allContent = ContentPanelMaster.Current.GetAllAllowedContentForGroup(_uniqueId);

                // Find the ids that should be cleared and clear them.
                foreach (var contentId in allContent)
                {
                    var found = false;
                    foreach (var tuple in setToUiList)
                    {
                        if (!tuple.Item1.Context.Post.Id.Equals(contentId)) continue;
                        found = true;
                        break;
                    }

                    if (!found)
                    {
                        // If we didn't find it clear it.
                        ContentPanelMaster.Current.RemoveAllowedContent(contentId);
                    }
                }
            });
        }

        #endregion

        #region Full Screen Loading

        /// <summary>
        /// Shows a loading overlay if there isn't one already
        /// </summary>
        private async void ShowFullScreenLoading()
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                // Make sure we don't have one already, if so get out of here.
                lock (this)
                {
                    if (_loadingOverlay != null)
                    {
                        return;
                    }
                    _loadingOverlay = new LoadingOverlay();
                }

                _loadingOverlay.OnHideComplete += LoadingOverlay_OnHideComplete;
                Grid.SetRowSpan(_loadingOverlay, 3);
                ui_contentRoot.Children.Add(_loadingOverlay);
                _loadingOverlay.Show();
            });
        }

        /// <summary>
        /// Hides the exiting loading overlay
        /// </summary>
        private void HideFullScreenLoading()
        {
            LoadingOverlay overlay;
            lock(this)
            {
                if (_loadingOverlay == null)
                {
                    return;
                }
                overlay = _loadingOverlay;
            }

            overlay.Hide();
        }

        /// <summary>
        /// Fired when the overlay is hidden.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadingOverlay_OnHideComplete(object sender, EventArgs e)
        {
            ui_contentRoot.Children.Remove(_loadingOverlay);
            lock(this)
            {
                _loadingOverlay = null;
            }
        }

        #endregion

        #region Comment Box

        /// <summary>
        /// Hides the box if it is open.
        /// </summary>
        private void HideCommentBoxIfOpen()
        {
            ui_commentBox?.HideBox();
        }

        /// <summary>
        /// Fired when a panel wasn't us to open the comment box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlipViewPostPanel_OnOpenCommentBox(object sender, OpenCommentBox e)
        {
            // Show the box with this data and the argument as the context.
            ShowCommentBox(e.RedditId, e.EditText, e);
        }

        /// <summary>
        /// Shows the comment box
        /// </summary>
        private void ShowCommentBox(string redditId, string editText, object context)
        {
            // Important! Call find name so the deferred loaded element is created!
            FindName("ui_commentBox");
            ui_commentBox.Visibility = Visibility.Visible;
            ui_commentBox.ShowBox(redditId, editText, context);
            SetCommentBoxHeight(ActualHeight);
        }

        /// <summary>
        /// Fired when the comment box is done opening.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CommentBox_OnBoxOpened(object sender, CommentBoxOnOpenedArgs e)
        {
            var openBoxContext = (OpenCommentBox)e.Context;
            if(openBoxContext != null)
            {
                // Replace the context
                e.Context = openBoxContext.Context;
                openBoxContext.CommentBoxOpened(sender, e);
            }
        }

        /// <summary>
        /// Fired when content has been submitted in the comment box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CommentBox_OnCommentSubmitted(object sender, CommentSubmittedArgs e)
        {
            var openBoxContext = (OpenCommentBox)e.Context;
            if (openBoxContext != null)
            {
                // Replace the context
                e.Context = openBoxContext.Context;
                var wasActionSuccessful = openBoxContext.CommentBoxSubmitted(sender, e);

                // Hide the box if good
                if (wasActionSuccessful)
                {
                    ui_commentBox.HideBox(true);
                }
                else
                {
                    ui_commentBox.HideLoadingOverlay();
                }
            }
        }

        /// <summary>
        /// Sets the comment box's max height
        /// </summary>
        /// <param name="height"></param>
        private void SetCommentBoxHeight(double height)
        {
            // We have to set the max height because the grid row is set to auto.
            // With auto the box will keep expanding as large as possible because
            // it isn't bound by the grid.
            if (ui_commentBox != null)
            {
                // -1 is needed to work around a layout cycle bug
                // https://github.com/QuinnDamerell/Baconit/issues/54
                ui_commentBox.MaxHeight = height - 1;
            }
        }

        /// <summary>
        /// Fired when the control changes size, we need up update the height of our comment box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetCommentBoxHeight(e.NewSize.Height);
        }

        #endregion

        /// <summary>
        /// Sets the post content. For now all we give the flip view control is the URL
        /// and it must figure out the rest on it's own.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="isVisiblePost"></param>
        private async Task SetPostContent(FlipViewPostItem item, bool isVisiblePost)
        {
            // Set that the post is visible if it is
            item.IsVisible = isVisiblePost;

            // Only load the content if we are doing it with out action. (most of the time)
            if (App.BaconMan.UiSettingsMan.FlipViewLoadPostContentWithoutAction)
            {
                await Task.Run(() =>
                {
                    ContentPanelMaster.Current.AddAllowedContent(ContentPanelSource.CreateFromPost(item.Context.Post), _uniqueId, !isVisiblePost);
                });
            }
        }

        /// <summary>
        /// Fired when the user has tapped the panel requesting the content to load.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void FlipViewPostPanel_OnContentLoadRequest(object sender, ContentLoadRequestArgs e)
        {
            // Find the post
            Post post = null;
            lock (_postsLists)
            {
                foreach (var item in _postsLists)
                {
                    if (!item.Context.Post.Id.Equals(e.SourceId)) continue;
                    post = item.Context.Post;
                    break;
                }
            }

            // Send off a command to load it.
            if (post != null)
            {
                await Task.Run(() =>
                {
                    ContentPanelMaster.Current.AddAllowedContent(ContentPanelSource.CreateFromPost(post), _uniqueId);
                });
            }
        }
    }
}
