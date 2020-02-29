using BaconBackend.Managers;
using Baconit.Interfaces;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.Panels.SettingsPanels
{
    public sealed partial class FlipViewSettings : UserControl, IPanel
    {
        private bool _mTakeChangeAction;
        private IPanelHost _mHost;

        public FlipViewSettings()
        {
            InitializeComponent();
        }
        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            _mHost = host;
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
            var statusBarHeight = await _mHost.SetStatusBar(null, 0);
            ui_contentRoot.Margin = new Thickness(0, -statusBarHeight, 0, 0);
            ui_contentRoot.Padding = new Thickness(0, statusBarHeight, 0, 0);

            _mTakeChangeAction = false;

            ui_preLoadComments.IsOn = App.BaconMan.UiSettingsMan.FlipViewPreloadComments;
            ui_showHelpTips.IsOn = App.BaconMan.UiSettingsMan.FlipViewShowCommentScrollTip;
            ui_flipViewNsfwType.SelectedIndex = (int)App.BaconMan.UiSettingsMan.FlipViewNsfwBlockingType;
            ui_disablePostLoad.IsOn = !App.BaconMan.UiSettingsMan.FlipViewLoadPostContentWithoutAction;
            ui_preloadPost.IsOn = App.BaconMan.UiSettingsMan.FlipViewPreloadFutureContent;
            ui_minimizeStoryHeader.IsOn = App.BaconMan.UiSettingsMan.FlipViewMinimizeStoryHeader;

            _mTakeChangeAction = true;
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

        private void PreLoadComments_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_mTakeChangeAction)
            {
                return;
            }

            App.BaconMan.UiSettingsMan.FlipViewPreloadComments = ui_preLoadComments.IsOn;
        }

        private void ShowHelpTips_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_mTakeChangeAction)
            {
                return;
            }

            App.BaconMan.UiSettingsMan.FlipViewShowCommentScrollTip = ui_showHelpTips.IsOn;
        }

        private void FlipViewNsfwType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_mTakeChangeAction)
            {
                return;
            }

            App.BaconMan.UiSettingsMan.FlipViewNsfwBlockingType = (NsfwBlockType)ui_flipViewNsfwType.SelectedIndex;
        }

        private void PreloadPost_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_mTakeChangeAction)
            {
                return;
            }

            App.BaconMan.UiSettingsMan.FlipViewPreloadFutureContent = ui_preloadPost.IsOn;
        }

        private void DisablePostLoad_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_mTakeChangeAction)
            {
                return;
            }

            App.BaconMan.UiSettingsMan.FlipViewLoadPostContentWithoutAction = !ui_disablePostLoad.IsOn;
        }

        private void MinimizeStoryHeader_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_mTakeChangeAction)
            {
                return;
            }

            App.BaconMan.UiSettingsMan.FlipViewMinimizeStoryHeader = ui_minimizeStoryHeader.IsOn;
        }
    }
}
