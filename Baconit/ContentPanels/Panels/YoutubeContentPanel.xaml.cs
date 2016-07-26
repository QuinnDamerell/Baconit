using Baconit.Interfaces;
using MyToolkit.Multimedia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class YoutubeContentPanel : UserControl, IContentPanel
    {
        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        IContentPanelBaseInternal m_base;

        /// <summary>
        /// Holds a ref to the media element that is playing.
        /// </summary>
        MediaElement m_youTubeVideo;

        /// <summary>
        /// Holds the request to not sleep the computer while a video is playing.
        /// </summary>
        DisplayRequest m_displayRequest;

        public YoutubeContentPanel(IContentPanelBaseInternal panelBase)
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
            // Note! We can't do the full Uri get because it relays on an Internet request and
            // we can't lose the time for this quick check. If we can get the youtube id assume we are good.

            // See if we can get a link
            return !String.IsNullOrWhiteSpace(TryToGetYouTubeId(source));
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
            // Since this can be costly kick it off to a background thread so we don't do work
            // as we are animating.
            Task.Run(async () =>
            {
                // Get the video Uri
                YouTubeUri youTubeUri = await GetYouTubeVideoUrl(m_base.Source);

                // Back to the UI thread with pri
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    if (youTubeUri == null)
                    {
                        // If we failed fallback to the browser.
                        m_base.FireOnFallbackToBrowser();
                       
                        return;
                    }

                    // Setup the video
                    m_youTubeVideo = new MediaElement();
                    m_youTubeVideo.AreTransportControlsEnabled = true;
                    m_youTubeVideo.TransportControls.IsCompact = true;
                    m_youTubeVideo.CurrentStateChanged += MediaElement_CurrentStateChanged;
                    m_youTubeVideo.Source = youTubeUri.Uri;
                    ui_contentRoot.Children.Add(m_youTubeVideo); 
              



                });
            });
        }

        /// <summary>
        /// Fired when we should destroy our content.
        /// </summary>
        public void OnDestroyContent()
        {
            // Delete the video
            if (m_youTubeVideo != null)
            {
                m_youTubeVideo.CurrentStateChanged -= MediaElement_CurrentStateChanged;
                m_youTubeVideo.Stop();
                m_youTubeVideo.Source = null;
            }

            // Clear the root
            ui_contentRoot.Children.Clear();

            // Null the object
            m_youTubeVideo = null;
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
            // If we are not visible and still have a video
            // pause it.
            if(m_youTubeVideo != null && !isVisible)
            {
                m_youTubeVideo.Pause();
            }
        }

        #endregion

        #region Media Controls

        /// <summary>
        /// Fired when the media state changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MediaElement_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            // Check to hide the loading.
            if (m_youTubeVideo.CurrentState != MediaElementState.Opening)
            {
                // When we get to the state paused hide loading.
                if (m_base.IsLoading)
                {
                    m_base.FireOnLoading(false);
                }
            }

            // If we are playing request for the screen not to turn off.
            if (m_youTubeVideo.CurrentState == MediaElementState.Playing)
            {
                if (m_displayRequest == null)
                {
                    m_displayRequest = new DisplayRequest();
                    m_displayRequest.RequestActive();
                }
            }
            else
            {
                // If anything else happens and we have a current request remove it.
                if (m_displayRequest != null)
                {
                    m_displayRequest.RequestRelease();
                    m_displayRequest = null;
                }
            }
        }

        #endregion

        #region Youtube Id

        /// <summary>
        /// Tries to get a youtube link from a post. If it fails
        /// it returns null.
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        private static async Task<YouTubeUri> GetYouTubeVideoUrl(ContentPanelSource source)
        {
            if (String.IsNullOrWhiteSpace(source.Url))
            {
                return null;
            }

            try
            {
                // Try to find the ID
                string youtubeVideoId = TryToGetYouTubeId(source);

                if (!String.IsNullOrWhiteSpace(youtubeVideoId))
                {
                    // We found it!
                    // #todo make this quality an option dependent on device!
                    return await YouTube.GetVideoUriAsync(youtubeVideoId, YouTubeQuality.QualityMedium);
                }
            }
            catch (Exception)
            {
              
            }

            return null;
        }

        /// <summary>
        /// Attempts to get a youtube id from a url.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private static string TryToGetYouTubeId(ContentPanelSource source)
        {
            if (String.IsNullOrWhiteSpace(source.Url))
            {
                return null;
            }

            try
            {
                // Try to find the ID
                string youtubeVideoId = String.Empty;
                string urlLower = source.Url.ToLower();
                if (urlLower.Contains("youtube.com"))
                {
                    // Check for an attribution link
                    int attribution = urlLower.IndexOf("attribution_link?");
                    if (attribution != -1)
                    {
                        // We need to parse out the video id
                        // looks like this attribution_link?a=bhvqtDGQD6s&amp;u=%2Fwatch%3Fv%3DrK0D1ehO7CA%26feature%3Dshare
                        int uIndex = urlLower.IndexOf("u=", attribution);
                        string encodedUrl = source.Url.Substring(uIndex + 2);
                        string decodedUrl = WebUtility.UrlDecode(encodedUrl);
                        urlLower = decodedUrl.ToLower();
                        // At this point urlLower should be something like "v=jfkldfjl&feature=share"
                    }

                    int beginId = urlLower.IndexOf("v=");
                    int endId = urlLower.IndexOf("&", beginId);
                    if (beginId != -1)
                    {
                        if (endId == -1)
                        {
                            endId = urlLower.Length;
                        }
                        // Important! Since this might be case sensitive use the original url!
                        beginId += 2;
                        youtubeVideoId = source.Url.Substring(beginId, endId - beginId);
                    }
                }
                else if (urlLower.Contains("youtu.be"))
                {
                    int domain = urlLower.IndexOf("youtu.be");
                    int beginId = urlLower.IndexOf("/", domain);
                    int endId = urlLower.IndexOf("?", beginId);
                    // If we can't find a ? search for a &
                    if (endId == -1)
                    {
                        endId = urlLower.IndexOf("&", beginId);
                    }

                    if (beginId != -1)
                    {
                        if (endId == -1)
                        {
                            endId = urlLower.Length;
                        }
                        // Important! Since this might be case sensitive use the original url!
                        beginId++;
                        youtubeVideoId = source.Url.Substring(beginId, endId - beginId);
                    }
                }

                return youtubeVideoId;
            }
            catch (Exception)
            {
               
            }
            return null;
        }

        #endregion
    }
}
