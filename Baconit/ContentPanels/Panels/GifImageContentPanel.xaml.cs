using Baconit.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class GifImageContentPanel : UserControl, IContentPanel
    {
        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        IContentPanelBaseInternal m_base;

        /// <summary>
        /// Indicates if we should be playing or not.
        /// </summary>
        bool m_shouldBePlaying = false;

        /// <summary>
        /// Holds a reference to the video we are playing.
        /// </summary>
        MediaElement m_gifVideo;

        public GifImageContentPanel(IContentPanelBaseInternal panelBase)
        {
            this.InitializeComponent();
            m_base = panelBase;
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a post.
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        static public bool CanHandlePost(ContentPanelSource source)
        {
            // See if we can find a imgur, gfycat gif, or a normal gif we can send to gfycat.
            if (String.IsNullOrWhiteSpace(GetImgurUrl(source.Url)) && String.IsNullOrWhiteSpace(GetGfyCatApiUrl(source.Url)) && String.IsNullOrWhiteSpace(GetGifUrl(source.Url)))
            {
                return false;
            }
            return true;
        }

        #region IContentPanel

        /// <summary>
        /// Indicates how large the panel is in memory.
        /// </summary>
        public PanelMemorySizes PanelMemorySize
        {
            get
            {
                // #todo can we figure this out?
                return PanelMemorySizes.Medium;
            }
        }

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
                string gifUrl = GetImgurUrl(m_base.Source.Url);

                // If that failed try to get a url from GfyCat
                if (gifUrl.Equals(String.Empty))
                {
                    // We have to get it from gfycat
                    gifUrl = await GetGfyCatGifUrl(GetGfyCatApiUrl(m_base.Source.Url));

                    if(String.IsNullOrWhiteSpace(gifUrl))
                    {
                        // If these failed it might just be a gif. try to send it to gfycat for conversion.
                        gifUrl = await ConvertGifUsingGfycat(m_base.Source.Url);
                    }
                }

                // Since some of this can be costly, delay the work load until we aren't animating.
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    // If we didn't get anything something went wrong.
                    if (String.IsNullOrWhiteSpace(gifUrl))
                    {
                        m_base.FireOnFallbackToBrowser();
                        App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToShowGifAfterConfirm");
                        return;
                    }

                    lock(this)
                    {
                        // Make sure we aren't destroyed.
                        if (m_base.IsDestoryed)
                        {
                            return;
                        }

                        // Create the media element
                        m_gifVideo = new MediaElement();
                        m_gifVideo.HorizontalAlignment = HorizontalAlignment.Stretch;
                        m_gifVideo.Tapped += OnVideoTapped;
                        m_gifVideo.CurrentStateChanged += OnVideoCurrentStateChanged;
                        m_gifVideo.IsLooping = true;

                        // Set the source
                        m_gifVideo.Source = new Uri(gifUrl, UriKind.Absolute);
                        m_gifVideo.Play();

                        // Add the video to the root                    
                        ui_contentRoot.Children.Add(m_gifVideo);
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
                if (m_gifVideo != null)
                {
                    m_gifVideo.CurrentStateChanged -= OnVideoCurrentStateChanged;
                    m_gifVideo.Tapped -= OnVideoTapped;
                    m_gifVideo.Stop();
                    m_gifVideo = null;
                }

                // Clear vars
                m_shouldBePlaying = false;

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
                m_shouldBePlaying = isVisible;

                if (m_gifVideo != null)
                {
                    // Call the action. If we are already playing or paused this
                    // will do nothing.
                    if(isVisible)
                    {
                        m_gifVideo.Play();
                    }
                    else
                    {
                        m_gifVideo.Pause();
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
            if (m_base.IsLoading && m_gifVideo.CurrentState == MediaElementState.Playing)
            {
                m_base.FireOnLoading(false);
            }

            // Make sure if we are playing that we should be (that we are actually visible)
            if (!m_shouldBePlaying && m_gifVideo.CurrentState == MediaElementState.Playing)
            {
                m_gifVideo.Pause();
            }            
        }

        /// <summary>
        /// Fired when the gif is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnVideoTapped(object sender, TappedRoutedEventArgs e)
        {
            if (m_gifVideo != null)
            {
                if (m_gifVideo.CurrentState == MediaElementState.Playing)
                {
                    m_gifVideo.Pause();
                }
                else
                {
                    m_gifVideo.Play();
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
            string postUrlLower = postUrl.ToLower();

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

            return String.Empty;
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
            string postUrlLower = postUrl.ToLower();

            int lastSlash = postUrlLower.LastIndexOf('/');
            if(lastSlash != -1)
            {
                string urlEnding = postUrlLower.Substring(lastSlash);
                if(urlEnding.Contains(".gif") || urlEnding.Contains(".gif?"))
                {
                    return postUrl;
                }
            }
            return String.Empty;
        }

        /// <summary>
        /// Tries to get a gfy cat api url.
        /// </summary>
        /// <param name="postUrl"></param>
        /// <returns></returns>
        private static string GetGfyCatApiUrl(string postUrl)
        {
            // Send the url to lower, but we need both because some websites
            // have case sensitive urls.
            string postUrlLower = postUrl.ToLower();

            int gifNameStart = postUrlLower.IndexOf("gfycat.com/");

            if (gifNameStart != -1)
            {
                // Get to the end of the domain
                gifNameStart += 11;

                // Look for the end of the gif name
                int gifNameEnd = postUrlLower.IndexOf('/', gifNameStart);
                if (gifNameEnd == -1)
                {
                    gifNameEnd = postUrlLower.Length;
                }

                // Use the original url to get the gif name.
                string gifName = postUrl.Substring(gifNameStart, gifNameEnd - gifNameStart);
                return $"http://gfycat.com/cajax/get/{gifName}";
            }
            return String.Empty;
        }

        // Disable this annoying warning.
#pragma warning disable CS0649

        private class GfyCatDataContainer
        {
            [JsonProperty(PropertyName = "gfyItem")]
            public GfyItem item;
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
            if (apiUrl.Equals(String.Empty))
            {
                return String.Empty;
            }

            try
            {
                // Make the call
                IHttpContent webResult = await App.BaconMan.NetworkMan.MakeGetRequest(apiUrl);

                // Get the input stream and json reader.
                // NOTE!! We are really careful not to use a string here so we don't have to allocate a huge string.
                IInputStream inputStream = await webResult.ReadAsInputStreamAsync();
                using (StreamReader reader = new StreamReader(inputStream.AsStreamForRead()))
                using (JsonReader jsonReader = new JsonTextReader(reader))
                {           
                    // Parse the Json as an object
                    JsonSerializer serializer = new JsonSerializer();
                    GfyCatDataContainer gfyData = await Task.Run(() => serializer.Deserialize<GfyCatDataContainer>(jsonReader));

                    // Validate the response
                    string mp4Url = gfyData.item.Mp4Url;
                    if (String.IsNullOrWhiteSpace(mp4Url))
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
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FaileGfyCatApiCall", e);
            }

            return String.Empty;
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
        /// <param name="apiUrl"></param>
        /// <returns></returns>
        private async Task<string> ConvertGifUsingGfycat(string gifUrl)
        {
            // Return if we have nothing.
            if (gifUrl.Equals(String.Empty))
            {
                return String.Empty;
            }

            try
            {
                // Make the call
                IHttpContent webResult = await App.BaconMan.NetworkMan.MakeGetRequest("https://upload.gfycat.com/transcode?fetchUrl="+gifUrl);

                // Get the input stream and json reader.
                // NOTE!! We are really careful not to use a string here so we don't have to allocate a huge string.
                IInputStream inputStream = await webResult.ReadAsInputStreamAsync();
                using (StreamReader reader = new StreamReader(inputStream.AsStreamForRead()))
                using (JsonReader jsonReader = new JsonTextReader(reader))
                {
                    // Parse the Json as an object
                    JsonSerializer serializer = new JsonSerializer();
                    GfyCatConversionData gfyData = await Task.Run(() => serializer.Deserialize<GfyCatConversionData>(jsonReader));

                    // Validate the response
                    string mp4Url = gfyData.Mp4Url;
                    if (String.IsNullOrWhiteSpace(mp4Url))
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
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "GfyCatConvertFailed", e);
            }

            return String.Empty;
        }

        #endregion
    }
}
