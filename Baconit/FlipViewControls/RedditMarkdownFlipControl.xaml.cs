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
using BaconBackend.DataObjects;
using Baconit.HelperControls;
using BaconBackend.Helpers;
using UniversalMarkdown;
using Windows.UI.Core;
using System.Threading.Tasks;

namespace Baconit.FlipViewControls
{
    public sealed partial class RedditMarkdownFlipControl : UserControl, IFlipViewContentControl
    {
        IFlipViewContentHost m_host;
        MarkdownTextBlock m_markdownBlock;

        public RedditMarkdownFlipControl(IFlipViewContentHost host)
        {
            m_host = host;
            this.InitializeComponent();
            m_host.ShowLoading();
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a post.
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        static public bool CanHandlePost(Post post)
        {
            // If it is a self post and it has text it is for us.
            return post.IsSelf && !String.IsNullOrWhiteSpace(post.Selftext);
        }

        /// <summary>
        /// Called when we should show the content
        /// </summary>
        /// <param name="post"></param>
        public async void OnPrepareContent(Post post)
        {
            // Since some of this can be costly, delay the work load until we aren't animating.
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                m_markdownBlock = new MarkdownTextBlock();
                m_markdownBlock.OnMarkdownLinkTapped += MarkdownBlock_OnMarkdownLinkTapped;
                m_markdownBlock.OnMarkdownReady += MarkdownBox_OnMarkdownReady;
                m_markdownBlock.Markdown = post.Selftext;                
                ui_contentRoot.Children.Add(m_markdownBlock);
            });     
        }

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
            if(e.WasError)
            {
                m_host.ShowError();
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToShowMarkdown", e.Exception);
            }
            else
            {
                // Hide loading
                m_host.HideLoading();
            }
        }

        /// <summary>
        /// Called when the  post actually becomes visible
        /// </summary>
        public void OnVisible()
        {
            // Ignore for now
        }

        /// <summary>
        /// Called when we should destroy the content
        /// </summary>
        public void OnDestroyContent()
        {
            // Clear the markdown
            if(m_markdownBlock != null)
            {
                m_markdownBlock.OnMarkdownReady -= MarkdownBox_OnMarkdownReady;
                m_markdownBlock.OnMarkdownLinkTapped -= MarkdownBlock_OnMarkdownLinkTapped;
            }
            m_markdownBlock = null;

            // Clear the UI
            ui_contentRoot.Children.Clear();
        }
    }
}
