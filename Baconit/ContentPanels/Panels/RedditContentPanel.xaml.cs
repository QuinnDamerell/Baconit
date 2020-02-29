using BaconBackend.Helpers;
using Baconit.Interfaces;
using System;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using BaconBackend.Managers;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class RedditContentPanel : UserControl, IContentPanel
    {
        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        private readonly IContentPanelBaseInternal _mBase;

        /// <summary>
        /// The current reddit content we have.
        /// </summary>
        private RedditContentContainer _mContent;


        public RedditContentPanel(IContentPanelBaseInternal panelBase)
        {
            InitializeComponent();
            _mBase = panelBase;
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a post.
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        static public bool CanHandlePost(ContentPanelSource source)
        {
            // If this is a self post with no text we will handle this.
            if (source.IsSelf && string.IsNullOrWhiteSpace(source.SelfText))
            {
                return true;
            }

            // Check if this is a reddit content post, if so this is us
            if (!string.IsNullOrWhiteSpace(source.Url))
            {
                // Check the content
                var container = MiscellaneousHelper.TryToFindRedditContentInLink(source.Url);

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
        public PanelMemorySizes PanelMemorySize => PanelMemorySizes.Small;

        /// <summary>
        /// Fired when we should load the content.
        /// </summary>
        /// <param name="source"></param>
        public async void OnPrepareContent()
        {
            // Defer so we give the UI time to work.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                var headerText = "";
                var minorText = "";

                if (_mBase.Source.IsSelf)
                {
                    headerText = "There's no content here!";
                    minorText = "Scroll down to view the discussion.";
                }
                else
                {
                    _mContent = MiscellaneousHelper.TryToFindRedditContentInLink(_mBase.Source.Url);

                    switch (_mContent.Type)
                    {
                        case RedditContentType.Subreddit:
                            headerText = "This post links to a subreddit";
                            minorText = $"Tap anywhere to view /r/{_mContent.Subreddit}";
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
                            minorText = $"Tap anywhere to view {_mContent.User}";
                            break;
                        case RedditContentType.Website:
                            // This shouldn't happen
                            App.BaconMan.MessageMan.DebugDia("Got website back when prepare on reddit content control");
                            TelemetryManager.ReportUnexpectedEvent(this, "GotWebsiteOnPrepareRedditContent");
                            break;
                    }
                }

                ui_headerText.Text = headerText;
                ui_minorText.Text = minorText;

                // Hide loading
                _mBase.FireOnLoading(false);
            });
        }

        /// <summary>
        /// Fired when we should destroy our content.
        /// </summary>
        public void OnDestroyContent()
        {
            _mContent = null;
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
            if (_mContent != null)
            {
                // If we have content pass it!
                App.BaconMan.ShowGlobalContent(_mContent);
            }
        }
    }
}
