using Baconit.Interfaces;
using MyToolkit.Multimedia;
using System;
using System.Net;
using System.Threading.Tasks;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using BaconBackend.Managers;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class YoutubeContentPanel : UserControl, IContentPanel
    {
        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        private readonly IContentPanelBaseInternal _mBase;

        /// <summary>
        /// Holds a ref to the media element that is playing.
        /// </summary>
        private MediaElement _mYouTubeVideo;

        /// <summary>
        /// Holds the request to not sleep the computer while a video is playing.
        /// </summary>
        private DisplayRequest _mDisplayRequest;

        public YoutubeContentPanel(IContentPanelBaseInternal panelBase)
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
            // Note! We can't do the full Uri get because it relays on an Internet request and
            // we can't lose the time for this quick check. If we can get the youtube id assume we are good.

            // See if we can get a link
            return !string.IsNullOrWhiteSpace(TryToGetYouTubeId(source));
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
            // Since this can be costly kick it off to a background thread so we don't do work
            // as we are animating.
            Task.Run(async () =>
            {
                // Get the video Uri
                var youTubeUri = await GetYouTubeVideoUrl(_mBase.Source);

                // Back to the UI thread with pri
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    if (youTubeUri == null)
                    {
                        // If we failed fallback to the browser.
                        _mBase.FireOnFallbackToBrowser();
                        TelemetryManager.ReportUnexpectedEvent(this, "FailedToGetYoutubeVideoAfterSuccess");
                        return;
                    }

                    // Setup the video
                    _mYouTubeVideo = new MediaElement();
                    _mYouTubeVideo.AutoPlay = false;
                    _mYouTubeVideo.AreTransportControlsEnabled = true;
                    _mYouTubeVideo.CurrentStateChanged += MediaElement_CurrentStateChanged;
                    _mYouTubeVideo.Source = youTubeUri.Uri;
                    ui_contentRoot.Children.Add(_mYouTubeVideo);
                });
            });
        }

        /// <summary>
        /// Fired when we should destroy our content.
        /// </summary>
        public void OnDestroyContent()
        {
            // Delete the video
            if (_mYouTubeVideo != null)
            {
                _mYouTubeVideo.CurrentStateChanged -= MediaElement_CurrentStateChanged;
                _mYouTubeVideo.Stop();
                _mYouTubeVideo.Source = null;
            }

            // Clear the root
            ui_contentRoot.Children.Clear();

            // Null the object
            _mYouTubeVideo = null;
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
            if(_mYouTubeVideo != null && !isVisible)
            {
                _mYouTubeVideo.Pause();
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
            if (_mYouTubeVideo.CurrentState != MediaElementState.Opening)
            {
                // When we get to the state paused hide loading.
                if (_mBase.IsLoading)
                {
                    _mBase.FireOnLoading(false);
                }
            }

            // If we are playing request for the screen not to turn off.
            if (_mYouTubeVideo.CurrentState == MediaElementState.Playing)
            {
                if (_mDisplayRequest == null)
                {
                    _mDisplayRequest = new DisplayRequest();
                    _mDisplayRequest.RequestActive();
                }
            }
            else
            {
                // If anything else happens and we have a current request remove it.
                if (_mDisplayRequest != null)
                {
                    _mDisplayRequest.RequestRelease();
                    _mDisplayRequest = null;
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
            if (string.IsNullOrWhiteSpace(source.Url))
            {
                return null;
            }

            try
            {
                // Try to find the ID
                var youtubeVideoId = TryToGetYouTubeId(source);

                if (!string.IsNullOrWhiteSpace(youtubeVideoId))
                {
                    // We found it!
                    // #todo make this quality an option dependent on device!
                    return await YouTube.GetVideoUriAsync(youtubeVideoId, YouTubeQuality.QualityMedium);
                }
            }
            catch (Exception)
            {
                TelemetryManager.ReportEvent("YoutubeString", "Failed to find youtube video");
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
            if (string.IsNullOrWhiteSpace(source.Url))
            {
                return null;
            }

            try
            {
                // Try to find the ID
                var youtubeVideoId = string.Empty;
                var urlLower = source.Url.ToLower();
                if (urlLower.Contains("youtube.com"))
                {
                    // Check for an attribution link
                    var attribution = urlLower.IndexOf("attribution_link?");
                    if (attribution != -1)
                    {
                        // We need to parse out the video id
                        // looks like this attribution_link?a=bhvqtDGQD6s&amp;u=%2Fwatch%3Fv%3DrK0D1ehO7CA%26feature%3Dshare
                        var uIndex = urlLower.IndexOf("u=", attribution);
                        var encodedUrl = source.Url.Substring(uIndex + 2);
                        var decodedUrl = WebUtility.UrlDecode(encodedUrl);
                        urlLower = decodedUrl.ToLower();
                        // At this point urlLower should be something like "v=jfkldfjl&feature=share"
                    }

                    var beginId = urlLower.IndexOf("v=");
                    var endId = urlLower.IndexOf("&", beginId);
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
                    var domain = urlLower.IndexOf("youtu.be");
                    var beginId = urlLower.IndexOf("/", domain);
                    var endId = urlLower.IndexOf("?", beginId);
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
                TelemetryManager.ReportEvent("YoutubeString", "Failed to find youtube video");
            }
            return null;
        }

        #endregion
    }
}
