using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using Baconit.Interfaces;
using Baconit.Panels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using BaconBackend.Managers;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.HelperControls
{
    public sealed partial class SubredditSideBar : UserControl
    {
        /// <summary>
        /// The panel host
        /// </summary>
        private IPanelHost _mHost;

        /// <summary>
        /// The current subreddit we are showing
        /// </summary>
        private Subreddit _mCurrentSubreddit;

        /// <summary>
        /// A brush for active buttons
        /// </summary>
        private readonly SolidColorBrush _mButtonActive;

        /// <summary>
        /// A brush for inactive buttons
        /// </summary>
        private readonly SolidColorBrush _mButtonInactive;

        /// <summary>
        /// Fired when the sidebar should be closed because we are navigating somewhere.
        /// </summary>
        public event EventHandler<EventArgs> OnShouldClose
        {
            add => _mOnShouldClose.Add(value);
            remove => _mOnShouldClose.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<EventArgs>> _mOnShouldClose = new SmartWeakEvent<EventHandler<EventArgs>>();


        public SubredditSideBar()
        {
            InitializeComponent();

            // Get the accent color
            var accentColor = ((SolidColorBrush)Application.Current.Resources["SystemControlBackgroundAccentBrush"]).Color;

            // Set the title color
            var darkAccent = accentColor;
            darkAccent.A = 170;
            ui_titleHeaderContainer.Background = new SolidColorBrush(darkAccent);

            // Set the button container background
            var darkerAccent = accentColor;
            darkerAccent.A = 90;
            ui_buttonContainer.Background = new SolidColorBrush(darkerAccent);

            // Set the active button background
            var buttonActive = accentColor;
            buttonActive.A = 200;
            _mButtonActive = new SolidColorBrush(buttonActive);

            // Get the button inactive color
            var buttonAccent = accentColor;
            buttonAccent.A = 90;
            _mButtonInactive = new SolidColorBrush(buttonAccent);

            // Set the submit link and text colors
            ui_submitPostButton.Background = _mButtonInactive;
            ui_searchSubreddit.Background = _mButtonInactive;
        }

        /// <summary>
        /// Fired when we should show a subreddit.
        /// </summary>
        /// <param name="subreddit"></param>
        public void SetSubreddit(IPanelHost host, Subreddit subreddit)
        {
            // Capture host
            _mHost = host;

            // Make sure we don't already have it.
            if (_mCurrentSubreddit != null && _mCurrentSubreddit.Id.Equals(subreddit.Id))
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
            _mCurrentSubreddit = subreddit;

            // Set the title
            ui_titleTextBlock.Text = _mCurrentSubreddit.DisplayName;

            // Set the subs, if the value doesn't exist or the subreddit is fake make it "many"
            if (_mCurrentSubreddit.SubscriberCount.HasValue && !_mCurrentSubreddit.IsArtificial)
            {
                ui_subscribersTextBlock.Text = $"{_mCurrentSubreddit.SubscriberCount:N0}" + " subscribers";
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
            if(_mCurrentSubreddit.IsArtificial)
            {
                ui_subscribeButton.IsEnabled = false;
            }

            // If we are being forced to show something show it, if not check if the user is subed or not.
            var isSubed = forcedState.HasValue ? forcedState.Value : App.BaconMan.SubredditMan.IsSubredditSubscribedTo(_mCurrentSubreddit.DisplayName);

            // Set the text
            ui_subscribeButton.Content = isSubed ? "unsubscribe" : "subscribe";

            // Set the button color
            ui_subscribeButton.Background = isSubed ? _mButtonActive : _mButtonInactive;
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
            TelemetryManager.ReportEvent(this, "SubredditSubscribeTapped");

            // Use the text here, this can be risky but it makes sure we do a action that matches the UI.
            var subscribe = ui_subscribeButton.Content.Equals("subscribe");

            // Update the button now so the UI responds
            SetSubButton(subscribe);

            // Make the request
            var success = await App.BaconMan.SubredditMan.ChangeSubscriptionStatus(_mCurrentSubreddit.Id, subscribe);

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
            var isPinned = forcedState.HasValue ? forcedState.Value : TileManager.IsSubredditPinned(_mCurrentSubreddit.DisplayName);

            // Set the text
            ui_pinToStartButton.Content = isPinned ? "unpin from start" : "pin to start";

            // Set the button color
            ui_pinToStartButton.Background = isPinned ? _mButtonActive : _mButtonInactive;
        }

        /// <summary>
        /// Fired when the user taps the pin button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void PinButton_Click(object sender, RoutedEventArgs e)
        {
            // Use the text here, this can be risky but it makes sure we do a action that matches the UI.
            var pin = ui_pinToStartButton.Content.Equals("pin to start");

            // Report
            TelemetryManager.ReportEvent(this, "SubPinToStartTapped");

            // Update the button now so the UI responds
            SetPinButton(pin);

            // Make the request
            var success = false;
            if(pin)
            {
                success = await TileManager.CreateSubredditTile(_mCurrentSubreddit);
            }
            else
            {
                success = await TileManager.RemoveSubredditTile(_mCurrentSubreddit);
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
            ui_searchSubreddit.Content = _mCurrentSubreddit.IsArtificial ? "search reddit" : "search this subreddit";
        }

        /// <summary>
        /// Fired when the user taps search.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SearchSubreddit_Click(object sender, RoutedEventArgs e)
        {
            TelemetryManager.ReportEvent(this, "SubSidebarSearchTapped");

            var args = new Dictionary<string, object>();
            // If this is an artificial subreddit make the name "" so we just search all posts.
            var displayName = _mCurrentSubreddit.IsArtificial ? "" : _mCurrentSubreddit.DisplayName;
            args.Add(PanelManager.NavArgsSearchSubredditName, displayName);
            _mHost.Navigate(typeof(Search), "Search", args);
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
            TelemetryManager.ReportEvent(this, "SidebarSubmitPostTapped");

            var args = new Dictionary<string, object>();
            if (!_mCurrentSubreddit.IsArtificial && !_mCurrentSubreddit.DisplayName.Equals("frontpage") && !_mCurrentSubreddit.DisplayName.Equals("all"))
            {
                args.Add(PanelManager.NavArgsSubmitPostSubreddit, _mCurrentSubreddit.DisplayName);
            }
            _mHost.Navigate(typeof(SubmitPost), _mCurrentSubreddit.DisplayName, args);
            FireShouldClose();
        }

        #endregion

        #region Markdown

        private async void SetMarkdown()
        {
            if (!string.IsNullOrWhiteSpace(_mCurrentSubreddit.Description))
            {
                // Delay this just a little so we don't hang the UI thread as much.
                await Task.Delay(5);

                ui_markdownTextBox.Markdown = _mCurrentSubreddit.Description;
            }
        }

        private void MarkdownTextBox_OnMarkdownLinkTapped(object sender, UniversalMarkdown.MarkdownLinkTappedArgs e)
        {
            App.BaconMan.ShowGlobalContent(e.Link);
            FireShouldClose();
        }

        #endregion

        private void FireShouldClose()
        {
            _mOnShouldClose.Raise(this, new EventArgs());
        }
    }
}
