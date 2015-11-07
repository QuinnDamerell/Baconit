using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace BaconBackend.DataObjects
{
    public class Message : INotifyPropertyChanged
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "body")]
        public string Body { get; set; }

        [JsonProperty(PropertyName = "subreddit")]
        public string Subreddit { get; set; }

        [JsonProperty(PropertyName = "author")]
        public string Author { get; set; }

        [JsonProperty(PropertyName = "was_comment")]
        public bool WasComment { get; set; }

        [JsonProperty(PropertyName = "subject")]
        public string Subject { get; set; }

        [JsonProperty(PropertyName = "context")]
        public string Context { get; set; }

        [JsonProperty(PropertyName = "new")]
        public bool IsNew
        {
            get
            {
                return m_isNew;
            }
            set
            {
                m_isNew = value;
                NotifyPropertyChanged(nameof(BorderColor));
                NotifyPropertyChanged(nameof(MarkAsReadText));
            }
        }
        bool m_isNew = false;

        /// <summary>
        /// Returns the full name of the message.
        /// </summary>
        /// <returns></returns>
        public string GetFullName()
        {
            return WasComment ? "t1_" + Id : "t4_" + Id;
        }

        //
        // UI vars
        //

        // Static Cache Colors
        private static SolidColorBrush s_accentBrush = null;
        private static SolidColorBrush s_grayBrush = null;
        private static SolidColorBrush GetAccentBrush()
        {
            // Not thread safe, but that's ok
            if (s_accentBrush == null)
            {
                s_accentBrush = (SolidColorBrush)Application.Current.Resources["SystemControlBackgroundAccentBrush"];
            }
            return s_accentBrush;
        }

        private static SolidColorBrush GetGrayBrush()
        {
            // Not thread safe, but that's ok
            if (s_grayBrush == null)
            {
                s_grayBrush = new SolidColorBrush(Color.FromArgb(153, 255, 255, 255));
            }
            return s_grayBrush;
        }

        [JsonIgnore]
        public string HeaderFirst { get; set; }

        [JsonIgnore]
        public string HeaderSecond { get; set; }

        [JsonIgnore]
        public SolidColorBrush BorderColor
        {
            get
            {
                return IsNew ? GetAccentBrush() : GetGrayBrush();
            }
        }

        [JsonIgnore]
        public string MarkAsReadText
        {
            get
            {
                return IsNew ? "mark as read" : "mark as unread";
            }
        }

        [JsonIgnore]
        public Visibility ShowViewContextUi
        {
            get
            {
                return WasComment ? Visibility.Visible : Visibility.Collapsed;
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
