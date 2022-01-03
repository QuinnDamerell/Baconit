using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using BaconBackend.Managers;
using Baconit.Interfaces;

namespace Baconit.Panels.SettingsPanels
{
    public sealed partial class ApplicationSettings : UserControl, IPanel
    {
        private IPanelHost _host;
        private bool _takeAction;

        public ApplicationSettings()
        {
            InitializeComponent();
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            _host = host;
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
            ApplicationSettingsRoot.Margin = new Thickness(0, -statusBarHeight, 0, 0);
            ApplicationSettingsRoot.Padding = new Thickness(0, statusBarHeight, 0, 0);

            _takeAction = false;
            TelemetryManager.ReportEvent(this, "ApplicationSettings");
            AnalyticCollection.IsOn = App.BaconMan.UiSettingsMan.AnalyticCollection;
            _takeAction = true;
        }

        public void OnNavigatingFrom() { }

        public void OnCleanupPanel() { }

        public void OnReduceMemory() { }

        private void AnalyticCollectionToggle(object sender, RoutedEventArgs e)
        {
            if(!_takeAction)
            {
                return;
            }
            App.BaconMan.UiSettingsMan.AnalyticCollection = AnalyticCollection.IsOn;
        }
    }
}
