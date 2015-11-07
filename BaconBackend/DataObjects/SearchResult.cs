using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace BaconBackend.DataObjects
{
    public enum SearchResultTypes
    {
        Header,
        ShowMore,
        NoResults,
        Subreddit,
        User,
        Post
    }

    /// <summary>
    /// Used to populate the search results list
    /// </summary>
    public class SearchResult : INotifyPropertyChanged
    {
        public SearchResultTypes ResultType;

        public string HeaderText { get; set; }

        public string MajorText { get; set; }

        public string MinorText { get; set; }

        public string MinorAccentText { get; set; }

        public object DataContext;

        public Visibility ShowMinorText { get; set; } = Visibility.Visible;

        public Visibility ShowHeader
        {
            get
            {
                return ResultType == SearchResultTypes.Header ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Visibility ShowText
        {
            get
            {
                return (ResultType == SearchResultTypes.Subreddit || ResultType == SearchResultTypes.User || ResultType == SearchResultTypes.Post) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Visibility ShowShowMore
        {
            get
            {
                return ResultType == SearchResultTypes.ShowMore ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Visibility ShowNoResults
        {
            get
            {
                return ResultType == SearchResultTypes.NoResults ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // UI property changed handler
        public event PropertyChangedEventHandler PropertyChanged;
        protected void NotifyPropertyChanged(String propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (null != handler)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
