using BaconBackend.DataObjects;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using BaconBackend.Managers;
using Windows.UI.Xaml.Media.Imaging;
using System.Collections.ObjectModel;
using BaconBackend.Collectors;
using System.Threading.Tasks;
using Baconit.HelperControls;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using Baconit.Panels.FlipView;

namespace Baconit.Panels
{
    public sealed partial class SubredditPanel : UserControl, IPanel
    {
        //
        // Private vars
        //
        private bool _mIsVisible;
        private Subreddit _mSubreddit;
        private PostCollector _mCollector;
        private IPanelHost _mHost;
        private readonly ObservableCollection<Post> _mPostsLists = new ObservableCollection<Post>();
        private SortTypes _mCurrentSortType;
        private SortTimeTypes _mCurrentSortTimeType;
        private LoadingOverlay _mLoadingOverlay;

        public SubredditPanel()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += SubredditPanel_Loaded;

            // Set the post list
            ui_postList.ItemsSource = _mPostsLists;
        }

        private void SubredditPanel_Loaded(object sender, RoutedEventArgs e)
        {
            ui_postList.OnListEndDetectedEvent += Ui_postList_OnListEndDetectedEvent;
            // Set the threshold so we have time to get stories before they get to the bottom.
            ui_postList.EndOfListDetectionThrehold = 0.70;
            ui_splitView.PaneClosing += SplitView_PaneClosing;
        }

        public async void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            // Capture the host
            _mHost = host;

            Subreddit subreddit = null;
            if (arguments.ContainsKey(PanelManager.NavArgsSubredditName))
            {
                // Try to get the subreddit locally
                subreddit = App.BaconMan.SubredditMan.GetSubredditByDisplayName((string)arguments[PanelManager.NavArgsSubredditName]);

                // If that failed try to get it from the web
                if(subreddit == null)
                {
                    // Show the loading UI
                    ShowFullScreenLoading();

                    // Try to get the subreddit from the web
                    subreddit = await App.BaconMan.SubredditMan.GetSubredditFromWebByDisplayName((string)arguments[PanelManager.NavArgsSubredditName]);

                    // Hide the loading UI
                    // The loading ring will be set inactive by the animation complete
                    HideFullScreenLoading();
                }
            }

            if(subreddit == null)
            {
                // Hmmmm. We can't load the subreddit. Show a message and go back
                ShowFullScreenLoading();
                App.BaconMan.MessageMan.ShowMessageSimple("Hmmm, That's Not Right", "We can't load this subreddit right now, check your Internet connection.");

                // We can't call go back with navigating, so use the dispatcher to make a delayed call.
                await Task.Run(async () =>
                {
                    // We need to wait some time until the transition animation is done or we can't go back.
                    await Task.Delay(500);

                    // Try to go back now.
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        _mHost.GoBack();
                    });
                });

                // Get out of here.
                return;
            }

            // Get the sort type
            var sortType = arguments.ContainsKey(PanelManager.NavArgsSubredditSort) ? (SortTypes)arguments[PanelManager.NavArgsSubredditSort] : App.BaconMan.UiSettingsMan.SubredditListDefaultSortType;
            var postSortTime = arguments.ContainsKey(PanelManager.NavArgsSubredditSortTime) ? (SortTimeTypes)arguments[PanelManager.NavArgsSubredditSortTime] : App.BaconMan.UiSettingsMan.SubredditListDefaultSortTimeType;

            // Do the rest of the setup
            SetupPage(subreddit, sortType, postSortTime);
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            OnNavigatingTo();
        }

        public void OnNavigatingFrom()
        {
            _mIsVisible = false;
        }

        public void OnNavigatingTo()
        {
            _mIsVisible = true;

            // Make sure we are up to date
            _mCollector?.Update();

            // Set the task bar color
            _mHost.SetStatusBar(Color.FromArgb(255,10,10,10));
        }

        public void OnCleanupPanel()
        {
            if(_mCollector != null)
            {
                // Remove the listeners so we won't get updates.
                _mCollector.OnCollectorStateChange -= Collector_OnCollectorStateChange;
                _mCollector.OnCollectionUpdated -= Collector_OnCollectionUpdated;
            }
        }

        /// <summary>
        /// Fired when the panel should try to reduce memory if possible. This will only be called
        /// while the panel isn't visible.
        /// </summary>
        public void OnReduceMemory()
        {
            // Ignore for now.
        }

        #region Subreddit Setup

        public void SetupPage(Subreddit subreddit, SortTypes sortType, SortTimeTypes sortTimeType)
        {
            // Capture the subreddit
            _mSubreddit = subreddit;

            // Get the sort type
            SetCurrentSort(sortType);

            // Set the time sort
            SetCurrentTimeSort(sortTimeType);

            // Get the collector and register for updates.
            _mCollector = PostCollector.GetCollector(_mSubreddit, App.BaconMan, _mCurrentSortType, _mCurrentSortTimeType);
            _mCollector.OnCollectorStateChange += Collector_OnCollectorStateChange;
            _mCollector.OnCollectionUpdated += Collector_OnCollectionUpdated;

            // Kick off an update of the subreddits if needed.
            _mCollector.Update(false, 30);

            // Set any posts that exist right now
            SetPosts(0, _mCollector.GetCurrentPosts(), true);

            // Setup the UI with the name.
            ui_subredditName.Text = $"/r/{_mSubreddit.DisplayName}";
        }

        #endregion

        #region Post Loading

        /// <summary>
        /// Fired when the collector state is updated.
        /// </summary>
        /// <param name="state">The new state</param>
        private async void Collector_OnCollectorStateChange(object sender, CollectorStateChangeArgs args)
        {
            // Set loading if needed.
            ToggleLoadingBar(args.State == CollectorState.Updating || args.State == CollectorState.Extending);

            // Toggle the suppress depending on if we are updating, extending, or idle
            ui_postList.SuppressEndOfListEvent = args.State == CollectorState.Updating || args.State == CollectorState.Extending;

            // If we had an error show a message.
            if (_mIsVisible && args.State == CollectorState.Error)
            {
                if(args.ErrorState == CollectorErrorState.ServiceDown)
                {
                    App.BaconMan.MessageMan.ShowRedditDownMessage();
                }
                else
                {
                    App.BaconMan.MessageMan.ShowMessageSimple("That's Not Right", "We can't update this subreddit right now, check your Internet connection.");
                }
            }

            // Show no posts if nothing was loaded
            if (args.State == CollectorState.Idle || args.State == CollectorState.FullyExtended)
            {
                var postLoaded = _mCollector.GetCurrentPosts().Count != 0;
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    ui_noPostText.Visibility = postLoaded ? Visibility.Collapsed : Visibility.Visible;
                });
            }
        }

        /// <summary>
        /// Fired when the collection list has been updated.
        /// </summary>
        /// <param name="startingPos"></param>
        /// <param name="changedPosts"></param>
        private void Collector_OnCollectionUpdated(object sender, CollectionUpdatedArgs<Post> args)
        {
            // Update the post or posts
            SetPosts(args.StartingPosition, args.ChangedItems, args.IsFreshUpdate);
        }

        /// <summary>
        /// Fired when there is an error loading the list
        /// </summary>
        /// <param name="e"></param>
        public async void OnError(Exception e)
        {
            // Dispatch to the UI thread
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                App.BaconMan.MessageMan.ShowMessageSimple("Oops", "Baconit can't load any posts right now, check your Internet connection.");
                ToggleLoadingBar(false);
            });
        }

        /// <summary>
        /// Sets the posts to the UI.
        /// </summary>
        /// <param name="startingPos"></param>
        /// <param name="newPosts"></param>
        private async void SetPosts(int startingPos, List<Post> newPosts, bool isFreshUpdate)
        {
            // Dispatch to the UI thread
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Setup the insert
                var insertIndex = startingPos;

                // Lock the list
                lock(_mPostsLists)
                {
                    // Set up the objects for the UI
                    foreach (var post in newPosts)
                    {
                        // Check if we are adding or inserting.
                        var isReplace = insertIndex < _mPostsLists.Count;

                        // If this is a replace and the urls are the same just set the image
                        if (isReplace && post.Url == _mPostsLists[insertIndex].Url)
                        {
                            post.Image = _mPostsLists[insertIndex].Image;
                            post.ImageVisibility = _mPostsLists[insertIndex].ImageVisibility;
                        }
                        else
                        {
                            // Request the image if there is one
                            if (ImageManager.IsThumbnailImage(post.Thumbnail))
                            {
                                var request = new ImageManager.ImageManagerRequest
                                {
                                    Url = post.Thumbnail,
                                    ImageId = post.Id
                                };
                                request.OnRequestComplete += OnRequestComplete;
                                App.BaconMan.ImageMan.QueueImageRequest(request);
                            }
                        }

                        if (isReplace)
                        {
                            if(_mPostsLists[insertIndex].Id.Equals(post.Id))
                            {
                                // If the post is the same, just update the UI vars.
                                // If we replace the entire post the UI freaks out.
                                _mPostsLists[insertIndex].Score = post.Score;
                                _mPostsLists[insertIndex].TitleTextColor = post.TitleTextColor;
                                _mPostsLists[insertIndex].Title = post.Title;
                                _mPostsLists[insertIndex].Likes = post.Likes;
                                _mPostsLists[insertIndex].SubTextLine1 = post.SubTextLine1;
                                _mPostsLists[insertIndex].NewCommentText = post.NewCommentText;
                                _mPostsLists[insertIndex].SubTextLine2PartOne = post.SubTextLine2PartOne;
                                _mPostsLists[insertIndex].SubTextLine2PartTwo = post.SubTextLine2PartTwo;
                            }
                            else
                            {
                                // Replace the current item
                                _mPostsLists[insertIndex] = post;
                            }
                        }
                        else
                        {
                            // Add it to the end
                            _mPostsLists.Add(post);
                        }
                        insertIndex++;
                    }

                    // If it was a fresh update, remove anything past the last story sent.
                    while(isFreshUpdate && _mPostsLists.Count > newPosts.Count)
                    {
                        _mPostsLists.RemoveAt(_mPostsLists.Count - 1);
                    }
                }
            });
        }

        #endregion

        #region Image Managment

        public async void OnRequestComplete(object sender, ImageManager.ImageManagerResponseEventArgs response)
        {
            // Remove the event
            var request = (ImageManager.ImageManagerRequest)sender;
            request.OnRequestComplete -= OnRequestComplete;

            // Make sure we were successful.
            if (response.Success)
            {
                // Try to find the post
                Post owningPost = null;

                // Lock the list
                lock(_mPostsLists)
                {
                    foreach (var post in _mPostsLists)
                    {
                        if (post.Id.Equals(response.Request.ImageId))
                        {
                            owningPost = post;
                            break;
                        }
                    }
                }

                // If we didn't find it just return
                if (owningPost == null)
                {
                    return;
                }

                // Dispatch to the UI thread
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    // Create a bitmap and set the source
                    owningPost.Image = new BitmapImage();
                    owningPost.Image.SetSource(response.ImageStream);

                    // Set the image visible
                    owningPost.ImageVisibility = Visibility.Visible;
                });
            }
        }


        #endregion

        #region Click Actions

        private void PostList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Just set it back to -1 to remove the highlight
            ui_postList.SelectedIndex = -1;
        }

        /// <summary>
        /// Fired when a post title is tapped.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PostTitle_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Get the post
            var tappedPost = (sender as FrameworkElement).DataContext as Post;

            // Go Go Go!
            NavigateToFlipView(tappedPost);
        }

        /// <summary>
        /// Fired when a up vote arrow is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UpVote_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var post = (sender as FrameworkElement).DataContext as Post;
            _mCollector.ChangePostVote(post, PostVoteAction.UpVote);
        }

        /// <summary>
        /// Fired when a down vote arrow is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DownVote_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var post = (sender as FrameworkElement).DataContext as Post;
            _mCollector.ChangePostVote(post, PostVoteAction.DownVote);
        }

        private void NavigateToFlipView(Post post)
        {
            // Send the subreddit and post to flipview
            var args = new Dictionary<string, object>();
            args.Add(PanelManager.NavArgsSubredditName, _mSubreddit.DisplayName);
            args.Add(PanelManager.NavArgsSubredditSort, _mCurrentSortType);
            args.Add(PanelManager.NavArgsSubredditSortTime, _mCurrentSortTimeType);
            args.Add(PanelManager.NavArgsPostId, post.Id);
            _mHost.Navigate(typeof(FlipViewPanel), _mSubreddit.DisplayName + _mCurrentSortType + _mCurrentSortTimeType, args);
        }

        private void Post_Holding(object sender, HoldingRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
            TelemetryManager.ReportEvent(this, "PostHeldOpenedContextMenu");
        }

        private void Post_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
            TelemetryManager.ReportEvent(this, "RightClickedOpenedContextMenu");
        }

        private void SavePost_Click(object sender, RoutedEventArgs e)
        {
            var post = (sender as FrameworkElement).DataContext as Post;
            _mCollector.SaveOrHidePost(post, !post.IsSaved, null);
            TelemetryManager.ReportEvent(this, "PostSavedTapped");
        }

        private void HidePost_Click(object sender, RoutedEventArgs e)
        {
            var post = (sender as FrameworkElement).DataContext as Post;
            _mCollector.SaveOrHidePost(post, null, !post.IsHidden);
            TelemetryManager.ReportEvent(this, "HidePostTapped");
        }

        private void CopyLink_Click(object sender, RoutedEventArgs e)
        {
            // Get the post and copy the url into the clipboard
            var post = (sender as FrameworkElement).DataContext as Post;
            var data = new DataPackage();
            if (string.IsNullOrWhiteSpace(post.Url))
            {
                data.SetText("http://www.reddit.com" + post.Permalink);
            }
            else
            {
                data.SetText(post.Url);
            }
            Clipboard.SetContent(data);
            TelemetryManager.ReportEvent(this, "CopyLinkTapped");
        }

        private void CopyPermalink_Click(object sender, RoutedEventArgs e)
        {
            // Get the post and copy the url into the clipboard
            var post = (sender as FrameworkElement).DataContext as Post;
            var data = new DataPackage();
            data.SetText("http://www.reddit.com" + post.Permalink);
            Clipboard.SetContent(data);
            TelemetryManager.ReportEvent(this, "CopyLinkTapped");
        }

        private void ViewUser_Click(object sender, RoutedEventArgs e)
        {
            // Get the post
            var post = (sender as FrameworkElement).DataContext as Post;
            var args = new Dictionary<string, object>();
            args.Add(PanelManager.NavArgsUserName, post.Author);
            _mHost.Navigate(typeof(UserProfile), post.Author, args);
            TelemetryManager.ReportEvent(this, "SubredditNavToUser");
        }

        private void SubredditHeader_Tapped(object sender, TappedRoutedEventArgs e)
        {
            _mHost.ToggleMenu(true);
        }

        #endregion

        #region EndlessScrollingLogic

        private void Ui_postList_OnListEndDetectedEvent(object sender, ListEndDetected e)
        {
            // Suppress more event until we get more items.
            ui_postList.SuppressEndOfListEvent = true;
            _mCollector.ExtendCollection();
        }

        #endregion

        #region Subreddit Sort

        /// <summary>
        /// Fired when the user taps the sort text
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Sort_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
        }

        /// <summary>
        /// Fired when a sort menu item is tapped.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SortMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Get the new sort
            var item = sender as MenuFlyoutItem;
            var newType = GetSortFromString(item.Text);

            // Don't do anything if we already are.
            if(newType == _mCurrentSortType)
            {
                return;
            }

            // Navigate to the new page
            var args = new Dictionary<string, object>();
            args.Add(PanelManager.NavArgsSubredditName, _mSubreddit.DisplayName);
            args.Add(PanelManager.NavArgsSubredditSort, newType);
            args.Add(PanelManager.NavArgsSubredditSortTime, _mCurrentSortTimeType);
            _mHost.Navigate(typeof(SubredditPanel), _mSubreddit.GetNavigationUniqueId(newType, _mCurrentSortTimeType), args);
        }

        private SortTypes GetSortFromString(string sort)
        {
            var text = sort.ToLower();
            switch(text)
            {
                case "rising":
                        return SortTypes.Rising;
                case "new":
                        return SortTypes.New;
                case "controversial":
                        return SortTypes.Controversial;
                case "top":
                        return SortTypes.Top;
                default:
                case "hot":
                        return SortTypes.Hot;
            }
        }

        private void SetCurrentSort(SortTypes type)
        {
            _mCurrentSortType = type;
            switch (type)
            {
                case SortTypes.Rising:
                    ui_sortText.Text = "Rising";
                    break;
                case SortTypes.Hot:
                    ui_sortText.Text = "Hot";
                    break;
                case SortTypes.Controversial:
                    ui_sortText.Text = "Controversial";
                    break;
                case SortTypes.New:
                    ui_sortText.Text = "New";
                    break;
                case SortTypes.Top:
                    ui_sortText.Text = "Top";
                    break;
            }

            // Show or hide the time sort if it is top or not.
            ui_timeSortHolder.Visibility = _mCurrentSortType == SortTypes.Top ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Subreddit Time Sort

        /// <summary>
        /// Fired when the user taps the sort time text
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SortTime_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
        }

        /// <summary>
        /// Fired when a sort menu item is tapped.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SortTimeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Get the new sort time
            var item = sender as MenuFlyoutItem;
            var newType = GetTimeSortFromString(item.Text);

            // Don't do anything if we already are.
            if (newType == _mCurrentSortTimeType)
            {
                return;
            }

            // Navigate to the new page
            var args = new Dictionary<string, object>();
            args.Add(PanelManager.NavArgsSubredditName, _mSubreddit.DisplayName);
            args.Add(PanelManager.NavArgsSubredditSort, _mCurrentSortType);
            args.Add(PanelManager.NavArgsSubredditSortTime, newType);
            _mHost.Navigate(typeof(SubredditPanel), _mSubreddit.GetNavigationUniqueId(_mCurrentSortType, newType), args);
        }

        private SortTimeTypes GetTimeSortFromString(string timeSort)
        {
            var text = timeSort.ToLower();
            switch (text)
            {
                case "all time":
                    return SortTimeTypes.AllTime;
                case "past day":
                    return SortTimeTypes.Day;
                case "past hour":
                    return SortTimeTypes.Hour;
                case "past month":
                    return SortTimeTypes.Month;
                default:
                case "past week":
                    return SortTimeTypes.Week;
                case "past year":
                    return SortTimeTypes.Year;
            }
        }

        private void SetCurrentTimeSort(SortTimeTypes type)
        {
            _mCurrentSortTimeType = type;
            switch (type)
            {
                case SortTimeTypes.AllTime:
                    ui_sortTimeText.Text = "All Time";
                    break;
                case SortTimeTypes.Day:
                    ui_sortTimeText.Text = "Past Day";
                    break;
                case SortTimeTypes.Hour:
                    ui_sortTimeText.Text = "Past Hour";
                    break;
                case SortTimeTypes.Month:
                    ui_sortTimeText.Text = "Past Month";
                    break;
                case SortTimeTypes.Week:
                    ui_sortTimeText.Text = "Past Week";
                    break;
                case SortTimeTypes.Year:
                    ui_sortTimeText.Text = "Past Year";
                    break;
            }
        }

        #endregion

        #region Side Bar Logic

        /// <summary>
        /// Fired when a user taps the subreddit side bar button is pressed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AppBarSideBarOpen_OnIconTapped(object sender, EventArgs e)
        {
            // Make sure we have a sub
            if(_mSubreddit == null)
            {
                return;
            }

            // Set the subreddit
            ui_subredditSideBar.SetSubreddit(_mHost, _mSubreddit);

            // Show the side bar
            ui_splitView.IsPaneOpen = true;

            // Show the blocking UI
            ShowFullScreenLoading(false);
        }

        /// <summary>
        /// Fired when the subreddit panel is closed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void SplitView_PaneClosing(SplitView sender, SplitViewPaneClosingEventArgs args)
        {
            // Hide the loading screen.
            HideFullScreenLoading();
        }

        /// <summary>
        /// Fired by the side bar when it should be closed because it is navigating.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SubredditSideBar_OnShouldClose(object sender, EventArgs e)
        {
            ui_splitView.IsPaneOpen = false;
        }

        #endregion

        #region Full Screen Loading

        /// <summary>
        /// Shows a loading overlay if there isn't one already
        /// </summary>
        private async void ShowFullScreenLoading(bool showLoading = true)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                // Make sure we don't have one already, if so get out of here.
                lock (this)
                {
                    if (_mLoadingOverlay != null)
                    {
                        return;
                    }
                    _mLoadingOverlay = new LoadingOverlay();
                }

                _mLoadingOverlay.OnHideComplete += LoadingOverlay_OnHideComplete;
                Grid.SetRowSpan(_mLoadingOverlay, 5);
                ui_contentRoot.Children.Add(_mLoadingOverlay);
                _mLoadingOverlay.Show(showLoading);
            });
        }

        /// <summary>
        /// Hides the exiting loading overlay
        /// </summary>
        private void HideFullScreenLoading()
        {
            LoadingOverlay overlay = null;
            lock (this)
            {
                if (_mLoadingOverlay == null)
                {
                    return;
                }
                overlay = _mLoadingOverlay;
            }

            overlay?.Hide();
        }

        /// <summary>
        /// Fired when the overlay is hidden.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadingOverlay_OnHideComplete(object sender, EventArgs e)
        {
            ui_contentRoot.Children.Remove(_mLoadingOverlay);
            lock (this)
            {
                _mLoadingOverlay = null;
            }
        }

        #endregion

        private async void ToggleLoadingBar(bool show)
        {
            // Dispatch to the UI thread
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ui_progressBar.IsIndeterminate = show ? true : false;
                ui_progressBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        private void MenuButton_Click(object sender, EventArgs e)
        {
            _mHost.ToggleMenu(true);
        }

        private void Refresh_Click(object sender, EventArgs e)
        {
            // Kick off an update.
            _mCollector.Update(true);
        }

        /// <summary>
        /// Fired when the panel is resized.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SplitView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Important! The open panel size becomes the min width of the entire split view!
            // Since there is no max panel size we must do it ourselves. Set the max to be 380, but if the
            // view is smaller make it smaller. Note we have to have the - 10 on the size or it will prevent
            // resizing when we hit the actualwidth.
            var panelSize = ui_splitView.ActualWidth - 10 < 380 ? ui_splitView.ActualWidth - 10 : 380;
            ui_splitView.OpenPaneLength = panelSize;
        }
    }
}
