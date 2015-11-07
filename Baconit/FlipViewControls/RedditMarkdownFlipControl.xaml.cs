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

namespace Baconit.FlipViewControls
{
    public sealed partial class RedditMarkdownFlipControl : UserControl, IFlipViewContentControl
    {
        IFlipViewContentHost m_host;
        MarkdownTextBox m_markdownBox;

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
        public void OnPrepareContent(Post post)
        {
            m_markdownBox = new MarkdownTextBox();
            m_markdownBox.OnMarkdownReady += MarkdownBox_OnMarkdownReady;
            m_markdownBox.Markdown = post.Selftext;
            ui_contentRoot.Children.Add(m_markdownBox);            
        }

        /// <summary>
        /// Hide the loading text when the markdown is done.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MarkdownBox_OnMarkdownReady(object sender, EventArgs e)
        {
            // Hide loading
            m_host.HideLoading();
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
            if(m_markdownBox != null)
            {
                m_markdownBox.OnMarkdownReady -= MarkdownBox_OnMarkdownReady;
            }
            m_markdownBox = null;

            // Clear the UI
            ui_contentRoot.Children.Clear();
        }
    }
}
