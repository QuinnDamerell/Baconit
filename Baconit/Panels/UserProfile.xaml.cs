using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using Baconit.Interfaces;
using Baconit.Panels.FlipView;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using BaconBackend.Managers;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.Panels
{
    public sealed partial class UserProfile : UserControl, IPanel
    {
        /// <summary>
        /// Holds the user
        /// </summary>
        private User _mUser;

        /// <summary>
        /// Holds a ref to the host
        /// </summary>
        private IPanelHost _mHost;

        /// <summary>
        /// Holds the subreddit collector if we have one.
        /// </summary>
        private PostCollector _mPostCollector;

        /// <summary>
        /// Holds the comment collector if we have one.
        /// </summary>
        private CommentCollector _mCommentCollector;

        /// <summary>
        /// Holds the list of current posts
        /// </summary>
        private readonly ObservableCollection<Post> _mPostList = new ObservableCollection<Post>();

        /// <summary>
        /// Holds the list of current comments
        /// </summary>
        private readonly ObservableCollection<Comment> _mCommentList = new ObservableCollection<Comment>();

        /// <summary>
        /// Holds a ref to the post sort text block
        /// </summary>
        private TextBlock _mPostSortText;

        /// <summary>
        /// Holds a ref to the comment sort text block
        /// </summary>
        private TextBlock _mCommentSortText;

        /// <summary>
        /// The current sort type for the posts
        /// </summary>
        private SortTypes _mPostSort = SortTypes.New;

        /// <summary>
        /// The current sort type for the comments
        /// </summary>
        private SortTypes _mCommentSort = SortTypes.New;

        public UserProfile()
        {
            InitializeComponent();

            // Set the list sources
            ui_postList.ItemsSource = _mPostList;
            ui_commentList.ItemsSource = _mCommentList;
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            _mHost = host;

            if(!arguments.ContainsKey(PanelManager.NavArgsUserName))
            {
                ReportUserLoadFailed();
                return;
            }

            // Get the user
            var userName = (string)arguments[PanelManager.NavArgsUserName];
            ui_userName.Text = userName;

            // Show loading
            ui_loadingOverlay.Show(true, "Finding "+ userName);

            // Do the loading on a thread to get off the UI
            Task.Run(async () =>
            {
                // Make the request
                _mUser = await MiscellaneousHelper.GetRedditUser(App.BaconMan, userName);

                // Jump back to the UI thread, we will use low priority so we don't make any animations choppy.
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    // Check we got it.
                    if (_mUser == null)
                    {
                        ReportUserLoadFailed();
                        return;
                    }

                    // Fill in the UI
                    var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                    var postTime = origin.AddSeconds(_mUser.CreatedUtc).ToLocalTime();
                    ui_accountAgeText.Text = $"{TimeToTextHelper.TimeElapseToText(postTime)} old";

                    // Set cake day
                    var elapsed = DateTime.Now - postTime;
                    var fullYears = Math.Floor((elapsed.TotalDays / 365));
                    var daysUntil = (int)(elapsed.TotalDays - (fullYears * 365));
                    ui_cakeDayHolder.Visibility = daysUntil == 0 ? Visibility.Visible : Visibility.Collapsed;

                    // Set karma
                    ui_linkKarmaText.Text = $"{_mUser.LinkKarma:N0}";
                    ui_commentKarmaText.Text = $"{_mUser.CommentKarma:N0}";

                    // Set Gold
                    ui_goldHolder.Visibility = _mUser.IsGold ? Visibility.Visible : Visibility.Collapsed;

                    // Hide loading
                    ui_loadingOverlay.Hide();

                    // Kick off the updates
                    GetUserComments();
                    GetUserPosts();
                });
            });
        }

        private async void ReportUserLoadFailed()
        {
            // Show a message
            App.BaconMan.MessageMan.ShowMessageSimple("Failed To Load", "Check your Internet connection.");

            // Report
            TelemetryManager.ReportEvent(this, "UserProfileFailedToLoad");

            var wentBack = false;
            do
            {
                // Try to go back
                wentBack = _mHost.GoBack();

                // Wait for a bit and try again.
                await Task.Delay(100);
            }
            while (wentBack);
        }

        public void OnNavigatingFrom()
        {
        }

        public void OnCleanupPanel()
        {
            lock (this)
            {
                if (_mPostCollector != null)
                {
                    _mPostCollector.OnCollectionUpdated -= PostCollector_OnCollectionUpdated;
                    _mPostCollector.OnCollectorStateChange -= PostCollector_OnCollectorStateChange;
                    _mPostCollector = null;
                }

                if (_mCommentCollector != null)
                {
                    _mCommentCollector.OnCollectionUpdated -= CommentCollector_OnCollectionUpdated;
                    _mCommentCollector.OnCollectorStateChange -= CommentCollector_OnCollectorStateChange;
                    _mCommentCollector = null;
                }
            }
        }

        public async void OnNavigatingTo()
        {
            // Set the status bar color and get the size returned. If it is not 0 use that to move the
            // color of the page into the status bar.
            var statusBarHeight = await _mHost.SetStatusBar(null, 0);
            ui_contentRoot.Margin = new Thickness(0, -statusBarHeight, 0, 0);
            ui_contentRoot.Padding = new Thickness(0, statusBarHeight, 0, 0);
            ui_loadingOverlay.Margin = new Thickness(0, -statusBarHeight, 0, 0);
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            OnNavigatingTo();
        }

        /// <summary>
        /// Fired when the panel should try to reduce memory if possible. This will only be called
        /// while the panel isn't visible.
        /// </summary>
        public void OnReduceMemory()
        {
            // Ignore for now.
        }

        #region Posts

        /// <summary>
        /// Ensures a post collector has been made
        /// </summary>
        public void EnsurePostCollector(bool makeNew = false)
        {
            lock (this)
            {
                if(makeNew && _mPostCollector != null)
                {
                    _mPostCollector.OnCollectionUpdated -= PostCollector_OnCollectionUpdated;
                    _mPostCollector.OnCollectorStateChange -= PostCollector_OnCollectorStateChange;
                    _mPostCollector = null;
                }

                if (_mPostCollector == null)
                {
                    _mPostCollector = PostCollector.GetCollector(_mUser, App.BaconMan, _mPostSort);
                    _mPostCollector.OnCollectionUpdated += PostCollector_OnCollectionUpdated;
                    _mPostCollector.OnCollectorStateChange += PostCollector_OnCollectorStateChange;
                }
            }
        }

        /// <summary>
        /// Kicks off a update for users posts
        /// </summary>
        private void GetUserPosts()
        {
            if(_mUser == null)
            {
                return;
            }

            EnsurePostCollector();

            // Update the collector
            _mPostCollector.Update();
        }

        /// <summary>
        /// Fired when the post list is loading
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void PostCollector_OnCollectorStateChange(object sender, CollectorStateChangeArgs e)
        {
            // Jump to the UI thread
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                // Kill anything we have
                ui_postLoadingBar.IsIndeterminate = false;
                ui_postLoadingBar.Visibility = Visibility.Collapsed;
                ui_postLoadingRing.IsActive = false;
                ui_postLoadingRing.Visibility = Visibility.Collapsed;

                // Set the new loading
                if (e.State == CollectorState.Extending || e.State == CollectorState.Updating)
                {
                    if(_mPostList.Count == 0)
                    {
                        ui_postLoadingRing.IsActive = true;
                        ui_postLoadingRing.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ui_postLoadingBar.IsIndeterminate = true;
                        ui_postLoadingBar.Visibility = Visibility.Visible;
                    }
                }

                // Check for no stories
                if ((e.State == CollectorState.Idle || e.State == CollectorState.FullyExtended) && _mPostList.Count == 0 && e.NewPostCount == 0)
                {
                    ui_postNoPostsText.Visibility = Visibility.Visible;
                    ui_postList.Visibility = Visibility.Collapsed;
                }
                else if(e.State == CollectorState.Idle || e.State == CollectorState.FullyExtended)
                {
                    ui_postNoPostsText.Visibility = Visibility.Collapsed;
                    ui_postList.Visibility = Visibility.Visible;
                }
            });
        }

        /// <summary>
        /// Fired when posts are updated
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void PostCollector_OnCollectionUpdated(object sender, CollectionUpdatedArgs<Post> e)
        {
            // Jump to the UI thread
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                // If this is fresh clear
                if(e.IsFreshUpdate)
                {
                    // Remove each individually so we get nice animations
                    while(_mPostList.Count != 0)
                    {
                        _mPostList.RemoveAt(_mPostList.Count - 1);
                    }
                }

                // Add the new posts to the end of the list
                foreach (var p in e.ChangedItems)
                {
                    _mPostList.Add(p);
                }
            });
        }

        /// <summary>
        /// Fired when a post is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PostList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(ui_postList.SelectedIndex == -1)
            {
                return;
            }

            // Get the post
            var tappedPost = (Post)ui_postList.SelectedItem;

            // Navigate to the post
            var args = new Dictionary<string, object>();
            args.Add(PanelManager.NavArgsSubredditName, tappedPost.Subreddit);
            args.Add(PanelManager.NavArgsForcePostId, tappedPost.Id);
            // Make sure the page id is unique
            _mHost.Navigate(typeof(FlipViewPanel), tappedPost.Subreddit + SortTypes.Hot + SortTimeTypes.Week + tappedPost.Id, args);

            // Color the title so the user knows they read this.
            tappedPost.TitleTextColor = Color.FromArgb(255, 152, 152, 152);

            // Reset the selected index
            ui_postList.SelectedIndex = -1;

            TelemetryManager.ReportEvent(this, "UserProfilePostOpened");
        }

        /// <summary>
        /// Fired when the list is scrolled down a ways.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PostList_OnListEndDetectedEvent(object sender, HelperControls.ListEndDetected e)
        {
            if(_mUser == null)
            {
                return;
            }

            EnsurePostCollector();

            // Request a extension.
            _mPostCollector.ExtendCollection();
        }

        /// <summary>
        /// Fired then the post sort is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PostSort_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var textBlock = FindSortText(element);
            if (textBlock != null)
            {
                FlyoutBase.ShowAttachedFlyout(textBlock);
            }
            TelemetryManager.ReportEvent(this, "UserProfilePostSort");
        }

        /// <summary>
        /// Fired when a new sort is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PostSortFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuFlyoutItem)sender;
            var newSort = GetSortFromString(item.Text);

            if(newSort == _mPostSort)
            {
                return;
            }

            // Set the new type
            SetPostSort(newSort);

            // Make a new collector
            EnsurePostCollector(true);

            // And refresh
            _mPostCollector.Update(true);
        }

        /// <summary>
        /// Gets a sort type from string
        /// </summary>
        /// <param name="sort"></param>
        /// <returns></returns>
        private SortTypes GetSortFromString(string sort)
        {
            var text = sort.ToLower();
            switch (text)
            {
                case "new":
                    return SortTypes.New;
                case "controversial":
                    return SortTypes.Controversial;
                case "top":
                    return SortTypes.Top;
                default:
                case "best":
                    return SortTypes.Hot;
            }
        }

        /// <summary>
        /// Sets the current post sort type
        /// </summary>
        /// <param name="type"></param>
        private void SetPostSort(SortTypes type, FrameworkElement parent = null)
        {
            _mPostSort = type;
            var textBlock = FindSortText(parent);
            if(textBlock != null)
            {
                switch (type)
                {
                    case SortTypes.Hot:
                        textBlock.Text = "Best";
                        break;
                    case SortTypes.Controversial:
                        textBlock.Text = "Controversial";
                        break;
                    case SortTypes.New:
                        textBlock.Text = "New";
                        break;
                    case SortTypes.Top:
                        textBlock.Text = "Top";
                        break;
                }
            }
        }

        /// <summary>
        /// Returns a reference to the sort text block.
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        private TextBlock FindSortText(FrameworkElement parent = null)
        {
            // Get if if we don't have it and we can.
            if(_mPostSortText == null && parent != null)
            {
                _mPostSortText = (TextBlock)parent.FindName("ui_postSortText");
            }
            return _mPostSortText;
        }

        #endregion

        #region Comments

        /// <summary>
        /// Ensures a post collector has been made
        /// </summary>
        public void EnsureCommentCollector(bool makeNew = false)
        {
            lock (this)
            {
                if (makeNew && _mCommentCollector != null)
                {
                    _mCommentCollector.OnCollectionUpdated -= CommentCollector_OnCollectionUpdated;
                    _mCommentCollector.OnCollectorStateChange -= CommentCollector_OnCollectorStateChange;
                    _mCommentCollector = null;
                }

                if (_mCommentCollector == null)
                {
                    _mCommentCollector = CommentCollector.GetCollector(_mUser, App.BaconMan, _mCommentSort);
                    _mCommentCollector.OnCollectionUpdated += CommentCollector_OnCollectionUpdated;
                    _mCommentCollector.OnCollectorStateChange += CommentCollector_OnCollectorStateChange;
                }
            }
        }

        /// <summary>
        /// Kicks off a update for users comments
        /// </summary>
        private void GetUserComments()
        {
            if (_mUser == null)
            {
                return;
            }

            EnsureCommentCollector();

            // Update the collector
            _mCommentCollector.Update();
        }

        /// <summary>
        /// Fired when the comment list is loading
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CommentCollector_OnCollectorStateChange(object sender, CollectorStateChangeArgs e)
        {
            // Jump to the UI thread
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                // Kill anything we have
                ui_commentLoadingBar.IsIndeterminate = false;
                ui_commentLoadingBar.Visibility = Visibility.Collapsed;
                ui_commentLoadingRing.IsActive = false;
                ui_commentLoadingRing.Visibility = Visibility.Collapsed;

                // Set the new loading
                if (e.State == CollectorState.Extending || e.State == CollectorState.Updating)
                {
                    if (_mCommentList.Count == 0)
                    {
                        ui_commentLoadingRing.IsActive = true;
                        ui_commentLoadingRing.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ui_commentLoadingBar.IsIndeterminate = true;
                        ui_commentLoadingBar.Visibility = Visibility.Visible;
                    }
                }

                // Check for no comments
                if ((e.State == CollectorState.Idle || e.State == CollectorState.FullyExtended) && _mCommentList.Count == 0 && e.NewPostCount == 0)
                {
                    ui_commentNoPostsText.Visibility = Visibility.Visible;
                    ui_commentList.Visibility = Visibility.Collapsed;
                }
                else if (e.State == CollectorState.Idle || e.State == CollectorState.FullyExtended)
                {
                    ui_commentNoPostsText.Visibility = Visibility.Collapsed;
                    ui_commentList.Visibility = Visibility.Visible;
                }
            });
        }

        /// <summary>
        /// Fired when comments are updated
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CommentCollector_OnCollectionUpdated(object sender, CollectionUpdatedArgs<Comment> e)
        {
            // Jump to the UI thread
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                // If this is fresh clear
                if (e.IsFreshUpdate)
                {
                    // Remove each individually so we get nice animations
                    while (_mCommentList.Count != 0)
                    {
                        _mCommentList.RemoveAt(_mCommentList.Count - 1);
                    }
                }

                // Add the new posts to the end of the list
                foreach (var comment in e.ChangedItems)
                {
                    _mCommentList.Add(comment);
                }
            });
        }

        /// <summary>
        /// Fired when a post is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CommentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ui_commentList.SelectedIndex == -1)
            {
                return;
            }

            // Get the post
            var tappedComment = (Comment)ui_commentList.SelectedItem;

            // Navigate flip view and force it to the post and comment.
            var args = new Dictionary<string, object>();
            args.Add(PanelManager.NavArgsSubredditName, tappedComment.Subreddit);
            args.Add(PanelManager.NavArgsForcePostId, tappedComment.LinkId.Substring(3));
            args.Add(PanelManager.NavArgsForceCommentId, tappedComment.Id);

            // Make sure the page Id is unique
            _mHost.Navigate(typeof(FlipViewPanel), tappedComment.Subreddit + SortTypes.Hot + SortTimeTypes.Week + tappedComment.LinkId + tappedComment.Id, args);

            // Reset the selected index
            ui_commentList.SelectedIndex = -1;

            // Report
            TelemetryManager.ReportEvent(this, "UserProfileCommentOpened");
        }

        /// <summary>
        /// Fired when the list is scrolled down a ways.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CommentList_OnListEndDetectedEvent(object sender, HelperControls.ListEndDetected e)
        {
            if (_mUser == null)
            {
                return;
            }

            EnsureCommentCollector();

            // Request a extension.
            _mCommentCollector.ExtendCollection();
        }

        /// <summary>
        /// Fired when a link is tapped in the comment list
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MarkdownTextBlock_OnMarkdownLinkTapped(object sender, UniversalMarkdown.MarkdownLinkTappedArgs e)
        {
            App.BaconMan.ShowGlobalContent(e.Link);
        }

        /// <summary>
        /// Fired then the post sort is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CommentSort_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var textBlock = FindCommentSortText(element);
            if (textBlock != null)
            {
                FlyoutBase.ShowAttachedFlyout(textBlock);
            }
            TelemetryManager.ReportEvent(this, "UserProfileCommentSort");
        }

        /// <summary>
        /// Fired when a new sort is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CommentSortFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuFlyoutItem)sender;
            var newSort = GetSortFromString(item.Text);

            if (newSort == _mCommentSort)
            {
                return;
            }

            // Set the new type
            SetCommentSort(newSort);

            // Make a new collector
            EnsureCommentCollector(true);

            // And refresh
            _mCommentCollector.Update(true);
        }

        /// <summary>
        /// Sets the current post sort type
        /// </summary>
        /// <param name="type"></param>
        private void SetCommentSort(SortTypes type, FrameworkElement parent = null)
        {
            _mCommentSort = type;
            var textBlock = FindCommentSortText(parent);
            if (textBlock != null)
            {
                switch (type)
                {
                    case SortTypes.Hot:
                        textBlock.Text = "Best";
                        break;
                    case SortTypes.Controversial:
                        textBlock.Text = "Controversial";
                        break;
                    case SortTypes.New:
                        textBlock.Text = "New";
                        break;
                    case SortTypes.Top:
                        textBlock.Text = "Top";
                        break;
                }
            }
        }

        /// <summary>
        /// Returns a reference to the sort text block.
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        private TextBlock FindCommentSortText(FrameworkElement parent = null)
        {
            // Get if if we don't have it and we can.
            if (_mCommentSortText == null && parent != null)
            {
                _mCommentSortText = (TextBlock)parent.FindName("ui_commentSortText");
            }
            return _mCommentSortText;
        }

        #endregion
    }
}
