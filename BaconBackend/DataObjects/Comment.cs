using BaconBackend.Helpers;
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
    public class Comment : INotifyPropertyChanged
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "author")]
        public string Author { get; set; }

        [JsonProperty(PropertyName = "body")]
        public string Body { get; set; }

        [JsonProperty(PropertyName = "replies")]
        public RootElement<Comment> Replies { get; set; }

        [JsonProperty(PropertyName = "created_utc")]
        public double CreatedUtc { get; set; }

        [JsonProperty(PropertyName = "link_id")]
        public string LinkId { get; set; }

        [JsonProperty(PropertyName = "author_flair_text")]
        public string AuthorFlairText { get; set; }

        [JsonProperty(PropertyName = "subreddit")]
        public string Subreddit { get; set; }        

        [JsonProperty(PropertyName = "score")]
        public int Score
        {
            get
            {
                return m_score;
            }
            set
            {
                m_score = value;
                NotifyPropertyChanged(nameof(ScoreText));
            }
        }
        [JsonIgnore]
        int m_score = 0;

        [JsonProperty(PropertyName = "likes")]
        public bool? Likes
        {
            get
            {
                return m_likes;
            }
            set
            {
                m_likes = value;
                NotifyPropertyChanged(nameof(DownVoteColor));
                NotifyPropertyChanged(nameof(UpVoteColor));
            }
        }
        [JsonIgnore]
        bool? m_likes = null;

        [JsonProperty(PropertyName = "saved")]
        public bool IsSaved
        {
            get
            {
                return m_isSaved;
            }
            set
            {
                m_isSaved = value;
                NotifyPropertyChanged(nameof(IsSavedMenuText));
            }
        }
        [JsonIgnore]
        bool m_isSaved;

        //
        // UI Vars
        //
        private static Color s_colorGray = Color.FromArgb(255, 153, 153, 153);
        private static SolidColorBrush s_transparentBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        private static SolidColorBrush s_veryLightAccentBrush = null;
        private static SolidColorBrush s_opAuthorBackground = null;
        private static SolidColorBrush s_accentBrush = null;
        private static SolidColorBrush s_brightAccentColor = null;

        private static SolidColorBrush GetAccentBrush()
        {
            // Not thread safe, but that's ok
            if (s_accentBrush == null)
            {
                s_accentBrush = (SolidColorBrush)Application.Current.Resources["SystemControlBackgroundAccentBrush"];
            }
            return s_accentBrush;
        }

        private static SolidColorBrush GetBrightAccentColor()
        {            
            // Not thread safe, but that's ok
            if (s_brightAccentColor == null)
            {
                Color accent = GetAccentBrush().Color;
                int colorAdd = 70;
                accent.B = (byte)Math.Min(255, accent.B + colorAdd);
                accent.R = (byte)Math.Min(255, accent.R + colorAdd);
                accent.G = (byte)Math.Min(255, accent.G + colorAdd);
                s_brightAccentColor = new SolidColorBrush(accent);
            }
            return s_brightAccentColor;
        }

        private static SolidColorBrush GetLightenedAccentBrush()
        {
            // Not thread safe, but that's ok
            if (s_veryLightAccentBrush == null)
            {
                SolidColorBrush accentBrush = GetAccentBrush();
                Color accentColor = accentBrush.Color;
                accentColor.A = 30;
                s_veryLightAccentBrush = new SolidColorBrush(accentColor);
            }
            return s_veryLightAccentBrush;
        }

        private static SolidColorBrush GetOpAuthorBackground()
        {
            // Not thread safe, but that's ok
            if (s_opAuthorBackground == null)
            {
                Color accent = GetAccentBrush().Color;
                int colorAdd = 25;
                accent.B = (byte)Math.Max(0, accent.B - colorAdd);
                accent.R = (byte)Math.Max(0, accent.R - colorAdd);
                accent.G = (byte)Math.Max(0, accent.G - colorAdd);
                s_opAuthorBackground = new SolidColorBrush(accent);
            }
            return s_opAuthorBackground;
        }

        [JsonIgnore]
        public bool IsHighlighted
        {
            get
            {
                return m_isHighlighted;
            }
            set
            {
                m_isHighlighted = value;
                NotifyPropertyChanged(nameof(CommentBackgroundColor));
            }
        }
        bool m_isHighlighted = false;


        [JsonIgnore]
        public string TimeString { get; set; }

        [JsonIgnore]
        public int CommentDepth { get; set; }

        [JsonIgnore]
        public Thickness CommentMargin
        {
            get
            {
                // Note the rest of the size are applied in XAML by padding.
                // We need a margin of 1 to keep the borders from overlapping.
                return new Thickness((CommentDepth * 8), 1, 0, 0);
            }
        }

        /// <summary>
        /// Sets text for a context menu item
        /// </summary>
        [JsonIgnore]
        public string IsSavedMenuText
        {
            get
            {
                return IsSaved ? "Unsave comment" : "Save comment";
            }
        }

        [JsonIgnore]
        public SolidColorBrush CommentBorderColor
        {
            get
            {
                // Get the accent color
                SolidColorBrush borderBrush;

                if (CommentDepth == 0)
                {
                    // For the first comment, make it darker, this will
                    // differentiate it more
                    borderBrush = GetBrightAccentColor();
                }
                else
                {
                    Color borderColor = GetAccentBrush().Color;
                    int colorSub = CommentDepth * 23;
                    borderColor.B = (byte)Math.Max(0, borderColor.B - colorSub);
                    borderColor.R = (byte)Math.Max(0, borderColor.R - colorSub);
                    borderColor.G = (byte)Math.Max(0, borderColor.G - colorSub);
                    borderBrush = new SolidColorBrush(borderColor);
                }
                return borderBrush;
            }
        }

        [JsonIgnore]
        public SolidColorBrush DownVoteColor
        {
            get
            {
                if (Likes.HasValue && !Likes.Value)
                {
                    return GetAccentBrush();
                }
                else
                {
                    return new SolidColorBrush(s_colorGray);
                }
            }
        }

        [JsonIgnore]
        public SolidColorBrush UpVoteColor
        {
            get
            {
                if (Likes.HasValue && Likes.Value)
                {
                    return GetAccentBrush();
                }
                else
                {
                    return new SolidColorBrush(s_colorGray);
                }
            }
        }

        [JsonIgnore]
        public SolidColorBrush CommentBackgroundColor
        {
            get
            {
                if (IsHighlighted)
                {
                    return GetLightenedAccentBrush();
                }
                else
                {
                    return s_transparentBrush;
                }
            }
        }

        [JsonIgnore]
        public SolidColorBrush FlairBrush
        {
            get
            {
                return GetBrightAccentColor();
            }
        }


        [JsonIgnore]
        public SolidColorBrush AuthorTextBackground
        {
            get
            {
                if (IsCommentFromOp)
                {
                    return GetOpAuthorBackground();
                }
                else
                {
                    return s_transparentBrush;
                }
            }
        }

        [JsonIgnore]
        public SolidColorBrush AuthorTextColor
        {
            get
            {
                if (IsCommentFromOp)
                {
                    return new SolidColorBrush(Color.FromArgb(255,255,255,255));
                }
                else
                {
                    return GetAccentBrush();
                }
            }
        }

        [JsonIgnore]
        public Visibility ShowFullCommentVis
        {
            get
            {
                return m_showFullComment ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        [JsonIgnore]
        public Visibility ShowCollapsedCommentVis
        {
            get
            {
                return m_showFullComment ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        [JsonIgnore]
        public Visibility ShowFlairText
        {
            get
            {
                return String.IsNullOrWhiteSpace(AuthorFlairText) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        [JsonIgnore]
        public bool ShowFullComment
        {
            get
            {
                return m_showFullComment;
            }
            set
            {
                m_showFullComment = value;
                NotifyPropertyChanged(nameof(ShowFullCommentVis));
                NotifyPropertyChanged(nameof(ShowCollapsedCommentVis));
            }
        }
        [JsonIgnore]
        bool m_showFullComment = true;

        [JsonIgnore]
        public bool IsCommentFromOp
        {
            get
            {
                return m_isCommentFromOp;
            }
            set
            {
                m_isCommentFromOp = value;
                NotifyPropertyChanged(nameof(AuthorTextBackground));
            }
        }
        [JsonIgnore]
        bool m_isCommentFromOp = false;

        [JsonIgnore]
        public string CollapsedCommentCount
        {
            get
            {
                return m_collapsedCommentCount;
            }
            set
            {
                m_collapsedCommentCount = value;
                NotifyPropertyChanged(nameof(CollapsedCommentCount));
            }
        }
        [JsonIgnore]
        string m_collapsedCommentCount = "";

        [JsonIgnore]
        public string ScoreText
        {
            get
            {
                return $"{Score} points";
            }
        }


        public Comment()
        { }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="copyComment"></param>
        public Comment(Comment copyComment)
        {
            Id = copyComment.Id;
            Author = copyComment.Author;
            Body = copyComment.Body;
            CommentDepth = copyComment.CommentDepth;
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
