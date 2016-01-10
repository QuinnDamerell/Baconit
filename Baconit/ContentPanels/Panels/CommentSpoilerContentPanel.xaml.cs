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

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class CommentSpoilerContentPanel : UserControl, IContentPanel
    {
        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        IContentPanelBaseInternal m_base;

        public CommentSpoilerContentPanel(IContentPanelBaseInternal panelBase)
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
            // Check if we have the spoiler tag.
            if (!String.IsNullOrWhiteSpace(source.Url) && source.Url.TrimStart().ToLower().StartsWith("/s"))
            {
                return true;
            }
            return false;
        }

        #region IContentPanel

        /// <summary>
        /// Indicates how large the panel is in memory.
        /// </summary>
        public PanelMemorySizes PanelMemorySize
        {
            get
            {
                return PanelMemorySizes.Small;
            }
        }

        /// <summary>
        /// Fired when we should load the content.
        /// </summary>
        /// <param name="source"></param>
        public void OnPrepareContent()
        {
            string spoilerText = m_base.Source.SelfText;

            // Parse out the spoiler
            int firstQuote = spoilerText.IndexOf('"');
            int lastQuote = spoilerText.LastIndexOf('"');
            if (firstQuote != -1 && lastQuote != -1 && firstQuote != lastQuote)
            {
                firstQuote++;
                spoilerText = spoilerText.Substring(firstQuote, lastQuote - firstQuote);
            }

            // Set the text
            ui_textBlock.Text = spoilerText;

            m_base.FireOnLoading(false);
        }

        /// <summary>
        /// Fired when we should destroy our content.
        /// </summary>
        public void OnDestroyContent()
        {
            // Ignore for now.
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
    }
}
