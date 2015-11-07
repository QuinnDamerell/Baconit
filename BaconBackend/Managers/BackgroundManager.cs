using BaconBackend.Managers.Background;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public BackgroundManager(BaconManager baconMan)
        {
            m_baconMan = baconMan;
            ImageUpdaterMan = new BackgroundImageUpdater(baconMan);
        }

        /// <summary>
        /// Used to make sure the background task is setup.
        /// </summary>
        public void EnsureBackgroundSetup()
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

            if (ImageUpdaterMan.IsLockScreenEnabled || ImageUpdaterMan.IsDeskopEnabled)
            {
                if(foundTask == null)
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
        /// Used to actually kick off all of the updating. This can be called in the app
        /// as well by the updater.
        /// </summary>
        public void RunUpdate()
        {
            // If we are a background task report the time
            if (m_baconMan.IsBackgroundTask)
            {
                LastAttemptedUpdate = DateTime.Now;
            }

            // If we are not a background task check message of the day
            if(!m_baconMan.IsBackgroundTask)
            {
                // This will continue on async
                m_baconMan.MotdMan.CheckForUpdates();

                // Sleep for a little while to give the UI some time get work done.
                new System.Threading.ManualResetEvent(false).WaitOne(5000);
            }

            // Ensure everything is ready
            EnsureBackgroundSetup();

            // Run the image update if needed
            bool wasSuccess = ImageUpdaterMan.RunUpdate();

            if(m_baconMan.IsBackgroundTask && wasSuccess)
            {
                LastSuccessfulUpdate = DateTime.Now;
            }
        }

        #region Vars

        /// <summary>
        /// The last time we tried to update
        /// </summary>
        public DateTime LastAttemptedUpdate
        {
            get
            {
                if (m_lastAttemptedUpdate.Equals(new DateTime(0)))
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundManager.LastAttemptedUpdate"))
                    {
                        m_lastAttemptedUpdate = m_baconMan.SettingsMan.ReadFromLocalSettings<DateTime>("BackgroundManager.LastAttemptedUpdate");
                    }
                }
                return m_lastAttemptedUpdate;
            }
            private set
            {
                m_lastAttemptedUpdate = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<DateTime>("BackgroundManager.LastAttemptedUpdate", m_lastAttemptedUpdate);
            }
        }
        private DateTime m_lastAttemptedUpdate = new DateTime(0);

        /// <summary>
        /// The last time we tried to update
        /// </summary>
        public DateTime LastSuccessfulUpdate
        {
            get
            {
                if (m_lastSuccessfulUpdate.Equals(new DateTime(0)))
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundManager.LastSuccessfulUpdate"))
                    {
                        m_lastSuccessfulUpdate = m_baconMan.SettingsMan.ReadFromLocalSettings<DateTime>("BackgroundManager.LastSuccessfulUpdate");
                    }
                }
                return m_lastSuccessfulUpdate;
            }
            private set
            {
                m_lastSuccessfulUpdate = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<DateTime>("BackgroundManager.LastSuccessfulUpdate", m_lastSuccessfulUpdate);
            }
        }
        private DateTime m_lastSuccessfulUpdate = new DateTime(0);

        #endregion
    }
}
