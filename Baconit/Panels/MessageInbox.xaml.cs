using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using Baconit.HelperControls;
using Baconit.Interfaces;
using Baconit.Panels.FlipView;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using BaconBackend.Managers;
using Microsoft.Toolkit.Uwp.UI.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.Panels
{
    public sealed partial class MessageInbox : UserControl, IPanel
    {
        private IPanelHost _mPanelHost;
        private MessageCollector _mCollector;
        private readonly ObservableCollection<Message> _mMessageList = new ObservableCollection<Message>();

        public MessageInbox()
        {
            InitializeComponent();

            // Hide the box
            ui_replyBox.HideBox();

            // Set the list source.
            ui_messageList.ItemsSource = _mMessageList;
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            // Grab the panel manager
            _mPanelHost = host;

            // Make a new collector
            _mCollector = new MessageCollector(App.BaconMan);

            // Sub to the collector callbacks
            _mCollector.OnCollectionUpdated += Collector_OnCollectionUpdated;
            _mCollector.OnCollectorStateChange += Collector_OnCollectorStateChange;

            // Ask the collector to update
            _mCollector.Update();
        }

        public void OnNavigatingFrom()
        {

        }

        public async void OnNavigatingTo()
        {
            // Ask the collector to update
            _mCollector.Update(true);

            // Set the status bar color and get the size returned. If it is not 0 use that to move the
            // color of the page into the status bar.
            var statusBarHeight = await _mPanelHost.SetStatusBar(null, 0);
            ui_contentRoot.Margin = new Thickness(0, -statusBarHeight, 0, 0);
            ui_contentRoot.Padding = new Thickness(0, statusBarHeight, 0, 0);
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            OnNavigatingTo();
        }

        public void OnCleanupPanel()
        {
            // Ignore for now.
        }

        /// <summary>
        /// Fired when the panel should try to reduce memory if possible. This will only be called
        /// while the panel isn't visible.
        /// </summary>
        public void OnReduceMemory()
        {
            // Ignore for now.
        }

        #region Collector callbacks

        private async void Collector_OnCollectorStateChange(object sender, CollectorStateChangeArgs e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Show or hide the progress bar
                ToggleProgressBar(e.State == CollectorState.Updating || e.State == CollectorState.Extending);

                if(e.State == CollectorState.Error && e.ErrorState == CollectorErrorState.ServiceDown)
                {
                    App.BaconMan.MessageMan.ShowRedditDownMessage();
                }
            });
        }

        private async void Collector_OnCollectionUpdated(object sender, CollectionUpdatedArgs<Message> e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Setup the insert
                var insertIndex = e.StartingPosition;

                // Lock the list
                lock (_mMessageList)
                {
                    // Set up the objects for the UI
                    foreach (var message in e.ChangedItems)
                    {
                        // Check if we are adding or inserting.
                        var isReplace = insertIndex < _mMessageList.Count;

                        if (isReplace)
                        {
                            if (_mMessageList[insertIndex].Id.Equals(message.Id))
                            {
                                // If the message is the same just update the UI vars
                                _mMessageList[insertIndex].Body = message.Body;
                                _mMessageList[insertIndex].IsNew = message.IsNew;
                            }
                            else
                            {
                                // Replace the current item
                                _mMessageList[insertIndex] = message;
                            }
                        }
                        else
                        {
                            // Add it to the end
                            _mMessageList.Add(message);
                        }
                        insertIndex++;
                    }

                    // If it was a fresh update, remove anything past the last story sent.
                    while (e.IsFreshUpdate && _mMessageList.Count > e.ChangedItems.Count)
                    {
                        _mMessageList.RemoveAt(_mMessageList.Count - 1);
                    }
                }
            });
        }

        #endregion

        #region Click actions

        /// <summary>
        /// Fired whent the list view selected item is changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MessageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Clear the selection.
            ui_messageList.SelectedIndex = -1;
        }

        /// <summary>
        /// Fired when a user taps reply on a message in the list.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Reply_Tapped(object sender, EventArgs e)
        {
            // Get the message
            var message = (Message)((FrameworkElement)sender).DataContext;
            ui_replyBox.ShowBox(message.GetFullName(), null, message);

            // Also if it is unread set it to read
            if(message.IsNew)
            {
                MarkAsRead_Tapped(sender, e);
            }
        }

        /// <summary>
        /// Fired when a user taps marked as read.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MarkAsRead_Tapped(object sender, EventArgs e)
        {
            // Get the message
            var message = (Message)((FrameworkElement)sender).DataContext;

            // Flip it
            _mCollector.ChangeMessageReadStatus(message, !message.IsNew);
        }

        /// <summary>
        /// Fired when a user taps view context.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ViewContext_OnButtonTapped(object sender, EventArgs e)
        {
            // Get the message
            var message = (Message)((FrameworkElement)sender).DataContext;

            // We need to get all of the parts of this message we need to send to flip view
            // The comment replies only have the post id in the "context" so use that for both.
            string postId = null;
            try
            {
                var context = message.Context;
                var contextIndex = context.IndexOf('/');
                var slashSeenCount = 0;
                while(contextIndex != -1)
                {
                    slashSeenCount++;
                    // Iterate one past the /
                    contextIndex++;

                    if (slashSeenCount == 4)
                    {
                        // After 4 slashes we should have the post id

                        var nextSlash = context.IndexOf('/', contextIndex);
                        postId = context.Substring(contextIndex, nextSlash - contextIndex);
                        break;
                    }

                    contextIndex = context.IndexOf('/', contextIndex);
                }

                if(string.IsNullOrEmpty(postId))
                {
                    throw new Exception("post id was empty");
                }
            }
            catch(Exception ex)
            {
                App.BaconMan.MessageMan.DebugDia("failed to parse message context", ex);
                App.BaconMan.MessageMan.ShowMessageSimple("Oops", "Something is wrong and we can't show this context right now.");
                TelemetryManager.ReportUnexpectedEvent(this, "failedToParseMessageContextString", ex);
                return;
            }

            // Navigate flip view and force it to the post and comment.
            var args = new Dictionary<string, object>();
            args.Add(PanelManager.NavArgsSubredditName, message.Subreddit);
            args.Add(PanelManager.NavArgsForcePostId, postId);
            args.Add(PanelManager.NavArgsForceCommentId, message.Id);

            // Make sure the page Id is unique
            _mPanelHost.Navigate(typeof(FlipViewPanel), message.Subreddit + SortTypes.Hot + SortTimeTypes.Week + postId + message.Id, args);

            // Also if it is unread set it to read
            if (message.IsNew)
            {
                MarkAsRead_Tapped(sender, e);
            }
        }

        #endregion

        #region Reply Box

        /// <summary>
        /// Fired when the comment box is open.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>

        private void ReplyBox_OnBoxOpened(object sender, CommentBoxOnOpenedArgs e)
        {
            // Scroll the message we are responding into view.
            ui_messageList.ScrollIntoView((Message)e.Context);
        }

        /// <summary>
        /// Fired when a new message has been submitted.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReplyBox_OnCommentSubmitted(object sender, CommentSubmittedArgs e)
        {
            // Validate the response, this isn't 100% fool proof but it is close.
            if(e.Response.Contains("\"author\":"))
            {
                // We are good, hide the box
                ui_replyBox.HideBox();
            }
            else
            {
                ui_replyBox.HideLoadingOverlay();
                App.BaconMan.MessageMan.ShowMessageSimple("That's Not Good", "We can't send this message right now, reddit returned an unexpected result. Try again later.");
            }
        }

        /// <summary>
        /// Fired when the control changes sizes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // We need to update the max size of the reply box otherwise it will extend out
            // of the current grid. This is because it is set to auto, which means "you can be as big as you want"
            // and the grid won't force a size on it.
            ui_replyBox.MaxHeight = e.NewSize.Height - ui_messageHeader.ActualHeight - ui_contentRoot.Padding.Top;
        }

        #endregion

        public void ToggleProgressBar(bool show)
        {
            if(show)
            {
                if (_mMessageList.Count == 0)
                {
                    ui_progressRing.Visibility = Visibility.Visible;
                    ui_progressRing.IsActive = true;
                }
                else
                {
                    ui_progressBar.Visibility = Visibility.Visible;
                    ui_progressBar.IsIndeterminate = true;
                }
            }
            else
            {
                ui_progressRing.Visibility = Visibility.Collapsed;
                ui_progressBar.Visibility = Visibility.Collapsed;
                ui_progressRing.IsActive = false;
                ui_progressBar.IsIndeterminate = false;
            }
        }

        /// <summary>
        /// Fired when a link is tapped in the markdown.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MarkdownTextBlock_OnMarkdownLinkTapped(object sender, UniversalMarkdown.MarkdownLinkTappedArgs e)
        {
            App.BaconMan.ShowGlobalContent(e.Link);
        }

        /// <summary>
        /// Fired when the refresh button is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            // Ask the collector to update
            _mCollector.Update(true);
        }
    }
}
