using Baconit.ContentPanels;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using BaconBackend;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.HelperControls
{
    /// <summary>
    /// States of the global content presenter
    /// </summary>
    public enum GlobalContentStates
    {
        Idle,
        Opening,
        Showing,
        Closing
    }

    public sealed partial class GlobalContentPresenter : UserControl
    {
        /// <summary>
        /// Represents the current state of the presenter
        /// </summary>
        public GlobalContentStates State { get; private set; }

        /// <summary>
        /// Holds a reference to the content control
        /// </summary>
        private ContentPanelHost _mContentControl;

        /// <summary>
        /// The current source we are showing.
        /// </summary>
        private ContentPanelSource _mSource;

        public GlobalContentPresenter()
        {
            InitializeComponent();
            ui_background.Visibility = Visibility.Collapsed;
            ui_mainHolder.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Called when we should show something
        /// </summary>
        /// <param name="link"></param>
        public void ShowContent(string link)
        {
            // Make sure we are in the correct state
            lock(this)
            {
                if(State != GlobalContentStates.Idle)
                {
                    return;
                }
                State = GlobalContentStates.Opening;
            }

            // Create the content control
            _mContentControl = new ContentPanelHost();

            // Disable full screen
            _mContentControl.CanGoFullscreen = false;

            // This isn't great, but for now mock a post
            _mSource = ContentPanelSource.CreateFromUrl(link);

            // Approve the content to be shown
            Task.Run(() =>
            {                
                ContentPanelMaster.Current.AddAllowedContent(_mSource);
            });     

            // Add the control to the UI
            ui_contentRoot.Children.Add(_mContentControl);

            // Set the post to begin loading
            _mContentControl.SourceId = _mSource.Id;
            _mContentControl.IsVisible = true;

            // Show the panel
            ToggleShown(true);
        }

        public void Close()
        {
            // Make sure we can close
            lock (this)
            {
                if (State != GlobalContentStates.Showing)
                {
                    return;
                }
                State = GlobalContentStates.Closing;
            }

            // Remove the content
            Task.Run(() =>
            {                
                ContentPanelMaster.Current.RemoveAllowedContent(_mSource.Id);
            });           

            ToggleShown(false);
        }

        /// <summary>
        /// Called when the close animation is complete
        /// </summary>
        private void CloseComplete()
        {
            // Hide the UI
            ui_background.Visibility = Visibility.Collapsed;
            ui_mainHolder.Visibility = Visibility.Collapsed;

            // Kill the story
            if(_mContentControl != null)
            {
                _mContentControl.SourceId = null;
            }
            _mContentControl = null;

            // Clear the UI
            ui_contentRoot.Children.Clear();
        }

        #region Click Handlers

        /// <summary>
        /// Fired when the close button is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Close_Tapped(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Fired when the user taps the browser button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Browser_Tapped(object sender, EventArgs e)
        {
            try
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(_mSource.Url.ToLowerInvariant().UseOldReddit(), UriKind.Absolute));
            }
            catch(Exception)
            { }
        }

        #endregion

        #region Animation

        /// <summary>
        /// Shows or hides the panel
        /// </summary>
        /// <param name="show"></param>
        public void ToggleShown(bool show)
        {
            // Setup
            ui_animBackgroundAnimation.To = show ? 1 : 0;
            ui_animBackgroundAnimation.From = show ? 0 : 1;
            ui_animContentRootAnimation.To = show ? 1 : 0;
            ui_animBackgroundAnimation.From = show ? 0 : 1;
            ui_animContentRootScaleXAnimation.To = show ? 1 : .5;
            ui_animContentRootScaleXAnimation.From = show ? .5 : 1;
            ui_animContentRootScaleYAnimation.To = show ? 1 : .5;
            ui_animContentRootScaleYAnimation.From = show ? .5 : 1;
            ui_easeContentRootScaleY.EasingMode = show ? EasingMode.EaseOut : EasingMode.EaseIn;
            ui_easeContentRootScaleX.EasingMode = show ? EasingMode.EaseOut : EasingMode.EaseIn;
            ui_background.Opacity = show ? 0 : 1;
            ui_mainHolder.Opacity = show ? 0 : 1;

            // Show
            ui_background.Visibility = Visibility.Visible;
            ui_mainHolder.Visibility = Visibility.Visible;

            // Animate
            ui_storyAnimation.Begin();
        }

        /// <summary>
        /// Called when the
        /// </summary>
        private void Animation_Completed(object sender, object e)
        {
            lock (this)
            {
                switch (State)
                {
                    case GlobalContentStates.Opening:
                        State = GlobalContentStates.Showing;
                        break;
                    case GlobalContentStates.Closing:
                        State = GlobalContentStates.Idle;
                        CloseComplete();
                        break;
                    case GlobalContentStates.Idle:
                        break;
                    case GlobalContentStates.Showing:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        #endregion
    }
}
