using BaconBackend.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
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
        private const int MAX_RUNNING_IMAGE_REQUESTS = 5;

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
                add { m_onRequestComplete.Add(value); }
                remove { m_onRequestComplete.Remove(value); }
            }
            internal SmartWeakEvent<EventHandler<ImageManagerResponseEventArgs>> m_onRequestComplete = new SmartWeakEvent<EventHandler<ImageManagerResponseEventArgs>>();
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
            public bool Success = false;

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
            public ImageManagerRequest Context;
            public bool isServicing = false;
        }

        //
        // Private Vars
        //
        BaconManager m_baconMan;
        List<ImageManagerRequestInternal> m_requestList = new List<ImageManagerRequestInternal>();

        public ImageManager(BaconManager baconMan)
        {
            m_baconMan = baconMan;
        }

        /// <summary>
        /// Queues a new image request. The request will be taken care of by an async operation.
        /// </summary>
        /// <param name="orgionalRequest">The image request</param>
        public void QueueImageRequest(ImageManagerRequest orgionalRequest)
        {
            // Validate
            if(String.IsNullOrWhiteSpace(orgionalRequest.Url))
            {
                throw new Exception("You must supply both a ULR and callback");
            }

            ImageManagerRequestInternal serviceRequest = null;
            // Lock the list
            lock(m_requestList)
            {
                // Add the current request to the back of the list.
                m_requestList.Add(new ImageManagerRequestInternal(orgionalRequest));

                // Check if we should make the request now or not. If we don't
                // when a request finishes it will pick up the new request.
                for (int i = 0; i < MAX_RUNNING_IMAGE_REQUESTS; i++)
                {
                    if (m_requestList.Count > i && !m_requestList[i].isServicing)
                    {
                        serviceRequest = m_requestList[i];
                        serviceRequest.isServicing = true;
                        break;
                    }
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
            ImageManagerRequestInternal currentRequest = startingRequestedContext;
            while (currentRequest != null)
            {
                // Service the request in a try catch
                try
                {
                    string fileName = MakeFileNameFromUrl(currentRequest.Context.Url);

                    // First check if we have the file
                    if (m_baconMan.CacheMan.HasFileCached(fileName))
                    {
                        // If we have it cached pull it from there
                    }
                    else
                    {
                        // If not we have to get the image
                        IBuffer imgBuffer = await m_baconMan.NetworkMan.MakeRawGetRequest(currentRequest.Context.Url);

                        // Turn the stream into an image
                        InMemoryRandomAccessStream imageStream = new InMemoryRandomAccessStream();
                        Stream readStream = imgBuffer.AsStream();
                        Stream writeStream = imageStream.AsStreamForWrite();

                        // Copy the buffer
                        // #todo perf - THERE HAS TO BE A BTTER WAY TO DO THIS.
                        readStream.CopyTo(writeStream);
                        writeStream.Flush();

                        // Seek to the start.
                        imageStream.Seek(0);

                        // Create a response
                        ImageManagerResponseEventArgs response = new ImageManagerResponseEventArgs()
                        {
                            ImageStream = imageStream,
                            Request = currentRequest.Context,
                            Success = true
                        };

                        // Fire the callback
                        currentRequest.Context.m_onRequestComplete.Raise(currentRequest.Context, response);
                    }
                }
                catch (Exception e)
                {
                    // Report the error
                    m_baconMan.MessageMan.DebugDia("Error getting image", e);                 

                    // Create a response
                    ImageManagerResponseEventArgs response = new ImageManagerResponseEventArgs()
                    {
                        Request = currentRequest.Context,
                        Success = false
                    };

                    // Send the response
                    currentRequest.Context.m_onRequestComplete.Raise(currentRequest.Context, response);
                }

                // Once we are done, check to see if there is another we should service.
                lock (m_requestList)
                {
                    // Remove the current request.
                    m_requestList.Remove(currentRequest);

                    // Kill the current request
                    currentRequest = null;

                    // Check if there is another request we should service now.
                    for (int i = 0; i < MAX_RUNNING_IMAGE_REQUESTS; i++)
                    {
                        if (m_requestList.Count > i && !m_requestList[i].isServicing)
                        {
                            currentRequest = m_requestList[i];
                            currentRequest.isServicing = true;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Converts the image file name into a file system safe string.
        /// </summary>
        /// <param name="url">The url</param>
        /// <returns>File system safe string</returns>
        private string MakeFileNameFromUrl(string url)
        {
            StringBuilder strBuilder = new StringBuilder();
            foreach(char c in url)
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
            if(!String.IsNullOrWhiteSpace(str)
                && str.Contains("http"))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets an image and saves it locally.
        /// </summary>
        /// <param name="postUrl"></param>
        public void SaveImageLocally(string postUrl)
        {
            try
            {
                string imageUrl = GetImageUrl(postUrl);

                // Fire off a request for the image.
                Random rand = new Random((int)DateTime.Now.Ticks);
                ImageManagerRequest request = new ImageManagerRequest()
                {
                    ImageId = rand.NextDouble().ToString(),
                    Url = imageUrl
                };

                // Set the callback
                request.OnRequestComplete += async (object sender, ImageManager.ImageManagerResponseEventArgs response) =>
                {
                    try
                    {
                        // If success
                        if(response.Success)
                        {
                            // Get the photos library
                            StorageLibrary myPictures = await Windows.Storage.StorageLibrary.GetLibraryAsync(Windows.Storage.KnownLibraryId.Pictures);

                            // Get the save folder
                            StorageFolder saveFolder = myPictures.SaveFolder;

                            // Try to find the saved pictures folder
                            StorageFolder savedPicturesFolder = null;
                            IReadOnlyList<StorageFolder> folders = await saveFolder.GetFoldersAsync();
                            foreach(StorageFolder folder in folders)
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
                            StorageFile file = await savedPicturesFolder.CreateFileAsync($"Baconit Saved Image {DateTime.Now.ToString("MM-dd-yy H.mm.ss")}.jpg");
                            using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                            {
                                await RandomAccessStream.CopyAndCloseAsync(response.ImageStream.GetInputStreamAt(0), fileStream.GetOutputStreamAt(0));
                            }

                            // Tell the user
                            m_baconMan.MessageMan.ShowMessageSimple("Image Saved", "You can find the image in the 'Saved Pictures' folder in your photos library.");
                        }
                    }
                    catch(Exception ex)
                    {
                        m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToSaveImageLocallyCallback", ex);
                        m_baconMan.MessageMan.DebugDia("failed to save image locally in callback", ex);
                    }
                };
                QueueImageRequest(request);
            }
            catch (Exception e)
            {
                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToSaveImageLocally", e);
                m_baconMan.MessageMan.DebugDia("failed to save image locally", e);
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
            string postUrlLower = postUrl.ToLower();

            // Do a simple check.
            if (postUrlLower.EndsWith(".png") || postUrlLower.EndsWith(".jpg") || postUrlLower.EndsWith(".bmp"))
            {
                return postUrl;
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
                int last = postUrl.LastIndexOf('/');
                if (last == -1)
                {
                    return String.Empty;
                }

                return "http://i.imgur.com/" + postUrl.Substring(last + 1) + ".jpg";
            }

            // See if we can get a qkme.me image
            if (postUrlLower.Contains("qkme.me/"))
            {
                // Try to parse the URL to get the image
                int last = postUrl.LastIndexOf('/');
                int lastQues = postUrl.LastIndexOf('?');
                if (last != -1 && lastQues != -1)
                {
                    return "http://i.qkme.me/" + postUrl.Substring(last + 1, lastQues - last - 1) + ".jpg";
                }
                else if (last != -1)
                {
                    return "http://i.qkme.me/" + postUrl.Substring(last + 1) + ".jpg";
                }

                // We failed
                return String.Empty;
            }

            // Try to get a quick meme image
            if (postUrlLower.Contains("quickmeme.com/"))
            {
                // Try to parse the URL to get the image
                int last = postUrl.LastIndexOf('/');
                if (last <= 0)
                {
                    // We failed
                    return String.Empty;
                }

                int secondSlash = postUrl.LastIndexOf('/', last - 1);
                if (last != -1 && secondSlash != -1)
                {
                    return "http://i.qkme.me/" + postUrl.Substring(secondSlash + 1, last - secondSlash - 1) + ".jpg";
                }
                else if (last != -1)
                {
                    return "http://i.qkme.me/" + postUrl.Substring(last + 1) + ".jpg";
                }
            }

            // We can't get an image, return null
            return String.Empty;
        }
    }
}
