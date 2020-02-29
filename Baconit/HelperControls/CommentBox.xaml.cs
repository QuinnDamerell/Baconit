using BaconBackend.Helpers;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using BaconBackend.Managers;

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
    public class CommentSubmittedArgs : EventArgs
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
        public event EventHandler<CommentSubmittedArgs> OnCommentSubmitted
        {
            add => _mOnCommentSubmitted.Add(value);
            remove => _mOnCommentSubmitted.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<CommentSubmittedArgs>> _mOnCommentSubmitted = new SmartWeakEvent<EventHandler<CommentSubmittedArgs>>();

        /// <summary>
        /// Fired after the box has been opened.
        /// </summary>
        public event EventHandler<CommentBoxOnOpenedArgs> OnBoxOpened
        {
            add => _mOnBoxOpened.Add(value);
            remove => _mOnBoxOpened.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<CommentBoxOnOpenedArgs>> _mOnBoxOpened = new SmartWeakEvent<EventHandler<CommentBoxOnOpenedArgs>>();

        /// <summary>
        /// Hold the current commenting id
        /// </summary>
        private string _mItemRedditId = "";

        /// <summary>
        /// Context for the current comment.
        /// </summary>
        private object _mContext;

        /// <summary>
        /// Indicates if this is an edit or not.
        /// </summary>
        private bool _mIsEdit;

        /// <summary>
        /// Indicate if we are open or not.
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// A timer used to update the preview
        /// </summary>
        private readonly DispatcherTimer _mPreviewTimer;

        /// <summary>
        /// Indicates the last time the text was edited.
        /// </summary>
        private DateTime _mLastTextEditTime = DateTime.MinValue;

        /// <summary>
        /// The amount of time from the last edit when we should update the preview again.
        /// </summary>
        private readonly int _mPreviewUpdateEditTimeout = 1000;

        public CommentBox()
        {
            InitializeComponent();

            _mPreviewTimer = new DispatcherTimer();
            _mPreviewTimer.Tick += PreviewTimer_Tick;

            // Set the interval and edit timeout. We will use the memory limit as a guess of what the
            // the device can handle.
            var memoryLimit = Windows.System.MemoryManager.AppMemoryUsageLimit / 1024 / 1024;
            if (memoryLimit < 250)
            {
                _mPreviewUpdateEditTimeout = 500;
            }
            else if (memoryLimit < 450)
            {
                _mPreviewUpdateEditTimeout = 200;
            }
            else if (memoryLimit < 1500)
            {
                _mPreviewUpdateEditTimeout = 100;
            }
            else
            {
                _mPreviewUpdateEditTimeout = 50;
            }
            _mPreviewTimer.Interval = new TimeSpan(0, 0, 0, 0, _mPreviewUpdateEditTimeout);

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

            if (string.IsNullOrWhiteSpace(redditId))
            {
                return;
            }

            // Do the pre-checks
            if (!ShowBoxInternalPreWork(redditId, editText, context))
            {
                return;
            }

            // Set the new reddit Id
            _mItemRedditId = redditId;

            // Do common code
            ShowBoxInternal();
        }

        /// <summary>
        /// Common code for show box
        /// </summary>
        private void ShowBoxInternal()
        {
            // Report
            TelemetryManager.ReportEvent(this, "CommentBoxOpened");

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
            _mPreviewTimer.Start();

            // Mark if this is an edit. Note if the string is empty we will
            // consider an edit for things like self posts with no text yet.
            var wasPastOpenEdit = _mIsEdit;
            _mIsEdit = editText != null;

            // Set the current context
            _mContext = context;

            // Update the button text
            ui_sendButton.Content = _mIsEdit ? "Update" : "Send";

            // If we are opening to a new item clear the text
            if (!_mItemRedditId.Equals(newRedditId) || wasPastOpenEdit != _mIsEdit)
            {
                // Set the text or empty if the id is new.
                ui_textBox.Text = string.IsNullOrWhiteSpace(editText) ? string.Empty : editText;
                _mLastTextEditTime = new DateTime(1989,4,19);
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
            _mPreviewTimer.Stop();

            // If we are done clear the reddit comment.
            if (isDoneWithComment)
            {
                _mItemRedditId = string.Empty;
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
            var args = new CommentBoxOnOpenedArgs
            {
                Context = _mContext,
                RedditId = _mItemRedditId
            };
            _mOnBoxOpened.Raise(this, args);
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

            var computedSize = ui_cancelButtonCol.ActualWidth - (ui_cancelButton.Margin.Left + ui_cancelButton.Margin.Right);
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
            var comment = ui_textBox.Text;
            if(string.IsNullOrWhiteSpace(comment) && !_mItemRedditId.StartsWith("t3_") && !_mIsEdit)
            {
                App.BaconMan.MessageMan.ShowMessageSimple("\"Silence is a source of great strength.\" - Lao Tzu", "Except on reddit. Go on, say something.");
                return;
            }

            // Show loading
            ui_sendingOverlayProgress.IsActive = true;
            VisualStateManager.GoToState(this, "ShowOverlay", true);

            // Try to send the comment
            var response = await Task.Run(() => MiscellaneousHelper.SendRedditComment(App.BaconMan, _mItemRedditId, comment, _mIsEdit));

            if (response != null)
            {
                // Now fire the event that a comment was submitted.
                try
                {
                    var args = new CommentSubmittedArgs
                    {
                        Response = response,
                        IsEdit = _mIsEdit,
                        RedditId = _mItemRedditId,
                        Context = _mContext,
                    };
                    _mOnCommentSubmitted.Raise(this, args);
                }
                catch (Exception ex)
                {
                    App.BaconMan.MessageMan.DebugDia("failed to fire OnCommentSubmitted", ex);
                    TelemetryManager.ReportUnexpectedEvent(this, "OnCommentSubmittedFireFailed", ex);
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
        private void LivePreviewBox_OnMarkdownLinkTapped(object sender, UniversalMarkdown.MarkdownLinkTappedArgs e)
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
            if(!_mLastTextEditTime.Equals(DateTime.MinValue))
            {
                var timeSinceEdit = DateTime.Now - _mLastTextEditTime;
                if (timeSinceEdit.TotalMilliseconds > _mPreviewUpdateEditTimeout)
                {
                    // Set the edit time so we won't come in again until an update.
                    // This is safe without locks bc this has to run on the UI thread and
                    // there is only one UI thread.
                    _mLastTextEditTime = DateTime.MinValue;

                    // Update the markdown text.
                    ui_livePreviewBox.Markdown = ui_textBox.Text;

                    // For some reason the SelectionStart count /r/n as 1 instead of two. So add one for each /r/n we find.
                    var selectionStart = ui_textBox.SelectionStart;
                    for (var count = 0; count < selectionStart; count++)
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
            _mLastTextEditTime = DateTime.Now;
        }

        #endregion

        #region Visual Helper

        /// <summary>
        /// Fired when the visual helper is tapped.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RedditMarkdownVisualHelper_OnHelperTapped(object sender, HelperTappedArgs e)
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
                ui_animFullscreenTextBoxGoTo.To = MaxHeight - (ActualHeight - ui_textBox.ActualHeight + (208 - ui_livePreviewGrid.ActualHeight));
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
