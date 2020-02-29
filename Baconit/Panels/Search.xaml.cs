using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using Baconit.Interfaces;
using Baconit.Panels.FlipView;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.Panels
{
    public sealed partial class Search : UserControl, IPanel
    {
        private const string CSubredditShowMoreHeader = "subreddit";
        private const string CPostShowMoreHeader = "post";

        /// <summary>
        /// Holds a reference to the panel manager
        /// </summary>
        private IPanelHost _mPanelManager;

        /// <summary>
        /// Holds a reference to the current subreddit collector
        /// </summary>
        private SearchSubredditCollector _mCurrentSubCollector;

        /// <summary>
        /// Holds a reference to the current post collector
        /// </summary>
        private SearchPostCollector _mCurrentPostCollector;

        /// <summary>
        /// Holds a reference to the current search list.
        /// </summary>
        private readonly ObservableCollection<SearchResult> _mSearchResultsList = new ObservableCollection<SearchResult>();

        /// <summary>
        /// Used to control the progress bars.
        /// </summary>
        private bool _mAreSubredditsSearching;

        private bool _mAreUsersSearching;
        private bool _mArePostsSearching;

        /// <summary>
        /// Indicates what we are searching for, if null it is everything.
        /// </summary>
        private SearchResultTypes? _mCurrentSearchType;


        public Search()
        {
            InitializeComponent();
            ui_searchResults.ItemsSource = _mSearchResultsList;
            VisualStateManager.GoToState(this, "HideFilters", false);
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            _mPanelManager = host;

            // If we get passed a search term search it right away.
            if(arguments.ContainsKey(PanelManager.NavArgsSearchQuery))
            {
                ui_searchBox.Text = (string)arguments[PanelManager.NavArgsSearchQuery];
                Search_Tapped(null, null);
            }
            else if (arguments.ContainsKey(PanelManager.NavArgsSearchSubredditName))
            {
                // We are opened from the side bar, set the subreddit name and go to post mode.
                // Set to post mode
                ui_searchForCombo.SelectedIndex = 3;

                // Fill out the subreddit name
                ui_postSubreddit.Text = (string)arguments[PanelManager.NavArgsSearchSubredditName];
            }
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            // If we get passed a search term search it right away.
            if (arguments.ContainsKey(PanelManager.NavArgsSearchQuery))
            {
                ui_searchBox.Text = (string)arguments[PanelManager.NavArgsSearchQuery];
                Search_Tapped(null, null);
            }

            OnNavigateToInternal();
        }

        public void OnNavigatingTo()
        {
            // Focus the search box when we open if the query is empty.
            if (string.IsNullOrWhiteSpace(ui_searchBox.Text))
            {
                ui_searchBox.Focus(FocusState.Programmatic);
            }

            OnNavigateToInternal();
        }

        private async void OnNavigateToInternal()
        {
            // Set the status bar color and get the size returned. If it is not 0 use that to move the
            // color of the page into the status bar.
            var statusBarHeight = await _mPanelManager.SetStatusBar(null, 0);
            ui_contentRoot.Margin = new Thickness(0, -statusBarHeight, 0, 0);
            ui_contentRoot.Padding = new Thickness(0, statusBarHeight, 0, 0);
        }

        public void OnNavigatingFrom()
        {
            // Ignore for now
        }

        public void OnCleanupPanel()
        {
            // Ignore for now.
            // #todo stop any on going requests.
        }

        /// <summary>
        /// Fired when the panel should try to reduce memory if possible. This will only be called
        /// while the panel isn't visible.
        /// </summary>
        public void OnReduceMemory()
        {
            // Ignore for now.
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
            var searchTerm = ui_searchBox.Text;
            if (string.IsNullOrWhiteSpace(searchTerm))
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
                    _mCurrentSearchType = null;
                    break;
                case 1:
                    _mCurrentSearchType = SearchResultTypes.Subreddit;
                    break;
                case 2:
                    _mCurrentSearchType = SearchResultTypes.User;
                    break;
                case 3:
                    _mCurrentSearchType = SearchResultTypes.Post;
                    break;
            }

            // Clear focus off the textbox
            ui_searchBox.IsEnabled = false;
            ui_searchBox.IsEnabled = true;

            if (!_mCurrentSearchType.HasValue || _mCurrentSearchType.Value == SearchResultTypes.Subreddit)
            {
                // Kick off a subreddit search
                DoSubredditSearch(searchTerm);
            }

            if (!_mCurrentSearchType.HasValue || _mCurrentSearchType.Value == SearchResultTypes.User)
            {
                // Kick off a user search
                DoUserSearch(searchTerm);
            }

            if (!_mCurrentSearchType.HasValue || _mCurrentSearchType.Value == SearchResultTypes.Post)
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
                _mCurrentSubCollector = new SearchSubredditCollector(App.BaconMan, searchTerm);
                _mCurrentSubCollector.OnCollectionUpdated += CurrentSubCollector_OnCollectionUpdated;
                _mCurrentSubCollector.OnCollectorStateChange += CurrentSubCollector_OnCollectorStateChange;
                _mCurrentSubCollector.Update(true);
            }
        }

        /// <summary>
        /// Fired when the collection state is changing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CurrentSubCollector_OnCollectorStateChange(object sender, CollectorStateChangeArgs e)
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
        private async void CurrentSubCollector_OnCollectionUpdated(object sender, CollectionUpdatedArgs<Subreddit> e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Subreddits are always at the top of the list
                var insertIndex = 1;
                var showAll = _mCurrentSearchType.HasValue && _mCurrentSearchType.Value == SearchResultTypes.Subreddit;

                // Lock the list
                lock(_mSearchResultsList)
                {
                    // Insert the header
                    var header = new SearchResult
                    {
                        ResultType = SearchResultTypes.Header,
                        HeaderText = "Subreddit Results"
                    };
                    _mSearchResultsList.Insert(0, header);

                    var count = 0;
                    foreach(var subreddit in e.ChangedItems)
                    {
                        // Make sure it isn't private. Since we can't show these we will skip them for now
                        // #todo fix this
                        if(subreddit.SubredditType != null && subreddit.SubredditType.Equals("private"))
                        {
                            continue;
                        }

                        // Make the result
                        var subredditResult = new SearchResult
                        {
                            ResultType = SearchResultTypes.Subreddit,
                            MajorText = subreddit.Title,
                            MarkdownText = subreddit.PublicDescription,
                            MinorAccentText = $"/r/{subreddit.DisplayName}",
                            DataContext = subreddit
                        };

                        // Add it to the list
                        _mSearchResultsList.Insert(insertIndex, subredditResult);

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
                        var noResults = new SearchResult
                        {
                            ResultType = SearchResultTypes.NoResults
                        };
                        _mSearchResultsList.Insert(insertIndex, noResults);
                        insertIndex++;
                    }

                    // Insert show more if we didn't show all
                    if(e.ChangedItems.Count != 0 && !showAll)
                    {
                        // Insert the header
                        var showMore = new SearchResult
                        {
                            ResultType = SearchResultTypes.ShowMore,
                            DataContext = CSubredditShowMoreHeader
                        };
                        _mSearchResultsList.Insert(insertIndex, showMore);
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
            var sort = GetCurrentPostSort();
            var times = GetCurrentPostTime();
            var subredditFilter = ui_postSubreddit.Text;
            var authorFilter = ui_postUserName.Text;
            var websiteFilter = ui_postWebsite.Text;
            var selftextFilter = ui_postSelfText.Text;
            var isSelfPost = ui_postIsSelf.SelectedIndex == 0 ? string.Empty : (ui_postIsSelf.SelectedIndex == 1 ? "yes" : "no");
            var isNsfw = ui_postIsNsfw.SelectedIndex == 0 ? string.Empty : (ui_postIsNsfw.SelectedIndex == 1 ? "yes" : "no");

            lock (this)
            {
                _mCurrentPostCollector = new SearchPostCollector(App.BaconMan, searchTerm, sort, times, subredditFilter, authorFilter, websiteFilter, selftextFilter, isSelfPost, isNsfw);
                _mCurrentPostCollector.OnCollectionUpdated += CurrentPostCollector_OnCollectionUpdated;
                _mCurrentPostCollector.OnCollectorStateChange += CurrentPostCollector_OnCollectorStateChange;
                _mCurrentPostCollector.Update(true);
            }
        }

        /// <summary>
        /// Fired when the collection state is changing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CurrentPostCollector_OnCollectorStateChange(object sender, CollectorStateChangeArgs e)
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
        private async void CurrentPostCollector_OnCollectionUpdated(object sender, CollectionUpdatedArgs<Post> e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Posts insert at the bottom of the list.
                var showAll = _mCurrentSearchType.HasValue && _mCurrentSearchType.Value == SearchResultTypes.Post;

                // Lock the list
                lock (_mSearchResultsList)
                {
                    // Insert the header
                    var header = new SearchResult
                    {
                        ResultType = SearchResultTypes.Header,
                        HeaderText = "Post Results"
                    };
                    _mSearchResultsList.Add(header);

                    // Insert the items
                    var count = 0;
                    foreach (var post in e.ChangedItems)
                    {
                        // Make the result
                        var postResult = new SearchResult
                        {
                            ResultType = SearchResultTypes.Post,
                            MajorText = post.Title,
                            MinorText = $"{post.SubTextLine1} to {post.Subreddit}",
                            MinorAccentText = $"({post.Score}) score; {post.NumComments} comments",
                            DataContext = post
                        };

                        // Add it to the list
                        _mSearchResultsList.Add(postResult);

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
                        var noResults = new SearchResult
                        {
                            ResultType = SearchResultTypes.NoResults
                        };
                        _mSearchResultsList.Add(noResults);
                    }

                    // Insert show more if we didn't show all
                    if (e.ChangedItems.Count != 0 && !showAll)
                    {
                        // Insert the header
                        var showMore = new SearchResult
                        {
                            ResultType = SearchResultTypes.ShowMore,
                            DataContext = CPostShowMoreHeader
                        };
                        _mSearchResultsList.Add(showMore);
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
            var userResult = await MiscellaneousHelper.GetRedditUser(App.BaconMan, searchTerm);

            // Else put it in the list
            lock(_mSearchResultsList)
            {
                // First check that we are still searching for the same thing
                if(!ui_searchBox.Text.Equals(searchTerm))
                {
                    return;
                }

                // Search for the footer of the subreddits if it is there
                var count = 0;
                var insertIndex = -1;
                foreach(var result in _mSearchResultsList)
                {
                    if(result.ResultType == SearchResultTypes.ShowMore && ((string)result.DataContext).Equals(CSubredditShowMoreHeader))
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
                var header = new SearchResult
                {
                    ResultType = SearchResultTypes.Header,
                    HeaderText = "User Result"
                };
                _mSearchResultsList.Insert(insertIndex, header);
                insertIndex++;

                if (userResult != null)
                {
                    // Insert the User
                    var userItem = new SearchResult
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
                    _mSearchResultsList.Insert(insertIndex, userItem);
                    insertIndex++;
                }
                else
                {
                    // Insert no results
                    var noResults = new SearchResult
                    {
                        ResultType = SearchResultTypes.NoResults
                    };
                    _mSearchResultsList.Insert(insertIndex, noResults);
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
                if(_mCurrentSubCollector != null)
                {
                    _mCurrentSubCollector.OnCollectionUpdated -= CurrentSubCollector_OnCollectionUpdated;
                    _mCurrentSubCollector.OnCollectorStateChange -= CurrentSubCollector_OnCollectorStateChange;
                    _mCurrentSubCollector = null;
                }
                if(_mCurrentPostCollector != null)
                {
                    _mCurrentPostCollector.OnCollectionUpdated -= CurrentPostCollector_OnCollectionUpdated;
                    _mCurrentPostCollector.OnCollectorStateChange -= CurrentPostCollector_OnCollectorStateChange;
                    _mCurrentPostCollector = null;
                }
            }

            // Clear the results
            lock(_mSearchResultsList)
            {
                // Clear them each one by one, this will make them animate out
                while(_mSearchResultsList.Count > 0)
                {
                    _mSearchResultsList.RemoveAt(_mSearchResultsList.Count - 1);
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
            var tappedResult = (SearchResult)ui_searchResults.SelectedItem;
            if(tappedResult != null)
            {
                if(tappedResult.ResultType == SearchResultTypes.ShowMore)
                {
                    var context = ((string)tappedResult.DataContext);
                    if(context.Equals(CSubredditShowMoreHeader))
                    {
                        DoFilteredSearch(SearchResultTypes.Subreddit);
                    }
                    else if(context.Equals(CPostShowMoreHeader))
                    {
                        DoFilteredSearch(SearchResultTypes.Post);
                    }
                }
                else if(tappedResult.ResultType == SearchResultTypes.Subreddit)
                {
                    var subreddit = (Subreddit)tappedResult.DataContext;
                    // If is very important to not send in a upper case display name
                    subreddit.DisplayName = subreddit.DisplayName.ToLower();
                    // Navigate to the subreddit
                    var args = new Dictionary<string, object>();
                    // Send the display name.
                    args.Add(PanelManager.NavArgsSubredditName, subreddit.DisplayName);
                    _mPanelManager.Navigate(typeof(SubredditPanel), subreddit.DisplayName + SortTypes.Hot + SortTimeTypes.Week, args);
                }
                else if(tappedResult.ResultType == SearchResultTypes.Post)
                {
                    var post = (Post)tappedResult.DataContext;

                    // Navigate to the post
                    var args = new Dictionary<string, object>();
                    args.Add(PanelManager.NavArgsSubredditName, post.Subreddit);
                    args.Add(PanelManager.NavArgsForcePostId, post.Id);
                    // Make sure the page id is unique
                    _mPanelManager.Navigate(typeof(FlipViewPanel), post.Subreddit + SortTypes.Hot + SortTimeTypes.Week + post.Id, args);
                }
                else if(tappedResult.ResultType == SearchResultTypes.User)
                {
                    var user = (User)tappedResult.DataContext;

                    // Navigate to the user
                    var args = new Dictionary<string, object>();
                    args.Add(PanelManager.NavArgsUserName, user.Name);
                    _mPanelManager.Navigate(typeof(UserProfile), user.Name, args);
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
                var showBar = !_mArePostsSearching && !_mAreSubredditsSearching && !_mAreUsersSearching;

                switch (type)
                {
                    case SearchResultTypes.Post:
                        _mArePostsSearching = true;
                        break;
                    case SearchResultTypes.Subreddit:
                        _mAreSubredditsSearching = true;
                        break;
                    case SearchResultTypes.User:
                        _mAreUsersSearching = true;
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
                        _mArePostsSearching = false;
                        break;
                    case SearchResultTypes.Subreddit:
                        _mAreSubredditsSearching = false;
                        break;
                    case SearchResultTypes.User:
                        _mAreUsersSearching = false;
                        break;
                }

                if (!_mArePostsSearching && !_mAreSubredditsSearching && !_mAreUsersSearching)
                {
                    ui_progressBar.Visibility = Visibility.Collapsed;
                    ui_progressBar.IsIndeterminate = false;
                }
            }
        }

        #endregion

        /// <summary>
        /// Fired when a link in the content is tapped.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MarkdownTextBlock_OnMarkdownLinkTapped(object sender, UniversalMarkdown.MarkdownLinkTappedArgs e)
        {
            // Show the link
            App.BaconMan.ShowGlobalContent(e.Link);
        }
    }
}
