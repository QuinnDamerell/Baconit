using BaconBackend.Managers;
using Baconit.Interfaces;
using System;
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
        private const float MaxZoomFactor = 5.0f;

        /// <summary>
        /// Possible states of the image
        /// </summary>
        private enum ImageState
        {
            Unloaded,
            Normal,
            NormalSizeUpdating,
            EnteringFullscreen,
            EnteringFullscreenComplete,
            Fullscreen,
            ExitingFullscreen,
        }

        private readonly object _lockObject = new object();

        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        private readonly IContentPanelBaseInternal _baseContentPanel;

        /// <summary>
        /// The image we have.
        /// </summary>
        private Image _image;

        /// <summary>
        /// The calculated min zoom factor for this image.
        /// </summary>
        private float _minZoomFactor = 1.0f;

        /// <summary>
        /// Holds the current size of the control.
        /// </summary>
        private Size _currentControlSize;

        /// <summary>
        /// Hold the size the image was last set at.
        /// </summary>
        private Size _lastImageSetSize = new Size(0, 0);

        /// <summary>
        /// Holds a reference to the image's memory source.
        /// </summary>
        private InMemoryRandomAccessStream _imageSourceStream;

        /// <summary>
        /// Indicates the current state of the image
        /// </summary>
        private ImageState _state = ImageState.Unloaded;

        /// <summary>
        /// The scale factor of the current device.
        /// </summary>
        private double _deviceScaleFactor;

        public BasicImageContentPanel(IContentPanelBaseInternal panelBaseContentPanel)
        {
            InitializeComponent();
            _baseContentPanel = panelBaseContentPanel;

            // Register for back button presses
            App.BaconMan.OnBackButton += BaconMan_OnBackButton;

            // Set the max zoom for the scroll
            ui_scrollViewer.MaxZoomFactor = MaxZoomFactor;

            // Get the current scale factor. Why 0.001? If we don't add that we kill the app
            // with a layout loop. I think this is a platform bug so we need it for now. If you don't
            // believe me remove it and see what happens.
            _deviceScaleFactor = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel + 0.001;
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a source.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static bool CanHandlePost(ContentPanelSource source)
        {
            // See if we can get an image from the url
            return !string.IsNullOrWhiteSpace(ImageManager.GetImageUrl(source.Url));
        }

        #region IContentPanel

        /// <summary>
        /// Indicates how large the panel is in memory.
        /// </summary>
        public PanelMemorySizes PanelMemorySize
        {
            get
            {
                ulong size;

                lock (_lockObject)
                {
                    if (_imageSourceStream == null) return PanelMemorySizes.Small;

                    // #todo revisit these sizes.
                    size = _imageSourceStream.Size;
                }

                if(size < 3145728)
                {
                    // < 3mb small
                    return PanelMemorySizes.Small;
                }

                return size < 8388608 ? PanelMemorySizes.Medium : PanelMemorySizes.Large;
                // > 8mb large
            }
        }

        /// <summary>
        /// Fired when we should load the content.
        /// </summary>
        public void OnPrepareContent()
        {
            // Run our work on a background thread.
            Task.Run(() =>
            {
                // Get the image Url
                var imageUrl = ImageManager.GetImageUrl(_baseContentPanel.Source.Url);

                // Make sure we got it.
                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    // This is bad, we should be able to get the url.
                    TelemetryManager.ReportUnexpectedEvent(this, "BasicImageControlNoImageUrl");

                    // Jump back to the UI thread
                    _baseContentPanel.FireOnFallbackToBrowser();
                    return;
                }

                // Make sure we aren't destroyed.
                if (_baseContentPanel.IsDestroyed)
                {
                    return;
                }

                // Fire off a request for the image.
                var request = new ImageManager.ImageManagerRequest
                {
                    ImageId = _baseContentPanel.Source.Id,
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
                _imageSourceStream = null;

                // Kill some listeners
                App.BaconMan.OnBackButton -= BaconMan_OnBackButton;
                ui_contentRoot.SizeChanged -= ContentRoot_SizeChanged;

                // Make sure the image was created, if it never loaded
                // then it won't be created.
                if (_image != null)
                {
                    var bmpImage = (BitmapImage) _image.Source;
                    if(bmpImage != null)
                    {
                        bmpImage.ImageOpened -= BitmapImage_ImageOpened;
                        bmpImage.ImageFailed -= BitmapImage_ImageFailed;
                    }

                    // Kill the image
                    _image.Source = null;
                    _image.RightTapped -= ContentRoot_RightTapped;
                    _image.Holding -= ContentRoot_Holding;
                    _image = null;
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
        /// <param name="sender"></param>
        /// <param name="response"></param>
        private async void OnRequestComplete(object sender, ImageManager.ImageManagerResponseEventArgs response)
        {
            // Remove the event
            var request = (ImageManager.ImageManagerRequest)sender;
            request.OnRequestComplete -= OnRequestComplete;

            // Jump back to the UI thread
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!response.Success)
                {
                    TelemetryManager.ReportUnexpectedEvent(this, "BasicImageControlNoImageUrl");
                    _baseContentPanel.FireOnFallbackToBrowser();
                    return;
                }

                lock (this)
                {
                    if (_baseContentPanel.IsDestroyed)
                    {
                        // Get out of here if we should be destroyed.
                        return;
                    }

                    // Grab the source, we need this to make the image
                    _imageSourceStream = response.ImageStream;

                    // Add the image to the UI
                    _image = new Image();

                    // We don't want to wait on this.
#pragma warning disable CS4014
                    // Set the image.
                    ReloadImage(false);
#pragma warning restore CS4014

                    // Set the image into the UI.
                    ui_scrollViewer.Content = _image;

                    // Setup the save image tap
                    _image.RightTapped += ContentRoot_RightTapped;
                    _image.Holding += ContentRoot_Holding;
                }
            });
        }

        /// <summary>
        /// Reloads (or loads) the image with a decode size that is scaled to the
        /// screen. Unless useFulsize is set.
        /// </summary>
        /// <param name="useFullSize"></param>
        private bool ReloadImage(bool useFullSize)
        {
            // Don't worry about this if we are destroyed.
            if (_baseContentPanel.IsDestroyed)
            {
                return false;
            }

            double currentControlSizeWidth;
            double currentControlSizeHeight;
            InMemoryRandomAccessStream stream;

            lock (_lockObject)
            {
                stream = _imageSourceStream;

                if(stream == null)
                {
                    return false;
                }

                currentControlSizeWidth = _currentControlSize.Width;
                currentControlSizeHeight = _currentControlSize.Height;

                // If we are already this size don't do anything.
                if (_lastImageSetSize.Equals(_currentControlSize) && !useFullSize)
                {
                    return false;
                }

                // If we don't have a control size yet don't do anything.
                if(_currentControlSize.IsEmpty)
                {
                    return false;
                }

                // Don't do anything if we are already full size.
                if (useFullSize && Math.Abs(_lastImageSetSize.Width) < 1 && Math.Abs(_lastImageSetSize.Height) < 1)
                {
                    return false;
                }

                // Set our last size.
                if(useFullSize)
                {
                    // Set our current size.
                    _lastImageSetSize.Width = 0;
                    _lastImageSetSize.Height = 0;
                }
                else
                {
                    // Set our current size.
                    _lastImageSetSize.Width = _currentControlSize.Width;
                    _lastImageSetSize.Height = _currentControlSize.Height;
                }
            }

            // Create a bitmap and
            var bitmapImage = new BitmapImage {CreateOptions = BitmapCreateOptions.None};
            bitmapImage.ImageOpened += BitmapImage_ImageOpened;
            bitmapImage.ImageFailed += BitmapImage_ImageFailed;

            // Get the decode height and width.
            var decodeWidth = 0;
            var decodeHeight = 0;
            if (!useFullSize)
            {
                var widthRatio = bitmapImage.PixelWidth / currentControlSizeWidth;
                var heightRatio = bitmapImage.PixelHeight / currentControlSizeHeight;
                if (widthRatio > heightRatio)
                {
                    decodeWidth = (int)currentControlSizeWidth;
                }
                else
                {
                    decodeHeight = (int)currentControlSizeHeight;
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
            lock(_lockObject)
            {
                var currentImage = (BitmapImage) _image.Source;
                if (currentImage != null)
                {
                    // Remove the handlers
                    currentImage.ImageOpened -= BitmapImage_ImageOpened;
                    currentImage.ImageFailed -= BitmapImage_ImageFailed;
                }

                // Set the image.
                _image.Source = bitmapImage;
            }
            return true;
        }

        /// <summary>
        /// Fired when the image is actually ready.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BitmapImage_ImageOpened(object sender, RoutedEventArgs e)
        {
            if(_baseContentPanel.IsDestroyed)
            {
                return;
            }

            // Update the zoom if needed
            await SetScrollZoomFactors();

            // Hide the loading screen
            _baseContentPanel.FireOnLoading(false);

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
            switch (_state)
            {
                case ImageState.Unloaded:
                    _state = ImageState.Normal;
                    break;
                case ImageState.EnteringFullscreen:
                    _state = ImageState.EnteringFullscreenComplete;
                    break;
                case ImageState.ExitingFullscreen:
                    _state = ImageState.Normal;
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
            _baseContentPanel.FireOnError(true, "This image failed to load");
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
            if (!string.IsNullOrWhiteSpace(_baseContentPanel.Source.Url))
            {
                App.BaconMan.ImageMan.SaveImageLocally(_baseContentPanel.Source.Url);
            }
        }

        /// <summary>
        /// Fired when the image is right clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentRoot_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                var p = e.GetPosition(element);
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
            var element = sender as FrameworkElement;
            if (element != null)
            {
                var p = e.GetPosition(element);
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
            if (_state != ImageState.Normal)
            {
                return;
            }

            // If the zooms don't match go full screen
            if (!AreCloseEnoughToEqual(_minZoomFactor, ui_scrollViewer.ZoomFactor))
            {
                // Set the state and hide the image, we do this make the transition smoother.
                _state = ImageState.EnteringFullscreen;
                VisualStateManager.GoToState(this, "HideImage", true);
                ui_scrollViewer.Visibility = Visibility.Collapsed;

                // wait a second for the vis change to apply
                await Task.Delay(10);

                // Try to go full screen
                _baseContentPanel.FireOnFullscreenChanged(true);

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
            if (_state == ImageState.EnteringFullscreenComplete)
            {
                _state = ImageState.Fullscreen;
                return;
            }

            if (_state != ImageState.Fullscreen)
            {
                return;
            }

            // If the two zoom factors are close enough, leave full screen.
            if (!AreCloseEnoughToEqual(_minZoomFactor, ui_scrollViewer.ZoomFactor)) return;

            // Set the state and hide the image, we do this make the transition smoother.
            _state = ImageState.ExitingFullscreen;
            ui_scrollViewer.Visibility = Visibility.Collapsed;
            VisualStateManager.GoToState(this, "HideImage", true);

            // Try to leave full screen.
            _baseContentPanel.FireOnFullscreenChanged(false);

            // Delay for a bit to let full screen settle.
            await Task.Delay(10);

            // Resize the image
            ReloadImage(false);
        }

        /// <summary>
        /// Fired when the user presses back.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BaconMan_OnBackButton(object sender, BaconBackend.BackButtonArgs e)
        {
            if (e.IsHandled)
            {
                return;
            }

            // If we are full screen reset the scroller which will take us out.
            if (_baseContentPanel.IsFullscreen)
            {
                e.IsHandled = true;
                ui_scrollViewer.ChangeView(null, null, _minZoomFactor);
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
            _currentControlSize = e.NewSize;

            if(_state == ImageState.Unloaded)
            {
                // If we are unloaded call this to ensure the image it loading.
                ReloadImage(false);
            }
            else if(_state == ImageState.Normal)
            {
                // Only reload the image if the size is larger than the current image.
                var didImageResize = false;
                if (_lastImageSetSize.Height < _currentControlSize.Height && _lastImageSetSize.Width < _currentControlSize.Width)
                {
                    // Resize the image.
                    didImageResize = ReloadImage(false);
                }

                // if we didn't resize the image just set the new zoom.
                if (!didImageResize)
                {
                    _state = ImageState.NormalSizeUpdating;

                    await SetScrollZoomFactors();

                    _state = ImageState.Normal;
                }
            }
        }

        /// <summary>
        /// Called when we should update the scroll zoom factor.
        /// </summary>
        private async Task SetScrollZoomFactors()
        {
            BitmapImage bitmapImage;

            lock (_lockObject)
            {
                // If we don't have a size yet ignore.
                if (_image?.Source == null || Math.Abs(_image.ActualHeight) < 1 || Math.Abs(_image.ActualWidth) < 1)
                {
                    return;
                }

                // Get the image and the size.
                bitmapImage = (BitmapImage)_image.Source;
            }

            var imageSize = new Size(bitmapImage.PixelWidth, bitmapImage.PixelHeight);

            // No matter what they pixel type, the scroll always seems to look at decoded pixel values
            // and not the real sizes. So we need to make this size match the decoded size.
            if (bitmapImage.DecodePixelWidth != 0)
            {
                var ratio = bitmapImage.PixelWidth / (double)bitmapImage.DecodePixelWidth;
                imageSize.Width /= ratio;
                imageSize.Height /= ratio;
            }
            else if (bitmapImage.DecodePixelHeight != 0)
            {
                var ratio = bitmapImage.PixelHeight / (double)bitmapImage.DecodePixelHeight;

                imageSize.Width /= ratio;
                imageSize.Height /= ratio;
            }

            // If we are using logical pixels and have a decode width or height we want to
            // scale the actual height and width down. We must do this because the scroll
            // expects logical pixels.
            if (bitmapImage.DecodePixelType == DecodePixelType.Logical)// && (bitmapImage.DecodePixelHeight != 0 || bitmapImage.DecodePixelWidth != 0))
            {

            }

            await SetScrollZoomFactors(imageSize);
        }

        /// <summary>
        /// Called when we should update the scroller zoom factor.
        /// NOTE!! The input sizes should be in logical pixels not physical
        /// </summary>
        private async Task SetScrollZoomFactors(Size imageSize)
        {
            if (Math.Abs(imageSize.Height) < 1 || Math.Abs(imageSize.Width) < 1 || Math.Abs(ui_scrollViewer.ActualHeight) < 1 || Math.Abs(ui_scrollViewer.ActualWidth) < 1)
            {
                return;
            }

            // If we are full screen don't update this.
            if (_state == ImageState.Fullscreen)
            {
                return;
            }

            // Figure out what the min zoom should be.
            var verticalZoomFactor = (float)(ui_scrollViewer.ActualHeight / imageSize.Height);
            var horizontalZoomFactor = (float)(ui_scrollViewer.ActualWidth / imageSize.Width);
            _minZoomFactor = Math.Min(verticalZoomFactor, horizontalZoomFactor);

            // For some reason, if the zoom factor is larger than 1 we set
            //it to 1 and everything is correct.
            if(_minZoomFactor > 1)
            {
                _minZoomFactor = 1;
            }

            // Do a check to make sure the zoom level is ok.
            if (_minZoomFactor < 0.1)
            {
                _minZoomFactor = 0.1f;
            }

            // Set the zoom to the min size for the zoom
            ui_scrollViewer.MinZoomFactor = _minZoomFactor;

            float newZoomFactor;
            double? offset = null;
            if(_state == ImageState.EnteringFullscreen)
            {
                // When entering full screen always go to the min zoom.
                newZoomFactor = _minZoomFactor;

                // Make sure we aren't too close to our min zoom.
                if (AreCloseEnoughToEqual(newZoomFactor, _minZoomFactor))
                {
                    // If so make it a little more so we don't instantly jump out of full screen.
                    newZoomFactor += 0.002f;
                }
            }
            else
            {
                // If we don't have an image already set the zoom to the min zoom.
                newZoomFactor = _minZoomFactor;
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
        private static bool AreCloseEnoughToEqual(float num1, float num2)
        {
            return Math.Abs(num1 - num2) < .001;
        }
    }
}
