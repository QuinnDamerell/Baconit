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
            m_settingsList.Add("Subreddit View");
            m_settingsList.Add("Flip View");
            m_settingsList.Add("Updating, Lock Screen, and Desktop Images");
            m_settingsList.Add("About");
            m_settingsList.Add("Privacy Policy");

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

        public void OnNavigatingTo()
        {
            // Ignore
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            // Ignore
        }

        private void SettingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (ui_settingsList.SelectedIndex)
            {
                case 0:
                    m_host.Navigate(typeof(SubredditViewSettings), "SubredditViewSettings");
                    break;
                case 1:
                    m_host.Navigate(typeof(FlipViewSettings), "FlipViewSettings");
                    break;
                case 2:
                    m_host.Navigate(typeof(BackgroundUpdatingSettings), "BackgroundUpdatingSettings");
                    break;
                case 3:
                    m_host.Navigate(typeof(AboutSettings), "AboutSettings");
                    break;
                case 4:
                    App.BaconMan.ShowGlobalContent("http://baconit.quinndamerell.com/privacy.html");
                    break;
                default:
                    break;
            }
            ui_settingsList.SelectedIndex = -1;
        }
    }
}
