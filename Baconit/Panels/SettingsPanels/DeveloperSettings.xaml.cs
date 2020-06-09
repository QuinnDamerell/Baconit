using System;
using Baconit.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using BaconBackend.Managers;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.Panels.SettingsPanels
{
    public sealed partial class DeveloperSettings : UserControl, IPanel
    {
        private IPanelHost _host;
        private bool _takeAction;

        public DeveloperSettings()
        {
            InitializeComponent();
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            _host = host;
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
            var statusBarHeight = await _host.SetStatusBar(null, 0);
            ui_contentRoot.Margin = new Thickness(0, -statusBarHeight, 0, 0);
            ui_contentRoot.Padding = new Thickness(0, statusBarHeight, 0, 0);

            _takeAction = false;
            TelemetryManager.ReportEvent(this, "DevSettingsOpened");
            ui_debuggingOn.IsOn = App.BaconMan.UiSettingsMan.DeveloperDebug;
            ui_preventAppCrashes.IsOn = App.BaconMan.UiSettingsMan.DeveloperStopFatalCrashesAndReport;
            ui_showMemoryOverlay.IsOn = App.BaconMan.UiSettingsMan.DeveloperShowMemoryOverlay;
            _takeAction = true;

            // Set the clean up text
            ui_numberPagesCleanedUp.Text = $"Pages cleaned up for memory pressure: {App.BaconMan.UiSettingsMan.PagesMemoryCleanedUp}";
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

        private void DebuggingOn_Toggled(object sender, RoutedEventArgs e)
        {
            if(!_takeAction)
            {
                return;
            }
            App.BaconMan.UiSettingsMan.DeveloperDebug = ui_debuggingOn.IsOn;
        }

        private void PreventAppCrashes_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_takeAction)
            {
                return;
            }
            App.BaconMan.UiSettingsMan.DeveloperStopFatalCrashesAndReport = ui_preventAppCrashes.IsOn;
        }

        private void ShowMemoryOverlay_Toggled(object sender, RoutedEventArgs e)
        {
            if(!_takeAction)
            {
                return;
            }
            App.BaconMan.UiSettingsMan.DeveloperShowMemoryOverlay = ui_showMemoryOverlay.IsOn;

            if (!ui_showMemoryOverlay.IsOn)
            {
                App.BaconMan.MessageMan.ShowMessageSimple("Restart", "The UI will not be removed until the app is restarted.");
            }
        }

        private void RateAndReviewReset_Click(object sender, RoutedEventArgs e)
        {
            App.BaconMan.UiSettingsMan.MainPageNextReviewAnnoy = 0;
        }
    }
}
