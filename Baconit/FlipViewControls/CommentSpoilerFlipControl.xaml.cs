using BaconBackend.DataObjects;
using BaconBackend.Helpers;
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

namespace Baconit.FlipViewControls
{
    public sealed partial class CommnetSpoilerFlipControl : UserControl, IFlipViewContentControl
    {
        IFlipViewContentHost m_host;

        public CommnetSpoilerFlipControl(IFlipViewContentHost host)
        {
            m_host = host;
            this.InitializeComponent();
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a post.
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        static public bool CanHandlePost(Post post)
        {
            // Check if we have the spoiler tag.
            if (!String.IsNullOrWhiteSpace(post.Url) && post.Url.TrimStart().ToLower().StartsWith("/s"))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Called when we should show the content
        /// </summary>
        /// <param name="post"></param>
        public void OnPrepareContent(Post post)
        {
            string spoilerText = post.Url;

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
        }
    }
}
