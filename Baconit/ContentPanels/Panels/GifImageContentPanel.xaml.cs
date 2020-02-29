using Baconit.Interfaces;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using BaconBackend.Managers;

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class GifImageContentPanel : UserControl, IContentPanel
    {
        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        private readonly IContentPanelBaseInternal _mBase;

        /// <summary>
        /// Indicates if we should be playing or not.
        /// </summary>
        private bool _mShouldBePlaying;

        /// <summary>
        /// Holds a reference to the video we are playing.
        /// </summary>
        private MediaElement _mGifVideo;

        public GifImageContentPanel(IContentPanelBaseInternal panelBase)
        {
            InitializeComponent();
            _mBase = panelBase;
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a post.
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        static public bool CanHandlePost(ContentPanelSource source)
        {
            // See if we can find a imgur, gfycat gif, or a normal gif we can send to gfycat.
            if (string.IsNullOrWhiteSpace(GetImgurUrl(source.Url)) && string.IsNullOrWhiteSpace(GetGfyCatApiUrl(source.Url)) && string.IsNullOrWhiteSpace(GetGifUrl(source.Url)))
            {
                return false;
            }
            return true;
        }

        #region IContentPanel

        /// <summary>
        /// Indicates how large the panel is in memory.
        /// </summary>
        // #todo can we figure this out?
        public PanelMemorySizes PanelMemorySize => PanelMemorySizes.Medium;

        /// <summary>
        /// Fired when we should load the content.
        /// </summary>
        /// <param name="source"></param>
        public void OnPrepareContent()
        {
            // Run the rest on a background thread.
            Task.Run(async () =>
            {
                // Try to get the imgur url
                var gifUrl = GetImgurUrl(_mBase.Source.Url);

                // If that failed try to get a url from GfyCat
                if (gifUrl.Equals(string.Empty))
                {
                    // We have to get it from gfycat
                    gifUrl = await GetGfyCatGifUrl(GetGfyCatApiUrl(_mBase.Source.Url));
                }

                // Since some of this can be costly, delay the work load until we aren't animating.
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    // If we didn't get anything something went wrong.
                    if (string.IsNullOrWhiteSpace(gifUrl))
                    {
                        _mBase.FireOnFallbackToBrowser();
                        TelemetryManager.ReportUnexpectedEvent(this, "FailedToShowGifAfterConfirm");
                        return;
                    }

                    lock(this)
                    {
                        // Make sure we aren't destroyed.
                        if (_mBase.IsDestoryed)
                        {
                            return;
                        }

                        // Create the media element
                        _mGifVideo = new MediaElement();
                        _mGifVideo.HorizontalAlignment = HorizontalAlignment.Stretch;
                        _mGifVideo.Tapped += OnVideoTapped;
                        _mGifVideo.CurrentStateChanged += OnVideoCurrentStateChanged;
                        _mGifVideo.IsLooping = true;

                        // Set the source
                        _mGifVideo.Source = new Uri(gifUrl, UriKind.Absolute);
                        _mGifVideo.Play();

                        // Add the video to the root                    
                        ui_contentRoot.Children.Add(_mGifVideo);
                    }
                });
            });
        }

        /// <summary>
        /// Fired when we should destroy our content.
        /// </summary>
        public void OnDestroyContent()
        {
            lock(this)
            {
                // Destroy the video
                if (_mGifVideo != null)
                {
                    _mGifVideo.CurrentStateChanged -= OnVideoCurrentStateChanged;
                    _mGifVideo.Tapped -= OnVideoTapped;
                    _mGifVideo.Stop();
                    _mGifVideo = null;
                }

                // Clear vars
                _mShouldBePlaying = false;

                // Clear the UI
                ui_contentRoot.Children.Clear();
            } 
        }

        /// <summary>
        /// Fired when a new host has been added.
        /// </summary>
        public void OnHostAdded()
        {
            // Ignore for now.
        }

        /// <summary>
        /// Fired when this post becomes visible
        /// </summary>
        public void OnVisibilityChanged(bool isVisible)
        {
            lock (this)
            {
                // Set that we should be playing
                _mShouldBePlaying = isVisible;

                if (_mGifVideo != null)
                {
                    // Call the action. If we are already playing or paused this
                    // will do nothing.
                    if(isVisible)
                    {
                        _mGifVideo.Play();
                    }
                    else
                    {
                        _mGifVideo.Pause();
                    }
                }
            }
        }

        #endregion

        #region Video Playback

        /// <summary>
        /// Hides the loading and fades in the video when it start playing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnVideoCurrentStateChanged(object sender, RoutedEventArgs e)
        {
            // If we start playing and the loading UI isn't hidden do so.
            if (_mBase.IsLoading && _mGifVideo.CurrentState == MediaElementState.Playing)
            {
                _mBase.FireOnLoading(false);
            }

            // Make sure if we are playing that we should be (that we are actually visible)
            if (!_mShouldBePlaying && _mGifVideo.CurrentState == MediaElementState.Playing)
            {
                _mGifVideo.Pause();
            }            
        }

        /// <summary>
        /// Fired when the gif is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnVideoTapped(object sender, TappedRoutedEventArgs e)
        {
            if (_mGifVideo != null)
            {
                if (_mGifVideo.CurrentState == MediaElementState.Playing)
                {
                    _mGifVideo.Pause();
                }
                else
                {
                    _mGifVideo.Play();
                }
            }
        }

        #endregion

        #region Gif Url Parsing

        /// <summary>
        /// Tries to get a Imgur gif url
        /// </summary>
        /// <param name="postUrl"></param>
        /// <returns></returns>
        private static string GetImgurUrl(string postUrl)
        {
            // Send the url to lower, but we need both because some websites
            // have case sensitive urls.
            var postUrlLower = postUrl.ToLower();

            // Check for imgur gifv
            if (postUrlLower.Contains(".gifv") && postUrlLower.Contains("imgur.com"))
            {
                // If the link is imgur, replace the .gifv with a .mp4 and we should get a video back.
                return postUrl.Replace(".gifv", ".mp4");
            }
            // Check for imgur gif
            if (postUrlLower.Contains(".gif") && postUrlLower.Contains("imgur.com"))
            {
                // If the link is imgur, replace the .gifv with a .mp4 and we should get a video back.
                return postUrl.Replace(".gif", ".mp4");
            }

            return string.Empty;
        }

        /// <summary>
        /// Attempts to find a .gif in the url.
        /// </summary>
        /// <param name="postUrl"></param>
        /// <returns></returns>
        private static string GetGifUrl(string postUrl)
        {
            // Send the url to lower, but we need both because some websites
            // have case sensitive urls.
            var postUrlLower = postUrl.ToLower();

            var lastSlash = postUrlLower.LastIndexOf('/');
            if(lastSlash != -1)
            {
                var urlEnding = postUrlLower.Substring(lastSlash);
                if(urlEnding.Contains(".gif") || urlEnding.Contains(".gif?"))
                {
                    return postUrl;
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Tries to get a gfy cat api url.
        /// </summary>
        /// <param name="postUrl"></param>
        /// <returns></returns>
        private static string GetGfyCatApiUrl(string postUrl)
        {
            var uri = new Uri(postUrl);
            var authority = uri.Authority.Replace("/", string.Empty).ToLowerInvariant();
            var segment = uri.LocalPath.Substring(1);

            return authority.Contains("gfycat") ? $"https://api.gfycat.com/v1/gfycats/{segment}" : string.Empty;
        }

        // Disable this annoying warning.
#pragma warning disable CS0649

        private class GfyCatDataContainer
        {
            [JsonProperty(PropertyName = "gfyItem")]
            public GfyItem Item;
        }

        private class GfyItem
        {
            [JsonProperty(PropertyName = "mp4Url")]
            public string Mp4Url;
        }

#pragma warning restore

        /// <summary>
        /// Gets a video url from gfycat
        /// </summary>
        /// <param name="apiUrl"></param>
        /// <returns></returns>
        private async Task<string> GetGfyCatGifUrl(string apiUrl)
        {
            // Return if we have nothing.
            if (apiUrl.Equals(string.Empty))
            {
                return string.Empty;
            }

            try
            {
                // Make the call
                var webResult = await NetworkManager.MakeGetRequest(apiUrl);

                // Get the input stream and json reader.
                // NOTE!! We are really careful not to use a string here so we don't have to allocate a huge string.
                var inputStream = await webResult.ReadAsInputStreamAsync();
                using (var reader = new StreamReader(inputStream.AsStreamForRead()))
                using (JsonReader jsonReader = new JsonTextReader(reader))
                {           
                    // Parse the Json as an object
                    var serializer = new JsonSerializer();
                    var gfyData = await Task.Run(() => serializer.Deserialize<GfyCatDataContainer>(jsonReader));

                    // Validate the response
                    var mp4Url = gfyData.Item.Mp4Url;
                    if (string.IsNullOrWhiteSpace(mp4Url))
                    {
                        throw new Exception("Gfycat response failed to parse");
                    }

                    // Return the url
                    return mp4Url;
                }     
            }
            catch (Exception e)
            {
                App.BaconMan.MessageMan.DebugDia("failed to get image from gfycat", e);
                TelemetryManager.ReportUnexpectedEvent(this, "FaileGfyCatApiCall", e);
            }

            return string.Empty;
        }

        // Disable this annoying warning.
#pragma warning disable CS0649

        private class GfyCatConversionData
        {
            [JsonProperty(PropertyName = "mp4Url")]
            public string Mp4Url;
        }

#pragma warning restore

        /// <summary>
        /// Uses GfyCat to convert a normal .gif into a video
        /// </summary>
        /// <param name="gifUrl"></param>
        /// <returns></returns>
        private async Task<string> ConvertGifUsingGfycat(string gifUrl)
        {
            // Return if we have nothing.
            if (gifUrl.Equals(string.Empty))
            {
                return string.Empty;
            }

            try
            {
                var url = $"https://upload.gfycat.com/transcode?fetchUrl={gifUrl}";
                // Make the call
                var webResult = await NetworkManager.MakeGetRequest("https://upload.gfycat.com/transcode?fetchUrl="+gifUrl);

                // Get the input stream and json reader.
                // NOTE!! We are really careful not to use a string here so we don't have to allocate a huge string.
                var inputStream = await webResult.ReadAsInputStreamAsync();
                using (var reader = new StreamReader(inputStream.AsStreamForRead()))
                using (JsonReader jsonReader = new JsonTextReader(reader))
                {
                    // Parse the Json as an object
                    var serializer = new JsonSerializer();
                    var gfyData = await Task.Run(() => serializer.Deserialize<GfyCatConversionData>(jsonReader));

                    // Validate the response
                    var mp4Url = gfyData.Mp4Url;
                    if (string.IsNullOrWhiteSpace(mp4Url))
                    {
                        throw new Exception("Gfycat failed to convert");
                    }

                    // Return the url
                    return mp4Url;
                }
            }
            catch (Exception e)
            {
                App.BaconMan.MessageMan.DebugDia("failed to convert gif via gfycat", e);
                TelemetryManager.ReportUnexpectedEvent(this, "GfyCatConvertFailed", e);
            }

            return string.Empty;
        }

        #endregion
    }
}
