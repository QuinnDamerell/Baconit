using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.HelperControls
{
    /// <summary>
    /// Event args for the comment box state changed event.
    /// </summary>
    public class CommentBoxSizeChangedEvent : EventArgs
    {
        public bool IsOpen;
        public double BoxHeight;
    }

    public sealed partial class CommentBox : UserControl
    {
        /// <summary>
        /// Hold the current commenting id
        /// </summary>
        string m_itemRedditId = "";

        /// <summary>
        /// Holds the post that is the parent of the comment;
        /// </summary>
        Post m_parentPost;

        /// <summary>
        /// Indicate if we are open or not.
        /// </summary>
        public bool IsOpen { get { return m_isOpen; } }
        bool m_isOpen = false;

        /// <summary>
        /// Indicates if the live preview is open.
        /// </summary>
        bool m_isShowingLivePreview = false;

        public CommentBox()
        {
            this.InitializeComponent();

            // Hide the box
            VisualStateManager.GoToState(this, "HideCommentBox", false);
            VisualStateManager.GoToState(this, "HideOverlay", false);
            ToggleLivePreview(false, false);
        }

        #region Animation Logic

        /// <summary>
        /// Called when the comment box should be opened, this is used by the message inbox to send messages.
        /// </summary>
        /// <param name="redditId"></param>
        public void ShowBox(string redditId)
        {
            // Make sure we are logged in
            if (!App.BaconMan.UserMan.IsUserSignedIn)
            {
                App.BaconMan.MessageMan.ShowSigninMessage("comment");
                return;
            }

            if (String.IsNullOrWhiteSpace(redditId))
            {
                return;
            }

            // Make sure we can comment on it
            if (!redditId.StartsWith("t1_") && !redditId.StartsWith("t4_"))
            {
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "OpenedWithoutRedditId");
                return;
            }

            // Do the pre-checks
            if (!ShowBoxInternalPreWork(redditId))
            {
                return;
            }

            // Set the new reddit Id
            m_itemRedditId = redditId;

            // Do common code
            ShowBoxInternal();
        }


        /// <summary>
        /// Called when the comment box should be opened
        /// </summary>
        /// <param name="redditId"></param>
        public void ShowBox(Post parentPost, string redditId)
        {
            // Make sure we are logged in
            if(!App.BaconMan.UserMan.IsUserSignedIn)
            {
                App.BaconMan.MessageMan.ShowSigninMessage("comment");
                return;
            }

            if(parentPost == null || String.IsNullOrWhiteSpace(redditId))
            {
                return;
            }

            // Make sure we can comment on it
            if(!redditId.StartsWith("t1_") && !redditId.StartsWith("t3_"))
            {
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "OpenedWithOutComment");
                return;
            }

            // Do the pre-checks
            if(!ShowBoxInternalPreWork(redditId))
            {
                return;
            }

            // Hang on to the commenting id.
            m_itemRedditId = redditId;
            m_parentPost = parentPost;

            // Do common code
            ShowBoxInternal();
        }

        /// <summary>
        /// Common code for show box
        /// </summary>
        private void ShowBoxInternal()
        {
            // Hide the preview
            ToggleLivePreview(false, false);

            // Report
            App.BaconMan.TelemetryMan.ReportEvent(this, "CommentBoxOpened");

            // Show the box
            VisualStateManager.GoToState(this, "ShowCommentBox", true);

            // Focus the text box
            ui_textBox.Focus(FocusState.Keyboard);
        }

        /// <summary>
        /// Common code for show box
        /// </summary>
        private bool ShowBoxInternalPreWork(string newRedditId)
        {
            // If we are already open ignore this request.
            lock (this)
            {
                if (m_isOpen)
                {
                    return false;
                }
                m_isOpen = true;
            }

            // If we are opening to a new item clear the text
            if (!m_itemRedditId.Equals(newRedditId))
            {
                ui_textBox.Text = "";
            }

            return true;
        }

        /// <summary>
        /// Called when the box should be hidden
        /// </summary>
        public void HideBox()
        {
            // Indicate we are closed.
            lock (this)
            {
                m_isOpen = false;
            }

            // Hide the box
            VisualStateManager.GoToState(this, "HideCommentBox", true);

            // Note: don't clear the text just yet, they might come back if it closed
            // accidentally.
        }

        private void HideOverlay_Completed(object sender, object e)
        {
            ui_sendingOverlayProgress.IsActive = false;
        }

        #endregion

        #region Click Actions

        /// <summary>
        /// Called when the box should be closed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Close_OnIconTapped(object sender, EventArgs e)
        {
            HideBox();
        }

        /// <summary>
        /// Called when the send button is tapped.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Send_OnIconTapped(object sender, EventArgs e)
        {
            string comment = ui_textBox.Text;
            if(String.IsNullOrWhiteSpace(comment))
            {
                App.BaconMan.MessageMan.ShowMessageSimple("\"Silence is a source of great strength.\" - Lao Tzu", "Except on reddit. Go on, say something.");
                return;
            }

            // Show loading
            ui_sendingOverlayProgress.IsActive = true;
            VisualStateManager.GoToState(this, "ShowOverlay", true);

            // Try to send the comment
            string response = await Task.Run(() => MiscellaneousHelper.SendRedditComment(App.BaconMan, m_itemRedditId, comment));

            // If we have a post send the status to the collector
            bool wasSuccess = false;
            if (m_parentPost != null)
            {
                // Get the comment collector for this post.
                CommentCollector collector = CommentCollector.GetCollector(m_parentPost, App.BaconMan);

                // Tell the collector of the change, this will show any message boxes needed.
                wasSuccess = await Task.Run(() => collector.AddNewUserComment(response, m_itemRedditId));
            }
            else
            {
                // If we are here we are a message. Validate for ourselves.
                // #todo, make this validation better, add capta support. Both responses to messages and post replies will have "author"
                if(!String.IsNullOrWhiteSpace(response) && response.Contains("author"))
                {
                    wasSuccess = true;
                }
                else
                {
                    wasSuccess = false;
                    App.BaconMan.MessageMan.ShowMessageSimple("That's Not Right", "We can't send this message right now, check your Internet connection");
                }
            }


            if(wasSuccess)
            {
                // Clear the reddit id so we won't open back up with the
                // same text
                m_itemRedditId = "";

                // Hide the comment box.
                HideBox();
            }
            else
            {
                // Hide the overlay
                VisualStateManager.GoToState(this, "HideOverlay", true);
            }
        }

        #endregion

        #region Real Time Preview

        private void ToggleLivePreview_OnIconTapped(object sender, EventArgs e)
        {
            ToggleLivePreview(!m_isShowingLivePreview);
        }

        private void ToggleLivePreview_TextTapped(object sender, TappedRoutedEventArgs e)
        {
            ToggleLivePreview(!m_isShowingLivePreview);
        }

        public void ToggleLivePreview(bool show, bool animate = true)
        {
            // Set the bool
            m_isShowingLivePreview = show;

            // Set the text only if we are showing or not animating.
            if (show || !animate)
            {
                TextBox_TextChanged(null, null);
            }

            if (show)
            {
                // Show
                VisualStateManager.GoToState(this, "ShowLivePreview", animate);
            }
            else
            {
                // Hide
                VisualStateManager.GoToState(this, "HideLivePreview", animate);
            }
        }


        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (m_isShowingLivePreview)
            {
                // Set the text
                ui_livePreviewBox.Markdown = ui_textBox.Text;

                // If the entry point is at the bottom of the text box, scroll the preview
                if(Math.Abs(ui_textBox.SelectionStart - ui_textBox.Text.Length) < 30)
                {
                    ui_livePreviewScroller.ChangeView(null, ui_livePreviewScroller.ScrollableHeight, null);
                }
            }
            else
            {
                ui_livePreviewBox.Markdown = "";
            }
        }

        #endregion

        private void LivePreviewBox_OnMarkdownLinkTapped(object sender, UniversalMarkdown.OnMarkdownLinkTappedArgs e)
        {
            App.BaconMan.ShowGlobalContent(e.Link);
        }
    }
}
