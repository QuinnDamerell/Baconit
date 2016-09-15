using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using UniversalMarkdown;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class MarkdownContentPanel : UserControl, IContentPanel
    {
        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        IContentPanelBaseInternal m_base;

        /// <summary>
        /// The current markdown text box.
        /// </summary>
        MarkdownTextBlock m_markdownBlock;

        public MarkdownContentPanel(IContentPanelBaseInternal panelBase)
        {
            this.InitializeComponent();
            m_base = panelBase;
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a post.
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        static public bool CanHandlePost(ContentPanelSource source)
        {
            // If it is a self post and it has text it is for us.
            return source.IsSelf && !String.IsNullOrWhiteSpace(source.SelfText);
        }

        #region IContentPanel

        /// <summary>
        /// Indicates how large the panel is in memory.
        /// </summary>
        public PanelMemorySizes PanelMemorySize
        {
            get
            {
                // #todo can we figure this out?
                return PanelMemorySizes.Small;
            }
        }

        /// <summary>
        /// Fired when we should load the content.
        /// </summary>
        /// <param name="source"></param>
        public async void OnPrepareContent()
        {
            // Since some of this can be costly, delay the work load until we aren't animating.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                m_markdownBlock = new MarkdownTextBlock();
                m_markdownBlock.OnMarkdownLinkTapped += MarkdownBlock_OnMarkdownLinkTapped;
                m_markdownBlock.OnMarkdownReady += MarkdownBox_OnMarkdownReady;
                m_markdownBlock.Markdown = m_base.Source.SelfText;

                Binding fontSizeBinding = new Binding
                {
                    Source = App.BaconMan.UiSettingsMan,
                    Path = new PropertyPath("PostView_Markdown_FontSize")
                };
                m_markdownBlock.SetBinding(MarkdownTextBlock.FontSizeProperty, fontSizeBinding);

                ui_contentRoot.Children.Add(m_markdownBlock);
            });
        }

        /// <summary>
        /// Fired when we should destroy our content.
        /// </summary>
        public void OnDestroyContent()
        {
            // Clear the markdown
            if (m_markdownBlock != null)
            {
                m_markdownBlock.OnMarkdownReady -= MarkdownBox_OnMarkdownReady;
                m_markdownBlock.OnMarkdownLinkTapped -= MarkdownBlock_OnMarkdownLinkTapped;
            }
            m_markdownBlock = null;

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
        private void MarkdownBlock_OnMarkdownLinkTapped(object sender, OnMarkdownLinkTappedArgs e)
        {
            App.BaconMan.ShowGlobalContent(e.Link);
        }

        /// <summary>
        /// Hide the loading text when the markdown is done.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MarkdownBox_OnMarkdownReady(object sender, OnMarkdownReadyArgs e)
        {
            if (e.WasError)
            {
                m_base.FireOnFallbackToBrowser();
                App.BaconMan.TelemetryMan.ReportUnexpectedEvent(this, "FailedToShowMarkdown", e.Exception);
            }
            else
            {
                // Hide loading
                m_base.FireOnLoading(false);
            }
        }

        #endregion
    }
}
