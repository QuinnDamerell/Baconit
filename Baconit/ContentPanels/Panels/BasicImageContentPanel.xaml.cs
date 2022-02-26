using BaconBackend.Managers;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using BaconBackend.DataObjects;

namespace Baconit.ContentPanels.Panels
{
    public class ImageViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ObservableCollection<MediaImage> _images = new ObservableCollection<MediaImage>();
        public ObservableCollection<MediaImage> Images
        {
            get => _images;
            set
            {
                _images = value;
                OnPropertyChanged();
            }
        }

        private MediaImage _selectedItem;
        public MediaImage SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
            }
        }

        private int _imageCount;
        public int ImageCount
        {
            get => _imageCount;
            set
            {
                _imageCount = value;
                OnPropertyChanged();
            }
        }

        private bool _isGallery;
        public bool IsGallery
        {
            get => _isGallery;
            set
            {
                _isGallery = value;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed partial class BasicImageContentPanel : IContentPanel
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
            //ui_scrollViewer.MaxZoomFactor = MaxZoomFactor;

            // Get the current scale factor. Why 0.001? If we don't add that we kill the app
            // with a layout loop. I think this is a platform bug so we need it for now. If you don't
            // believe me remove it and see what happens.
            _deviceScaleFactor = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel + 0.001;
            DataContext = new ImageViewModel();

        }

        /// <summary>
        /// Called by the host when it queries if we can handle a source.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static bool CanHandlePost(ContentPanelSource source)
        {
            // See if we can get an image from the url
            return !string.IsNullOrWhiteSpace(ImageManager.GetImageUrl(source.Url)) || source.IsImageGallery;
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
            var images = new List<MediaImage>();

            // Run our work on a background thread.
            if (_baseContentPanel.Source.IsImageGallery)
            {
                images = _baseContentPanel.Source.RedditImageGalleryUriList.Select((p, index) => new MediaImage
                {
                    Uri = p,
                    Index = index + 1,
                    Url = p.ToString(),
                    Id = (index + 1).ToString()
                }).ToList();
            }
            else
            {
                var url = new Uri(_baseContentPanel.Source.Url);
                images.Add(new MediaImage
                {
                    Uri = url,
                    Index = 1,
                    Url = url.ToString(),
                    Id = "1"
                });
            }

            if (DataContext is ImageViewModel viewModel)
            {
                viewModel.IsGallery = images.Count > 1;
                viewModel.ImageCount = images.Count;
                viewModel.Images = new ObservableCollection<MediaImage>(images);
            }

            _baseContentPanel.FireOnLoading(false);
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
            if (!(sender is FrameworkElement element)) return;
            var p = e.GetPosition(element);
            flyoutMenu.ShowAt(element, p);
        }

        /// <summary>
        /// Fired when the image is press and held
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentRoot_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element)) return;
            var p = e.GetPosition(element);
            flyoutMenu.ShowAt(element, p);
        }

        #endregion

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
                //ui_scrollViewer.ChangeView(null, null, _minZoomFactor);
            }
        }
    }
}
