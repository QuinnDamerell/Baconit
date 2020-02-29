using Baconit.Interfaces;
using System;
using System.Net;
using System.Threading.Tasks;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using BaconBackend.Helpers;
using BaconBackend.Managers;

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class YoutubeContentPanel : IContentPanel
    {
        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        private readonly IContentPanelBaseInternal _contentPanelBase;

        /// <summary>
        /// Holds a ref to the media element that is playing.
        /// </summary>
        private MediaElement _videoPlayer;

        /// <summary>
        /// Holds the request to not sleep the computer while a video is playing.
        /// </summary>
        private DisplayRequest _displayRequest;

        public YoutubeContentPanel(IContentPanelBaseInternal panelBase)
        {
            InitializeComponent();
            _contentPanelBase = panelBase;
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a post.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static bool CanHandlePost(ContentPanelSource source)
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
        public void OnPrepareContent()
        {
            // Since this can be costly kick it off to a background thread so we don't do work
            // as we are animating.
            Task.Run(async () =>
            {
                // Get the video Uri
                var youTubeUri = await GetYouTubeVideoUrl(_contentPanelBase.Source);

                // Back to the UI thread with pri
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    if (youTubeUri == null)
                    {
                        // If we failed fallback to the browser.
                        _contentPanelBase.FireOnFallbackToBrowser();
                        TelemetryManager.ReportUnexpectedEvent(this, "FailedToGetYoutubeVideoAfterSuccess");
                        return;
                    }

                    // Setup the video
                    _videoPlayer = new MediaElement {AutoPlay = false, AreTransportControlsEnabled = true};
                    _videoPlayer.CurrentStateChanged += VideoPlayerOnCurrentStateChanged;
                    _videoPlayer.Source = youTubeUri.Uri;
                    ui_contentRoot.Children.Add(_videoPlayer);
                });
            });
        }

        /// <summary>
        /// Fired when we should destroy our content.
        /// </summary>
        public void OnDestroyContent()
        {
            try
            {
                if (_videoPlayer == null) return;
                _videoPlayer.CurrentStateChanged -= VideoPlayerOnCurrentStateChanged;
                _videoPlayer.Stop();
                _videoPlayer.Source = null;
            }
            finally
            {
                _videoPlayer = null;
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
            if(_videoPlayer != null && !isVisible)
            {
                _videoPlayer.Pause();
            }
        }

        #endregion

        #region Media Controls

        /// <summary>
        /// Fired when the media state changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VideoPlayerOnCurrentStateChanged(object sender, RoutedEventArgs e)
        {
            // Check to hide the loading.
            if (_videoPlayer.CurrentState != MediaElementState.Opening)
            {
                // When we get to the state paused hide loading.
                if (_contentPanelBase.IsLoading)
                {
                    _contentPanelBase.FireOnLoading(false);
                }
            }

            // If we are playing request for the screen not to turn off.
            if (_videoPlayer.CurrentState == MediaElementState.Playing)
            {
                if (_displayRequest == null)
                {
                    _displayRequest = new DisplayRequest();
                    _displayRequest.RequestActive();
                }
            }
            else
            {
                // If anything else happens and we have a current request remove it.
                if (_displayRequest != null)
                {
                    _displayRequest.RequestRelease();
                    _displayRequest = null;
                }
            }
        }

        #endregion

        #region Youtube Id

        /// <summary>
        /// Tries to get a youtube link from a post. If it fails
        /// it returns null.
        /// </summary>
        /// <param name="source"></param>
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
                    return await YouTubeHelper.GetVideoUriAsync(youtubeVideoId, YouTubeQuality.QualityMedium);
                }
            }
            catch (Exception ex)
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
                    var attribution = urlLower.IndexOf("attribution_link?", StringComparison.OrdinalIgnoreCase);
                    if (attribution != -1)
                    {
                        // We need to parse out the video id
                        // looks like this attribution_link?a=bhvqtDGQD6s&amp;u=%2Fwatch%3Fv%3DrK0D1ehO7CA%26feature%3Dshare
                        var uIndex = urlLower.IndexOf("u=", attribution, StringComparison.OrdinalIgnoreCase);
                        var encodedUrl = source.Url.Substring(uIndex + 2);
                        var decodedUrl = WebUtility.UrlDecode(encodedUrl);
                        urlLower = decodedUrl.ToLower();
                        // At this point urlLower should be something like "v=jfkldfjl&feature=share"
                    }

                    var beginId = urlLower.IndexOf("v=", StringComparison.OrdinalIgnoreCase);
                    var endId = urlLower.IndexOf("&", beginId, StringComparison.OrdinalIgnoreCase);
                    if (beginId == -1) return youtubeVideoId;
                    if (endId == -1)
                    {
                        endId = urlLower.Length;
                    }
                    // Important! Since this might be case sensitive use the original url!
                    beginId += 2;
                    youtubeVideoId = source.Url.Substring(beginId, endId - beginId);
                }
                else if (urlLower.Contains("youtu.be"))
                {
                    var domain = urlLower.IndexOf("youtu.be", StringComparison.OrdinalIgnoreCase);
                    var beginId = urlLower.IndexOf("/", domain, StringComparison.OrdinalIgnoreCase);
                    var endId = urlLower.IndexOf("?", beginId, StringComparison.OrdinalIgnoreCase);
                    // If we can't find a ? search for a &
                    if (endId == -1)
                    {
                        endId = urlLower.IndexOf("&", beginId, StringComparison.OrdinalIgnoreCase);
                    }

                    if (beginId == -1) return youtubeVideoId;

                    if (endId == -1)
                    {
                        endId = urlLower.Length;
                    }
                    // Important! Since this might be case sensitive use the original url!
                    beginId++;
                    youtubeVideoId = source.Url.Substring(beginId, endId - beginId);
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
