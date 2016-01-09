using BaconBackend.Managers;
using Baconit.Interfaces;
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
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class BasicImageContentPanel : UserControl, IContentPanel
    {
        const float MAX_ZOOM_FACTOR = 5.0f;

        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        IContentPanelBaseInternal m_base;

        /// <summary>
        /// The image we have.
        /// </summary>
        Image m_image;

        /// <summary>
        /// The calculated min zoom factor for this image.
        /// </summary>
        float m_minZoomFactor = 1.0f;

        /// <summary>
        /// Indicates if we should ignore scrolling changes.
        /// </summary>
        bool m_ignoreZoomChanges = false;

        /// <summary>
        /// Holds the current size of the control.
        /// </summary>
        Size m_currentControlSize;

        /// <summary>
        /// Hold the size the image was last set at.
        /// </summary>
        Size m_lastImageSetSize = new Size(0, 0);

        /// <summary>
        /// Holds a reference to the image's memory source.
        /// </summary>
        InMemoryRandomAccessStream m_imageSourceStream = null; 


        public BasicImageContentPanel(IContentPanelBaseInternal panelBase)
        {
            this.InitializeComponent();
            m_base = panelBase;

            // Register for back button presses
            App.BaconMan.OnBackButton += BaconMan_OnBackButton;

            // Set the max zoom for the scroller
            ui_scrollViewer.MaxZoomFactor = MAX_ZOOM_FACTOR;
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a source.
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        static public bool CanHandlePost(ContentPanelSource source)
        {
            // See if we can get an image from the url
            if (String.IsNullOrWhiteSpace(ImageManager.GetImageUrl(source.Url)))
            {
                return false;
            }
            return true;
        }

        #region IContentPanel

        /// <summary>
        /// Fired when we should load the content.
        /// </summary>
        /// <param name="source"></param>
        public void OnPrepareContent()
        {
            // Run our work on a background thread.
            Task.Run(async () =>
            {
                // Get the image Url
                string imageUrl = ImageManager.GetImageUrl(m_base.Source.Url);

                // Make sure we got it.
                if (String.IsNullOrWhiteSpace(imageUrl))
                {
                    // This is bad, we should be able to get the url.
                    App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "BasicImageControlNoImageUrl");

                    // Jump back to the UI thread
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        m_base.FireOnFallbackToBrowser();
                    });

                    return;
                }

                // Make sure we aren't destroyed.
                if(m_base.IsDestoryed)
                {
                    return;
                }

                // Fire off a request for the image.
                ImageManager.ImageManagerRequest request = new ImageManager.ImageManagerRequest()
                {
                    ImageId = m_base.Source.Id,
                    Url = imageUrl
                };
                request.OnRequestComplete += OnRequestComplete;
                App.BaconMan.ImageMan.QueueImageRequest(request);
            });
        }

        /// <summary>
        /// Fired when we should destroy our content.
        /// </summary>
        public void OnDestroyContent()
        {
            // Grab the lock since the image callback can take sometime. We don't want to
            // have two threads running at the same time on the same data
            lock (this)
            {
                // Remove the image from the UI
                ui_contentRoot.Children.Clear();

                // Kill the source.
                m_imageSourceStream = null;

                // Kill some listeners
                App.BaconMan.OnBackButton -= BaconMan_OnBackButton;
                ui_contentRoot.SizeChanged -= ContentRoot_SizeChanged;

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
        /// Fired when a new host has been added.
        /// </summary>
        public void OnHostAdded()
        {

        }

        /// <summary>
        /// Fired when this post becomes visible
        /// </summary>
        public void OnVisibilityChanged(bool isVisible)
        {

        }

        #endregion

        #region Image Managment

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
                    m_base.FireOnFallbackToBrowser();
                    return;
                }

                lock (this)
                {
                    if (m_base.IsDestoryed)
                    {
                        // Get out of here if we should be destroyed.
                        return;
                    }

                    // Set the current size of the control, this will also be set
                    // by the size changed listener but we want to make sure it is correct.
                    m_currentControlSize.Height = ui_contentRoot.ActualHeight;
                    m_currentControlSize.Width = ui_contentRoot.ActualWidth;

                    // Grab the source, we need this to make the image
                    m_imageSourceStream = response.ImageStream;

                    // Add the image to the UI
                    m_image = new Image();

                    // Add a loaded listener so we can size the image when loaded
                    m_image.Loaded += Image_Loaded;

                    // Set the image.
                    ReloadImage(false);

                    // Set the image into the UI.
                    ui_scrollViewer.Content = m_image;

                    // Setup the save image tap
                    m_image.RightTapped += ContentRoot_RightTapped;
                    m_image.Holding += ContentRoot_Holding;

                    m_base.FireOnLoading(false);
                }
            });
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
            m_base.FireOnLoading(false);
        }

        /// <summary>
        /// Reloads (or loads) the image with a decode size that is scaled to the
        /// screen. Unless useFulsize is set.
        /// </summary>
        /// <param name="useFullSize"></param>
        public void ReloadImage(bool useFullsize)
        {
            // Don't worry about this if we are destroyed.
            if (m_base.IsDestoryed)
            {
                return;
            }

            // If we are already this size don't do anything.
            if (m_lastImageSetSize.Equals(m_currentControlSize) && !useFullsize)
            {
                return;
            }

            // If we don't have a control size yet don't do anything.
            if(m_currentControlSize.IsEmpty)
            {
                return;
            }

            // Don't do anything if we are already full size.
            if (useFullsize && m_lastImageSetSize.Width == 0 && m_lastImageSetSize.Height == 0)
            {
                return;
            }

            // Get the stream.
            InMemoryRandomAccessStream stream = m_imageSourceStream;
            if(stream == null)
            {
                return;
            }

            // Set our last size.
            if(useFullsize)
            {
                // Set our current size.
                m_lastImageSetSize.Width = 0;
                m_lastImageSetSize.Height = 0;
            }
            else
            {
                // Set our current size.
                m_lastImageSetSize.Width = m_currentControlSize.Width;
                m_lastImageSetSize.Height = m_currentControlSize.Height;
            }
            
            // Get the decode height and width.
            int decodeWidth = 0;
            int decodeHeight = 0;

            if (!useFullsize)
            {
                int controlWidth = (int)m_currentControlSize.Width;
                int controlHeight = (int)m_currentControlSize.Height;
                if (controlWidth < controlHeight)
                {
                    decodeWidth = controlWidth;
                }
                else
                {
                    decodeHeight = controlHeight;
                }
            }

            // Create a bitmap and
            BitmapImage bitmapImage = new BitmapImage();     

            // Set the decode size.
            bitmapImage.DecodePixelHeight = decodeHeight;
            bitmapImage.DecodePixelWidth = decodeWidth;
            bitmapImage.DecodePixelType = DecodePixelType.Physical;

            // Set the source.
            stream.Seek(0);
            bitmapImage.SetSource(stream);

            // Set the image.
            m_image.Source = bitmapImage;
        }

        #endregion

        #region Save Image

        /// <summary>
        /// Fired when save image is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            if (!String.IsNullOrWhiteSpace(m_base.Source.Url))
            {
                App.BaconMan.ImageMan.SaveImageLocally(m_base.Source.Url);
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

        #endregion

        #region Image Scrolling Logic

        /// <summary>
        /// Fired when the scroller is moving.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScrollViewer_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            if (m_ignoreZoomChanges)
            {
                return;
            }

            // If the zooms don't match go full screen
            if (Math.Abs(m_minZoomFactor - ui_scrollViewer.ZoomFactor) > .001)
            {
                // Load the image full screen if not already.
                ReloadImage(true);

                // Try to go full screen
                m_base.FireOnFullscreenChanged(true);
            }
        }

        /// <summary>
        /// Fired when the scroller is done moving.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (m_ignoreZoomChanges)
            {
                return;
            }

            // If the two zoom factors are close enough, leave full screen.
            if (Math.Abs(m_minZoomFactor - ui_scrollViewer.ZoomFactor) < .001)
            {
                // Load the image as screen size.
                ReloadImage(false);

                // Try to leave full screen.
                m_base.FireOnFullscreenChanged(false);
            }
        }

        /// <summary>
        /// Fired when the user presses back.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BaconMan_OnBackButton(object sender, BaconBackend.OnBackButtonArgs e)
        {
            if (e.IsHandled)
            {
                return;
            }

            // If we are full screen reset the scroller which will take us out.
            if (m_base.IsFullscreen)
            {
                e.IsHandled = true;
                ui_scrollViewer.ChangeView(null, null, m_minZoomFactor);
            }
        }

        #endregion

        #region Scroller Setting

        /// <summary>
        /// Fired when the control changes sizes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if(m_base.IsFullscreen)
            {
                return;
            }

            // Grab the new size.
            m_currentControlSize = e.NewSize;

            // Resize the image.
            ReloadImage(false);

            bool previousIgnoreState = m_ignoreZoomChanges;

            // Ignore zoom changes and update the zoom factors
            m_ignoreZoomChanges = true;

            SetScrollerZoomFactors();

            m_ignoreZoomChanges = previousIgnoreState;
        }

        /// <summary>
        /// Called when we should update the scroller zoom factor.
        /// </summary>
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

        /// <summary>
        /// Called when we should update the scroller zoom factor.
        /// </summary>
        private async void SetScrollerZoomFactors(Size imageSize)
        {
            if (imageSize == null || imageSize.Height == 0 || imageSize.Width == 0 || ui_scrollViewer.ActualHeight == 0 || ui_scrollViewer.ActualWidth == 0)
            {
                return;
            }

            // If we are full screen don't update this.
            if (m_base.IsFullscreen)
            {
                return;
            }

            // Figure out what the min zoom should be.
            float vertZoomFactor = (float)(ui_scrollViewer.ActualHeight / imageSize.Height);
            float horzZoomFactor = (float)(ui_scrollViewer.ActualWidth / imageSize.Width);
            m_minZoomFactor = Math.Min(vertZoomFactor, horzZoomFactor);

            // Do a check to make sure the zoom level is ok.
            if (m_minZoomFactor < 0.1)
            {
                m_minZoomFactor = 0.1f;
            }

            // Set the zoomer to the min size for the zoom
            ui_scrollViewer.MinZoomFactor = m_minZoomFactor;

            // Set the zoom size, we need to delay for a bit so it actually applies.
            await Task.Delay(1);
            ui_scrollViewer.ChangeView(null, null, m_minZoomFactor, true);
        }

        #endregion

    }
}
