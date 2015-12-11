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
using Windows.ApplicationModel.Background;
using System.Threading;

namespace BaconBackend
{
    // Used for the on back event
    public class OnBackButtonArgs : EventArgs
    {
        public bool IsHandled = false;
    }

    public class OnSuspendingArgs : EventArgs
    {
        public RefCountedDeferral RefDeferral;
    }

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
        public event EventHandler<OnSuspendingArgs> OnSuspending
        {
            add { m_onSuspending.Add(value); }
            remove { m_onSuspending.Remove(value); }
        }
        SmartWeakEvent<EventHandler<OnSuspendingArgs>> m_onSuspending = new SmartWeakEvent<EventHandler<OnSuspendingArgs>>();

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

            // Setup the in between invoke handler for the onBackButton event. This will allow us to stop
            // calling the handlers when one returns true.
            m_onBackButton.SetInBetweenInvokesAction(new Func<EventArgs, bool>(InBetweenInvokeHandlerForOnBackButton));
        }

        /// <summary>
        /// Called by the app when the app is suspending
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnSuspending_Fired(object sender, SuspendingEventArgs e)
        {
            // Setup a ref deferral for everyone to hold. We also need to setup a clean up action to save the setting
            // when the deferral is done.
            RefCountedDeferral refDeferral = new RefCountedDeferral(e.SuspendingOperation.GetDeferral(), () =>
            {
                // We need to flush the settings here just before we complete the deferal. We need to block this function
                // until the settings are flushed.
                using (AutoResetEvent are = new AutoResetEvent(false))
                {
                    Task.Run(async () =>
                    {
                        // Flush out the local settings
                        await SettingsMan.FlushLocalSettings();
                        are.Set();
                    });
                    are.WaitOne();
                }
            });

            // Add a ref to cover anyone down this call stack.
            refDeferral.AddRef();

            // Make the 
            OnSuspendingArgs args = new OnSuspendingArgs()
            {
                RefDeferral = refDeferral
            };

            // Fire the event
            m_onSuspending.Raise(this, args);

            // Release our ref to the deferral
            refDeferral.ReleaseRef();
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
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnBackButton_Fired(ref bool isHandled)
        {
            // Fire the event.
            OnBackButtonArgs args = new OnBackButtonArgs();
            m_onBackButton.Raise(this, args);

            // If someone handled it don't navigate back
            if(args.IsHandled)
            {
                isHandled = true;
                return;
            }

            // Tell the UI to go back. Technically it could just listen to the event
            // and check the handled var, but this ensures it is always last.
            isHandled = m_backendActionListener.NavigateBack();
        }

        /// <summary>
        /// This is called between invokes of m_onBackButton while it is being raised to each
        /// listener. If a listener sets e.IsHandled to true we should stop asking more people.
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private bool InBetweenInvokeHandlerForOnBackButton(EventArgs e)
        {
            return !((OnBackButtonArgs)e).IsHandled;
        }

        /// <summary>
        /// Used to fire off and update if one is needed.
        /// </summary>
        public void FireOffUpdate()
        {
            // Fire off an update on a background thread.
            Task.Run(async () =>
            {
                // Call update on background man and give him a null deferral
                // since this won't be called from the background.
                await BackgroundMan.RunUpdate(new RefCountedDeferral(null));
            });            
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
