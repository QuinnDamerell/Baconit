using BaconBackend.Helpers;
using BaconBackend.Managers.Background;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;

namespace BaconBackend.Managers
{
    public class BackgroundManager
    {
        // Since this updater will do multiple things, it will always fire at 30 minutes
        // and each action will check if they should act or not.
        private const int c_backgroundUpdateTime = 30;
        private const string c_backgroundTaskName = "Baconit Background Updater";

        private BaconManager m_baconMan;

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
            m_baconMan = baconMan;
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
                if(task.Value.Name.Equals(c_backgroundTaskName))
                {
                    foundTask = task.Value;
                }
            }

            // For some dumb reason in WinRT we have to ask for permission to run in the background each time
            // our app is updated....
            String currentAppVersion = String.Format("{0}.{1}.{2}.{3}",
                Package.Current.Id.Version.Build,
                Package.Current.Id.Version.Major,
                Package.Current.Id.Version.Minor,
                Package.Current.Id.Version.Revision);
            if (String.IsNullOrWhiteSpace(AppVersionWithBackgroundAccess) || !AppVersionWithBackgroundAccess.Equals(currentAppVersion))
            {
                // Update current version
                AppVersionWithBackgroundAccess = currentAppVersion;

                // Remove access... stupid winrt.
                BackgroundExecutionManager.RemoveAccess();
            }

            // Request access to run in the background.
            BackgroundAccessStatus status = await BackgroundExecutionManager.RequestAccessAsync();
            LastSystemBackgroundUpdateStatus = (int)status;
            if(status != BackgroundAccessStatus.AllowedMayUseActiveRealTimeConnectivity && status != BackgroundAccessStatus.AllowedWithAlwaysOnRealTimeConnectivity)
            {
                m_baconMan.MessageMan.DebugDia("System denied us access from running in the background");
            }
            
            if (ImageUpdaterMan.IsLockScreenEnabled || ImageUpdaterMan.IsDeskopEnabled || MessageUpdaterMan.IsEnabled)
            {
                if (foundTask == null)
                {
                    // We need to make a new task
                    BackgroundTaskBuilder builder = new BackgroundTaskBuilder();
                    builder.Name = c_backgroundTaskName;
                    builder.TaskEntryPoint = "BaconBackground.BackgroundEntry";
                    builder.SetTrigger(new TimeTrigger(c_backgroundUpdateTime, false));
                    builder.SetTrigger(new MaintenanceTrigger(c_backgroundUpdateTime, false));

                    try
                    {
                        builder.Register();
                    }
                    catch(Exception e)
                    {
                        m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "failed to register background task", e);
                        m_baconMan.MessageMan.DebugDia("failed to register background task", e);
                    }
                }
            }
            else
            {
                if(foundTask != null)
                {
                    // We need to stop and unregister the background task.
                    foundTask.Unregister(true);
                }
            }
        }

        /// <summary>
        /// This will actually kick off all of the work.
        /// </summary>
        public async Task RunUpdate(RefCountedDeferral refDefferal)
        {
            // If we are a background task report the time
            if (m_baconMan.IsBackgroundTask)
            {
                LastUpdateTime = DateTime.Now;
            }

            // If we are not a background task check message of the day
            if(!m_baconMan.IsBackgroundTask)
            {
                // Check for MOTD updates.
                await m_baconMan.MotdMan.CheckForUpdates();

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
                if (m_lastAttemptedUpdate.Equals(new DateTime(0)))
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundManager.LastUpdateTime"))
                    {
                        m_lastAttemptedUpdate = m_baconMan.SettingsMan.ReadFromLocalSettings<DateTime>("BackgroundManager.LastUpdateTime");
                    }
                }
                return m_lastAttemptedUpdate;
            }
            private set
            {
                m_lastAttemptedUpdate = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<DateTime>("BackgroundManager.LastUpdateTime", m_lastAttemptedUpdate);
            }
        }
        private DateTime m_lastAttemptedUpdate = new DateTime(0);

        /// <summary>
        /// Indicates what we got as a response the last time we asked for permission to run
        /// in the background.
        /// </summary>
        public int LastSystemBackgroundUpdateStatus
        {
            get
            {
                if (!m_lastSystemBackgroundUpdateStatus.HasValue)
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundManager.LastSystemBackgroundUpdateStatus"))
                    {
                        m_lastSystemBackgroundUpdateStatus = m_baconMan.SettingsMan.ReadFromLocalSettings<int>("BackgroundManager.LastSystemBackgroundUpdateStatus");
                    }
                    else
                    {
                        m_lastSystemBackgroundUpdateStatus = 3;
                    }
                }
                return m_lastSystemBackgroundUpdateStatus.Value;
            }
            private set
            {
                m_lastSystemBackgroundUpdateStatus = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<int>("BackgroundManager.LastSystemBackgroundUpdateStatus", m_lastSystemBackgroundUpdateStatus.Value);
            }
        }
        private int? m_lastSystemBackgroundUpdateStatus = null;

        /// <summary>
        /// The last app version we requested access for. For some reason in WinRT you have to request access each time
        /// you update.
        /// </summary>
        private string AppVersionWithBackgroundAccess
        {
            get
            {
                if (m_appVersionWithBackgroundAccess ==  null)
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundManager.AppVersionWithBackgroundAccess"))
                    {
                        m_appVersionWithBackgroundAccess = m_baconMan.SettingsMan.ReadFromLocalSettings<string>("BackgroundManager.AppVersionWithBackgroundAccess");
                    }
                    else
                    {
                        m_appVersionWithBackgroundAccess = String.Empty;
                    }
                }
                return m_appVersionWithBackgroundAccess;
            }
            set
            {
                m_appVersionWithBackgroundAccess = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<string>("BackgroundManager.AppVersionWithBackgroundAccess", m_appVersionWithBackgroundAccess);
            }
        }
        private string m_appVersionWithBackgroundAccess = null;

        #endregion
    }
}
