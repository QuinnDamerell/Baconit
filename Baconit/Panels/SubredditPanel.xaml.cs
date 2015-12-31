using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using BaconBackend.Interfaces;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using BaconBackend.Managers;
using Windows.UI.Xaml.Media.Imaging;
using System.Collections.ObjectModel;
using BaconBackend.Collectors;
using System.Threading.Tasks;
using Baconit.HelperControls;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace Baconit.Panels
{
    public sealed partial class SubredditPanel : UserControl, IPanel
    {
        //
        // Private vars
        //
        bool m_isVisible = false;
        Subreddit m_subreddit;
        PostCollector m_collector;
        IPanelHost m_host;
        ObservableCollection<Post> m_postsLists = new ObservableCollection<Post>();
        SortTypes m_currentSortType;
        SortTimeTypes m_currentSortTimeType;
        LoadingOverlay m_loadingOverlay = null;

        public SubredditPanel()
        {
            this.InitializeComponent();
            this.DataContext = this;
            Loaded += SubredditPanel_Loaded;

            // Set the post list
            ui_postList.ItemsSource = m_postsLists;
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
            m_host = host;

            Subreddit subreddit = null;
            if (arguments.ContainsKey(PanelManager.NAV_ARGS_SUBREDDIT_NAME))
            {
                // Try to get the subreddit locally
                subreddit = App.BaconMan.SubredditMan.GetSubredditByDisplayName((string)arguments[PanelManager.NAV_ARGS_SUBREDDIT_NAME]);

                // If that failed try to get it from the web
                if(subreddit == null)
                {
                    // Show the loading UI
                    ShowFullScreenLoading();

                    // Try to get the subreddit from the web
                    subreddit = await App.BaconMan.SubredditMan.GetSubredditFromWebByDisplayName((string)arguments[PanelManager.NAV_ARGS_SUBREDDIT_NAME]);

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
                        m_host.GoBack();
                    });
                });

                // Get out of here.
                return;
            }

            // Get the sort type
            SortTypes sortType = arguments.ContainsKey(PanelManager.NAV_ARGS_SUBREDDIT_SORT) ? (SortTypes)arguments[PanelManager.NAV_ARGS_SUBREDDIT_SORT] : SortTypes.Hot;
            SortTimeTypes postSortTime = arguments.ContainsKey(PanelManager.NAV_ARGS_SUBREDDIT_SORT_TIME) ? (SortTimeTypes)arguments[PanelManager.NAV_ARGS_SUBREDDIT_SORT_TIME] : SortTimeTypes.Week;

            // Do the rest of the setup
            SetupPage(subreddit, sortType, postSortTime);
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            OnNavigatingTo();
        }

        public void OnNavigatingFrom()
        {
            m_isVisible = false;
        }

        public void OnNavigatingTo()
        {
            m_isVisible = true;

            if (m_collector != null)
            {
                // Make sure we are up to date
                m_collector.Update();
            }

            // Set the task bar color
            m_host.SetStatusBar(Color.FromArgb(255,10,10,10));
        }

        #region Subreddit Setup

        public void SetupPage(Subreddit subreddit, SortTypes sortType, SortTimeTypes sortTimeType)
        {
            // Capture the subreddit
            m_subreddit = subreddit;

            // Get the sort type
            SetCurrentSort(sortType);

            // Set the time sort
            SetCurrentTimeSort(sortTimeType);

            // Get the collector and register for updates.
            m_collector = PostCollector.GetCollector(m_subreddit, App.BaconMan, m_currentSortType, m_currentSortTimeType);
            m_collector.OnCollectorStateChange += Collector_OnCollectorStateChange;
            m_collector.OnCollectionUpdated += Collector_OnCollectionUpdated;

            // Kick off an update of the subreddits if needed.
            m_collector.Update(false, 30);

            // Set any posts that exist right now
            SetPosts(0, m_collector.GetCurrentPosts(), true);

            // Setup the UI with the name.
            ui_subredditName.Text = $"/r/{m_subreddit.DisplayName}";
        }

        #endregion

        #region Post Loading

        /// <summary>
        /// Fired when the collector state is updated.
        /// </summary>
        /// <param name="state">The new state</param>
        private async void Collector_OnCollectorStateChange(object sender, OnCollectorStateChangeArgs args)
        {
            // Set loading if needed.
            ToggleLoadingBar(args.State == CollectorState.Updating || args.State == CollectorState.Extending);

            // Toggle the suppress depending on if we are updating, extending, or idle
            ui_postList.SuppressEndOfListEvent = args.State == CollectorState.Updating || args.State == CollectorState.Extending;

            // If we had an error show a message.
            if (m_isVisible && args.State == CollectorState.Error)
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
                bool postLoaded = m_collector.GetCurrentPosts().Count != 0;
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
        private void Collector_OnCollectionUpdated(object sender, OnCollectionUpdatedArgs<Post> args)
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
                int insertIndex = startingPos;

                // Lock the list
                lock(m_postsLists)
                {
                    // Set up the objects for the UI
                    foreach (Post post in newPosts)
                    {
                        // Check if we are adding or inserting.
                        bool isReplace = insertIndex < m_postsLists.Count;

                        // If this is a replace and the urls are the same just set the image
                        if (isReplace && post.Url == m_postsLists[insertIndex].Url)
                        {
                            post.Image = m_postsLists[insertIndex].Image;
                            post.ImageVisibility = m_postsLists[insertIndex].ImageVisibility;
                        }
                        else
                        {
                            // Request the image if there is one
                            if (ImageManager.IsThumbnailImage(post.Thumbnail))
                            {
                                ImageManager.ImageManagerRequest request = new ImageManager.ImageManagerRequest()
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
                            if(m_postsLists[insertIndex].Id.Equals(post.Id))
                            {
                                // If the post is the same, just update the UI vars.
                                // If we replace the entire post the UI freaks out.
                                m_postsLists[insertIndex].Score = post.Score;
                                m_postsLists[insertIndex].TitleTextColor = post.TitleTextColor;
                                m_postsLists[insertIndex].Title = post.Title;
                                m_postsLists[insertIndex].Likes = post.Likes;
                                m_postsLists[insertIndex].SubTextLine1 = post.SubTextLine1;
                                m_postsLists[insertIndex].NewCommentText = post.NewCommentText;
                                m_postsLists[insertIndex].SubTextLine2PartOne = post.SubTextLine2PartOne;
                                m_postsLists[insertIndex].SubTextLine2PartTwo = post.SubTextLine2PartTwo;
                            }
                            else
                            {
                                // Replace the current item
                                m_postsLists[insertIndex] = post;
                            }
                        }
                        else
                        {
                            // Add it to the end
                            m_postsLists.Add(post);
                        }
                        insertIndex++;
                    }

                    // If it was a fresh update, remove anything past the last story sent.
                    while(isFreshUpdate && m_postsLists.Count > newPosts.Count)
                    {
                        m_postsLists.RemoveAt(m_postsLists.Count - 1);
                    }
                }
            });
        }

        #endregion

        #region Image Managment

        public async void OnRequestComplete(object sender, ImageManager.ImageManagerResponseEventArgs response)
        {
            // Remove the event
            ImageManager.ImageManagerRequest request = (ImageManager.ImageManagerRequest)sender;
            request.OnRequestComplete -= OnRequestComplete;

            // Make sure we were successful.
            if (response.Success)
            {
                // Try to find the post
                Post owningPost = null;

                // Lock the list
                lock(m_postsLists)
                {
                    foreach (Post post in m_postsLists)
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
            Post tappedPost = (sender as FrameworkElement).DataContext as Post;

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
            Post post = (sender as FrameworkElement).DataContext as Post;
            m_collector.ChangePostVote(post, PostVoteAction.UpVote);
        }

        /// <summary>
        /// Fired when a down vote arrow is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DownVote_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Post post = (sender as FrameworkElement).DataContext as Post;
            m_collector.ChangePostVote(post, PostVoteAction.DownVote);
        }

        private void NavigateToFlipView(Post post)
        {
            // Send the subreddit and post to flipview
            Dictionary<string, object> args = new Dictionary<string, object>();
            args.Add(PanelManager.NAV_ARGS_SUBREDDIT_NAME, m_subreddit.DisplayName);
            args.Add(PanelManager.NAV_ARGS_SUBREDDIT_SORT, m_currentSortType);
            args.Add(PanelManager.NAV_ARGS_SUBREDDIT_SORT_TIME, m_currentSortTimeType);
            args.Add(PanelManager.NAV_ARGS_POST_ID, post.Id);
            m_host.Navigate(typeof(FlipViewPanel), m_subreddit.DisplayName + m_currentSortType + m_currentSortTimeType, args);
        }

        private void Post_Holding(object sender, HoldingRoutedEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (element != null)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
            App.BaconMan.TelemetryMan.ReportEvent(this, "PostHeldOpenedContextMenu");
        }

        private void Post_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (element != null)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
            App.BaconMan.TelemetryMan.ReportEvent(this, "RightClickedOpenedContextMenu");
        }

        private void SavePost_Click(object sender, RoutedEventArgs e)
        {
            Post post = (sender as FrameworkElement).DataContext as Post;
            m_collector.SaveOrHidePost(post, !post.IsSaved, null);
            App.BaconMan.TelemetryMan.ReportEvent(this, "PostSavedTapped");
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
            if (String.IsNullOrWhiteSpace(post.Url))
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

        private void ViewUser_Click(object sender, RoutedEventArgs e)
        {
            // Get the post
            Post post = (sender as FrameworkElement).DataContext as Post;
            Dictionary<string, object> args = new Dictionary<string, object>();
            args.Add(PanelManager.NAV_ARGS_USER_NAME, post.Author);
            m_host.Navigate(typeof(UserProfile), post.Author, args);
            App.BaconMan.TelemetryMan.ReportEvent(this, "SubredditNavToUser");
        }

        private void SubredditHeader_Tapped(object sender, TappedRoutedEventArgs e)
        {
            m_host.ToggleMenu(true);
        }

        #endregion

        #region EndlessScrollingLogic

        private void Ui_postList_OnListEndDetectedEvent(object sender, HelperControls.OnListEndDetected e)
        {
            // Suppress more event until we get more items.
            ui_postList.SuppressEndOfListEvent = true;
            m_collector.ExtendCollection();
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
            FrameworkElement element = sender as FrameworkElement;
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
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            SortTypes newType = GetSortFromString(item.Text);

            // Don't do anything if we already are.
            if(newType == m_currentSortType)
            {
                return;
            }

            // Navigate to the new page
            Dictionary<string, object> args = new Dictionary<string, object>();
            args.Add(PanelManager.NAV_ARGS_SUBREDDIT_NAME, m_subreddit.DisplayName);
            args.Add(PanelManager.NAV_ARGS_SUBREDDIT_SORT, newType);
            args.Add(PanelManager.NAV_ARGS_SUBREDDIT_SORT_TIME, m_currentSortTimeType);
            m_host.Navigate(typeof(SubredditPanel), m_subreddit.GetNavigationUniqueId(newType, m_currentSortTimeType), args);
        }

        private SortTypes GetSortFromString(string sort)
        {
            string text = sort.ToLower();
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
            m_currentSortType = type;
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
            ui_timeSortHolder.Visibility = m_currentSortType == SortTypes.Top ? Visibility.Visible : Visibility.Collapsed;
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
            FrameworkElement element = sender as FrameworkElement;
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
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            SortTimeTypes newType = GetTimeSortFromString(item.Text);

            // Don't do anything if we already are.
            if (newType == m_currentSortTimeType)
            {
                return;
            }

            // Navigate to the new page
            Dictionary<string, object> args = new Dictionary<string, object>();
            args.Add(PanelManager.NAV_ARGS_SUBREDDIT_NAME, m_subreddit.DisplayName);
            args.Add(PanelManager.NAV_ARGS_SUBREDDIT_SORT, m_currentSortType);
            args.Add(PanelManager.NAV_ARGS_SUBREDDIT_SORT_TIME, newType);
            m_host.Navigate(typeof(SubredditPanel), m_subreddit.GetNavigationUniqueId(m_currentSortType, newType), args);
        }

        private SortTimeTypes GetTimeSortFromString(string timeSort)
        {
            string text = timeSort.ToLower();
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
            m_currentSortTimeType = type;
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
            if(m_subreddit == null)
            {
                return;
            }

            // Set the subreddit
            ui_subredditSideBar.SetSubreddit(m_host, m_subreddit);

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
                    if (m_loadingOverlay != null)
                    {
                        return;
                    }
                    m_loadingOverlay = new LoadingOverlay();
                }

                m_loadingOverlay.OnHideComplete += LoadingOverlay_OnHideComplete;
                Grid.SetRowSpan(m_loadingOverlay, 5);
                ui_contentRoot.Children.Add(m_loadingOverlay);
                m_loadingOverlay.Show(showLoading);
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
                if (m_loadingOverlay == null)
                {
                    return;
                }
                overlay = m_loadingOverlay;
            }

            if(overlay != null)
            {
                overlay.Hide();
            }
        }

        /// <summary>
        /// Fired when the overlay is hidden.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadingOverlay_OnHideComplete(object sender, EventArgs e)
        {
            ui_contentRoot.Children.Remove(m_loadingOverlay);
            lock (this)
            {
                m_loadingOverlay = null;
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
            m_host.ToggleMenu(true);
        }

        private void Refresh_Click(object sender, EventArgs e)
        {
            // Kick off an update.
            m_collector.Update(true);
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
            double panelSize = ui_splitView.ActualWidth - 10 < 380 ? ui_splitView.ActualWidth - 10 : 380;
            ui_splitView.OpenPaneLength = panelSize;
        }
    }
}
