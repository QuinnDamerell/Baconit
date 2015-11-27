using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using Baconit.Interfaces;
using Baconit.Panels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
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
    public sealed partial class SubredditSideBar : UserControl
    {
        /// <summary>
        /// The panel host
        /// </summary>
        IPanelHost m_host;

        /// <summary>
        /// The current subreddit we are showing
        /// </summary>
        Subreddit m_currentSubreddit;

        /// <summary>
        /// A brush for active buttons
        /// </summary>
        SolidColorBrush m_buttonActive;

        /// <summary>
        /// A brush for inactive buttons
        /// </summary>
        SolidColorBrush m_buttonInactive;

        /// <summary>
        /// Fired when the sidebar should be closed because we are navigating somewhere.
        /// </summary>
        public event EventHandler<EventArgs> OnShouldClose
        {
            add { m_onShouldClose.Add(value); }
            remove { m_onShouldClose.Remove(value); }
        }
        SmartWeakEvent<EventHandler<EventArgs>> m_onShouldClose = new SmartWeakEvent<EventHandler<EventArgs>>();


        public SubredditSideBar()
        {
            this.InitializeComponent();

            // Get the accent color
            Color accentColor = ((SolidColorBrush)App.Current.Resources["SystemControlBackgroundAccentBrush"]).Color;

            // Set the title color
            Color darkAccent = accentColor;
            darkAccent.A = 170;
            ui_titleHeaderContainer.Background = new SolidColorBrush(darkAccent);

            // Set the button container background
            Color darkerAccent = accentColor;
            darkerAccent.A = 90;
            ui_buttonContainer.Background = new SolidColorBrush(darkerAccent);

            // Set the active button background
            Color buttonActive = accentColor;
            buttonActive.A = 200;
            m_buttonActive = new SolidColorBrush(buttonActive);

            // Get the button inactive color
            Color buttonAccent = accentColor;
            buttonAccent.A = 90;
            m_buttonInactive = new SolidColorBrush(buttonAccent);

            // Set the submit link and text colors
            ui_submitPostButton.Background = m_buttonInactive;
            ui_searchSubreddit.Background = m_buttonInactive;
        }

        /// <summary>
        /// Fired when we should show a subreddit.
        /// </summary>
        /// <param name="subreddit"></param>
        public void SetSubreddit(IPanelHost host, Subreddit subreddit)
        {
            // Capture host
            m_host = host;

            // Make sure we don't already have it.
            if (m_currentSubreddit != null && m_currentSubreddit.Id.Equals(subreddit.Id))
            {
                // Update the buttons
                SetSubButton();
                SetSearchButton();
                SetPinButton();

                // Scroll the scroller to the top
                ui_contentRoot.ChangeView(null, 0, null, true);

                return;
            }

            // Set the current subreddit
            m_currentSubreddit = subreddit;

            // Set the title
            ui_titleTextBlock.Text = m_currentSubreddit.DisplayName;

            // Set the subs, if the value doesn't exist or the subreddit is fake make it "many"
            if (m_currentSubreddit.SubscriberCount.HasValue && !m_currentSubreddit.IsArtifical)
            {
                ui_subscribersTextBlock.Text = String.Format("{0:N0}", m_currentSubreddit.SubscriberCount) + " subscribers";
            }
            else
            {
                ui_subscribersTextBlock.Text = "many subscribers";
            }

            // Set the subscribe button
            SetSubButton();
            SetPinButton();
            SetSearchButton();

            // Set the markdown
            SetMarkdown();
        }

        #region Subscribe button

        /// <summary>
        /// Setups the sub button
        /// </summary>
        private void SetSubButton(bool? forcedState = null)
        {
            // If the subreddit is artifical disable this button
            if(m_currentSubreddit.IsArtifical)
            {
                ui_subscribeButton.IsEnabled = false;
            }

            // If we are being forced to show something show it, if not check if the user is subed or not.
            bool isSubed = forcedState.HasValue ? forcedState.Value : App.BaconMan.SubredditMan.IsSubredditSubscribedTo(m_currentSubreddit.DisplayName);

            // Set the text
            ui_subscribeButton.Content = isSubed ? "unsubscribe" : "subscribe";

            // Set the button color
            ui_subscribeButton.Background = isSubed ? m_buttonActive : m_buttonInactive;
        }

        /// <summary>
        /// Fired when the user taps the subscribe button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SubscribeButton_Click(object sender, RoutedEventArgs e)
        {
            // Ensure we are signed in.
            // #todo should we disable the button if they aren't signed in?
            if (!App.BaconMan.UserMan.IsUserSignedIn)
            {
                App.BaconMan.MessageMan.ShowSigninMessage("subscribe to reddits");
                return;
            }

            // Report
            App.BaconMan.TelemetryMan.ReportEvent(this, "SubredditSubscribeTapped");

            // Use the text here, this can be risky but it makes sure we do a action that matches the UI.
            bool subscribe = ui_subscribeButton.Content.Equals("subscribe");

            // Update the button now so the UI responds
            SetSubButton(subscribe);

            // Make the request
            bool success = await App.BaconMan.SubredditMan.ChangeSubscriptionStatus(m_currentSubreddit.Id, subscribe);

            if (!success)
            {
                App.BaconMan.MessageMan.ShowMessageSimple("Oops", "We can't subscribe or unsubscribe to this subreddit right now. Check your Internet connection.");

                // Fix the button
                SetSubButton(!subscribe);
            }
        }

        #endregion

        #region Pin To Start Button

        /// <summary>
        /// Setups the pin button
        /// </summary>
        private void SetPinButton(bool? forcedState = null)
        {
            // If we are being forced to show something show it, if not check if the user is subed or not.
            bool isPinned = forcedState.HasValue ? forcedState.Value : App.BaconMan.TileMan.IsSubredditPinned(m_currentSubreddit.DisplayName);

            // Set the text
            ui_pinToStartButton.Content = isPinned ? "unpin from start" : "pin to start";

            // Set the button color
            ui_pinToStartButton.Background = isPinned ? m_buttonActive : m_buttonInactive;
        }

        /// <summary>
        /// Fired when the user taps the pin button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void PinButton_Click(object sender, RoutedEventArgs e)
        {
            // Use the text here, this can be risky but it makes sure we do a action that matches the UI.
            bool pin = ui_pinToStartButton.Content.Equals("pin to start");

            // Report
            App.BaconMan.TelemetryMan.ReportEvent(this, "SubPinToStartTapped");

            // Update the button now so the UI responds
            SetPinButton(pin);

            // Make the request
            bool success = false;
            if(pin)
            {
                success = await App.BaconMan.TileMan.CreateSubredditTile(m_currentSubreddit);
            }
            else
            {
                success = await App.BaconMan.TileMan.RemoveSubredditTile(m_currentSubreddit);
            }

            if (!success)
            {
                // Don't show an error bc the only way this can really happen is if the user cancled the action

                // Fix the button
                SetPinButton(!pin);
            }
        }

        #endregion

        #region Search

        private void SetSearchButton()
        {
            ui_searchSubreddit.Content = m_currentSubreddit.IsArtifical ? "search reddit" : "search this subreddit";
        }

        /// <summary>
        /// Fired when the user taps search.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SearchSubreddit_Click(object sender, RoutedEventArgs e)
        {
            App.BaconMan.TelemetryMan.ReportEvent(this, "SubSidebarSearchTapped");

            Dictionary<string, object> args = new Dictionary<string, object>();
            // If this is an artificial subreddit make the name "" so we just search all posts.
            string displayName = m_currentSubreddit.IsArtifical ? "" : m_currentSubreddit.DisplayName;
            args.Add(PanelManager.NAV_ARGS_SEARCH_SUBREDDIT_NAME, displayName);
            m_host.Navigate(typeof(Search), "Search", args);
            FireShouldClose();
        }


        #endregion

        #region Submit post

        private void SubmitPostButton_Click(object sender, RoutedEventArgs e)
        {
            if (!App.BaconMan.UserMan.IsUserSignedIn)
            {
                App.BaconMan.MessageMan.ShowSigninMessage("submit a new post");
                return;
            }

            // Report
            App.BaconMan.TelemetryMan.ReportEvent(this, "SidebarSubmitPostTapped");

            Dictionary<string, object> args = new Dictionary<string, object>();
            if (!m_currentSubreddit.IsArtifical && !m_currentSubreddit.DisplayName.Equals("frontpage") && !m_currentSubreddit.DisplayName.Equals("all"))
            {
                args.Add(PanelManager.NAV_ARGS_SUBMIT_POST_SUBREDDIT, m_currentSubreddit.DisplayName);
            }
            m_host.Navigate(typeof(SubmitPost), m_currentSubreddit.DisplayName, args);
            FireShouldClose();
        }

        #endregion

        #region Markdown

        private async void SetMarkdown()
        {
            if (!String.IsNullOrWhiteSpace(m_currentSubreddit.Description))
            {
                // Delay this just a little so we don't hang the UI thread as much.
                await Task.Delay(5);

                ui_markdownTextBox.Markdown = m_currentSubreddit.Description;
            }
        }

        private void MarkdownTextBox_OnMarkdownLinkTapped(object sender, UniversalMarkdown.OnMarkdownLinkTappedArgs e)
        {
            App.BaconMan.ShowGlobalContent(e.Link);
            FireShouldClose();
        }

        #endregion

        private void FireShouldClose()
        {
            m_onShouldClose.Raise(this, new EventArgs());
        }
    }
}
