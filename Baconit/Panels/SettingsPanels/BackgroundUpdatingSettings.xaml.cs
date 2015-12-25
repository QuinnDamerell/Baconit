using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System.UserProfile;
using Windows.UI.Core;
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
    public sealed partial class BackgroundUpdatingSettings : UserControl, IPanel
    {
        const string c_earthPornReplace = "earthimages";
        List<string> m_updateFrequencys = new List<string> { "30 minutes", "1 hour", "2 hours", "3 hours", "4 hours", "5 hours", "daily" };
        List<string> m_subredditNameList = new List<string> { "earthporn" };
        bool m_ingoreUpdates = false;
        bool m_hasChanges = true;
        IPanelHost m_host;

        public BackgroundUpdatingSettings()
        {
            this.InitializeComponent();
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            // Ignore
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            m_host = host;
        }

        public async void OnNavigatingFrom()
        {
            // Update the settings
            App.BaconMan.BackgroundMan.ImageUpdaterMan.UpdateFrquency = FrequencyListIndexToSettings(ui_imageFrequency.SelectedIndex);
            App.BaconMan.BackgroundMan.ImageUpdaterMan.IsDeskopEnabled = ui_enableDesktop.IsOn;
            App.BaconMan.BackgroundMan.ImageUpdaterMan.IsLockScreenEnabled = ui_enableLockScreen.IsOn;
            App.BaconMan.BackgroundMan.ImageUpdaterMan.DesktopSubredditName = (string)ui_desktopSource.SelectedItem;
            App.BaconMan.BackgroundMan.ImageUpdaterMan.LockScreenSubredditName = (string)ui_lockScreenSource.SelectedItem;

            // See below for a full comment on this. But if the user isn't logged in we want to clean up the name
            // a little.
            if(App.BaconMan.BackgroundMan.ImageUpdaterMan.DesktopSubredditName.Equals(c_earthPornReplace))
            {
                App.BaconMan.BackgroundMan.ImageUpdaterMan.DesktopSubredditName = "earthporn";
            }
            if (App.BaconMan.BackgroundMan.ImageUpdaterMan.LockScreenSubredditName.Equals(c_earthPornReplace))
            {
                App.BaconMan.BackgroundMan.ImageUpdaterMan.LockScreenSubredditName = "earthporn";
            }

            // Kill the listener
            App.BaconMan.SubredditMan.OnSubredditsUpdated -= SubredditMan_OnSubredditsUpdated;

            // When we leave run an update
            if(m_hasChanges)
            {
                m_hasChanges = false;

                // Make sure the updater is enabled
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

        public async void OnNavigatingTo()
        {
            // Set the status bar color and get the size returned. If it is not 0 use that to move the
            // color of the page into the status bar.
            double statusBarHeight = await m_host.SetStatusBar(null, 0);
            ui_contentRoot.Margin = new Thickness(0, -statusBarHeight, 0, 0);
            ui_contentRoot.Padding = new Thickness(0, statusBarHeight, 0, 0);

            m_ingoreUpdates = true;

            // Setup the UI
            ui_enableLockScreen.IsOn = App.BaconMan.BackgroundMan.ImageUpdaterMan.IsLockScreenEnabled;
            ui_enableDesktop.IsOn = App.BaconMan.BackgroundMan.ImageUpdaterMan.IsDeskopEnabled;
            SetupSubredditLists();
            ui_imageFrequency.ItemsSource = m_updateFrequencys;
            ui_imageFrequency.SelectedIndex = FrequencySettingToListIndex(App.BaconMan.BackgroundMan.ImageUpdaterMan.UpdateFrquency);

            m_ingoreUpdates = false;

            // Set our status
            ui_lastUpdate.Text = "Last Update: " + (App.BaconMan.BackgroundMan.LastUpdateTime.Equals(new DateTime(0)) ? "Never" : App.BaconMan.BackgroundMan.LastUpdateTime.ToString("g"));
            ui_currentSystemUpdateStatus.Text = "System State: " + (App.BaconMan.BackgroundMan.LastSystemBackgroundUpdateStatus != 3 ? "Allowed" : "Denied");

            // Setup the listener
            App.BaconMan.SubredditMan.OnSubredditsUpdated += SubredditMan_OnSubredditsUpdated;
        }

        /// <summary>
        /// If the subreddits change update the UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SubredditMan_OnSubredditsUpdated(object sender, BaconBackend.Managers.OnSubredditsUpdatedArgs e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                SetupSubredditLists();
            });            
        }

        #region Click Helpers

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if(m_ingoreUpdates)
            {
                return;
            }

            m_hasChanges = true;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_ingoreUpdates)
            {
                return;
            }

            m_hasChanges = true;
        }

        #endregion

        private int FrequencySettingToListIndex(int mintues)
        {
            int desiredIndex = 0;
            if (mintues <= 30)
            {
                desiredIndex = 0;
            }
            else if(mintues <= 60)
            {
                desiredIndex = 1;
            }
            else if (mintues <= 120)
            {
                desiredIndex = 2;
            }
            else if (mintues <= 180)
            {
                desiredIndex = 3;
            }
            else if (mintues <= 240)
            {
                desiredIndex = 4;
            }
            else if (mintues <= 300)
            {
                desiredIndex = 5;
            }
            else
            {
                desiredIndex = 6;
            }
            if(desiredIndex >= m_updateFrequencys.Count)
            {
                desiredIndex = m_updateFrequencys.Count - 1;
            }
            return desiredIndex;
        }

        private int FrequencyListIndexToSettings(int index)
        {
            switch(index)
            {
                case 0:
                    {
                        return 30;
                    }
                case 1:
                    {
                        return 60;
                    }
                default:
                case 2:
                    {
                        return 120;
                    }
                case 3:
                    {
                        return 180;
                    }
                case 4:
                    {
                        return 240;
                    }
                case 5:
                    {
                        return 300;
                    }
                case 6:
                    {
                        return 1440;
                    }
            }
        }

        private void SetupSubredditLists()
        {
            // Get the current name list.
            m_subredditNameList = SubredditListToNameList(App.BaconMan.SubredditMan.SubredditList);

            // See if the user is signed in.
            bool isUserSignedIn = App.BaconMan.UserMan.IsUserSignedIn;

            // Try to find the indexes
            int desktopIndex = -1;
            int lockScreenIndex = -1;
            int earthPornIndex = -1;
            int count = 0;
            foreach (string name in m_subredditNameList)
            {
                if(desktopIndex == -1)
                {
                    if(name.Equals(App.BaconMan.BackgroundMan.ImageUpdaterMan.DesktopSubredditName))
                    {
                        desktopIndex = count;
                    }
                }
                if (lockScreenIndex == -1)
                {
                    if (name.Equals(App.BaconMan.BackgroundMan.ImageUpdaterMan.LockScreenSubredditName))
                    {
                        lockScreenIndex = count;
                    }
                }
                if(earthPornIndex == -1 && !isUserSignedIn)
                {
                    if (name.Equals("earthporn"))
                    {
                        earthPornIndex = count;
                    }
                }

                if(earthPornIndex != -1 && lockScreenIndex != -1 && desktopIndex != -1)
                {
                    // We are done.
                    break;
                }

                count++;
            }

            // Some people may not like this name even though it is actually ok. If the user isn't
            // signed in and this name appears, replace it with something nicer. We also use this as a default,
            // so play nice.
            if(earthPornIndex != -1)
            {
                m_subredditNameList[earthPornIndex] = c_earthPornReplace;
            }

            // Fix up the subs if they weren't found.
            if (lockScreenIndex == -1)
            {
                m_subredditNameList.Add(App.BaconMan.BackgroundMan.ImageUpdaterMan.LockScreenSubredditName);
                lockScreenIndex = m_subredditNameList.Count - 1;
            }
            if(desktopIndex == -1)
            {
                m_subredditNameList.Add(App.BaconMan.BackgroundMan.ImageUpdaterMan.DesktopSubredditName);
                desktopIndex = m_subredditNameList.Count - 1;
            }

            // Set the list
            ui_lockScreenSource.ItemsSource = m_subredditNameList;
            ui_desktopSource.ItemsSource = m_subredditNameList;

            // Set the values
            ui_lockScreenSource.SelectedIndex = lockScreenIndex;
            ui_desktopSource.SelectedIndex = desktopIndex;
        }

        private List<string> SubredditListToNameList(List<Subreddit> subreddits)
        {
            List<string> nameList = new List<string>();
            foreach(Subreddit sub in subreddits)
            {
                nameList.Add(sub.DisplayName.ToLower());
            }
            return nameList;
        }
    }
}
