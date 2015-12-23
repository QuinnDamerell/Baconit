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
    /// <summary>
    /// A message in a user's inbox. This may be a reply on a comment or post the user made.
    /// Comments can be read or unread.
    /// </summary>
    public class Message : INotifyPropertyChanged
    {
        /// <summary>
        /// The message's unique ID, or the comment's ID if it is a comment. Prefixed with "t1_" for comments, 
        /// or "t4_" for private messages, this is the message's fullname.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        /// <summary>
        /// The message's body text, in Markdown.
        /// </summary>
        [JsonProperty(PropertyName = "body")]
        public string Body { get; set; }

        /// <summary>
        /// If the message is a comment reply, the subreddit the comment is in.
        /// Otherwise, null.
        /// </summary>
        [JsonProperty(PropertyName = "subreddit")]
        public string Subreddit { get; set; }

        /// <summary>
        /// The user who wrote the message.
        /// </summary>
        [JsonProperty(PropertyName = "author")]
        public string Author { get; set; }

        /// <summary>
        /// If the message was in the form of a comment, rather than a PM.
        /// </summary>
        [JsonProperty(PropertyName = "was_comment")]
        public bool WasComment { get; set; }

        /// <summary>
        /// If a private message, the subject of the message. Otherwise "comment reply".
        /// </summary>
        [JsonProperty(PropertyName = "subject")]
        public string Subject { get; set; }

        /// <summary>
        /// A reference to the comment with context to the highest ancestor comment.
        /// This link leaves off the domain, and starts with "/r/".
        /// </summary>
        [JsonProperty(PropertyName = "context")]
        public string Context { get; set; }

        /// <summary>
        /// If the user marked this message as read.
        /// </summary>
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

        /// <summary>
        /// The message subject.
        /// </summary>
        [JsonIgnore]
        public string HeaderFirst { get; set; }

        /// <summary>
        /// The message author, followed by the subreddit it was posted on if this message is a comment.
        /// </summary>
        [JsonIgnore]
        public string HeaderSecond { get; set; }

        /// <summary>
        /// Get the color the message's unread indicator should be.
        /// It should be accented if and only if the message is unread.
        /// </summary>
        [JsonIgnore]
        public SolidColorBrush BorderColor
        {
            get
            {
                return IsNew ? GetAccentBrush() : GetGrayBrush();
            }
        }

        /// <summary>
        /// Get the text of the button to toggle the read state of this message.
        /// This should prompt the user to make the message in the opposite state
        /// as it currently is.
        /// </summary>
        [JsonIgnore]
        public string MarkAsReadText
        {
            get
            {
                return IsNew ? "mark as read" : "mark as unread";
            }
        }

        /// <summary>
        /// The visibility of the "view comment in context" button on the message in the inbox.
        /// </summary>
        [JsonIgnore]
        public Visibility ShowViewContextUi
        {
            get
            {
                return WasComment ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// UI property changed handler that's called when a property of this comment is changed.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Called to indicate a property of this object has changed.
        /// </summary>
        /// <param name="propertyName">Name of the changed property.</param>
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
