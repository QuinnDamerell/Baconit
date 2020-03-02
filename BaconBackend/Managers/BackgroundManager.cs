using BaconBackend.Helpers;
using BaconBackend.Managers.Background;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;

namespace BaconBackend.Managers
{
    public class BackgroundManager
    {
        // Since this updater will do multiple things, it will always fire at 30 minutes
        // and each action will check if they should act or not.
        private const int BackgroundUpdateTime = 30;
        private const string BackgroundTaskName = "Baconit Background Updater";

        private readonly BaconManager _baconMan;

        /// <summary>
        /// In charge of image updates
        /// </summary>
        public BackgroundImageUpdater ImageUpdaterMan { get; }

        /// <summary>
        /// In charge of image updates
        /// </summary>
        public BackgroundMessageUpdater MessageUpdaterMan { get; }

        /// <summary>
        /// In charge of image updates
        /// </summary>
        public BackgroundBandManager BandMan { get; }


        public BackgroundManager(BaconManager baconMan)
        {
            _baconMan = baconMan;
            ImageUpdaterMan = new BackgroundImageUpdater(baconMan);
            MessageUpdaterMan = new BackgroundMessageUpdater(baconMan);
            BandMan = new BackgroundBandManager(baconMan);
        }

        /// <summary>
        /// Used to make sure the background task is setup.
        /// </summary>
        public async Task EnsureBackgroundSetup()
        {
            // Try to find any task that we might have
            IBackgroundTaskRegistration foundTask = null;
            foreach (var task in BackgroundTaskRegistration.AllTasks)
            {
                if(task.Value.Name.Equals(BackgroundTaskName))
                {
                    foundTask = task.Value;
                }
            }

            // For some dumb reason in WinRT we have to ask for permission to run in the background each time
            // our app is updated....
            var currentAppVersion =
                $"{Package.Current.Id.Version.Build}.{Package.Current.Id.Version.Major}.{Package.Current.Id.Version.Minor}.{Package.Current.Id.Version.Revision}";
            if (string.IsNullOrWhiteSpace(AppVersionWithBackgroundAccess) || !AppVersionWithBackgroundAccess.Equals(currentAppVersion))
            {
                // Update current version
                AppVersionWithBackgroundAccess = currentAppVersion;

                // Remove access... stupid winrt.
                BackgroundExecutionManager.RemoveAccess();
            }

            // Request access to run in the background.
            var status = await BackgroundExecutionManager.RequestAccessAsync();
            LastSystemBackgroundUpdateStatus = (int)status;
            if(status != BackgroundAccessStatus.AllowedSubjectToSystemPolicy && status != BackgroundAccessStatus.AllowedSubjectToSystemPolicy)
            {
                _baconMan.MessageMan.DebugDia("System denied us access from running in the background");
            }
            
            if (ImageUpdaterMan.IsLockScreenEnabled || ImageUpdaterMan.IsDesktopEnabled || MessageUpdaterMan.IsEnabled)
            {
                if (foundTask == null)
                {
                    // We need to make a new task
                    var builder = new BackgroundTaskBuilder
                    {
                        Name = BackgroundTaskName, TaskEntryPoint = "BaconBackground.BackgroundEntry"
                    };
                    builder.SetTrigger(new TimeTrigger(BackgroundUpdateTime, false));
                    builder.SetTrigger(new MaintenanceTrigger(BackgroundUpdateTime, false));

                    try
                    {
                        builder.Register();
                    }
                    catch(Exception e)
                    {
                        TelemetryManager.ReportUnexpectedEvent(this, "failed to register background task", e);
                        _baconMan.MessageMan.DebugDia("failed to register background task", e);
                    }
                }
            }
            else
            {
                // We need to stop and unregister the background task.
                foundTask?.Unregister(true);
            }
        }

        /// <summary>
        /// This will actually kick off all of the work.
        /// </summary>
        public async Task RunUpdate(RefCountedDeferral refDefferal)
        {
            // If we are a background task report the time
            if (_baconMan.IsBackgroundTask)
            {
                LastUpdateTime = DateTime.Now;
            }

            // If we are not a background task check message of the day
            if(!_baconMan.IsBackgroundTask)
            {
                // Sleep for a little while to give the UI some time get work done.
                await Task.Delay(1000);

                // Check for MOTD updates.
                await _baconMan.MotdMan.CheckForUpdates();

                // Sleep for a little while to give the UI some time get work done.
                await Task.Delay(5000);
            }

            // Ensure everything is ready
            await EnsureBackgroundSetup();

            // Run the image update if needed
            await ImageUpdaterMan.RunUpdate(refDefferal);

            // Run the message update if needed.
            MessageUpdaterMan.RunUpdate(refDefferal);            
        }

        #region Vars

        /// <summary>
        /// The last time we tried to update
        /// </summary>
        public DateTime LastUpdateTime
        {
            get
            {
                if (!_lastAttemptedUpdate.Equals(new DateTime(0))) return _lastAttemptedUpdate;
                if (_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundManager.LastUpdateTime"))
                {
                    _lastAttemptedUpdate = _baconMan.SettingsMan.ReadFromLocalSettings<DateTime>("BackgroundManager.LastUpdateTime");
                }
                return _lastAttemptedUpdate;
            }
            private set
            {
                _lastAttemptedUpdate = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundManager.LastUpdateTime", _lastAttemptedUpdate);
            }
        }
        private DateTime _lastAttemptedUpdate = new DateTime(0);

        /// <summary>
        /// Indicates what we got as a response the last time we asked for permission to run
        /// in the background.
        /// </summary>
        public int LastSystemBackgroundUpdateStatus
        {
            get
            {
                if (_lastSystemBackgroundUpdateStatus.HasValue) return _lastSystemBackgroundUpdateStatus.Value;
                _lastSystemBackgroundUpdateStatus = _baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundManager.LastSystemBackgroundUpdateStatus") 
                    ? _baconMan.SettingsMan.ReadFromLocalSettings<int>("BackgroundManager.LastSystemBackgroundUpdateStatus") 
                    : 3;
                return _lastSystemBackgroundUpdateStatus.Value;
            }
            private set
            {
                _lastSystemBackgroundUpdateStatus = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundManager.LastSystemBackgroundUpdateStatus", _lastSystemBackgroundUpdateStatus.Value);
            }
        }
        private int? _lastSystemBackgroundUpdateStatus;

        /// <summary>
        /// The last app version we requested access for. For some reason in WinRT you have to request access each time
        /// you update.
        /// </summary>
        private string AppVersionWithBackgroundAccess
        {
            get =>
                _appVersionWithBackgroundAccess ?? (_appVersionWithBackgroundAccess =
                    _baconMan.SettingsMan.LocalSettings.ContainsKey(
                        "BackgroundManager.AppVersionWithBackgroundAccess")
                        ? _baconMan.SettingsMan.ReadFromLocalSettings<string>(
                            "BackgroundManager.AppVersionWithBackgroundAccess")
                        : string.Empty);
            set
            {
                _appVersionWithBackgroundAccess = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundManager.AppVersionWithBackgroundAccess", _appVersionWithBackgroundAccess);
            }
        }
        private string _appVersionWithBackgroundAccess;

        #endregion
    }
}
