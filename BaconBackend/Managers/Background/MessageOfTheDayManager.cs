using BaconBackend.DataObjects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Web.Http;

namespace BaconBackend.Managers
{
    public class MessageOfTheDayManager
    {
        const string c_motdUrl = "https://dl.dropboxusercontent.com/u/451896/motd_win10.txt";
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

            // Check if we should update
            TimeSpan timeSinceLastUpdate = DateTime.Now - LastUpdate;
            if(timeSinceLastUpdate.TotalHours > c_minHoursBetweenCheck)
            {
                // Get the new message
                MessageOfTheDay newMotd = await GetNewMessage();

                // Make sure everything went well
                if(newMotd != null)
                {
                    // Check for an update
                    if(LastMotd == null || !newMotd.UniqueId.Equals(LastMotd.UniqueId))
                    {
                        // Get the current package version.
                        PackageVersion curVer = Package.Current.Id.Version;

                        // There is an update! If we have a version number in the MOTD make sure we are >= the min.
                        if ((newMotd.MinVerMajor == 0)
                            || (curVer.Major >= newMotd.MinVerMajor && curVer.Minor >= newMotd.MinVerMinor && curVer.Build >= newMotd.MinVerBuild && curVer.Revision >= newMotd.MinVerRev))
                        {
                            // Make sure we have been opened enough.
                            if (m_baconMan.UiSettingsMan.AppOpenedCount > newMotd.MinOpenTimes)
                            {
                                if (!newMotd.isIngore)
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

                                // Update the message and last updated time
                                LastUpdate = DateTime.Now;
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
                // Make the request
                IHttpContent response = await m_baconMan.NetworkMan.MakeGetRequest(c_motdUrl);
                string jsonResponse = await response.ReadAsStringAsync();

                // Try to parse it
                return JsonConvert.DeserializeObject<MessageOfTheDay>(jsonResponse);
            }
            catch(Exception e)
            {
                m_baconMan.MessageMan.DebugDia("failed to get motd", e);
                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToGetMotd", e);
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

        #endregion
    }
}
