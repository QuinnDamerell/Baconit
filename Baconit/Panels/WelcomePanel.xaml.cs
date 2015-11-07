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

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.Panels
{
    public sealed partial class WelcomePanel : UserControl, IPanel
    {
        IPanelHost m_host;

        public WelcomePanel()
        {
            this.InitializeComponent();

            // Setup the image
            List<Uri> uriList = new List<Uri>();
            uriList.Add(new Uri("ms-appx:///Assets/Welcome/MarilynImageMedium.jpg", UriKind.Absolute));
            ui_slidingImageControl.SetImages(uriList);
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            m_host = host;
        }

        public void OnNavigatingFrom()
        {
            ui_slidingImageControl.StopAnimation();
        }

        public void OnNavigatingTo()
        {
            if(m_host.CurrentScreenMode() == ScreenMode.Split)
            {
                ui_slidingImageControl.BeginAnimation();                
            }
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
        }
    }
}
