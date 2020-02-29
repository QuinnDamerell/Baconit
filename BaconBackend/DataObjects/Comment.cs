using BaconBackend.Helpers;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace BaconBackend.DataObjects
{
    /// <summary>
    /// A comment on a post. May or may not be a comment on another comment.
    /// A comment has a score, which is the total number of upvotes - total number of downvotes.
    /// </summary>
    public class Comment : INotifyPropertyChanged
    {
        /// <summary>
        /// The comment's unique ID. Prefixed with "t1_", this 
        /// is the comment's fullname.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        /// <summary>
        /// The username of the user who wrote this comment.
        /// </summary>
        [JsonProperty(PropertyName = "author")]
        public string Author { get; set; }

        /// <summary>
        /// The comment's body text, in Markdown.
        /// </summary>
        [JsonProperty(PropertyName = "body")]
        public string Body
        {
            get => _body;
            set
            {
                _body = value;
                NotifyPropertyChanged(nameof(Body));
            }
        }
        [JsonIgnore] private string _body;

        /// <summary>
        /// The tree of comments replied to this one.
        /// </summary>
        [JsonProperty(PropertyName = "replies")]
        public RootElement<Comment> Replies { get; set; }

        /// <summary>
        /// Unix timestamp of the time this comment was posted.
        /// Or, the number of seconds that have passed since
        /// January 1, 1970 UTC until this comment was posted.
        /// </summary>
        [JsonProperty(PropertyName = "created_utc")]
        public double CreatedUtc { get; set; }

        /// <summary>
        /// The fullname of the post this comment is on.
        /// </summary>
        [JsonProperty(PropertyName = "link_id")]
        public string LinkId { get; set; }

        /// <summary>
        /// The flair text of the comment's author.
        /// </summary>
        [JsonProperty(PropertyName = "author_flair_text")]
        public string AuthorFlairText { get; set; }
        
        /// <summary>
        /// The subreddit the post this comment is on is in.
        /// </summary>
        [JsonProperty(PropertyName = "subreddit")]
        public string Subreddit { get; set; }

        /// <summary>
        /// IF the comment has been gilded
        /// </summary>
        [JsonProperty(PropertyName = "gilded")]
        public bool IsGilded { get; set; }
        
        /// <summary>
        /// The comment's score: total upvotes - total downvotes.
        /// </summary>
        [JsonProperty(PropertyName = "score")]
        public int Score
        {
            get => _mScore;
            set
            {
                _mScore = value;
                NotifyPropertyChanged(nameof(ScoreText));
            }
        }
        [JsonIgnore] private int _mScore;

        /// <summary>
        /// true: the logged-in user upvoted the comment.
        /// false: the logged-in user downvoted the comment.
        /// null: the logged-in user has neither upvoted nor downvoted the comment.
        /// </summary>
        [JsonProperty(PropertyName = "likes")]
        public bool? Likes
        {
            get => _likes;
            set
            {
                _likes = value;
                NotifyPropertyChanged(nameof(DownVoteColor));
                NotifyPropertyChanged(nameof(UpVoteColor));
            }
        }
        [JsonIgnore] private bool? _likes;

        /// <summary>
        /// If the logged-in user saved the comment.
        /// </summary>
        [JsonProperty(PropertyName = "saved")]
        public bool IsSaved
        {
            get => _isSaved;
            set
            {
                _isSaved = value;
                NotifyPropertyChanged(nameof(IsSavedMenuText));
            }
        }
        [JsonIgnore] private bool _isSaved;

        //
        // UI Vars
        //
        private static readonly Color ColorGray = Color.FromArgb(255, 153, 153, 153);
        private static readonly SolidColorBrush TransparentBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        private static SolidColorBrush s_veryLightAccentBrush;
        private static SolidColorBrush s_opAuthorBackground;
        private static SolidColorBrush s_accentBrush;
        private static SolidColorBrush s_brightAccentColor;

        private static SolidColorBrush GetAccentBrush()
        {
            // Not thread safe, but that's ok
            return s_accentBrush ?? (s_accentBrush =
                (SolidColorBrush) Application.Current.Resources["SystemControlBackgroundAccentBrush"]);
        }

        private static SolidColorBrush GetBrightAccentColor()
        {            
            // Not thread safe, but that's ok
            if (s_brightAccentColor != null) return s_brightAccentColor;
            var accent = GetAccentBrush().Color;
            const int colorAdd = 70;
            accent.B = (byte)Math.Min(255, accent.B + colorAdd);
            accent.R = (byte)Math.Min(255, accent.R + colorAdd);
            accent.G = (byte)Math.Min(255, accent.G + colorAdd);
            s_brightAccentColor = new SolidColorBrush(accent);
            return s_brightAccentColor;
        }

        private static SolidColorBrush GetLightenedAccentBrush()
        {
            // Not thread safe, but that's ok
            if (s_veryLightAccentBrush != null) return s_veryLightAccentBrush;
            var accentBrush = GetAccentBrush();
            var accentColor = accentBrush.Color;
            accentColor.A = 30;
            s_veryLightAccentBrush = new SolidColorBrush(accentColor);
            return s_veryLightAccentBrush;
        }

        private static SolidColorBrush GetOpAuthorBackground()
        {
            // Not thread safe, but that's ok
            if (s_opAuthorBackground != null) return s_opAuthorBackground;
            var accent = GetAccentBrush().Color;
            const int colorAdd = 25;
            accent.B = (byte)Math.Max(0, accent.B - colorAdd);
            accent.R = (byte)Math.Max(0, accent.R - colorAdd);
            accent.G = (byte)Math.Max(0, accent.G - colorAdd);
            s_opAuthorBackground = new SolidColorBrush(accent);
            return s_opAuthorBackground;
        }

        /// <summary>
        /// If this comment should be highlighted in the current UI,
        /// e.g. if it's the target of a link followed from elsewhere in the app.
        /// </summary>
        [JsonIgnore]
        public bool IsHighlighted
        {
            get => _mIsHighlighted;
            set
            {
                _mIsHighlighted = value;
                NotifyPropertyChanged(nameof(CommentBackgroundColor));
            }
        }

        private bool _mIsHighlighted;

        /// <summary>
        /// Text representing the elapsed time since this comment was made.
        /// </summary>
        [JsonIgnore]
        public string TimeString { get; set; }

        /// <summary>
        /// The number of comments above this one in the comment tree.
        /// </summary>
        [JsonIgnore]
        public int CommentDepth { get; set; }

        /// <summary>
        /// The thickness that should precede this comment,
        /// to represent its depth.
        /// </summary>
        [JsonIgnore]
        // Note the rest of the size are applied in XAML by padding.
        // We need a margin of 1 to keep the borders from overlapping.
        public Thickness CommentMargin => new Thickness((CommentDepth * 8), 1, 0, 0);

        /// <summary>
        /// Sets text for a context menu item
        /// </summary>
        [JsonIgnore]
        public string IsSavedMenuText => IsSaved ? "Unsave comment" : "Save comment";

        /// <summary>
        /// The color this comment's left bar should be in the UI.
        /// </summary>
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
                    var borderColor = GetAccentBrush().Color;
                    var colorSub = CommentDepth * 23;
                    borderColor.B = (byte)Math.Max(0, borderColor.B - colorSub);
                    borderColor.R = (byte)Math.Max(0, borderColor.R - colorSub);
                    borderColor.G = (byte)Math.Max(0, borderColor.G - colorSub);
                    borderBrush = new SolidColorBrush(borderColor);
                }
                return borderBrush;
            }
        }

        /// <summary>
        /// The color this comment's downvote button should be in the UI.
        /// It is accented if and only if the user has downvoted this comment.
        /// </summary>
        [JsonIgnore]
        public SolidColorBrush DownVoteColor
        {
            get
            {
                if (Likes.HasValue && !Likes.Value)
                {
                    return GetAccentBrush();
                }

                return new SolidColorBrush(ColorGray);
            }
        }

        /// <summary>
        /// The color this comment's upvote button should be in the UI.
        /// It is accented if and only if the user has upvoted this comment.
        /// </summary>
        [JsonIgnore]
        public SolidColorBrush UpVoteColor
        {
            get
            {
                if (Likes.HasValue && Likes.Value)
                {
                    return GetAccentBrush();
                }

                return new SolidColorBrush(ColorGray);
            }
        }

        /// <summary>
        /// The color this comment's background should be in the UI.
        /// It is accented if and only if the comment is highlighted.
        /// </summary>

        [JsonIgnore]
        public SolidColorBrush CommentBackgroundColor
        {
            get
            {
                if (IsHighlighted)
                {
                    return GetLightenedAccentBrush();
                }

                return TransparentBrush;
            }
        }

        /// <summary>
        /// Unused?
        /// </summary>
        [JsonIgnore]
        public SolidColorBrush FlairBrush => GetBrightAccentColor();

        /// <summary>
        /// The highlight color of the comment author's name in the UI.
        /// It is accented only if the comment is written by the author of the post it is on.
        /// </summary>
        [JsonIgnore]
        public SolidColorBrush AuthorTextBackground
        {
            get
            {
                if (IsCommentFromOp)
                {
                    return GetOpAuthorBackground();
                }

                return TransparentBrush;
            }
        }

        /// <summary>
        /// The text color of the comment author's name in the UI.
        /// It is accented only if the comment is written by the author of the post it is on.
        /// </summary>
        [JsonIgnore]
        public SolidColorBrush AuthorTextColor
        {
            get
            {
                if (IsCommentFromOp)
                {
                    return new SolidColorBrush(Color.FromArgb(255,255,255,255));
                }

                return GetAccentBrush();
            }
        }

        /// <summary>
        /// The visibility of this comment uncollapsed.
        /// </summary>
        [JsonIgnore]
        public Visibility ShowFullCommentVis => _mShowFullComment ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// The visibility of this comment as collapsed.
        /// </summary>
        [JsonIgnore]
        public Visibility ShowCollapsedCommentVis => _mShowFullComment ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// The visibility of the comment author's flair.
        /// </summary>
        [JsonIgnore]
        public Visibility ShowFlairText => string.IsNullOrWhiteSpace(AuthorFlairText) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// The visibility of the comment giled status.
        /// </summary>
        [JsonIgnore]
        public Visibility GildedVisibility => IsGilded ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Whether the comment should be uncollapsed.
        /// </summary>
        [JsonIgnore]
        public bool ShowFullComment
        {
            get => _mShowFullComment;
            set
            {
                _mShowFullComment = value;
                NotifyPropertyChanged(nameof(ShowFullCommentVis));
                NotifyPropertyChanged(nameof(ShowCollapsedCommentVis));
            }
        }
        [JsonIgnore] private bool _mShowFullComment = true;

        /// <summary>
        /// Whether the comment is written by the author of the post it is on.
        /// </summary>
        [JsonIgnore]
        public bool IsCommentFromOp
        {
            get => _mIsCommentFromOp;
            set
            {
                _mIsCommentFromOp = value;
                NotifyPropertyChanged(nameof(AuthorTextBackground));
            }
        }
        [JsonIgnore] private bool _mIsCommentFromOp;

        /// <summary>
        /// Text representing the additional comments that are children to this one.
        /// This should always be a plus sign (+) followed by an integer.
        /// </summary>
        [JsonIgnore]
        public string CollapsedCommentCount
        {
            get => _mCollapsedCommentCount;
            set
            {
                _mCollapsedCommentCount = value;
                NotifyPropertyChanged(nameof(CollapsedCommentCount));
            }
        }
        [JsonIgnore] private string _mCollapsedCommentCount = "";

        /// <summary>
        /// Text to represent this comment's current score.
        /// </summary>
        [JsonIgnore]
        public string ScoreText => $"{Score} points";

        /// <summary>
        /// Indicates if this comment is from the user.
        /// </summary>
        [JsonIgnore]
        public bool IsCommentOwnedByUser
        {
            get => _mIsCommentOwnedByUser;
            set
            {
                _mIsCommentOwnedByUser = value;
                NotifyPropertyChanged(nameof(CommentButton3Text));
                NotifyPropertyChanged(nameof(CommentButton4Text));
            }

        }
        [JsonIgnore] private bool _mIsCommentOwnedByUser;

        /// <summary>
        /// Text that is shown on the UI for comment button 3
        /// </summary>
        [JsonIgnore]
        public string CommentButton3Text => IsCommentOwnedByUser ? "edit" : "reply";

        /// <summary>
        /// Text that is shown on the UI for comment button 4
        /// </summary>
        [JsonIgnore]
        public string CommentButton4Text => IsCommentOwnedByUser ? "delete" : "user";

        /// <summary>
        /// Indicates if the comment has been deleted or not.
        /// </summary>
        [JsonIgnore]
        public bool IsDeleted = false;

        /// <summary>
        /// Construct a new empty comment.
        /// </summary>
        public Comment()
        { }

        /// <summary>
        /// Construct a new comment with the same content as a given comment.
        /// </summary>
        /// <param name="copyComment">Comment to copy.</param>
        public Comment(Comment copyComment)
        {
            Id = copyComment.Id;
            Author = copyComment.Author;
            Body = copyComment.Body;
            CommentDepth = copyComment.CommentDepth;
        }

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
