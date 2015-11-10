
using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using BaconBackend.Interfaces;
using BaconBackend.Managers;
using Baconit.HelperControls;
using Baconit.Interfaces;
using Baconit.Panels;
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
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Baconit
{
    /// <summary>
    /// The even used to show global content
    /// </summary>
    public class OnShowGlobalContentEventArgs : EventArgs
    {
        public string LinkToShow;
    }

    public sealed partial class MainPage : Page, IMainPage, IBackendActionListener
    {
        //
        // Private Vars
        //

        /// <summary>
        /// The main panel manager for the app
        /// </summary>
        PanelManager m_panelManager;

        /// <summary>
        /// Holds a ref to the current trending subs helper
        /// </summary>
        TrendingSubredditsHelper m_trendingSubsHelper = null;

        /// <summary>
        /// Holds the current subreddit list
        /// </summary>
        ObservableCollection<Subreddit> m_subreddits = new ObservableCollection<Subreddit>();

        public MainPage()
        {
            this.InitializeComponent();

            // Set ourselves as the backend action listener
            App.BaconMan.SetBackendActionListner(this);

            // Set the title bar color
            ApplicationView.GetForCurrentView().TitleBar.BackgroundColor = Color.FromArgb(255, 51, 51, 51);
            ApplicationView.GetForCurrentView().TitleBar.ButtonBackgroundColor = Color.FromArgb(255, 51, 51, 51);
            ApplicationView.GetForCurrentView().TitleBar.InactiveBackgroundColor = Color.FromArgb(255, 51, 51, 51);
            ApplicationView.GetForCurrentView().TitleBar.ButtonInactiveBackgroundColor = Color.FromArgb(255, 51, 51, 51);
            ApplicationView.GetForCurrentView().TitleBar.ForegroundColor = Color.FromArgb(255, 255, 255, 255);
            ApplicationView.GetForCurrentView().TitleBar.InactiveForegroundColor = Color.FromArgb(255, 255, 255, 255);
            ApplicationView.GetForCurrentView().TitleBar.ButtonForegroundColor = Color.FromArgb(255, 255, 255, 255);
            ApplicationView.GetForCurrentView().TitleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 255, 255, 255);

            // Create the starting panel
            WelcomePanel panel = new WelcomePanel();

            // Create the panel manager
            m_panelManager = new PanelManager(this, (IPanel)panel);
            ui_contentRoot.Children.Add(m_panelManager);

            // Add transparency to the account header, this will make it darker
            Color accentColor = (ui_accountHeaderGrid.Background as SolidColorBrush).Color;
            accentColor.A = 200;
            ui_accountHeaderGrid.Background = new SolidColorBrush(accentColor);

            // Add some transparency to the subreddit header also.
            accentColor = (ui_accountHeaderGrid.Background as SolidColorBrush).Color;
            accentColor.A = 137;
            ui_trendingSubredditsHeader.Background = new SolidColorBrush(accentColor);

            // Add some transparency to the search header also.
            accentColor = (ui_accountHeaderGrid.Background as SolidColorBrush).Color;
            accentColor.A = 75;
            ui_searchHeader.Background = new SolidColorBrush(accentColor);

            // Sub to callbacks
            App.BaconMan.SubredditMan.OnSubredditsUpdated += SubredditMan_OnSubredditUpdate;
            App.BaconMan.UserMan.OnUserUpdated += UserMan_OnUserUpdated;

            // Sub to loaded
            Loaded += MainPage_Loaded;
            App.BaconMan.OnResuming += App_OnResuming;

            // Set the subreddit list
            ui_subredditList.ItemsSource = m_subreddits;
        }

        /// <summary>
        /// Fired when the main page is loaded.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Set the current information to the UI.
            UpdateSubredditList(App.BaconMan.SubredditMan.SubredditList);
            UpdateAccount();

            // Request an update if needed
            App.BaconMan.SubredditMan.Update();
            App.BaconMan.UserMan.UpdateUser();

            // Get the default subreddit.
            string defaultDisplayName = App.BaconMan.UiSettingsMan.SubredditList_DefaultSubredditDisplayName;
            if (String.IsNullOrWhiteSpace(defaultDisplayName))
            {
                defaultDisplayName = "frontpage";
            }

            // Navigate to the start
            Dictionary<string, object> args = new Dictionary<string, object>();
            args.Add(PanelManager.NAV_ARGS_SUBREDDIT_NAME, defaultDisplayName);
            m_panelManager.Navigate(typeof(SubredditPanel), defaultDisplayName + SortTypes.Hot, args);
            m_panelManager.Navigate(typeof(WelcomePanel), "WelcomePanel");

            // Update the trending subreddits
            UpdateTrendingSubreddits();

            // Show review if we should
            CheckShowReviewAndFeedback();
        }

        /// <summary>
        /// Fired when the app is resuming.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void App_OnResuming(object sender, object e)
        {
            UpdateTrendingSubreddits();
        }

        #region UI Updates

        /// <summary>
        /// Fired when the subreddits are updated
        /// </summary>
        /// <param name="newSubreddits"></param>
        private async void SubredditMan_OnSubredditUpdate(object sender, OnSubredditsUpdatedArgs args)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                UpdateSubredditList(args.NewSubreddits);
            });
        }

        /// <summary>
        /// Fired when the user is updated
        /// </summary>
        private async void UserMan_OnUserUpdated(object sender, OnUserUpdatedArgs args)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                UpdateAccount();
            });
        }

        /// <summary>
        /// Updates the subreddit list
        /// </summary>
        /// <param name="newSubreddits"></param>
        private void UpdateSubredditList(List<Subreddit> newSubreddits)
        {
            int insertCount = 0;
            for (int newListCount = 0; newListCount < newSubreddits.Count; newListCount++)
            {
                Subreddit newSubreddit = newSubreddits[newListCount];

                // Set some UI properties.
                newSubreddit.FavIconUri = newSubreddit.IsFavorite ? "ms-appx:///Assets/MainPage/FavoriteIcon.png" : "ms-appx:///Assets/MainPage/NotFavoriteIcon.png";
                newSubreddit.DisplayName = newSubreddit.DisplayName.ToLower();

                // If the two are the same, just update them.
                if (m_subreddits.Count > insertCount && m_subreddits[insertCount].Id.Equals(newSubreddit.Id))
                {
                    // If they are the same just update it
                    m_subreddits[insertCount] = newSubreddit;
                }
                // (subreddit insert) If the next element in the new list is the same as the current element in the old list, insert.
                else if(m_subreddits.Count > insertCount && newSubreddits.Count > newListCount + 1 && newSubreddits[newListCount + 1].Id.Equals(m_subreddits[insertCount].Id))
                {
                    m_subreddits.Insert(insertCount, newSubreddit);
                }
                // (subreddit remove) If the current element in the new list is the same as the next element in the old list.
                else if (m_subreddits.Count > insertCount + 1 && newSubreddits.Count > newListCount && newSubreddits[newListCount].Id.Equals(m_subreddits[insertCount + 1].Id))
                {
                    m_subreddits.RemoveAt(insertCount);
                }
                // If the old list is still larger than the new list, replace
                else if(m_subreddits.Count > insertCount)
                {
                    m_subreddits[insertCount] = newSubreddit;
                }
                // Or just add.
                else
                {
                    m_subreddits.Add(newSubreddit);
                }
                insertCount++;
            }
        }

        /// <summary>
        /// Updates the account information in the side bar
        /// </summary>
        private void UpdateAccount()
        {
            // Set the defaults
            string userName = App.BaconMan.UserMan.IsUserSignedIn && App.BaconMan.UserMan.CurrentUser != null && App.BaconMan.UserMan.CurrentUser.Name != null ? App.BaconMan.UserMan.CurrentUser.Name : "Unknown";
            ui_accountHeader.Text = App.BaconMan.UserMan.IsUserSignedIn ? userName : "Account";
            ui_signInText.Text = App.BaconMan.UserMan.IsUserSignedIn ? "Sign Out" : "Sign In";
            ui_inboxGrid.Visibility = App.BaconMan.UserMan.IsUserSignedIn ? Visibility.Visible : Visibility.Collapsed;
            ui_accountHeaderMailBox.Visibility = Visibility.Collapsed;
            ui_accountHeaderKarmaHolder.Visibility = Visibility.Collapsed;

            if (App.BaconMan.UserMan.IsUserSignedIn)
            {
                // If we have mail so the mail icon.
                if (App.BaconMan.UserMan.CurrentUser.HasMail)
                {
                    ui_accountHeaderMailBox.Visibility = Visibility.Visible;
                }
                else
                {
                    ui_accountHeaderKarmaHolder.Visibility = Visibility.Visible;
                    ui_accountHeaderKaramaLink.Text = App.BaconMan.UserMan.CurrentUser.LinkKarma.ToString();
                    // The space is need to ensure the UI looks correct.
                    ui_accountHeaderKaramaComment.Text = " "+App.BaconMan.UserMan.CurrentUser.CommentKarma;
                }
            }
        }

        #endregion

        #region Subreddit Logic

        private void Favorite_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Get the subreddit from the sender
            Subreddit sub = ((sender as Grid).DataContext as Subreddit);

            // Reverse the status
            App.BaconMan.SubredditMan.SetFavorite(sub.Id, !sub.IsFavorite);

            App.BaconMan.TelemetryMan.ReportEvent(this, "SubredditListFavoriteTapped");
        }

        /// <summary>
        /// Fired when a subreddit is tapped and we should navigate to it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Subreddit_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Close the menu
            ToggleMenu(false);

            Subreddit subreddit = (sender as Grid).DataContext as Subreddit;
            Dictionary<string, object> args = new Dictionary<string, object>();
            args.Add(PanelManager.NAV_ARGS_SUBREDDIT_NAME, subreddit.DisplayName.ToLower());
            m_panelManager.Navigate(typeof(SubredditPanel), subreddit.GetNavigationUniqueId(SortTypes.Hot), args);
        }

        private void SubredditList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Remove the highlight
            ui_subredditList.SelectedIndex = -1;
        }


        #endregion

        #region Account Logic And Trending Subreddit

        /// <summary>
        /// Fired when a user taps the account header
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AccountHeader_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ToggleAccountPanel();
            CloseTrendingSubredditsPanelIfOpen();
        }

        /// <summary>
        /// Fired when a user taps the manage subreddit header
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TrendingSubredditsHeader_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ToggleTrendingSubredditsPanel();
            CloseAccoutPanelIfOpen();
        }

        public void CloseAllPanels(bool closeMenu = false)
        {
            CloseTrendingSubredditsPanelIfOpen();
            CloseAccoutPanelIfOpen();

            if(closeMenu)
            {
                ui_splitView.IsPaneOpen = false;
            }
        }

        public void CloseTrendingSubredditsPanelIfOpen()
        {
            if (ui_trendingSubredditsPanel.MaxHeight != 0)
            {
                ToggleTrendingSubredditsPanel();
            }
        }

        private void CloseAccoutPanelIfOpen()
        {
            if(ui_accountGrid.MaxHeight != 0)
            {
                ToggleAccountPanel();
            }
        }

        private void ToggleAccountPanel()
        {
            if (ui_accountGrid.MaxHeight == 0)
            {
                // If it is closed open it
                ui_animAccountGrid.To = ui_accountGrid.ActualHeight;
                ui_animAccountGrid.From = 0;
            }
            else
            {
                // Else close it
                ui_animAccountGrid.To = 0;
                ui_animAccountGrid.From = ui_accountGrid.ActualHeight;
            }
            // Start the animation.
            ui_storyAccountGrid.Begin();
        }

        private void ToggleTrendingSubredditsPanel()
        {
            if (ui_trendingSubredditsPanel.MaxHeight == 0)
            {
                // If it is closed open it
                ui_animSubredditsPanel.To = ui_trendingSubredditsPanel.ActualHeight;
                ui_animSubredditsPanel.From = 0;
            }
            else
            {
                // Else close it
                ui_animSubredditsPanel.To = 0;
                ui_animSubredditsPanel.From = ui_trendingSubredditsPanel.ActualHeight;
            }
            // Start the animation.
            ui_storySubredditsPanel.Begin();
        }

        private async void SigninGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (App.BaconMan.UserMan.IsUserSignedIn)
            {
                // Confirm with the user
                bool? response = await App.BaconMan.MessageMan.ShowYesNoMessage("Are You Sure?", "Are you sure you want to sign out? Was it something I did?");

                if (response.HasValue && response.Value)
                {
                    // Sign out the user
                    App.BaconMan.UserMan.SignOut();
                }
            }
            else
            {
                // Close the panel
                CloseAllPanels(true);

                // Navigate
                m_panelManager.Navigate(typeof(LoginPanel), "LoginPanel");
            }
        }

        private void InboxGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Navigate to the inbox
            m_panelManager.Navigate(typeof(MessageInbox), "MessageInbox");
            CloseAllPanels(true);
        }

        private void SettingsGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Navigate to the settings page.
            m_panelManager.Navigate(typeof(Settings), "Settings");
            CloseAllPanels(true);
        }

        private void ExploreSubreddits_Tapped(object sender, TappedRoutedEventArgs e)
        {
            CloseAllPanels(true);
        }

        private void AddOrRemoveSubreddits_Tapped(object sender, TappedRoutedEventArgs e)
        {
            CloseAllPanels(true);
        }

        private void SearchHeader_Tapped(object sender, TappedRoutedEventArgs e)
        {
            m_panelManager.Navigate(typeof(Search), "Search");
            CloseAllPanels(true);
        }

        #endregion

        #region Trending Subreddits

        /// <summary>
        ///  Updates the trending subreddits
        /// </summary>
        public void UpdateTrendingSubreddits()
        {
            // Kick off a thread so we dont use the Ui
            Task.Run(async () =>
            {
                // Make sure we don't already have one running.
                lock(this)
                {
                    if(m_trendingSubsHelper != null)
                    {
                        return;
                    }
                    m_trendingSubsHelper = new TrendingSubredditsHelper(App.BaconMan);
                }

                // Delay for a little while, give app time to process things it needs to.
                await Task.Delay(500);

                // Out side of the lock request an update
                m_trendingSubsHelper.OnTrendingSubReady += TrendingSubsHelper_OnTrendingSubReady;
                m_trendingSubsHelper.GetTrendingSubreddits();
            });
        }

        private async void TrendingSubsHelper_OnTrendingSubReady(object sender, TrendingSubsReadyEvent e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Loop through the results and add them to the UI.
                for(int i = 0; i < e.TrendingSubredditsDisplayNames.Count; i++)
                {
                    if(i > 4)
                    {
                        break;
                    }

                    switch(i)
                    {
                        case 0:
                            ui_trendingSubreddit1.Text = e.TrendingSubredditsDisplayNames[i];
                            break;
                        case 1:
                            ui_trendingSubreddit2.Text = e.TrendingSubredditsDisplayNames[i];
                            break;

                        case 2:
                            ui_trendingSubreddit3.Text = e.TrendingSubredditsDisplayNames[i];
                            break;
                        case 3:
                            ui_trendingSubreddit4.Text = e.TrendingSubredditsDisplayNames[i];
                            break;
                        default:
                        case 4:
                            ui_trendingSubreddit5.Text = e.TrendingSubredditsDisplayNames[i];
                            break;
                    }
                }
            });

            // Remove the callback
            m_trendingSubsHelper.OnTrendingSubReady -= TrendingSubsHelper_OnTrendingSubReady;

            // kill the object
            m_trendingSubsHelper = null;
        }

        /// <summary>
        /// Fired when a user taps a trending subreddit
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TrendingSubreddit_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Get the text block
            Grid holder = (Grid)sender;
            TextBlock textBlock = (TextBlock)holder.Children[0];

            if(textBlock != null)
            {
                // Get the subreddit and show it.
                // Note since this will have caps it is important to remove them.
                string subreddit = textBlock.Text.ToLower();
                App.BaconMan.ShowGlobalContent(subreddit);
            }

            // Close the panels
            CloseAllPanels(true);
        }

        #endregion

        #region IBackendActionListener

        /// <summary>
        /// Fired when someone wants to show the global content presenter
        /// </summary>
        /// <param name="link">What to show</param>
        public void ShowGlobalContent(string link)
        {
            // Validate that the link can't be opened by the subreddit viewer
            RedditContentContainer container = MiscellaneousHelper.TryToFindRedditContentInLink(link);
            if (container != null)
            {
                ShowGlobalContent(container);
            }
            else
            {
                ui_globalContentPresenter.ShowContent(link);
            }
        }

        /// <summary>
        /// Fired when someone wants to show the global content presenter
        /// </summary>
        /// <param name="link">What to show</param>
        public void ShowGlobalContent(RedditContentContainer container)
        {
            // We got reddit content, navigate to it!
            switch (container.Type)
            {
                case RedditContentType.Subreddit:
                    Dictionary<string, object> args = new Dictionary<string, object>();
                    args.Add(PanelManager.NAV_ARGS_SUBREDDIT_NAME, container.Subreddit);
                    m_panelManager.Navigate(typeof(SubredditPanel), container.Subreddit + SortTypes.Hot, args);
                    break;
                case RedditContentType.Post:
                    Dictionary<string, object> postArgs = new Dictionary<string, object>();
                    postArgs.Add(PanelManager.NAV_ARGS_SUBREDDIT_NAME, container.Subreddit);
                    postArgs.Add(PanelManager.NAV_ARGS_FORCE_POST_ID, container.Post);
                    m_panelManager.Navigate(typeof(FlipViewPanel), container.Subreddit + SortTypes.Hot + container.Post, postArgs);
                    break;
                case RedditContentType.Comment:
                    Dictionary<string, object> commentArgs = new Dictionary<string, object>();
                    commentArgs.Add(PanelManager.NAV_ARGS_SUBREDDIT_NAME, container.Subreddit);
                    commentArgs.Add(PanelManager.NAV_ARGS_FORCE_POST_ID, container.Post);
                    commentArgs.Add(PanelManager.NAV_ARGS_FORCE_COMMENT_ID, container.Comment);
                    m_panelManager.Navigate(typeof(FlipViewPanel), container.Subreddit + SortTypes.Hot + container.Post + container.Comment, commentArgs);
                    break;
            }
        }

        /// <summary>
        /// Called when we should show the message of they dialog
        /// </summary>
        /// <param name="title"></param>
        /// <param name="markdownContent"></param>
        public async void ShowMessageOfTheDay(string title, string markdownContent)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Make the popup.
                MotdPopUp motdPopUp = new MotdPopUp(title, markdownContent);
                motdPopUp.OnHideComplete += MotdPopUp_OnHideComplete;

                // This is a little tricky, we need to add it second to last
                // so the global content presenter can sill come up over it for links.
                if (ui_mainHolder.Children.Count > 0)
                {
                    ui_mainHolder.Children.Insert(ui_mainHolder.Children.Count - 1, motdPopUp);
                }
                else
                {
                    ui_mainHolder.Children.Add(motdPopUp);
                }

                motdPopUp.ShowPopUp();
            });
        }

        /// <summary>
        /// Fired when the MOTD is hidden
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MotdPopUp_OnHideComplete(object sender, EventArgs e)
        {
            // Remove it
            ui_mainHolder.Children.Remove((MotdPopUp)sender);
        }

        /// <summary>
        /// Called by the action listener when we should nav to login
        /// </summary>
        public void NavigateToLogin()
        {
            // Navigate
            m_panelManager.Navigate(typeof(LoginPanel), "LoginPanel");
        }

        #endregion

        #region Review and Feedback

        /// <summary>
        ///
        /// </summary>
        /// <param name="title"></param>
        /// <param name="markdownContent"></param>
        public async void CheckShowReviewAndFeedback()
        {
            // Check to see if we should show the review message.
            if(App.BaconMan.UiSettingsMan.AppOpenedCount > App.BaconMan.UiSettingsMan.MainPage_NextReviewAnnoy)
            {
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    // Make the popup.
                    RateAndFeedbackPopUp reviewPopup = new RateAndFeedbackPopUp();
                    reviewPopup.OnHideComplete += ReviewPopup_OnHideComplete;

                    // This is a little tricky, we need to add it second to last
                    // so the global content presenter can sill come up over it for links.
                    if (ui_mainHolder.Children.Count > 0)
                    {
                        ui_mainHolder.Children.Insert(ui_mainHolder.Children.Count - 1, reviewPopup);
                    }
                    else
                    {
                        ui_mainHolder.Children.Add(reviewPopup);
                    }

                    reviewPopup.ShowPopUp();
                });

                // If we showed the UI update the value to the next time we should annoy them.
                // If they leave a review we will make this huge.
                App.BaconMan.UiSettingsMan.MainPage_NextReviewAnnoy += 40;
            }
        }

        /// <summary>
        /// Fired when the review box is hidden
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReviewPopup_OnHideComplete(object sender, RateAndFeedbackClosed e)
        {
            // Remove it
            ui_mainHolder.Children.Remove((RateAndFeedbackPopUp)sender);

            // Set the rate value
            if(e.WasReviewGiven)
            {
                // Assume they rated the app. Set this to be huge.
                App.BaconMan.UiSettingsMan.MainPage_NextReviewAnnoy = int.MaxValue;
            }
        }

        #endregion

        /// <summary>
        /// Called by the page manager when the menu should be opened.
        /// </summary>
        /// <param name="show">If it should be shown or hidden</param>
        public void ToggleMenu(bool show)
        {
            ui_splitView.IsPaneOpen = show;
        }
    }
}
