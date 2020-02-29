using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;
using Newtonsoft.Json;
using System.Threading;
using System.IO;

namespace BaconBackend.Managers
{
    public class SettingsManager
    {
        private const string LocalSettingsFile = "LocalSettings.data";
        private readonly object _objectLock = new object();
        private readonly BaconManager _baconMan;
        private readonly ManualResetEvent _localSettingsReady = new ManualResetEvent(false);

        public SettingsManager(BaconManager baconMan)
        {
            _baconMan = baconMan;

            // Setup the local settings.
            InitLocalSettings();

            // We can't do this from a background task
            if (!baconMan.IsBackgroundTask)
            {
                // Register for resuming callbacks.
                _baconMan.OnResuming += BaconMan_OnResuming;
            }
        }

        /// <summary>
        /// Gets and instance of the roaming settings
        /// </summary>
        public IPropertySet RoamingSettings
        {
            get
            {
                lock(_objectLock)
                {
                    if (_roamingSettings != null) return _roamingSettings;

                    if(_roamingSettings == null)
                    {
                        _roamingSettings = ApplicationData.Current.RoamingSettings.Values;
                    }
                }
                return _roamingSettings;
            }
        }
        private IPropertySet _roamingSettings;

        /// <summary>
        /// Returns an instance of the local settings
        /// </summary>
        public Dictionary<string, object> LocalSettings
        {
            get
            {
                // Since opening a file is async, we need to make sure it has been opened before
                _localSettingsReady.WaitOne();
                return _localSettings;
            }
        }
        private Dictionary<string, object> _localSettings;

        /// <summary>
        /// Helper that writes objects to the local storage
        /// </summary>
        /// <param name="name"></param>
        /// <param name="obj"></param>
        public void WriteToLocalSettings<T>(string name, T obj)
        {
            try
            {
                LocalSettings[name] = JsonConvert.SerializeObject(obj);
            }
            catch(Exception e)
            {
                _baconMan.MessageMan.DebugDia("failed to write setting " + name, e);
                TelemetryManager.ReportUnexpectedEvent(this, "failedToWriteSetting" + name, e);
            }            
        }

        /// <summary>
        /// Helper that reads objects from the local storage
        /// Useful for things WinRt can't serialize.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public T ReadFromLocalSettings<T>(string name)
        {
            var content = (string)LocalSettings[name];
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
            var content = (string)RoamingSettings[name];
            return JsonConvert.DeserializeObject<T>(content);
        }

        private async void InitLocalSettings()
        {
            try
            {
                // Get the file.
                var folder = ApplicationData.Current.LocalFolder;
                var file = await folder.CreateFileAsync(LocalSettingsFile, CreationCollisionOption.OpenIfExists);

                // Check the file size
                var fileProps = await file.GetBasicPropertiesAsync();
                if(fileProps.Size > 0)
                {
                    // Get the input stream and json reader.
                    // NOTE!! We are really careful not to use a string here so we don't have to allocate a huge string.
                    IInputStream inputStream = await file.OpenReadAsync();
                    using (var reader = new StreamReader(inputStream.AsStreamForRead()))
                    using (JsonReader jsonReader = new JsonTextReader(reader))
                    {
                        // Parse the settings file into the dictionary.
                        var serializer = new JsonSerializer();
                        _localSettings = await Task.Run(() => serializer.Deserialize<Dictionary<string, object>>(jsonReader));
                    }
                }
                else
                {
                    // The file is empty, just make a new dictionary.
                    _localSettings = new Dictionary<string, object>();
                }
            }
            catch(Exception e)
            {
                _baconMan.MessageMan.DebugDia("Unable to load settings file! "+e.Message);
                _localSettings = new Dictionary<string, object>();
            }

            // Signal we are ready
            _localSettingsReady.Set();
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
                var folder = ApplicationData.Current.LocalFolder;
                var file = await folder.CreateFileAsync(LocalSettingsFile, CreationCollisionOption.OpenIfExists);

                // Serialize the Json
                var json = JsonConvert.SerializeObject(_localSettings);

                // Write to the file
                await FileIO.WriteTextAsync(file, json);
            }
            catch (Exception ex)
            {
                _baconMan.MessageMan.DebugDia("Failed to write settings", ex);
            }      
        }
    }
}
