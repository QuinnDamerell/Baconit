using Baconit.Interfaces;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.Panels.SettingsPanels
{
    public sealed partial class BackgroundMessageUpdatingSettings : UserControl, IPanel
    {
        private bool _mHasChanges;
        private bool _mIgnoreUpdates;
        private IPanelHost _mHost;

        public BackgroundMessageUpdatingSettings()
        {
            InitializeComponent();
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            _mHost = host;
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

            _mIgnoreUpdates = true;

            ui_enableBackgroundMessages.IsOn = App.BaconMan.BackgroundMan.MessageUpdaterMan.IsEnabled;
            ui_messageNotificationType.SelectedIndex = App.BaconMan.BackgroundMan.MessageUpdaterMan.NotificationType;
            ui_addNotesSliently.IsOn = App.BaconMan.BackgroundMan.MessageUpdaterMan.AddToNotificationCenterSilently;

            _mIgnoreUpdates = false;
        }

        public async void OnNavigatingFrom()
        {
            // When we leave run an update
            if (_mHasChanges)
            {
                _mHasChanges = false;

                // Make sure the updater is enabled if it should be.
                await App.BaconMan.BackgroundMan.EnsureBackgroundSetup();
            }
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

        private void MessageNotificationType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(_mIgnoreUpdates)
            {
                return;
            }

            App.BaconMan.BackgroundMan.MessageUpdaterMan.NotificationType = ui_messageNotificationType.SelectedIndex;
        }

        private void EnableBackgroundMessages_Toggled(object sender, RoutedEventArgs e)
        {
            if (_mIgnoreUpdates)
            {
                return;
            }

            App.BaconMan.BackgroundMan.MessageUpdaterMan.IsEnabled = ui_enableBackgroundMessages.IsOn;
            _mHasChanges = true;
        }

        private void AddNotesSliently_Toggled(object sender, RoutedEventArgs e)
        {
            if (_mIgnoreUpdates)
            {
                return;
            }

            App.BaconMan.BackgroundMan.MessageUpdaterMan.AddToNotificationCenterSilently = ui_addNotesSliently.IsOn;
        }
    }
}
