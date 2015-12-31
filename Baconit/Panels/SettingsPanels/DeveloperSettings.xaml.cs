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
    public sealed partial class DeveloperSettings : UserControl, IPanel
    {
        IPanelHost m_host;
        bool m_takeAction = false;

        public DeveloperSettings()
        {
            this.InitializeComponent();
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            m_host = host;
        }

        public void OnNavigatingFrom()
        {
            // Ignore
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            OnNavigatingTo();
        }

        public async void OnNavigatingTo()
        {
            // Set the status bar color and get the size returned. If it is not 0 use that to move the
            // color of the page into the status bar.
            double statusBarHeight = await m_host.SetStatusBar(null, 0);
            ui_contentRoot.Margin = new Thickness(0, -statusBarHeight, 0, 0);
            ui_contentRoot.Padding = new Thickness(0, statusBarHeight, 0, 0);

            m_takeAction = false;
            App.BaconMan.TelemetryMan.ReportEvent(this, "DevSettingsOpened");
            ui_debuggingOn.IsOn = App.BaconMan.UiSettingsMan.Developer_Debug;
            ui_preventAppCrashes.IsOn = App.BaconMan.UiSettingsMan.Developer_StopFatalCrashesAndReport;
            ui_showMemoryOverlay.IsOn = App.BaconMan.UiSettingsMan.Developer_ShowMemoryOverlay;
            m_takeAction = true;
        }

        private void DebuggingOn_Toggled(object sender, RoutedEventArgs e)
        {
            if(!m_takeAction)
            {
                return;
            }
            App.BaconMan.UiSettingsMan.Developer_Debug = ui_debuggingOn.IsOn;
        }

        private void PreventAppCrashes_Toggled(object sender, RoutedEventArgs e)
        {
            if (!m_takeAction)
            {
                return;
            }
            App.BaconMan.UiSettingsMan.Developer_StopFatalCrashesAndReport = ui_preventAppCrashes.IsOn;
        }

        private void ShowMemoryOverlay_Toggled(object sender, RoutedEventArgs e)
        {
            if(!m_takeAction)
            {
                return;
            }
            App.BaconMan.UiSettingsMan.Developer_ShowMemoryOverlay = ui_showMemoryOverlay.IsOn;

            if(ui_showMemoryOverlay.IsOn)
            {
                App.BaconMan.MemoryMan.StartMemoryWatch();
            }
            else
            {
                App.BaconMan.MemoryMan.StopMemoryWatch();
                App.BaconMan.MessageMan.ShowMessageSimple("Restart", "The UI will not be removed until the app is restarted.");
            }
        }
    }
}
