using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.Panels
{
    public sealed partial class MessageInbox : UserControl, IPanel
    {
        IPanelHost m_panelHost;
        MessageCollector m_collector;
        ObservableCollection<Message> m_messageList = new ObservableCollection<Message>();

        public MessageInbox()
        {
            this.InitializeComponent();

            // Hide the box
            ui_replyBox.HideBox();

            // Set the list source.
            ui_messageList.ItemsSource = m_messageList;
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            // Grab the panel manager
            m_panelHost = host;

            // Make a new collector
            m_collector = new MessageCollector(App.BaconMan);

            // Sub to the collector callbacks
            m_collector.OnCollectionUpdated += Collector_OnCollectionUpdated;
            m_collector.OnCollectorStateChange += Collector_OnCollectorStateChange;

            // Ask the collector to update
            m_collector.Update();
        }

        public void OnNavigatingFrom()
        {

        }

        public async void OnNavigatingTo()
        {
            // Ask the collector to update
            m_collector.Update(true);

            // Set the status bar color and get the size returned. If it is not 0 use that to move the
            // color of the page into the status bar.
            double statusBarHeight = await m_panelHost.SetStatusBar(null, 0);
            ui_contentRoot.Margin = new Thickness(0, -statusBarHeight, 0, 0);
            ui_contentRoot.Padding = new Thickness(0, statusBarHeight, 0, 0);
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            OnNavigatingTo();
        }

        #region Collector callbacks

        private async void Collector_OnCollectorStateChange(object sender, OnCollectorStateChangeArgs e)
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

        private async void Collector_OnCollectionUpdated(object sender, OnCollectionUpdatedArgs<Message> e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Setup the insert
                int insertIndex = e.StartingPosition;

                // Lock the list
                lock (m_messageList)
                {
                    // Set up the objects for the UI
                    foreach (Message message in e.ChangedItems)
                    {
                        // Check if we are adding or inserting.
                        bool isReplace = insertIndex < m_messageList.Count;

                        if (isReplace)
                        {
                            if (m_messageList[insertIndex].Id.Equals(message.Id))
                            {
                                // If the message is the same just update the UI vars
                                m_messageList[insertIndex].Body = message.Body;
                                m_messageList[insertIndex].IsNew = message.IsNew;
                            }
                            else
                            {
                                // Replace the current item
                                m_messageList[insertIndex] = message;
                            }
                        }
                        else
                        {
                            // Add it to the end
                            m_messageList.Add(message);
                        }
                        insertIndex++;
                    }

                    // If it was a fresh update, remove anything past the last story sent.
                    while (e.IsFreshUpdate && m_messageList.Count > e.ChangedItems.Count)
                    {
                        m_messageList.RemoveAt(m_messageList.Count - 1);
                    }
                }
            });
        }

        #endregion

        #region Click actions

        private void MessageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Clear the selection.
            ui_messageList.SelectedIndex = -1;
        }

        private void Reply_Tapped(object sender, EventArgs e)
        {
            // Get the message
            Message message = (Message)((FrameworkElement)sender).DataContext;
            ui_replyBox.ShowBox(message.GetFullName());

            // Also if it is unread set it to read
            if(message.IsNew)
            {
                MarkAsRead_Tapped(sender, e);
            }
        }

        private void MarkAsRead_Tapped(object sender, EventArgs e)
        {
            // Get the message
            Message message = (Message)((FrameworkElement)sender).DataContext;

            // Flip it
            m_collector.ChangeMessageReadStatus(message, !message.IsNew);
        }

        private void ViewContext_OnButtonTapped(object sender, EventArgs e)
        {
            // Get the message
            Message message = (Message)((FrameworkElement)sender).DataContext;

            // We need to get all of the parts of this message we need to send to flip view
            // The comment replies only have the post id in the "context" so use that for both.
            string postId = null;
            try
            {
                string context = message.Context;
                int contextIndex = context.IndexOf('/');
                int slashSeenCount = 0;
                while(contextIndex != -1)
                {
                    slashSeenCount++;
                    // Iterate one past the /
                    contextIndex++;

                    if (slashSeenCount == 4)
                    {
                        // After 4 slashes we should have the post id

                        int nextSlash = context.IndexOf('/', contextIndex);
                        postId = context.Substring(contextIndex, nextSlash - contextIndex);
                        break;
                    }

                    contextIndex = context.IndexOf('/', contextIndex);
                }

                if(String.IsNullOrEmpty(postId))
                {
                    throw new Exception("post id was empty");
                }
            }
            catch(Exception ex)
            {
                App.BaconMan.MessageMan.DebugDia("failed to parse message context", ex);
                App.BaconMan.MessageMan.ShowMessageSimple("Oops", "Something is wrong and we can't show this context right now.");
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "failedToParseMessageContextString", ex);
                return;
            }

            // Navigate flip view and force it to the post and comment.
            Dictionary<string, object> args = new Dictionary<string, object>();
            args.Add(PanelManager.NAV_ARGS_SUBREDDIT_NAME, message.Subreddit);
            args.Add(PanelManager.NAV_ARGS_FORCE_POST_ID, postId);
            args.Add(PanelManager.NAV_ARGS_FORCE_COMMENT_ID, message.Id);

            // Make sure the page Id is unique
            m_panelHost.Navigate(typeof(FlipViewPanel), message.Subreddit + SortTypes.Hot + SortTimeTypes.Week + postId + message.Id, args);

            // Also if it is unread set it to read
            if (message.IsNew)
            {
                MarkAsRead_Tapped(sender, e);
            }
        }

        #endregion

        public void ToggleProgressBar(bool show)
        {
            if(show)
            {
                if (m_messageList.Count == 0)
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

        private void MarkdownTextBlock_OnMarkdownLinkTapped(object sender, UniversalMarkdown.OnMarkdownLinkTappedArgs e)
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
            m_collector.Update(true);
        }
    }
}
