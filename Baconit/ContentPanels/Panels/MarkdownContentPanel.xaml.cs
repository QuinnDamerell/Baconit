using Baconit.Interfaces;
using System;
using UniversalMarkdown;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using BaconBackend.Managers;

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class MarkdownContentPanel : IContentPanel
    {
        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        private readonly IContentPanelBaseInternal _contentPanelBase;

        /// <summary>
        /// The current markdown text box.
        /// </summary>
        private MarkdownTextBlock _markdownBlock;

        public MarkdownContentPanel(IContentPanelBaseInternal panelBase)
        {
            InitializeComponent();
            _contentPanelBase = panelBase;
            InitializePinchHandling();
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a post.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static bool CanHandlePost(ContentPanelSource source)
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
        public async void OnPrepareContent()
        {
            // Since some of this can be costly, delay the work load until we aren't animating.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                _markdownBlock = new MarkdownTextBlock();
                _markdownBlock.OnMarkdownLinkTapped += MarkdownBlock_OnMarkdownLinkTapped;
                _markdownBlock.OnMarkdownReady += MarkdownBox_OnMarkdownReady;
                _markdownBlock.Markdown = _contentPanelBase.Source.SelfText;

                var fontSizeBinding = new Binding
                {
                    Source = App.BaconMan.UiSettingsMan,
                    Path = new PropertyPath("PostView_Markdown_FontSize")
                };
                _markdownBlock.SetBinding(FontSizeProperty, fontSizeBinding);

                ui_contentRoot.Children.Add(_markdownBlock);
            });
        }

        /// <summary>
        /// Fired when we should destroy our content.
        /// </summary>
        public void OnDestroyContent()
        {
            // Clear the markdown
            if (_markdownBlock != null)
            {
                _markdownBlock.OnMarkdownReady -= MarkdownBox_OnMarkdownReady;
                _markdownBlock.OnMarkdownLinkTapped -= MarkdownBlock_OnMarkdownLinkTapped;
            }
            _markdownBlock = null;

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
        private static void MarkdownBlock_OnMarkdownLinkTapped(object sender, MarkdownLinkTappedArgs e)
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
                _contentPanelBase.FireOnFallbackToBrowser();
                TelemetryManager.ReportUnexpectedEvent(this, "FailedToShowMarkdown", e.Exception);
            }
            else
            {
                // Hide loading
                _contentPanelBase.FireOnLoading(false);
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
        private int _initialFontSize;

        /// <summary>
        /// save font size at pinch start
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PinchManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            _initialFontSize = App.BaconMan.UiSettingsMan.PostViewMarkdownFontSize;
        }

        /// <summary>
        /// set new font size while pinching (in steps of 1)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PinchManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            double newSize = e.Cumulative.Scale * _initialFontSize;

            if (Math.Abs(newSize - App.BaconMan.UiSettingsMan.PostViewMarkdownFontSize) >= 1) {
                App.BaconMan.UiSettingsMan.PostViewMarkdownFontSize = (int)newSize;
            }
        }
        #endregion
    }
}
