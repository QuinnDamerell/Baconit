using BaconBackend.Helpers;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class RedditContentPanel : UserControl, IContentPanel
    {
        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        IContentPanelBaseInternal m_base;

        /// <summary>
        /// The current reddit content we have.
        /// </summary>
        RedditContentContainer m_content;


        public RedditContentPanel(IContentPanelBaseInternal panelBase)
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
            // If this is a self post with no text we will handle this.
            if (source.IsSelf && String.IsNullOrWhiteSpace(source.SelfText))
            {
                return true;
            }

            // Check if this is a reddit content post, if so this is us
            if (!String.IsNullOrWhiteSpace(source.Url))
            {
                // Check the content
                RedditContentContainer container = MiscellaneousHelper.TryToFindRedditContentInLink(source.Url);

                // If we got a container and it isn't a web site return it.
                if (container != null && container.Type != RedditContentType.Website)
                {
                    return true;
                }
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
        public async void OnPrepareContent()
        {
            // Defer so we give the UI time to work.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                string headerText = "";
                string minorText = "";

                if (m_base.Source.IsSelf)
                {
                    headerText = "There's no content here!";
                    minorText = "Scroll down to view the discussion.";
                }
                else
                {
                    m_content = MiscellaneousHelper.TryToFindRedditContentInLink(m_base.Source.Url);

                    switch (m_content.Type)
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
                        case RedditContentType.User:
                            headerText = "This post links to a reddit user page";
                            minorText = $"Tap anywhere to view {m_content.User}";
                            break;
                        case RedditContentType.Website:
                            // This shouldn't happen
                            App.BaconMan.MessageMan.DebugDia("Got website back when prepare on reddit content control");
                            break;
                    }
                }

                ui_headerText.Text = headerText;
                ui_minorText.Text = minorText;

                // Hide loading
                m_base.FireOnLoading(false);
            });
        }

        /// <summary>
        /// Fired when we should destroy our content.
        /// </summary>
        public void OnDestroyContent()
        {
            m_content = null;
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

        /// <summary>
        /// Call the global content viewer to show the content.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentRoot_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (m_content != null)
            {
                // If we have content pass it!
                App.BaconMan.ShowGlobalContent(m_content);
            }
        }
    }
}
