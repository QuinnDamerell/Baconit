using BaconBackend.DataObjects;
using Baconit.FlipViewControls;
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
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.HelperControls
{
    public sealed partial class GlobalContentPresenter : UserControl
    {
        enum State
        {
            Idle,
            Opening,
            Showing,
            Closing
        }

        /// <summary>
        /// Represents the current state of the presenter
        /// </summary>
        State m_state;

        /// <summary>
        /// Holds a reference to the content control
        /// </summary>
        FlipViewContentControl m_contentControl;

        public GlobalContentPresenter()
        {
            this.InitializeComponent();
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
                if(m_state != State.Idle)
                {
                    return;
                }
                m_state = State.Opening;
            }

            // Create the content control
            m_contentControl = new FlipViewContentControl();

            // This isn't great, but for now mock a post
            Post post = new Post() { Url = link, Id = "quinn" };

            // Add the control to the UI
            ui_contentRoot.Children.Add(m_contentControl);

            // Set the post to begin loading
            m_contentControl.FlipPost = post;
            m_contentControl.IsVisible = true;

            // Show the panel
            ToggleShown(true);
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
            if(m_contentControl != null)
            {
                m_contentControl.FlipPost = null;
            }
            m_contentControl = null;

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
            // Make sure we can close
            lock (this)
            {
                if (m_state != State.Showing)
                {
                    return;
                }
                m_state = State.Closing;
            }

            ToggleShown(false);
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
                await Windows.System.Launcher.LaunchUriAsync(new Uri(m_contentControl.FlipPost.Url, UriKind.Absolute));
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
                if (m_state == State.Opening)
                {
                    m_state = State.Showing;
                }
                else if (m_state == State.Closing)
                {
                    m_state = State.Idle;
                    CloseComplete();
                }
            }
        }

        #endregion
    }
}
