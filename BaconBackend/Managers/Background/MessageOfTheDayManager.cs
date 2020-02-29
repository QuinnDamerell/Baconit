using BaconBackend.DataObjects;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace BaconBackend.Managers
{
    public class MessageOfTheDayManager
    {
        private const string CMotdUrl = "http://baconit.quinndamerell.com/motd/motd_win10.json";
        private const int CMinHoursBetweenCheck = 12;

        private readonly BaconManager _mBaconMan;

        public MessageOfTheDayManager(BaconManager baconMan)
        {
            _mBaconMan = baconMan;
        }

        public async Task CheckForUpdates()
        {
            // Don't check from the background
            if(_mBaconMan.IsBackgroundTask)
            {
                return;
            }

            // The current app version.
            var curAppVersion = Package.Current.Id.Version;

            // Check if the current version of the app is larger than the version we 
            // last checked on. (meaning we were updated)
            var forceUpdateDueAppVersion = LastVersionCheckedOn == null || LastVersionCheckedOn.Major < curAppVersion.Major || LastVersionCheckedOn.Minor < curAppVersion.Minor || LastVersionCheckedOn.Build < curAppVersion.Build || LastVersionCheckedOn.Revision < curAppVersion.Revision;

            // Check if we should update
            var timeSinceLastUpdate = DateTime.Now - LastUpdate;
            if(timeSinceLastUpdate.TotalHours > CMinHoursBetweenCheck || forceUpdateDueAppVersion)
            {
                // Get the new message
                MessageOfTheDay newMotd = null;

                // Make sure everything went well
                if (newMotd != null)
                {
                    // Since we successfully got a MOTD update the last update time
                    // and app version.
                    LastUpdate = DateTime.Now;                    
                    LastVersionCheckedOn = new AppVersion
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
                            if (_mBaconMan.UiSettingsMan.AppOpenedCount > newMotd.MinOpenTimes)
                            {
                                if (!newMotd.IsIgnore)
                                {
                                    // Show the message!
                                    // We need to loop because sometimes we can get the message faster than the UI is even ready.
                                    // When the UI isn't ready the function will return false. So just sleep until we can show it.
                                    var showSuccess = false;
                                    while (!showSuccess)
                                    {
                                        showSuccess = _mBaconMan.ShowMessageOfTheDay(newMotd.Title, newMotd.MarkdownContent);

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
                var webResult = await NetworkManager.MakeGetRequest(CMotdUrl);

                // Get the input stream and json reader.
                // NOTE!! We are really careful not to use a string here so we don't have to allocate a huge string.
                var inputStream = await webResult.ReadAsInputStreamAsync();
                using (var reader = new StreamReader(inputStream.AsStreamForRead()))
                using (JsonReader jsonReader = new JsonTextReader(reader))
                {
                    // Parse the Json as an object
                    var serializer = new JsonSerializer();
                    return await Task.Run(() => serializer.Deserialize<MessageOfTheDay>(jsonReader));
                }                    
            }
            catch(Exception e)
            {
                _mBaconMan.MessageMan.DebugDia("failed to get motd", e);
                TelemetryManager.ReportUnexpectedEvent(this, "FailedToGetMotd", e);
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
                if (!_lastUpdate.Equals(new DateTime(0))) return _lastUpdate;
                if (_mBaconMan.SettingsMan.RoamingSettings.ContainsKey("MessageOfTheDayManager.LastUpdate"))
                {
                    _lastUpdate = _mBaconMan.SettingsMan.ReadFromRoamingSettings<DateTime>("MessageOfTheDayManager.LastUpdate");
                }
                return _lastUpdate;
            }
            private set
            {
                _lastUpdate = value;
                _mBaconMan.SettingsMan.WriteToRoamingSettings("MessageOfTheDayManager.LastUpdate", _lastUpdate);
            }
        }
        private DateTime _lastUpdate = new DateTime(0);


        /// <summary>
        /// The message of the day object we last got.
        /// </summary>
        public MessageOfTheDay LastMotd
        {
            get
            {
                if (_lastMotd != null) return _lastMotd;
                if (_mBaconMan.SettingsMan.RoamingSettings.ContainsKey("MessageOfTheDayManager.LastMotd"))
                {
                    _lastMotd = _mBaconMan.SettingsMan.ReadFromRoamingSettings<MessageOfTheDay>("MessageOfTheDayManager.LastMotd");
                }
                return _lastMotd;
            }
            private set
            {
                _lastMotd = value;
                _mBaconMan.SettingsMan.WriteToRoamingSettings("MessageOfTheDayManager.LastMotd", _lastMotd);
            }
        }
        private MessageOfTheDay _lastMotd;

        /// <summary>
        /// The last version of the app we checked on.
        /// </summary>
        public AppVersion LastVersionCheckedOn
        {
            get
            {
                if (_lastVersionCheckedOn != null) return _lastVersionCheckedOn;
                if (_mBaconMan.SettingsMan.RoamingSettings.ContainsKey("MessageOfTheDayManager.LastVersionCheckedOn"))
                {
                    _lastVersionCheckedOn = _mBaconMan.SettingsMan.ReadFromRoamingSettings<AppVersion>("MessageOfTheDayManager.LastVersionCheckedOn");
                }
                return _lastVersionCheckedOn;
            }
            private set
            {
                _lastVersionCheckedOn = value;
                _mBaconMan.SettingsMan.WriteToRoamingSettings("MessageOfTheDayManager.LastVersionCheckedOn", _lastVersionCheckedOn);
            }
        }
        private AppVersion _lastVersionCheckedOn;

        #endregion
    }
}
