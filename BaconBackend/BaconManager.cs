using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaconBackend.Managers;
using Windows.UI.Xaml;
using Windows.ApplicationModel;
using BaconBackend.Interfaces;
using BaconBackend.Helpers;

namespace BaconBackend
{
    public class BaconManager
    {
        /// <summary>
        /// Indicates if this is a background task or not.
        /// </summary>
        public bool IsBackgroundTask
        {
            get; private set;
        }

        /// <summary>
        /// Fired when the app is suspending
        /// </summary>
        public event SuspendingEventHandler OnSuspending;

        /// <summary>
        /// Fired when the app is resuming
        /// </summary>
        public event EventHandler<object> OnResuming;

        /// <summary>
        /// Responsible for managing the current subreddits.
        /// </summary>
        public SubredditManager SubredditMan { get; }

        /// <summary>
        /// Responsible for managing settings
        /// </summary>
        public SettingsManager SettingsMan { get; }

        /// <summary>
        /// Responsible for managing network calls
        /// </summary>
        public NetworkManager NetworkMan { get; }

        /// <summary>
        /// Responsible for dealing with user messages
        /// </summary>
        public MessageManager MessageMan { get; }

        /// <summary>
        /// Responsible for dealing with the user information
        /// </summary>
        public UserManager UserMan { get; }

        /// <summary>
        /// Responsible for dealing with images
        /// </summary>
        public ImageManager ImageMan { get; }

        /// <summary>
        /// Responsible for dealing with file caching
        /// </summary>
        public CacheManager CacheMan { get; }

        /// <summary>
        /// Holds settings for the UI classes
        /// </summary>
        public UiSettingManager UiSettingsMan { get; }

        /// <summary>
        /// Manages all app telemetry
        /// </summary>
        public TelemetryManager TelemetryMan { get; }

        /// <summary>
        /// Manages all background tasks
        /// </summary>
        public BackgroundManager BackgroundMan { get; }

        /// <summary>
        /// Manages the message of the day prompt
        /// </summary>
        public MessageOfTheDayManager MotdMan { get; }

        /// <summary>
        /// Holds a connection to the front end, a way for things back here to
        /// interact with the front end.
        /// </summary>
        private IBackendActionListener m_backendActionListener;


        public BaconManager(bool isBackgroundTask)
        {
            // Set background task flag
            IsBackgroundTask = isBackgroundTask;

            // Init managers
            UserMan = new UserManager(this);
            CacheMan = new CacheManager(this);
            ImageMan = new ImageManager(this);
            SubredditMan = new SubredditManager(this);
            SettingsMan = new SettingsManager(this);
            NetworkMan = new NetworkManager(this);
            MessageMan = new MessageManager(this);
            UiSettingsMan = new UiSettingManager(this);
            TelemetryMan = new TelemetryManager();
            BackgroundMan = new BackgroundManager(this);
            MotdMan = new MessageOfTheDayManager(this);

            // Don't do this if we are a background task; it will
            // call this when it is ready.
            if (!isBackgroundTask)
            {
                FireOffUpdate();
            }
        }

        /// <summary>
        /// Called by the app when the app is suspending
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnSuspending_Fired(object sender, SuspendingEventArgs e)
        {
            if(OnSuspending != null)
            {
                OnSuspending(sender, e);
            }
        }

        /// <summary>
        /// Called by the app when the app is resuming
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnResuming_Fired(object sender, object e)
        {
            if(OnResuming != null)
            {
                OnResuming(sender, e);
            }

            // Fire off an update
            FireOffUpdate();
        }

        /// <summary>
        /// Used to fire off and update if one is needed.
        /// </summary>
        public void FireOffUpdate(bool runAsync = true)
        {
            if (runAsync)
            {
                Task.Run(() =>
                {
                    BackgroundMan.RunUpdate();
                });
            }
            else
            {
                BackgroundMan.RunUpdate();
            }
        }

        #region Global Back to Front Actions

        /// <summary>
        /// Gives us a reference to the backend action listener
        /// </summary>
        /// <param name="actionListener"></param>
        public void SetBackendActionListner(IBackendActionListener actionListener)
        {
            m_backendActionListener = actionListener;
        }

        /// <summary>
        /// Shows any link globally. This will intelligently handle the link, it handle anything flip view can
        /// as well as subreddits.
        /// </summary>
        /// <param name="link"></param>
        /// <returns></returns>
        public bool ShowGlobalContent(string link)
        {
            if(m_backendActionListener == null)
            {
                return false;
            }

            m_backendActionListener.ShowGlobalContent(link);
            return true;
        }

        /// <summary>
        /// Shows any link globally. This will intelligently handle the link, it handle anything flip view can
        /// as well as subreddits.
        /// </summary>
        /// <param name="link"></param>
        /// <returns></returns>
        public bool ShowGlobalContent(RedditContentContainer container)
        {
            if (m_backendActionListener == null)
            {
                return false;
            }

            m_backendActionListener.ShowGlobalContent(container);
            return true;
        }

        /// <summary>
        /// Shows the message of they day dialog with a title and markdown content
        /// </summary>
        /// <param name="title"></param>
        /// <param name="contentMarkdown"></param>
        /// <returns></returns>
        public bool ShowMessageOfTheDay(string title, string contentMarkdown)
        {
            if (m_backendActionListener == null)
            {
                return false;
            }

            m_backendActionListener.ShowMessageOfTheDay(title, contentMarkdown);
            return true;
        }

        /// <summary>
        /// Tells the Ui to navigate to login.
        /// </summary>
        /// <returns></returns>
        public bool NavigateToLogin()
        {
            if (m_backendActionListener == null)
            {
                return false;
            }

            m_backendActionListener.NavigateToLogin();
            return true;
        }

        #endregion
    }
}
