using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using Microsoft.ApplicationInsights.DataContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
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
            All,
            LockScreen,
            Desktop,
            Band
        }

        ///
        /// Const Vars
        ///
        private const int CMaxImageCacheCount = 5;
        private const string CImageLockScreenCacheFolderPath = "LockScreenImageCache";
        private const string CImageDesktopCacheFolderPath = "DesktopImageCache";
        private const string CImageBandCacheFolderPath = "BandImageCache";

        ///
        /// Private
        ///
        private readonly BaconManager _baconMan;
        private StorageFolder _mLockScreenImageCacheFolder;
        private StorageFolder _mDesktopImageCacheFolder;
        private StorageFolder _mBandImageCacheFolder;
        private bool _isRunning;

        private RefCountedDeferral _lockScreenRefDeferral;
        private RefCountedDeferral _mDesktopRefDeferral;
        private RefCountedDeferral _mBandRefDeferral;


        public BackgroundImageUpdater(BaconManager baconMan)
        {
            _baconMan = baconMan;
        }

        /// <summary>
        /// Kicks off an update of the images.
        /// </summary>
        /// <param name="refDeferral"></param>
        /// <param name="force"></param>
        public async Task RunUpdate(RefCountedDeferral refDeferral, bool force = false)
        {
            if ((IsDesktopEnabled || IsLockScreenEnabled || IsBandWallpaperEnabled) && UserProfilePersonalizationSettings.IsSupported())
            {
                var timeSinceLastRun = DateTime.Now - LastImageUpdate;
                if(timeSinceLastRun.TotalMinutes > UpdateFrequency || force)
                {
                    // Make sure we aren't running
                    lock(this)
                    {
                        if(_isRunning)
                        {
                            return;
                        }
                        _isRunning = true;
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
            TelemetryManager.ReportLog(this, "Updater updating.");
            try
            {
                // Figure out if we need to do a full update
                var fullUpdateFrequencyMin = UpdateFrequency * CMaxImageCacheCount;
                var timeSinceLastFullUpdate = DateTime.Now - LastFullUpdate;

                if (timeSinceLastFullUpdate.TotalMinutes > fullUpdateFrequencyMin || force)
                {
                    TelemetryManager.ReportLog(this, "Running full update");

                    // Update lock screen images
                    if (IsLockScreenEnabled)
                    {
                        TelemetryManager.ReportLog(this, "Updating lock screen", SeverityLevel.Verbose);

                        // Make a deferral scope object so we can do our work without being killed.
                        // Note! We need release this object or it will hang the app around forever!
                        _lockScreenRefDeferral = refDeferral;
                        _lockScreenRefDeferral.AddRef();

                        // Kick off the update, this will happen async.
                        GetSubredditStories(LockScreenSubredditName, UpdateTypes.LockScreen);
                    }

                    if(IsDesktopEnabled)
                    {
                        TelemetryManager.ReportLog(this, "Updating lock screen", SeverityLevel.Verbose); 
                                        
                        // Shortcut: If lock screen in enabled and it is the same subreddit just share the same cache. If not,
                        // get the desktop images
                        if (IsLockScreenEnabled && LockScreenSubredditName.Equals(DesktopSubredditName))
                        {
                            TelemetryManager.ReportLog(this, "Desktop same sub as lockscreen, skipping image update.", SeverityLevel.Verbose);
                        }
                        else
                        {
                            TelemetryManager.ReportLog(this, "Updating desktop image", SeverityLevel.Verbose);

                            // Make a deferral scope object so we can do our work without being killed.
                            // Note! We need release this object or it will hang the app around forever!
                            _mDesktopRefDeferral = refDeferral;
                            _mDesktopRefDeferral.AddRef();

                            // Kick off the update, this will happen async.
                            GetSubredditStories(DesktopSubredditName, UpdateTypes.Desktop);                           
                        }
                    }

                    // Update lock screen images
                    if (IsBandWallpaperEnabled)
                    {
                        TelemetryManager.ReportLog(this, "Updating band wallpaper", SeverityLevel.Verbose);

                        // Make a deferral scope object so we can do our work without being killed.
                        // Note! We need release this object or it will hang the app around forever!
                        _mBandRefDeferral = refDeferral;
                        _mBandRefDeferral.AddRef();

                        // Kick off the update, this will happen async.
                        GetSubredditStories(BandSubredditName, UpdateTypes.Band);
                    }
                }
                // Else full update
                else
                {
                    TelemetryManager.ReportLog(this, "No need for a full update, just rotating.");

                    // If we aren't doing a full update just rotate the images.
                    await DoImageRotation(UpdateTypes.All);

                    // Stop the running state
                    lock (this)
                    {
                        _isRunning = false;
                    }
                }
            }
            catch(Exception e)
            {
                _baconMan.MessageMan.DebugDia("Failed to set background image", e);
                TelemetryManager.ReportUnexpectedEvent(this, "Failed to set background image", e);
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
                var wasSuccess = false;
                switch (type)
                {
                    case UpdateTypes.LockScreen:
                        wasSuccess = await DoSingleImageRotation(UpdateTypes.LockScreen);
                        break;
                    case UpdateTypes.Desktop:
                        wasSuccess = await DoSingleImageRotation(UpdateTypes.Desktop);
                        break;
                    case UpdateTypes.Band:
                        wasSuccess = await DoSingleImageRotation(UpdateTypes.Band);
                        break;
                    case UpdateTypes.All:
                        break;
                    default:
                    {
                        var firstSuccess = await DoSingleImageRotation(UpdateTypes.LockScreen);
                        var secondSuccess = await DoSingleImageRotation(UpdateTypes.Desktop);
                        var thirdSuccess = await DoSingleImageRotation(UpdateTypes.Band);
                        wasSuccess = firstSuccess && secondSuccess && thirdSuccess;
                        break;
                    }
                }      
                
                // If we successfully updated set the time.
                if(wasSuccess)
                {
                    LastImageUpdate = DateTime.Now;
                }       
            }
            catch (Exception e)
            {
                _baconMan.MessageMan.DebugDia("Failed to set background image", e);
                TelemetryManager.ReportUnexpectedEvent(this, "Failed to set background image", e);
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
            if ((!IsLockScreenEnabled || type != UpdateTypes.LockScreen) &&
                (!IsDesktopEnabled || type != UpdateTypes.Desktop) &&
                (!IsBandWallpaperEnabled || type != UpdateTypes.Band)) return true;
            // If the lock screen and desktop are the same subreddit use the same files.
            var fileCacheType = type;    
            // If this is a desktop update, and lock is enabled, and they are the same subreddit...
            if (type == UpdateTypes.Desktop && IsLockScreenEnabled && LockScreenSubredditName.Equals(DesktopSubredditName))
            {
                // If they are all the same use the lock screen cache for the desktop.
                fileCacheType = UpdateTypes.LockScreen;
            }

            // Get the current files..
            var files = await GetCurrentCacheImages(fileCacheType);

            TelemetryManager.ReportLog(this, "Current images in cache :" + files.Count);

            if (files.Count == 0) return true;
            var currentIndex = 0;
            switch (type)
            {
                case UpdateTypes.LockScreen:
                {
                    // Update the index
                    CurrentLockScreenRotationIndex++;
                    if (CurrentLockScreenRotationIndex >= files.Count)
                    {
                        CurrentLockScreenRotationIndex = 0;
                    }
                    currentIndex = CurrentLockScreenRotationIndex;
                    break;
                }
                case UpdateTypes.Band:
                {
                    // Update the index
                    CurrentBandRotationIndex++;
                    if (CurrentBandRotationIndex >= files.Count)
                    {
                        CurrentBandRotationIndex = 0;
                    }
                    currentIndex = CurrentBandRotationIndex;
                    break;
                }
                case UpdateTypes.All:
                    break;
                case UpdateTypes.Desktop:
                    break;
                default:
                {
                    // Update the index
                    CurrentDesktopRotationIndex++;
                    if (CurrentDesktopRotationIndex >= files.Count)
                    {
                        CurrentDesktopRotationIndex = 0;
                    }
                    currentIndex = CurrentDesktopRotationIndex;
                    break;
                }
            }

            TelemetryManager.ReportLog(this, "Current index used to set image :" + currentIndex);

            // Set the image
            var wasSuccess = await SetBackgroundImage(type, files[currentIndex]);

            if (!wasSuccess)
            {
                TelemetryManager.ReportLog(this, "Image update failed", SeverityLevel.Error);
                TelemetryManager.ReportUnexpectedEvent(this, type == UpdateTypes.LockScreen ? "LockscreenImageUpdateFailed" : "DesktopImageUpdateFailed");
            }
            else
            {
                TelemetryManager.ReportLog(this, "Image update success");
            }

            return wasSuccess;
            // Return true if we are disabled
        }

        #endregion

        #region Get Posts

        /// <summary>
        /// This will kick off the process of getting the stories for a subreddit.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private void GetSubredditStories(string name, UpdateTypes type)
        {
            // Create a fake subreddit, the ID needs to be unique
            var subreddit = new Subreddit { Id = DateTime.Now.Ticks.ToString(), DisplayName = name };

            // Get the collector for the subreddit
            var collector = PostCollector.GetCollector(subreddit, _baconMan);

            switch (type)
            {
                // Sub to the collector callback
                case UpdateTypes.LockScreen:
                    collector.OnCollectionUpdated += Collector_OnCollectionUpdatedLockScreen;
                    collector.OnCollectorStateChange += Collector_OnCollectorStateChangeLockScreen;
                    break;
                case UpdateTypes.Band:
                    collector.OnCollectionUpdated += Collector_OnCollectionUpdatedBand;
                    collector.OnCollectorStateChange += Collector_OnCollectorStateChangeBand;
                    break;
                case UpdateTypes.All:
                    break;
                case UpdateTypes.Desktop:
                    break;
                default:
                    collector.OnCollectionUpdated += Collector_OnCollectionUpdatedDesktop;
                    collector.OnCollectorStateChange += Collector_OnCollectorStateChangeDesktop;
                    break;
            }

            // Kick off the update
            collector.Update(true);
        }

        /// <summary>
        /// Fired when the collector state changed for the lock screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectorStateChangeLockScreen(object sender, CollectorStateChangeArgs e)
        {
            Collector_OnCollectorStateChange(UpdateTypes.LockScreen, e);
        }

        /// <summary>
        /// Fired when the collector state changed for the lock screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectorStateChangeDesktop(object sender, CollectorStateChangeArgs e)
        {
            Collector_OnCollectorStateChange(UpdateTypes.Desktop, e);
        }

        /// <summary>
        /// Fired when the collector state changed for the band
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectorStateChangeBand(object sender, CollectorStateChangeArgs e)
        {
            Collector_OnCollectorStateChange(UpdateTypes.Band, e);
        }

        /// <summary>
        /// Fired when the collector state changed for the either
        /// </summary>
        /// <param name="type"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectorStateChange(UpdateTypes type, CollectorStateChangeArgs e)
        {
            if (e.State != CollectorState.Error) return;
            // We had an error. This is the end of the line, kill the deferral
            ReleaseDeferral(type);

            // And try to set isRunning
            UnSetIsRunningIfDone();
        }

        /// <summary>
        /// Fired when the stories come in for a type
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectionUpdatedLockScreen(object sender, CollectionUpdatedArgs<Post> e)
        {
            Collector_OnCollectionUpdated(UpdateTypes.LockScreen, e);
        }

        /// <summary>
        /// Fired when the stories come in for a type
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectionUpdatedDesktop(object sender, CollectionUpdatedArgs<Post> e)
        {
            Collector_OnCollectionUpdated(UpdateTypes.Desktop, e);
        }

        /// <summary>
        /// Fired when the stories come in for a type
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectionUpdatedBand(object sender, CollectionUpdatedArgs<Post> e)
        {
            Collector_OnCollectionUpdated(UpdateTypes.Band, e);
        }

        /// <summary>
        /// Fired when the stories come in for a type
        /// </summary>

        /// <param name="type"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectionUpdated(UpdateTypes type, CollectionUpdatedArgs<Post> e)
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
        private readonly object _getImageLock = new object();

        private int _desktopRequestCount;
        private int _desktopDoneCount;
        private int _lockScreenRequestCount;
        private int _lockScreenDoneCount;
        private int _bandRequestCount;
        private int _bandDoneCount;

        /// <summary>
        /// Given a list of post this gets the images and puts them into file cache.
        /// </summary>
        /// <param name="posts"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private async void GetImagesFromPosts(IEnumerable<Post> posts, UpdateTypes type)
        {
            // First, remove all of the existing images
            try
            {
                await DeleteAllCacheImages(type);
            }
            catch(Exception e)
            {
                TelemetryManager.ReportUnexpectedEvent(this, "FailedToDeleteCacheImages", e);
            }

            switch (type)
            {
                // Setup the vars
                case UpdateTypes.LockScreen:
                    _lockScreenRequestCount = 0;
                    _lockScreenDoneCount = 0;
                    break;
                case UpdateTypes.Band:
                    _bandRequestCount = 0;
                    _bandDoneCount = 0;
                    break;
                case UpdateTypes.All:
                    break;
                case UpdateTypes.Desktop:
                    break;
                default:
                    _desktopRequestCount = 0;
                    _desktopDoneCount = 0;
                    break;
            }

            // Figure out all of the images we need to request.
            var imageRequestList = new List<Tuple<string, string>>();
            foreach (var post in posts)
            {
                var imageUrl = ImageManager.GetImageUrl(post.Url);
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    imageRequestList.Add(new Tuple<string, string>(imageUrl, post.Id));
                }

                if(imageRequestList.Count > CMaxImageCacheCount)
                {
                    break;
                }
            }

            switch (type)
            {
                // Now set our request counts before we start requesting. We must do this to ensure
                // the numbers are correct when the request come back.
                case UpdateTypes.LockScreen:
                    _lockScreenRequestCount = imageRequestList.Count;
                    break;
                case UpdateTypes.Band:
                    _bandRequestCount = imageRequestList.Count;
                    break;
                case UpdateTypes.All:
                    break;
                case UpdateTypes.Desktop:
                    break;
                default:
                    _desktopRequestCount = imageRequestList.Count;
                    break;
            }

            // Now make all of the request.
            foreach (var imageManagerRequest in imageRequestList.Select(request => new ImageManager.ImageManagerRequest
            {
                Url = request.Item1,
                ImageId = request.Item2,
                Context = type
            }))
            {
                imageManagerRequest.OnRequestComplete += OnRequestComplete;
                _baconMan.ImageMan.QueueImageRequest(imageManagerRequest);
            }  

            // If we have nothing to request this is the end of the line for this type.
            if (imageRequestList.Count != 0) return;
            // And kill the deferral
            ReleaseDeferral(type);

            // And set us to stopped.
            UnSetIsRunningIfDone();
        }

        /// <summary>
        /// Fired when an image request is done.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="response"></param>
        private async void OnRequestComplete(object sender, ImageManager.ImageManagerResponseEventArgs response)
        {
            // Remove event listener
            var request = (ImageManager.ImageManagerRequest)sender;
            request.OnRequestComplete -= OnRequestComplete;

            var type = (UpdateTypes)response.Request.Context;

            // Make sure we were successfully.
            if (response.Success)
            {
                try
                {
                    // Get the target size
                    var targetImageSize = LastKnownScreenResolution;
                    switch (type)
                    {
                        case UpdateTypes.Band when _baconMan.BackgroundMan.BandMan.BandVersion == BandVersions.V1:
                            targetImageSize = new Size(310, 102);
                            break;
                        case UpdateTypes.Band:
                            targetImageSize = new Size(310, 128);
                            break;
                        case UpdateTypes.Desktop when DeviceHelper.CurrentDevice() == DeviceTypes.Mobile:
                            // If we are desktop on mobile we want to do a bit larger than the screen res because
                            // there is a sliding image animation when you switch to all apps. Lets make the width 30% larger.
                            targetImageSize.Width *= 1.3;
                            break;
                        case UpdateTypes.All:
                            break;
                        case UpdateTypes.LockScreen:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    // Resize the image to fit nicely
                    var image = await ResizeImage(response.ImageStream, targetImageSize);

                    // Write the file
                    await WriteImageToFile(image, type);
                }
                catch (Exception e)
                {
                    _baconMan.MessageMan.DebugDia("Failed to write background image", e);
                    TelemetryManager.ReportUnexpectedEvent(this, "Failed to write background image", e);
                }
            }

            // Indicate that this image is done.
            var isDone = false;
            lock (_getImageLock)
            {
                switch (type)
                {
                    case UpdateTypes.LockScreen:
                        _lockScreenDoneCount++;
                        isDone = _lockScreenDoneCount >= _lockScreenRequestCount;
                        break;
                    case UpdateTypes.Band:
                        _bandDoneCount++;
                        isDone = _bandDoneCount >= _bandRequestCount;
                        break;
                    case UpdateTypes.All:
                        break;
                    case UpdateTypes.Desktop:
                        break;
                    default:
                        _desktopDoneCount++;
                        isDone = _desktopDoneCount >= _desktopRequestCount;
                        break;
                }
            }

            // if we are done done then tell the images to rotate and clean up.
            if (!isDone) return;
            switch (type)
            {
                // Set this high so we roll over.
                case UpdateTypes.LockScreen:
                    CurrentLockScreenRotationIndex = 99;
                    break;
                case UpdateTypes.Band:
                    CurrentBandRotationIndex = 99;
                    break;
                case UpdateTypes.All:
                    break;
                case UpdateTypes.Desktop:
                    break;
                default:
                    CurrentDesktopRotationIndex = 99;
                    break;
            }

            // Do the rotate
            await DoImageRotation(type);

            // If this is a lock screen update this might also be a desktop update. This happens when the lock screen and desktop
            // share the same subreddit, they share the same images.
            if (type == UpdateTypes.LockScreen && IsDesktopEnabled && LockScreenSubredditName.Equals(DesktopSubredditName))
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

        private async Task DeleteAllCacheImages(UpdateTypes type)
        {
            var localFolder = await GetImageCacheFolder(type);
            var files = await localFolder.GetFilesAsync();
            foreach (var file in files)
            {
                await file.DeleteAsync();
            }
        }

        private async Task WriteImageToFile(InMemoryRandomAccessStream stream, UpdateTypes type)
        {
            var localFolder = await GetImageCacheFolder(type);
            var file = await localFolder.CreateFileAsync("cachedImage.jpg", CreationCollisionOption.GenerateUniqueName);

            using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await RandomAccessStream.CopyAndCloseAsync(stream.GetInputStreamAt(0), fileStream.GetOutputStreamAt(0));
            }
        }

        private async Task<InMemoryRandomAccessStream> ResizeImage(InMemoryRandomAccessStream stream, Size requiredSize)
        {
            // Make a decoder for the current stream
            var decoder = await BitmapDecoder.CreateAsync(stream);

            var imageHeight = decoder.PixelHeight;
            var imageWidth = decoder.PixelWidth;

            var widthRatio = imageWidth / requiredSize.Width;
            var heightRatio = imageHeight / requiredSize.Height;
               
            uint outputHeight;
            uint outputWidth;
            var centerOnX = false;

            if (widthRatio < heightRatio)
            {
                outputHeight = (uint)(imageHeight / widthRatio);
                outputWidth = (uint)requiredSize.Width;
            }
            else
            {
                outputWidth = (uint)(imageWidth / heightRatio);
                outputHeight = (uint)requiredSize.Height;
                centerOnX = true;
            }

            // Make an output stream and an encoder
            var outputStream = new InMemoryRandomAccessStream();
            var enc = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);

            // convert the entire bitmap to a 125px by 125px bitmap
            enc.BitmapTransform.ScaledHeight = outputHeight;
            enc.BitmapTransform.ScaledWidth = outputWidth;
            var bound = new BitmapBounds();
            bound.Height = (uint)requiredSize.Height;
            bound.Width = (uint)requiredSize.Width;

            // Choose Fant for quality over perf.
            enc.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;

            if(centerOnX)
            {
                var width = ((int)outputWidth / 2) - ((int)bound.Width / 2);
                bound.X = (uint)(width > 0 ? width : 0);
            }
            else
            {
                var height = ((int)outputHeight / 2) - ((int)bound.Height / 2);
                bound.Y = (uint)(height > 0 ? height : 0);
            }
            enc.BitmapTransform.Bounds = bound;

            // Do it
            await enc.FlushAsync();

            // Return the new stream
            return outputStream;
        }


        #endregion

        /// <summary>
        /// Turns off is running if both the desktop and locks screens are done updating.
        /// </summary>
        private void UnSetIsRunningIfDone()
        {
            lock(this)
            {
                if(_lockScreenRefDeferral == null && _mDesktopRefDeferral == null && _mBandRefDeferral == null)
                {
                    _isRunning = false;
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
                switch (type)
                {
                    case UpdateTypes.LockScreen:
                        _lockScreenRefDeferral?.ReleaseRef();
                        _lockScreenRefDeferral = null;
                        break;
                    case UpdateTypes.Band:
                        _mBandRefDeferral?.ReleaseRef();
                        _mBandRefDeferral = null;
                        break;
                    case UpdateTypes.All:
                        break;
                    case UpdateTypes.Desktop:
                        break;
                    default:
                        _mDesktopRefDeferral?.ReleaseRef();
                        _mDesktopRefDeferral = null;
                        break;
                }
            }  
        }

        /// <summary>
        /// Returns the current contents of the cache folder
        /// </summary>
        /// <returns></returns>
        private async Task<IReadOnlyList<StorageFile>> GetCurrentCacheImages(UpdateTypes type)
        {
            var localFolder = await GetImageCacheFolder(type);
            return await localFolder.GetFilesAsync();
        }

        /// <summary>
        /// Returns the image cache folder for desktop images
        /// </summary>
        /// <returns></returns>
        private async Task<StorageFolder> GetImageCacheFolder(UpdateTypes type)
        {
            switch (type)
            {
                case UpdateTypes.LockScreen:
                {
                    return _mLockScreenImageCacheFolder ?? (_mLockScreenImageCacheFolder =
                        await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                            CImageLockScreenCacheFolderPath, CreationCollisionOption.OpenIfExists));
                }
                case UpdateTypes.Band:
                {
                    return _mBandImageCacheFolder ?? (_mBandImageCacheFolder =
                        await ApplicationData.Current.LocalFolder.CreateFolderAsync(CImageBandCacheFolderPath,
                            CreationCollisionOption.OpenIfExists));
                }
                case UpdateTypes.All:
                    break;
                case UpdateTypes.Desktop:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            return _mDesktopImageCacheFolder ?? (_mDesktopImageCacheFolder =
                await ApplicationData.Current.LocalFolder.CreateFolderAsync(CImageDesktopCacheFolderPath,
                    CreationCollisionOption.OpenIfExists));
        }

        /// <summary>
        /// Returns the current contents of the cache folder
        /// </summary>
        /// <returns></returns>
        private async Task<bool> SetBackgroundImage(UpdateTypes type, StorageFile file)
        {
            var wasSuccess = false;

            if(type == UpdateTypes.Band)
            {
                await _baconMan.BackgroundMan.BandMan.UpdateBandWallpaper(file);
                // The band can fail quite a lot, if so don't count this as a fail.
                wasSuccess = true;
            }
            // Make sure we can do it
            else if (UserProfilePersonalizationSettings.IsSupported())
            {
                // Try to set the image
                var profileSettings = UserProfilePersonalizationSettings.Current;
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
                if (_isLockScreenEnabled.HasValue) return _isLockScreenEnabled.Value;
                _isLockScreenEnabled = _baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.IsLockScreenEnabled") && _baconMan.SettingsMan.ReadFromLocalSettings<bool>("BackgroundImageUpdater.IsLockScreenEnabled");
                return _isLockScreenEnabled.Value;
            }
            set
            {
                _isLockScreenEnabled = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundImageUpdater.IsLockScreenEnabled", _isLockScreenEnabled.Value);
            }
        }
        private bool? _isLockScreenEnabled;

        /// <summary>
        /// Indicates if desktop wallpaper update is enabled
        /// </summary>
        public bool IsDesktopEnabled
        {
            get
            {
                if (_isDesktopEnabled.HasValue) return _isDesktopEnabled.Value;
                _isDesktopEnabled = _baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.IsDesktopEnabled") && _baconMan.SettingsMan.ReadFromLocalSettings<bool>("BackgroundImageUpdater.IsDesktopEnabled");
                return _isDesktopEnabled.Value;
            }
            set
            {
                _isDesktopEnabled = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundImageUpdater.IsDesktopEnabled", _isDesktopEnabled.Value);
            }
        }
        private bool? _isDesktopEnabled;

        /// <summary>
        /// Indicates if band wallpaper update is enabled
        /// </summary>
        public bool IsBandWallpaperEnabled
        {
            get
            {
                if (_isBandWallpaperEnabled.HasValue) return _isBandWallpaperEnabled.Value;
                _isBandWallpaperEnabled = _baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.IsBandWallpaperEnabled") && _baconMan.SettingsMan.ReadFromLocalSettings<bool>("BackgroundImageUpdater.IsBandWallpaperEnabled");
                return _isBandWallpaperEnabled.Value;
            }
            set
            {
                _isBandWallpaperEnabled = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundImageUpdater.IsBandWallpaperEnabled", _isBandWallpaperEnabled.Value);
            }
        }
        private bool? _isBandWallpaperEnabled;

        /// <summary>
        /// Indicates which image in the cache we are on for lock screen
        /// </summary>
        private int CurrentLockScreenRotationIndex
        {
            get
            {
                if (_currentLockScreenRotationIndex.HasValue) return _currentLockScreenRotationIndex.Value;
                _currentLockScreenRotationIndex = _baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.CurrentLockScreenRotationIndex") ? _baconMan.SettingsMan.ReadFromLocalSettings<int>("BackgroundImageUpdater.CurrentLockScreenRotationIndex") : 0;
                return _currentLockScreenRotationIndex.Value;
            }
            set
            {
                _currentLockScreenRotationIndex = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundImageUpdater.CurrentLockScreenRotationIndex", _currentLockScreenRotationIndex.Value);
            }
        }
        private int? _currentLockScreenRotationIndex;

        /// <summary>
        /// Indicates which image in the cache we are on for band screen
        /// </summary>
        private int CurrentBandRotationIndex
        {
            get
            {
                if (_currentBandRotationIndex.HasValue) return _currentBandRotationIndex.Value;
                _currentBandRotationIndex = _baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.CurrentBandRotationIndex") ? _baconMan.SettingsMan.ReadFromLocalSettings<int>("BackgroundImageUpdater.CurrentBandRotationIndex") : 0;
                return _currentBandRotationIndex.Value;
            }
            set
            {
                _currentBandRotationIndex = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundImageUpdater.CurrentBandRotationIndex", _currentBandRotationIndex.Value);
            }
        }
        private int? _currentBandRotationIndex;

        /// <summary>
        /// Indicates which image in the cache we are on for desktop
        /// </summary>
        private int CurrentDesktopRotationIndex
        {
            get
            {
                if (_currentDesktopRotationIndex.HasValue) return _currentDesktopRotationIndex.Value;
                _currentDesktopRotationIndex = _baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.CurrentDesktopRotationIndex") ? _baconMan.SettingsMan.ReadFromLocalSettings<int>("BackgroundImageUpdater.CurrentDesktopRotationIndex") : 0;
                return _currentDesktopRotationIndex.Value;
            }
            set
            {
                _currentDesktopRotationIndex = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundImageUpdater.CurrentDesktopRotationIndex", _currentDesktopRotationIndex.Value);
            }
        }
        private int? _currentDesktopRotationIndex;

        /// <summary>
        /// Indicates which image in the cache we are on
        /// </summary>
        public int UpdateFrequency
        {
            get
            {
                if (_updateFrequency.HasValue) return _updateFrequency.Value;
                _updateFrequency = _baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.UpdateFrequency") ? _baconMan.SettingsMan.ReadFromLocalSettings<int>("BackgroundImageUpdater.UpdateFrequency") : 120;
                return _updateFrequency.Value;
            }
            set
            {
                _updateFrequency = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundImageUpdater.UpdateFrequency", _updateFrequency.Value);
            }
        }
        private int? _updateFrequency;

        /// <summary>
        /// The last time the background image was rotated
        /// </summary>
        public DateTime LastImageUpdate
        {
            get
            {
                if (!_lastImageUpdate.Equals(new DateTime(0))) return _lastImageUpdate;
                if (_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.LastImageUpdate"))
                {
                    _lastImageUpdate = _baconMan.SettingsMan.ReadFromLocalSettings<DateTime>("BackgroundImageUpdater.LastImageUpdate");
                }
                return _lastImageUpdate;
            }
            private set
            {
                _lastImageUpdate = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundImageUpdater.LastImageUpdate", _lastImageUpdate);
            }
        }
        private DateTime _lastImageUpdate = new DateTime(0);

        /// <summary>
        /// The last time the background images where updated
        /// </summary>
        public DateTime LastFullUpdate
        {
            get
            {
                if (!_lastFullUpdate.Equals(new DateTime(0))) return _lastFullUpdate;
                if (_baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.LastFullUpdate"))
                {
                    _lastFullUpdate = _baconMan.SettingsMan.ReadFromLocalSettings<DateTime>("BackgroundImageUpdater.LastFullUpdate");
                }
                return _lastFullUpdate;
            }
            private set
            {
                _lastFullUpdate = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundImageUpdater.LastFullUpdate", _lastFullUpdate);
            }
        }
        private DateTime _lastFullUpdate = new DateTime(0);

        /// <summary>
        /// Indicates which subreddit to update from. Note we only grab the URL here
        /// because we don't want to write a subreddit object across roaming settings.
        /// </summary>
        public string LockScreenSubredditName
        {
            get
            {
                if (_lockScreenSubredditUrl != null) return _lockScreenSubredditUrl;
                _lockScreenSubredditUrl = _baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.LockScreenSubredditUrl") ? _baconMan.SettingsMan.ReadFromLocalSettings<string>("BackgroundImageUpdater.LockScreenSubredditUrl") : "earthporn";
                return _lockScreenSubredditUrl;
            }
            set
            {
                _lockScreenSubredditUrl = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundImageUpdater.LockScreenSubredditUrl", _lockScreenSubredditUrl);
            }
        }
        private string _lockScreenSubredditUrl;

        /// <summary>
        /// Indicates which subreddit to update from. Note we only grab the URL here
        /// because we don't want to write a subreddit object across roaming settings.
        /// </summary>
        public string DesktopSubredditName
        {
            get
            {
                if (_desktopSubredditUrl != null) return _desktopSubredditUrl;
                _desktopSubredditUrl = _baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.DesktopSubredditUrl") ? _baconMan.SettingsMan.ReadFromLocalSettings<string>("BackgroundImageUpdater.DesktopSubredditUrl") : "earthporn";
                return _desktopSubredditUrl;
            }
            set
            {
                _desktopSubredditUrl = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundImageUpdater.DesktopSubredditUrl", _desktopSubredditUrl);
            }
        }
        private string _desktopSubredditUrl;

        /// <summary>
        /// Indicates which subreddit to update from. Note we only grab the URL here
        /// because we don't want to write a subreddit object across roaming settings.
        /// </summary>
        public string BandSubredditName
        {
            get
            {
                if (_bandSubredditName != null) return _bandSubredditName;
                _bandSubredditName = _baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.BandSubredditName") ? _baconMan.SettingsMan.ReadFromLocalSettings<string>("BackgroundImageUpdater.BandSubredditName") : "earthporn";
                return _bandSubredditName;
            }
            set
            {
                _bandSubredditName = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundImageUpdater.BandSubredditName", _bandSubredditName);
            }
        }
        private string _bandSubredditName;


        /// <summary>
        /// Indicates which subreddit to update from. Note we only grab the URL here
        /// because we don't want to write a subreddit object across roaming settings.
        /// </summary>
        public Size LastKnownScreenResolution
        {
            get
            {
                if (_lastKnownScreenResolution.HasValue) return _lastKnownScreenResolution.Value;
                _lastKnownScreenResolution = _baconMan.SettingsMan.LocalSettings.ContainsKey("BackgroundImageUpdater.LastKnownScreenResolution") ? _baconMan.SettingsMan.ReadFromLocalSettings<Size>("BackgroundImageUpdater.LastKnownScreenResolution") : new Size(1920, 1080);
                return _lastKnownScreenResolution.Value;
            }
            set
            {
                _lastKnownScreenResolution = value;
                _baconMan.SettingsMan.WriteToLocalSettings("BackgroundImageUpdater.LastKnownScreenResolution", _lastKnownScreenResolution.Value);
            }
        }
        private Size? _lastKnownScreenResolution;

        #endregion
    }
}
