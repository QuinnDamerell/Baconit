using BaconBackend.Collectors;
using BaconBackend.DataObjects;

namespace BaconBackend.Managers
{
    public enum NsfwBlockType
    {
        Always = 0,
        PerSubreddit,
        Never,
    }

    public class UiSettingManager : BindableBase
    {
        private readonly BaconManager _baconMan;

        public UiSettingManager(BaconManager baconMan)
        {
            _baconMan = baconMan;

            // If we aren't a background +1 app opened.
            if(!baconMan.IsBackgroundTask)
            {
                AppOpenedCount++;
            }

            baconMan.OnResuming += BaconMan_OnResuming;
        }

        private void BaconMan_OnResuming(object sender, object e)
        {
            // When we are resumed +1 the count;
            AppOpenedCount++;
        }

        /// <summary>
        /// Counts the number of times the app has been opened (or resumed)
        /// </summary>
        public int AppOpenedCount
        {
            get
            {
                if (_appOpenedCount.HasValue) return _appOpenedCount.Value;
                _appOpenedCount = _baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.AppOpenedCount") 
                    ? _baconMan.SettingsMan.ReadFromRoamingSettings<int>("UiSettingManager.AppOpenedCount") 
                    : 0;
                return _appOpenedCount.Value;
            }
            private set
            {
                _appOpenedCount = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("UiSettingManager.AppOpenedCount", _appOpenedCount.Value);
            }
        }
        private int? _appOpenedCount;

        #region Settings

        /// <summary>
        /// If the user is in debug mode or not.
        /// </summary>
        public bool DeveloperDebug
        {
            get
            {
                if (_developerDebug.HasValue) return _developerDebug.Value;
                _developerDebug = _baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.Developer_Debug") && _baconMan.SettingsMan.ReadFromRoamingSettings<bool>("UiSettingManager.Developer_Debug");
                return _developerDebug.Value;
            }
            set
            {
                _developerDebug = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("UiSettingManager.Developer_Debug", _developerDebug.Value);
            }
        }
        private bool? _developerDebug;

        /// <summary>
        /// If the app will prevent crashing and report any fatal errors.
        /// </summary>
        public bool DeveloperStopFatalCrashesAndReport
        {
            get
            {
                if (_developerStopFatalCrashesAndReport.HasValue) return _developerStopFatalCrashesAndReport.Value;
                _developerStopFatalCrashesAndReport = _baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.Developer_StopFatalCrashesAndReport") && _baconMan.SettingsMan.ReadFromRoamingSettings<bool>("UiSettingManager.Developer_StopFatalCrashesAndReport");
                return _developerStopFatalCrashesAndReport.Value;
            }
            set
            {
                _developerStopFatalCrashesAndReport = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("UiSettingManager.Developer_StopFatalCrashesAndReport", _developerStopFatalCrashesAndReport.Value);
            }
        }
        private bool? _developerStopFatalCrashesAndReport;

        /// <summary>
        /// Shows a memory overlay for the app.
        /// </summary>
        public bool DeveloperShowMemoryOverlay
        {
            get
            {
                if (_developerShowMemoryOverlay.HasValue) return _developerShowMemoryOverlay.Value;
                _developerShowMemoryOverlay = _baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.Developer_ShowMemoryOverlay") && _baconMan.SettingsMan.ReadFromRoamingSettings<bool>("UiSettingManager.Developer_ShowMemoryOverlay");
                return _developerShowMemoryOverlay.Value;
            }
            set
            {
                _developerShowMemoryOverlay = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("UiSettingManager.Developer_ShowMemoryOverlay", _developerShowMemoryOverlay.Value);
            }
        }
        private bool? _developerShowMemoryOverlay;

        public bool DisableAnalyticCollection
        {
            get
            {
                var hasValue =
                    _baconMan.SettingsMan.RoamingSettings.ContainsKey(
                        "UiSettingManager.Developer_DisableAnalyticCollection");

                if (hasValue)
                {
                    _disableAnalyticCollection =
                        _baconMan.SettingsMan.ReadFromRoamingSettings<bool>(
                            "UiSettingManager.Developer_DisableAnalyticCollection");
                }
                return _disableAnalyticCollection;
            }
            set
            {
                if (_disableAnalyticCollection == value) return;
                _disableAnalyticCollection = value;
                _baconMan.SettingsMan.WriteToRoamingSettings(
                    "UiSettingManager.Developer_DisableAnalyticCollection", 
                    _disableAnalyticCollection);
                if (value)
                {
                    _baconMan.TelemetryMan.ReportEvent(this, "AnalyticCollectionEnabled", "true");
                }
            }
        }
        private bool _disableAnalyticCollection = true;

        #endregion

        #region MainPage

        /// <summary>
        /// The next time we should annoy the user to leave a review
        /// </summary>
        public int MainPageNextReviewAnnoy
        {
            get
            {
                if (_mainPageNextReviewAnnoy.HasValue) return _mainPageNextReviewAnnoy.Value;
                _mainPageNextReviewAnnoy = _baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.MainPage_NextReviewAnnoy") ? _baconMan.SettingsMan.ReadFromRoamingSettings<int>("UiSettingManager.MainPage_NextReviewAnnoy") : 5;
                return _mainPageNextReviewAnnoy.Value;
            }
            set
            {
                _mainPageNextReviewAnnoy = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("UiSettingManager.MainPage_NextReviewAnnoy", _mainPageNextReviewAnnoy.Value);
            }
        }
        private int? _mainPageNextReviewAnnoy;

        #endregion

        #region Subreddit View

        /// <summary>
        /// If the user wants to show full titles or not.
        /// </summary>
        public bool SubredditListShowFullTitles
        {
            get
            {
                if (_subredditListShowFullTitles.HasValue) return _subredditListShowFullTitles.Value;
                _subredditListShowFullTitles = _baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.SubredditList_ShowFullTitles") && _baconMan.SettingsMan.ReadFromRoamingSettings<bool>("UiSettingManager.SubredditList_ShowFullTitles");
                return _subredditListShowFullTitles.Value;
            }
            set
            {
                _subredditListShowFullTitles = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("UiSettingManager.SubredditList_ShowFullTitles", _subredditListShowFullTitles.Value);
            }
        }
        private bool? _subredditListShowFullTitles;

        /// <summary>
        /// The default subreddit sort type
        /// </summary>
        public SortTypes SubredditListDefaultSortType
        {
            get
            {
                if (_subredditListDefaultSortType.HasValue) return _subredditListDefaultSortType.Value;
                _subredditListDefaultSortType = _baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.SubredditList_DefaultSortType") ? _baconMan.SettingsMan.ReadFromRoamingSettings<SortTypes>("UiSettingManager.SubredditList_DefaultSortType") : SortTypes.Hot;
                return _subredditListDefaultSortType.Value;
            }
            set
            {
                _subredditListDefaultSortType = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("UiSettingManager.SubredditList_DefaultSortType", _subredditListDefaultSortType.Value);
            }
        }
        private SortTypes? _subredditListDefaultSortType;

        /// <summary>
        /// The default subreddit sort time type
        /// </summary>
        public SortTimeTypes SubredditListDefaultSortTimeType
        {
            get
            {
                if (_subredditListDefaultSortTimeType.HasValue) return _subredditListDefaultSortTimeType.Value;
                _subredditListDefaultSortTimeType = _baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.SubredditList_DefaultSortTimeType") ? _baconMan.SettingsMan.ReadFromRoamingSettings<SortTimeTypes>("UiSettingManager.SubredditList_DefaultSortTimeType") : SortTimeTypes.Week;
                return _subredditListDefaultSortTimeType.Value;
            }
            set
            {
                _subredditListDefaultSortTimeType = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("UiSettingManager.SubredditList_DefaultSortTimeType", _subredditListDefaultSortTimeType.Value);
            }
        }
        private SortTimeTypes? _subredditListDefaultSortTimeType;

        /// <summary>
        /// The default Subreddit to show when the app opens
        /// </summary>
        public string SubredditListDefaultSubredditDisplayName
        {
            get
            {
                if (_subredditListDefaultSubreddit != null) return _subredditListDefaultSubreddit;
                _subredditListDefaultSubreddit = _baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.SubredditList_DefaultSubredditDisplayName") ? _baconMan.SettingsMan.ReadFromRoamingSettings<string>("UiSettingManager.SubredditList_DefaultSubredditDisplayName") : "frontpage";
                return _subredditListDefaultSubreddit;
            }
            set
            {
                // Validate
                if(string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                _subredditListDefaultSubreddit = value.ToLower();
                _baconMan.SettingsMan.WriteToRoamingSettings("UiSettingManager.SubredditList_DefaultSubredditDisplayName", _subredditListDefaultSubreddit);
            }
        }
        private string _subredditListDefaultSubreddit;

        #endregion

        #region Comments

        /// <summary>
        /// The default comment sort type
        /// </summary>
        public CommentSortTypes CommentsDefaultSortType
        {
            get
            {
                if (_commentsDefaultSortType.HasValue) return _commentsDefaultSortType.Value;
                _commentsDefaultSortType = _baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.Comments_DefaultSortType") ? _baconMan.SettingsMan.ReadFromRoamingSettings<CommentSortTypes>("UiSettingManager.Comments_DefaultSortType") : CommentSortTypes.Best;
                return _commentsDefaultSortType.Value;
            }
            set
            {
                _commentsDefaultSortType = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("UiSettingManager.Comments_DefaultSortType", _commentsDefaultSortType.Value);
            }
        }
        private CommentSortTypes? _commentsDefaultSortType;


        /// <summary>
        /// The default comment count number
        /// </summary>
        public int CommentsDefaultCount
        {
            get
            {
                if (_commentsDefaultCount.HasValue) return _commentsDefaultCount.Value;
                _commentsDefaultCount = _baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.Comments_DefaultCount") ? _baconMan.SettingsMan.ReadFromRoamingSettings<int>("UiSettingManager.Comments_DefaultCount") : 150;
                return _commentsDefaultCount.Value;
            }
            set
            {
                _commentsDefaultCount = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("UiSettingManager.Comments_DefaultCount", _commentsDefaultCount.Value);
            }
        }
        private int? _commentsDefaultCount;

        #endregion

        #region Flip View

        /// <summary>
        /// Indicates what we should do with NSFW blocks
        /// </summary>
        public NsfwBlockType FlipViewNsfwBlockingType
        {
            get
            {
                if (_flipViewNsfwBlockingType.HasValue) return _flipViewNsfwBlockingType.Value;
                _flipViewNsfwBlockingType = _baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.FlipView_NsfwBlockingType") ? _baconMan.SettingsMan.ReadFromRoamingSettings<NsfwBlockType>("UiSettingManager.FlipView_NsfwBlockingType") : NsfwBlockType.Always;
                return _flipViewNsfwBlockingType.Value;
            }
            set
            {
                _flipViewNsfwBlockingType = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("UiSettingManager.FlipView_NsfwBlockingType", _flipViewNsfwBlockingType.Value);
            }
        }
        private NsfwBlockType? _flipViewNsfwBlockingType;

        /// <summary>
        /// If the user wants to pre load comments or not.
        /// </summary>
        public bool FlipViewPreloadComments
        {
            get
            {
                if (_flipViewPreloadComments.HasValue) return _flipViewPreloadComments.Value;
                _flipViewPreloadComments = !_baconMan.SettingsMan.LocalSettings.ContainsKey("UiSettingManager.FlipView_PreloadComments") || _baconMan.SettingsMan.ReadFromLocalSettings<bool>("UiSettingManager.FlipView_PreloadComments");
                return _flipViewPreloadComments.Value;
            }
            set
            {
                _flipViewPreloadComments = value;
                _baconMan.SettingsMan.WriteToLocalSettings("UiSettingManager.FlipView_PreloadComments", _flipViewPreloadComments.Value);
            }
        }
        private bool? _flipViewPreloadComments;

        /// <summary>
        /// If the user wants us to load post content before they tap the screen.
        /// </summary>
        public bool FlipViewLoadPostContentWithoutAction
        {
            get
            {
                if (_flipViewLoadPostContentWithoutAction.HasValue)
                    return _flipViewLoadPostContentWithoutAction.Value;
                _flipViewLoadPostContentWithoutAction = !_baconMan.SettingsMan.LocalSettings.ContainsKey("UiSettingManager.FlipView_LoadPostContentWithoutAction") || _baconMan.SettingsMan.ReadFromLocalSettings<bool>("UiSettingManager.FlipView_LoadPostContentWithoutAction");
                return _flipViewLoadPostContentWithoutAction.Value;
            }
            set
            {
                _flipViewLoadPostContentWithoutAction = value;
                _baconMan.SettingsMan.WriteToLocalSettings("UiSettingManager.FlipView_LoadPostContentWithoutAction", _flipViewLoadPostContentWithoutAction.Value);
            }
        }
        private bool? _flipViewLoadPostContentWithoutAction;

        /// <summary>
        /// If the user wants us to pre-load future flip view content.
        /// </summary>
        public bool FlipViewPreloadFutureContent
        {
            get
            {
                if (_flipViewPreloadFutureContent.HasValue) return _flipViewPreloadFutureContent.Value;
                _flipViewPreloadFutureContent = !_baconMan.SettingsMan.LocalSettings.ContainsKey("UiSettingManager.FlipView_PreloadFutureContent") || _baconMan.SettingsMan.ReadFromLocalSettings<bool>("UiSettingManager.FlipView_PreloadFutureContent");
                return _flipViewPreloadFutureContent.Value;
            }
            set
            {
                _flipViewPreloadFutureContent = value;
                _baconMan.SettingsMan.WriteToLocalSettings("UiSettingManager.FlipView_PreloadFutureContent", _flipViewPreloadFutureContent.Value);
            }
        }
        private bool? _flipViewPreloadFutureContent;

        /// <summary>
        /// If we should show the user the comment tip or not.
        /// </summary>
        public bool FlipViewShowCommentScrollTip
        {
            get
            {
                if (_flipViewShowCommentScrollTip.HasValue) return _flipViewShowCommentScrollTip.Value;
                _flipViewShowCommentScrollTip = !_baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.FlipView_ShowCommentScrollTip") || _baconMan.SettingsMan.ReadFromRoamingSettings<bool>("UiSettingManager.FlipView_ShowCommentScrollTip");
                return _flipViewShowCommentScrollTip.Value;
            }
            set
            {
                _flipViewShowCommentScrollTip = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("UiSettingManager.FlipView_ShowCommentScrollTip", _flipViewShowCommentScrollTip.Value);
            }
        }
        private bool? _flipViewShowCommentScrollTip;

        /// <summary>
        /// If the user wants us to minimize the story header.
        /// </summary>
        public bool FlipViewMinimizeStoryHeader
        {
            get
            {
                if (_flipViewMinimizeStoryHeader.HasValue) return _flipViewMinimizeStoryHeader.Value;
                _flipViewMinimizeStoryHeader = !_baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.FlipView_MinimizeStoryHeader") || _baconMan.SettingsMan.ReadFromRoamingSettings<bool>("UiSettingManager.FlipView_MinimizeStoryHeader");
                return _flipViewMinimizeStoryHeader.Value;
            }
            set
            {
                _flipViewMinimizeStoryHeader = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("UiSettingManager.FlipView_MinimizeStoryHeader", _flipViewMinimizeStoryHeader.Value);
            }
        }
        private bool? _flipViewMinimizeStoryHeader;

        #endregion

        #region Developer

        /// <summary>
        /// Used to keep track of how many pages have been removed.
        /// </summary>
        public int PagesMemoryCleanedUp = 0;

        #endregion

        #region PostView

        /// <summary>
        /// The current zoom level for MarkDown View
        /// </summary>
        public int PostViewMarkdownFontSize
        {
            get
            {
                if (_postViewMarkdownFontSize.HasValue) return _postViewMarkdownFontSize.Value;
                if (_baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.PostView_Markdown_FontSize"))
                {
                    SetProperty(
                        ref _postViewMarkdownFontSize,
                        _baconMan.SettingsMan.ReadFromRoamingSettings<int>("UiSettingManager.PostView_Markdown_FontSize"),
                        "PostView_Markdown_FontSize"
                    );
                }
                else
                {
                    SetProperty(
                        ref _postViewMarkdownFontSize,
                        14,
                        "PostView_Markdown_FontSize"
                    );
                }
                return _postViewMarkdownFontSize.Value;
            }
            set
            {
                SetProperty(
                    ref _postViewMarkdownFontSize,
                    value,
                    "PostView_Markdown_FontSize"
                );
                _baconMan.SettingsMan.WriteToRoamingSettings("UiSettingManager.PostView_Markdown_FontSize", _postViewMarkdownFontSize.Value);
            }
        }
        private int? _postViewMarkdownFontSize;

        #endregion
    }
}
