using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using Baconit.Interfaces;
using Microsoft.Band;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.Panels.SettingsPanels
{
    public sealed partial class MicrosoftBandSettings : UserControl, IPanel
    {
        private const string CEarthPornReplace = "earthimages";
        private List<string> _mSubredditNameList = new List<string> { "earthporn" };

        private readonly DispatcherTimer _mBandCheckTimer;

        private bool _mHasChanges;
        private bool _mIgnoreUpdates;
        private IPanelHost _mHost;

        public MicrosoftBandSettings()
        {
            InitializeComponent();

            _mBandCheckTimer = new DispatcherTimer();
            _mBandCheckTimer.Tick += BandCheckTimer_Tick;
            _mBandCheckTimer.Interval = new TimeSpan(0, 0, 5);
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
            ui_loadingOverlay.Margin = new Thickness(0, -statusBarHeight, 0, 0);

            // Star the band detection timer
            BandCheckTimer_Tick(null, null);

            // Update the UI
            _mIgnoreUpdates = true;

            ui_enableBandWallpaperUpdate.IsOn = App.BaconMan.BackgroundMan.ImageUpdaterMan.IsBandWallpaperEnabled;
            ui_wallpaperSubreddit.IsEnabled = App.BaconMan.BackgroundMan.ImageUpdaterMan.IsBandWallpaperEnabled;
            SetupSubredditLists();

            ui_syncToBand.IsOn = App.BaconMan.BackgroundMan.BandMan.ShowInboxOnBand;
            ui_syncToBand.IsEnabled = App.BaconMan.BackgroundMan.MessageUpdaterMan.IsEnabled;
            ui_messageInboxNotEnabled.Visibility = App.BaconMan.BackgroundMan.MessageUpdaterMan.IsEnabled ? Visibility.Collapsed : Visibility.Visible;

            _mIgnoreUpdates = false;
        }

        public async void OnNavigatingFrom()
        {
            // Stop the timer
            _mBandCheckTimer.Stop();

            // When we leave run an update
            if (_mHasChanges)
            {
                _mHasChanges = false;

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

        /// <summary>
        /// Ticks every 5 seconds until a band is found.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BandCheckTimer_Tick(object sender, object e)
        {
            var pairedBands = await BandClientManager.Instance.GetBandsAsync();
            if (pairedBands.Length != 0)
            {
                ui_noBandConnected.Visibility = Visibility.Collapsed;
                ui_noBandBlock.Visibility = Visibility.Collapsed;
                _mBandCheckTimer.Stop();

                // Get the band version
                App.BaconMan.BackgroundMan.BandMan.GetBandVersion();
            }
            else
            {
                ui_noBandConnected.Visibility = Visibility.Visible;
                ui_noBandBlock.Visibility = Visibility.Visible;
                _mBandCheckTimer.Start();
            }
        }

        #region Subreddit updating

        private void EnableBandWallpaperUpdate_Toggled(object sender, RoutedEventArgs e)
        {
            if(_mIgnoreUpdates)
            {
                return;
            }

            App.BaconMan.BackgroundMan.ImageUpdaterMan.IsBandWallpaperEnabled = ui_enableBandWallpaperUpdate.IsOn;
            ui_wallpaperSubreddit.IsEnabled = ui_enableBandWallpaperUpdate.IsOn;
            _mHasChanges = true;
        }

        private void WallpaperSubreddit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_mIgnoreUpdates)
            {
                return;
            }

            App.BaconMan.BackgroundMan.ImageUpdaterMan.BandSubredditName = (string)ui_wallpaperSubreddit.SelectedItem;
            _mHasChanges = true;
        }

        private void SetupSubredditLists()
        {
            // Get the current name list.
            _mSubredditNameList = SubredditListToNameList(App.BaconMan.SubredditMan.SubredditList);

            // See if the user is signed in.
            var isUserSignedIn = App.BaconMan.UserMan.IsUserSignedIn;

            // Try to find the indexes
            var bandIndex = -1;
            var earthPornIndex = -1;
            var count = 0;
            foreach (var name in _mSubredditNameList)
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
                _mSubredditNameList[earthPornIndex] = CEarthPornReplace;
            }

            // Fix up the subs if they weren't found.
            if (bandIndex == -1)
            {
                _mSubredditNameList.Add(App.BaconMan.BackgroundMan.ImageUpdaterMan.DesktopSubredditName);
                bandIndex = _mSubredditNameList.Count - 1;
            }

            // Set the list
            ui_wallpaperSubreddit.ItemsSource = _mSubredditNameList;

            // Set the values
            ui_wallpaperSubreddit.SelectedIndex = bandIndex;
        }

        private List<string> SubredditListToNameList(List<Subreddit> subreddits)
        {
            var nameList = new List<string>();
            foreach (var sub in subreddits)
            {
                nameList.Add(sub.DisplayName.ToLower());
            }
            return nameList;
        }

        #endregion

        #region Inbox Settings

        private async void SyncToBand_Toggled(object sender, RoutedEventArgs e)
        {
            if(_mIgnoreUpdates)
            {
                return;
            }

            // When this is toggled we need to make sure we setup the band tile
            ui_loadingOverlay.Show(true, "Connecting To Your Band");

            // Update the setting
            App.BaconMan.BackgroundMan.BandMan.ShowInboxOnBand = ui_syncToBand.IsOn;

            // Attempt to set the new state.
            var wasSuccess = await App.BaconMan.BackgroundMan.BandMan.EnsureBandTileState();

            // Done, hide the loading.
            ui_loadingOverlay.Hide();

            if (!wasSuccess)
            {
                // We failed, reset the values
                _mIgnoreUpdates = true;
                App.BaconMan.BackgroundMan.BandMan.ShowInboxOnBand = !App.BaconMan.BackgroundMan.BandMan.ShowInboxOnBand;
                ui_syncToBand.IsOn = !ui_syncToBand.IsOn;
                _mIgnoreUpdates = false;

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
