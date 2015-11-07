using BaconBackend.DataObjects;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

namespace Baconit.FlipViewControls
{
    public sealed partial class GifImageFliplControl : UserControl, IFlipViewContentControl
    {
        IFlipViewContentHost m_host;
        MediaElement m_gifVideo;
        bool m_loadingHidden = false;
        bool m_shouldBePlaying = false;

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
            // See if we can get an image from the url
            if (String.IsNullOrWhiteSpace(GetImageUrl(post.Url)))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Called when the flip view consent should shown.
        /// </summary>
        /// <param name="url"></param>
        public async void OnPrepareContent(Post post)
        {
            m_shouldBePlaying = false;
            m_loadingHidden = false;

            // Show loading
            m_host.ShowLoading();

            // Since some of this can be costly, delay the work load until we aren't animating.
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                // Create the media element
                m_gifVideo = new MediaElement();
                m_gifVideo.HorizontalAlignment = HorizontalAlignment.Stretch;
                m_gifVideo.Tapped += OnVideoTapped;
                m_gifVideo.CurrentStateChanged += OnVideoCurrentStateChanged;
                m_gifVideo.Source = new Uri(GetImageUrl(post.Url), UriKind.Absolute);
                m_gifVideo.Play();
                m_gifVideo.IsLooping = true;
                ui_contentRoot.Children.Add(m_gifVideo);
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

        private static string GetImageUrl(string postUrl)
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
    }
}
