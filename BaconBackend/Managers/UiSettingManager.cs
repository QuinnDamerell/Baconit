using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.Managers
{
    public enum NsfwBlockType
    {
        Always = 0,        
        PerSubreddit,
        Never,
    }

    public class UiSettingManager
    {
        BaconManager m_baconMan;

        public UiSettingManager(BaconManager baconMan)
        {
            m_baconMan = baconMan;

            // If we aren't a background +1 app opened.
            if(!baconMan.IsBackgroundTask)
            {
                AppOpenedCount++;
            }

            baconMan.OnResuming += BaconMan_OnResuming;
        }

        private void BaconMan_OnResuming(object sender, object e)
        {
            // When we are resuemd +1 the count;
            AppOpenedCount++;
        }

        /// <summary>
        /// Counts the number of times the app has been opened (or resumed)
        /// </summary>
        public int AppOpenedCount
        {
            get
            {
                if (!m_appOpenedCount.HasValue)
                {
                    if (m_baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.AppOpenedCount"))
                    {
                        m_appOpenedCount = m_baconMan.SettingsMan.ReadFromRoamingSettings<int>("UiSettingManager.AppOpenedCount");
                    }
                    else
                    {
                        m_appOpenedCount = 0;
                    }
                }
                return m_appOpenedCount.Value;
            }
            set
            {
                m_appOpenedCount = value;
                m_baconMan.SettingsMan.WriteToRoamingSettings<int>("UiSettingManager.AppOpenedCount", m_appOpenedCount.Value);
            }
        }
        private int? m_appOpenedCount = null;

        #region Settings

        /// <summary>
        /// If the user is in debug mode or not.
        /// </summary>
        public bool Developer_Debug
        {
            get
            {
                if (!m_developer_Debug.HasValue)
                {
                    if (m_baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.Developer_Debug"))
                    {
                        m_developer_Debug = m_baconMan.SettingsMan.ReadFromRoamingSettings<bool>("UiSettingManager.Developer_Debug");
                    }
                    else
                    {
                        m_developer_Debug = false;
                    }
                }
                return m_developer_Debug.Value;
            }
            set
            {
                m_developer_Debug = value;
                m_baconMan.SettingsMan.WriteToRoamingSettings<bool>("UiSettingManager.Developer_Debug", m_developer_Debug.Value);
            }
        }
        private bool? m_developer_Debug = null;

        /// <summary>
        /// If the app will prevent crashing and report any fatal errors.
        /// </summary>
        public bool Developer_StopFatalCrashesAndReport
        {
            get
            {
                if (!m_developer_StopFatalCrashesAndReport.HasValue)
                {
                    if (m_baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.Developer_StopFatalCrashesAndReport"))
                    {
                        m_developer_StopFatalCrashesAndReport = m_baconMan.SettingsMan.ReadFromRoamingSettings<bool>("UiSettingManager.Developer_StopFatalCrashesAndReport");
                    }
                    else
                    {
                        m_developer_StopFatalCrashesAndReport = false;
                    }
                }
                return m_developer_StopFatalCrashesAndReport.Value;
            }
            set
            {
                m_developer_StopFatalCrashesAndReport = value;
                m_baconMan.SettingsMan.WriteToRoamingSettings<bool>("UiSettingManager.Developer_StopFatalCrashesAndReport", m_developer_StopFatalCrashesAndReport.Value);
            }
        }
        private bool? m_developer_StopFatalCrashesAndReport = null;

        #endregion

        #region MainPage

        /// <summary>
        /// The next time we should annoy the user to leave a review
        /// </summary>
        public int MainPage_NextReviewAnnoy
        {
            get
            {
                if (!m_mainPage_NextReviewAnnoy.HasValue)
                {
                    if (m_baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.MainPage_NextReviewAnnoy"))
                    {
                        m_mainPage_NextReviewAnnoy = m_baconMan.SettingsMan.ReadFromRoamingSettings<int>("UiSettingManager.MainPage_NextReviewAnnoy");
                    }
                    else
                    {
                        m_mainPage_NextReviewAnnoy = 5;
                    }
                }
                return m_mainPage_NextReviewAnnoy.Value;
            }
            set
            {
                m_mainPage_NextReviewAnnoy = value;
                m_baconMan.SettingsMan.WriteToRoamingSettings<int>("UiSettingManager.MainPage_NextReviewAnnoy", m_mainPage_NextReviewAnnoy.Value);
            }
        }
        private int? m_mainPage_NextReviewAnnoy = null;

        #endregion

        #region Subreddit View

        /// <summary>
        /// If the user wants to show full titles or not.
        /// </summary>
        public bool SubredditList_ShowFullTitles
        {
            get
            {
                if (!m_subredditList_ShowFullTitles.HasValue)
                {
                    if (m_baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.SubredditList_ShowFullTitles"))
                    {
                        m_subredditList_ShowFullTitles = m_baconMan.SettingsMan.ReadFromRoamingSettings<bool>("UiSettingManager.SubredditList_ShowFullTitles");
                    }
                    else
                    {
                        m_subredditList_ShowFullTitles = false;
                    }
                }
                return m_subredditList_ShowFullTitles.Value;
            }
            set
            {
                m_subredditList_ShowFullTitles = value;
                m_baconMan.SettingsMan.WriteToRoamingSettings<bool>("UiSettingManager.SubredditList_ShowFullTitles", m_subredditList_ShowFullTitles.Value);
            }
        }
        private bool? m_subredditList_ShowFullTitles = null;

        /// <summary>
        /// The default Subreddit to show when the app opens
        /// </summary>
        public string SubredditList_DefaultSubredditDisplayName
        {
            get
            {
                if (m_subredditList_DefaultSubreddit == null)
                {
                    if (m_baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.SubredditList_DefaultSubredditDisplayName"))
                    {
                        m_subredditList_DefaultSubreddit = m_baconMan.SettingsMan.ReadFromRoamingSettings<string>("UiSettingManager.SubredditList_DefaultSubredditDisplayName");
                    }
                    else
                    {
                        m_subredditList_DefaultSubreddit = "frontpage";
                    }
                }
                return m_subredditList_DefaultSubreddit;
            }
            set
            {
                // Validate
                if(String.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                m_subredditList_DefaultSubreddit = value.ToLower();
                m_baconMan.SettingsMan.WriteToRoamingSettings<string>("UiSettingManager.SubredditList_DefaultSubredditDisplayName", m_subredditList_DefaultSubreddit);
            }
        }
        private string m_subredditList_DefaultSubreddit = null;

        #endregion

        #region Flip View

        /// <summary>
        /// Indicates what we should do with NSFW blocks
        /// </summary>
        public NsfwBlockType FlipView_NsfwBlockingType
        {
            get
            {
                if (!m_flipView_NsfwBlockingType.HasValue)
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("UiSettingManager.FlipView_NsfwBlockingType"))
                    {
                        m_flipView_NsfwBlockingType = m_baconMan.SettingsMan.ReadFromLocalSettings<NsfwBlockType>("UiSettingManager.FlipView_NsfwBlockingType");
                    }
                    else
                    {
                        m_flipView_NsfwBlockingType = NsfwBlockType.Always;
                    }
                }
                return m_flipView_NsfwBlockingType.Value;
            }
            set
            {
                m_flipView_NsfwBlockingType = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<NsfwBlockType>("UiSettingManager.FlipView_NsfwBlockingType", m_flipView_NsfwBlockingType.Value);
            }
        }
        private NsfwBlockType? m_flipView_NsfwBlockingType = null;

        /// <summary>
        /// If the user wants to pre load comments or not.
        /// </summary>
        public bool FlipView_PreloadComments
        {
            get
            {
                if (!m_flipView_PreloadComments.HasValue)
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("UiSettingManager.FlipView_PreloadComments"))
                    {
                        m_flipView_PreloadComments = m_baconMan.SettingsMan.ReadFromLocalSettings<bool>("UiSettingManager.FlipView_PreloadComments");
                    }
                    else
                    {
                        m_flipView_PreloadComments = true;
                    }
                }
                return m_flipView_PreloadComments.Value;
            }
            set
            {
                m_flipView_PreloadComments = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<bool>("UiSettingManager.FlipView_PreloadComments", m_flipView_PreloadComments.Value);
            }
        }
        private bool? m_flipView_PreloadComments = null;

        /// <summary>
        /// If the user wants us to load post content before they tap the screen.
        /// </summary>
        public bool FlipView_LoadPostContentWithoutAction
        {
            get
            {
                if (!m_flipView_LoadPostContentWithoutAction.HasValue)
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("UiSettingManager.FlipView_LoadPostContentWithoutAction"))
                    {
                        m_flipView_LoadPostContentWithoutAction = m_baconMan.SettingsMan.ReadFromLocalSettings<bool>("UiSettingManager.FlipView_LoadPostContentWithoutAction");
                    }
                    else
                    {
                        m_flipView_LoadPostContentWithoutAction = true;
                    }
                }
                return m_flipView_LoadPostContentWithoutAction.Value;
            }
            set
            {
                m_flipView_LoadPostContentWithoutAction = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<bool>("UiSettingManager.FlipView_LoadPostContentWithoutAction", m_flipView_LoadPostContentWithoutAction.Value);
            }
        }
        private bool? m_flipView_LoadPostContentWithoutAction = null;

        /// <summary>
        /// If the user wants us to prelaod future flip view content.
        /// </summary>
        public bool FlipView_PreloadFutureContent
        {
            get
            {
                if (!m_flipView_PreloadFutureContent.HasValue)
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("UiSettingManager.FlipView_PreloadFutureContent"))
                    {
                        m_flipView_PreloadFutureContent = m_baconMan.SettingsMan.ReadFromLocalSettings<bool>("UiSettingManager.FlipView_PreloadFutureContent");
                    }
                    else
                    {
                        m_flipView_PreloadFutureContent = true;
                    }
                }
                return m_flipView_PreloadFutureContent.Value;
            }
            set
            {
                m_flipView_PreloadFutureContent = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<bool>("UiSettingManager.FlipView_PreloadFutureContent", m_flipView_PreloadFutureContent.Value);
            }
        }
        private bool? m_flipView_PreloadFutureContent = null;

        /// <summary>
        /// If we should show the user the comment tip or not.
        /// </summary>
        public bool FlipView_ShowCommentScrollTip
        {
            get
            {
                if (!m_flipView_ShowCommentScrollTip.HasValue)
                {
                    if (m_baconMan.SettingsMan.RoamingSettings.ContainsKey("UiSettingManager.FlipView_ShowCommentScrollTip"))
                    {
                        m_flipView_ShowCommentScrollTip = m_baconMan.SettingsMan.ReadFromRoamingSettings<bool>("UiSettingManager.FlipView_ShowCommentScrollTip");
                    }
                    else
                    {
                        m_flipView_ShowCommentScrollTip = true;
                    }
                }
                return m_flipView_ShowCommentScrollTip.Value;
            }
            set
            {
                m_flipView_ShowCommentScrollTip = value;
                m_baconMan.SettingsMan.WriteToRoamingSettings<bool>("UiSettingManager.FlipView_ShowCommentScrollTip", m_flipView_ShowCommentScrollTip.Value);
            }
        }
        private bool? m_flipView_ShowCommentScrollTip = null;

        #endregion
    }
}
