using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using BaconBackend.Helpers;
using Windows.Storage;

namespace BaconBackend.Managers
{
    public class ImageManager
    {
        /// <summary>
        /// The max number of image request allowed in parallel
        /// </summary>
        private const int MaxRunningImageRequests = 5;

        /// <summary>
        /// Holds the context of each image.
        /// </summary>
        public class ImageManagerRequest
        {
            /// <summary>
            /// The URL of the image
            /// </summary>
            public string Url;

            /// <summary>
            /// A consumer provided Id for the image
            /// </summary>
            public string ImageId = "";

            /// <summary>
            /// A consumer provided context that can be anything they want it to be.
            /// </summary>
            public object Context;

            /// <summary>
            /// Fired when the image is ready.
            /// </summary>
            public event EventHandler<ImageManagerResponseEventArgs> OnRequestComplete
            {
                add => MOnRequestComplete.Add(value);
                remove => MOnRequestComplete.Remove(value);
            }
            internal SmartWeakEvent<EventHandler<ImageManagerResponseEventArgs>> MOnRequestComplete = new SmartWeakEvent<EventHandler<ImageManagerResponseEventArgs>>();
        }

        public class ImageManagerResponseEventArgs : EventArgs
        {
            /// <summary>
            /// The original request.
            /// </summary>
            public ImageManagerRequest Request;

            /// <summary>
            /// Indicates if the request was successful
            /// </summary>
            public bool Success;

            /// <summary>
            /// The image stream returned.
            /// </summary>
            public InMemoryRandomAccessStream ImageStream;
        }

        /// <summary>
        /// An internal context for each image request
        /// </summary>
        private class ImageManagerRequestInternal
        {
            public ImageManagerRequestInternal(ImageManagerRequest context) { Context = context; }
            public readonly ImageManagerRequest Context;
            public bool IsServicing;
        }

        //
        // Private Vars
        //
        private readonly BaconManager _baconMan;
        private readonly List<ImageManagerRequestInternal> _requestList = new List<ImageManagerRequestInternal>();

        public ImageManager(BaconManager baconMan)
        {
            _baconMan = baconMan;
        }

        /// <summary>
        /// Queues a new image request. The request will be taken care of by an async operation.
        /// </summary>
        /// <param name="orgionalRequest">The image request</param>
        public void QueueImageRequest(ImageManagerRequest orgionalRequest)
        {
            // Validate
            if(string.IsNullOrWhiteSpace(orgionalRequest.Url))
            {
                throw new Exception("You must supply both a ULR and callback");
            }

            ImageManagerRequestInternal serviceRequest = null;
            // Lock the list
            lock(_requestList)
            {
                // Add the current request to the back of the list.
                _requestList.Add(new ImageManagerRequestInternal(orgionalRequest));

                // Check if we should make the request now or not. If we don't
                // when a request finishes it will pick up the new request.
                for (var i = 0; i < MaxRunningImageRequests; i++)
                {
                    if (_requestList.Count <= i || _requestList[i].IsServicing) continue;
                    serviceRequest = _requestList[i];
                    serviceRequest.IsServicing = true;
                    break;
                }
            }

            if(serviceRequest != null)
            {
                // Kick off a new thread to service the request.
                Task.Run(async () =>
                {
                    await ServiceRequest(serviceRequest);
                });
            }
        }

        /// <summary>
        /// Used to service the given request. Once the image has been responded to the
        /// function will continue with requests until the queue is empty
        /// </summary>
        /// <param name="startingRequestedContext">The request to start with</param>
        /// <returns></returns>
        private async Task ServiceRequest(ImageManagerRequestInternal startingRequestedContext)
        {
            var currentRequest = startingRequestedContext;
            while (currentRequest != null)
            {
                // Service the request in a try catch
                try
                {
                    var fileName = MakeFileNameFromUrl(currentRequest.Context.Url);

                    // First check if we have the file
                    if (CacheManager.HasFileCached(fileName))
                    {
                        // If we have it cached pull it from there
                    }
                    else
                    {
                        // If not we have to get the image
                        var imgBuffer = await NetworkManager.MakeRawGetRequest(currentRequest.Context.Url);

                        // Turn the stream into an image
                        var imageStream = new InMemoryRandomAccessStream();
                        var readStream = imgBuffer.AsStream();
                        var writeStream = imageStream.AsStreamForWrite();

                        // Copy the buffer
                        // #todo perf - THERE HAS TO BE A BETTER WAY TO DO THIS.
                        readStream.CopyTo(writeStream);
                        writeStream.Flush();

                        // Seek to the start.
                        imageStream.Seek(0);

                        // Create a response
                        var response = new ImageManagerResponseEventArgs
                        {
                            ImageStream = imageStream,
                            Request = currentRequest.Context,
                            Success = true
                        };

                        // Fire the callback
                        currentRequest.Context.MOnRequestComplete.Raise(currentRequest.Context, response);
                    }
                }
                catch (Exception e)
                {
                    // Report the error
                    _baconMan.MessageMan.DebugDia("Error getting image", e);                 

                    // Create a response
                    var response = new ImageManagerResponseEventArgs
                    {
                        Request = currentRequest.Context,
                        Success = false
                    };

                    // Send the response
                    currentRequest.Context.MOnRequestComplete.Raise(currentRequest.Context, response);
                }

                // Once we are done, check to see if there is another we should service.
                lock (_requestList)
                {
                    // Remove the current request.
                    _requestList.Remove(currentRequest);

                    // Kill the current request
                    currentRequest = null;

                    // Check if there is another request we should service now.
                    for (var i = 0; i < MaxRunningImageRequests; i++)
                    {
                        if (_requestList.Count <= i || _requestList[i].IsServicing) continue;
                        currentRequest = _requestList[i];
                        currentRequest.IsServicing = true;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Converts the image file name into a file system safe string.
        /// </summary>
        /// <param name="url">The url</param>
        /// <returns>File system safe string</returns>
        private static string MakeFileNameFromUrl(string url)
        {
            var strBuilder = new StringBuilder();
            foreach(var c in url)
            {
                if ((c >= '0' && c <= '9')
                    || (c >= 'A' && c <= 'z'
                    || (c == '_')))
                {
                    strBuilder.Append(c);
                }
            }
            return strBuilder.ToString();
        }

        /// <summary>
        /// Checks if the given string is a thumbnail image
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsThumbnailImage(string str)
        {
            return !string.IsNullOrWhiteSpace(str)
                   && str.Contains("http");
        }

        /// <summary>
        /// Gets an image and saves it locally.
        /// </summary>
        /// <param name="postUrl"></param>
        public void SaveImageLocally(string postUrl)
        {
            try
            {
                var imageUrl = GetImageUrl(postUrl);

                // Fire off a request for the image.
                var rand = new Random((int)DateTime.Now.Ticks);
                var request = new ImageManagerRequest
                {
                    ImageId = rand.NextDouble().ToString(CultureInfo.InvariantCulture),
                    Url = imageUrl
                };

                // Set the callback
                request.OnRequestComplete += async (sender, response) =>
                {
                    try
                    {
                        // If success
                        if (!response.Success) return;
                        // Get the photos library
                        var myPictures = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);

                        // Get the save folder
                        var saveFolder = myPictures.SaveFolder;

                        // Try to find the saved pictures folder
                        StorageFolder savedPicturesFolder = null;
                        var folders = await saveFolder.GetFoldersAsync();
                        foreach(var folder in folders)
                        {
                            if (folder.DisplayName.Equals("Saved Pictures"))
                            {
                                savedPicturesFolder = folder;
                            }
                        }

                        // If not found create it.
                        if(savedPicturesFolder == null)
                        {
                            savedPicturesFolder = await saveFolder.CreateFolderAsync("Saved Pictures");
                        }

                        // Write the file.
                        var file = await savedPicturesFolder.CreateFileAsync($"Baconit Saved Image {DateTime.Now:MM-dd-yy H.mm.ss}.jpg");
                        using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            await RandomAccessStream.CopyAndCloseAsync(response.ImageStream.GetInputStreamAt(0), fileStream.GetOutputStreamAt(0));
                        }

                        // Tell the user
                        _baconMan.MessageMan.ShowMessageSimple("Image Saved", "You can find the image(s) in the 'Saved Pictures' folder in your photos library.");
                    }
                    catch(Exception ex)
                    {
                        TelemetryManager.ReportUnexpectedEvent(this, "FailedToSaveImageLocallyCallback", ex);
                        _baconMan.MessageMan.DebugDia("failed to save image locally in callback", ex);
                    }
                };
                QueueImageRequest(request);
            }
            catch (Exception e)
            {
                TelemetryManager.ReportUnexpectedEvent(this, "FailedToSaveImageLocally", e);
                _baconMan.MessageMan.DebugDia("failed to save image locally", e);
            }
        }

        /// <summary>
        /// Tries to get an image url from any given url. Returns empty if it fails.
        /// </summary>
        /// <param name="postUrl"></param>
        /// <returns></returns>
        public static string GetImageUrl(string postUrl)
        {
            // Send the url to lower, but we need both because some websites
            // have case sensitive urls.
            var postUrlLower = postUrl.ToLower();

            // Do a simple check.
            if (postUrlLower.EndsWith(".png") || postUrlLower.EndsWith(".jpg") || postUrlLower.EndsWith(".bmp"))
            {
                return postUrl;
            }

            // We also should look for something like quinn.com/image/hotStuff.jpg?argument=arg
            // But we must be careful not to just match anything with png or jpg in it.
            var postOfLastSlash = postUrlLower.LastIndexOf('/');
            if(postOfLastSlash != -1)
            {
                // Check if we can find anything in the ending string.
                if (postUrlLower.Contains(".png?") || postUrlLower.Contains(".jpg?") || postUrlLower.Contains(".bmp?"))
                {
                    return postUrl;
                }
            }

            // See if it is imgur
            if (postUrlLower.Contains("imgur.com/"))
            {
                // Fix for albums, let albums go to the web page handler for now.
                if (postUrlLower.Contains("imgur.com/a/") || postUrlLower.Contains("imgur.com/gallery/"))
                {
                    return null;
                }

                // See if we can get an image out of a url with no extension.
                // #todo, is there  a better way?
                var last = postUrl.LastIndexOf('/');
                if (last == -1)
                {
                    return string.Empty;
                }

                return "http://i.imgur.com/" + postUrl.Substring(last + 1) + ".jpg";
            }

            // See if we can get a qkme.me image
            if (postUrlLower.Contains("qkme.me/"))
            {
                // Try to parse the URL to get the image
                var last = postUrl.LastIndexOf('/');
                var lastQues = postUrl.LastIndexOf('?');
                if (last != -1 && lastQues != -1)
                {
                    return "http://i.qkme.me/" + postUrl.Substring(last + 1, lastQues - last - 1) + ".jpg";
                }

                if (last != -1)
                {
                    return "http://i.qkme.me/" + postUrl.Substring(last + 1) + ".jpg";
                }

                // We failed
                return string.Empty;
            }

            // Try to get a quick meme image
            if (!postUrlLower.Contains("quickmeme.com/")) return string.Empty;
            {
                // Try to parse the URL to get the image
                var last = postUrl.LastIndexOf('/');
                if (last <= 0)
                {
                    // We failed
                    return string.Empty;
                }

                var secondSlash = postUrl.LastIndexOf('/', last - 1);
                if (last != -1 && secondSlash != -1)
                {
                    return "http://i.qkme.me/" + postUrl.Substring(secondSlash + 1, last - secondSlash - 1) + ".jpg";
                }

                if (last != -1)
                {
                    return "http://i.qkme.me/" + postUrl.Substring(last + 1) + ".jpg";
                }
            }

            // We can't get an image, return null
            return string.Empty;
        }
    }
}
