using Baconit.Interfaces;
using Baconit.Panels.SettingsPanels;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Baconit.Panels
{
    public sealed partial class Settings : UserControl, IPanel
    {
        private readonly List<string> _mSettingsList = new List<string>();
        private IPanelHost _mHost;

        public Settings()
        {
            InitializeComponent();

            // Add the settings to the list
            _mSettingsList.Add("Application");
            _mSettingsList.Add("Flip view");
            _mSettingsList.Add("Subreddit");
            _mSettingsList.Add("Comments");
            _mSettingsList.Add("Microsoft Band");
            _mSettingsList.Add("Inbox background updating");
            _mSettingsList.Add("Lock screen & desktop wallpaper updating");
            _mSettingsList.Add("Terms and conditions");
            _mSettingsList.Add("Privacy policy");
            _mSettingsList.Add("About");

            // Set the list
            ui_settingsList.ItemsSource = _mSettingsList;
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            _mHost = host;
        }

        public void OnNavigatingFrom()
        {
            // Ignore
        }

        public async void OnNavigatingTo()
        {
            // Set the status bar color and get the size returned. If it is not 0 use that to move the
            // color of the page into the status bar.
            var statusBarHeight = await _mHost.SetStatusBar(null, 0);
            ui_contentRoot.Margin = new Thickness(0, -statusBarHeight, 0, 0);
            ui_contentRoot.Padding = new Thickness(0, statusBarHeight, 0, 0);
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

        private void SettingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (ui_settingsList.SelectedIndex)
            {
                case 0:
                    _mHost.Navigate(typeof(ApplicationSettings), "ApplicationSettings");
                    break;
                case 1:
                    _mHost.Navigate(typeof(FlipViewSettings), "FlipViewSettings");
                    break;
                case 2:
                    _mHost.Navigate(typeof(SubredditViewSettings), "SubredditViewSettings");
                    break;
                case 3:
                    _mHost.Navigate(typeof(CommentSettings), "CommentsSettings");
                    break;
                case 4:
                    _mHost.Navigate(typeof(MicrosoftBandSettings), "MicrosoftBandSettings");
                    break;
                case 5:
                    _mHost.Navigate(typeof(BackgroundMessageUpdatingSettings), "BackgroundMessageUpdating");
                    break;
                case 6:
                    _mHost.Navigate(typeof(BackgroundUpdatingSettings), "BackgroundUpdatingSettings");
                    break;
                case 7:
                case 8:
                    App.BaconMan.ShowGlobalContent("http://baconit.quinndamerell.com/privacy.html");
                    break;
                case 9:
                    _mHost.Navigate(typeof(AboutSettings), "AboutSettings");
                    break;
                default:
                    break;
            }
            ui_settingsList.SelectedIndex = -1;
        }
    }
}
