using Baconit.Interfaces;
using Baconit.Panels.SettingsPanels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Resources.Core;
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
            ResourceContext resourceContext = new ResourceContext();
            ResourceMap resourceMap = ResourceManager.Current.MainResourceMap.GetSubtree("Resources");

            // Add the settings to the list
            m_settingsList.Add(resourceMap.GetValue("FlipViewCode/Text", resourceContext).ValueAsString);
            m_settingsList.Add("Subreddit");
            m_settingsList.Add(resourceMap.GetValue("CommentsSCode/Text", resourceContext).ValueAsString);
            m_settingsList.Add("Microsoft Band");
            m_settingsList.Add(resourceMap.GetValue("InboxbackgroundupdatingCode/Text", resourceContext).ValueAsString);
            m_settingsList.Add(resourceMap.GetValue("LockscreendesktopwallpaperupdatingCode/Text", resourceContext).ValueAsString);
            m_settingsList.Add(resourceMap.GetValue("TermsandConditionsCode/Text", resourceContext).ValueAsString);
            m_settingsList.Add(resourceMap.GetValue("PrivacyPolicyCode/Text", resourceContext).ValueAsString);
            m_settingsList.Add(resourceMap.GetValue("AboutSCode/Text", resourceContext).ValueAsString);

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
                    m_host.Navigate(typeof(FlipViewSettings), "FlipViewSettings");
                    break;
                case 1:
                    m_host.Navigate(typeof(SubredditViewSettings), "SubredditViewSettings");
                    break;
                case 2:
                    m_host.Navigate(typeof(CommentSettings), "CommentsSettings");
                    break;
                case 3:
                    m_host.Navigate(typeof(MicrosoftBandSettings), "MicrosoftBandSettings");
                    break;
                case 4:
                    m_host.Navigate(typeof(BackgroundMessageUpdatingSettings), "BackgroundMessageUpdating");
                    break;
                case 5:
                    m_host.Navigate(typeof(BackgroundUpdatingSettings), "BackgroundUpdatingSettings");
                    break;
                case 6:
                case 7:
                    App.BaconMan.ShowGlobalContent("https://github.com/vitorgrs/Reddunt/wiki/Reddunt-Privacy-Policy");
                    break;
                case 8:
                    m_host.Navigate(typeof(AboutSettings), "AboutSettings");
                    break;
                default:
                    break;
            }
            ui_settingsList.SelectedIndex = -1;
        }
    }
}
