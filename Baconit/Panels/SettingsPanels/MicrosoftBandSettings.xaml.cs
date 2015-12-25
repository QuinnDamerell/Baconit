using BaconBackend.DataObjects;
using BaconBackend.Helpers;
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
    public sealed partial class MicrosoftBandSettings : UserControl, IPanel
    {
        const string c_earthPornReplace = "earthimages";
        List<string> m_subredditNameList = new List<string> { "earthporn" };

        DispatcherTimer m_bandCheckTimer = null;       

        bool m_hasChanges = false;
        bool m_ignoreUpdates = false;
        IPanelHost m_host;

        public MicrosoftBandSettings()
        {
            this.InitializeComponent();

            m_bandCheckTimer = new DispatcherTimer();
            m_bandCheckTimer.Tick += BandCheckTimer_Tick;
            m_bandCheckTimer.Interval = new TimeSpan(0, 0, 5);
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
            ui_loadingOverlay.Margin = new Thickness(0, -statusBarHeight, 0, 0);            

            // Star the band detection timer
            BandCheckTimer_Tick(null, null);

            // Update the UI
            m_ignoreUpdates = true;

            ui_enableBandWallpaperUpdate.IsOn = App.BaconMan.BackgroundMan.ImageUpdaterMan.IsBandWallpaperEnabled;
            ui_wallpaperSubreddit.IsEnabled = App.BaconMan.BackgroundMan.ImageUpdaterMan.IsBandWallpaperEnabled;
            SetupSubredditLists();

            ui_syncToBand.IsOn = App.BaconMan.BackgroundMan.BandMan.ShowInboxOnBand;
            ui_syncToBand.IsEnabled = App.BaconMan.BackgroundMan.MessageUpdaterMan.IsEnabled;
            ui_messageInboxNotEnabled.Visibility = App.BaconMan.BackgroundMan.MessageUpdaterMan.IsEnabled ? Visibility.Collapsed : Visibility.Visible;

            m_ignoreUpdates = false;
        }
        
        public async void OnNavigatingFrom()
        {
            // Stop the timer
            m_bandCheckTimer.Stop();

            // When we leave run an update
            if (m_hasChanges)
            {
                m_hasChanges = false;

                // Make sure the updater is enabled if it should be.
                await App.BaconMan.BackgroundMan.EnsureBackgroundSetup();

                // On a background thread kick off an update. This call will block so it has to be done in
                // the background.
                await Task.Run(async () =>
                {
                    // Force a update, give it a null deferral since this isn't a background task.
                    await App.BaconMan.BackgroundMan.ImageUpdaterMan.RunUpdate(new RefCountedDeferral(null), true);
                });
            }
        }


        /// <summary>
        /// Ticks every 5 seconds until a band is found.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BandCheckTimer_Tick(object sender, object e)
        {
            IBandInfo[] pairedBands = await BandClientManager.Instance.GetBandsAsync();
            if (pairedBands.Length != 0)
            {
                ui_noBandConnected.Visibility = Visibility.Collapsed;
                ui_noBandBlock.Visibility = Visibility.Collapsed;
                m_bandCheckTimer.Stop();

                // Get the band version
                App.BaconMan.BackgroundMan.BandMan.GetBandVersion();
            }
            else
            {
                ui_noBandConnected.Visibility = Visibility.Visible;
                ui_noBandBlock.Visibility = Visibility.Visible;
                m_bandCheckTimer.Start();
            }
        }

        #region Subreddit updating

        private void EnableBandWallpaperUpdate_Toggled(object sender, RoutedEventArgs e)
        {
            if(m_ignoreUpdates)
            {
                return;
            }

            App.BaconMan.BackgroundMan.ImageUpdaterMan.IsBandWallpaperEnabled = ui_enableBandWallpaperUpdate.IsOn;
            ui_wallpaperSubreddit.IsEnabled = ui_enableBandWallpaperUpdate.IsOn;
            m_hasChanges = true;
        }

        private void WallpaperSubreddit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_ignoreUpdates)
            {
                return;
            }

            App.BaconMan.BackgroundMan.ImageUpdaterMan.BandSubredditName = (string)ui_wallpaperSubreddit.SelectedItem;
            m_hasChanges = true;
        }

        private void SetupSubredditLists()
        {
            // Get the current name list.
            m_subredditNameList = SubredditListToNameList(App.BaconMan.SubredditMan.SubredditList);

            // See if the user is signed in.
            bool isUserSignedIn = App.BaconMan.UserMan.IsUserSignedIn;

            // Try to find the indexes
            int bandIndex = -1;
            int earthPornIndex = -1;
            int count = 0;
            foreach (string name in m_subredditNameList)
            {
                if (bandIndex == -1)
                {
                    if (name.Equals(App.BaconMan.BackgroundMan.ImageUpdaterMan.BandSubredditName))
                    {
                        bandIndex = count;
                    }
                }
                if (earthPornIndex == -1 && !isUserSignedIn)
                {
                    if (name.Equals("earthporn"))
                    {
                        earthPornIndex = count;
                    }
                }

                if (earthPornIndex != -1 && bandIndex != -1)
                {
                    // We are done.
                    break;
                }

                count++;
            }

            // Some people may not like this name even though it is actually ok. If the user isn't
            // signed in and this name appears, replace it with something nicer. We also use this as a default,
            // so play nice.
            if (earthPornIndex != -1)
            {
                m_subredditNameList[earthPornIndex] = c_earthPornReplace;
            }

            // Fix up the subs if they weren't found.
            if (bandIndex == -1)
            {
                m_subredditNameList.Add(App.BaconMan.BackgroundMan.ImageUpdaterMan.DesktopSubredditName);
                bandIndex = m_subredditNameList.Count - 1;
            }

            // Set the list
            ui_wallpaperSubreddit.ItemsSource = m_subredditNameList;

            // Set the values
            ui_wallpaperSubreddit.SelectedIndex = bandIndex;
        }

        private List<string> SubredditListToNameList(List<Subreddit> subreddits)
        {
            List<string> nameList = new List<string>();
            foreach (Subreddit sub in subreddits)
            {
                nameList.Add(sub.DisplayName.ToLower());
            }
            return nameList;
        }

        #endregion

        #region Inbox Settings

        private async void SyncToBand_Toggled(object sender, RoutedEventArgs e)
        {
            if(m_ignoreUpdates)
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
                    App.BaconMan.MessageMan.ShowMessageSimple("All Done", "We have successfully removed the Baconit tile from your Band.");
                }
            }
        }       

        #endregion
    }
}
