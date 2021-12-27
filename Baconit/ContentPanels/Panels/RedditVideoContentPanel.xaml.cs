using System;
using System.Threading.Tasks;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Baconit.Interfaces;

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class RedditVideoContentPanel : IContentPanel
    {
        private readonly IContentPanelBaseInternal _contentPanelBase;
        private MediaElement _videoPlayer;
        private DisplayRequest _displayRequest;

        public PanelMemorySizes PanelMemorySize => PanelMemorySizes.Medium;

        public RedditVideoContentPanel(IContentPanelBaseInternal contentPanelBase)
        {
            InitializeComponent();
            _contentPanelBase = contentPanelBase;
            
        }

        public static bool CanHandlePost(ContentPanelSource source)
        {
            return source.IsRedditVideo && !string.IsNullOrWhiteSpace(source.VideoUrl.AbsoluteUri);
        }

        public void OnPrepareContent()
        {
            var source = _contentPanelBase.Source;

            Task.Run(async () =>
            {
                // Back to the UI thread with pri
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    // Setup the video
                    _videoPlayer = new MediaElement {AutoPlay = false, AreTransportControlsEnabled = true};
                    _videoPlayer.CurrentStateChanged += VideoPlayerOnCurrentStateChanged;
                    _videoPlayer.Source = source.VideoUrl;
                        _videoPlayer.AudioDeviceType = AudioDeviceType.Multimedia;
                    _videoPlayer.IsMuted = false;
                    ui_contentRoot.Children.Add(_videoPlayer);
                });
            });
        }

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
                if (_displayRequest != null) return;
                _displayRequest = new DisplayRequest();
                _displayRequest.RequestActive();
            }
            else
            {
                // If anything else happens and we have a current request remove it.
                if (_displayRequest == null) return;
                _displayRequest.RequestRelease();
                _displayRequest = null;
            }
        }

        public void OnVisibilityChanged(bool isVisible)
        {
            if(_videoPlayer != null && !isVisible)
            {
                _videoPlayer.Pause();
            }
        }

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

        public void OnHostAdded()
        {
            // Ignore for now.
        }
    }
}
