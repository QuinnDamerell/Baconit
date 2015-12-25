using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using BaconBackend.Managers;
using Baconit.FlipViewControls;
using Baconit.HelperControls;
using Baconit.Interfaces;
using Microsoft.ApplicationInsights.DataContracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.Panels
{
    public sealed partial class FlipViewPanel : UserControl, IPanel
    {
        const int c_hiddenCommentHeaderHeight = 36;
        const int c_hiddenShowAllCommentsHeight = 36;

        //
        // Private Vars
        //

        /// <summary>
        /// The subreddit this flip view is representing.
        /// </summary>
        Subreddit m_subreddit;

        /// <summary>
        /// The current sort for this flip view instance
        /// </summary>
        SortTypes m_currentSort;

        /// <summary>
        /// The current sort time for this flip view instance
        /// </summary>
        SortTimeTypes m_currentSortTime;

        /// <summary>
        /// The collector backing this flip view
        /// </summary>
        PostCollector m_collector;

        /// <summary>
        /// A reference to the main panel host.
        /// </summary>
        IPanelHost m_host;

        /// <summary>
        /// The list of posts that back the flip view
        /// </summary>
        ObservableCollection<Post> m_postsLists = new ObservableCollection<Post>();

        /// <summary>
        /// Holds the protip pop up if one exists.
        /// </summary>
        TipPopUp m_commentTipPopUp = null;

        /// <summary>
        /// Indicates that there is a target post we are trying to get to.
        /// </summary>
        string m_targetPost = "";

        /// <summary>
        /// Indicates that there is a target comment we are trying to get to.
        /// </summary>
        string m_targetComment = "";

        /// <summary>
        /// Holds the current flip view comment manager
        /// </summary>
        List<FlipViewPostCommentManager> m_commentManagers = new List<FlipViewPostCommentManager>();

        /// <summary>
        /// Holds the current story headers, we need to to figure out how large they are.
        /// </summary>
        List<Grid> m_flipViewStoryHeaders = new List<Grid>();

        /// <summary>
        /// Used by the comment box to differ the header update until the scrolling gets close to the top.
        /// Without this every time the comment box opens or closes the UI jumps because the header gets
        /// moved.
        /// </summary>
        bool m_hasDeferredHeaderSizeUpdate = false;

        /// <summary>
        /// Keeps track of the last known scroll position for the UI on the screen.
        /// </summary>
        int m_lastKnownScrollOffset = 0;

        /// <summary>
        /// Holds a reference to the loading overlay if there is one.
        /// </summary>
        LoadingOverlay m_loadingOverlay = null;

        /// <summary>
        /// Used to defer the first comments loading so we give the UI time to load before we start
        /// the intense work of loading comments.
        /// </summary>
        bool m_isFirstPostLoad = true;

        /// <summary>
        /// Indicates if we are full screen from the flipveiw control or not.
        /// </summary>
        bool m_isFullScreen = false;


        public FlipViewPanel()
        {
            this.InitializeComponent();

            // Set the comment box invisible for now. It should hide itself
            // when created but it doesn't seem to work. So for now just hide it
            // and when the first person tries to open it we will show it.
            ui_commmentBox.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Fired when the panel is being created.
        /// </summary>
        /// <param name="host">A reference to the host.</param>
        /// <param name="arguments">Arguments for the panel</param>
        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            // Capture the host
            m_host = host;

            // Check for the subreddit arg
            if (!arguments.ContainsKey(PanelManager.NAV_ARGS_SUBREDDIT_NAME))
            {
                throw new Exception("No subreddit was given!");
            }
            string subredditName = (string)arguments[PanelManager.NAV_ARGS_SUBREDDIT_NAME];

            // Kick off a background task to do the work
            Task.Run(async () =>
            {
                // Try to get the subreddit from the local cache.
                Subreddit subreddit = App.BaconMan.SubredditMan.GetSubredditByDisplayName(subredditName);

                // It is very rare that we can't get it from the cache because something
                // else usually request it from the web and then it will be cached.
                if (subreddit == null)
                {
                    // Since this can take some time, show the loading overlay
                    ShowFullScreenLoading();

                    // Try to get the subreddit from the web
                    subreddit = await App.BaconMan.SubredditMan.GetSubredditFromWebByDisplayName((string)arguments[PanelManager.NAV_ARGS_SUBREDDIT_NAME]);
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
                        m_host.GoBack();
                    });

                    // Get out of here.
                    return;
                }

                // Capture the subreddit
                m_subreddit = subreddit;

                // Get the current sort
                m_currentSort = arguments.ContainsKey(PanelManager.NAV_ARGS_SUBREDDIT_SORT) ? (SortTypes)arguments[PanelManager.NAV_ARGS_SUBREDDIT_SORT] : SortTypes.Hot;

                // Get the current sort time
                m_currentSortTime = arguments.ContainsKey(PanelManager.NAV_ARGS_SUBREDDIT_SORT_TIME) ? (SortTimeTypes)arguments[PanelManager.NAV_ARGS_SUBREDDIT_SORT_TIME] : SortTimeTypes.Week;

                // Try to get the target post id
                if (arguments.ContainsKey(PanelManager.NAV_ARGS_POST_ID))
                {
                    m_targetPost = (string)arguments[PanelManager.NAV_ARGS_POST_ID];
                }

                // Try to get the force post, this will make us show only one post for the subreddit,
                // which is the post given.
                string forcePostId = null;
                if (arguments.ContainsKey(PanelManager.NAV_ARGS_FORCE_POST_ID))
                {
                    forcePostId = (string)arguments[PanelManager.NAV_ARGS_FORCE_POST_ID];

                    // If the UI isn't already shown show the loading UI. Most of the time this post wont' be cached
                    // so it can take some time to load.
                    ShowFullScreenLoading();
                }

                // See if we are targeting a comment
                if (arguments.ContainsKey(PanelManager.NAV_ARGS_FORCE_COMMENT_ID))
                {
                    m_targetComment = (string)arguments[PanelManager.NAV_ARGS_FORCE_COMMENT_ID];
                }

                // Get the collector and register for updates.
                m_collector = PostCollector.GetCollector(m_subreddit, App.BaconMan, m_currentSort, m_currentSortTime, forcePostId);
                m_collector.OnCollectionUpdated += Collector_OnCollectionUpdated;

                // Kick off an update of the subreddits if needed.
                m_collector.Update();

                // Set any posts that exist right now
                UpdatePosts(0, m_collector.GetCurrentPosts());

                // Set the command bar
                m_host.OnScreenModeChanged += OnScreenModeChanged;
            });
        }

        public void OnNavigatingTo()
        {
            // Set the task bar color
            m_host.SetStatusBar(Color.FromArgb(255, 25, 25, 25));
        }

        public void OnNavigatingFrom()
        {
            // #todo reduce memory foot print.
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

            if(!arguments.ContainsKey(PanelManager.NAV_ARGS_POST_ID))
            {
                return;
            }

            // Set the target post.
            m_targetPost = (string)arguments[PanelManager.NAV_ARGS_POST_ID];

            // Kick off to the UI thread
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Lock the post list
                lock(m_postsLists)
                {
                    // Make sure the string is still valid
                    if (String.IsNullOrWhiteSpace(m_targetPost))
                    {
                        return;
                    }

                    // Set up the objects for the UI
                    for (int i = 0; i < m_postsLists.Count; i++)
                    {
                        // Check if this post is it
                        if (m_postsLists[i].Id == m_targetPost)
                        {
                            // It is important we set the target post to empty string first!
                            m_targetPost = string.Empty;

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
        /// Fired when the screen mode changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnScreenModeChanged(object sender, OnScreenModeChangedArgs e)
        {
            lock (m_postsLists)
            {
                Visibility flipViewMenuVis = m_host.CurrentScreenMode() == ScreenMode.Single ? Visibility.Visible : Visibility.Collapsed;
                foreach (Post post in m_postsLists)
                {
                    post.FlipViewMenuButton = flipViewMenuVis;
                }
            }
        }

        /// <summary>
        /// Fired when the collection list has been updated.
        /// </summary>
        /// <param name="startingPos"></param>
        /// <param name="changedPosts"></param>
        private void Collector_OnCollectionUpdated(object sender , OnCollectionUpdatedArgs<Post> args)
        {
            // Update the posts
            UpdatePosts(args.StartingPosition, args.ChangedItems);
        }

        /// <summary>
        /// Update the posts in flip view. Staring at the index given and going until the list is empty.
        /// </summary>
        /// <param name="startingPos"></param>
        /// <param name="newPosts"></param>
        private async void UpdatePosts(int startingPos, List<Post> newPosts)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Visibility flipViewMenuVis = m_host.CurrentScreenMode() == ScreenMode.Single ? Visibility.Visible : Visibility.Collapsed;

                // Grab the list lock
                lock (m_postsLists)
                {
                    // Setup the insert
                    int insertIndex = startingPos;

                    int foundTargetPost = -1;

                    // Set up the objects for the UI
                    foreach (Post post in newPosts)
                    {
                        // Check if we are adding or inserting.
                        bool isReplace = insertIndex < m_postsLists.Count;

                        if (isReplace)
                        {
                            if (m_postsLists[insertIndex].Id.Equals(post.Id))
                            {
                                // We can't replace the time because flip view will freak out
                                // so just update whatever UI we need to update.
                                m_postsLists[insertIndex].Likes = post.Likes;
                                m_postsLists[insertIndex].SubTextLine1 = post.SubTextLine1;
                                m_postsLists[insertIndex].SubTextLine2PartOne = post.SubTextLine2PartOne;
                                m_postsLists[insertIndex].SubTextLine2PartTwo = post.SubTextLine2PartTwo;
                                m_postsLists[insertIndex].Domain = post.Domain;
                                m_postsLists[insertIndex].Score = post.Score;
                            }
                            else
                            {
                                // Replace the entire post if it brand new
                                m_postsLists[insertIndex] = post;
                            }
                        }
                        else
                        {
                            // Add it to the end
                            m_postsLists.Add(post);
                        }

                        // Set the menu button
                        post.FlipViewMenuButton = flipViewMenuVis;

                        // Check if we are looking for a post
                        if (foundTargetPost == -1 && !String.IsNullOrWhiteSpace(m_targetPost))
                        {
                            // Check if this post is it
                            if (post.Id.Equals(m_targetPost))
                            {
                                // Found it! Cache the index for now, we will set it when the list is done loading.
                                foundTargetPost = insertIndex;
                            }
                        }
                        insertIndex++;
                    }

                    // If the list isn't set set it now. We want to delay this set so as we add elements into 
                    // the flipview they don't get virtualized in until we can also set the selected index.
                    if (ui_flipView.ItemsSource == null)
                    {
                        ui_flipView.ItemsSource = m_postsLists;
                    }                    

                    // Now that we set the list set the target index
                    if (foundTargetPost != -1)
                    {
                        // Note this is very important to be unset here. All selected item changed events on flip view 
                        // will be ignored until this is empty, so if we set this empty before we set the list (above) the 0
                        // indexes will fire and set.
                        lock(m_postsLists)
                        {
                            m_targetPost = String.Empty;
                        }

                        ui_flipView.SelectedIndex = foundTargetPost;

                        if (foundTargetPost == 0)
                        {
                            // If this is the first post the content won't be set unless we call selection changed again.
                            // This is because the flip view index defaults to 0, but we ignored the request because targetpost was not empty.
                            // Normally the selected index would change and fire the listener and all would be good, but since we didn't change
                            // the index it won't. So we will just do it manually.
                            FlipView_SelectionChanged(null, null);
                        }

                        // This is a good place to show the comment tip if we have to
                        ShowCommentScrollTipIfNeeded();
                    }

               
                }

                SetHeaderSizes();

                // Hide the loading overlay if it is visible
                HideFullScreenLoading();
            });
        }

        #region Post Click Actions

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            m_host.ToggleMenu(true);
        }

        /// <summary>
        /// Fired when a up vote arrow is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UpVote_Tapped(object sender, EventArgs e)
        {
            Post post = (sender as FrameworkElement).DataContext as Post;
            m_collector.ChangePostVote(post, PostVoteAction.UpVote);
        }

        /// <summary>
        /// Fired when a down vote arrow is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DownVote_Tapped(object sender, EventArgs e)
        {
            Post post = (sender as FrameworkElement).DataContext as Post;
            m_collector.ChangePostVote(post, PostVoteAction.DownVote);
        }

        private async void OpenBrowser_Tapped(object sender, EventArgs e)
        {
            Post post = (sender as FrameworkElement).DataContext as Post;
            string url = post.Url;
            if (String.IsNullOrWhiteSpace(url))
            {
                url = post.Permalink;
            }

            if (!String.IsNullOrWhiteSpace(url))
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(url, UriKind.Absolute));
                App.BaconMan.TelemetryMan.ReportEvent(this, "OpenInBrowser");
            }
        }

        private void More_Tapped(object sender, EventArgs e)
        {
            // Show the more menu
            FrameworkElement element = sender as FrameworkElement;
            if (element != null)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }

            App.BaconMan.TelemetryMan.ReportEvent(this, "MoreTapped");
        }

        private void SavePost_Click(object sender, RoutedEventArgs e)
        {
            Post post = (sender as FrameworkElement).DataContext as Post;
            m_collector.SaveOrHidePost(post, !post.IsSaved, null);
            App.BaconMan.TelemetryMan.ReportEvent(this, "SavePostTapped");
        }

        private void HidePost_Click(object sender, RoutedEventArgs e)
        {
            Post post = (sender as FrameworkElement).DataContext as Post;
            m_collector.SaveOrHidePost(post, null, !post.IsHidden);
            App.BaconMan.TelemetryMan.ReportEvent(this, "HidePostTapped");
        }

        private void CopyLink_Click(object sender, RoutedEventArgs e)
        {
            // Get the post and copy the url into the clipboard
            Post post = (sender as FrameworkElement).DataContext as Post;
            DataPackage data = new DataPackage();
            if(String.IsNullOrWhiteSpace(post.Url))
            {
                data.SetText("http://www.reddit.com" + post.Permalink);
            }
            else
            {
                data.SetText(post.Url);
            }
            Clipboard.SetContent(data);
            App.BaconMan.TelemetryMan.ReportEvent(this, "CopyLinkTapped");
        }

        private void CopyPermalink_Click(object sender, RoutedEventArgs e)
        {
            // Get the post and copy the url into the clipboard
            Post post = (sender as FrameworkElement).DataContext as Post;
            DataPackage data = new DataPackage();
            data.SetText("http://www.reddit.com" + post.Permalink);
            Clipboard.SetContent(data);
            App.BaconMan.TelemetryMan.ReportEvent(this, "CopyLinkTapped");
        }

        private void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            Post post = (sender as FrameworkElement).DataContext as Post;
            App.BaconMan.ImageMan.SaveImageLocally(post.Url);
            App.BaconMan.TelemetryMan.ReportEvent(this, "CopyLinkTapped");
        }

        // I threw up a little while I wrote this.
        Post m_sharePost = null;
        private void SharePost_Click(object sender, RoutedEventArgs e)
        {
            Post post = (sender as FrameworkElement).DataContext as Post;
            // #todo handle things other than URLs
            if (!String.IsNullOrWhiteSpace(post.Url))
            {
                m_sharePost = post;
                // Setup the share contract so we can share data
                DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
                dataTransferManager.DataRequested += DataTransferManager_DataRequested;
                DataTransferManager.ShowShareUI();
                App.BaconMan.TelemetryMan.ReportEvent(this, "SharePostTapped");

            }
        }

        private void DataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            if(m_sharePost != null)
            {
                args.Request.Data.Properties.ApplicationName = "Baconit";
                args.Request.Data.Properties.ContentSourceWebLink = new Uri(m_sharePost.Url, UriKind.Absolute);
                args.Request.Data.Properties.Title = "A Reddit Post Shared From Baconit";
                args.Request.Data.Properties.Description = m_sharePost.Title;
                args.Request.Data.SetText($"Check this out! \r\n\r\n{m_sharePost.Title}\r\n\r\n{m_sharePost.Url}");
                m_sharePost = null;
                App.BaconMan.TelemetryMan.ReportEvent(this, "PostShared");
            }
            else
            {
                args.Request.FailWithDisplayText("Baconit doesn't have anything to share!");
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToShareFilpViewPostNoSharePost");
            }
        }

        /// <summary>
        /// Called when we should open the global menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MenuPost_Tapped(object sender, EventArgs e)
        {
            // Show the global menu
            m_host.ToggleMenu(true);
        }

        /// <summary>
        /// Called when the user wants to comment on the post
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PostCommentOn_OnIconTapped(object sender, EventArgs e)
        {
            Post post = (Post)((FrameworkElement)sender).DataContext;
            ui_commmentBox.Visibility = Visibility.Visible;
            ui_commmentBox.ShowBox(post, "t3_"+post.Id);
        }

        /// <summary>
        /// Called when we should show more for the post
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>

        private void MorePost_Tapped(object sender, EventArgs e)
        {
            Post post = (sender as FrameworkElement).DataContext as Post;
        }

        /// <summary>
        /// Fired when the user taps the go to subreddit button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GoToSubreddit_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to the subreddit.
            Post post = (sender as FrameworkElement).DataContext as Post;
            Dictionary<string, object> args = new Dictionary<string, object>();
            args.Add(PanelManager.NAV_ARGS_SUBREDDIT_NAME, post.Subreddit);
            m_host.Navigate(typeof(SubredditPanel), post.Subreddit + SortTypes.Hot + SortTimeTypes.Week, args);
            App.BaconMan.TelemetryMan.ReportEvent(this, "GoToSubredditFlipView");
        }

        /// <summary>
        /// Fired when the user taps the go to user button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GoToUser_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to the user.
            Post post = (sender as FrameworkElement).DataContext as Post;
            Dictionary<string, object> args = new Dictionary<string, object>();
            args.Add(PanelManager.NAV_ARGS_USER_NAME, post.Author);
            m_host.Navigate(typeof(UserProfile), post.Author, args);
            App.BaconMan.TelemetryMan.ReportEvent(this, "GoToUserFlipView");
        }

        #endregion

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

            lock(m_postsLists)
            {
                // Check if we are jumping to a target, if so don't do anything.
                if(!String.IsNullOrWhiteSpace(m_targetPost))
                {
                    return;
                }
            }

            // Mark the item read.
            m_collector.MarkPostRead((Post)ui_flipView.SelectedItem, ui_flipView.SelectedIndex);

            // Hide the comment box if shown
            ui_commmentBox.HideBox();

            // Reset the scroll pos
            m_lastKnownScrollOffset = 0;

            // Kick off the panel content update to the UI thread with idle pri to give the UI time to setup.
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                // Update the posts
                UpdatePanelContent();
            });      
        }

        /// <summary>
        /// Updates the content in all of the panels in flipview.
        /// </summary>
        private async void UpdatePanelContent()
        {
            // Create a list we need to set to the UI.
            List<Tuple<Post, bool>> setToUiList = new List<Tuple<Post, bool>>();

            // Lock the list
            lock(m_postsLists)
            {
                // Get the min and max number of posts to load.
                int minContentLoad = ui_flipView.SelectedIndex;
                int maxContentLoad = ui_flipView.SelectedIndex;
                if(App.BaconMan.UiSettingsMan.FlipView_PreloadFutureContent)
                {
                    maxContentLoad++;
                }

                for (int i = 0; i < m_postsLists.Count; i++)
                {
                    Post post = m_postsLists[i];
                    if (i >= minContentLoad && i <= maxContentLoad)
                    {
                        // Add the post to the list of posts to set. We have to do this outside of the lock
                        // because we might delay while doing it.
                        setToUiList.Add(new Tuple<Post, bool>(post, ui_flipView.SelectedIndex == i));            
                    }
                    else
                    {
                        // If we don't want these clear out the values.
                        ClearPostContent(ref post);
                        ClearPostComments(ref post);
                    }
                }

                // Check if we should load more posts. Note we want to check how many post the
                // collector has because this gets called when the m_postList is being built, thus
                // the count will be wrong.
                if(m_postsLists.Count > 5 && m_collector.GetCurrentPosts().Count < maxContentLoad + 4)
                {
                    m_collector.ExtendCollection(25);
                }
            }

            // Now that we are out of lock set the items we want to set.
            foreach(Tuple<Post, bool> tuple in setToUiList)
            {
                // We found an item to show or prelaod, do it.
                Post postToSet = tuple.Item1;
                SetPostContent(ref postToSet, tuple.Item2);

                // If this is the first post to load delay for a while to give the UI time to setup.
                // This will delay setting the next post as well as prefetching the comments for this
                // current post.
                if(m_isFirstPostLoad)
                {
                    m_isFirstPostLoad = false;
                    await Task.Delay(500);
                }

                // If this is the current item also preload the comments now.
                if (tuple.Item2)
                {
                    PreFetchPostComments(ref postToSet);
                }
            }

            // Update the header sizes
            SetHeaderSizes();
        }

        #endregion

        #region CommentScrollingLogic

        /// <summary>
        /// Creates a comment manager for the post and prefetches the comments
        /// </summary>
        /// <param name="post"></param>
        private void PreFetchPostComments(ref Post post, bool forcePreFetch = false, bool showThreadSubset = true)
        {
            // Ensure we don't already have the comments for this post
            lock (m_commentManagers)
            {
                for (int i = 0; i < m_commentManagers.Count; i++)
                {
                    if (m_commentManagers[i].Post.Id.Equals(post.Id))
                    {
                        return;
                    }
                }
            }

            // Create a comment manager for the post
            FlipViewPostCommentManager manager = new FlipViewPostCommentManager(ref post, m_targetComment, showThreadSubset);

            // If the user wanted, kick off pre load of comments.
            if (forcePreFetch || App.BaconMan.UiSettingsMan.FlipView_PreloadComments)
            {
                manager.PreFetchComments();
            }

            // Add the manager to the current list
            lock(m_commentManagers)
            {
                m_commentManagers.Add(manager);
            }
        }

        /// <summary>
        /// Will remove any comment managers that may exist for a post, and clears the comments.
        /// </summary>
        /// <param name="post"></param>
        private void ClearPostComments(ref Post post)
        {
            // Look for the comment manager, clean it up and remove it.
            lock(m_commentManagers)
            {
                for(int i = 0; i  < m_commentManagers.Count; i++)
                {
                    if(m_commentManagers[i].Post.Id.Equals(post.Id))
                    {
                        m_commentManagers[i].PrepareForDeletion();
                        m_commentManagers.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Fired when a user tap the header to view an entire thread instead of just the
        /// context of a message
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ViewEntireThread_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Get the post and the manager
            Post post = ((Post)((FrameworkElement)sender).DataContext);

            // First remove the current comment manager
            ClearPostComments(ref post);

            // Now make a new one without using the subset
            PreFetchPostComments(ref post, true, false);

            // Update the header sizes to fix the UI
            SetHeaderSizes();
        }

        /// <summary>
        /// Fired when one of the lists are scrolling to the bottom.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void List_OnListEndDetectedEvent(object sender, OnListEndDetected e)
        {
            // NOTE!!!
            // This is a very hot code path, so anything done here should be really quick!

            // Get the post
            Post post = ((Post)((FrameworkElement)sender).DataContext);

            // Show or hide the scroll bar depending if we have gotten to comments yet or not.
            post.VerticalScrollBarVisibility = e.ListScrollTotalDistance > 60 ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden;

            // Find the header size for this post
            int currentScrollAera = GetCurrentScrollArea(post);

            // Get the height of the current post header
            double currentPostHeaderSize = 0;
            lock (m_flipViewStoryHeaders)
            {
                foreach (Grid flipHeader in m_flipViewStoryHeaders)
                {
                    Post headerPost = (Post)(flipHeader.DataContext);
                    if (headerPost != null && headerPost.Id.Equals(post.Id))
                    {
                        currentPostHeaderSize = flipHeader.ActualHeight;
                        break;
                    }
                }
            }

            // Use the header size and the control size to figure out if we should show the static header.
            bool showHeader = e.ListScrollTotalDistance > currentScrollAera - currentPostHeaderSize;
            post.FlipViewStickyHeaderVis = showHeader ? Visibility.Visible : Visibility.Collapsed;

            // If we have a differed header update and we are near the top (not showing the header)
            // do the update now. For a full story see the comment on m_hasDeferredHeaderSizeUpdate
            if (m_hasDeferredHeaderSizeUpdate && !showHeader)
            {
                SetHeaderSizes();
            }

            // Update the last known scroll pos
            m_lastKnownScrollOffset = (int)e.ListScrollTotalDistance;

            // Hide the tip box if needed.
            HideCommentScrollTipIfNeeded();

            // Do the rest of this work on a background thread
            Task.Run(() =>
            {
                // Get the comment manager for this post
                lock (m_commentManagers)
                {
                    foreach (FlipViewPostCommentManager manager in m_commentManagers)
                    {
                        if (manager.Post.Id.Equals(post.Id))
                        {
                            // When found, request more posts
                            manager.RequestMorePosts();
                            break;
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Fired when a story header loads. We need to keep track of them
        /// so we can get their sizes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlipViewStoryHeader_Loaded(object sender, RoutedEventArgs e)
        {
            // Add this to our list
            lock(m_flipViewStoryHeaders)
            {
                m_flipViewStoryHeaders.Add(sender as Grid);
            }
        }

        /// <summary>
        /// Updates the header sizes for the flip post so they fit perfectly on the screen.
        /// </summary>
        private void SetHeaderSizes()
        {
            // Lock the post list
            lock (m_postsLists)
            {
                // Get the screen size, account for the comment box if it is open.
                int currentScrollAera = GetCurrentScrollArea();

                // Set up the objects for the UI
                foreach (Post post in m_postsLists)
                {
                    if(post.HeaderSize != currentScrollAera)
                    {
                        post.HeaderSize = currentScrollAera;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the current space that's available for the flipview scroller
        /// </summary>
        /// <returns></returns>
        private int GetCurrentScrollArea(Post post = null)
        {
            // If the post is null get the current post
            if(post == null && ui_flipView.SelectedIndex != -1)
            {
                post = m_postsLists[ui_flipView.SelectedIndex];
            }

            // Get the control size
            int screenSize = (int)ui_contentRoot.ActualHeight;

            // If the comment box is open remove the height of it.
            if (ui_commmentBox.IsOpen)
            {
                screenSize -= (int)ui_commmentBox.ActualHeight;
            }

            // If post is null back out here.
            if(post == null)
            {
                return screenSize;
            }

            // If we are showing the show all comments header add the height of that so it won't be on the screen by default
            if(post.FlipViewShowEntireThreadMessage == Visibility.Visible)
            {
                screenSize += c_hiddenShowAllCommentsHeight;
            }

            // If we are not hiding the large header also add the height of the comment bar so it will be off screen by default
            // or if we are full screen from the flip control.
            if(post.FlipviewHeaderVisibility == Visibility.Visible || m_isFullScreen)
            {
                screenSize += c_hiddenCommentHeaderHeight;
            }

            return screenSize;
        }

        /// <summary>
        /// Fired when the comment box changes states.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CommentBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // We do this so we jump the UI ever time the comment box is open.
            // see the description on m_hasDeferredHeaderSizeUpdate for a full story.
            if (e.NewSize.Height > 0 && m_lastKnownScrollOffset > 30)
            {
                m_hasDeferredHeaderSizeUpdate = true;
            }
            else
            {
                // Fire a header size change to fix them up.
                SetHeaderSizes();
            }
        }

        private void EndDetectingListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            EndDetectingListView listview = sender as EndDetectingListView;
            listview.SelectedIndex = -1;
        }

        /// <summary>
        /// When a new list loads add our listeners
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EndDetectingListView_Loaded(object sender, RoutedEventArgs e)
        {
            EndDetectingListView list = sender as EndDetectingListView;
            list.OnListEndDetectedEvent += List_OnListEndDetectedEvent;

            // Set the threshold to 0 so we always get notifications
            list.EndOfListDetectionThrehold = 0.0;
        }

        #endregion

        #region Comment Click Listeners

        /// <summary>
        /// Fired when the refresh button is pressed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CommentRefresh_Click(object sender, RoutedEventArgs e)
        {
            Post post = (sender as FrameworkElement).DataContext as Post;
            FlipViewPostCommentManager manager = FindCommentManager(post.Id);
            if (manager != null)
            {
                manager.Refresh();
            }
        }

        private void CommentUp_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // We don't need to animate here, the vote will do it for us
            Comment comment = (sender as FrameworkElement).DataContext as Comment;
            FlipViewPostCommentManager manager = FindCommentManager(comment.LinkId);
            if (manager != null)
            {
                manager.UpVote_Tapped(comment);
            }
        }

        private void CommentDown_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // We don't need to animate here, the vote will do it for us
            Comment comment = (sender as FrameworkElement).DataContext as Comment;
            FlipViewPostCommentManager manager = FindCommentManager(comment.LinkId);
            if (manager != null)
            {
                manager.DownVote_Tapped(comment);
            }
        }

        private void CommentReply_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Animate the text
            AnimateText((FrameworkElement)sender);

            // Get the comment
            Comment comment = (sender as FrameworkElement).DataContext as Comment;

            // Get the parent post
            Post post = null;
            string lookingForPostId = comment.LinkId.Substring(3);
            lock(m_postsLists)
            {
                foreach(Post searchPost in m_postsLists)
                {
                    if(searchPost.Id.Equals(lookingForPostId))
                    {
                        post = searchPost;
                    }
                }
            }

            if(post == null)
            {
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "CommentReplyTriedToFindParentPostButFailed");
                return;
            }

            // Show the comment box
            ui_commmentBox.Visibility = Visibility.Visible;
            ui_commmentBox.ShowBox(post, "t1_" +comment.Id);
        }

        private void CommentUser_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Animate the text
            AnimateText((FrameworkElement)sender);

            // Get the comment
            Comment comment = (sender as FrameworkElement).DataContext as Comment;

            // Navigate to the user
            Dictionary<string, object> args = new Dictionary<string, object>();
            args.Add(PanelManager.NAV_ARGS_USER_NAME, comment.Author);
            m_host.Navigate(typeof(UserProfile), comment.Author, args);
            App.BaconMan.TelemetryMan.ReportEvent(this, "GoToUserFromComment");
        }

        private void CommentMore_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Animate the text
            AnimateText((FrameworkElement)sender);

            // Show the more menu
            FrameworkElement element = sender as FrameworkElement;
            if (element != null)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }

            App.BaconMan.TelemetryMan.ReportEvent(this, "CommentMoreTapped");
        }

        private void CommentSave_Click(object sender, RoutedEventArgs e)
        {
            Comment comment = (sender as FrameworkElement).DataContext as Comment;
            FlipViewPostCommentManager manager = FindCommentManager(comment.LinkId);
            if (manager != null)
            {
                manager.Save_Tapped(comment);
            }
            App.BaconMan.TelemetryMan.ReportEvent(this, "CommentSaveTapped");
        }

        private void CommentShare_Click(object sender, RoutedEventArgs e)
        {
            Comment comment = (sender as FrameworkElement).DataContext as Comment;
            FlipViewPostCommentManager manager = FindCommentManager(comment.LinkId);
            if (manager != null)
            {
                manager.Share_Tapped(comment);
            }
            App.BaconMan.TelemetryMan.ReportEvent(this, "CommentShareTapped");
        }

        private void CommentPermalink_Click(object sender, RoutedEventArgs e)
        {
            Comment comment = (sender as FrameworkElement).DataContext as Comment;

            FlipViewPostCommentManager manager = FindCommentManager(comment.LinkId);
            if (manager != null)
            {
                manager.CopyPermalink_Tapped(comment);
            }
            App.BaconMan.TelemetryMan.ReportEvent(this, "CommentPermalinkTapped");
        }

        private void CommentCollpase_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Animate the text
            AnimateText((FrameworkElement)sender);

            Comment comment = (sender as FrameworkElement).DataContext as Comment;
            FlipViewPostCommentManager manager = FindCommentManager(comment.LinkId);
            if (manager != null)
            {
                manager.Collpase_Tapped(comment);
            }
        }

        private void CollapsedComment_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Comment comment = (sender as FrameworkElement).DataContext as Comment;
            FlipViewPostCommentManager manager = FindCommentManager(comment.LinkId);
            if (manager != null)
            {
                manager.Expand_Tapped(comment);
            }
        }

        /// <summary>
        /// Does the text animation on the text box in the Grid.
        /// Note!! We do this crazy animation stuff so the UI feel responsive.
        /// It would be ideal to use the SimpleButtonText control but it is too expensive for the virtual list.
        /// </summary>
        /// <param name="textBlockContainer"></param>
        private void AnimateText(FrameworkElement textBlockContainer)
        {
            // Make sure it has children
            if(VisualTreeHelper.GetChildrenCount(textBlockContainer) != 1)
            {
                return;
            }

            // Try to get the text block
            TextBlock textBlock = (TextBlock)VisualTreeHelper.GetChild(textBlockContainer, 0);

            // Return if failed.
            if(textBlock == null)
            {
                return;
            }

            // Make a storyboard
            Storyboard storyboard = new Storyboard();
            ColorAnimation colorAnimation = new ColorAnimation();
            storyboard.Children.Add(colorAnimation);

            // Set them up.
            Storyboard.SetTarget(storyboard, textBlock);
            Storyboard.SetTargetProperty(storyboard, "(TextBlock.Foreground).(SolidColorBrush.Color)");
            storyboard.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 400));
            colorAnimation.Duration = new Duration(new TimeSpan(0, 0, 0, 0, 400));
            storyboard.BeginTime = new TimeSpan(0, 0, 0, 0, 100);

            // Set the colors
            colorAnimation.To = Color.FromArgb(255, 153, 153, 153);
            colorAnimation.From = ((SolidColorBrush)Application.Current.Resources["SystemControlBackgroundAccentBrush"]).Color;

            // Set the text to be the accent color
            textBlock.Foreground = ((SolidColorBrush)Application.Current.Resources["SystemControlBackgroundAccentBrush"]);

            // And animate it back out.
            storyboard.Begin();
        }

        /// <summary>
        /// Finds the comment manager for a given post. If one isn't found it returns null.
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        private FlipViewPostCommentManager FindCommentManager(string postId)
        {
            string searchId = postId;
            if (postId.StartsWith("t3_"))
            {
                searchId = postId.Substring(3);
            }

            lock (m_commentManagers)
            {
                foreach (FlipViewPostCommentManager searchManager in m_commentManagers)
                {
                    if (searchManager.Post.Id.Equals(searchId))
                    {
                        return searchManager;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Fired when a user taps a link in a comment
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MarkdownTextBlock_OnMarkdownLinkTapped(object sender, UniversalMarkdown.OnMarkdownLinkTappedArgs e)
        {
            App.BaconMan.ShowGlobalContent(e.Link);
        }

        #endregion

        #region Pro Tip Logic

        /// <summary>
        /// Shows the comment scroll tip if needed
        /// </summary>
        public void ShowCommentScrollTipIfNeeded()
        {
            if(!App.BaconMan.UiSettingsMan.FlipView_ShowCommentScrollTip)
            {
                return;
            }

            // Never show it again.
            App.BaconMan.UiSettingsMan.FlipView_ShowCommentScrollTip = false;

            // Create the tip UI, add it to the UI and show it.
            m_commentTipPopUp = new TipPopUp();
            m_commentTipPopUp.Margin = new Thickness(0, 0, 0, 60);
            m_commentTipPopUp.VerticalAlignment = VerticalAlignment.Bottom;
            m_commentTipPopUp.TipHideComplete += CommentTipPopUp_TipHideComplete;
            ui_contentRoot.Children.Add(m_commentTipPopUp);
            m_commentTipPopUp.ShowTip();
        }

        /// <summary>
        /// Hides the comment scroll tip if needed.
        /// </summary>
        public void HideCommentScrollTipIfNeeded()
        {
            // If we don't have one return.
            if(m_commentTipPopUp == null)
            {
                return;
            }

            // Tell it to hide and set it to null
            m_commentTipPopUp.HideTip();
            m_commentTipPopUp = null;
        }

        /// <summary>
        /// Fired when the hide is complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CommentTipPopUp_TipHideComplete(object sender, EventArgs e)
        {
            TipPopUp popUp = (TipPopUp)sender;

            // Unregister the event
            popUp.TipHideComplete += CommentTipPopUp_TipHideComplete;

            // Remove the tip from the UI
            ui_contentRoot.Children.Remove(popUp);
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
                    if (m_loadingOverlay != null)
                    {
                        return;
                    }
                    m_loadingOverlay = new LoadingOverlay();
                }

                m_loadingOverlay.OnHideComplete += LoadingOverlay_OnHideComplete;
                Grid.SetRowSpan(m_loadingOverlay, 3);
                ui_contentRoot.Children.Add(m_loadingOverlay);
                m_loadingOverlay.Show();
            });
        }

        /// <summary>
        /// Hides the exiting loading overlay
        /// </summary>
        private void HideFullScreenLoading()
        {
            LoadingOverlay overlay = null;
            lock(this)
            {
                if (m_loadingOverlay == null)
                {
                    return;
                }
                overlay = m_loadingOverlay;
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
            ui_contentRoot.Children.Remove(m_loadingOverlay);
            lock(this)
            {
                m_loadingOverlay = null;
            }
        }

        #endregion

        #region Full Screen Logic

        /// <summary>
        /// Fired when the control wants to go full screen.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlipViewContentControl_OnToggleFullscreen(object sender, FlipViewContentControl.OnToggleFullScreenEventArgs e)
        {
            // Get the post
            Post post = ((Post)((FrameworkElement)sender).DataContext);

            // Set if we are full screen or not.
            m_isFullScreen = e.GoFullScreen;

            // Hide or show the header
            if (e.GoFullScreen == (post.FlipviewHeaderVisibility == Visibility.Visible))
            {
                ToggleHeader(post);
            }

            // #todo scroll comments to the top so they aren't visible?
        }

        /// <summary>
        /// Fired when the post header toggle is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PostHeaderToggle_Click(object sender, RoutedEventArgs e)
        {
            Post post = (Post)((FrameworkElement)sender).DataContext;
            ToggleHeader(post);
        }

        /// <summary>
        /// Given a post toggles the header
        /// </summary>
        /// <param name="post"></param>
        private void ToggleHeader(Post post)
        {
            // #todo animate this
            post.FlipviewHeaderVisibility = post.FlipviewHeaderVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            post.HeaderCollpaseToggleAngle = post.FlipviewHeaderVisibility == Visibility.Visible ? 180 : 0;

            // Update the header size
            SetHeaderSizes();
        }

        #endregion

        #region Comment Sort

        /// <summary>
        /// Fired when either comment sort is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenMenuFlyout_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (element != null)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
            App.BaconMan.TelemetryMan.ReportEvent(this, "CommentSortTapped");
        }

        /// <summary>
        /// Fired when a user taps a new sort type for comments.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CommentSortMenu_Click(object sender, RoutedEventArgs e)
        {
            // Get the post
            Post post = (Post)((FrameworkElement)sender).DataContext;

            // Update sort type
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            post.CommentSortType = GetCommentSortFromString(item.Text);

            // Get the collector and update the sort
            FlipViewPostCommentManager commentManager = FindCommentManager(post.Id);
            commentManager.ChangeCommentSort();
        }

        private CommentSortTypes GetCommentSortFromString(string typeString)
        {
            typeString = typeString.ToLower();
            switch(typeString)
            {
                case "best":
                default:
                    return CommentSortTypes.Best;
                case "controversial":
                    return CommentSortTypes.Controversial;
                case "new":
                    return CommentSortTypes.New;
                case "old":
                    return CommentSortTypes.Old;
                case "q&a":
                    return CommentSortTypes.QA;
                case "top":
                    return CommentSortTypes.Top;
            }
        }

        private void CommentShowingCountMenu_Click(object sender, RoutedEventArgs e)
        {
            // Get the post
            Post post = (Post)((FrameworkElement)sender).DataContext;

            // Parse the new comment count
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            post.CurrentCommentShowingCount = int.Parse(item.Text);

            // Get the collector and update the sort
            FlipViewPostCommentManager commentManager = FindCommentManager(post.Id);
            commentManager.UpdateShowingCommentCount(post.CurrentCommentShowingCount);
        }

        #endregion

        /// <summary>
        /// Sets the post content. For now all we give the flip view control is the URL
        /// and it must figure out the rest on it's own.
        /// </summary>
        /// <param name="post"></param>
        private void SetPostContent(ref Post post, bool isVisiblePost)
        {
            if(post.FlipPost == null)
            {
                post.FlipPost = post;
            }

            // Set that the post is visible if it is
            post.IsPostVisible = isVisiblePost;
        }

        /// <summary>
        /// Clears the post of any content
        /// </summary>
        /// <param name="post"></param>
        private void ClearPostContent(ref Post post)
        {
            post.FlipPost = null;
            post.IsPostVisible = false;
        }

        private void ui_contentRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetHeaderSizes();
        }

        private void Grid_Tapped(object sender, TappedRoutedEventArgs e)
        {

        }
    }
}
