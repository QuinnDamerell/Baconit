using BaconBackend.DataObjects;
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


namespace Baconit.FlipViewControls
{
    public sealed partial class YoutubeFlipControl : UserControl, IFlipViewContentControl
    {
        /// <summary>
        /// Reference to the host
        /// </summary>
        IFlipViewContentHost m_host;

        /// <summary>
        /// The currently displayed post.
        /// </summary>
        Post m_post;

        /// <summary>
        /// Indicates if we have hidden loading yet
        /// </summary>
        bool m_hasHiddenLoading = false;

        /// <summary>
        /// Holds a ref to the media element that is playing.
        /// </summary>
        MediaElement m_youTubeVideo;

        /// <summary>
        /// Holds the request to not sleep the computer while a video is playing.
        /// </summary>
        DisplayRequest m_displayRequest;

        public YoutubeFlipControl(IFlipViewContentHost host)
        {
            this.InitializeComponent();
            m_host = host;
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a post.
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        static public bool CanHandlePost(Post post)
        {
            // Note! We can't do the full Uri get because it relays on an Internet request and
            // we can't lose the time for this quick check. If we can get the youtube id assume we are good.

            // See if we can get a link
            return !String.IsNullOrWhiteSpace(TryToGetYouTubeId(post));
        }

        /// <summary>
        /// Called by the host when we should show content.
        /// </summary>
        /// <param name="post"></param>
        public void OnPrepareContent(Post post)
        {
            // So the loading UI
            m_host.ShowLoading();

            m_post = post;

            // Since this can be costly kick it off to a background thread so we don't do work
            // as we are animating.
            Task.Run(async () =>
            {
                // Get the video Uri
                YouTubeUri youTubeUri = await GetYouTubeVideoUrl(post);

                // Back to the UI thread with pri
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    if (youTubeUri == null)
                    {
                        // If we failed fallback to the browser.
                        m_host.FallbackToWebBrowser(m_post);
                        App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToGetYoutubeVideoAfterSuccess");
                        return;
                    }

                    // Setup the video
                    m_youTubeVideo = new MediaElement();
                    m_youTubeVideo.AutoPlay = false;
                    m_youTubeVideo.AreTransportControlsEnabled = true;
                    m_youTubeVideo.CurrentStateChanged += MediaElement_CurrentStateChanged;
                    m_youTubeVideo.Source = youTubeUri.Uri;
                    ui_contentRoot.Children.Add(m_youTubeVideo);
                });
            });
        }

        /// <summary>
        /// Called when the  post actually becomes visible
        /// </summary>
        public void OnVisible()
        {
            // Ignore for now
        }

        /// <summary>
        /// Called then the content should be killed
        /// </summary>
        public void OnDestroyContent()
        {
            if(m_youTubeVideo != null)
            {
                m_youTubeVideo.CurrentStateChanged -= MediaElement_CurrentStateChanged;
                m_youTubeVideo.Stop();
                m_youTubeVideo.Source = null;
            }
            m_youTubeVideo = null;
            m_post = null;
        }

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
                if (!m_hasHiddenLoading)
                {
                    m_hasHiddenLoading = true;
                    m_host.HideLoading();
                    ui_storyContentRoot.Begin();
                }
            }

            // If we are playing request for the screen not to turn off.
            if (m_youTubeVideo.CurrentState == MediaElementState.Playing)
            {
                if(m_displayRequest == null)
                {
                    m_displayRequest = new DisplayRequest();
                    m_displayRequest.RequestActive();
                }
            }
            else
            {
                // If anything else happens and we have a current request remove it.
                if(m_displayRequest != null)
                {
                    m_displayRequest.RequestRelease();
                    m_displayRequest = null;
                }
            }
        }

        /// <summary>
        /// Tries to get a youtube link from a post. If it fails
        /// it returns null.
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        private static async Task<YouTubeUri> GetYouTubeVideoUrl(Post post)
        {
            if (String.IsNullOrWhiteSpace(post.Url))
            {
                return null;
            }

            try
            {
                // Try to find the ID
                string youtubeVideoId = TryToGetYouTubeId(post);

                if (!String.IsNullOrWhiteSpace(youtubeVideoId))
                {
                    // We found it!
                    // #todo make this quality an option dependent on device!
                    return await YouTube.GetVideoUriAsync(youtubeVideoId, YouTubeQuality.QualityMedium);
                }
            }
            catch (Exception)
            {
                App.BaconMan.TelemetryMan.ReportEvent("YoutubeString", "Failed to find youtube video");
            }

            return null;
        }

        private static string TryToGetYouTubeId(Post post)
        {
            if (String.IsNullOrWhiteSpace(post.Url))
            {
                return null;
            }

            try
            {
                // Try to find the ID
                string youtubeVideoId = String.Empty;
                string postUrl = WebUtility.HtmlDecode(post.Url);
                string urlLower = postUrl.ToLower();
                if (urlLower.Contains("youtube.com"))
                {
                    // Check for an attribution link
                    int attribution = urlLower.IndexOf("attribution_link?");
                    if(attribution != -1)
                    {
                        // We need to parse out the video id
                        // looks like this attribution_link?a=bhvqtDGQD6s&amp;u=%2Fwatch%3Fv%3DrK0D1ehO7CA%26feature%3Dshare
                        int uIndex = urlLower.IndexOf("u=", attribution);
                        string encodedUrl = postUrl.Substring(uIndex + 2);
                        postUrl = WebUtility.UrlDecode(encodedUrl);
                        urlLower = postUrl.ToLower();
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
                        youtubeVideoId = postUrl.Substring(beginId, endId - beginId);
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
                        youtubeVideoId = postUrl.Substring(beginId, endId - beginId);
                    }
                }

                return youtubeVideoId;
            }
            catch (Exception)
            {
                App.BaconMan.TelemetryMan.ReportEvent("YoutubeString", "Failed to find youtube video");
            }
            return null;
        }
    }
}
