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
using Windows.UI.Core;

namespace BaconBackend
{
    /// <summary>
    /// Provides data for BaconManager's OnBackButton event.
    /// </summary>
    public class OnBackButtonArgs : EventArgs
    {
        /// <summary>
        /// If the back button press has been handled already.
        /// </summary>
        public bool IsHandled = false;
    }

    /// <summary>
    /// A background object that manages all things reddit.
    /// </summary>
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
        public event EventHandler<EventArgs> OnSuspending
        {
            add { m_onSuspending.Add(value); }
            remove { m_onSuspending.Remove(value); }
        }
        SmartWeakEvent<EventHandler<EventArgs>> m_onSuspending = new SmartWeakEvent<EventHandler<EventArgs>>();

        /// <summary>
        /// Fired when the app is resuming
        /// </summary>
        public event EventHandler<EventArgs> OnResuming
        {
            add { m_onResuming.Add(value); }
            remove { m_onResuming.Remove(value); }
        }
        SmartWeakEvent<EventHandler<EventArgs>> m_onResuming = new SmartWeakEvent<EventHandler<EventArgs>>();

        /// <summary>
        /// Fired when the back button is pressed
        /// </summary>
        public event EventHandler<OnBackButtonArgs> OnBackButton
        {
            add { m_onBackButton.Add(value); }
            remove { m_onBackButton.Remove(value); }
        }
        SmartWeakEvent<EventHandler<OnBackButtonArgs>> m_onBackButton = new SmartWeakEvent<EventHandler<OnBackButtonArgs>>();

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
        /// Manages the tiles on the start screen
        /// </summary>
        public TileManager TileMan { get; }

        /// <summary>
        /// Used for draft management.
        /// </summary>
        public DraftManager DraftMan { get; }

        /// <summary>
        /// Holds a connection to the front end, a way for things back here to
        /// interact with the front end.
        /// </summary>
        private IBackendActionListener m_backendActionListener;

        /// <summary>
        /// Create a new BaconManager.
        /// </summary>
        /// <param name="isBackgroundTask">If this Manager should be run in the background.</param>
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
            TileMan = new TileManager(this);
            DraftMan = new DraftManager(this);

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
            // Fire the event
            m_onSuspending.Raise(this, new EventArgs());
        }

        /// <summary>
        /// Called by the app when the app is resuming
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnResuming_Fired(object sender, object e)
        {
            // Fire the event.
            m_onResuming.Raise(this, new EventArgs());

            // Fire off an update
            FireOffUpdate();
        }

        /// <summary>
        /// Called by the app when the back button is pressed
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Back button navigation event data.</param>
        public void OnBackButton_Fired(object sender, BackRequestedEventArgs e)
        {
            // Fire the event.
            OnBackButtonArgs args = new OnBackButtonArgs();
            m_onBackButton.Raise(this, args);

            // If someone handled it don't navigate back
            if(args.IsHandled)
            {
                e.Handled = true;
                return;
            }

            // Tell the UI to go back. Technically it could just listen to the event
            // and check the handled var, but this ensures it is always last.
            e.Handled = m_backendActionListener.NavigateBack();
        }

        /// <summary>
        /// Used to fire off and update if one is needed.
        /// <param name="runAsync">If the update should run asynchronously.</param>
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
        /// <param name="actionListener">Backend action listener</param>
        public void SetBackendActionListener(IBackendActionListener actionListener)
        {
            m_backendActionListener = actionListener;
        }

        /// <summary>
        /// Tries to show any link globally. This will intelligently handle the link;
        /// it can handle anything flip view can, as well as subreddits.
        /// </summary>
        /// <param name="link">URL to show.</param>
        /// <returns>If the link was successfully shown.</returns>
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
        /// Tries to show any link globally. This will intelligently handle the link, it handle anything flip view can
        /// as well as subreddits.
        /// </summary>
        /// <param name="container">Content to be shown.</param>
        /// <returns>If the content was successfully shown.</returns>
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
        /// Try to show the message of they day dialog with a title and markdown content
        /// </summary>
        /// <param name="title">Message of the day's title.</param>
        /// <param name="contentMarkdown">Markdown of the Message of the day's body.</param>
        /// <returns>If the Message of the day was successfully shown.</returns>
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
        /// Try to navigate the application to a login form.
        /// </summary>
        /// <returns>If the login form was successfully shown.</returns>
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
