﻿using System.ComponentModel;
using Windows.UI.Xaml;

namespace BaconBackend.DataObjects
{
    /// <summary>
    /// Types of list items in a search result list.
    /// </summary>
    public enum SearchResultTypes
    {
        /// <summary>
        /// A header to indicate another section of search results. Not a search result type itself.
        /// </summary>
        Header,
        /// <summary>
        /// A footer button to show more results of the header type when clicked. Not a search result type itself.
        /// </summary>
        ShowMore,
        /// <summary>
        /// An item that indicates there are no search results of the header type. Not a search result type itself.
        /// </summary>
        NoResults,
        /// <summary>
        /// A subreddit that matches the search term.
        /// </summary>
        Subreddit,
        /// <summary>
        /// A user that matches the search term.
        /// </summary>
        User,
        /// <summary>
        /// A post that matches the search term.
        /// </summary>
        Post
    }

    /// <summary>
    /// Used to populate the search results list
    /// </summary>
    public class SearchResult : INotifyPropertyChanged
    {
        /// <summary>
        /// The type of search result list item this is.
        /// </summary>
        public SearchResultTypes ResultType;

        /// <summary>
        /// The header text to display when when the search result type is Header. Null otherwise.
        /// </summary>
        public string HeaderText { get; set; }

        /// <summary>
        /// The main title of the search result.
        /// For a subreddit or post, its title.
        /// For a user, its username.
        /// For other result types, it is null.
        /// </summary>
        public string MajorText { get; set; }

        /// <summary>
        /// The subtitle of the search result.
        /// For a subreddit, its description.
        /// For a user, its karma counts.
        /// For a post, its age and subreddit.
        /// For other result types, it is null.
        /// </summary>
        public string MinorText { get; set; }

        /// <summary>
        /// The subtitle of the search result formatted with markdown.
        /// For a subreddit, its description.
        /// For others it is null.
        /// </summary>
        public string MarkdownText { get; set; }

        /// <summary>
        /// The accented text of the search result.
        /// For a subreddit, its name.
        /// For a post, its score and comment count.
        /// For other result types, it is null.
        /// </summary>
        public string MinorAccentText { get; set; }

        /// <summary>
        /// This is the context for the search result, a user, post, or subreddit.
        /// </summary>
        public object DataContext;

        /// <summary>
        /// Whether the search result view should show the minor text.
        /// This is only true on subreddit search results without minor text.
        /// </summary>
        public Visibility ShowMinorText => string.IsNullOrWhiteSpace(MinorText) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// Whether the search result view should show this result's header.
        /// True only if this search result list item is a header.
        /// </summary>
        public Visibility ShowHeader => ResultType == SearchResultTypes.Header ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Whether the search result view should show this result's major and minor text.
        /// True only if the search result item has major or minor text.
        /// </summary>
        public Visibility ShowText => (ResultType == SearchResultTypes.Subreddit || ResultType == SearchResultTypes.User || ResultType == SearchResultTypes.Post) ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Whether the search result view should show the "show more" text.
        /// True only if this search result list item's type is ShowMore.
        /// </summary>
        public Visibility ShowShowMore => ResultType == SearchResultTypes.ShowMore ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Whether the search result view should show the "no results" text.
        /// True only if this search result list item's type is NoResults.
        /// </summary>
        public Visibility ShowNoResults => ResultType == SearchResultTypes.NoResults ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Whether the search result view should show the "no results" text.
        /// True only if this search result list item's type is NoResults.
        /// </summary>
        public Visibility ShowMarkdownText => string.IsNullOrWhiteSpace(MarkdownText) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// UI property changed handler that's called when a property of this comment is changed.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Called to indicate a property of this object has changed.
        /// </summary>
        /// <param name="propertyName">Name of the changed property.</param>
        protected void NotifyPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
