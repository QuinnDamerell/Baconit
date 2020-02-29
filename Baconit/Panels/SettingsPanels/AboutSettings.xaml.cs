using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Store;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using BaconBackend.Managers;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.Panels.SettingsPanels
{
    public sealed partial class AboutSettings : UserControl, IPanel
    {
        private IPanelHost _mHost;

        public AboutSettings()
        {
            InitializeComponent();
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            _mHost = host;
        }

        public void OnNavigatingFrom()
        {
            // Pause the snow if going
            ui_letItSnow.AllOfTheSnowIsNowBlackSlushPlsSuspendIt();
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            OnNavigatingTo();
        }

        public async void OnNavigatingTo()
        {
            // Set the status bar color and get the size returned. If it is not 0 use that to move the
            // color of the page into the status bar.
            var statusBarHeight = await _mHost.SetStatusBar(null, 0);
            ui_contentRoot.Margin = new Thickness(0, -statusBarHeight, 0, 0);
            ui_contentRoot.Padding = new Thickness(0, statusBarHeight, 0, 0);

            var package = Package.Current;
            var packageId = package.Id;
            var version = packageId.Version;
            ui_buildString.Text = $"Build: {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";

            TelemetryManager.ReportEvent(this, "AboutOpened");

            // Resume snow if it was going
            ui_letItSnow.OkNowIWantMoreSnowIfItHasBeenStarted();
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

        private async void RateAndReview_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store:reviewapp?appid=" + CurrentApp.AppId));
            TelemetryManager.ReportEvent(this, "RateAndReviewTapped");
        }

        private void Facebook_Tapped(object sender, TappedRoutedEventArgs e)
        {
            OpenGlobalPresenter("http://facebook.com/Baconit");
            TelemetryManager.ReportEvent(this, "FacebookOpened");
        }

        private void Website_Tapped(object sender, TappedRoutedEventArgs e)
        {
            OpenGlobalPresenter("http://baconit.quinndamerell.com/");
            TelemetryManager.ReportEvent(this, "WebsiteOpened");
        }

        private void Twitter_Tapped(object sender, TappedRoutedEventArgs e)
        {
            OpenGlobalPresenter("http://twitter.com/BaconitWP");
            TelemetryManager.ReportEvent(this, "TwitterOpened");
        }

        private void ShowSource_Tapped(object sender, TappedRoutedEventArgs e)
        {
            OpenGlobalPresenter("http://github.com/QuinnDamerell/Baconit");
            TelemetryManager.ReportEvent(this, "SourceOpened");
        }

        private void OpenBaconitSub_Tapped(object sender, TappedRoutedEventArgs e)
        {
            //#todo make this open in app when we support it
            OpenGlobalPresenter("http://reddit.com/r/Baconit");
            TelemetryManager.ReportEvent(this, "SubredditOpened");
        }

        private void Logo_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Start the snow
            ui_letItSnow.MakeItSnow();

            // Navigate to developer settings
            _mHost.Navigate(typeof(DeveloperSettings), "DeveloperSettings");
        }

        private void OpenGlobalPresenter(string url)
        {
            App.BaconMan.ShowGlobalContent(url);
        }
    }
}
