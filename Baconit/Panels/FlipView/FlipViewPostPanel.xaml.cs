using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using Baconit.ContentPanels;
using Baconit.HelperControls;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using BaconBackend;
using BaconBackend.Managers;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.Panels.FlipView
{
    /// <summary>
    /// Arguments used to request the comment box opens.
    /// </summary>
    public class OpenCommentBox : EventArgs
    {
        public string RedditId;
        public string EditText;
        public object Context;
        public Action<object, CommentBoxOnOpenedArgs> CommentBoxOpened;
        public Func<object, CommentSubmittedArgs, bool> CommentBoxSubmitted;
    }

    public sealed partial class FlipViewPostPanel
    {
        private const int CHiddenCommentHeaderHeight = 36;
        private const int CHiddenShowAllCommentsHeight = 36;

        /// <summary>
        /// Indicates if we have screen mode changed setup.
        /// </summary>
        private bool _isScreenModeChangedSetup;

        /// <summary>
        /// Holds the current comment manager.
        /// </summary>
        private FlipViewPostCommentManager _commentManager;

        /// <summary>
        /// The last known scroll position.
        /// </summary>
        private int _lastKnownScrollOffset;

        /// <summary>
        /// Holds the pro-tip pop up if one exists.
        /// </summary>
        private TipPopUp _commentTipPopUp;

        /// <summary>
        /// Indicates if the header is showing or not.
        /// </summary>
        private bool _isFullscreen;

        /// <summary>
        /// Indicates if the user has overwritten full screen.
        /// </summary>
        private bool? _fullScreenOverwrite;

        /// <summary>
        /// Indicates if the fullness overwrite is from the user.
        /// </summary>
        private bool? _isFullScreenOverWriteUser = true;

        /// <summary>
        /// A grid to hold on to the sticky header.
        /// </summary>
        private Grid _stickyHeader;

        /// <summary>
        /// A grid to hold on to the normal header
        /// </summary>
        private Grid _storyHeader;

        /// <summary>
        /// Fired when the user taps the content load request message.
        /// </summary>
        public event EventHandler<ContentLoadRequestArgs> OnContentLoadRequest
        {
            add => _onContentLoadRequest.Add(value);
            remove => _onContentLoadRequest.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<ContentLoadRequestArgs>> _onContentLoadRequest = new SmartWeakEvent<EventHandler<ContentLoadRequestArgs>>();

        /// <summary>
        /// Fired when the host UI should show a message box.
        /// </summary>
        public event EventHandler<OpenCommentBox> OnOpenCommentBox
        {
            add => _onOpenCommentBox.Add(value);
            remove => _onOpenCommentBox.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<OpenCommentBox>> _onOpenCommentBox = new SmartWeakEvent<EventHandler<OpenCommentBox>>();


        public FlipViewPostPanel()
        {
            InitializeComponent();
        }

        #region IsVisible Logic

        /// <summary>
        /// This it how we get the isvisible from the xmal binding.
        /// </summary>
        public bool IsVisible
        {
            get => (bool)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register(
                "IsVisible",
                typeof(bool),
                typeof(FlipViewPostPanel),
                new PropertyMetadata(false, OnIsVisibleChangedStatic));

        private static void OnIsVisibleChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            OnIsVisibleChanged((bool)e.NewValue);
        }

        /// <summary>
        /// Fired when the OnVisible property changes.
        /// </summary>
        /// <param name="newVis"></param>
        private static void OnIsVisibleChanged(bool newVis)
        {
        }

        #endregion

        #region LoadComments Logic

        /// <summary>
        /// This it how we get the LoadComments from the xmal binding.
        /// </summary>
        public bool LoadComments
        {
            get => (bool)GetValue(LoadCommentsProperty);
            set => SetValue(LoadCommentsProperty, value);
        }

        public static readonly DependencyProperty LoadCommentsProperty =
            DependencyProperty.Register(
                "LoadComments",
                typeof(bool),
                typeof(FlipViewPostPanel),
                new PropertyMetadata(false, OnLoadCommentsChangedStatic));

        private static void OnLoadCommentsChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = (FlipViewPostPanel)d;
            instance?.OnLoadCommentsChanged((bool)e.NewValue);
        }

        /// <summary>
        /// Fired when the LoadComments property changes.
        /// </summary>
        /// <param name="loadComments"></param>
        private void OnLoadCommentsChanged(bool loadComments)
        {
            if (loadComments)
            {
                PreFetchPostComments();
            }
            else
            {
                ClearCommentManger();
            }
        }

        #endregion

        #region Context Logic

        /// <summary>
        /// This it how we get the context from the xmal binding.
        /// </summary>
        public FlipViewPostContext PanelContext
        {
            get => (FlipViewPostContext)GetValue(PanelContextProperty);
            set => SetValue(PanelContextProperty, value);
        }

        public static readonly DependencyProperty PanelContextProperty =
            DependencyProperty.Register(
                "PanelContext",
                typeof(FlipViewPostContext),
                typeof(FlipViewPostPanel),
                new PropertyMetadata(null, OnContextChangedStatic));

        private static void OnContextChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = (FlipViewPostPanel)d;
            instance?.OnContextChanged((FlipViewPostContext)e.NewValue);
        }

        /// <summary>
        /// Fired when the Context property changes.
        /// </summary>
        /// <param name="newContext"></param>
        private void OnContextChanged(FlipViewPostContext newContext)
        {
            // Setup screen mode changing if not already.
            if (!_isScreenModeChangedSetup)
            {
                _isScreenModeChangedSetup = true;
                PanelContext.Host.OnScreenModeChanged += OnScreenModeChanged;
            }

            // When our context changes make sure our comments are reset
            ClearCommentManger();

            // If we should be pre-fetching comments do so now.
            if(LoadComments)
            {
                PreFetchPostComments();
            }

            // If we have a target comment show the UI now.
            if(!string.IsNullOrWhiteSpace(newContext.TargetComment))
            {
                newContext.Post.FlipViewShowEntireThreadMessage = Visibility.Visible;
            }

            // Setup the UI for the new context
            SetupListViewForNewContext();
            SetupHeaderForNewContext();
            SetupScreenModeForNewContext(newContext.Host.CurrentScreenMode());
            ShowCommentScrollTipIfNeeded();
            SetupFullScreenForNewContext();
        }

        /// <summary>
        /// Ensures that we have a good state for this post.
        /// </summary>
        /// <returns></returns>
        private FlipViewPostContext GetContext()
        {
            // Make sure we are good
            if (PanelContext?.Collector != null && PanelContext.Host != null && PanelContext.Post != null)
            {
                return PanelContext;
            }
            return null;
        }

        #endregion

        #region Post Click Actions

        /// <summary>
        /// Fired when a up vote arrow is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UpVote_Tapped(object sender, EventArgs e)
        {
            var context = GetContext();
            context?.Collector.ChangePostVote(context.Post, PostVoteAction.UpVote);
        }

        /// <summary>
        /// Fired when a down vote arrow is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DownVote_Tapped(object sender, EventArgs e)
        {
            var context = GetContext();
            context?.Collector.ChangePostVote(context.Post, PostVoteAction.DownVote);
        }

        private async void OpenBrowser_Tapped(object sender, EventArgs e)
        {
            var context = GetContext();
            if (context != null)
            {
                var url = context.Post.Url;
                if (string.IsNullOrWhiteSpace(url))
                {
                    url = context.Post.Permalink;
                }
                if (!string.IsNullOrWhiteSpace(url))
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(url.UseOldReddit(), UriKind.Absolute));
                    TelemetryManager.ReportEvent(this, "OpenInBrowser");
                }
            }
        }

        private void More_Tapped(object sender, EventArgs e)
        {
            // Show the more menu
            var element = sender as FrameworkElement;
            if (element != null)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
            TelemetryManager.ReportEvent(this, "MoreTapped");
        }

        private void SavePost_Click(object sender, RoutedEventArgs e)
        {
            var context = GetContext();
            if (context != null)
            {
                context.Collector.SaveOrHidePost(context.Post, !context.Post.IsSaved, null);
                TelemetryManager.ReportEvent(this, "SavePostTapped");
            }
        }

        private void HidePost_Click(object sender, RoutedEventArgs e)
        {
            var context = GetContext();
            if (context != null)
            {
                context.Collector.SaveOrHidePost(context.Post, null, !context.Post.IsHidden);
                TelemetryManager.ReportEvent(this, "HidePostTapped");
            }
        }

        private void CopyLink_Click(object sender, RoutedEventArgs e)
        {
            var context = GetContext();
            if (context != null)
            {
                var data = new DataPackage();
                if (string.IsNullOrWhiteSpace(context.Post.Url))
                {
                    data.SetText("http://www.reddit.com" + context.Post.Permalink);
                }
                else
                {
                    data.SetText(context.Post.Url);
                }
                Clipboard.SetContent(data);
                TelemetryManager.ReportEvent(this, "CopyLinkTapped");
            }
        }

        private void CopyPermalink_Click(object sender, RoutedEventArgs e)
        {
            var context = GetContext();
            if (context != null)
            {
                var data = new DataPackage();
                data.SetText("http://www.reddit.com" + context.Post.Permalink);
                Clipboard.SetContent(data);
                TelemetryManager.ReportEvent(this, "CopyLinkTapped");
            }
        }

        private void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            var context = GetContext();
            if (context == null) return;
            if (context.Post.IsGallery)
            {
                foreach (var image in context.Post.MediaImages)
                {
                    App.BaconMan.ImageMan.SaveImageLocally(image.Url);
                }
            }
            else
            {
                App.BaconMan.ImageMan.SaveImageLocally(context.Post.Url);
            }

            TelemetryManager.ReportEvent(this, "CopyLinkTapped");
        }

        // I threw up a little while I wrote this.
        private Post _sharePost;
        private void SharePost_Click(object sender, RoutedEventArgs e)
        {
            var context = GetContext();
            if (context != null)
            {
                if (!string.IsNullOrWhiteSpace(context.Post.Url))
                {
                    _sharePost = context.Post;
                    // Setup the share contract so we can share data
                    var dataTransferManager = DataTransferManager.GetForCurrentView();
                    dataTransferManager.DataRequested += DataTransferManager_DataRequested;
                    DataTransferManager.ShowShareUI();
                    TelemetryManager.ReportEvent(this, "SharePostTapped");
                }
            }
        }

        private void DataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            if (_sharePost != null)
            {
                args.Request.Data.Properties.ApplicationName = "Baconit";
                args.Request.Data.Properties.ContentSourceWebLink = new Uri(_sharePost.Url, UriKind.Absolute);
                args.Request.Data.Properties.Title = "A Reddit Post Shared From Baconit";
                args.Request.Data.Properties.Description = _sharePost.Title;
                args.Request.Data.SetText($"\r\n\r\n{_sharePost.Title}\r\n\r\n{_sharePost.Url}");
                _sharePost = null;
                TelemetryManager.ReportEvent(this, "PostShared");
            }
            else
            {
                args.Request.FailWithDisplayText("Baconit doesn't have anything to share!");
                TelemetryManager.ReportUnexpectedEvent(this, "FailedToShareFilpViewPostNoSharePost");
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
            var context = GetContext();
            context?.Host.ToggleMenu(true);
        }

        /// <summary>
        /// Fired when the user taps delete post.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void DeletePost_Click(object sender, RoutedEventArgs e)
        {
            var context = GetContext();
            if (context != null)
            {
                // Confirm
                var doIt = await App.BaconMan.MessageMan.ShowYesNoMessage("Delete Post", "Are you sure you want to?");

                if (doIt.HasValue && doIt.Value)
                {
                    // Delete it.
                    context.Collector.DeletePost(context.Post);
                }
            }
        }

        /// <summary>
        /// Fired when the user taps edit post.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EditPost_Click(object sender, RoutedEventArgs e)
        {
            var context = GetContext();
            if (context != null)
            {
                ShowCommentBox("t3_" + context.Post.Id, context.Post.Selftext, context.Post);
            }
        }

        /// <summary>
        /// Called when the user wants to comment on the post
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PostCommentOn_OnIconTapped(object sender, EventArgs e)
        {
            var context = GetContext();
            if (context != null)
            {
                ShowCommentBox("t3_" + context.Post.Id, null, context.Post);
            }
        }

        /// <summary>
        /// Fired when the user taps the go to subreddit button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GoToSubreddit_Click(object sender, RoutedEventArgs e)
        {
            var context = GetContext();
            if (context != null)
            {
                // Navigate to the subreddit.
                var args = new Dictionary<string, object>();
                args.Add(PanelManager.NavArgsSubredditName, context.Post.Subreddit);
                context.Host.Navigate(typeof(SubredditPanel), context.Post.Subreddit + SortTypes.Hot + SortTimeTypes.Week, args);
                TelemetryManager.ReportEvent(this, "GoToSubredditFlipView");
            }
        }

        /// <summary>
        /// Fired when the user taps the go to user button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GoToUser_Click(object sender, RoutedEventArgs e)
        {
            var context = GetContext();
            if (context != null)
            {
                // Navigate to the user.
                var args = new Dictionary<string, object>();
                args.Add(PanelManager.NavArgsUserName, context.Post.Author);
                context.Host.Navigate(typeof(UserProfile), context.Post.Author, args);
                TelemetryManager.ReportEvent(this, "GoToUserFlipView");
            }
        }

        #endregion

        #region List Scrolling Logic

        private void SetupListViewForNewContext()
        {
            // Hide the scroll bar.
            ScrollViewer.SetVerticalScrollBarVisibility(ui_listView, ScrollBarVisibility.Hidden);
            _lastKnownScrollOffset = 0;
        }

        /// <summary>
        /// Creates a comment manager for the post and pre-fetches the comments
        /// </summary>
        /// <param name="forcePreFetch"></param>
        /// <param name="showThreadSubset"></param>
        private void PreFetchPostComments(bool forcePreFetch = false, bool showThreadSubset = true)
        {
            // Make sure we aren't already ready.
            if(_commentManager != null)
            {
                return;
            }

            var context = GetContext();
            if(context == null)
            {
                return;
            }

            // Create a comment manager for the post
            var refPost = context.Post;
            _commentManager = new FlipViewPostCommentManager(ref refPost, context.TargetComment, showThreadSubset);

            // Set the comment list to the list view
            ui_listView.ItemsSource = _commentManager.Comments;

            // If the user wanted, kick off pre load of comments.
            if (forcePreFetch || App.BaconMan.UiSettingsMan.FlipViewPreloadComments)
            {
                _commentManager.PreFetchComments();
            }
        }

        /// <summary>
        /// Will remove any comment managers that may exist for a post, and clears the comments.
        /// </summary>
        private void ClearCommentManger()
        {
            if (_commentManager == null) return;
            _commentManager.PrepareForDeletion();
            _commentManager = null;
        }

        /// <summary>
        /// Returns the comment manger if we have one.
        /// </summary>
        /// <returns></returns>
        private FlipViewPostCommentManager GetCommentManger()
        {
            return _commentManager;
        }

        /// <summary>
        /// Fired when a user tap the header to view an entire thread instead of just the
        /// context of a message
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ViewEntireThread_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // First remove the current comment manager
            ClearCommentManger();

            // Now make a new one without using the subset
            PreFetchPostComments(true, false);

            // Update the header sizes to fix the UI
            SetHeaderSize();
        }

        /// <summary>
        /// Fired when one of the lists are scrolling to the bottom.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void List_OnListEndDetectedEvent(object sender, ListEndDetected e)
        {
            // NOTE!!!
            // This is a very hot code path, so anything done here should be really quick!

            var context = GetContext();
            if(context == null)
            {
                return;
            }

            // Make sure the margin is normal.
            if (Math.Abs(ui_stickyHeader.Margin.Top) > 1)
            {
                // If the margin is not 0 we are playing our render tick. If the sticky header
                // is always set to collapsed it actually never loads or render until we set it to
                // visible while we are scrolling which makes it 'pop' in with a delay. To avoid this
                // it defaults visible but way off the screen, this makes it load and render so it is ready
                // when we set it visible. Here is is safe to restore it to the normal state.
                ui_stickyHeader.Visibility = Visibility.Collapsed;
                ui_stickyHeader.Margin = new Thickness(0, 0, 0, 0);
            }

            // Show or hide the scroll bar depending if we have gotten to comments yet or not.
            ScrollViewer.SetVerticalScrollBarVisibility(ui_listView, e.ListScrollTotalDistance > 60 ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden);

            // Find the header size for this post
            var currentScrollAera = GetCurrentScrollArea();

            // This will return -1 if we can't get this yet, so just get out of here.
            if (currentScrollAera == -1)
            {
                return;
            }

            // Use the header size and the control size to figure out if we should show the static header.
            var showHeader = e.ListScrollTotalDistance > currentScrollAera - ui_stickyHeader.ActualHeight;
            ui_stickyHeader.Visibility = showHeader ? Visibility.Visible : Visibility.Collapsed;

            // Get the distance for the animation header to move. We need to account for the header
            // size here because when we move from full screen to not it will toggle. This is very touchy, we 
            // also only want to do this logic if we are scrolling down. On the way back up we need to unminimize 
            // (if the user forced us to be mini) before we hit the real header or things will be wack.
            var headerAnimationDistance = currentScrollAera;
            if(_isFullscreen && e.ScrollDirection != ScrollDirection.Up)
            {
                var headerGrid = (Grid)_storyHeader.FindName("ui_storyHeaderBlock");
                if (headerGrid != null) headerAnimationDistance -= (int) headerGrid.ActualHeight;
            }

            // If we are far enough to also hide the header consider hiding it.
            if (e.ListScrollTotalDistance > headerAnimationDistance)
            {
                if (App.BaconMan.UiSettingsMan.FlipViewMinimizeStoryHeader)
                {
                    switch (e.ScrollDirection)
                    {
                        case ScrollDirection.Down:
                            ToggleFullscreen(true);
                            break;
                        case ScrollDirection.Up:
                            ToggleFullscreen(false);
                            break;
                        case ScrollDirection.Null:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
            else
            {
                // If we are the top force it.
                ToggleFullscreen(false, true);
            }


            // If we have a differed header update and we are near the top (not showing the header)
            // do the update now. For a full story see the comment on m_hasDeferredHeaderSizeUpdate
            if (currentScrollAera != context.HeaderSize && context.HeaderSize + 80 > e.ListScrollTotalDistance && e.ScrollDirection == ScrollDirection.Up)
            {
                SetHeaderSize();
            }

            //// Update the last known scroll pos
            _lastKnownScrollOffset = (int)e.ListScrollTotalDistance;

            //// Hide the tip box if needed.
            HideCommentScrollTipIfNeeded();

            // If we have a manager request more posts.
            if (_commentManager == null) return;
            if (_commentManager.IsOnlyShowingSubset())
            {
                _commentManager.RequestMorePosts();
            }
        }

        /// <summary>
        /// Fired when a item is selected, just clear it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EndDetectingListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is EndDetectingListView listView) listView.SelectedIndex = -1;
        }

        /// <summary>
        /// Scrolls to the top of the list view for a post.
        /// </summary>
        private void ScrollCommentsToTop()
        {
            ui_listView.ScrollIntoView(null);
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
            var manager = GetCommentManger();
            if (manager != null)
            {
                _commentManager.Refresh();
            }
        }

        private void CommentUp_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // We don't need to animate here, the vote will do it for us
            var comment = (sender as FrameworkElement)?.DataContext as Comment;
            var manager = GetCommentManger();
            manager?.OnUpVoteTapped(comment);
        }

        private void CommentDown_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // We don't need to animate here, the vote will do it for us
            var comment = (sender as FrameworkElement)?.DataContext as Comment;
            var manager = GetCommentManger();
            manager?.OnDownVoteTapped(comment);
        }

        private void CommentButton3_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Animate the text
            AnimateText((FrameworkElement)sender);

            // Get the comment
            var comment = ((FrameworkElement) sender)?.DataContext as Comment;

            if (comment != null && comment.IsDeleted)
            {
                App.BaconMan.MessageMan.ShowMessageSimple("LET IT GO!", "You can't edit a deleted comment!");
                return;
            }

            if (comment != null && comment.IsCommentOwnedByUser)
            {
                // Edit
                ShowCommentBox("t1_" + comment.Id, comment.Body, comment);
            }
            else
            {
                // Reply
                if (comment != null) ShowCommentBox("t1_" + comment.Id, null, comment);
            }
        }

        private async void CommentButton4_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Animate the text
            AnimateText((FrameworkElement)sender);

            // Get the comment
            var comment = ((FrameworkElement) sender)?.DataContext as Comment;

            if (comment != null && comment.IsCommentOwnedByUser)
            {
                // Delete the comment
                var response = await App.BaconMan.MessageMan.ShowYesNoMessage("Delete Comment", "Are you sure?");

                if (!response.HasValue || !response.Value) return;
                // Find the manager
                var manager = GetCommentManger();
                manager?.CommentDeleteRequest(comment);
            }
            else
            {
                var context = GetContext();
                if(context == null)
                {
                    return;
                }

                // Navigate to the user
                if (comment != null)
                {
                    var args = new Dictionary<string, object> {{PanelManager.NavArgsUserName, comment.Author}};
                    context.Host.Navigate(typeof(UserProfile), comment.Author, args);
                }

                TelemetryManager.ReportEvent(this, "GoToUserFromComment");
            }
        }

        private void CommentMore_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Animate the text
            AnimateText((FrameworkElement)sender);

            // Show the more menu
            var element = (FrameworkElement) sender;
            if (element != null)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }

            TelemetryManager.ReportEvent(this, "CommentMoreTapped");
        }

        private void CommentSave_Click(object sender, RoutedEventArgs e)
        {
            var comment = (sender as FrameworkElement)?.DataContext as Comment;
            var manager = GetCommentManger();
            FlipViewPostCommentManager.OnSaveTapped(comment);
            TelemetryManager.ReportEvent(this, "CommentSaveTapped");
        }

        private void CommentShare_Click(object sender, RoutedEventArgs e)
        {
            var comment = (sender as FrameworkElement)?.DataContext as Comment;
            var manager = GetCommentManger();
            manager?.OnShareTapped(comment);
            TelemetryManager.ReportEvent(this, "CommentShareTapped");
        }

        private void CommentPermalink_Click(object sender, RoutedEventArgs e)
        {
            var comment = (sender as FrameworkElement)?.DataContext as Comment;
            var manager = GetCommentManger();
            manager?.OnCopyPermalinkTapped(comment);
            TelemetryManager.ReportEvent(this, "CommentPermalinkTapped");
        }

        private void CommentCollapse_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Animate the text
            AnimateText((FrameworkElement)sender);

            var comment = ((FrameworkElement) sender)?.DataContext as Comment;
            var manager = GetCommentManger();
            manager?.OnCollapseTapped(comment);
        }

        private void CollapsedComment_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var comment = (sender as FrameworkElement)?.DataContext as Comment;
            var manager = GetCommentManger();
            manager?.OnExpandTapped(comment);
        }

        /// <summary>
        /// Does the text animation on the text box in the Grid.
        /// Note!! We do this crazy animation stuff so the UI feel responsive.
        /// It would be ideal to use the SimpleButtonText control but it is too expensive for the virtual list.
        /// </summary>
        /// <param name="textBlockContainer"></param>
        private static void AnimateText(DependencyObject textBlockContainer)
        {
            // Make sure it has children
            if (VisualTreeHelper.GetChildrenCount(textBlockContainer) != 1)
            {
                return;
            }

            // Try to get the text block
            var textBlock = (TextBlock)VisualTreeHelper.GetChild(textBlockContainer, 0);

            // Return if failed.
            if (textBlock == null)
            {
                return;
            }

            // Make a storyboard
            var storyboard = new Storyboard();
            var colorAnimation = new ColorAnimation();
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
        /// Fired when a user taps a link in a comment
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MarkdownTextBlock_OnMarkdownLinkTapped(object sender, UniversalMarkdown.MarkdownLinkTappedArgs e)
        {
            App.BaconMan.ShowGlobalContent(e.Link);
        }

        /// <summary>
        /// Fired when a user clicks the "zoom in" menu entry
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            App.BaconMan.UiSettingsMan.PostViewMarkdownFontSize++;
        }

        /// <summary>
        /// Fired when a user clicks the "zoom out" menu entry
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            App.BaconMan.UiSettingsMan.PostViewMarkdownFontSize--;
        }

        /// <summary>
        /// Fired when a user clicks the "reset zoom" menu entry
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ZoomReset_Click(object sender, RoutedEventArgs e)
        {
            App.BaconMan.UiSettingsMan.PostViewMarkdownFontSize = 14;
        }

        #endregion

        #region Full Screen Logic

        /// <summary>
        /// Gets full screen ready for a new context
        /// </summary>
        private void SetupFullScreenForNewContext()
        {
            _fullScreenOverwrite = null;
            _isFullScreenOverWriteUser = null;
            ToggleFullscreen(false, true);

            var headerGrid = (Grid) _storyHeader?.FindName("ui_storyHeaderBlock");
            if (headerGrid != null) headerGrid.MaxHeight = double.PositiveInfinity;
        }

        /// <summary>
        /// Fired when the control wants to go full screen.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentPanelHost_OnToggleFullscreen(object sender, ToggleFullScreenEventArgs e)
        {
            // Set the overwrite
            if(e.GoFullScreen)
            {
                // Scroll the header into view
                ScrollCommentsToTop();

                // Set the overwrite
                _fullScreenOverwrite = true;
                _isFullScreenOverWriteUser = false;
            }
            else
            {
                // Disable the overwrite
                _isFullScreenOverWriteUser = null;
                _fullScreenOverwrite = null;
            }

            // Toggle full screen.
            ToggleFullscreen(e.GoFullScreen);
        }

        /// <summary>
        /// Fired when the post header toggle is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PostHeaderToggle_Click(object sender, RoutedEventArgs e)
        {
            // Set the user overwrite, only overwrite the hide.
            if(!_isFullscreen)
            {
                _fullScreenOverwrite = true;
                _isFullScreenOverWriteUser = true;
            }
            else
            {
                _fullScreenOverwrite = null;
                _isFullScreenOverWriteUser = null;
            }

            ToggleFullscreen(!_isFullscreen);
        }

        /// <summary>
        /// Given a post toggles the header
        /// </summary>
        /// <param name="goFullscreen"></param>
        /// <param name="force"></param>
        private void ToggleFullscreen(bool goFullscreen, bool force = false)
        {
            // If we are already there don't do anything.
            if(_isFullscreen == goFullscreen)
            {
                return;
            }

            // Make sure the user hasn't overwritten this.
            var localOverwriteValue = _fullScreenOverwrite;
            if (localOverwriteValue.HasValue)
            {
                // If we are being force and the overwrite isn't from the user skip this.
                if(!force || !localOverwriteValue.Value)
                {
                    if (localOverwriteValue.Value != goFullscreen)
                    {
                        return;
                    }
                }
            }
            _isFullscreen = goFullscreen;

            var traceString = "";
            try
            {
                if (IsVisible)
                {
                    // Get our elements for the sticky header
                    var storyboard = (Storyboard)_stickyHeader.FindName("ui_storyCollapseHeader");
                    var animBody = (DoubleAnimation)_stickyHeader.FindName("ui_animHeaderBodyTranslate");
                    var animTitle = (DoubleAnimation)_stickyHeader.FindName("ui_animHeaderTitleTranslate");
                    //var animSubtext = (DoubleAnimation)_stickyHeader.FindName("ui_animHeaderSubtextTranslate");
                    var animIcons = (DoubleAnimation)_stickyHeader.FindName("ui_animHeaderIconsTranslate");
                    var animFullscreenButton = (DoubleAnimation)_stickyHeader.FindName("ui_animHeaderFullscreenButtonRotate");
                    var stickyGrid = (Grid)_stickyHeader.FindName("ui_storyHeaderBlock");

                    traceString += " gotstickelement ";

                    // Stop any past animations.
                    if (storyboard != null && storyboard.GetCurrentState() != ClockState.Stopped)
                    {
                        storyboard.Stop();
                    }

                    if (stickyGrid != null)
                    {
                        traceString += $" settingStickAnim height [{stickyGrid.ActualHeight}[ ";

                        // Setup the animations.
                        var animTo = goFullscreen ? -stickyGrid.ActualHeight : 0;
                        var animFrom = goFullscreen ? 0 : -stickyGrid.ActualHeight;
                        if (animBody != null)
                        {
                            animBody.To = animTo;
                            animBody.From = animFrom;
                        }

                        if (animTitle != null)
                        {
                            animTitle.To = animTo;
                            animTitle.From = animFrom;
                        }

                        //if (animSubtext != null)
                        //{
                        //    animSubtext.To = animTo;
                        //    animSubtext.From = animFrom;
                        //}

                        if (animIcons != null)
                        {
                            animIcons.To = animTo;
                            animIcons.From = animFrom;
                        }
                    }

                    if (animFullscreenButton != null)
                    {
                        animFullscreenButton.To = goFullscreen ? 0 : 180;
                        animFullscreenButton.From = goFullscreen ? 180 : 0;
                    }

                    traceString += " gettingnormalHeader ";

                    // For the normal header
                    var storyNormal = (Storyboard)_storyHeader.FindName("ui_storyCollapseHeaderHeight");
                    var headerGrid = (Grid)_storyHeader.FindName("ui_storyHeaderBlock");
                    var animNormal = (DoubleAnimation)_storyHeader.FindName("ui_animHeaderHeightCollapse");
                    var animNormalFullscreenButton = (DoubleAnimation)_storyHeader.FindName("ui_animHeaderHeightButtonRotate");

                    traceString += " stoppingclock ";

                    // Stop any past animations.
                    if (storyNormal.GetCurrentState() != ClockState.Stopped)
                    {
                        storyNormal.Stop();
                    }

                    traceString += " settingnormalheaders ";

                    // Set the normal animations.
                    animNormal.To = goFullscreen ? 0 : headerGrid.ActualHeight;
                    animNormal.From = goFullscreen ? headerGrid.ActualHeight : 0;
                    animNormalFullscreenButton.To = goFullscreen ? 0 : 180;
                    animNormalFullscreenButton.From = goFullscreen ? 180 : 0;

                    traceString += " play ";


                    // Play the animations.
                    storyboard.Begin();
                    storyNormal.Begin();
                }
                else
                {
                    // If not visible, just reset the UI.

                    traceString += " gettingElements ";

                    // For the normal header set the size and the button
                    var headerGrid = (Grid)_storyHeader.FindName("ui_storyHeaderBlock");
                    var headerFullscreenButtonRotate = (RotateTransform)_storyHeader.FindName("ui_headerFullscreenButtonRotate");
                    if (headerFullscreenButtonRotate != null)
                        headerFullscreenButtonRotate.Angle = goFullscreen ? 0 : 180;
                    if (headerGrid != null)
                    {
                        headerGrid.MaxHeight = goFullscreen ? double.NaN : headerGrid.ActualHeight;

                        traceString += $" SettingElements height[{headerGrid.ActualHeight}] ";
                    }

                    // For the sticky header reset the transforms.
                    var titleTrans = (TranslateTransform)_storyHeader.FindName("ui_headerTitleTransform");
                    //var subtextTrans = (TranslateTransform)_storyHeader.FindName("ui_headerSubtextTransform");
                    var iconTrans = (TranslateTransform)_storyHeader.FindName("ui_headerIconsTransform");
                    var bodyTrans = (TranslateTransform)_storyHeader.FindName("ui_headerBodyTransform");
                    var stickyGrid = (Grid)_stickyHeader.FindName("ui_storyHeaderBlock");
                    if (stickyGrid == null) return;
                    var setTo = goFullscreen ? -stickyGrid.ActualHeight : 0;
                    if (titleTrans != null) titleTrans.Y = setTo;
                    //if (subtextTrans != null) subtextTrans.Y = setTo;
                    if (iconTrans != null) iconTrans.Y = setTo;
                    if (bodyTrans != null) bodyTrans.Y = setTo;
                }
            }
            catch(Exception e)
            {
                TelemetryManager.ReportUnexpectedEvent(this, $"FullscreenToggleFailed IsVis:{IsVisible}, gofull:{goFullscreen}, trace string [{traceString}]", e);
                App.BaconMan.MessageMan.DebugDia($"FullscreenToggleFailed IsVis:{IsVisible}, gofull:{goFullscreen}, trace string [{traceString}]", e);
            }
        }

        /// <summary>
        /// Fired when a header loads.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StoryHeader_Loaded(object sender, RoutedEventArgs e)
        {
            // The normal header will always load first.
            if(_storyHeader == null)
            {
                _storyHeader = (Grid)sender;
            }
            else
            {
                _stickyHeader = (Grid)sender;
            }
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
            var element = sender as FrameworkElement;
            if (element != null)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
            TelemetryManager.ReportEvent(this, "CommentSortTapped");
        }

        /// <summary>
        /// Fired when a user taps a new sort type for comments.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CommentSortMenu_Click(object sender, RoutedEventArgs e)
        {
            var context = GetContext();
            if(context == null)
            {
                return;
            }

            // Update sort type
            if (sender is MenuFlyoutItem item) context.Post.CommentSortType = GetCommentSortFromString(item.Text);

            // Get the collector and update the sort
            var commentManager = GetCommentManger();
            commentManager.ChangeCommentSort();
        }

        private static CommentSortTypes GetCommentSortFromString(string typeString)
        {
            typeString = typeString.ToLower();
            switch (typeString)
            {
                default:
                    return CommentSortTypes.Best;
                case "controversial":
                    return CommentSortTypes.Controversial;
                case "new":
                    return CommentSortTypes.New;
                case "old":
                    return CommentSortTypes.Old;
                case "q&a":
                    return CommentSortTypes.Qa;
                case "top":
                    return CommentSortTypes.Top;
            }
        }

        private void CommentShowingCountMenu_Click(object sender, RoutedEventArgs e)
        {
            var context = GetContext();
            if (context == null)
            {
                return;
            }

            // Parse the new comment count
            if (sender is MenuFlyoutItem item) context.Post.CurrentCommentShowingCount = int.Parse(item.Text);

            // Get the collector and update the sort
            var commentManager = GetCommentManger();
            commentManager.UpdateShowingCommentCount(context.Post.CurrentCommentShowingCount);
        }

        #endregion

        #region Screen Mode

        /// <summary>
        /// Fired when the screen mode changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnScreenModeChanged(object sender, ScreenModeChangedArgs e)
        {
            SetupScreenModeForNewContext(e.NewScreenMode);
        }

        /// <summary>
        /// Sets the menu icon correctly.
        /// </summary>
        /// <param name="newMode"></param>
        private void SetupScreenModeForNewContext(ScreenMode newMode)
        {
            var context = GetContext();
            if(context == null)
            {
                return;
            }
            context.PostMenuIconVisibility = newMode == ScreenMode.Single ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Header Logic

        /// <summary>
        /// Sets up the header for the new context
        /// </summary>
        private void SetupHeaderForNewContext()
        {
            // If we have not done our trick yet, don't collapse it.
            ui_stickyHeader.Visibility = Math.Abs(ui_stickyHeader.Margin.Top) > 1 ? Visibility.Visible : Visibility.Collapsed;

            // Set the header size for this.
            SetHeaderSize();
        }

        /// <summary>
        /// Updates the header sizes for the flip post so they fit perfectly on the screen.
        /// </summary>
        private void SetHeaderSize()
        {
            // Get the screen size, account for the comment box if it is open.
            var currentScrollArea = GetCurrentScrollArea();

            // This will return -1 if we can't get this number yet.
            if (currentScrollArea == -1)
            {
                return;
            }

            var context = GetContext();
            context.HeaderSize = currentScrollArea;
        }

        /// <summary>
        /// Returns the current space that's available for the flipview scroller
        /// </summary>
        /// <returns></returns>
        private int GetCurrentScrollArea()
        {
            // Get the control size
            var screenSize = (int)ui_contentRoot.ActualHeight;

            // Make sure we are ready.
            if (Math.Abs(ui_contentRoot.ActualHeight) < 1)
            {
                // If not return -1.
                return -1;
            }

            var context = GetContext();
            if(context == null)
            {
                return -1;
            }

            // If we are showing the show all comments header add the height of that so it won't be on the screen by default
            if (context.Post.FlipViewShowEntireThreadMessage == Visibility.Visible)
            {
                screenSize += CHiddenShowAllCommentsHeight;
            }

            // Add a the comment header size.
            screenSize += CHiddenCommentHeaderHeight;

            // If we are full screen account for the header being gone.
            if (!_isFullscreen) return screenSize;
            var headerGrid = (Grid)_storyHeader.FindName("ui_storyHeaderBlock");
            if (headerGrid != null) screenSize += (int) headerGrid.ActualHeight;

            return screenSize;
        }

        /// <summary>
        /// Fired when the content root changes sizes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // We do this so we jump the UI ever time the comment box is open.
            // see the description on m_hasDeferredHeaderSizeUpdate for a full story.
            // This isn't 100% correct, but we are looking for comment box changes.
            // So if the width doesn't change assume it is the comment box.
            var currentScrollArea = GetCurrentScrollArea();
            if (_lastKnownScrollOffset < currentScrollArea)
            {
                // Fire a header size change to fix them up.
                SetHeaderSize();
            }
        }

        #endregion

        #region Comment Box

        /// <summary>
        /// Shows the comment box
        /// </summary>
        private void ShowCommentBox(string redditId, string editText, object context)
        {
            var args = new OpenCommentBox
            {
                Context = context,
                RedditId = redditId,
                EditText = editText,
                CommentBoxOpened = CommentBox_OnBoxOpened,
                CommentBoxSubmitted = CommentBox_OnCommentSubmitted
            };
            _onOpenCommentBox.Raise(this, args);
        }

        /// <summary>
        /// Fired when the comment box is done opening.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void CommentBox_OnBoxOpened(object sender, CommentBoxOnOpenedArgs e)
        {
            // We want to scroll the comment we are working off of into view.
            if (e.RedditId.StartsWith("t1_"))
            {
                var comment = (Comment)e.Context;
                ui_listView.ScrollIntoView(comment);
            }
        }

        /// <summary>
        /// Fired when the comment in the comment box was submitted
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private bool CommentBox_OnCommentSubmitted(object sender, CommentSubmittedArgs e)
        {
            var wasActionSuccessful = false;

            var context = GetContext();
            if(context == null)
            {
                return false;
            }

            if (e.RedditId.StartsWith("t3_"))
            {
                var post = (Post)e.Context;

                if (post != null)
                {
                    if (e.IsEdit)
                    {
                        // We edited selftext
                        wasActionSuccessful = context.Collector.EditSelfPost(post, e.Response);

                        if (!wasActionSuccessful) return false;
                        // If we are successful to update the UI we will remove the post
                        // and re-allow it.
                        ContentPanelMaster.Current.RemoveAllowedContent(post.Id);

                        // Now ask the host to reallow it.
                        _onContentLoadRequest.Raise(this, new ContentLoadRequestArgs { SourceId = post.Id });
                    }
                    else
                    {
                        // We added a new comment
                        var manager = GetCommentManger();
                        if (manager != null)
                        {
                            wasActionSuccessful = manager.CommentAddedOrEdited("t3_" + post.Id, e);
                        }
                        else
                        {
                            TelemetryManager.ReportUnexpectedEvent(this, "CommentSubmitManagerObjNull");
                        }
                    }
                }
                else
                {
                    TelemetryManager.ReportUnexpectedEvent(this, "CommentSubmitPostObjNull");
                }
            }
            else if (e.RedditId.StartsWith("t1_"))
            {
                var comment = (Comment)e.Context;
                if (comment != null)
                {
                    // Comment added or edited.
                    var manager = GetCommentManger();
                    if (manager != null)
                    {
                        wasActionSuccessful = manager.CommentAddedOrEdited("t1_" + comment.Id, e);
                    }
                    else
                    {
                        TelemetryManager.ReportUnexpectedEvent(this, "CommentSubmitManagerObjNull");
                    }
                }
                else
                {
                    TelemetryManager.ReportUnexpectedEvent(this, "CommentSubmitCommentObjNull");
                }
            }

            return wasActionSuccessful;
        }

        #endregion

        #region Pro Tip Logic

        /// <summary>
        /// Shows the comment scroll tip if needed
        /// </summary>
        private void ShowCommentScrollTipIfNeeded()
        {
            if (!App.BaconMan.UiSettingsMan.FlipViewShowCommentScrollTip)
            {
                return;
            }

            // Never show it again.
            App.BaconMan.UiSettingsMan.FlipViewShowCommentScrollTip = false;

            // Create the tip UI, add it to the UI and show it.
            _commentTipPopUp = new TipPopUp
            {
                Margin = new Thickness(0, 0, 0, 90), VerticalAlignment = VerticalAlignment.Bottom
            };
            _commentTipPopUp.OnTipHideComplete += CommentTipPopUp_TipHideComplete;
            ui_contentRoot.Children.Add(_commentTipPopUp);
            _commentTipPopUp.ShowTip();
        }

        /// <summary>
        /// Hides the comment scroll tip if needed.
        /// </summary>
        private void HideCommentScrollTipIfNeeded()
        {
            // If we don't have one return.
            if (_commentTipPopUp == null)
            {
                return;
            }

            // Tell it to hide and set it to null
            _commentTipPopUp.HideTip();
            _commentTipPopUp = null;
        }

        /// <summary>
        /// Fired when the hide is complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CommentTipPopUp_TipHideComplete(object sender, EventArgs e)
        {
            var popUp = (TipPopUp)sender;

            // Un-register the event
            popUp.OnTipHideComplete += CommentTipPopUp_TipHideComplete;

            // Remove the tip from the UI
            ui_contentRoot.Children.Remove(popUp);
        }

        #endregion

        /// <summary>
        /// Fired when the user has tapped the panel requesting the content to load.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentPanelHost_OnContentLoadRequest(object sender, ContentLoadRequestArgs e)
        {
            // Forward the call along.
            _onContentLoadRequest.Raise(this, e);
        }

        private void OnUserTapped(object sender, TappedRoutedEventArgs e)
        {
            var context = GetContext();
            if (context == null) return;

            var args = new Dictionary<string, object> {{PanelManager.NavArgsUserName, context.Post.Author}};
            context.Host.Navigate(typeof(UserProfile), context.Post.Author, args);
        }

        private void OnSubredditTapped(object sender, TappedRoutedEventArgs e)
        {
            var context = GetContext();
            if (context == null) return;

            var args = new Dictionary<string, object> {{PanelManager.NavArgsSubredditName, context.Post.Subreddit}};
            context.Host.Navigate(typeof(SubredditPanel), context.Post.Subreddit + SortTypes.Hot + SortTimeTypes.Week, args);
        }
    }
}
