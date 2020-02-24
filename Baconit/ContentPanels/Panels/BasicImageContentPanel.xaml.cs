using BaconBackend.Managers;
using Baconit.Interfaces;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class BasicImageContentPanel : UserControl, IContentPanel
    {
        const float MAX_ZOOM_FACTOR = 5.0f;

        /// <summary>
        /// Possible states of the image
        /// </summary>
        enum ImageState
        {
            Unloaded,
            Normal,
            NormalSizeUpdating,
            EnteringFullscreen,
            EnteringFullscreenComplete,
            Fullscreen,
            ExitingFullscreen,
        }

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

        /// <summary>
        /// Indicates the current state of the image
        /// </summary>
        ImageState m_state = ImageState.Unloaded;

        /// <summary>
        /// The scale factor of the current device.
        /// </summary>
        double m_deviceScaleFactor;

        public BasicImageContentPanel(IContentPanelBaseInternal panelBase)
        {
            this.InitializeComponent();
            m_base = panelBase;

            // Register for back button presses
            App.BaconMan.OnBackButton += BaconMan_OnBackButton;

            // Set the max zoom for the scroller
            ui_scrollViewer.MaxZoomFactor = MAX_ZOOM_FACTOR;

            // Get the current scale factor. Why 0.001? If we don't add that we kill the app
            // with a layout loop. I think this is a platform bug so we need it for now. If you don't
            // believe me remove it and see what happens.
            m_deviceScaleFactor = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel + 0.001;
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
        /// Indicates how large the panel is in memory.
        /// </summary>
        public PanelMemorySizes PanelMemorySize
        {
            get
            {
                if(m_imageSourceStream != null)
                {
                    // #todo revisit these sizes.
                    ulong size = m_imageSourceStream.Size;
                    if(size < 3145728)
                    {
                        // < 3mb small
                        return PanelMemorySizes.Small;
                    }
                    else if(size < 8388608)
                    {
                        // < 8mb medium
                        return PanelMemorySizes.Medium;
                    }
                    else
                    {
                        // > 8mb large
                        return PanelMemorySizes.Large;
                    }
                }
                return PanelMemorySizes.Small;
            }
        }

        /// <summary>
        /// Fired when we should load the content.
        /// </summary>
        /// <param name="source"></param>
        public void OnPrepareContent()
        {
            // Run our work on a background thread.
            Task.Run(() =>
            {
                // Get the image Url
                string imageUrl = ImageManager.GetImageUrl(m_base.Source.Url);

                // Make sure we got it.
                if (String.IsNullOrWhiteSpace(imageUrl))
                {
                    // This is bad, we should be able to get the url.
                    App.BaconMan.TelemetryMan.ReportUnexpectedEvent(this, "BasicImageControlNoImageUrl");

                    // Jump back to the UI thread
                    m_base.FireOnFallbackToBrowser();
                    return;
                }

                // Make sure we aren't destroyed.
                if (m_base.IsDestoryed)
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
                    if(m_image.Source != null)
                    {
                        BitmapImage bmpImage = (BitmapImage)m_image.Source;
                        if(bmpImage != null)
                        {
                            bmpImage.ImageOpened -= BitmapImage_ImageOpened;
                            bmpImage.ImageFailed -= BitmapImage_ImageFailed;
                        }
                    }

                    // Kill the image
                    m_image.Source = null;
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
                    App.BaconMan.TelemetryMan.ReportUnexpectedEvent(this, "BasicImageControlNoImageUrl");
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

                    // Grab the source, we need this to make the image
                    m_imageSourceStream = response.ImageStream;

                    // Add the image to the UI
                    m_image = new Image();

                    // We don't want to wait on this.
#pragma warning disable CS4014
                    // Set the image.
                    ReloadImage(false);
#pragma warning restore CS4014

                    // Set the image into the UI.
                    ui_scrollViewer.Content = m_image;

                    // Setup the save image tap
                    m_image.RightTapped += ContentRoot_RightTapped;
                    m_image.Holding += ContentRoot_Holding;
                }
            });
        }

        /// <summary>
        /// Reloads (or loads) the image with a decode size that is scaled to the
        /// screen. Unless useFulsize is set.
        /// </summary>
        /// <param name="useFullSize"></param>
        public bool ReloadImage(bool useFullsize)
        {
            // Don't worry about this if we are destroyed.
            if (m_base.IsDestoryed)
            {
                return false;
            }

            // If we are already this size don't do anything.
            if (m_lastImageSetSize.Equals(m_currentControlSize) && !useFullsize)
            {
                return false;
            }

            // If we don't have a control size yet don't do anything.
            if(m_currentControlSize.IsEmpty)
            {
                return false;
            }

            // Don't do anything if we are already full size.
            if (useFullsize && m_lastImageSetSize.Width == 0 && m_lastImageSetSize.Height == 0)
            {
                return false;
            }

            // Get the stream.
            InMemoryRandomAccessStream stream = m_imageSourceStream;
            if(stream == null)
            {
                return false;
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

            // Create a bitmap and
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.CreateOptions = BitmapCreateOptions.None;
            bitmapImage.ImageOpened += BitmapImage_ImageOpened;
            bitmapImage.ImageFailed += BitmapImage_ImageFailed;

            // Get the decode height and width.
            int decodeWidth = 0;
            int decodeHeight = 0;
            if (!useFullsize)
            {
                double widthRatio = bitmapImage.PixelWidth / m_currentControlSize.Width;
                double heightRatio = bitmapImage.PixelHeight / m_currentControlSize.Height;
                if (widthRatio > heightRatio)
                {
                    decodeWidth = (int)m_currentControlSize.Width;
                }
                else
                {
                    decodeHeight = (int)m_currentControlSize.Height;
                }
            }

            // Set the decode size.
            bitmapImage.DecodePixelHeight = decodeHeight;
            bitmapImage.DecodePixelWidth = decodeWidth;

            // If we have memory to play with use logical pixels for decode. This will cause the image to decode to the
            // logical size, meaning the physical pixels will actually be the screen resolution. If we choose physical pixels
            // the image will decode to the width of the control in physical pixels, so the image size to control size will be one to one.
            // But since this is scaled, the control size is something like 3 physical pixels for each logical pixel, so the image is lower res
            // if we use physical.
            bitmapImage.DecodePixelType = App.BaconMan.MemoryMan.MemoryPressure < MemoryPressureStates.Medium ? DecodePixelType.Logical : DecodePixelType.Physical;

            // Set the source. This must be done after setting decode size and other parameters, so those are respected.
            stream.Seek(0);
            bitmapImage.SetSource(stream);

            // Destroy the old image.
            if (m_image.Source != null)
            {
                BitmapImage currentImage = (BitmapImage)m_image.Source;
                if (currentImage != null)
                {
                    // Remove the handlers
                    currentImage.ImageOpened -= BitmapImage_ImageOpened;
                    currentImage.ImageFailed -= BitmapImage_ImageFailed;
                }
            }

            // Set the image.
            m_image.Source = bitmapImage;
            return true;
        }

        /// <summary>
        /// Fired when the image is actually ready.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BitmapImage_ImageOpened(object sender, RoutedEventArgs e)
        {
            if(m_base.IsDestoryed)
            {
                return;
            }

            // Update the zoom if needed
            await SetScrollerZoomFactors();

            // Hide the loading screen
            m_base.FireOnLoading(false);

            // This can throw if we are being destroyed.
            try
            {
                // Restore the state
                ui_scrollViewer.Visibility = Visibility.Visible;
                VisualStateManager.GoToState(this, "ShowImage", true);
            }
            catch(Exception)
            {
                return;
            }

            // wait a little bit before we set the new state so things can settle.
            await Task.Delay(50);

            // Update the state
            switch (m_state)
            {
                case ImageState.Unloaded:
                    m_state = ImageState.Normal;
                    break;
                case ImageState.EnteringFullscreen:
                    m_state = ImageState.EnteringFullscreenComplete;
                    break;
                case ImageState.ExitingFullscreen:
                    m_state = ImageState.Normal;
                    break;
            }
        }

        /// <summary>
        /// Fired when an image fails to load.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BitmapImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            m_base.FireOnError(true, "This image failed to load");
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
        private async void ScrollViewer_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            if (m_state != ImageState.Normal)
            {
                return;
            }

            // If the zooms don't match go full screen
            if (!AreCloseEnoughToEqual(m_minZoomFactor, ui_scrollViewer.ZoomFactor))
            {
                // Set the state and hide the image, we do this make the transition smoother.
                m_state = ImageState.EnteringFullscreen;
                VisualStateManager.GoToState(this, "HideImage", true);
                ui_scrollViewer.Visibility = Visibility.Collapsed;

                // wait a second for the vis change to apply
                await Task.Delay(10);

                // Try to go full screen
                m_base.FireOnFullscreenChanged(true);

                // Delay for a bit to let full screen settle.
                await Task.Delay(10);

                // Resize the image.
                ReloadImage(true);
            }
        }

        /// <summary>
        /// Fired when the scroller is done moving.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            // If we are done entering full screen set the var and return.
            if (m_state == ImageState.EnteringFullscreenComplete)
            {
                m_state = ImageState.Fullscreen;
                return; ;
            }
            else if (m_state != ImageState.Fullscreen)
            {
                return;
            }

            // If the two zoom factors are close enough, leave full screen.
            if (AreCloseEnoughToEqual(m_minZoomFactor, ui_scrollViewer.ZoomFactor))
            {
                // Set the state and hide the image, we do this make the transition smoother.
                m_state = ImageState.ExitingFullscreen;
                ui_scrollViewer.Visibility = Visibility.Collapsed;
                VisualStateManager.GoToState(this, "HideImage", true);

                // Try to leave full screen.
                m_base.FireOnFullscreenChanged(false);

                // Delay for a bit to let full screen settle.
                await Task.Delay(10);

                // Resize the image
                ReloadImage(false);
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
        private async void ContentRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Grab the new size.
            m_currentControlSize = e.NewSize;

            if(m_state == ImageState.Unloaded)
            {
                // If we are unloaded call this to ensure the image it loading.
                ReloadImage(false);
            }
            else if(m_state == ImageState.Normal)
            {
                // Only reload the image if the size is larger than the current image.
                bool didImageResize = false;
                if (m_lastImageSetSize.Height < m_currentControlSize.Height && m_lastImageSetSize.Width < m_currentControlSize.Width)
                {
                    // Resize the image.
                    didImageResize = ReloadImage(false);
                }

                // if we didn't resize the image just set the new zoom.
                if (!didImageResize)
                {
                    m_state = ImageState.NormalSizeUpdating;

                    await SetScrollerZoomFactors();

                    m_state = ImageState.Normal;
                }
            }
        }

        /// <summary>
        /// Called when we should update the scroller zoom factor.
        /// </summary>
        private async Task SetScrollerZoomFactors()
        {
            // If we don't have a size yet ignore.
            if (m_image == null || m_image.Source == null || m_image.ActualHeight == 0 || m_image.ActualWidth == 0)
            {
                return;
            }

            // Get the image and the size.
            BitmapImage bitmapImage = (BitmapImage)m_image.Source;
            Size imageSize = new Size(bitmapImage.PixelWidth, bitmapImage.PixelHeight);

            // No matter what they pixel type, the scroller always seems to look at decoded pixel values
            // and not the real sizes. So we need to make this size match the decoded size.
            if (bitmapImage.DecodePixelWidth != 0)
            {
                double ratio = ((double)bitmapImage.PixelWidth) / ((double)bitmapImage.DecodePixelWidth);
                imageSize.Width /= ratio;
                imageSize.Height /= ratio;
            }
            else if (bitmapImage.DecodePixelHeight != 0)
            {
                double ratio = ((double)bitmapImage.PixelHeight) / ((double)bitmapImage.DecodePixelHeight);

                imageSize.Width /= ratio;
                imageSize.Height /= ratio;
            }

            // If we are using logical pixels and have a decode width or height we want to
            // scale the actual height and width down. We must do this because the scroller
            // expects logical pixels.
            if (bitmapImage.DecodePixelType == DecodePixelType.Logical)// && (bitmapImage.DecodePixelHeight != 0 || bitmapImage.DecodePixelWidth != 0))
            {

            }

            await SetScrollerZoomFactors(imageSize);
        }

        /// <summary>
        /// Called when we should update the scroller zoom factor.
        /// NOTE!! The input sizes should be in logical pixels not physical
        /// </summary>
        private async Task SetScrollerZoomFactors(Size imageSize)
        {
            if (imageSize == null || imageSize.Height == 0 || imageSize.Width == 0 || ui_scrollViewer.ActualHeight == 0 || ui_scrollViewer.ActualWidth == 0)
            {
                return;
            }

            // If we are full screen don't update this.
            if (m_state == ImageState.Fullscreen)
            {
                return;
            }

            // Figure out what the min zoom should be.
            float vertZoomFactor = (float)(ui_scrollViewer.ActualHeight / imageSize.Height);
            float horzZoomFactor = (float)(ui_scrollViewer.ActualWidth / imageSize.Width);
            m_minZoomFactor = Math.Min(vertZoomFactor, horzZoomFactor);

            // For some reason, if the zoom factor is larger than 1 we set
            //it to 1 and everything is correct.
            if(m_minZoomFactor > 1)
            {
                m_minZoomFactor = 1;
            }

            // Do a check to make sure the zoom level is ok.
            if (m_minZoomFactor < 0.1)
            {
                m_minZoomFactor = 0.1f;
            }

            // Set the zoomer to the min size for the zoom
            ui_scrollViewer.MinZoomFactor = m_minZoomFactor;

            float newZoomFactor = 0;
            double? offset = null;
            if(m_state == ImageState.EnteringFullscreen)
            {
                // When entering full screen always go to the min zoom.
                newZoomFactor = m_minZoomFactor;

                // Make sure we aren't too close to our min zoom.
                if (AreCloseEnoughToEqual(newZoomFactor, m_minZoomFactor))
                {
                    // If so make it a little more so we don't instantly jump out of full screen.
                    newZoomFactor += 0.002f;
                }
            }
            else
            {
                // If we don't have an image already set the zoom to the min zoom.
                newZoomFactor = m_minZoomFactor;
                offset = 0;
            }

            // Do a check to make sure the zoom level is ok.
            if (newZoomFactor < 0.1)
            {
                newZoomFactor = 0.1f;
            }

            // Set the zoom size, we need to delay for a bit so it actually applies.
            await Task.Delay(5);
            ui_scrollViewer.ChangeView(offset, offset, newZoomFactor, true);
        }

        #endregion

        /// <summary>
        /// Indicates if two floats are close enough.
        /// </summary>
        /// <param name="num1"></param>
        /// <param name="num2"></param>
        /// <returns></returns>
        private bool AreCloseEnoughToEqual(float num1, float num2)
        {
            return Math.Abs(num1 - num2) < .001;
        }
    }
}
