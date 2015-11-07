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
    public sealed partial class BasicImageFlipControl : UserControl, IFlipViewContentControl, IImageManagerCallback
    {
        bool m_isDestoryed = false;
        IFlipViewContentHost m_host;
        Image m_image;

        public BasicImageFlipControl(IFlipViewContentHost host)
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
                        m_host.ShowError();
                    });

                    return;
                }

                // Fire off a request for the image.
                ImageManager.ImageManagerRequest request = new ImageManager.ImageManagerRequest()
                {
                    Callback = this,
                    ImageId = post.Id,
                    Url = imageUrl
                };
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

                // Remove the image from the UI
                ui_contentRoot.Children.Clear();

                // Make sure the image was created, if it never loaded
                // then it won't be created.
                if (m_image != null)
                {
                    // Kill the image
                    m_image.Source = null;
                    m_image = null;
                }
            }
        }

        /// <summary>
        /// Callback when we get the image.
        /// </summary>
        /// <param name="response"></param>
        public async void OnRequestComplete(ImageManager.ImageManagerResponse response)
        {
            // Jump back to the UI thread
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (!response.Success)
                {
                    App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "BasicImageControlNoImageUrl");
                    m_host.ShowError();
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
                    BitmapImage bitImage = new BitmapImage();
                    bitImage.SetSource(response.ImageStream);

                    // Add the image to the UI
                    m_image = new Image();
                    m_image.Source = bitImage;
                    ui_contentRoot.Children.Add(m_image);

                    // Hide the loading screen
                    m_host.HideLoading();

                    // Show the image
                    ui_storyContentRoot.Begin();
                }
            });
        }
    }
}
