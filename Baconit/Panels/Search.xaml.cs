using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
    public sealed partial class Search : UserControl, IPanel
    {
        const string c_subredditShowMoreHeader = "subreddit";
        const string c_postShowMoreHeader = "post";

        /// <summary>
        /// Holds a reference to the panel manager
        /// </summary>
        IPanelHost m_panelManager = null;

        /// <summary>
        /// Holds a reference to the current subreddit collector
        /// </summary>
        SearchSubredditCollector m_currentSubCollector = null;

        /// <summary>
        /// Holds a reference to the current post collector
        /// </summary>
        SearchPostCollector m_currentPostCollector = null;

        /// <summary>
        /// Holds a reference to the current search list.
        /// </summary>
        ObservableCollection<SearchResult> m_searchResultsList = new ObservableCollection<SearchResult>();

        /// <summary>
        /// Used to control the progress bars.
        /// </summary>
        bool m_areSubredditsSearching = false;
        bool m_areUsersSearching = false;
        bool m_arePostsSearching = false;

        /// <summary>
        /// Indicates what we are searching for, if null it is everything.
        /// </summary>
        SearchResultTypes? m_currentSearchType = null;


        public Search()
        {
            this.InitializeComponent();
            ui_searchResults.ItemsSource = m_searchResultsList;
            VisualStateManager.GoToState(this, "HideFilters", false);
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            m_panelManager = host;

            // If we get passed a search term search it right away.
            if(arguments.ContainsKey(PanelManager.NAV_ARGS_SEARCH_QUERY))
            {
                ui_searchBox.Text = (string)arguments[PanelManager.NAV_ARGS_SEARCH_QUERY];
                Search_Tapped(null, null);
            }
            else if (arguments.ContainsKey(PanelManager.NAV_ARGS_SEARCH_SUBREDDIT_NAME))
            {
                // We are opened from the side bar, set the subreddit name and go to post mode.
                // Set to post mode
                ui_searchForCombo.SelectedIndex = 3;

                // Fill out the subreddit name
                ui_postSubreddit.Text = (string)arguments[PanelManager.NAV_ARGS_SEARCH_SUBREDDIT_NAME];
            }
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            // If we get passed a search term search it right away.
            if (arguments.ContainsKey(PanelManager.NAV_ARGS_SEARCH_QUERY))
            {
                ui_searchBox.Text = (string)arguments[PanelManager.NAV_ARGS_SEARCH_QUERY];
                Search_Tapped(null, null);
            }

            OnNavigateToInternal();
        }

        public void OnNavigatingTo()
        {
            // Focus the search box when we open if the query is empty.
            if (String.IsNullOrWhiteSpace(ui_searchBox.Text))
            {
                ui_searchBox.Focus(FocusState.Programmatic);
            }

            OnNavigateToInternal();
        }

        private async void OnNavigateToInternal()
        {
            // Set the status bar color and get the size returned. If it is not 0 use that to move the
            // color of the page into the status bar.
            double statusBarHeight = await m_panelManager.SetStatusBar(null, 0);
            ui_contentRoot.Margin = new Thickness(0, -statusBarHeight, 0, 0);
            ui_contentRoot.Padding = new Thickness(0, statusBarHeight, 0, 0);
        }

        public void OnNavigatingFrom()
        {
            // Ignore for now
        }

        #region Search Button UI Logic

        private void SearchBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            // If they tapped enter, fire search
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                Search_Tapped(null, null);
            }
        }

        private void Search_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Make sure we have something to search.
            string searchTerm = ui_searchBox.Text;
            if (String.IsNullOrWhiteSpace(searchTerm))
            {
                return;
            }

            // Kill any existing searches
            KillAllOutgoingSearchesAndReset();

            // Grab the current search type
            switch(ui_searchForCombo.SelectedIndex)
            {
                // Everything
                case 0:
                    m_currentSearchType = null;
                    break;
                case 1:
                    m_currentSearchType = SearchResultTypes.Subreddit;
                    break;
                case 2:
                    m_currentSearchType = SearchResultTypes.User;
                    break;
                case 3:
                    m_currentSearchType = SearchResultTypes.Post;
                    break;
            }

            // Clear focus off the textbox
            ui_searchBox.IsEnabled = false;
            ui_searchBox.IsEnabled = true;

            if (!m_currentSearchType.HasValue || m_currentSearchType.Value == SearchResultTypes.Subreddit)
            {
                // Kick off a subreddit search
                DoSubredditSearch(searchTerm);
            }

            if (!m_currentSearchType.HasValue || m_currentSearchType.Value == SearchResultTypes.User)
            {
                // Kick off a user search
                DoUserSearch(searchTerm);
            }

            if (!m_currentSearchType.HasValue || m_currentSearchType.Value == SearchResultTypes.Post)
            {
                // Kick off a post search
                DoPostSearch(searchTerm);
            }
        }

        /// <summary>
        /// Fired when the combo box is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SearchForCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Search_Tapped(null, null);

            // If it is a post show the advance filter
            if (ui_searchForCombo.SelectedIndex == 3)
            {
                VisualStateManager.GoToState(this, "ShowFilters", true);
            }
            else
            {
                VisualStateManager.GoToState(this, "HideFilters", true);
            }
        }

        /// <summary>
        /// Called when we should change the filter and search again
        /// </summary>
        /// <param name="type"></param>
        public void DoFilteredSearch(SearchResultTypes? type)
        {
            // Set the UI list type
            if (!type.HasValue)
            {
                ui_searchForCombo.SelectedIndex = 0;
            }
            else
            {
                switch (type.Value)
                {
                    case SearchResultTypes.Post:
                        ui_searchForCombo.SelectedIndex = 3;
                        break;
                    case SearchResultTypes.Subreddit:
                        ui_searchForCombo.SelectedIndex = 1;
                        break;
                    case SearchResultTypes.User:
                        ui_searchForCombo.SelectedIndex = 2;
                        break;
                }
            }

            // The search will kick off automatically when the selection is changed.
        }

        #endregion

        #region Subreddit search logic

        /// <summary>
        /// Creates a new collector and starts a search.
        /// </summary>
        /// <param name="searchTerm"></param>
        private void DoSubredditSearch(string searchTerm)
        {
            lock (this)
            {
                m_currentSubCollector = new SearchSubredditCollector(App.BaconMan, searchTerm);
                m_currentSubCollector.OnCollectionUpdated += CurrentSubCollector_OnCollectionUpdated;
                m_currentSubCollector.OnCollectorStateChange += CurrentSubCollector_OnCollectorStateChange;
                m_currentSubCollector.Update(true);
            }
        }

        /// <summary>
        /// Fired when the collection state is changing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CurrentSubCollector_OnCollectorStateChange(object sender, OnCollectorStateChangeArgs e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (e.State == CollectorState.Idle || e.State == CollectorState.Error || e.State == CollectorState.FullyExtended)
                {
                    HideProgressBar(SearchResultTypes.Subreddit);
                }
                else
                {
                    ShowProgressBar(SearchResultTypes.Subreddit);
                }
            });
        }

        /// <summary>
        /// Fired when we have subreddit results
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CurrentSubCollector_OnCollectionUpdated(object sender, OnCollectionUpdatedArgs<Subreddit> e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Subreddits are always at the top of the list
                int insertIndex = 1;
                bool showAll = m_currentSearchType.HasValue && m_currentSearchType.Value == SearchResultTypes.Subreddit;

                // Lock the list
                lock(m_searchResultsList)
                {
                    // Insert the header
                    SearchResult header = new SearchResult()
                    {
                        ResultType = SearchResultTypes.Header,
                        HeaderText = "Subreddit Results"
                    };
                    m_searchResultsList.Insert(0, header);

                    int count = 0;
                    foreach(Subreddit subreddit in e.ChangedItems)
                    {
                        // Make sure it isn't private. Since we can't show these we will skip them for now
                        // #todo fix this
                        if(subreddit.SubredditType != null && subreddit.SubredditType.Equals("private"))
                        {
                            continue;
                        }

                        // Make the result
                        SearchResult subredditResult = new SearchResult()
                        {
                            ResultType = SearchResultTypes.Subreddit,
                            MajorText = subreddit.Title,
                            MinorText = subreddit.PublicDescription,
                            MinorAccentText = $"/r/{subreddit.DisplayName}",
                            DataContext = subreddit
                        };

                        // Hide the minor text if there isn't any
                        if(String.IsNullOrWhiteSpace(subreddit.PublicDescription))
                        {
                            subredditResult.ShowMinorText = Visibility.Collapsed;
                        }

                        // Add it to the list
                        m_searchResultsList.Insert(insertIndex, subredditResult);

                        // Itter
                        count++;
                        insertIndex++;

                        // If we are showing everything only show the top 3 results.
                        // Otherwise show everything.
                        if(!showAll && count > 2)
                        {
                            break;
                        }
                    }

                    // Insert no results if so
                    if(e.ChangedItems.Count == 0)
                    {
                        // Insert the header
                        SearchResult noResults = new SearchResult()
                        {
                            ResultType = SearchResultTypes.NoResults
                        };
                        m_searchResultsList.Insert(insertIndex, noResults);
                        insertIndex++;
                    }

                    // Insert show more if we didn't show all
                    if(e.ChangedItems.Count != 0 && !showAll)
                    {
                        // Insert the header
                        SearchResult showMore = new SearchResult()
                        {
                            ResultType = SearchResultTypes.ShowMore,
                            DataContext = c_subredditShowMoreHeader
                        };
                        m_searchResultsList.Insert(insertIndex, showMore);
                    }
                }
            });
        }

        #endregion

        #region Post Search Logic

        /// <summary>
        /// Creates a new collector and starts a search.
        /// </summary>
        /// <param name="searchTerm"></param>
        private void DoPostSearch(string searchTerm)
        {
            // Get all of the filters.
            PostSearchSorts sort = GetCurrentPostSort();
            PostSearchTimes times = GetCurrentPostTime();
            string subredditFilter = ui_postSubreddit.Text;
            string authorFilter = ui_postUserName.Text;
            string websiteFilter = ui_postWebsite.Text;
            string selftextFilter = ui_postSelfText.Text;
            string isSelfPost = ui_postIsSelf.SelectedIndex == 0 ? String.Empty : (ui_postIsSelf.SelectedIndex == 1 ? "yes" : "no");
            string isNsfw = ui_postIsNsfw.SelectedIndex == 0 ? String.Empty : (ui_postIsNsfw.SelectedIndex == 1 ? "yes" : "no");

            lock (this)
            {
                m_currentPostCollector = new SearchPostCollector(App.BaconMan, searchTerm, sort, times, subredditFilter, authorFilter, websiteFilter, selftextFilter, isSelfPost, isNsfw);
                m_currentPostCollector.OnCollectionUpdated += CurrentPostCollector_OnCollectionUpdated;
                m_currentPostCollector.OnCollectorStateChange += CurrentPostCollector_OnCollectorStateChange;
                m_currentPostCollector.Update(true);
            }
        }

        /// <summary>
        /// Fired when the collection state is changing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CurrentPostCollector_OnCollectorStateChange(object sender, OnCollectorStateChangeArgs e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (e.State == CollectorState.Idle || e.State == CollectorState.Error || e.State == CollectorState.FullyExtended)
                {
                    HideProgressBar(SearchResultTypes.Post);
                }
                else
                {
                    ShowProgressBar(SearchResultTypes.Post);
                }
            });
        }

        /// <summary>
        /// Fired when we have post results
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CurrentPostCollector_OnCollectionUpdated(object sender, OnCollectionUpdatedArgs<Post> e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Posts insert at the bottom of the list.
                bool showAll = m_currentSearchType.HasValue && m_currentSearchType.Value == SearchResultTypes.Post;

                // Lock the list
                lock (m_searchResultsList)
                {
                    // Insert the header
                    SearchResult header = new SearchResult()
                    {
                        ResultType = SearchResultTypes.Header,
                        HeaderText = "Post Results"
                    };
                    m_searchResultsList.Add(header);

                    // Insert the items
                    int count = 0;
                    foreach (Post post in e.ChangedItems)
                    {
                        // Make the result
                        SearchResult postResult = new SearchResult()
                        {
                            ResultType = SearchResultTypes.Post,
                            MajorText = post.Title,
                            MinorText = $"{post.SubTextLine1} to {post.Subreddit}",
                            MinorAccentText = $"({post.Score}) score; {post.NumComments} comments",
                            DataContext = post
                        };

                        // Add it to the list
                        m_searchResultsList.Add(postResult);

                        // Itter
                        count++;

                        // If we are showing everything only show the top 3 results.
                        // Otherwise show everything.
                        if (!showAll && count > 2)
                        {
                            break;
                        }
                    }

                    // Insert no results if so
                    if (e.ChangedItems.Count == 0)
                    {
                        // Insert the header
                        SearchResult noResults = new SearchResult()
                        {
                            ResultType = SearchResultTypes.NoResults
                        };
                        m_searchResultsList.Add(noResults);
                    }

                    // Insert show more if we didn't show all
                    if (e.ChangedItems.Count != 0 && !showAll)
                    {
                        // Insert the header
                        SearchResult showMore = new SearchResult()
                        {
                            ResultType = SearchResultTypes.ShowMore,
                            DataContext = c_postShowMoreHeader
                        };
                        m_searchResultsList.Add(showMore);
                    }
                }
            });
        }

        public PostSearchSorts GetCurrentPostSort()
        {
            switch(ui_postSort.SelectedIndex)
            {
                default:
                case 0:
                    return PostSearchSorts.Relevance;
                case 1:
                    return PostSearchSorts.Top;
                case 2:
                    return PostSearchSorts.New;
                case 3:
                    return PostSearchSorts.Comments;
            }
        }

        public PostSearchTimes GetCurrentPostTime()
        {
            switch (ui_postTime.SelectedIndex)
            {
                default:
                case 0:
                    return PostSearchTimes.AllTime;
                case 1:
                    return PostSearchTimes.PastHour;
                case 2:
                    return PostSearchTimes.PastDay;
                case 3:
                    return PostSearchTimes.PastWeek;
                case 4:
                    return PostSearchTimes.PastMonth;
                case 5:
                    return PostSearchTimes.PastYear;
            }
        }

        #endregion
        
        #region User Search Logic

        /// <summary>
        /// Creates a new collector and starts a search.
        /// </summary>
        /// <param name="searchTerm"></param>
        private async void DoUserSearch(string searchTerm)
        {
            // Make a request for the user
            User userResult = await MiscellaneousHelper.GetRedditUser(App.BaconMan, searchTerm);

            // Else put it in the list
            lock(m_searchResultsList)
            {
                // First check that we are still searching for the same thing
                if(!ui_searchBox.Text.Equals(searchTerm))
                {
                    return;
                }

                // Search for the footer of the subreddits if it is there
                int count = 0;
                int insertIndex = -1;
                foreach(SearchResult result in m_searchResultsList)
                {
                    if(result.ResultType == SearchResultTypes.ShowMore && ((string)result.DataContext).Equals(c_subredditShowMoreHeader))
                    {
                        insertIndex = count;
                        break;
                    }
                    count++;
                }

                // See if we found it, if not insert at the top.
                if(insertIndex == -1)
                {
                    insertIndex = 0;
                }

                // Insert the header
                SearchResult header = new SearchResult()
                {
                    ResultType = SearchResultTypes.Header,
                    HeaderText = "User Result"
                };
                m_searchResultsList.Insert(insertIndex, header);
                insertIndex++;

                if (userResult != null)
                {
                    // Insert the User
                    SearchResult userItem = new SearchResult()
                    {
                        ResultType = SearchResultTypes.User,
                        MajorText = userResult.Name,
                        MinorText = $"link karma {userResult.LinkKarma}; comment karma {userResult.CommentKarma}",
                        DataContext = userResult
                    };
                    if (userResult.IsGold)
                    {
                        userItem.MinorAccentText = "Has Gold";
                    }
                    m_searchResultsList.Insert(insertIndex, userItem);
                    insertIndex++;
                }
                else
                {
                    // Insert no results
                    SearchResult noResults = new SearchResult()
                    {
                        ResultType = SearchResultTypes.NoResults
                    };
                    m_searchResultsList.Insert(insertIndex, noResults);
                    insertIndex++;
                }
            }
        }


        #endregion

        private void KillAllOutgoingSearchesAndReset()
        {
            // Kill any collectors
            lock(this)
            {
                if(m_currentSubCollector != null)
                {
                    m_currentSubCollector.OnCollectionUpdated -= CurrentSubCollector_OnCollectionUpdated;
                    m_currentSubCollector.OnCollectorStateChange -= CurrentSubCollector_OnCollectorStateChange;
                    m_currentSubCollector = null;
                }
                if(m_currentPostCollector != null)
                {
                    m_currentPostCollector.OnCollectionUpdated -= CurrentPostCollector_OnCollectionUpdated;
                    m_currentPostCollector.OnCollectorStateChange -= CurrentPostCollector_OnCollectorStateChange;
                    m_currentPostCollector = null;
                }
            }

            // Clear the results
            lock(m_searchResultsList)
            {
                // Clear them each one by one, this will make them animate out
                while(m_searchResultsList.Count > 0)
                {
                    m_searchResultsList.RemoveAt(m_searchResultsList.Count - 1);
                }
            }

            // Hide progress
            HideProgressBar(SearchResultTypes.Subreddit);
            HideProgressBar(SearchResultTypes.User);
            HideProgressBar(SearchResultTypes.Post);
        }

        #region Search Tapped Logic

        /// <summary>
        /// Fired when a search result is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Grab the result
            SearchResult tappedResult = (SearchResult)ui_searchResults.SelectedItem;
            if(tappedResult != null)
            {
                if(tappedResult.ResultType == SearchResultTypes.ShowMore)
                {
                    string context = ((string)tappedResult.DataContext);
                    if(context.Equals(c_subredditShowMoreHeader))
                    {
                        DoFilteredSearch(SearchResultTypes.Subreddit);
                    }
                    else if(context.Equals(c_postShowMoreHeader))
                    {
                        DoFilteredSearch(SearchResultTypes.Post);
                    }
                }
                else if(tappedResult.ResultType == SearchResultTypes.Subreddit)
                {
                    Subreddit subreddit = (Subreddit)tappedResult.DataContext;
                    // If is very important to not send in a upper case display name
                    subreddit.DisplayName = subreddit.DisplayName.ToLower();
                    // Navigate to the subreddit
                    Dictionary<string, object> args = new Dictionary<string, object>();
                    // Send the display name.
                    args.Add(PanelManager.NAV_ARGS_SUBREDDIT_NAME, subreddit.DisplayName);
                    m_panelManager.Navigate(typeof(SubredditPanel), subreddit.DisplayName + SortTypes.Hot + SortTimeTypes.Week, args);
                }
                else if(tappedResult.ResultType == SearchResultTypes.Post)
                {
                    Post post = (Post)tappedResult.DataContext;

                    // Navigate to the post
                    Dictionary<string, object> args = new Dictionary<string, object>();
                    args.Add(PanelManager.NAV_ARGS_SUBREDDIT_NAME, post.Subreddit);
                    args.Add(PanelManager.NAV_ARGS_FORCE_POST_ID, post.Id);
                    // Make sure the page id is unique
                    m_panelManager.Navigate(typeof(FlipViewPanel), post.Subreddit + SortTypes.Hot + SortTimeTypes.Week + post.Id, args);
                }
                else if(tappedResult.ResultType == SearchResultTypes.User)
                {
                    User user = (User)tappedResult.DataContext;

                    // Navigate to the user
                    Dictionary<string, object> args = new Dictionary<string, object>();
                    args.Add(PanelManager.NAV_ARGS_USER_NAME, user.Name);
                    m_panelManager.Navigate(typeof(UserProfile), user.Name, args);
                }
            }

            // Reset the list
            ui_searchResults.SelectedIndex = -1;
        }

        #endregion

        #region Progress Bar Logic

        /// <summary>
        /// Shows the progress bar and keeps track of who said so.
        /// </summary>
        /// <param name="type"></param>
        public void ShowProgressBar(SearchResultTypes type)
        {
            lock(ui_progressBar)
            {
                // Figure out if one is already true, thus the bar would be shown.
                bool showBar = !m_arePostsSearching && !m_areSubredditsSearching && !m_areUsersSearching;

                switch (type)
                {
                    case SearchResultTypes.Post:
                        m_arePostsSearching = true;
                        break;
                    case SearchResultTypes.Subreddit:
                        m_areSubredditsSearching = true;
                        break;
                    case SearchResultTypes.User:
                        m_areUsersSearching = true;
                        break;
                }

                if (showBar)
                {
                    ui_progressBar.Visibility = Visibility.Visible;
                    ui_progressBar.IsIndeterminate = true;
                }
            }
        }

        /// <summary>
        /// Hides the progress bar if everyone is done searching.
        /// </summary>
        /// <param name="type"></param>
        public void HideProgressBar(SearchResultTypes type)
        {
            lock (ui_progressBar)
            {
                // Update the state
                switch (type)
                {
                    case SearchResultTypes.Post:
                        m_arePostsSearching = false;
                        break;
                    case SearchResultTypes.Subreddit:
                        m_areSubredditsSearching = false;
                        break;
                    case SearchResultTypes.User:
                        m_areUsersSearching = false;
                        break;
                }

                if (!m_arePostsSearching && !m_areSubredditsSearching && !m_areUsersSearching)
                {
                    ui_progressBar.Visibility = Visibility.Collapsed;
                    ui_progressBar.IsIndeterminate = false;
                }
            }
        }

        #endregion
    }
}
