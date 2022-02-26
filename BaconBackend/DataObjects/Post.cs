using BaconBackend.Collectors;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Newtonsoft.Json.Linq;

namespace BaconBackend.DataObjects
{
    /// <summary>
    /// A reddit post, either of a link or of text.
    /// A post has a score, which is the total number of up votes - total number of down votes.
    /// </summary>
    [JsonObject(MemberSerialization.OptOut)]
    public class Post : BindableBase
    {
        /// <summary>
        /// The comment's unique ID. Prefixed with "t3_", this
        /// is the post's fullname.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        /// <summary>
        /// If the post is self-text, the post's subreddit, preceded by "self.".
        /// If the post is a link, the website domain the post is a link to.
        /// </summary>
        [JsonProperty(PropertyName = "domain")]
        public string Domain { get; set; }

        /// <summary>
        /// The subreddit this post occurs in.
        /// </summary>
        [JsonProperty(PropertyName = "subreddit")]
        public string Subreddit { get; set; }

        /// <summary>
        /// The post's self-text. If the post is a link, this is the empty string.
        /// </summary>
        [JsonProperty(PropertyName = "selftext")]
        public string Selftext { get; set; }

        /// <summary>
        /// The user who submitted this post.
        /// </summary>
        [JsonProperty(PropertyName = "author")]
        public string Author { get; set; }

        /// <summary>
        /// The comment's score: total up votes - total down votes.
        /// </summary>
        [JsonProperty(PropertyName = "score")]
        public int Score
        {
            get => _mScore;
            set => SetProperty(ref _mScore, value);
        }
        [JsonIgnore] private int _mScore;

        /// <summary>
        /// If this post is marked a only for ages 18 years or older.
        /// </summary>
        [JsonProperty(PropertyName = "over_18")]
        public bool IsOver18 { get; set; }

        /// <summary>
        /// If this post is stickied to the top of the subreddit's posts, when sorted by hotness.
        /// </summary>
        [JsonProperty(PropertyName = "stickied")]
        public bool IsStickied { get; set; }

        /// <summary>
        /// If this post is self-text, (instead of a link).
        /// </summary>
        [JsonProperty(PropertyName = "is_self")]
        public bool IsSelf { get; set; }

        /// <summary>
        /// The post's link, or the post's permalink if it is self-text.
        /// </summary>
        [JsonProperty(PropertyName = "url")]
        public string Url { get; set; }

        /// <summary>
        /// The post's title.
        /// </summary>
        [JsonProperty(PropertyName = "title")]
        public string Title { get; set; }

        /// <summary>
        /// Unix timestamp of the time this post was submitted.
        /// Or, the number of seconds that have passed since
        /// January 1, 1970 UTC until this post was submitted.
        /// </summary>
        [JsonProperty(PropertyName = "created_utc")]
        public double CreatedUtc { get; set; }

        /// <summary>
        /// The number of comments on this post, counting all replies to other comments.
        /// </summary>
        [JsonProperty(PropertyName = "num_comments")]
        public int NumComments { get; set; }

        /// <summary>
        /// A link to the preview image used when listing posts, "self" if the post is self-text,
        /// or the empty string if there is no preview image.
        /// </summary>
        [JsonProperty(PropertyName = "thumbnail")]
        public string Thumbnail { get; set; }

        /// <summary>
        /// A link to the post.
        /// </summary>
        [JsonProperty(PropertyName = "permalink")]
        public string Permalink { get; set; }

        /// <summary>
        /// The post's flair text.
        /// </summary>
        [JsonProperty(PropertyName = "link_flair_text")]
        public string LinkFlairText { get; set; }

        /// <summary>
        /// If the post is gilded or not.
        /// </summary>
        [JsonProperty(PropertyName = "gilded")]
        public bool Gilded { get; set; }

        /// <summary>
        /// true: the logged-in user upvoted the post.
        /// false: the logged-in user downvoted the post.
        /// null: the logged-in user has neither upvoted nor downvoted the post.
        /// </summary>
        [JsonProperty(PropertyName = "likes")]
        public bool? Likes
        {
            get => _likes;
            set
            {
                if (!SetProperty(ref _likes, value)) return;
                OnPropertyChanged(nameof(DownVoteColor));
                OnPropertyChanged(nameof(UpVoteColor));
            }
        }
        [JsonIgnore] private bool? _likes;

        /// <summary>
        /// Whether the logged in user has saved the post.
        /// </summary>
        [JsonProperty(PropertyName = "saved")]
        public bool IsSaved
        {
            get => _isSaved;
            set
            {
                if(SetProperty(ref _isSaved, value))
                {
                    OnPropertyChanged(nameof(IsSavedMenuText));
                }
            }
        }
        [JsonIgnore] private bool _isSaved;

        /// <summary>
        /// Whether the user hid the post from being listed normally.
        /// </summary>
        [JsonProperty(PropertyName = "hidden")]
        public bool IsHidden
        {
            get => _isHidden;
            set
            {
                if (SetProperty(ref _isHidden, value))
                {
                    OnPropertyChanged(nameof(IsHiddenMenuText));
                }
            }
        }
        [JsonIgnore] private bool _isHidden;

        [JsonProperty(PropertyName = "secure_media")]
        public SecureMedia SecureMedia { get; set; }

        [JsonProperty(PropertyName = "post_hint")]
        public string PostHint { get; set; }
        
        [JsonProperty(PropertyName = "gallery_data")]
        public GalleryData GalleryData { get; set; }

        private dynamic _mediaMetaData;

        [JsonProperty(PropertyName = "media_metadata")]
        public dynamic MediaMetaData
        {
            get => _mediaMetaData;
            set
            {
                _mediaMetaData = value;
                MediaImages = GetMediaImages();
            }
        }

        public bool IsGallery => MediaImages.Any();

        public IEnumerable<MediaImage> MediaImages { get; internal set; } = new List<MediaImage>();

        internal IEnumerable<MediaImage> GetMediaImages()
        {
            if (MediaMetaData == null) return new List<MediaImage>();
            var jObject = new JObject(MediaMetaData);
            var props = jObject.Properties().Where(p => p.HasValues);

            var previewImages = new List<PreviewMediaImage>();
            foreach (var prop in props)
            {
                var children = prop.Children().ToList();
                var previews = children.SelectMany(p => p["p"]).Select(p => p.ToObject<PreviewMediaImage>()).Where(p => p != null).OrderBy(p => p.Width).ToList();
                if (previews.Count.Equals(0))
                {
                    continue;
                }
                var preview = previews.Any(p => p.Width > 640) ? previews.First(p => p.Width > 640) : previews.Last();
                preview.MediaId = prop.Name;
                previewImages.Add(preview);
            }

            return previewImages.Where(p => p != null && !string.IsNullOrWhiteSpace(p.Url)).Select(p => new MediaImage
            {
                Uri = new Uri(p.Url),
                Url = p.Url,
                Id = p.MediaId
            });
        }

        internal class PreviewMediaImage
        {
            public string MediaId { get; set; }
            [JsonProperty(PropertyName = "y")]
            public int Height { get; set; }
            [JsonProperty(PropertyName = "x")]
            public int Width { get; set; }
            [JsonProperty(PropertyName = "u")]
            public string Url { get; set; }
        }
        [JsonIgnore]
        public bool IsVideo => !string.IsNullOrWhiteSpace(PostHint) && PostHint.Contains("video");

        /// <summary>
        /// Represents the current comment sort type for this post
        /// </summary>
        [JsonIgnore]
        public CommentSortTypes CommentSortType
        {
            get => _mCommentSortType;
            set
            {
                if(SetProperty(ref _mCommentSortType, value))
                {
                    OnPropertyChanged(nameof(CommentCurrentSortTypeString));
                }
            }
        }
        [JsonIgnore] private CommentSortTypes _mCommentSortType = CommentSortTypes.Best;

        /// <summary>
        /// Indicates if we have seeded this post with the defaults yet.
        /// </summary>
        [JsonIgnore]
        public bool HaveCommentDefaultsBeenSet;

        //
        // UI Vars
        //

        // Static Cache Colors
        private static Color s_colorWhite = Color.FromArgb(255, 255, 255, 255);
        private static Color s_colorGray = Color.FromArgb(255, 152, 152, 152);
        private static SolidColorBrush s_accentBrush;
        private static SolidColorBrush s_lightenedAccentBrush;
        private static SolidColorBrush s_darkenedAccentBrush;
        private static SolidColorBrush GetAccentBrush()
        {
            // Not thread safe, but that's ok
            return s_accentBrush ?? (s_accentBrush =
                (SolidColorBrush) Application.Current.Resources["SystemControlBackgroundAccentBrush"]);
        }

        private static SolidColorBrush GetLightenedAccentBrush()
        {
            // Not thread safe, but that's ok
            if (s_lightenedAccentBrush != null) return s_lightenedAccentBrush;
            var accentBrush = GetAccentBrush();
            var accentColor = accentBrush.Color;
            accentColor.B = (byte)Math.Min(255, accentColor.B + 80);
            accentColor.R = (byte)Math.Min(255, accentColor.R + 80);
            accentColor.G = (byte)Math.Min(255, accentColor.G + 80);
            s_lightenedAccentBrush = new SolidColorBrush(accentColor);
            return s_lightenedAccentBrush;
        }

        private static SolidColorBrush GetDarkenedAccentBrush()
        {
            // Not thread safe, but that's ok
            if (s_darkenedAccentBrush != null) return s_darkenedAccentBrush;
            var accentBrush = GetAccentBrush();
            var accentColor = accentBrush.Color;
            accentColor.B = (byte)Math.Max(0, accentColor.B - 40);
            accentColor.R = (byte)Math.Max(0, accentColor.R - 40);
            accentColor.G = (byte)Math.Max(0, accentColor.G - 40);
            s_darkenedAccentBrush = new SolidColorBrush(accentColor);
            return s_darkenedAccentBrush;
        }

        /// <summary>
        /// Used in the subreddit view for the sub text
        /// </summary>
        [JsonIgnore]
        public string SubTextLine1 { get; set; }

        /// <summary>
        /// Used in subreddit view for the 2nd line first color
        /// </summary>
        [JsonIgnore]
        public string SubTextLine2PartTwo { get; set; }

        /// <summary>
        /// Used in subreddit view for the 2nd line second color
        /// </summary>
        [JsonIgnore]
        public string SubTextLine2PartOne { get; set; }

        /// <summary>
        /// Used in flip view for the second line of text
        /// </summary>
        [JsonIgnore]
        public string FlipViewSecondary { get; set; }

        /// <summary>
        /// Used by subreddit view to show unread comment count
        /// </summary>
        [JsonIgnore]
        public string NewCommentText
        {
            get => _mNewCommentText;
            set
            {
                if (!SetProperty(ref _mNewCommentText, value)) return;
                OnPropertyChanged(nameof(NewCommentColor));
                OnPropertyChanged(nameof(NewCommentMargin));
            }
        }
        [JsonIgnore] private string _mNewCommentText = "";

        /// <summary>
        /// Used by the subreddit view to show the image grid
        /// </summary>
        [JsonIgnore]
        public Visibility ImageVisibility
        {
            get => _mImageVisibility;
            set => SetProperty(ref _mImageVisibility, value);
        }
        [JsonIgnore] private Visibility _mImageVisibility = Visibility.Collapsed;

        /// <summary>
        /// Used by subreddit view to hold the bit map
        /// </summary>
        [JsonIgnore]
        public BitmapImage Image
        {
            get => _mImage;
            set => SetProperty(ref _mImage, value);
        }
        [JsonIgnore] private BitmapImage _mImage;

        /// <summary>
        /// Note! This is the color for the title but the UI really looks at the brush.
        /// Thus when the color update we want to fire the change on the brush.
        /// </summary>
        [JsonIgnore]
        public Color TitleTextColor
        {
            get => _mTitleTextColor;
            set
            {
                _mTitleTextColor = value;
                OnPropertyChanged(nameof(TitleTextBrush));
            }
        }
        [JsonIgnore] private Color _mTitleTextColor = s_colorWhite;

        /// <summary>
        /// Sets how many line the title can show max
        /// </summary>
        [JsonIgnore]
        public int TitleMaxLines
        {
            get => _mTitleMaxLines;
            set => SetProperty(ref _mTitleMaxLines, value);
        }
        [JsonIgnore] private int _mTitleMaxLines = 2;

        /// <summary>
        /// Sets text for comment sort
        /// </summary>
        [JsonIgnore]
        public string CommentCurrentSortTypeString
        {
            get
            {
                switch(CommentSortType)
                {
                    default:
                    case CommentSortTypes.Best:
                        return "Best";
                    case CommentSortTypes.Controversial:
                        return "Controversial";
                    case CommentSortTypes.New:
                        return "New";
                    case CommentSortTypes.Old:
                        return "Old";
                    case CommentSortTypes.Qa:
                        return "Q&A";
                    case CommentSortTypes.Top:
                        return "Top";
                }
            }
        }

        /// <summary>
        /// Sets text for a context menu item
        /// </summary>
        [JsonIgnore]
        public string IsSavedMenuText => IsSaved ? "Unsave post" : "Save post";

        /// <summary>
        /// Sets text for a context menu item
        /// </summary>
        [JsonIgnore]
        public string IsHiddenMenuText => IsHidden ? "Unhide post" : "Hide post";

        /// <summary>
        /// Used by subreddit view to mark the title read
        /// </summary>
        [JsonIgnore]
        public SolidColorBrush TitleTextBrush => new SolidColorBrush(TitleTextColor);

        /// <summary>
        /// The color this post's upvote button should be in the UI.
        /// It is accented if and only if the user has upvoted this comment.
        /// </summary>
        [JsonIgnore]
        public SolidColorBrush UpVoteColor
        {
            get
            {
                if(Likes.HasValue && Likes.Value)
                {
                    return GetAccentBrush();
                }

                return new SolidColorBrush(s_colorGray);
            }
        }

        /// <summary>
        /// The color this post's downvote button should be in the UI.
        /// It is accented if and only if the user has upvoted this comment.
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

                return new SolidColorBrush(s_colorGray);
            }
        }

        /// <summary>
        /// The color the UI should use to indicate there are unread comments on this post.
        /// </summary>
        [JsonIgnore]
        public SolidColorBrush NewCommentColor
        {
            get
            {
                return string.IsNullOrWhiteSpace(NewCommentText) ? new SolidColorBrush(s_colorGray) : GetLightenedAccentBrush();
            }
        }

        /// <summary>
        /// A darker accented color.
        /// </summary>
        [JsonIgnore]
        public SolidColorBrush DarkenedAccentColorBrush => GetDarkenedAccentBrush();

        /// <summary>
        /// The spacing to leave before the new-comments indicator.
        /// This is empty if there are no new comments.
        /// </summary>
        [JsonIgnore]
        public Thickness NewCommentMargin
        {
            get
            {
                return string.IsNullOrWhiteSpace(NewCommentText) ? new Thickness(0) : new Thickness(0, 0, 3, 0);
            }
        }

        /// <summary>
        /// Used by the subreddit list to show or hide sticky
        /// </summary>
        [JsonIgnore]
        public Visibility StickyVisibility => IsStickied ? Visibility.Visible: Visibility.Collapsed;

        /// <summary>
        /// Used by the subreddit list to show or hide link flair
        /// </summary>
        [JsonIgnore]
        public Visibility FlairVisibility => string.IsNullOrWhiteSpace(LinkFlairText) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// Used by the subreddit list to show or hide gilded tag
        /// </summary>
        [JsonIgnore]
        public Visibility GildedVisibility => Gilded ? Visibility.Visible : Visibility.Collapsed;

        [JsonIgnore] public Visibility HasIcons => Gilded || IsStickied ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Used by the subreddit list to show or hide NSFW tag
        /// </summary>
        [JsonIgnore]
        public Visibility NsfwVisibility => IsOver18 ? Visibility.Visible : Visibility.Collapsed;

        #region FlipView Vars


        /// <summary>
        /// The visibility of "Loading Comments", depending on if the comments have loaded yet.
        /// </summary>
        [JsonIgnore]
        public Visibility ShowCommentLoadingMessage
        {
            get => _showCommentLoadingMessage;
            set => SetProperty(ref _showCommentLoadingMessage, value);
        }
        [JsonIgnore] private Visibility _showCommentLoadingMessage = Visibility.Collapsed;

        /// <summary>
        /// A string that is used to show errors in the footer of the comment list. If "" it is hidden.
        /// </summary>
        [JsonIgnore]
        public string ShowCommentsErrorMessage
        {
            get => _showCommentsErrorMessage;
            set => SetProperty(ref _showCommentsErrorMessage, value);
        }
        [JsonIgnore] private string _showCommentsErrorMessage = "";

        /// <summary>
        /// The visibility of the menu button when the post is displayed in flip view.
        /// </summary>
        [JsonIgnore]
        public Visibility FlipViewMenuButton
        {
            get => _flipViewMenuButton;
            set => SetProperty(ref _flipViewMenuButton, value);
        }
        [JsonIgnore] private Visibility _flipViewMenuButton = Visibility.Collapsed;

        /// <summary>
        /// The visibility of the button to show all comments on a post.
        /// This should be visible when only some comments are visible.
        /// </summary>
        [JsonIgnore]
        public Visibility FlipViewShowEntireThreadMessage
        {
            get => _flipViewShowEntireThreadMessage;
            set => SetProperty(ref _flipViewShowEntireThreadMessage, value);
        }
        [JsonIgnore] private Visibility _flipViewShowEntireThreadMessage = Visibility.Collapsed;

        /// <summary>
        ///  Used to indicate if the save image option should be visible
        /// </summary>
        [JsonIgnore]
        public Visibility ShowSaveImageMenu { get; set; }

        /// <summary>
        /// Used by flip view to show the comment or post reply box
        /// </summary>
        [JsonIgnore]
        public string CommentingOnId
        {
            get => _commentingOnId;
            set => SetProperty(ref _commentingOnId, value);
        }
        [JsonIgnore] private string _commentingOnId = "";


        /// <summary>
        /// Indicates how many comments we are showing
        /// </summary>
        [JsonIgnore]
        public int CurrentCommentShowingCount
        {
            get => _currentCommentCount;
            set => SetProperty(ref _currentCommentCount, value);
        }
        [JsonIgnore] private int _currentCommentCount = 150;

        /// <summary>
        /// Shows or hides the loading more progress bar for comments.
        /// </summary>
        [JsonIgnore]
        public bool FlipViewShowLoadingMoreComments
        {
            get => _flipViewShowLoadingMoreComments;
            set
            {
                if (SetProperty(ref _flipViewShowLoadingMoreComments, value))
                {
                    OnPropertyChanged(nameof(FlipViewShowLoadingMoreCommentsVis));
                }
            }
        }
        [JsonIgnore] private bool _flipViewShowLoadingMoreComments;

        /// <summary>
        /// Indicates if the post is owned by the current user.
        /// </summary>
        [JsonIgnore]
        public bool IsPostOwnedByUser
        {
            get => _isPostOwnedByUser;
            set
            {
                if (!SetProperty(ref _isPostOwnedByUser, value)) return;
                OnPropertyChanged(nameof(DeletePostVisibility));
                OnPropertyChanged(nameof(EditPostVisibility));
            }
        }
        [JsonIgnore] private bool _isPostOwnedByUser;

        /// <summary>
        /// Used by the flip view to indicate if this post can be deleted by this user.
        /// </summary>
        [JsonIgnore]
        public Visibility DeletePostVisibility => IsPostOwnedByUser ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Used by the flip view to indicate if this post can be edited by this user.
        /// </summary>
        [JsonIgnore]
        public Visibility EditPostVisibility => IsPostOwnedByUser && IsSelf ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Shows or hides the loading more progress bar for comments.
        /// </summary>
        [JsonIgnore]
        public Visibility FlipViewShowLoadingMoreCommentsVis => _flipViewShowLoadingMoreComments ? Visibility.Visible : Visibility.Collapsed;

        #endregion
    }

    public class MediaImage
    {
        public string Id { get; set; }
        public string Url { get; set; }
        public Uri Uri { get; set; }
        public int Index { get; set; }
    }

    public class GalleryData
    {
        [JsonProperty(PropertyName = "items")]
        public IEnumerable<GalleryMedia> Items { get; set; }
    }

    public class GalleryMedia
    {
        [JsonProperty(PropertyName = "media_id")]
        public string MediaId { get; set; }
        [JsonProperty(PropertyName = "id")]
        public long Id { get; set; }
    }

    public class MediaMetaData
    {

    }

    public class SecureMedia
    {
        [JsonProperty(PropertyName = "reddit_video")]
        public RedditVideo RedditVideo { get; set; }
    }

    public class RedditVideo
    {
        [JsonProperty(PropertyName = "hls_url")]
        public string Url { get; set; }

        [JsonProperty(PropertyName = "height")]
        public int Height { get; set; }

        [JsonProperty(PropertyName = "width")]
        public int Width { get; set; }

        [JsonProperty(PropertyName = "duration")]
        public double Duration { get; set; }

        [JsonProperty(PropertyName = "is_gif")]
        public bool IsGif { get; set; }
    }
}
