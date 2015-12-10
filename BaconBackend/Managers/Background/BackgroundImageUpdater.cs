using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using BaconBackend.Interfaces;
using Microsoft.ApplicationInsights.DataContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.UserProfile;

namespace BaconBackend.Managers.Background
{
    public class BackgroundImageUpdater
    {
        /// <summary>
        /// Indicates which update a type should do.
        /// </summary>
        private enum UpdateTypes
        {
            Both,
            LockScreen,
            Desktop
        }

        ///
        /// Const Vars
        ///
        private const int c_maxImageCacheCount = 5;
        private const string c_imageLockScreenCacheFolderPath = "LockScreenImageCache";
        private const string c_imageDesktopCacheFolderPath = "DesktopImageCache";

        ///
        /// Private
        ///
        private BaconManager m_baconMan;
        private StorageFolder m_lockScreenImageCacheFolder = null;
        private StorageFolder m_desktopImageCacheFolder = null;
        private bool m_isRunning = false;

        private RefCountedDeferral m_lockScreenRefDeferral = null;
        private RefCountedDeferral m_desktopRefDeferral = null;

        public BackgroundImageUpdater(BaconManager baconMan)
        {
            m_baconMan = baconMan;
        }

        /// <summary>
        /// Kicks off an update of the images.
        /// </summary>
        /// <param name="force"></param>
        public async Task RunUpdate(RefCountedDeferral refDeferral, bool force = false)
        {
            if ((IsDeskopEnabled || IsLockScreenEnabled) && UserProfilePersonalizationSettings.IsSupported())
            {
                TimeSpan timeSinceLastRun = DateTime.Now - LastImageUpdate;
                if(timeSinceLastRun.TotalMinutes > UpdateFrquency || force)
                {
                    // Make sure we aren't running
                    lock(this)
                    {
                        if(m_isRunning)
                        {
                            return;
                        }
                        m_isRunning = true;
                    }

                    // Do the updates
                    await KickOffUpdate(force, refDeferral);
                }
            }
        }

        /// <summary>
        /// Actually does the update
        /// </summary>
        private async Task KickOffUpdate(bool force, RefCountedDeferral refDeferral)
        {
            m_baconMan.TelemetryMan.ReportLog(this, "Updater updating.");
            try
            {
                // Figure out if we need to do a full update
                int fullUpdateFequencyMins = UpdateFrquency * c_maxImageCacheCount;
                TimeSpan timeSinceLastFullUpdate = DateTime.Now - LastFullUpdate;

                if (timeSinceLastFullUpdate.TotalMinutes > fullUpdateFequencyMins || force)
                {
                    m_baconMan.TelemetryMan.ReportLog(this, "Running full update");

                    // Update lock screen images
                    if (IsLockScreenEnabled)
                    {
                        m_baconMan.TelemetryMan.ReportLog(this, "Updating lock screen", SeverityLevel.Verbose);

                        // Make a deferral scope object so we can do our work without being killed.
                        // Note! We need release this object or it will hang the app around forever!
                        m_lockScreenRefDeferral = refDeferral;
                        m_lockScreenRefDeferral.AddRef();

                        // Kick off the update, this will happen async.
                        GetSubredditStories(LockScreenSubredditName, UpdateTypes.LockScreen);
                    }

                    if(IsDeskopEnabled)
                    {
                        m_baconMan.TelemetryMan.ReportLog(this, "Updating lock screen", SeverityLevel.Verbose); 
                                        
                        // Shortcut: If lock screen in enabled and it is the same subreddit just share the same cache. If not,
                        // get the desktop images
                        if (IsLockScreenEnabled && LockScreenSubredditName.Equals(DesktopSubredditName))
                        {
                            m_baconMan.TelemetryMan.ReportLog(this, "Desktop same sub as lockscreen, skipping image update.", SeverityLevel.Verbose);
                        }
                        else
                        {
                            m_baconMan.TelemetryMan.ReportLog(this, "Updating desktop image", SeverityLevel.Verbose);

                            // Make a deferral scope object so we can do our work without being killed.
                            // Note! We need release this object or it will hang the app around forever!
                            m_desktopRefDeferral = refDeferral;
                            m_desktopRefDeferral.AddRef();

                            // Kick off the update, this will happen async.
                            GetSubredditStories(DesktopSubredditName, UpdateTypes.Desktop);                           
                        }
                    }
                }
                // Else full update
                else
                {
                    m_baconMan.TelemetryMan.ReportLog(this, "No need for a full update, just rotating.");

                    // If we aren't doing a full update just rotate the images.
                    await DoImageRotation(UpdateTypes.Both);

                    // Stop the running state
                    lock (this)
                    {
                        m_isRunning = false;
                    }
                }
            }
            catch(Exception e)
            {
                m_baconMan.MessageMan.DebugDia("Failed to set background image", e);
                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "Failed to set background image", e);
            }
        }

        #region Image Rotation

        /// <summary>
        /// Assuming there are images, this does the rotation of the lock screen images.
        /// </summary>
        /// <returns></returns>
        private async Task DoImageRotation(UpdateTypes type)
        {
            try
            {
                bool wasSuccess = false;
                if(type == UpdateTypes.LockScreen)
                {
                    wasSuccess = await DoSingleImageRotation(UpdateTypes.LockScreen);
                }
                else if(type == UpdateTypes.Desktop)
                {
                    wasSuccess = await DoSingleImageRotation(UpdateTypes.Desktop);
                }
                else
                {
                    bool firstSuccess = await DoSingleImageRotation(UpdateTypes.LockScreen);
                    bool secondSuccess = await DoSingleImageRotation(UpdateTypes.Desktop);
                    wasSuccess = firstSuccess && secondSuccess;
                }      
                
                // If we successfully updated set the time.
                if(wasSuccess)
                {
                    LastImageUpdate = DateTime.Now;
                }       
            }
            catch (Exception e)
            {
                m_baconMan.MessageMan.DebugDia("Failed to set background image", e);
                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "Failed to set background image", e);
            }
        }

        /// <summary>
        /// Rotate a single image type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private async Task<bool> DoSingleImageRotation(UpdateTypes type)
        {
            // Make sure we should do the update.
            if ((IsLockScreenEnabled && type == UpdateTypes.LockScreen) || (IsDeskopEnabled && type == UpdateTypes.Desktop))
            {
                // If the lock screen and desktop are the same subreddit use the same files.
                UpdateTypes fileCacheType = type;    
                // If this is a desktop update, and lock is enabled, and they are the same subreddit...
                if (type == UpdateTypes.Desktop && IsLockScreenEnabled && LockScreenSubredditName.Equals(DesktopSubredditName))
                {
                    // If they are all the same use the lock screen cache for the desktop.
                    fileCacheType = UpdateTypes.LockScreen;
                }

                // Get the current files..
                IReadOnlyList<StorageFile> files = await GetCurrentCacheImages(fileCacheType);

                m_baconMan.TelemetryMan.ReportLog(this, "Current images in cache :" + files.Count);

                if (files != null && files.Count != 0)
                {
                    int currentIndex = 0;
                    if(type == UpdateTypes.LockScreen)
                    {
                        // Update the index
                        CurrentLockScreenRotationIndex++;
                        if (CurrentLockScreenRotationIndex > files.Count)
                        {
                            CurrentLockScreenRotationIndex = 0;
                        }
                        currentIndex = CurrentLockScreenRotationIndex;
                    }
                    else
                    {
                        // Update the index
                        CurrentDesktopRotationIndex++;
                        if (CurrentDesktopRotationIndex > files.Count)
                        {
                            CurrentDesktopRotationIndex = 0;
                        }
                        currentIndex = CurrentDesktopRotationIndex;
                    }

                    m_baconMan.TelemetryMan.ReportLog(this, "Current index used to set image :" + currentIndex);

                    // Set the image
                    bool wasSuccess = await SetBackgroundImage(type, files[currentIndex]);

                    if (!wasSuccess)
                    {
                        m_baconMan.TelemetryMan.ReportLog(this, "Image update failed", SeverityLevel.Error);
                        m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, type == UpdateTypes.LockScreen ? "LockscreenImageUpdateFailed" : "DesktopImageUpdateFailed");
                    }
                    else
                    {
                        m_baconMan.TelemetryMan.ReportLog(this, "Image update success");
                    }

                    return wasSuccess;
                }
            }
            return false;
        }

        #endregion

        #region Get Posts

        /// <summary>
        /// This will kick off the process of getting the stories for a subreddit.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private void GetSubredditStories(string name, UpdateTypes type)
        {
            // Create a fake subreddit, the ID needs to be unique
            Subreddit subreddit = new Subreddit() { Id = DateTime.Now.Ticks.ToString(), DisplayName = name };

            // Get the collector for the subreddit
            SubredditCollector collector = SubredditCollector.GetCollector(subreddit, m_baconMan);

            // Sub to the collector callback
            if(type == UpdateTypes.LockScreen)
            {
                collector.OnCollectionUpdated += Collector_OnCollectionUpdatedLockScreen;
                collector.OnCollectorStateChange += Collector_OnCollectorStateChangeLockScreen;
            }
            else
            {
                collector.OnCollectionUpdated += Collector_OnCollectionUpdatedDesktop;
                collector.OnCollectorStateChange += Collector_OnCollectorStateChangeDesktop;
            }

            // Kick off the update
            collector.Update(true);
        }

        /// <summary>
        /// Fired when the collector state changed for the lock screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectorStateChangeLockScreen(object sender, OnCollectorStateChangeArgs e)
        {
            Collector_OnCollectorStateChange(UpdateTypes.LockScreen, e);
        }

        /// <summary>
        /// Fired when the collector state changed for the lock screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectorStateChangeDesktop(object sender, OnCollectorStateChangeArgs e)
        {
            Collector_OnCollectorStateChange(UpdateTypes.Desktop, e);
        }

        /// <summary>
        /// Fired when the collector state changed for the either
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectorStateChange(UpdateTypes type, OnCollectorStateChangeArgs e)
        {
            if(e.State == CollectorState.Error)
            {
                // We had an error. This is the end of the line, kill the deferral
                ReleaseDeferral(type);

                // And try to set isRunning
                UnSetIsRunningIfDone();
            }
        }

        /// <summary>
        /// Fired when the stories come in for a type
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectionUpdatedLockScreen(object sender, OnCollectionUpdatedArgs<Post> e)
        {
            Collector_OnCollectionUpdated(UpdateTypes.LockScreen, e);
        }

        /// <summary>
        /// Fired when the stories come in for a type
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectionUpdatedDesktop(object sender, OnCollectionUpdatedArgs<Post> e)
        {
            Collector_OnCollectionUpdated(UpdateTypes.Desktop, e);
        }

        /// <summary>
        /// Fired when the stories come in for a type
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectionUpdated(UpdateTypes type, OnCollectionUpdatedArgs<Post> e)
        {
            if(e.ChangedItems.Count > 0)
            {
                // If we successfully got the post now get the images
                GetImagesFromPosts(e.ChangedItems, type);
            }
        }

        #endregion

        #region Get Images

        /// <summary>
        /// Used to lock the get images process
        /// </summary>
        object m_getImageLock = new object();

        int m_desktopRequestCount = 0;
        int m_desktopDoneCount = 0;
        int m_lockScreenRequestCount = 0;
        int m_lockScreenDoneCount = 0;

        /// <summary>
        /// Given a list of post this gets the images and puts them into file cache.
        /// </summary>
        /// <param name="posts"></param>
        /// <param name="isLockScreen"></param>
        /// <returns></returns>
        private async void GetImagesFromPosts(List<Post> posts, UpdateTypes type)
        {
            // First, remove all of the existing images
            try
            {
                await DeleteAllCacheImages(type);
            }
            catch(Exception e)
            {
                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToDeleteCacheImages", e);
            }

            // Setup the vars
            if(type == UpdateTypes.LockScreen)
            {
                m_lockScreenRequestCount = 0;
                m_lockScreenDoneCount = 0;
            }
            else
            {
                m_desktopRequestCount = 0;
                m_desktopDoneCount = 0;
            }

            // Figure out all of the images we need to request.
            List<Tuple<string, string>> imageRequestList = new List<Tuple<string, string>>();
            foreach (Post post in posts)
            {
                string imageUrl = ImageManager.GetImageUrl(post.Url);
                if (!String.IsNullOrWhiteSpace(imageUrl))
                {
                    imageRequestList.Add(new Tuple<string, string>(imageUrl, post.Id));
                }

                if(imageRequestList.Count > c_maxImageCacheCount)
                {
                    break;
                }
            }

            // Now set our request counts before we start requesting. We must do this to ensure
            // the numbers are correct when the request come back.
            if(type == UpdateTypes.LockScreen)
            {
                m_lockScreenRequestCount = imageRequestList.Count;
            }
            else
            {
                m_desktopRequestCount = imageRequestList.Count;
            }

            // Now make all of the request.
            foreach(Tuple<string, string> request in imageRequestList)
            {
                // Make the request
                ImageManager.ImageManagerRequest imageRequst = new ImageManager.ImageManagerRequest()
                {
                    Url = request.Item1,
                    ImageId = request.Item2,
                    Context = type
                };
                imageRequst.OnRequestComplete += OnRequestComplete;
                m_baconMan.ImageMan.QueueImageRequest(imageRequst);
            }  

            // If we have nothing to request this is the end of the line for this type.
            if(imageRequestList.Count == 0)
            {
                // And kill the deferral
                ReleaseDeferral(type);

                // And set us to stopped.
                UnSetIsRunningIfDone();
            }
        }

        /// <summary>
        /// Fired when an image request is done.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="response"></param>
        public async void OnRequestComplete(object sender, ImageManager.ImageManagerResponseEventArgs response)
        {
            // Remove event listener
            ImageManager.ImageManagerRequest request = (ImageManager.ImageManagerRequest)sender;
            request.OnRequestComplete -= OnRequestComplete;

            UpdateTypes type = (UpdateTypes)response.Request.Context;

            // Make sure we were successfully.
            if (response.Success)
            {
                try
                {
                    // Write the file
                    await WriteImageToFile(response.ImageStream, type);
                }
                catch (Exception e)
                {
                    m_baconMan.MessageMan.DebugDia("Failed to write background image", e);
                    m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "Failed to write background image", e);
                }
            }

            // Indicate that this image is done.
            bool isDone = false;
            lock (m_getImageLock)
            {                
                if(type == UpdateTypes.LockScreen)
                {
                    m_lockScreenDoneCount++;
                    isDone = m_lockScreenDoneCount >= m_lockScreenRequestCount;
                }
                else
                {
                    m_desktopDoneCount++;
                    isDone = m_desktopDoneCount >= m_desktopRequestCount;
                }
            }

            // if we are done done then tell the images to rotate and clean up.
            if(isDone)
            {
                // Set this high so we roll over.
                if(type == UpdateTypes.LockScreen)
                {
                    CurrentLockScreenRotationIndex = 99;
                }
                else
                {
                    CurrentDesktopRotationIndex = 99;
                }

                // Do the rotate
                await DoImageRotation(type);

                // If this is a lock screen update this might also be a desktop update. This happens when the lock screen and desktop
                // share the same subreddit, they share the same images.
                if (type == UpdateTypes.LockScreen && IsDeskopEnabled && LockScreenSubredditName.Equals(DesktopSubredditName))
                {
                    // Off set the two so they don't show the same image
                    CurrentDesktopRotationIndex = 1;

                    // We also need to update the desktop.
                    await DoImageRotation(UpdateTypes.Desktop);
                }

                // We are done, set the last update time
                LastFullUpdate = DateTime.Now;

                // And kill the deferral
                ReleaseDeferral(type);

                // And set us to stopped.
                UnSetIsRunningIfDone(); 
            }
        }

        private async Task DeleteAllCacheImages(UpdateTypes type)
        {
            StorageFolder localFolder = await GetImageCacheFolder(type);
            IReadOnlyList<StorageFile> files = await localFolder.GetFilesAsync();
            foreach (StorageFile file in files)
            {
                await file.DeleteAsync();
            }
        }

        private async Task WriteImageToFile(InMemoryRandomAccessStream stream, UpdateTypes type)
        {
            StorageFolder localFolder = await GetImageCacheFolder(type);
            StorageFile file = await localFolder.CreateFileAsync("cachedImage.jpg", CreationCollisionOption.GenerateUniqueName);

            using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await RandomAccessStream.CopyAndCloseAsync(stream.GetInputStreamAt(0), fileStream.GetOutputStreamAt(0));
            }
        }

        #endregion

        /// <summary>
        /// Turns off is running if both the desktop and locks screens are done updating.
        /// </summary>
        private void UnSetIsRunningIfDone()
        {
            lock(this)
            {
                if(m_lockScreenRefDeferral == null && m_desktopRefDeferral == null)
                {
                    m_isRunning = false;
                }
            }
        }

        /// <summary>
        /// Release the deferral for a type if it is held
        /// </summary>
        /// <param name="type"></param>
        private void ReleaseDeferral(UpdateTypes type)
        {
            lock(this)
            {
                if (type == UpdateTypes.LockScreen)
                {
                    if (m_lockScreenRefDeferral != null)
                    {
                        m_lockScreenRefDeferral.ReleaseRef();
                    }
                    m_lockScreenRefDeferral = null;
                }
                else
                {
                    if (m_desktopRefDeferral != null)
                    {
                        m_desktopRefDeferral.ReleaseRef();
                    }
                    m_desktopRefDeferral = null;
                }
            }  
        }

        /// <summary>
        /// Returns the current contents of the cache folder
        /// </summary>
        /// <returns></returns>
        private async Task<IReadOnlyList<StorageFile>> GetCurrentCacheImages(UpdateTypes type)
        {
            StorageFolder localFolder = await GetImageCacheFolder(type);
            return await localFolder.GetFilesAsync();
        }

        /// <summary>
        /// Returns the image cache folder for desktop images
        /// </summary>
        /// <returns></returns>
        private async Task<StorageFolder> GetImageCacheFolder(UpdateTypes type)
        {
            if(type == UpdateTypes.LockScreen)
            {
                if (m_lockScreenImageCacheFolder == null)
                {
                    m_lockScreenImageCacheFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(c_imageLockScreenCacheFolderPath, CreationCollisionOption.OpenIfExists);
                }
                return m_lockScreenImageCacheFolder;
            }
            else
            {
                if (m_desktopImageCacheFolder == null)
                {
                    m_desktopImageCacheFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(c_imageDesktopCacheFolderPath, CreationCollisionOption.OpenIfExists);
                }
                return m_desktopImageCacheFolder;
            }
        }

        /// <summary>
        /// Returns the current contents of the cache folder
        /// </summary>
        /// <returns></returns>
        private async Task<bool> SetBackgroundImage(UpdateTypes type, StorageFile file)
        {
            bool wasSuccess = false;
            // Make sure we can do it
            if (UserProfilePersonalizationSettings.IsSupported())
            {
                // Try to set the image
                UserProfilePersonalizationSettings profileSettings = UserProfilePersonalizationSettings.Current;
                if (type == UpdateTypes.LockScreen)
                {
                    wasSuccess = await profileSettings.TrySetLockScreenImageAsync(file);
                }
                else
                {
                    wasSuccess = await profileSettings.TrySetWallpaperImageAsync(file);
                }
            }
            return wasSuccess;
        }

        #region Vars

        /// <summary>
        /// Indicates if lock screen image update is enabled
        /// </summary>
        public bool IsLockScreenEnabled
        {
            get
            {
                if (!m_isLockScreenEnabled.HasValue)
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.IsLockScreenEnabled"))
                    {
                        m_isLockScreenEnabled = m_baconMan.SettingsMan.ReadFromLocalSettings<bool>("BackgroundImageUpdater.IsLockScreenEnabled");
                    }
                    else
                    {
                        m_isLockScreenEnabled = false;
                    }
                }
                return m_isLockScreenEnabled.Value;
            }
            set
            {
                m_isLockScreenEnabled = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<bool>("BackgroundImageUpdater.IsLockScreenEnabled", m_isLockScreenEnabled.Value);
            }
        }
        private bool? m_isLockScreenEnabled = null;

        /// <summary>
        /// Indicates if desktop wallpaper update is enabled
        /// </summary>
        public bool IsDeskopEnabled
        {
            get
            {
                if (!m_isDeskopEnabled.HasValue)
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.IsDeskopEnabled"))
                    {
                        m_isDeskopEnabled = m_baconMan.SettingsMan.ReadFromLocalSettings<bool>("BackgroundImageUpdater.IsDeskopEnabled");
                    }
                    else
                    {
                        m_isDeskopEnabled = false;
                    }
                }
                return m_isDeskopEnabled.Value;
            }
            set
            {
                m_isDeskopEnabled = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<bool>("BackgroundImageUpdater.IsDeskopEnabled", m_isDeskopEnabled.Value);
            }
        }
        private bool? m_isDeskopEnabled = null;

        /// <summary>
        /// Indicates which image in the cache we are on for lock screen
        /// </summary>
        private int CurrentLockScreenRotationIndex
        {
            get
            {
                if (!m_currentLockScreenRotationIndex.HasValue)
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.CurrentLockScreenRotationIndex"))
                    {
                        m_currentLockScreenRotationIndex = m_baconMan.SettingsMan.ReadFromLocalSettings<int>("BackgroundImageUpdater.CurrentLockScreenRotationIndex");
                    }
                    else
                    {
                        m_currentLockScreenRotationIndex = 0;
                    }
                }
                return m_currentLockScreenRotationIndex.Value;
            }
            set
            {
                m_currentLockScreenRotationIndex = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<int>("BackgroundImageUpdater.CurrentLockScreenRotationIndex", m_currentLockScreenRotationIndex.Value);
            }
        }
        private int? m_currentLockScreenRotationIndex = null;

        /// <summary>
        /// Indicates which image in the cache we are on for desktop
        /// </summary>
        private int CurrentDesktopRotationIndex
        {
            get
            {
                if (!m_currentDesktopRotationIndex.HasValue)
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.CurrentDesktopRotationIndex"))
                    {
                        m_currentDesktopRotationIndex = m_baconMan.SettingsMan.ReadFromLocalSettings<int>("BackgroundImageUpdater.CurrentDesktopRotationIndex");
                    }
                    else
                    {
                        m_currentDesktopRotationIndex = 0;
                    }
                }
                return m_currentDesktopRotationIndex.Value;
            }
            set
            {
                m_currentDesktopRotationIndex = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<int>("BackgroundImageUpdater.CurrentDesktopRotationIndex", m_currentDesktopRotationIndex.Value);
            }
        }
        private int? m_currentDesktopRotationIndex = null;

        /// <summary>
        /// Indicates which image in the cache we are on
        /// </summary>
        public int UpdateFrquency
        {
            get
            {
                if (!m_updateFrquency.HasValue)
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.UpdateFrquency"))
                    {
                        m_updateFrquency = m_baconMan.SettingsMan.ReadFromLocalSettings<int>("BackgroundImageUpdater.UpdateFrquency");
                    }
                    else
                    {
                        m_updateFrquency = 120;
                    }
                }
                return m_updateFrquency.Value;
            }
            set
            {
                m_updateFrquency = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<int>("BackgroundImageUpdater.UpdateFrquency", m_updateFrquency.Value);
            }
        }
        private int? m_updateFrquency = null;

        /// <summary>
        /// The last time the background image was rotated
        /// </summary>
        public DateTime LastImageUpdate
        {
            get
            {
                if (m_lastImageUpdate.Equals(new DateTime(0)))
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.LastImageUpdate"))
                    {
                        m_lastImageUpdate = m_baconMan.SettingsMan.ReadFromLocalSettings<DateTime>("BackgroundImageUpdater.LastImageUpdate");
                    }
                }
                return m_lastImageUpdate;
            }
            private set
            {
                m_lastImageUpdate = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<DateTime>("BackgroundImageUpdater.LastImageUpdate", m_lastImageUpdate);
            }
        }
        private DateTime m_lastImageUpdate = new DateTime(0);

        /// <summary>
        /// The last time the background images where updated
        /// </summary>
        public DateTime LastFullUpdate
        {
            get
            {
                if (m_lastFullUpdate.Equals(new DateTime(0)))
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.LastFullUpdate"))
                    {
                        m_lastFullUpdate = m_baconMan.SettingsMan.ReadFromLocalSettings<DateTime>("BackgroundImageUpdater.LastFullUpdate");
                    }
                }
                return m_lastFullUpdate;
            }
            private set
            {
                m_lastFullUpdate = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<DateTime>("BackgroundImageUpdater.LastFullUpdate", m_lastFullUpdate);
            }
        }
        private DateTime m_lastFullUpdate = new DateTime(0);

        /// <summary>
        /// Indicates which subreddit to update from. Note we only grab the URL here
        /// because we don't want to write a subreddit object across roaming settings.
        /// </summary>
        public string LockScreenSubredditName
        {
            get
            {
                if (m_lockScreenSubredditUrl == null)
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.LockScreenSubredditUrl"))
                    {
                        m_lockScreenSubredditUrl = m_baconMan.SettingsMan.ReadFromLocalSettings<string>("BackgroundImageUpdater.LockScreenSubredditUrl");
                    }
                    else
                    {
                        // Default to a nice earth image subreddit, it is not what it sounds like.
                        m_lockScreenSubredditUrl = "earthporn";
                    }
                }
                return m_lockScreenSubredditUrl;
            }
            set
            {
                m_lockScreenSubredditUrl = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<string>("BackgroundImageUpdater.LockScreenSubredditUrl", m_lockScreenSubredditUrl);
            }
        }
        private string m_lockScreenSubredditUrl = null;

        /// <summary>
        /// Indicates which subreddit to update from. Note we only grab the URL here
        /// because we don't want to write a subreddit object across roaming settings.
        /// </summary>
        public string DesktopSubredditName
        {
            get
            {
                if (m_desktopSubredditUrl == null)
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.DesktopSubredditUrl"))
                    {
                        m_desktopSubredditUrl = m_baconMan.SettingsMan.ReadFromLocalSettings<string>("BackgroundImageUpdater.DesktopSubredditUrl");
                    }
                    else
                    {
                        // Default to a nice earth image subreddit, it is not what it sounds like.
                        m_desktopSubredditUrl = "earthporn";
                    }
                }
                return m_desktopSubredditUrl;
            }
            set
            {
                m_desktopSubredditUrl = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<string>("BackgroundImageUpdater.DesktopSubredditUrl", m_desktopSubredditUrl);
            }
        }
        private string m_desktopSubredditUrl = null;

        #endregion
    }
}
