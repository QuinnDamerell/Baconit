using Baconit.Interfaces;
using Microsoft.Band;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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
    public sealed partial class BackgroundMessageUpdatingSettings : UserControl, IPanel
    {
        bool m_hasChanges = false;
        bool m_ignoreUpdates = false;
        IPanelHost m_host;

        public BackgroundMessageUpdatingSettings()
        {
            this.InitializeComponent();
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            m_host = host;
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

            m_ignoreUpdates = true;

            ui_enableBackgroundMessages.IsOn = App.BaconMan.BackgroundMan.MessageUpdaterMan.IsEnabled;
            ui_messageNotificationType.SelectedIndex = App.BaconMan.BackgroundMan.MessageUpdaterMan.NotificationType;
            ui_addNotesSliently.IsOn = App.BaconMan.BackgroundMan.MessageUpdaterMan.AddToNotificationCenterSilently;

            m_ignoreUpdates = false;
        }

        public async void OnNavigatingFrom()
        {
            // When we leave run an update
            if (m_hasChanges)
            {
                m_hasChanges = false;

                // Make sure the updater is enabled if it should be.
                await App.BaconMan.BackgroundMan.EnsureBackgroundSetup();
            }
        }

        private void MessageNotificationType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(m_ignoreUpdates)
            {
                return;
            }

            App.BaconMan.BackgroundMan.MessageUpdaterMan.NotificationType = ui_messageNotificationType.SelectedIndex;
        }

        private void EnableBackgroundMessages_Toggled(object sender, RoutedEventArgs e)
        {
            if (m_ignoreUpdates)
            {
                return;
            }

            App.BaconMan.BackgroundMan.MessageUpdaterMan.IsEnabled = ui_enableBackgroundMessages.IsOn;
            m_hasChanges = true;
        }

        private void AddNotesSliently_Toggled(object sender, RoutedEventArgs e)
        {
            if (m_ignoreUpdates)
            {
                return;
            }

            App.BaconMan.BackgroundMan.MessageUpdaterMan.AddToNotificationCenterSilently = ui_addNotesSliently.IsOn;
        }    
    }
}
