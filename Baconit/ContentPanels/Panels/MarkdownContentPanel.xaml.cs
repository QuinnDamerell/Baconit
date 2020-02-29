using Baconit.Interfaces;
using System;
using UniversalMarkdown;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using BaconBackend.Managers;

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class MarkdownContentPanel : UserControl, IContentPanel
    {
        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        private readonly IContentPanelBaseInternal _mBase;

        /// <summary>
        /// The current markdown text box.
        /// </summary>
        private MarkdownTextBlock _mMarkdownBlock;

        public MarkdownContentPanel(IContentPanelBaseInternal panelBase)
        {
            InitializeComponent();
            _mBase = panelBase;
            InitializePinchHandling();
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a post.
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        static public bool CanHandlePost(ContentPanelSource source)
        {
            // If it is a self post and it has text it is for us.
            return source.IsSelf && !string.IsNullOrWhiteSpace(source.SelfText);
        }

        #region IContentPanel

        /// <summary>
        /// Indicates how large the panel is in memory.
        /// </summary>
        // #todo can we figure this out?
        public PanelMemorySizes PanelMemorySize => PanelMemorySizes.Small;

        /// <summary>
        /// Fired when we should load the content.
        /// </summary>
        /// <param name="source"></param>
        public async void OnPrepareContent()
        {
            // Since some of this can be costly, delay the work load until we aren't animating.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                _mMarkdownBlock = new MarkdownTextBlock();
                _mMarkdownBlock.OnMarkdownLinkTapped += MarkdownBlock_OnMarkdownLinkTapped;
                _mMarkdownBlock.OnMarkdownReady += MarkdownBox_OnMarkdownReady;
                _mMarkdownBlock.Markdown = _mBase.Source.SelfText;

                var fontSizeBinding = new Binding
                {
                    Source = App.BaconMan.UiSettingsMan,
                    Path = new PropertyPath("PostView_Markdown_FontSize")
                };
                _mMarkdownBlock.SetBinding(FontSizeProperty, fontSizeBinding);

                ui_contentRoot.Children.Add(_mMarkdownBlock);
            });
        }

        /// <summary>
        /// Fired when we should destroy our content.
        /// </summary>
        public void OnDestroyContent()
        {
            // Clear the markdown
            if (_mMarkdownBlock != null)
            {
                _mMarkdownBlock.OnMarkdownReady -= MarkdownBox_OnMarkdownReady;
                _mMarkdownBlock.OnMarkdownLinkTapped -= MarkdownBlock_OnMarkdownLinkTapped;
            }
            _mMarkdownBlock = null;

            // Clear the UI
            ui_contentRoot.Children.Clear();
        }

        /// <summary>
        /// Fired when a new host has been added.
        /// </summary>
        public void OnHostAdded()
        {
            // Ignore for now.
        }

        /// <summary>
        /// Fired when this post becomes visible
        /// </summary>
        public void OnVisibilityChanged(bool isVisible)
        {
            // Ignore for now
        }

        #endregion

        #region Markdown Events

        /// <summary>
        /// Fired when a link is tapped in the markdown.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MarkdownBlock_OnMarkdownLinkTapped(object sender, MarkdownLinkTappedArgs e)
        {
            App.BaconMan.ShowGlobalContent(e.Link);
        }

        /// <summary>
        /// Hide the loading text when the markdown is done.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MarkdownBox_OnMarkdownReady(object sender, MarkdownReadyArgs e)
        {
            if (e.WasError)
            {
                _mBase.FireOnFallbackToBrowser();
                TelemetryManager.ReportUnexpectedEvent(this, "FailedToShowMarkdown", e.Exception);
            }
            else
            {
                // Hide loading
                _mBase.FireOnLoading(false);
            }
        }

        #endregion

        #region Pinch Scale Handling
        /// <summary>
        /// enables listening for "pinch" scaling events
        /// </summary>
        private void InitializePinchHandling()
        {
            ManipulationMode |= ManipulationModes.Scale;
            ManipulationStarted += PinchManipulationStarted;
            ManipulationDelta += PinchManipulationDelta;
        }

        /// <summary>
        /// font size at pinch start
        /// </summary>
        private int _mInitialFontSize;

        /// <summary>
        /// save font size at pinch start
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PinchManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            _mInitialFontSize = App.BaconMan.UiSettingsMan.PostViewMarkdownFontSize;
        }

        /// <summary>
        /// set new font size while pinching (in steps of 1)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PinchManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            double newSize = e.Cumulative.Scale * _mInitialFontSize;

            if (Math.Abs(newSize - App.BaconMan.UiSettingsMan.PostViewMarkdownFontSize) >= 1) {
                App.BaconMan.UiSettingsMan.PostViewMarkdownFontSize = (int)newSize;
            }
        }
        #endregion
    }
}
