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

    /// <summary>
    /// Event args for the comment box opened event.
    /// </summary>
    public class CommentBoxOnOpenedArgs : EventArgs
    {
        public object Context;
        public string RedditId;
    }

    /// <summary>
    /// Args for the comment submitted event.
    /// </summary>
    public class OnCommentSubmittedArgs : EventArgs
    {
        public string Response;
        public string RedditId;
        public object Context;
        public bool IsEdit;
    }

    public sealed partial class CommentBox : UserControl
    {
        /// <summary>
        /// Fired when a comment is submitted and posted. Fired
        /// if the post fails or succeeds.
        /// </summary>
        public event EventHandler<OnCommentSubmittedArgs> OnCommentSubmitted
        {
            add { m_onCommentSubmitted.Add(value); }
            remove { m_onCommentSubmitted.Remove(value); }
        }
        SmartWeakEvent<EventHandler<OnCommentSubmittedArgs>> m_onCommentSubmitted = new SmartWeakEvent<EventHandler<OnCommentSubmittedArgs>>();

        /// <summary>
        /// Fired after the box has been opened.
        /// </summary>
        public event EventHandler<CommentBoxOnOpenedArgs> OnBoxOpened
        {
            add { m_onBoxOpened.Add(value); }
            remove { m_onBoxOpened.Remove(value); }
        }
        SmartWeakEvent<EventHandler<CommentBoxOnOpenedArgs>> m_onBoxOpened = new SmartWeakEvent<EventHandler<CommentBoxOnOpenedArgs>>();

        /// <summary>
        /// Hold the current commenting id
        /// </summary>
        string m_itemRedditId = "";

        /// <summary>
        /// Context for the current comment.
        /// </summary>
        object m_context = null;

        /// <summary>
        /// Indicates if this is an edit or not.
        /// </summary>
        bool m_isEdit = false;

        /// <summary>
        /// Indicate if we are open or not.
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// A timer used to update the preview
        /// </summary>
        DispatcherTimer m_previewTimer;

        /// <summary>
        /// Indicates the last time the text was edited.
        /// </summary>
        DateTime m_lastTextEditTime = DateTime.MinValue;

        /// <summary>
        /// The amount of time from the last edit when we should update the preview again.
        /// </summary>
        int m_previewUpdateEditTimeout = 1000;

        public CommentBox()
        {
            this.InitializeComponent();

            m_previewTimer = new DispatcherTimer();
            m_previewTimer.Tick += PreviewTimer_Tick;

            // Set the interval and edit timeout. We will use the memory limit as a guess of what the
            // the device can handle.
            ulong memoryLimit = Windows.System.MemoryManager.AppMemoryUsageLimit / 1024 / 1024;
            if (memoryLimit < 250)
            {
                m_previewUpdateEditTimeout = 500;
            }
            else if (memoryLimit < 450)
            {
                m_previewUpdateEditTimeout = 200;
            }
            else if (memoryLimit < 1500)
            {
                m_previewUpdateEditTimeout = 100;
            }
            else
            {
                m_previewUpdateEditTimeout = 50;
            }
            m_previewTimer.Interval = new TimeSpan(0, 0, 0, 0, m_previewUpdateEditTimeout);

            // Hide the box
            VisualStateManager.GoToState(this, "HideCommentBox", false);
            VisualStateManager.GoToState(this, "HideOverlay", false);
        }

        #region Animation Logic

        /// <summary>
        /// Called when the comment box should be shown. This take the reddit id of the thing
        /// commenting on and also any text that might have exited before.
        /// </summary>
        /// <param name="redditId"></param>
        public void ShowBox(string redditId, string editText = null, object context = null)
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

            // Do the pre-checks
            if (!ShowBoxInternalPreWork(redditId, editText, context))
            {
                return;
            }

            // Set the new reddit Id
            m_itemRedditId = redditId;

            // Do common code
            ShowBoxInternal();
        }

        /// <summary>
        /// Common code for show box
        /// </summary>
        private void ShowBoxInternal()
        {
           

            // Show the box
            VisualStateManager.GoToState(this, "ShowCommentBox", true);

            // Focus the text box
            ui_textBox.Focus(FocusState.Keyboard);
        }

        /// <summary>
        /// Common code for show box
        /// </summary>
        private bool ShowBoxInternalPreWork(string newRedditId, string editText, object context)
        {
            // If we are already open ignore this request.
            lock (this)
            {
                if (IsOpen)
                {
                    return false;
                }
                IsOpen = true;
            }

            // Start the preview timer
            m_previewTimer.Start();

            // Mark if this is an edit. Note if the string is empty we will
            // consider an edit for things like self posts with no text yet.
            bool wasPastOpenEdit = m_isEdit;
            m_isEdit = editText != null;

            // Set the current context
            m_context = context;

            // Update the button text
            ui_sendButton.Content = m_isEdit ? "Update" : "Send";

            // If we are opening to a new item clear the text
            if (!m_itemRedditId.Equals(newRedditId) || wasPastOpenEdit != m_isEdit)
            {
                // Set the text or empty if the id is new.
                ui_textBox.Text = String.IsNullOrWhiteSpace(editText) ? String.Empty : editText;
                m_lastTextEditTime = new DateTime(1989,4,19);
                PreviewTimer_Tick(null, null);
            }

            return true;
        }

        /// <summary>
        /// Called when the box should be hidden
        /// </summary>
        public void HideBox(bool isDoneWithComment = false)
        {
            // Indicate we are closed.
            lock (this)
            {
                IsOpen = false;
            }

            // Stop the preview timer
            m_previewTimer.Stop();

            // If we are done clear the reddit comment.
            if (isDoneWithComment)
            {
                m_itemRedditId = String.Empty;
            }

            // Hide the box
            VisualStateManager.GoToState(this, "HideCommentBox", true);

            // Note: don't clear the text just yet, they might come back if it closed
            // accidentally.
        }

        /// <summary>
        /// Public hides the overlay if opened.
        /// </summary>
        public void HideLoadingOverlay()
        {
            VisualStateManager.GoToState(this, "HideOverlay", true);
        }

        /// <summary>
        /// Fired when the overlay is hidden.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HideOverlay_Completed(object sender, object e)
        {
            ui_sendingOverlayProgress.IsActive = false;
        }

        /// <summary>
        /// Fired when the box hide is complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HideCommentBox_Completed(object sender, object e)
        {
            // Make sure the overlay is hidden
            VisualStateManager.GoToState(this, "HideOverlay", true);

            // Remove full screen.
            ToggleFullscreen(false);
        }

        /// <summary>
        /// Fired when the show is complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowCommentBox_Completed(object sender, object e)
        {
            // Fire the event.
            CommentBoxOnOpenedArgs args = new CommentBoxOnOpenedArgs()
            {
                Context = m_context,
                RedditId = m_itemRedditId
            };
            m_onBoxOpened.Raise(this, args);
        }

        /// <summary>
        /// Fired when the button holder changes sizes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Since we want these to be *, but also float to the center, but also stretch the button we have to do
            // this manually. Figure out how large the button should be, and then set it.

            double computedSize = ui_cancelButtonCol.ActualWidth - (ui_cancelButton.Margin.Left + ui_cancelButton.Margin.Right);
            ui_cancelButton.Width = computedSize < 200 ? computedSize : 200;
            computedSize = ui_sendButtonCol.ActualWidth - (ui_sendButton.Margin.Left + ui_sendButton.Margin.Right);
            ui_sendButton.Width = computedSize < 200 ? computedSize : 200;
        }

        #endregion

        #region Click Actions

        /// <summary>
        /// Called when the box should be closed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            HideBox();
        }

        /// <summary>
        /// Called when the send button is tapped.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            // Make sure we have a string unless we are editing a post selftext, then we can have an empty comment.
            string comment = ui_textBox.Text;
            if(String.IsNullOrWhiteSpace(comment) && !m_itemRedditId.StartsWith("t3_") && !m_isEdit)
            {
                App.BaconMan.MessageMan.ShowMessageSimple("\"Silence is a source of great strength.\" - Lao Tzu", "Except on reddit. Go on, say something.");
                return;
            }

            // Show loading
            ui_sendingOverlayProgress.IsActive = true;
            VisualStateManager.GoToState(this, "ShowOverlay", true);

            // Try to send the comment
            string response = await Task.Run(() => MiscellaneousHelper.SendRedditComment(App.BaconMan, m_itemRedditId, comment, m_isEdit));

            if (response != null)
            {
                // Now fire the event that a comment was submitted.
                try
                {
                    OnCommentSubmittedArgs args = new OnCommentSubmittedArgs()
                    {
                        Response = response,
                        IsEdit = m_isEdit,
                        RedditId = m_itemRedditId,
                        Context = m_context,
                    };
                    m_onCommentSubmitted.Raise(this, args);
                }
                catch (Exception ex)
                {
                    App.BaconMan.MessageMan.DebugDia("failed to fire OnCommentSubmitted", ex);
                   
                }
            }
            else
            {
                // The network call failed.
                App.BaconMan.MessageMan.ShowMessageSimple("Can't Submit", "We can't seem to submit this right now, check your internet connection.");
                HideLoadingOverlay();
            }
        }

        #endregion

        #region Markdown Preview

        /// <summary>
        /// Fired when a link is tapped in the markdown.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LivePreviewBox_OnMarkdownLinkTapped(object sender, UniversalMarkdown.OnMarkdownLinkTappedArgs e)
        {
            App.BaconMan.ShowGlobalContent(e.Link);
        }

        /// <summary>
        /// Fired when the preview timer ticks.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PreviewTimer_Tick(object sender, object e)
        {
            // Make sure we have a new edit.
            if(!m_lastTextEditTime.Equals(DateTime.MinValue))
            {
                TimeSpan timeSinceEdit = DateTime.Now - m_lastTextEditTime;
                if (timeSinceEdit.TotalMilliseconds > m_previewUpdateEditTimeout)
                {
                    // Set the edit time so we won't come in again until an update.
                    // This is safe without locks bc this has to run on the UI thread and
                    // there is only one UI thread.
                    m_lastTextEditTime = DateTime.MinValue;

                    // Update the markdown text.
                    ui_livePreviewBox.Markdown = ui_textBox.Text;

                    // For some reason the SelectionStart count /r/n as 1 instead of two. So add one for each /r/n we find.
                    int selectionStart = ui_textBox.SelectionStart;
                    for (int count = 0; count < selectionStart; count++)
                    {
                        if (ui_textBox.Text[count] == '\r' && count + 1 < ui_textBox.Text.Length && ui_textBox.Text[count + 1] == '\n')
                        {
                            selectionStart++;
                        }
                    }

                    // If the entry point is at the bottom of the text box, scroll the preview
                    if (Math.Abs(selectionStart - ui_textBox.Text.Length) < 30)
                    {
                        ui_livePreviewScroller.ChangeView(null, ui_livePreviewScroller.ScrollableHeight, null);
                    }
                }
            }
        }

        /// <summary>
        /// Fired whenever text is edited.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void TextBox_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            // Update the last edit time.
            m_lastTextEditTime = DateTime.Now;
        }

        #endregion

        #region Visual Helper

        /// <summary>
        /// Fired when the visual helper is tapped.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RedditMarkdownVisualHelper_OnHelperTapped(object sender, OnHelperTappedArgs e)
        {
            if(e.Type == VisualHelperTypes.Fullscreen)
            {
                ToggleFullscreen(ui_visualHelper.FullscreenStatus == VisualHelperFullscreenStatus.GotoFullscreen);
            }
            else
            {
                RedditMarkdownVisualHelper.DoEdit(ui_textBox, e.Type);
            }
        }

        #endregion

        #region Full Screen Logic

        public void ToggleFullscreen(bool goToFullscreen)
        {
            if (goToFullscreen)
            {
                ui_visualHelper.FullscreenStatus = VisualHelperFullscreenStatus.RestoreFromFullscreen;
                // Figure out about how high the box should be. We should shoot over, it might make the animation look a little odd but it will be ok.
                ui_animFullscreenTextBoxGoTo.To = this.MaxHeight - (this.ActualHeight - ui_textBox.ActualHeight + (208 - ui_livePreviewGrid.ActualHeight));
                VisualStateManager.GoToState(this, "GoFullscreen", true);
            }
            else
            {
                ui_visualHelper.FullscreenStatus = VisualHelperFullscreenStatus.GotoFullscreen;
                // Grab the actual height right now to animate back into.
                ui_animFullscreenTextBoxRestore.From = ui_textBox.ActualHeight;
                VisualStateManager.GoToState(this, "RestoreFullscreen", true);
            }
        }

        #endregion
    }
}
