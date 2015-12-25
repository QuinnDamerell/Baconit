using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using BaconBackend.DataObjects;
using BaconBackend.Managers;
using BaconBackend.Interfaces;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;
using System.Threading.Tasks;

namespace Baconit.FlipViewControls
{
    public sealed partial class BasicImageFlipControl : UserControl, IFlipViewContentControl
    {
        const float MAX_ZOOM_FACTOR = 5.0f;

        bool m_isDestoryed = false;
        IFlipViewContentHost m_host;
        Image m_image;
        Post m_post;
        float m_minZoomFactor = 1.0f;
        bool m_ignoreZoomChanges = false;

        public BasicImageFlipControl(IFlipViewContentHost host)
        {
            m_host = host;
            this.InitializeComponent();
            App.BaconMan.OnBackButton += BaconMan_OnBackButton;

            // Set the max zoom for the scroller
            ui_scrollViewer.MaxZoomFactor = MAX_ZOOM_FACTOR;
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a post.
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        static public bool CanHandlePost(Post post)
        {
            // See if we can get an image from the url
            if(String.IsNullOrWhiteSpace(ImageManager.GetImageUrl(post.Url)))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Called when we should show content
        /// </summary>
        /// <param name="post"></param>
        public void OnPrepareContent(Post post)
        {
            // Set our flag
            m_isDestoryed = false;

            // First show loading
            m_host.ShowLoading();

            // Hide the content root
            ui_contentRoot.Opacity = 0;

            // Grab the post URL
            m_post = post;

            // Do the rest of the work on a background thread.
            Task.Run(async () =>
            {
                // Get the image Url
                string imageUrl = ImageManager.GetImageUrl(post.Url);

                if (String.IsNullOrWhiteSpace(imageUrl))
                {
                    // This is bad, we should be able to get the url.
                    App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "BasicImageControlNoImageUrl");

                    // Jump back to the UI thread
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        m_host.FallbackToWebBrowser(post);
                    });

                    return;
                }

                // Fire off a request for the image.
                ImageManager.ImageManagerRequest request = new ImageManager.ImageManagerRequest()
                {
                    ImageId = post.Id,
                    Url = imageUrl
                };
                request.OnRequestComplete += OnRequestComplete;
                App.BaconMan.ImageMan.QueueImageRequest(request);
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
        /// Called when we should destroy the content
        /// </summary>
        public void OnDestroyContent()
        {
            // Grab the lock since the image callback can take sometime. We don't want to
            // have two threads running at the same time on the same data
            lock (m_host)
            {
                // Set the flag
                m_isDestoryed = true;

                // Kill the post
                m_post = null;

                // Remove the image from the UI
                ui_contentRoot.Children.Clear();

                // Make sure the image was created, if it never loaded
                // then it won't be created.
                if (m_image != null)
                {
                    // Kill the image
                    m_image.Source = null;
                    m_image.Loaded -= Image_Loaded;
                    m_image.RightTapped -= ContentRoot_RightTapped;
                    m_image.Holding -= ContentRoot_Holding;
                    m_image = null;
                }
            }
        }

        /// <summary>
        /// Callback when we get the image.
        /// </summary>
        /// <param name="response"></param>
        public async void OnRequestComplete(object sender, ImageManager.ImageManagerResponseEventArgs response)
        {
            // Remove the event
            ImageManager.ImageManagerRequest request = (ImageManager.ImageManagerRequest)sender;
            request.OnRequestComplete -= OnRequestComplete;

            // Jump back to the UI thread
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!response.Success)
                {
                    App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "BasicImageControlNoImageUrl");
                    m_host.FallbackToWebBrowser(m_post);
                    return;
                }

                lock (m_host)
                {
                    if(m_isDestoryed)
                    {
                        // Get out of here if we should be destroyed.
                        return;
                    }

                    // Create a bitmap and set the source
                    // #todo, initially we can use a bitmap here decoded to the size of the control
                    // then when the user zooms in we can swap it for a larger image.
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.SetSource(response.ImageStream);

                    // Add the image to the UI
                    m_image = new Image();

                    // Add a loaded listener so we can size the image when loaded
                    m_image.Loaded += Image_Loaded;

                    // Set the image.
                    m_image.Source = bitmapImage;      

                    // Set the image into the UI.
                    ui_scrollViewer.Content = m_image;

                    // Setup the save image tap
                    m_image.RightTapped += ContentRoot_RightTapped;
                    m_image.Holding += ContentRoot_Holding;
                }
            });
        }

        /// <summary>
        /// Fired when save image is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            if(!String.IsNullOrWhiteSpace(m_post.Url))
            {
                App.BaconMan.ImageMan.SaveImageLocally(m_post.Url);
            }
        }

        /// <summary>
        /// Fired when the image is right clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentRoot_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (element != null)
            {
                Point p = e.GetPosition(element);
                flyoutMenu.ShowAt(element, p);
            }
        }

        /// <summary>
        /// Fired when the image is press and held
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentRoot_Holding(object sender, HoldingRoutedEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (element != null)
            {
                Point p = e.GetPosition(element);
                flyoutMenu.ShowAt(element, p);
            }
        }

        #region Full Screen Scroll Logic

        private void ScrollViewer_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            if(m_ignoreZoomChanges)
            {
                return;
            }

            // If the zooms don't match go full screen
            if(Math.Abs(m_minZoomFactor - ui_scrollViewer.ZoomFactor) > .001)
            {
                m_host.ToggleFullScreen(true);
            }
        }

        private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (m_ignoreZoomChanges)
            {
                return;
            }

            // If the two zoom factors are close enough, leave full screen.
            if (Math.Abs(m_minZoomFactor - ui_scrollViewer.ZoomFactor) < .001)
            {
                m_host.ToggleFullScreen(false);
            }
        }

        /// <summary>
        /// Fired when the user presses back
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BaconMan_OnBackButton(object sender, BaconBackend.OnBackButtonArgs e)
        {
            if(e.IsHandled)
            {
                return;
            }

            if(m_host.IsFullScreen())
            {
                e.IsHandled = true;
                ui_scrollViewer.ChangeView(null, null, m_minZoomFactor);
            }
        }

        #endregion

        private void ContentRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool previousIgnoreState = m_ignoreZoomChanges;

            // Ignore zoom changes and update the zoom factors
            m_ignoreZoomChanges = true;

            SetScrollerZoomFactors();

            m_ignoreZoomChanges = previousIgnoreState;
        }

        /// <summary>
        /// When the image is loaded size the actual image
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Image_Loaded(object sender, RoutedEventArgs e)
        {
            // Ignore zoom changes and update the zoom factors
            m_ignoreZoomChanges = true;

            SetScrollerZoomFactors();

            m_ignoreZoomChanges = false;

            // Hide the loading screen
            m_host.HideLoading();

            // Show the image
            ui_storyContentRoot.Begin();            
        }

        private void SetScrollerZoomFactors()
        {
            // If we don't have a size yet ignore.
            if (m_image == null || m_image.ActualHeight == 0 || m_image.ActualWidth == 0)
            {
                return;
            }

            Size imageSize = new Size()
            {
                Width = m_image.ActualWidth,
                Height = m_image.ActualHeight
            };
            SetScrollerZoomFactors(imageSize);
        }

        private async void SetScrollerZoomFactors(Size imageSize)
        {
            if (imageSize == null || imageSize.Height == 0 || imageSize.Width == 0 || ui_scrollViewer.ActualHeight == 0 || ui_scrollViewer.ActualWidth == 0)
            {
                return;
            }

            // If we are full screen don't update this.
            // #todo, we probably should, just not if we are being touched currently.
            if (m_host.IsFullScreen())
            {
                return;
            }

            // Figure out what the min zoom should be.
            float vertZoomFactor = (float)(ui_scrollViewer.ActualHeight / imageSize.Height);
            float horzZoomFactor = (float)(ui_scrollViewer.ActualWidth / imageSize.Width);
            m_minZoomFactor = Math.Min(vertZoomFactor, horzZoomFactor);

            // Do a check to make sure the zoom level is ok.
            if(m_minZoomFactor < 0.1)
            {                
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, $"minZoomTooSmall{m_minZoomFactor}:{vertZoomFactor},{horzZoomFactor};{ui_scrollViewer.ActualHeight};{ui_scrollViewer.ActualWidth};{imageSize.Height};{imageSize.Width}");
                m_minZoomFactor = 0.1f;
            }

            // Set the zoomer to the min size for the zoom
            ui_scrollViewer.MinZoomFactor = m_minZoomFactor;

            // Set the zoom size, we need to delay for a bit so it actually applies.
            await Task.Delay(1);
            ui_scrollViewer.ChangeView(null, null, m_minZoomFactor, true);
        }
    }
}
