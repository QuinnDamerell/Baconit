using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.Panels
{
    public sealed partial class WelcomePanel : UserControl, IPanel
    {
        private IPanelHost _mHost;

        public WelcomePanel()
        {
            InitializeComponent();

            // Setup the image
            var uriList = new List<Uri>();
            uriList.Add(new Uri("ms-appx:///Assets/Welcome/MarilynImageMedium.jpg", UriKind.Absolute));
            ui_slidingImageControl.SetImages(uriList);
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            _mHost = host;
        }

        public void OnNavigatingFrom()
        {
            ui_slidingImageControl.StopAnimation();
        }

        public async void OnNavigatingTo()
        {
            if(_mHost.CurrentScreenMode() == ScreenMode.Split)
            {
                ui_slidingImageControl.BeginAnimation();
            }

            // Set the status bar color and get the size returned. If it is not 0 use that to move the
            // color of the page into the status bar.
            var statusBarHeight = await _mHost.SetStatusBar(null, 0);
            ui_contentRoot.Margin = new Thickness(0, -statusBarHeight, 0, 0);
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            OnNavigatingTo();
        }

        public void OnCleanupPanel()
        {
            // Ignore for now.
        }

        /// <summary>
        /// Fired when the panel should try to reduce memory if possible. This will only be called
        /// while the panel isn't visible.
        /// </summary>
        public void OnReduceMemory()
        {
            // Ignore for now.
        }
    }
}
