using BaconBackend.DataObjects;
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
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;

namespace Baconit.FlipViewControls
{
    public sealed partial class GifImageFliplControl : UserControl, IFlipViewContentControl
    {
        IFlipViewContentHost m_host;
        MediaElement m_gifVideo;
        bool m_loadingHidden = false;
        bool m_shouldBePlaying = false;
        bool m_isDestoryed = false;

        public GifImageFliplControl(IFlipViewContentHost host)
        {
            m_host = host;
            this.InitializeComponent();
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a post.
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        static public bool CanHandlePost(Post post)
        {
            // See if we can find a imgur or gfycat gif
            if (String.IsNullOrWhiteSpace(GetImgurUrl(post.Url)) && String.IsNullOrWhiteSpace(GetGfyCatApiUrl(post.Url)))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Called when the flip view consent should shown.
        /// </summary>
        /// <param name="url"></param>
        public void OnPrepareContent(Post post)
        {
            m_shouldBePlaying = false;
            m_loadingHidden = false;

            // Show loading
            m_host.ShowLoading();

            // Run the rest on a background thread.
            Task.Run(async () =>
            {
                // Try to get the imgur url
                string gifUrl = GetImgurUrl(post.Url);

                // If that failed try to get a url from GfyCat
                if (gifUrl.Equals(String.Empty))
                {
                    // We have to get it from gyfcat
                    gifUrl = await GetGfyCatGifUrl(GetGfyCatApiUrl(post.Url));
                }               

                // Since some of this can be costly, delay the work load until we aren't animating.
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    // Make sure we aren't destroyed.
                    if (m_isDestoryed)
                    {
                        return;
                    }

                    // If we didn't get anything something went wrong.
                    if (String.IsNullOrWhiteSpace(gifUrl))
                    {
                        m_host.FallbackToWebBrowser(post);
                        App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToShowGifAfterConfirm");
                        return;
                    }

                    // Create the media element
                    m_gifVideo = new MediaElement();
                    m_gifVideo.HorizontalAlignment = HorizontalAlignment.Stretch;
                    m_gifVideo.Tapped += OnVideoTapped;
                    m_gifVideo.CurrentStateChanged += OnVideoCurrentStateChanged;
                    m_gifVideo.Source = new Uri(gifUrl, UriKind.Absolute);
                    m_gifVideo.Play();
                    m_gifVideo.IsLooping = true;
                    ui_contentRoot.Children.Add(m_gifVideo);
                });
            });    
        }

        /// <summary>
        /// Called when the  post actually becomes visible
        /// </summary>
        public void OnVisible()
        {
            lock(this)
            {
                // Set that we should be playing
                m_shouldBePlaying = true;

                if (m_gifVideo != null)
                {
                    // Call play. If we are playing this will do nothing, if we are paused it will start playing.
                    m_gifVideo.Play();
                }
            }
        }

        /// <summary>
        /// Called when the flip view content should be destroyed.
        /// </summary>
        public void OnDestroyContent()
        {
            // Set that we are destroyed
            m_isDestoryed = true;

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

        /// <summary>
        /// Hides the loading and fades in the video when it start playing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnVideoCurrentStateChanged(object sender, RoutedEventArgs e)
        {
            lock(this)
            {
                // If we start playing and the loading UI isn't hidden do so.
                if(!m_loadingHidden && m_gifVideo.CurrentState == MediaElementState.Playing)
                {
                    m_host.HideLoading();
                    ui_storyContentRoot.Begin();
                    m_loadingHidden = true;
                }

                // Make sure if we are playing that we should be (that we are actually visible)
                if (!m_shouldBePlaying && m_gifVideo.CurrentState == MediaElementState.Playing)
                {
                    m_gifVideo.Pause();
                }
            }
        }

        /// <summary>
        /// Fired when the gif is tapped
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnVideoTapped(object sender, TappedRoutedEventArgs e)
        {
            if(m_gifVideo != null)
            {
                if(m_gifVideo.CurrentState == MediaElementState.Playing)
                {
                    m_gifVideo.Pause();
                }
                else
                {
                    m_gifVideo.Play();
                }
            }
        }

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
            if(apiUrl.Equals(String.Empty))
            {
                return String.Empty;
            }

            try
            {
                // Make the call
                IHttpContent response = await App.BaconMan.NetworkMan.MakeGetRequest(apiUrl);
                string jsonReponse = await response.ReadAsStringAsync();

                // Parse the data
                GfyCatDataContainer gfyData = JsonConvert.DeserializeObject<GfyCatDataContainer>(jsonReponse);

                // Validate the repsonse
                string mp4Url = gfyData.item.Mp4Url;
                if(String.IsNullOrWhiteSpace(mp4Url))
                {
                    throw new Exception("Gfycat response failed to parse");
                }

                // Return the url
                return mp4Url;
            }
            catch(Exception e)
            {
                App.BaconMan.MessageMan.DebugDia("failed to get image from gfycat", e);
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FaileGfyCatApiCall", e);
            }

            return String.Empty;
        }
    }
}
