using BaconBackend.Managers;
using Baconit.Interfaces;
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

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.Panels.SettingsPanels
{
    public sealed partial class FlipViewSettings : UserControl, IPanel
    {
        bool m_takeChangeAction = false;
        IPanelHost m_host;

        public FlipViewSettings()
        {
            this.InitializeComponent();
        }
        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            m_host = host;
        }

        public void OnNavigatingFrom()
        {
            // Ignore
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

            m_takeChangeAction = false;

            ui_preLoadComments.IsOn = App.BaconMan.UiSettingsMan.FlipView_PreloadComments;
            ui_showHelpTips.IsOn = App.BaconMan.UiSettingsMan.FlipView_ShowCommentScrollTip;
            ui_flipViewNsfwType.SelectedIndex = (int)App.BaconMan.UiSettingsMan.FlipView_NsfwBlockingType;
            ui_disablePostLoad.IsOn = !App.BaconMan.UiSettingsMan.FlipView_LoadPostContentWithoutAction;
            ui_preloadPost.IsOn = App.BaconMan.UiSettingsMan.FlipView_PreloadFutureContent;

            m_takeChangeAction = true;
        }

        private void PreLoadComments_Toggled(object sender, RoutedEventArgs e)
        {
            if (!m_takeChangeAction)
            {
                return;
            }

            App.BaconMan.UiSettingsMan.FlipView_PreloadComments = ui_preLoadComments.IsOn;
        }

        private void ShowHelpTips_Toggled(object sender, RoutedEventArgs e)
        {
            if (!m_takeChangeAction)
            {
                return;
            }

            App.BaconMan.UiSettingsMan.FlipView_ShowCommentScrollTip = ui_showHelpTips.IsOn;
        }

        private void FlipViewNsfwType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!m_takeChangeAction)
            {
                return;
            }

            App.BaconMan.UiSettingsMan.FlipView_NsfwBlockingType = (NsfwBlockType)ui_flipViewNsfwType.SelectedIndex;
        }

        private void PreloadPost_Toggled(object sender, RoutedEventArgs e)
        {
            if (!m_takeChangeAction)
            {
                return;
            }

            App.BaconMan.UiSettingsMan.FlipView_PreloadFutureContent = ui_preloadPost.IsOn;
        }

        private void DisablePostLoad_Toggled(object sender, RoutedEventArgs e)
        {
            if (!m_takeChangeAction)
            {
                return;
            }

            App.BaconMan.UiSettingsMan.FlipView_LoadPostContentWithoutAction = !ui_disablePostLoad.IsOn;
        }
    }
}
