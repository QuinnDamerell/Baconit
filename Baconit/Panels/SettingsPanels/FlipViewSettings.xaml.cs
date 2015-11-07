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

namespace Baconit.Panels.SettingsPanels
{
    public sealed partial class FlipViewSettings : UserControl, IPanel
    {
        bool m_takeChangeAction = false;

        public FlipViewSettings()
        {
            this.InitializeComponent();
        }
        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            // Ignore
        }

        public void OnNavigatingFrom()
        {
            // Ignore
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            // Ignore
        }

        public void OnNavigatingTo()
        {
            m_takeChangeAction = false;

            ui_preLoadComments.IsOn = App.BaconMan.UiSettingsMan.FlipView_PreloadComments;
            ui_showHelpTips.IsOn = App.BaconMan.UiSettingsMan.FlipView_ShowCommentScrollTip;

            m_takeChangeAction = true;
        }

        private void PreLoadComments_Toggled(object sender, RoutedEventArgs e)
        {
            if (!m_takeChangeAction)
            {
                return;
            }

            App.BaconMan.UiSettingsMan.FlipView_PreloadComments = ui_preLoadComments.IsOn;
        }

        private void ShowHelpTips_Toggled(object sender, RoutedEventArgs e)
        {
            if (!m_takeChangeAction)
            {
                return;
            }

            App.BaconMan.UiSettingsMan.FlipView_ShowCommentScrollTip = ui_showHelpTips.IsOn;
        }
    }
}
