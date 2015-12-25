using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;
using Newtonsoft.Json;
using Windows.UI.Xaml;
using System.Threading;

namespace BaconBackend.Managers
{
    public class SettingsManager
    {
        private const string LOCAL_SETTINGS_FILE = "LocalSettings.data";
        object objectLock = new object();
        BaconManager m_baconMan;
        ManualResetEvent m_localSettingsReady = new ManualResetEvent(false);

        public SettingsManager(BaconManager baconMan)
        {
            m_baconMan = baconMan;

            // Setup the local settings.
            InitLocalSettings();

            // We can't do this from a background task
            if (!baconMan.IsBackgroundTask)
            {
                // Register for resuming callbacks.
                m_baconMan.OnResuming += BaconMan_OnResuming;
            }
        }

        /// <summary>
        /// Gets and instance of the roaming settings
        /// </summary>
        public IPropertySet RoamingSettings
        {
            get
            {
                if(m_roamingSettings == null)
                {
                    lock(objectLock)
                    {
                        if(m_roamingSettings == null)
                        {
                            m_roamingSettings = Windows.Storage.ApplicationData.Current.RoamingSettings.Values;
                        }
                    }
                }
                return m_roamingSettings;
            }
        }
        private IPropertySet m_roamingSettings;

        /// <summary>
        /// Returns an instance of the local settings
        /// </summary>
        public Dictionary<string, object> LocalSettings
        {
            get
            {
                // Since opening a file is async, we need to make sure it has been opened before
                m_localSettingsReady.WaitOne();
                return m_localSettings;
            }
        }
        private Dictionary<string, object> m_localSettings;

        /// <summary>
        /// Helper that writes objects to the local storage
        /// </summary>
        /// <param name="name"></param>
        /// <param name="obj"></param>
        public void WriteToLocalSettings<T>(string name, T obj)
        {
            LocalSettings[name] = JsonConvert.SerializeObject(obj);
        }

        /// <summary>
        /// Helper that reads objects from the local storage
        /// Useful for things WinRt can't serialize.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public T ReadFromLocalSettings<T>(string name)
        {
            string content = (string)LocalSettings[name];
            return JsonConvert.DeserializeObject<T>(content);
        }

        /// <summary>
        /// Helper that writes objects to the roaming storage
        /// </summary>
        /// <param name="name"></param>
        /// <param name="obj"></param>
        public void WriteToRoamingSettings<T>(string name, T obj)
        {
            RoamingSettings[name] = JsonConvert.SerializeObject(obj);
        }

        /// <summary>
        /// Helper that reads objects from the roaming storage
        /// Useful for things WinRt can't serialize.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public T ReadFromRoamingSettings<T>(string name)
        {
            string content = (string)RoamingSettings[name];
            return JsonConvert.DeserializeObject<T>(content);
        }

        private async void InitLocalSettings()
        {
            try
            {
                StorageFolder folder = Windows.Storage.ApplicationData.Current.LocalFolder;
                StorageFile file = await folder.CreateFileAsync(LOCAL_SETTINGS_FILE, CreationCollisionOption.OpenIfExists);

                // Read the file
                string content = await Windows.Storage.FileIO.ReadTextAsync(file);

                // Deserialize the json
                if (content != "")
                {
                    m_localSettings = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                }
                else
                {
                    m_localSettings = new Dictionary<string, object>();
                }
            }
            catch(Exception e)
            {
                m_baconMan.MessageMan.DebugDia("Unable to load settings file! "+e.Message);
                m_localSettings = new Dictionary<string, object>();
            }

            // Signal we are ready
            m_localSettingsReady.Set();
        }

        /// <summary>
        /// Fired when the app is resuming.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BaconMan_OnResuming(object sender, EventArgs e)
        {
            // When we resume read the settings again to pick up anything from the updater
            InitLocalSettings();
        }

        public async Task FlushLocalSettings()
        {
            try
            {
                StorageFolder folder = Windows.Storage.ApplicationData.Current.LocalFolder;
                StorageFile file = await folder.CreateFileAsync(LOCAL_SETTINGS_FILE, CreationCollisionOption.OpenIfExists);

                // Serialize the Json
                string json = JsonConvert.SerializeObject(m_localSettings);

                // Write to the file
                await Windows.Storage.FileIO.WriteTextAsync(file, json);
            }
            catch (Exception ex)
            {
                m_baconMan.MessageMan.DebugDia("Failed to write settings", ex);
            }      
        }
    }
}
