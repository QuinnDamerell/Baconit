using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using BaconBackend.Managers;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Baconit.FlipViewControls
{
    public sealed partial class FlipViewContentControl : UserControl, IFlipViewContentHost
    {
        /// <summary>
        /// This holds a list of all of the post where the block has been lowered.
        /// </summary>
        private static Dictionary<string, bool> s_previousLoweredNsfwBlocks = new Dictionary<string, bool>();

        /// <summary>
        /// This is a list of subreddits the block has been lowered for
        /// </summary>
        private static Dictionary<string, bool> s_previousLoweredNsfwSubreddits = new Dictionary<string, bool>();

        /// <summary>
        /// Holds a reference to the control we are using.
        /// </summary>
        IFlipViewContentControl m_control;

        /// <summary>
        /// Holds the current post id, this is used to ignore multiple calls.
        /// </summary>
        Post m_currentPost;

        /// <summary>
        /// Indicates if we can go full screen or not.
        /// </summary>
        bool m_canGoFullScreen = true;

        /// <summary>
        /// Indicates is we are current fullscreen or not.
        /// </summary>
        bool m_isFullScreen = false;

        /// <summary>
        /// Args for toggle full screen
        /// </summary>
        public class OnToggleFullScreenEventArgs : EventArgs
        {
            public bool GoFullScreen { get; set; }
        }

        /// <summary>
        /// Fired when full screen is toggled
        /// </summary>
        public event EventHandler<OnToggleFullScreenEventArgs> OnToggleFullscreen
        {
            add { m_onToggleFullscreen.Add(value); }
            remove { m_onToggleFullscreen.Remove(value); }
        }
        SmartWeakEvent<EventHandler<OnToggleFullScreenEventArgs>> m_onToggleFullscreen = new SmartWeakEvent<EventHandler<OnToggleFullScreenEventArgs>>();

        public FlipViewContentControl()
        {
            this.InitializeComponent();
        }

        #region FlipPost Logic

        /// <summary>
        /// This it how we get the post form the xmal binding.
        /// </summary>
        public Post FlipPost
        {
            get { return (Post)GetValue(FlipPostProperty); }
            set { SetValue(FlipPostProperty, value); }
        }

       public static readonly DependencyProperty FlipPostProperty =
           DependencyProperty.Register(
               "FlipPost",                      // The name of the DependencyProperty
               typeof(Post),                 // The type of the DependencyProperty
               typeof(FlipViewContentControl), // The type of the owner of the DependencyProperty
               new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                   false,                      // The default value of the DependencyProperty
                   new PropertyChangedCallback(OnFlipPostChangedStatic)
               ));

        private static void OnFlipPostChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as FlipViewContentControl;
            if (instance != null)
            {
                // Send the post to the class.
                Post newPost = null;
                if(e.NewValue != null && e.NewValue.GetType() == typeof(Post))
                {
                    newPost = (Post)e.NewValue;
                }
                instance.OnPostChanged(newPost);
            }
        }

        #endregion

        #region IsVisible Logic

        /// <summary>
        /// This it how we get the visibility from flip view
        /// </summary>
        public bool IsVisible
        {
            get { return (bool)GetValue(IsVisibleProperty); }
            set { SetValue(IsVisibleProperty, value); }
        }

        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register(
                "IsVisible",                      // The name of the DependencyProperty
                typeof(bool),                 // The type of the DependencyProperty
                typeof(FlipViewContentControl), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    false,                      // The default value of the DependencyProperty
                    new PropertyChangedCallback(OnIsVisibleChangedStatic)
                ));

        private static void OnIsVisibleChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as FlipViewContentControl;
            if (instance != null)
            {
                instance.OnVisibleChanged(e.NewValue.GetType() == typeof(bool) ? (bool)e.NewValue : false);
            }
        }

        #endregion

        #region CanGoFullScreen Logic

        /// <summary>
        /// This it how we get the visibility from flip view
        /// </summary>
        public bool CanGoFullScreen
        {
            get { return (bool)GetValue(CanGoFullScreenProperty); }
            set { SetValue(CanGoFullScreenProperty, value); }
        }

        public static readonly DependencyProperty CanGoFullScreenProperty =
            DependencyProperty.Register(
                "CanGoFullScreen",                      // The name of the DependencyProperty
                typeof(bool),                 // The type of the DependencyProperty
                typeof(FlipViewContentControl), // The type of the owner of the DependencyProperty
                new PropertyMetadata(           // OnBlinkChanged will be called when Blink changes
                    true,                      // The default value of the DependencyProperty
                    new PropertyChangedCallback(OnCanGoFullScreenChangedStatic)
                ));

        private static void OnCanGoFullScreenChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as FlipViewContentControl;
            if (instance != null)
            {
                // Set the new state
                instance.m_canGoFullScreen = (bool)e.NewValue;
            }
        }

        #endregion

        #region Content Logic

        /// <summary>
        /// Fired when the backing post changed
        /// </summary>
        /// <param name="flipPost">New post</param>
        public void OnPostChanged(Post flipPost)
        {
            if(m_currentPost != null && flipPost != null && m_currentPost.Id.Equals(flipPost.Id))
            {
                // We already have this loaded, jump out of here
                return;
            }

            // Delete the current control
            DeleteCurrentControl();

            if (flipPost != null)
            {
                m_currentPost = flipPost;

                if (App.BaconMan.UiSettingsMan.FlipView_LoadPostContentWithoutAction)
                {
                    // The normal case, load the content now.
                    ShowNsfwIfNeeded(flipPost);
                    CreateNewControl(flipPost);
                }
                else
                {
                    // The user don't want us to load content without them tapping the screen.
                    VisualStateManager.GoToState(this, "ShowDontPreloadBlock", true);
                }             
            }
        }

        /// <summary>
        /// Deletes the current post's content.
        /// </summary>
        private void DeleteCurrentControl()
        {
            // Clear out the panel
            ui_contentRoot.Children.Clear();

            // Clear the current post
            m_currentPost = null;

            if (m_control != null)
            {
                // Destroy it
                m_control.OnDestroyContent();

                // Kill it.
                m_control = null;
            }
        }

        /// <summary>
        /// Creates new post content
        /// </summary>
        /// <param name="flipPost"></param>
        private void CreateNewControl(Post flipPost)
        {
            // First figure out who can handle this post.
            // Important, the order we ask matter since there maybe overlap
            // in handlers.
            if (GifImageFliplControl.CanHandlePost(flipPost))
            {
                m_control = new GifImageFliplControl(this);
            }
            else if (YoutubeFlipControl.CanHandlePost(flipPost))
            {
                m_control = new YoutubeFlipControl(this);
            }
            else if (BasicImageFlipControl.CanHandlePost(flipPost))
            {
                m_control = new BasicImageFlipControl(this);
            }
            else if (RedditMarkdownFlipControl.CanHandlePost(flipPost))
            {
                m_control = new RedditMarkdownFlipControl(this);
            }
            else if (RedditContentFlipControl.CanHandlePost(flipPost))
            {
                m_control = new RedditContentFlipControl(this);
            }
            else if(CommnetSpoilerFlipControl.CanHandlePost(flipPost))
            {
                m_control = new CommnetSpoilerFlipControl(this);
            }
            else if (WindowsAppFlipControl.CanHandlePost(flipPost))
            {
                m_control = new WindowsAppFlipControl(this);
            }
            else
            {
                m_control = new WebPageFlipControl(this);
            }

            // Setup the control
            m_control.OnPrepareContent(flipPost);

            // Add the control to the UI
            ui_contentRoot.Children.Add((UserControl)m_control);
        }

        /// <summary>
        /// Called when a post fails to load, we should fallback to the browser.
        /// </summary>
        /// <param name="post"></param>
        public void FallbackToWebBrowser(Post post)
        {
            // Show loading
            ShowLoading();

            // Delete what we have
            DeleteCurrentControl();

            // Reset the post
            m_currentPost = post;

            // Make a web control
            m_control = new WebPageFlipControl(this);

            // Setup the control
            m_control.OnPrepareContent(post);

            // Add the control to the UI
            ui_contentRoot.Children.Add((UserControl)m_control);
        }

        #endregion

        #region IsVisible Logic

        /// <summary>
        /// Fired when IsVisible changes. This indicates the post should play or show now.
        /// </summary>
        /// <param name="isVisible"></param>
        public void OnVisibleChanged(bool isVisible)
        {
            if(isVisible)
            {
                // Confirm the NSFW block is correct. We need to do this again to check if the user has lowered
                // the nsfw block for this subreddit.
                ShowNsfwIfNeeded(m_currentPost);

                // If we are now visible fire the on visible event.
                if(m_control != null)
                {
                    m_control.OnVisible();
                }
            }
        }


        #endregion

        #region Loading UI

        /// <summary>
        /// Called by the control when we should show loading UI.
        /// </summary>
        public void ShowLoading()
        {
            ShowProgressHolder(true, "Loading");
        }

        /// <summary>
        /// Called by the control when we should hide loading UI.
        /// </summary>
        public void HideLoading()
        {
            FadeProgressHolder(false);
        }

        /// <summary>
        /// Called by the control when we should show an error.
        /// </summary>
        public void ShowError()
        {
            // #todo offer to open in web browser
            ShowProgressHolder(false, "Unable To Load Post... Sorry About That.");
        }

        /// <summary>
        /// Shows the progress with the string and possible loading indicator
        /// </summary>
        /// <param name="showProgress"></param>
        /// <param name="text"></param>
        private void ShowProgressHolder(bool showProgress, string text)
        {
            // Show the background blocker
            ui_backgroundBlocker.Opacity = 1;
            ui_backgroundBlocker.Visibility = Visibility.Visible;

            // Prepare the loading UI
            ui_progressHolder.Opacity = 0;
            ui_progressHolder.Visibility = Visibility.Visible;
            ui_progressRing.IsActive = showProgress;
            ui_progressRing.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
            ui_progressText.Text = text;

            // Animate the UI in
            FadeProgressHolder(true);
        }

        /// <summary>
        /// Fades in or out the progress UI.
        /// </summary>
        /// <param name="isFadingIn"></param>
        private void FadeProgressHolder(bool isFadingIn)
        {
            // Set the values
            ui_animProgressHolder.To = isFadingIn ? 1 : 0;
            ui_animProgressHolder.From = ui_progressHolder.Opacity;

            // Stop the current animation if running
            ui_storyProgressHolder.Stop();

            // Play the new animation
            ui_storyProgressHolder.Begin();

            // If we are fading in we also want to fade out the back blocker.
            if (isFadingIn)
            {
                ui_storyBackBlock.Begin();
            }
        }

        private void StoryProgressHolder_Completed(object sender, object e)
        {
            if(ui_progressHolder.Opacity == 0)
            {
                ui_progressRing.IsActive = false;
                ui_progressHolder.Visibility = Visibility.Collapsed;
                ui_backgroundBlocker.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region NSFW block

        public void ShowNsfwIfNeeded(Post post)
        {
            // If the post is over 18, and it hasn't been lowered, and we don't have block off, and we won't have per subreddit on and the subreddit has been lowered
            if(post != null && post.IsOver18 &&
               !s_previousLoweredNsfwBlocks.ContainsKey(post.Id) &&
                    (App.BaconMan.UiSettingsMan.FlipView_NsfwBlockingType == NsfwBlockType.Always ||
                        (App.BaconMan.UiSettingsMan.FlipView_NsfwBlockingType == NsfwBlockType.PerSubreddit &&
                        !s_previousLoweredNsfwSubreddits.ContainsKey(post.Subreddit))))
            {
                VisualStateManager.GoToState(this, "ShowNsfwBlock", false);
            }
            else
            {
                VisualStateManager.GoToState(this, "HideNsfwBlock", false);
            }
        }

        private void NsfwBlockRoot_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Post currentPost = m_currentPost;
            // Return if there is no post.
            if(currentPost == null)
            {
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "CurrentPostNullInNSFWBlockTapped");
                return;
            }

            if (currentPost.Id == null)
            {
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "CurrentPostNullInNSFWBlockTapped_IDNULL");
                return;
            }

            if (currentPost.Subreddit == null)
            {
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "CurrentPostNullInNSFWBlockTapped_SUBREDDITNUL");
                return;
            }

            // When the block is tapped, animate out the block screen and add it to the list
            // not to block again
            if(!s_previousLoweredNsfwBlocks.ContainsKey(currentPost.Id))
            {
                s_previousLoweredNsfwBlocks.Add(currentPost.Id, true);
            }

            // If the block is tapped and we are in subreddit mode add it to the ignore subreddit list.
            if (App.BaconMan.UiSettingsMan.FlipView_NsfwBlockingType == NsfwBlockType.PerSubreddit &&
                !s_previousLoweredNsfwSubreddits.ContainsKey(currentPost.Subreddit))
            {
                s_previousLoweredNsfwSubreddits.Add(currentPost.Subreddit, true);
            }

            // Animate out the NSFW block.
            VisualStateManager.GoToState(this, "HideNsfwBlock", true);
        }

        #endregion

        #region Full Screen

        public bool ToggleFullScreen(bool goFullscreen)
        {
            // If we are disabled don't let them.
            if (!m_canGoFullScreen)
            {
                return false;
            }

            // Ignore if we are already in this state
            if(goFullscreen == m_isFullScreen)
            {
                return false;
            }
            m_isFullScreen = goFullscreen;

            // Set the manipulation mode to steal all of the touch events
            ManipulationMode = goFullscreen ? ManipulationModes.All : ManipulationModes.System;

            // Fire the event
            OnToggleFullScreenEventArgs args = new OnToggleFullScreenEventArgs()
            {
                GoFullScreen = goFullscreen
            };
            m_onToggleFullscreen.Raise(this, args);
            return true;
        }

        /// <summary>
        /// Indicates if the control is currently full screen.
        /// </summary>
        /// <returns></returns>
        public bool IsFullScreen()
        {
            return m_isFullScreen;
        }

        #endregion

        #region Dont Prelaod Logic

        /// <summary>
        /// Fried when the user taps the option to show the post.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DontPreloadBlock_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Post currentPost = m_currentPost;
            if (currentPost == null)
            {
                return;
            }

            // Show the post.
            ShowNsfwIfNeeded(currentPost);
            CreateNewControl(currentPost);

            // Hide the block
            VisualStateManager.GoToState(this, "HideDontPreloadBlock", true);
        }

        #endregion
    }
}
