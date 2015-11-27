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
    public sealed partial class RedditContentFlipControl : UserControl, IFlipViewContentControl
    {
        IFlipViewContentHost m_host;
        RedditContentContainer m_content;

        public RedditContentFlipControl(IFlipViewContentHost host)
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
            // If this is a self post with no text we will handle this.
            if(post.IsSelf && String.IsNullOrWhiteSpace(post.Selftext))
            {
                return true;
            }
            
            // Check if this is a reddit content post, if so this is us
            if (!String.IsNullOrWhiteSpace(post.Url))
            {
                // Check the content
                RedditContentContainer container = MiscellaneousHelper.TryToFindRedditContentInLink(post.Url);

                // If we got a container and it isn't a web site return it.
                if(container != null && container.Type != RedditContentType.Website)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Called when we should show the content
        /// </summary>
        /// <param name="post"></param>
        public void OnPrepareContent(Post post)
        {
            string headerText = "";
            string minorText = "";

            if (post.IsSelf)
            {
                headerText = "There's no content here!";
                minorText = "Scroll down to view the discussion.";
            }
            else
            {
                m_content = MiscellaneousHelper.TryToFindRedditContentInLink(post.Url);

                switch(m_content.Type)
                {
                    case RedditContentType.Subreddit:
                        headerText = "This post links to a subreddit";
                        minorText = $"Tap anywhere to view /r/{m_content.Subreddit}";
                        break;
                    case RedditContentType.Comment:
                        headerText = "This post links to a comment thread";
                        minorText = "Tap anywhere to view it";
                        break;
                    case RedditContentType.Post:
                        headerText = "This post links to a reddit post";
                        minorText = $"Tap anywhere to view it";
                        break;
                    case RedditContentType.Website:
                        // This shouldn't happen
                        App.BaconMan.MessageMan.DebugDia("Got website back when prepare on reddit content control");
                        App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "GotWebsiteOnPrepareRedditContent");
                        break;
                }
            }

            ui_headerText.Text = headerText;
            ui_minorText.Text = minorText;
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
            m_content = null;
        }

        /// <summary>
        /// Call the global content viewer to show the content.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentRoot_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if(m_content != null)
            {
                // If we have content pass it!
                App.BaconMan.ShowGlobalContent(m_content);
            }
        }
    }
}
