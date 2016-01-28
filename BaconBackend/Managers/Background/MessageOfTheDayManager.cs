using BaconBackend.DataObjects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace BaconBackend.Managers
{
    public class MessageOfTheDayManager
    {
        const string c_motdUrl = "http://baconit.quinndamerell.com/motd/motd_win10.json";
        const int c_minHoursBetweenCheck = 12;

        BaconManager m_baconMan;

        public MessageOfTheDayManager(BaconManager baconMan)
        {
            m_baconMan = baconMan;
        }

        public async Task CheckForUpdates()
        {
            // Don't check from the background
            if(m_baconMan.IsBackgroundTask)
            {
                return;
            }

            // The current app version.
            PackageVersion curAppVersion = Package.Current.Id.Version;

            // Check if the current version of the app is larger than the version we 
            // last checked on. (meaning we were updated)
            bool forceUpdateDueAppVersion = false;
            if(LastVersionCheckedOn == null || LastVersionCheckedOn.Major < curAppVersion.Major || LastVersionCheckedOn.Minor < curAppVersion.Minor || LastVersionCheckedOn.Build < curAppVersion.Build || LastVersionCheckedOn.Revision < curAppVersion.Revision)
            {
                forceUpdateDueAppVersion = true;
            }

            // Check if we should update
            TimeSpan timeSinceLastUpdate = DateTime.Now - LastUpdate;
            if(timeSinceLastUpdate.TotalHours > c_minHoursBetweenCheck || forceUpdateDueAppVersion)
            {
                // Get the new message
                MessageOfTheDay newMotd = await GetNewMessage();

                // Make sure everything went well
                if (newMotd != null)
                {
                    // Since we successfully got a MOTD update the last update time
                    // and app version.
                    LastUpdate = DateTime.Now;                    
                    LastVersionCheckedOn = new AppVersion()
                    {
                        Major = curAppVersion.Major,
                        Minor = curAppVersion.Minor,
                        Build = curAppVersion.Build,
                        Revision = curAppVersion.Revision
                    };

                    // Check for a difference
                    if (LastMotd == null || !newMotd.UniqueId.Equals(LastMotd.UniqueId))
                    {
                        // There is an update! If we have a version number in the MOTD make sure we are >= the min.
                        if ((newMotd.MinVerMajor == 0)
                            || (curAppVersion.Major >= newMotd.MinVerMajor && curAppVersion.Minor >= newMotd.MinVerMinor && curAppVersion.Build >= newMotd.MinVerBuild && curAppVersion.Revision >= newMotd.MinVerRev))
                        {
                            // Make sure we have been opened enough.
                            if (m_baconMan.UiSettingsMan.AppOpenedCount > newMotd.MinOpenTimes)
                            {
                                if (!newMotd.isIgnore)
                                {
                                    // Show the message!
                                    // We need to loop because sometimes we can get the message faster than the UI is even ready.
                                    // When the UI isn't ready the function will return false. So just sleep until we can show it.
                                    bool showSuccess = false;
                                    while (!showSuccess)
                                    {
                                        showSuccess = m_baconMan.ShowMessageOfTheDay(newMotd.Title, newMotd.MarkdownContent);

                                        if (!showSuccess)
                                        {
                                            await Task.Delay(500);
                                        }
                                    }
                                }

                                // Update that we have processed this newest MOTD.
                                LastMotd = newMotd;
                            }
                        }
                    }             
                }
            }
        }

        public async Task<MessageOfTheDay> GetNewMessage()
        {
            try
            {             
                // Make the request.
                IHttpContent webResult = await m_baconMan.NetworkMan.MakeGetRequest(c_motdUrl);

                // Get the input stream and json reader.
                // NOTE!! We are really careful not to use a string here so we don't have to allocate a huge string.
                IInputStream inputStream = await webResult.ReadAsInputStreamAsync();
                using (StreamReader reader = new StreamReader(inputStream.AsStreamForRead()))
                using (JsonReader jsonReader = new JsonTextReader(reader))
                {
                    // Parse the Json as an object
                    JsonSerializer serializer = new JsonSerializer();
                    return await Task.Run(() => serializer.Deserialize<MessageOfTheDay>(jsonReader));
                }                    
            }
            catch(Exception e)
            {
                m_baconMan.MessageMan.DebugDia("failed to get motd", e);
                m_baconMan.TelemetryMan.ReportUnexpectedEvent(this, "FailedToGetMotd", e);
            }

            return null;
        }

        #region Vars

        /// <summary>
        /// The last time the message was updated.
        /// </summary>
        public DateTime LastUpdate
        {
            get
            {
                if (m_lastUpdate.Equals(new DateTime(0)))
                {
                    if (m_baconMan.SettingsMan.RoamingSettings.ContainsKey("MessageOfTheDayManager.LastUpdate"))
                    {
                        m_lastUpdate = m_baconMan.SettingsMan.ReadFromRoamingSettings<DateTime>("MessageOfTheDayManager.LastUpdate");
                    }
                }
                return m_lastUpdate;
            }
            private set
            {
                m_lastUpdate = value;
                m_baconMan.SettingsMan.WriteToRoamingSettings<DateTime>("MessageOfTheDayManager.LastUpdate", m_lastUpdate);
            }
        }
        private DateTime m_lastUpdate = new DateTime(0);


        /// <summary>
        /// The message of the day object we last got.
        /// </summary>
        public MessageOfTheDay LastMotd
        {
            get
            {
                if (m_lastMotd == null)
                {
                    if (m_baconMan.SettingsMan.RoamingSettings.ContainsKey("MessageOfTheDayManager.LastMotd"))
                    {
                        m_lastMotd = m_baconMan.SettingsMan.ReadFromRoamingSettings<MessageOfTheDay>("MessageOfTheDayManager.LastMotd");
                    }
                }
                return m_lastMotd;
            }
            private set
            {
                m_lastMotd = value;
                m_baconMan.SettingsMan.WriteToRoamingSettings<MessageOfTheDay>("MessageOfTheDayManager.LastMotd", m_lastMotd);
            }
        }
        private MessageOfTheDay m_lastMotd = null;

        /// <summary>
        /// The last version of the app we checked on.
        /// </summary>
        public AppVersion LastVersionCheckedOn
        {
            get
            {
                if (m_LastVersionCheckedOn == null)
                {
                    if (m_baconMan.SettingsMan.RoamingSettings.ContainsKey("MessageOfTheDayManager.LastVersionCheckedOn"))
                    {
                        m_LastVersionCheckedOn = m_baconMan.SettingsMan.ReadFromRoamingSettings<AppVersion>("MessageOfTheDayManager.LastVersionCheckedOn");
                    }
                }
                return m_LastVersionCheckedOn;
            }
            private set
            {
                m_LastVersionCheckedOn = value;
                m_baconMan.SettingsMan.WriteToRoamingSettings<AppVersion>("MessageOfTheDayManager.LastVersionCheckedOn", m_LastVersionCheckedOn);
            }
        }
        private AppVersion m_LastVersionCheckedOn = null;

        #endregion
    }
}
