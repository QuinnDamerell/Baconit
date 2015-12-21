using Baconit.Interfaces;
using Baconit.Panels.SettingsPanels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Baconit.Panels
{
    public sealed partial class Settings : UserControl, IPanel
    {
        List<string> m_settingsList = new List<string>();
        IPanelHost m_host;

        public Settings()
        {
            this.InitializeComponent();

            // Add the settings to the list
            m_settingsList.Add("Flip view");
            m_settingsList.Add("Subreddit view");
            m_settingsList.Add("Microsoft Band");
            m_settingsList.Add("Inbox background updating");
            m_settingsList.Add("Lock screen & desktop wallpaper updating");
            m_settingsList.Add("Terms and conditions");
            m_settingsList.Add("Privacy policy");
            m_settingsList.Add("About");

            // Set the list
            ui_settingsList.ItemsSource = m_settingsList;
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            m_host = host;
        }

        public void OnNavigatingFrom()
        {
            // Ignore
        }

        public async void OnNavigatingTo()
        {
            // Set the status bar color and get the size returned. If it is not 0 use that to move the
            // color of the page into the status bar.
            double statusBarHeight = await m_host.SetStatusBar(null, 0);
            ui_contentRoot.Margin = new Thickness(0, -statusBarHeight, 0, 0);
            ui_contentRoot.Padding = new Thickness(0, statusBarHeight, 0, 0);
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            OnNavigatingTo();
        }

        private void SettingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (ui_settingsList.SelectedIndex)
            {
                case 0:
                    m_host.Navigate(typeof(FlipViewSettings), "FlipViewSettings");
                    break;
                case 1:
                    m_host.Navigate(typeof(SubredditViewSettings), "SubredditViewSettings");
                    break;
                case 2:
                    m_host.Navigate(typeof(MicrosoftBandSettings), "MicrosoftBandSettings");
                    break;          
                case 3:
                    m_host.Navigate(typeof(BackgroundMessageUpdatingSettings), "BackgroundMessageUpdating");
                    break;
                case 4:
                    m_host.Navigate(typeof(BackgroundUpdatingSettings), "BackgroundUpdatingSettings");
                    break;                
                case 5:
                case 6:
                    App.BaconMan.ShowGlobalContent("http://baconit.quinndamerell.com/privacy.html");
                    break;
                case 7:
                    m_host.Navigate(typeof(AboutSettings), "AboutSettings");
                    break;
                default:
                    break;
            }
            ui_settingsList.SelectedIndex = -1;
        }
    }
}
