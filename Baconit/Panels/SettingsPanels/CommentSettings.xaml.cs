using BaconBackend.Collectors;
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
    public sealed partial class CommentSettings : UserControl, IPanel
    {
        bool m_takeChangeAction = false;
        IPanelHost m_host;

        public CommentSettings()
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

            SetDefaultSortType(App.BaconMan.UiSettingsMan.Comments_DefaultSortType);
            SetCount(App.BaconMan.UiSettingsMan.Comments_DefaultCount);

            m_takeChangeAction = true;
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

        private void DefaultSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(!m_takeChangeAction)
            {
                return;
            }

            switch (ui_defaultSort.SelectedIndex)
            {
                case 0:
                    App.BaconMan.UiSettingsMan.Comments_DefaultSortType = CommentSortTypes.Best;
                    break;
                case 1:
                    App.BaconMan.UiSettingsMan.Comments_DefaultSortType = CommentSortTypes.Top;
                    break;
                case 2:
                    App.BaconMan.UiSettingsMan.Comments_DefaultSortType = CommentSortTypes.Controversial;
                    break;
                case 3:
                    App.BaconMan.UiSettingsMan.Comments_DefaultSortType = CommentSortTypes.New;
                    break;
                case 4:
                    App.BaconMan.UiSettingsMan.Comments_DefaultSortType = CommentSortTypes.Old;
                    break;
                case 5:
                    App.BaconMan.UiSettingsMan.Comments_DefaultSortType = CommentSortTypes.QA;
                    break;
            }
        }

        private void DefaultCommentCount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!m_takeChangeAction)
            {
                return;
            }

            switch (ui_defaultCommentCount.SelectedIndex)
            {
                case 0:
                    App.BaconMan.UiSettingsMan.Comments_DefaultCount = 50;
                    break;
                case 1:
                    App.BaconMan.UiSettingsMan.Comments_DefaultCount = 150;
                    break;
                case 2:
                    App.BaconMan.UiSettingsMan.Comments_DefaultCount = 200;
                    break;
                case 3:
                    App.BaconMan.UiSettingsMan.Comments_DefaultCount = 300;
                    break;
                case 4:
                    // We don't allow setting the default to 500 even though the user can still do so
                    // per post.
                    //App.BaconMan.UiSettingsMan.Comments_DefaultCount = 500;
                    break;
            }
        }

        private void SetDefaultSortType(CommentSortTypes type)
        {
            switch(type)
            {
                case CommentSortTypes.Best:
                    ui_defaultSort.SelectedIndex = 0;
                    break;
                case CommentSortTypes.Controversial:
                    ui_defaultSort.SelectedIndex = 2;
                    break;
                case CommentSortTypes.New:
                    ui_defaultSort.SelectedIndex = 3;
                    break;
                case CommentSortTypes.Old:
                    ui_defaultSort.SelectedIndex = 4;
                    break;
                case CommentSortTypes.QA:
                    ui_defaultSort.SelectedIndex = 5;
                    break;
                case CommentSortTypes.Top:
                    ui_defaultSort.SelectedIndex = 1;
                    break;
            }
        }

        private void SetCount(int count)
        {
            switch(count)
            {
                case 50:
                    ui_defaultCommentCount.SelectedIndex = 0;
                    break;
                case 150:
                    ui_defaultCommentCount.SelectedIndex = 1;
                    break;
                case 200:
                    ui_defaultCommentCount.SelectedIndex = 2;
                    break;
                case 300:
                    ui_defaultCommentCount.SelectedIndex = 3;
                    break;
                case 500:
                    // We don't allow setting the default to 500 even though the user can still do so
                    // per post.
                    //ui_defaultCommentCount.SelectedIndex = 4;
                    break;
            }
        }
    }
}
