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

        public BackgroundMessageUpdatingSettings()
        {
            this.InitializeComponent();
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
        }

        public void OnNavigatingTo()
        {
            m_ignoreUpdates = true;

            ui_enableBackgroundMessages.IsOn = App.BaconMan.BackgroundMan.MessageUpdaterMan.IsEnabled;
            ui_messageNotificationType.SelectedIndex = App.BaconMan.BackgroundMan.MessageUpdaterMan.NotificationType;
            ui_addNotesSliently.IsOn = App.BaconMan.BackgroundMan.MessageUpdaterMan.AddToNotificationCenterSilently;
            ui_syncToBand.IsOn = App.BaconMan.BackgroundMan.BandMan.ShowInboxOnBand;
            CheckForBand();

            m_ignoreUpdates = false;
        }

        public async void CheckForBand()
        {
            IBandInfo[] pairedBands = await BandClientManager.Instance.GetBandsAsync();
            if(pairedBands.Length != 0)
            {
                ui_syncToBand.IsEnabled = true;
                ui_noBandConnected.Visibility = Visibility.Collapsed;
            }
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

        /// <summary>
        /// This is really odd, but it is the best we can do. When we enable the tile the band SDK will pop up a view 
        /// for the user to confirm. After they do so it will navigate us back to the main subreddit list. This is really odd, but
        /// we can't do anything about it. :(
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SyncToBand_Toggled(object sender, RoutedEventArgs e)
        {
            if (m_ignoreUpdates)
            {
                return;
            }

            // When this is toggled we need to make sure we setup the band tile
            ui_loadingOverlay.Show(true, "Connecting To Your Band");

            // Update the setting
            App.BaconMan.BackgroundMan.BandMan.ShowInboxOnBand = ui_syncToBand.IsOn;

            // Attempt to set the new state.
            bool wasSuccess = await App.BaconMan.BackgroundMan.BandMan.EnsureBandTileState();            

            // Done, hide the loading.
            ui_loadingOverlay.Hide();

            if (!wasSuccess)
            {
                // We failed, reset the values
                m_ignoreUpdates = true;
                App.BaconMan.BackgroundMan.BandMan.ShowInboxOnBand = !App.BaconMan.BackgroundMan.BandMan.ShowInboxOnBand;
                ui_syncToBand.IsOn = !ui_syncToBand.IsOn;
                m_ignoreUpdates = false;

                // Show a message
                App.BaconMan.MessageMan.ShowMessageSimple("Can't Connect", "We can't connected to you band right now. Make sure it is in range of your device.");
            }
            else
            {
                if (App.BaconMan.BackgroundMan.BandMan.ShowInboxOnBand)
                {
                    App.BaconMan.MessageMan.ShowMessageSimple("Mission Complete", "You can now find the Baconit tile on your Band!");
                }
                else
                {
                    App.BaconMan.MessageMan.ShowMessageSimple("All Done", "We have succesfully removed the Baconit tile from your Band.");
                }
            }
        }
    }
}
