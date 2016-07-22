using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using Baconit.ContentPanels;
using Baconit.HelperControls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.Panels.FlipView
{
    /// <summary>
    /// Arguments used to request the comment box opens.
    /// </summary>
    public class OnOpenCommentBox : EventArgs
    {
        public string RedditId;
        public string EditText;
        public object Context;
        public Action<object, CommentBoxOnOpenedArgs> CommentBoxOpened;
        public Func<object, OnCommentSubmittedArgs, bool> CommentBoxSubmitted;
    }

    public sealed partial class FlipViewPostPanel : UserControl
    {
        const int c_hiddenCommentHeaderHeight = 36;
        const int c_hiddenShowAllCommentsHeight = 36;

        /// <summary>
        /// Indicates if we have screen mode changed setup.
        /// </summary>
        bool m_isScrrenModeChangedSetup = false;

        /// <summary>
        /// Holds the current comment manager.
        /// </summary>
        FlipViewPostCommentManager m_commentManager = null;

        /// <summary>
        /// The last known scroll position.
        /// </summary>
        int m_lastKnownScrollOffset = 0;

        /// <summary>
        /// Holds the protip pop up if one exists.
        /// </summary>
        TipPopUp m_commentTipPopUp = null;

        /// <summary>
        /// Indicates if the header is showing or not.
        /// </summary>
        bool m_isFullscreen = false;

        /// <summary>
        /// Indicates if the user has overwritten full screen.
        /// </summary>
        bool? m_fullScreenOverwrite = null;

        /// <summary>
        /// Indicates if the fullnesses overwrite is from the user.
        /// </summary>
        bool? m_isfullScreenOverwriteUser = true;

        /// <summary>
        /// A grid to hold on to the sticky header.
        /// </summary>
        Grid m_stickyHeader;

        /// <summary>
        /// A grid to hold on to the normal header
        /// </summary>
        Grid m_storyHeader;

        /// <summary>
        /// Fired when the user taps the content load request message.
        /// </summary>
        public event EventHandler<OnContentLoadRequestArgs> OnContentLoadRequest
        {
            add { m_onContentLoadRequest.Add(value); }
            remove { m_onContentLoadRequest.Remove(value); }
        }
        SmartWeakEvent<EventHandler<OnContentLoadRequestArgs>> m_onContentLoadRequest = new SmartWeakEvent<EventHandler<OnContentLoadRequestArgs>>();

        /// <summary>
        /// Fired when the host UI should show a message box.
        /// </summary>
        public event EventHandler<OnOpenCommentBox> OnOpenCommentBox
        {
            add { m_onOpenCommentBox.Add(value); }
            remove { m_onOpenCommentBox.Remove(value); }
        }
        SmartWeakEvent<EventHandler<OnOpenCommentBox>> m_onOpenCommentBox = new SmartWeakEvent<EventHandler<OnOpenCommentBox>>();


        public FlipViewPostPanel()
        {
            this.InitializeComponent();
        }

        #region IsVisible Logic

        /// <summary>
        /// This it how we get the isvisible from the xmal binding.
        /// </summary>
        public bool IsVisible
        {
            get { return (bool)GetValue(IsVisibleProperty); }
            set { SetValue(IsVisibleProperty, value); }
        }

        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register(
                "IsVisible",
                typeof(bool),
                typeof(FlipViewPostPanel),
                new PropertyMetadata(false, new PropertyChangedCallback(OnIsVisibleChangedStatic)));

        private static void OnIsVisibleChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = (FlipViewPostPanel)d;
            if (instance != null)
            {
                instance.OnIsVisibleChanged((bool)e.NewValue);
            }
        }

        /// <summary>
        /// Fired when the OnVisible property changes.
        /// </summary>
        /// <param name="newVis"></param>
        private void OnIsVisibleChanged(bool newVis)
        {
        }

        #endregion

        #region LoadComments Logic

        /// <summary>
        /// This it how we get the LoadComments from the xmal binding.
        /// </summary>
        public bool LoadComments
        {
            get { return (bool)GetValue(LoadCommentsProperty); }
            set { SetValue(LoadCommentsProperty, value); }
        }

        public static readonly DependencyProperty LoadCommentsProperty =
            DependencyProperty.Register(
                "LoadComments",
                typeof(bool),
                typeof(FlipViewPostPanel),
                new PropertyMetadata(false, new PropertyChangedCallback(OnLoadCommentsChangedStatic)));

        private static void OnLoadCommentsChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = (FlipViewPostPanel)d;
            if (instance != null)
            {
                instance.OnLoadCommentsChanged((bool)e.NewValue);
            }
        }

        /// <summary>
        /// Fired when the LoadComments property changes.
        /// </summary>
        /// <param name="newVis"></param>
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
            get { return (FlipViewPostContext)GetValue(PanelContextProperty); }
            set { SetValue(PanelContextProperty, value); }
        }

        public static readonly DependencyProperty PanelContextProperty =
            DependencyProperty.Register(
                "PanelContext",
                typeof(FlipViewPostContext),
                typeof(FlipViewPostPanel),
                new PropertyMetadata(null, new PropertyChangedCallback(OnContextChangedStatic)));

        private static void OnContextChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = (FlipViewPostPanel)d;
            if (instance != null)
            {
                instance.OnContextChanged((FlipViewPostContext)e.NewValue);
            }
        }

        /// <summary>
        /// Fired when the Context property changes.
        /// </summary>
        /// <param name="newVis"></param>
        private void OnContextChanged(FlipViewPostContext newContext)
        {
            // Setup screen mode changing if not already.
            if (!m_isScrrenModeChangedSetup)
            {
                m_isScrrenModeChangedSetup = true;
                PanelContext.Host.OnScreenModeChanged += OnScreenModeChanged;
            }

            // When our context changes make sure our comments are reset
            ClearCommentManger();

            // If we should be prefetching comments do so now.
            if(LoadComments)
            {
                PreFetchPostComments();
            }

            // If we have a target comment show the UI now.
            if(!String.IsNullOrWhiteSpace(newContext.TargetComment))
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
            if (PanelContext != null)
            {
                // Make sure we are good
                if (PanelContext.Collector != null && PanelContext.Host != null && PanelContext.Post != null)
                {
                    return PanelContext;
                }
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
            FlipViewPostContext context = GetContext();
            if(context != null)
            {
                context.Collector.ChangePostVote(context.Post, PostVoteAction.UpVote);
            }
        }

        /// <summary>
        /// Fired when a down vote arrow is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DownVote_Tapped(object sender, EventArgs e)
        {
            FlipViewPostContext context = GetContext();
            if (context != null)
            {
                context.Collector.ChangePostVote(context.Post, PostVoteAction.DownVote);
            }
        }

        private async void OpenBrowser_Tapped(object sender, EventArgs e)
        {
            FlipViewPostContext context = GetContext();
            if (context != null)
            {
                string url = context.Post.Url;
                if (String.IsNullOrWhiteSpace(url))
                {
                    url = context.Post.Permalink;
                }
                if (!String.IsNullOrWhiteSpace(url))
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(url, UriKind.Absolute));
                 
                }
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
           
        }

        private void SavePost_Click(object sender, RoutedEventArgs e)
        {
            FlipViewPostContext context = GetContext();
            if (context != null)
            {
                context.Collector.SaveOrHidePost(context.Post, !context.Post.IsSaved, null);
                
            }
        }

        private void HidePost_Click(object sender, RoutedEventArgs e)
        {
            FlipViewPostContext context = GetContext();
            if (context != null)
            {
                context.Collector.SaveOrHidePost(context.Post, null, !context.Post.IsHidden);
               
            }
        }

        private void CopyLink_Click(object sender, RoutedEventArgs e)
        {
            FlipViewPostContext context = GetContext();
            if (context != null)
            {
                DataPackage data = new DataPackage();
                if (String.IsNullOrWhiteSpace(context.Post.Url))
                {
                    data.SetText("http://www.reddit.com" + context.Post.Permalink);
                }
                else
                {
                    data.SetText(context.Post.Url);
                }
                Clipboard.SetContent(data);
               
            }
        }

        private void CopyPermalink_Click(object sender, RoutedEventArgs e)
        {
            FlipViewPostContext context = GetContext();
            if (context != null)
            {
                DataPackage data = new DataPackage();
                data.SetText("http://www.reddit.com" + context.Post.Permalink);
                Clipboard.SetContent(data);
            }
        }

        private void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            FlipViewPostContext context = GetContext();
            if (context != null)
            {
                App.BaconMan.ImageMan.SaveImageLocally(context.Post.Url);
            }
        }

        // I threw up a little while I wrote this.
        Post m_sharePost = null;
        private void SharePost_Click(object sender, RoutedEventArgs e)
        {
            FlipViewPostContext context = GetContext();
            if (context != null)
            {
                if (!String.IsNullOrWhiteSpace(context.Post.Url))
                {
                    m_sharePost = context.Post;
                    // Setup the share contract so we can share data
                    DataTransferManager dataTransferManager = DataTransferManager.GetForCurrentView();
                    dataTransferManager.DataRequested += DataTransferManager_DataRequested;
                    DataTransferManager.ShowShareUI();
                   
                }
            }
        }

        private void DataTransferManager_DataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            if (m_sharePost != null)
            {
                args.Request.Data.Properties.ApplicationName = "Baconit";
                args.Request.Data.Properties.ContentSourceWebLink = new Uri(m_sharePost.Url, UriKind.Absolute);
                args.Request.Data.Properties.Title = "A Reddit Post Shared From Baconit";
                args.Request.Data.Properties.Description = m_sharePost.Title;
                args.Request.Data.SetText($"\r\n\r\n{m_sharePost.Title}\r\n\r\n{m_sharePost.Url}");
                m_sharePost = null;
               
            }
            else
            {
                args.Request.FailWithDisplayText("Baconit doesn't have anything to share!");
               
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
            FlipViewPostContext context = GetContext();
            if (context != null)
            {
                context.Host.ToggleMenu(true);
            }
        }

        /// <summary>
        /// Fired when the user taps delete post.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void DeletePost_Click(object sender, RoutedEventArgs e)
        {
            FlipViewPostContext context = GetContext();
            if (context != null)
            {
                // Confirm
                bool? doIt = await App.BaconMan.MessageMan.ShowYesNoMessage("Delete Post", "Are you sure you want to?");

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
            FlipViewPostContext context = GetContext();
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
            FlipViewPostContext context = GetContext();
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
        private void GoToSubreddit_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FlipViewPostContext context = GetContext();
            if (context != null)
            {
                // Navigate to the subreddit.
                Dictionary<string, object> args = new Dictionary<string, object>();
                args.Add(PanelManager.NAV_ARGS_SUBREDDIT_NAME, context.Post.Subreddit);
                context.Host.Navigate(typeof(SubredditPanel), context.Post.Subreddit + SortTypes.Hot + SortTimeTypes.Week, args);
                
            }
        }

        /// <summary>
        /// Fired when the user taps the go to user button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GoToUser_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FlipViewPostContext context = GetContext();
            if (context != null)
            {
                // Navigate to the user.
                Dictionary<string, object> args = new Dictionary<string, object>();
                args.Add(PanelManager.NAV_ARGS_USER_NAME, context.Post.Author);
                context.Host.Navigate(typeof(UserProfile), context.Post.Author, args);    
                
            }
        }

        #endregion

        #region List Scrolling Logic

        private void SetupListViewForNewContext()
        {
            // Hide the scroll bar.
            ScrollViewer.SetVerticalScrollBarVisibility(ui_listView, ScrollBarVisibility.Hidden);
            m_lastKnownScrollOffset = 0;
        }

        /// <summary>
        /// Creates a comment manager for the post and prefetches the comments
        /// </summary>
        /// <param name="post"></param>
        private void PreFetchPostComments(bool forcePreFetch = false, bool showThreadSubset = true)
        {
            // Make sure we aren't already ready.
            if(m_commentManager != null)
            {
                return;
            }

            FlipViewPostContext context = GetContext();
            if(context == null)
            {
                return;
            }

            // Create a comment manager for the post
            Post refPost = context.Post;
            m_commentManager = new FlipViewPostCommentManager(ref refPost, context.TargetComment, showThreadSubset);

            // Set the comment list to the list view
            ui_listView.ItemsSource = m_commentManager.Comments;

            // If the user wanted, kick off pre load of comments.
            if (forcePreFetch || App.BaconMan.UiSettingsMan.FlipView_PreloadComments)
            {
                m_commentManager.PreFetchComments();
            }
        }

        /// <summary>
        /// Will remove any comment managers that may exist for a post, and clears the comments.
        /// </summary>
        /// <param name="post"></param>
        private void ClearCommentManger()
        {
            if(m_commentManager != null)
            {
                m_commentManager.PrepareForDeletion();
                m_commentManager = null;
            }
        }

        /// <summary>
        /// Returns the comment manger if we have one.
        /// </summary>
        /// <returns></returns>
        private FlipViewPostCommentManager GetCommentManger()
        {
            return m_commentManager;
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
        private void List_OnListEndDetectedEvent(object sender, OnListEndDetected e)
        {
            // NOTE!!!
            // This is a very hot code path, so anything done here should be really quick!

            FlipViewPostContext context = GetContext();
            if(context == null)
            {
                return;
            }

            // Make sure the margin is normal.
            if (ui_stickyHeader.Margin.Top != 0)
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
            int currentScrollAera = GetCurrentScrollArea();

            // This will return -1 if we can't get this yet, so just get out of here.
            if (currentScrollAera == -1)
            {
                return;
            }

            // Use the header size and the control size to figure out if we should show the static header.
            bool showHeader = e.ListScrollTotalDistance > currentScrollAera - ui_stickyHeader.ActualHeight;
            ui_stickyHeader.Visibility = showHeader ? Visibility.Visible : Visibility.Collapsed;

            // Get the distance for the animation header to move. We need to account for the header
            // size here because when we move from full screen to not it will toggle. This is very touchy, we 
            // also only want to do this logic if we are scrolling down. On the way back up we need to unminimize 
            // (if the user forced us to be mini) before we hit the real header or things will be wack.
            int headerAniamtionDistance = currentScrollAera;
            if(m_isFullscreen && e.ScrollDirection != ScrollDirection.Up)
            {
                Grid headerGrid = (Grid)m_storyHeader.FindName("ui_storyHeaderBlock");
                headerAniamtionDistance -= (int)headerGrid.ActualHeight;
            }

            // If we are far enough to also hide the header consider hiding it.
            if (e.ListScrollTotalDistance > headerAniamtionDistance)
            {
                if (App.BaconMan.UiSettingsMan.FlipView_MinimizeStoryHeader)
                {
                    if (e.ScrollDirection == ScrollDirection.Down)
                    {
                        ToggleFullscreen(true);
                    }
                    else if(e.ScrollDirection == ScrollDirection.Up)
                    {
                        ToggleFullscreen(false);
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
            m_lastKnownScrollOffset = (int)e.ListScrollTotalDistance;

            //// Hide the tip box if needed.
            HideCommentScrollTipIfNeeded();

            // If we have a manager request more posts.
            if (m_commentManager != null)
            {
                if (m_commentManager.IsOnlyShowingSubset())
                {
                    m_commentManager.RequestMorePosts();
                }
            }
        }

        /// <summary>
        /// Fired when a item is selected, just clear it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EndDetectingListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            EndDetectingListView listview = sender as EndDetectingListView;
            listview.SelectedIndex = -1;
        }

        /// <summary>
        /// Scrolls to the top of the list view for a post.
        /// </summary>
        /// <param name="postId"></param>
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
            FlipViewPostCommentManager manager = GetCommentManger();
            if (manager != null)
            {
                m_commentManager.Refresh();
            }
        }

        private void CommentUp_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // We don't need to animate here, the vote will do it for us
            Comment comment = (sender as FrameworkElement).DataContext as Comment;
            FlipViewPostCommentManager manager = GetCommentManger();
            if (manager != null)
            {
                manager.UpVote_Tapped(comment);
            }
        }

        private void CommentDown_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // We don't need to animate here, the vote will do it for us
            Comment comment = (sender as FrameworkElement).DataContext as Comment;
            FlipViewPostCommentManager manager = GetCommentManger();
            if (manager != null)
            {
                manager.DownVote_Tapped(comment);
            }
        }

        private void CommentButton3_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Animate the text
            AnimateText((FrameworkElement)sender);

            // Get the comment
            Comment comment = (sender as FrameworkElement).DataContext as Comment;

            if (comment.IsDeleted)
            {
                App.BaconMan.MessageMan.ShowMessageSimple("LET IT GO!", "You can't edit a deleted comment!");
                return;
            }

            if (comment.IsCommentOwnedByUser)
            {
                // Edit
                ShowCommentBox("t1_" + comment.Id, comment.Body, comment);
            }
            else
            {
                // Reply
                ShowCommentBox("t1_" + comment.Id, null, comment);
            }
        }

        private async void CommentButton4_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Animate the text
            AnimateText((FrameworkElement)sender);

            // Get the comment
            Comment comment = (sender as FrameworkElement).DataContext as Comment;

            if (comment.IsCommentOwnedByUser)
            {
                // Delete the comment
                bool? response = await App.BaconMan.MessageMan.ShowYesNoMessage("Delete Comment", "Are you sure?");

                if (response.HasValue && response.Value)
                {
                    // Find the manager
                    FlipViewPostCommentManager manager = GetCommentManger();
                    if (manager != null)
                    {
                        manager.CommentDeleteRequest(comment);
                    }
                }
            }
            else
            {
                
                
            }
        }

        private void UserPanel_Tapped(object sender, TappedRoutedEventArgs e)
        {

            Comment comment = (sender as FrameworkElement).DataContext as Comment;
            FlipViewPostContext context = GetContext();
            if (context == null)
            {
                return;
            }

            else
            {
                // Navigate to the user
                Dictionary<string, object> args = new Dictionary<string, object>();
                args.Add(PanelManager.NAV_ARGS_USER_NAME, comment.Author);
                context.Host.Navigate(typeof(UserProfile), comment.Author, args);
            }
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

            
        }

        private void CommentSave_Click(object sender, RoutedEventArgs e)
        {
            Comment comment = (sender as FrameworkElement).DataContext as Comment;
            FlipViewPostCommentManager manager = GetCommentManger();
            if (manager != null)
            {
                manager.Save_Tapped(comment);
            }
            
        }

        private void CommentShare_Click(object sender, RoutedEventArgs e)
        {
            Comment comment = (sender as FrameworkElement).DataContext as Comment;
            FlipViewPostCommentManager manager = GetCommentManger();
            if (manager != null)
            {
                manager.Share_Tapped(comment);
            }
            
        }

        private void CommentPermalink_Click(object sender, RoutedEventArgs e)
        {
            Comment comment = (sender as FrameworkElement).DataContext as Comment;
            FlipViewPostCommentManager manager = GetCommentManger();
            if (manager != null)
            {
                manager.CopyPermalink_Tapped(comment);
            }
           
        }

        private void CommentCollapse_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Animate the text
            AnimateText((FrameworkElement)sender);

            Comment comment = (sender as FrameworkElement).DataContext as Comment;
            FlipViewPostCommentManager manager = GetCommentManger();
            if (manager != null)
            {
                manager.Collpase_Tapped(comment);
            }
        }

        private void CollapsedComment_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Comment comment = (sender as FrameworkElement).DataContext as Comment;
            FlipViewPostCommentManager manager = GetCommentManger();
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
            if (VisualTreeHelper.GetChildrenCount(textBlockContainer) != 1)
            {
                return;
            }

            // Try to get the text block
            TextBlock textBlock = (TextBlock)VisualTreeHelper.GetChild(textBlockContainer, 0);

            // Return if failed.
            if (textBlock == null)
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
        /// Fired when a user taps a link in a comment
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MarkdownTextBlock_OnMarkdownLinkTapped(object sender, UniversalMarkdown.OnMarkdownLinkTappedArgs e)
        {
            App.BaconMan.ShowGlobalContent(e.Link);
        }

        #endregion

        #region Full Screen Logic

        /// <summary>
        /// Gets full screen ready for a new context
        /// </summary>
        private void SetupFullScreenForNewContext()
        {
            m_fullScreenOverwrite = null;
            m_isfullScreenOverwriteUser = null;
            ToggleFullscreen(false, true);

            if (m_storyHeader != null)
            {
                Grid headerGrid = (Grid)m_storyHeader.FindName("ui_storyHeaderBlock");
                headerGrid.MaxHeight = double.PositiveInfinity;
            }
        }

        /// <summary>
        /// Fired when the control wants to go full screen.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentPanelHost_OnToggleFullscreen(object sender, OnToggleFullScreenEventArgs e)
        {
            // Set the overwrite
            if(e.GoFullScreen)
            {
                // Scroll the header into view
                ScrollCommentsToTop();

                // Set the overwrite
                m_fullScreenOverwrite = true;
                m_isfullScreenOverwriteUser = false;
            }
            else
            {
                // Disable the overwrite
                m_isfullScreenOverwriteUser = null;
                m_fullScreenOverwrite = null;
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
            if(!m_isFullscreen)
            {
                m_fullScreenOverwrite = true;
                m_isfullScreenOverwriteUser = true;
            }
            else
            {
                m_fullScreenOverwrite = null;
                m_isfullScreenOverwriteUser = null;
            }

            ToggleFullscreen(!m_isFullscreen);
        }

        /// <summary>
        /// Given a post toggles the header
        /// </summary>
        /// <param name="post"></param>
        private void ToggleFullscreen(bool goFullscreen, bool force = false)
        {
            // If we are already there don't do anything.
            if(m_isFullscreen == goFullscreen)
            {
                return;
            }

            // Make sure the user hasn't overwritten this.
            bool? localOverwriteValue = m_fullScreenOverwrite;
            if (localOverwriteValue.HasValue)
            {
                // If we are being force and the overwrite isn't from the user skip this.
                if(!force || (localOverwriteValue.HasValue && !localOverwriteValue.Value))
                {
                    if (localOverwriteValue.Value != goFullscreen)
                    {
                        return;
                    }
                }
            }
            m_isFullscreen = goFullscreen;

            string traceString = "";
            try
            {
                if (IsVisible)
                {
                    // Get our elements for the sticky header
                    Storyboard storyboard = (Storyboard)m_stickyHeader.FindName("ui_storyCollapseHeader");
                    DoubleAnimation animBody = (DoubleAnimation)m_stickyHeader.FindName("ui_animHeaderBodyTranslate");
                    DoubleAnimation animTitle = (DoubleAnimation)m_stickyHeader.FindName("ui_animHeaderTitleTranslate");
                    DoubleAnimation animSubtext = (DoubleAnimation)m_stickyHeader.FindName("ui_animHeaderSubtextTranslate");
                    DoubleAnimation animIcons = (DoubleAnimation)m_stickyHeader.FindName("ui_animHeaderIconsTranslate");
                    DoubleAnimation animFullscreenButton = (DoubleAnimation)m_stickyHeader.FindName("ui_animHeaderFullscreenButtonRotate");
                    Grid stickyGrid = (Grid)m_stickyHeader.FindName("ui_storyHeaderBlock");

                    traceString += " gotstickelement ";

                    // Stop any past animations.
                    if (storyboard.GetCurrentState() != ClockState.Stopped)
                    {
                        storyboard.Stop();
                    }

                    traceString += $" settingStickAnim height [{stickyGrid.ActualHeight}[ ";

                    // Setup the animations.
                    double animTo = goFullscreen ? -stickyGrid.ActualHeight : 0;
                    double animFrom = goFullscreen ? 0 : -stickyGrid.ActualHeight;
                    animBody.To = animTo;
                    animBody.From = animFrom;
                    animTitle.To = animTo;
                    animTitle.From = animFrom;
                    animSubtext.To = animTo;
                    animSubtext.From = animFrom;
                    animIcons.To = animTo;
                    animIcons.From = animFrom;
                    animFullscreenButton.To = goFullscreen ? 0 : 180;
                    animFullscreenButton.From = goFullscreen ? 180 : 0;

                    traceString += " gettingnormalHeader ";

                    // For the normal header
                    Storyboard storyNormal = (Storyboard)m_storyHeader.FindName("ui_storyCollapseHeaderHeight");
                    Grid headerGrid = (Grid)m_storyHeader.FindName("ui_storyHeaderBlock");
                    DoubleAnimation animNormal = (DoubleAnimation)m_storyHeader.FindName("ui_animHeaderHeightCollapse");
                    DoubleAnimation animNormalFullscreenButton = (DoubleAnimation)m_storyHeader.FindName("ui_animHeaderHeightButtonRotate");

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
                    Grid headerGrid = (Grid)m_storyHeader.FindName("ui_storyHeaderBlock");
                    RotateTransform headerFullscreenButtonRotate = (RotateTransform)m_storyHeader.FindName("ui_headerFullscreenButtonRotate");
                    headerFullscreenButtonRotate.Angle = goFullscreen ? 0 : 180;
                    headerGrid.MaxHeight = goFullscreen ? double.NaN : headerGrid.ActualHeight;

                    traceString += $" SettingElements height[{headerGrid.ActualHeight}] ";

                    // For the sticky header reset the transforms.
                    TranslateTransform titleTrans = (TranslateTransform)m_storyHeader.FindName("ui_headerTitleTransform");
                    TranslateTransform subtextTrans = (TranslateTransform)m_storyHeader.FindName("ui_headerSubtextTransform");
                    TranslateTransform iconTrans = (TranslateTransform)m_storyHeader.FindName("ui_headerIconsTransform");
                    TranslateTransform bodyTrans = (TranslateTransform)m_storyHeader.FindName("ui_headerBodyTransform");
                    Grid stickyGrid = (Grid)m_stickyHeader.FindName("ui_storyHeaderBlock");
                    double setTo = goFullscreen ? -stickyGrid.ActualHeight : 0;
                    titleTrans.Y = setTo;
                    subtextTrans.Y = setTo;
                    iconTrans.Y = setTo;
                    bodyTrans.Y = setTo;
                }
            }
            catch(Exception e)
            {
              
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
            if(m_storyHeader == null)
            {
                m_storyHeader = (Grid)sender;
            }
            else
            {
                m_stickyHeader = (Grid)sender;
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
            FrameworkElement element = sender as FrameworkElement;
            if (element != null)
            {
                FlyoutBase.ShowAttachedFlyout(element);
            }
            
        }

        /// <summary>
        /// Fired when a user taps a new sort type for comments.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CommentSortMenu_Click(object sender, RoutedEventArgs e)
        {
            FlipViewPostContext context = GetContext();
            if(context == null)
            {
                return;
            }

            // Update sort type
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            context.Post.CommentSortType = GetCommentSortFromString(item.Text);

            // Get the collector and update the sort
            FlipViewPostCommentManager commentManager = GetCommentManger();
            commentManager.ChangeCommentSort();
        }

        private CommentSortTypes GetCommentSortFromString(string typeString)
        {
            typeString = typeString.ToLower();
            switch (typeString)
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
            FlipViewPostContext context = GetContext();
            if (context == null)
            {
                return;
            }

            // Parse the new comment count
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            context.Post.CurrentCommentShowingCount = int.Parse(item.Text);

            // Get the collector and update the sort
            FlipViewPostCommentManager commentManager = GetCommentManger();
            commentManager.UpdateShowingCommentCount(context.Post.CurrentCommentShowingCount);
        }

        #endregion

        #region Screen Mode

        /// <summary>
        /// Fired when the screen mode changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnScreenModeChanged(object sender, OnScreenModeChangedArgs e)
        {
            SetupScreenModeForNewContext(e.NewScreenMode);
        }

        /// <summary>
        /// Sets the menu icon correctly.
        /// </summary>
        /// <param name="newMode"></param>
        private void SetupScreenModeForNewContext(ScreenMode newMode)
        {
            FlipViewPostContext context = GetContext();
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
        public void SetupHeaderForNewContext()
        {
            // If we have not done our trick yet, don't collapse it.
            if (ui_stickyHeader.Margin.Top != 0)
            {
                ui_stickyHeader.Visibility = Visibility.Visible;
            }
            else
            {
                ui_stickyHeader.Visibility = Visibility.Collapsed;
            }

            // Set the header size for this.
            SetHeaderSize();
        }

        /// <summary>
        /// Updates the header sizes for the flip post so they fit perfectly on the screen.
        /// </summary>
        private void SetHeaderSize()
        {
            // Get the screen size, account for the comment box if it is open.
            int currentScrollAera = GetCurrentScrollArea();

            // This will return -1 if we can't get this number yet.
            if (currentScrollAera == -1)
            {
                return;
            }

            FlipViewPostContext context = GetContext();
            context.HeaderSize = currentScrollAera;
        }

        /// <summary>
        /// Returns the current space that's available for the flipview scroller
        /// </summary>
        /// <returns></returns>
        private int GetCurrentScrollArea()
        {
            // Get the control size
            int screenSize = (int)ui_contentRoot.ActualHeight;

            // Make sure we are ready.
            if (ui_contentRoot.ActualHeight == 0)
            {
                // If not return -1.
                return -1;
            }

            FlipViewPostContext context = GetContext();
            if(context == null)
            {
                return -1;
            }

            // If we are showing the show all comments header add the height of that so it won't be on the screen by default
            if (context.Post.FlipViewShowEntireThreadMessage == Visibility.Visible)
            {
                screenSize += c_hiddenShowAllCommentsHeight;
            }

            // Add a the comment header size.
            screenSize += c_hiddenCommentHeaderHeight;

            // If we are full screen account for the header being gone.
            if (m_isFullscreen)
            {
                Grid headerGrid = (Grid)m_storyHeader.FindName("ui_storyHeaderBlock");
                screenSize += (int)headerGrid.ActualHeight;
            }

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
            int currentScrollArea = GetCurrentScrollArea();
            if (m_lastKnownScrollOffset < currentScrollArea)
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
            OnOpenCommentBox args = new OnOpenCommentBox()
            {
                Context = context,
                RedditId = redditId,
                EditText = editText,
                CommentBoxOpened = CommentBox_OnBoxOpened,
                CommentBoxSubmitted = CommentBox_OnCommentSubmitted
            };
            m_onOpenCommentBox.Raise(this, args);
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
                Comment comment = (Comment)e.Context;
                ui_listView.ScrollIntoView(comment);
            }
        }

        /// <summary>
        /// Fired when the comment in the comment box was submitted
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private bool CommentBox_OnCommentSubmitted(object sender, OnCommentSubmittedArgs e)
        {
            bool wasActionSuccessful = false;

            FlipViewPostContext context = GetContext();
            if(context == null)
            {
                return wasActionSuccessful;
            }

            if (e.RedditId.StartsWith("t3_"))
            {
                Post post = (Post)e.Context;

                if (post != null)
                {
                    if (e.IsEdit)
                    {
                        // We edited selftext
                        wasActionSuccessful = context.Collector.EditSelfPost(post, e.Response);

                        if (wasActionSuccessful)
                        {
                            // If we are successful to update the UI we will remove the post
                            // and reallow it.
                            ContentPanelMaster.Current.RemoveAllowedContent(post.Id);

                            // Now ask the host to reallow it.
                            m_onContentLoadRequest.Raise(this, new OnContentLoadRequestArgs() { SourceId = post.Id });
                        }
                    }
                    else
                    {
                        // We added a new comment
                        FlipViewPostCommentManager manager = GetCommentManger();
                        if (manager != null)
                        {
                            wasActionSuccessful = manager.CommentAddedOrEdited("t3_" + post.Id, e);
                        }
                        else
                        {
                            
                        }
                    }
                }
                else
                {
                    
                }
            }
            else if (e.RedditId.StartsWith("t1_"))
            {
                Comment comment = (Comment)e.Context;
                if (comment != null)
                {
                    // Comment added or edited.
                    FlipViewPostCommentManager manager = GetCommentManger();
                    if (manager != null)
                    {
                        wasActionSuccessful = manager.CommentAddedOrEdited("t1_" + comment.Id, e);
                    }
                    else
                    {
                        
                    }
                }
                else
                {
                   
                }
            }

            return wasActionSuccessful;
        }

        #endregion

        #region Pro Tip Logic

        /// <summary>
        /// Shows the comment scroll tip if needed
        /// </summary>
        public void ShowCommentScrollTipIfNeeded()
        {
            if (!App.BaconMan.UiSettingsMan.FlipView_ShowCommentScrollTip)
            {
                return;
            }

            // Never show it again.
            App.BaconMan.UiSettingsMan.FlipView_ShowCommentScrollTip = false;

            // Create the tip UI, add it to the UI and show it.
            m_commentTipPopUp = new TipPopUp();
            m_commentTipPopUp.Margin = new Thickness(0, 0, 0, 90);
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
            if (m_commentTipPopUp == null)
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

        /// <summary>
        /// Fired when the user has tapped the panel requesting the content to load.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentPanelHost_OnContentLoadRequest(object sender, OnContentLoadRequestArgs e)
        {
            // Forward the call along.
            m_onContentLoadRequest.Raise(this, e);
        }
    }
}
