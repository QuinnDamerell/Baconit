using BaconBackend.Collectors;
using BaconBackend.DataObjects;
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

        //
        // Used for the updater. It really sucks but due to they way we block the thread for background
        // updating we can't use lambdas due to the weak events, so some of the work must be done with
        // events to sync everything.
        //
        bool m_isRunning = false;
        AutoResetEvent m_autoReset = new AutoResetEvent(false);

        public BackgroundImageUpdater(BaconManager baconMan)
        {
            m_baconMan = baconMan;
        }

        /// <summary>
        /// THREAD BLOCKING: Kicks off an update of the images, this will block the current thread.
        /// </summary>
        /// <param name="force"></param>
        public bool RunUpdate(bool force = false)
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
                            return false;
                        }
                        m_isRunning = true;
                    }

                    // Do the updates
                    bool success = ActuallyDoTheUpdate(force);
                    if (success)
                    {
                        // Set the new update time
                        LastImageUpdate = DateTime.Now;
                    }
                    return success;
                }
            }
            return true;
        }

        /// <summary>
        /// THREAD BLOCKING: Actually does the update, this will block the current thread.
        /// </summary>
        private bool ActuallyDoTheUpdate(bool force)
        {
            m_baconMan.TelemetryMan.ReportLog(this, "Updater updating.");
            bool wasSuccessfull = true;
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

                        // We are doing a full update, grab new stories for lock screen
                        List<Post> posts = GetSubredditStories(LockScreenSubredditName);

                        // Check we successfully got new posts
                        if (posts != null)
                        {
                            m_baconMan.TelemetryMan.ReportLog(this, "Lock screen post retrieved, getting images", SeverityLevel.Verbose);

                            // If so, get the images for the posts
                            if (GetImagesFromPosts(posts, true))
                            {
                                LastFullUpdate = DateTime.Now;
                                // Set the index high so it will roll over.
                                CurrentLockScreenRotationIndex = 99;
                            }
                            else
                            {
                                m_baconMan.TelemetryMan.ReportLog(this, "Updating lock screen update failed, we failed to get images", SeverityLevel.Error);
                                wasSuccessfull = false;
                            }
                        }
                        else
                        {
                            m_baconMan.TelemetryMan.ReportLog(this, "Updating lock screen update failed, we have no posts", SeverityLevel.Error);
                            wasSuccessfull = false;
                        }
                    }

                    if(IsDeskopEnabled)
                    {
                        m_baconMan.TelemetryMan.ReportLog(this, "Updating lock screen", SeverityLevel.Verbose);
                        // Shortcut: If lock screen in enabled and it is the same subreddit just share the same cache. If not,
                        // get the desktop images
                        if (IsLockScreenEnabled && LockScreenSubredditName.Equals(DesktopSubredditName))
                        {
                            // Set the desktop rotation to 1 so we offset lock screen
                            CurrentDesktopRotationIndex = 1;
                            m_baconMan.TelemetryMan.ReportLog(this, "Desktop same sub as lockscreen, skipping image update.", SeverityLevel.Verbose);
                        }
                        else
                        {
                            m_baconMan.TelemetryMan.ReportLog(this, "Updating desktop image", SeverityLevel.Verbose);

                            // We are doing a full update, grab new stories for desktop
                            List<Post> posts = GetSubredditStories(DesktopSubredditName);

                            // Check we successfully got new posts
                            if (posts != null)
                            {
                                m_baconMan.TelemetryMan.ReportLog(this, "Desktop posts retrieved, getting images", SeverityLevel.Verbose);

                                // If so, get the images for the posts
                                if (GetImagesFromPosts(posts, false))
                                {
                                    // Set the index high so it will roll over.
                                    LastFullUpdate = DateTime.Now;
                                    CurrentDesktopRotationIndex = 99;
                                }
                                else
                                {
                                    m_baconMan.TelemetryMan.ReportLog(this, "Updating desktop image failed, we failed to get images", SeverityLevel.Error);
                                    wasSuccessfull = false;
                                }
                            }
                            else
                            {
                                m_baconMan.TelemetryMan.ReportLog(this, "Updating desktop image failed, we failed to get posts", SeverityLevel.Error);
                                wasSuccessfull = false;
                            }
                        }
                    }
                }

                m_baconMan.TelemetryMan.ReportLog(this, "Image gathering done or skipped, moving on to setting.");

                // Now update the images
                using (AutoResetEvent aEvent = new AutoResetEvent(false))
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            // Update the lock screen image
                            IReadOnlyList<StorageFile> lockScreenFiles = null;
                            if (IsLockScreenEnabled)
                            {
                                lockScreenFiles = await GetCurrentCacheImages(true);

                                m_baconMan.TelemetryMan.ReportLog(this, "Current lockscreen images in cache :"+lockScreenFiles.Count);

                                if (lockScreenFiles.Count != 0)
                                {
                                    // Update the index
                                    CurrentLockScreenRotationIndex++;
                                    if (CurrentLockScreenRotationIndex > lockScreenFiles.Count)
                                    {
                                        CurrentLockScreenRotationIndex = 0;
                                    }

                                    m_baconMan.TelemetryMan.ReportLog(this, "Current lockscreen index used to set image :" + CurrentLockScreenRotationIndex);

                                    // Set the image
                                    bool localResult = await SetBackgroundImage(true, lockScreenFiles[CurrentLockScreenRotationIndex]);

                                    if (!localResult)
                                    {
                                        m_baconMan.TelemetryMan.ReportLog(this, "Lockscreen image update failed", SeverityLevel.Error);
                                        m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "LockscreenImageUpdateFailed");
                                    }
                                    else
                                    {
                                        m_baconMan.TelemetryMan.ReportLog(this, "Lockscreen image update success");
                                    }

                                    // Only set the result if we were already successful
                                    if(wasSuccessfull)
                                    {
                                        wasSuccessfull = localResult;
                                    }
                                }
                                else
                                {
                                    wasSuccessfull = false;
                                }
                            }

                            // Update the desktop
                            if (IsDeskopEnabled)
                            {
                                // If the lock screen and desktop are the same subreddit use the same files.
                                IReadOnlyList<StorageFile> desktopFiles;
                                if (IsLockScreenEnabled && LockScreenSubredditName.Equals(DesktopSubredditName))
                                {
                                    desktopFiles = lockScreenFiles;
                                }
                                else
                                {
                                    desktopFiles = await GetCurrentCacheImages(false);
                                }

                                m_baconMan.TelemetryMan.ReportLog(this, "Current desktop images in cache :" + lockScreenFiles.Count);

                                if (desktopFiles != null && desktopFiles.Count != 0)
                                {
                                    // Update the index
                                    CurrentDesktopRotationIndex++;
                                    if (CurrentDesktopRotationIndex > desktopFiles.Count)
                                    {
                                        CurrentDesktopRotationIndex = 0;
                                    }

                                    m_baconMan.TelemetryMan.ReportLog(this, "Current desktop index used to set image :" + CurrentDesktopRotationIndex);

                                    // Set the image
                                    bool localResult = await SetBackgroundImage(false, desktopFiles[CurrentDesktopRotationIndex]);

                                    if (!localResult)
                                    {
                                        m_baconMan.TelemetryMan.ReportLog(this, "Desktop image update failed", SeverityLevel.Error);
                                        m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "DesktopImageUpdateFailed");
                                    }
                                    else
                                    {
                                        m_baconMan.TelemetryMan.ReportLog(this, "Desktop image update success");
                                    }

                                    // Only set the result if we were already successful
                                    if (wasSuccessfull)
                                    {
                                        wasSuccessfull = localResult;
                                    }
                                }
                                else
                                {
                                    wasSuccessfull = false;
                                }
                            }
                        }
                        catch(Exception e)
                        {
                            m_baconMan.MessageMan.DebugDia("Failed to set background image", e);
                            m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "Failed to set background image",e);
                            wasSuccessfull = false;
                        }

                        // Set that we are done!
                        aEvent.Set();
                    });
                    // Wait for the image setting to finish
                    aEvent.WaitOne();
                }
            }
            catch(Exception e)
            {
                m_baconMan.MessageMan.DebugDia("Failed to set background image", e);
                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "Failed to set background image", e);
                wasSuccessfull = false;
            }

            // Stop the running state
            lock(this)
            {
                m_isRunning = false;
            }

            return wasSuccessfull;
        }

        #region Get Posts

        // Note: This is horrible but we can't really do any better because we can't use lambdas.
        // These are protected by a lock, so the function should never be called more than once
        // at the same time.
        List<Post> m_currentSubredditPosts = null;

        /// <summary>
        /// THREAD BLOCKING this function will get post from a subreddit while blocking
        /// the calling thread.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private List<Post> GetSubredditStories(string name)
        {
            // Create a fake subreddit, the ID needs to be unique
            Subreddit subreddit = new Subreddit() { Id = DateTime.Now.Ticks.ToString(), DisplayName = name };

            // Get the collector for the subreddit
            SubredditCollector collector = SubredditCollector.GetCollector(subreddit, m_baconMan);

            // Sub to the collector callback
            collector.OnCollectionUpdated += Collector_OnCollectionUpdated;

            // Reset the event
            m_autoReset.Reset();
            m_currentSubredditPosts = null;

            // Kick off the update
            collector.Update(true);

            // Block until the posts are done or until 10 seconds passes
            m_autoReset.WaitOne(10000);

            return m_currentSubredditPosts;
        }

        /// <summary>
        /// Used by the get subreddit stories to return posts.
        /// We can't use a lambda because the weak events don't support it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectionUpdated(object sender, OnCollectionUpdatedArgs<Post> e)
        {
            m_currentSubredditPosts = e.ChangedItems;
            m_autoReset.Set();
        }

        #endregion

        #region Get Images

        // Note: This is horrible but we can't really do any better because we can't use lambdas.
        // These are protected by a lock, so the function should never be called more than once
        // at the same time.
        int m_reqeustCount = 0;
        int m_imageReturnCount = 0;
        int m_imageDoneCount = 0;
        bool m_isCurrentImageRequestLockScreen = false;
        object m_getImageLock = new object();

        /// <summary>
        /// THREAD BLOCKING Given a list of post this gets the images and puts them into file cache.
        /// </summary>
        /// <param name="posts"></param>
        /// <param name="isLockScreen"></param>
        /// <returns></returns>
        private bool GetImagesFromPosts(List<Post> posts, bool isLockScreen)
        {
            // First, remove all of the existing images
            using (AutoResetEvent aEvent = new AutoResetEvent(false))
            {
                Task.Run(async () => { await DeleteAllCacheImages(isLockScreen); aEvent.Set(); });
                aEvent.WaitOne();
            }

            // Reset the wait event
            m_autoReset.Reset();

            // Setup vars
            m_reqeustCount = 0;
            m_imageReturnCount = 0;
            m_imageDoneCount = 0;
            m_isCurrentImageRequestLockScreen = isLockScreen;

            // Make request for the images
            foreach (Post post in posts)
            {
                string imageUrl = ImageManager.GetImageUrl(post.Url);
                if (!String.IsNullOrWhiteSpace(imageUrl))
                {
                    // Make the request
                    ImageManager.ImageManagerRequest request = new ImageManager.ImageManagerRequest()
                    {
                        Url = imageUrl,
                        ImageId = post.Id
                    };
                    request.OnRequestComplete += OnRequestComplete;
                    m_baconMan.ImageMan.QueueImageRequest(request);
                    m_reqeustCount++;
                }

                if(m_reqeustCount > c_maxImageCacheCount)
                {
                    break;
                }
            }

            // Wait for all of the images to be downloaded and stored.
            m_autoReset.WaitOne(10000);

            // See if we got all of the requests, then we are successful.
            return m_imageDoneCount >= (c_maxImageCacheCount - 2);
        }

        public async void OnRequestComplete(object sender, ImageManager.ImageManagerResponseEventArgs response)
        {
            // Remove event
            ImageManager.ImageManagerRequest request = (ImageManager.ImageManagerRequest)sender;
            request.OnRequestComplete -= OnRequestComplete;

            // Make sure we were successfully.
            if (!response.Success)
            {
                return;
            }

            // Get a unique name for this image
            int imageCount = 0;
            lock(m_getImageLock)
            {
                imageCount = m_imageReturnCount;
                m_imageReturnCount++;
            }

            // Get the file name
            string imageFileName = imageCount + ".jpg";

            try
            {
                // Write the file
                await WriteImageToFile(response.ImageStream, imageFileName, m_isCurrentImageRequestLockScreen);
            }
            catch(Exception e)
            {
                m_baconMan.MessageMan.DebugDia("Failed to write background image", e);
                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "Failed to write background image", e);
            }

            // Add the file to the list
            lock (m_getImageLock)
            {
                // Report we are done
                m_imageDoneCount++;

                // Check to see if we were last
                if(m_imageDoneCount == c_maxImageCacheCount)
                {
                    m_autoReset.Set();
                }
            }
        }

        private async Task DeleteAllCacheImages(bool isLockScreen)
        {
            StorageFolder localFolder = await GetImageCacheFolder(isLockScreen);
            IReadOnlyList<StorageFile> files = await localFolder.GetFilesAsync();
            foreach (StorageFile file in files)
            {
                await file.DeleteAsync();
            }
        }

        private async Task WriteImageToFile(InMemoryRandomAccessStream stream, string fileName, bool isLockScreen)
        {
            StorageFolder localFolder = await GetImageCacheFolder(isLockScreen);
            StorageFile file = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await RandomAccessStream.CopyAndCloseAsync(stream.GetInputStreamAt(0), fileStream.GetOutputStreamAt(0));
            }
        }

        #endregion

        /// <summary>
        /// Returns the current contents of the cache folder
        /// </summary>
        /// <returns></returns>
        private async Task<IReadOnlyList<StorageFile>> GetCurrentCacheImages(bool isLockScreen)
        {
            StorageFolder localFolder = await GetImageCacheFolder(isLockScreen);
            return  await localFolder.GetFilesAsync();
        }

        /// <summary>
        /// Returns the image cache folder for desktop images
        /// </summary>
        /// <returns></returns>
        private async Task<StorageFolder> GetImageCacheFolder(bool isForLockScreen)
        {
            if(isForLockScreen)
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
        private async Task<bool> SetBackgroundImage(bool isLockScreen, StorageFile file)
        {
            bool wasSuccess = false;
            // Make sure we can do it
            if (UserProfilePersonalizationSettings.IsSupported())
            {
                // Try to set the image
                UserProfilePersonalizationSettings profileSettings = UserProfilePersonalizationSettings.Current;
                if (isLockScreen)
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
