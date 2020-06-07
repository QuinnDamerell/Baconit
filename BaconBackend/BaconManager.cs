using System;
using System.Threading.Tasks;
using BaconBackend.Managers;
using Windows.ApplicationModel;
using BaconBackend.Interfaces;
using BaconBackend.Helpers;
using System.Threading;

namespace BaconBackend
{
    /// <summary>
    /// Provides data for BaconManager's OnBackButton event.
    /// </summary>
    public class BackButtonArgs : EventArgs
    {
        /// <summary>
        /// If the back button press has been handled already.
        /// </summary>
        public bool IsHandled = false;
    }

    /// <summary>
    /// Provides data for when the application is suspending.
    /// </summary>
    public class SuspendingArgs : EventArgs
    {
        public RefCountedDeferral RefDeferral;
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
        public event EventHandler<SuspendingArgs> OnSuspending
        {
            add => _suspending.Add(value);
            remove => _suspending.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<SuspendingArgs>> _suspending = new SmartWeakEvent<EventHandler<SuspendingArgs>>();

        /// <summary>
        /// Fired when the app is resuming
        /// </summary>
        public event EventHandler<EventArgs> OnResuming
        {
            add => _resuming.Add(value);
            remove => _resuming.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<EventArgs>> _resuming = new SmartWeakEvent<EventHandler<EventArgs>>();

        /// <summary>
        /// Fired when the back button is pressed
        /// </summary>
        public event EventHandler<BackButtonArgs> OnBackButton
        {
            add => _backButton.Add(value);
            remove => _backButton.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<BackButtonArgs>> _backButton = new SmartWeakEvent<EventHandler<BackButtonArgs>>();

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
        /// Used for watching the current app's memory.
        /// </summary>
        public MemoryManager MemoryMan { get; }

        /// <summary>
        /// Holds a connection to the front end, a way for things back here to
        /// interact with the front end.
        /// </summary>
        private IBackendActionListener _backendActionListener;

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
            TelemetryMan = new TelemetryManager(this);
            BackgroundMan = new BackgroundManager(this);
            MotdMan = new MessageOfTheDayManager(this);
            TileMan = new TileManager(this);
            DraftMan = new DraftManager(this);
            MemoryMan = new MemoryManager(this);

            // Don't do this if we are a background task; it will
            // call this when it is ready.
            if (!isBackgroundTask)
            {
                FireOffUpdate();
            }

            // Setup the in between invoke handler for the onBackButton event. This will allow us to stop
            // calling the handlers when one returns true.
            _backButton.SetInBetweenInvokesAction(InBetweenInvokeHandlerForOnBackButton);
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
            var refDeferral = new RefCountedDeferral(e.SuspendingOperation.GetDeferral(), () =>
            {
                // We need to flush the settings here just before we complete the deferral. We need to block this function
                // until the settings are flushed.
                using (var are = new AutoResetEvent(false))
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
            var args = new SuspendingArgs
            {
                RefDeferral = refDeferral
            };

            // Fire the event
            _suspending.Raise(this, args);

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
            _resuming.Raise(this, new EventArgs());

            // Fire off an update
            FireOffUpdate();
        }

        /// <summary>
        /// Called by the app when the back button is pressed
        /// </summary>
        /// <param name="isHandled">Reference variable whether the back navigation has been handled.</param>
        public void OnBackButton_Fired(ref bool isHandled)
        {
            // Fire the event.
            var args = new BackButtonArgs();
            _backButton.Raise(this, args);

            // If someone handled it don't navigate back
            if(args.IsHandled)
            {
                isHandled = true;
                return;
            }

            // Tell the UI to go back. Technically it could just listen to the event
            // and check the handled var, but this ensures it is always last.
            isHandled = _backendActionListener.NavigateBack();
        }

        /// <summary>
        /// This is called between invokes of _backButton while it is being raised to each
        /// listener. If a listener sets e.IsHandled to true we should stop asking more people.
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private static bool InBetweenInvokeHandlerForOnBackButton(EventArgs e)
        {
            return !((BackButtonArgs)e).IsHandled;
        }

        /// <summary>
        /// Used to fire off and update if one is needed.
        /// <param name="runAsync">If the update should run asynchronously.</param>
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
        /// <param name="actionListener">Backend action listener</param>
        public void SetBackendActionListener(IBackendActionListener actionListener)
        {
            _backendActionListener = actionListener;
        }

        /// <summary>
        /// Tries to show any link globally. This will intelligently handle the link;
        /// it can handle anything flip view can, as well as subreddits.
        /// </summary>
        /// <param name="link">URL to show.</param>
        /// <returns>If the link was successfully shown.</returns>
        public bool ShowGlobalContent(string link)
        {
            if( _backendActionListener == null)
            {
                return false;
            }

            _backendActionListener.ShowGlobalContent(link);
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
            if ( _backendActionListener == null)
            {
                return false;
            }

            _backendActionListener.ShowGlobalContent(container);
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
            if ( _backendActionListener == null)
            {
                return false;
            }

            _backendActionListener.ShowMessageOfTheDay(title, contentMarkdown);
            return true;
        }

        /// <summary>
        /// Try to navigate the application to a login form.
        /// </summary>
        /// <returns>If the login form was successfully shown.</returns>
        public bool NavigateToLogin()
        {
            if ( _backendActionListener == null)
            {
                return false;
            }

            _backendActionListener.NavigateToLogin();
            return true;
        }

        #endregion
    }
}
