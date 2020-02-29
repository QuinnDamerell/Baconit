using BaconBackend.Helpers;
using Baconit.Interfaces;
using System;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Input;
using BaconBackend.Managers;

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class RedditContentPanel : IContentPanel
    {
        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        private readonly IContentPanelBaseInternal _contentPanelBase;

        /// <summary>
        /// The current reddit content we have.
        /// </summary>
        private RedditContentContainer _content;


        public RedditContentPanel(IContentPanelBaseInternal panelBase)
        {
            InitializeComponent();
            _contentPanelBase = panelBase;
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a post.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static bool CanHandlePost(ContentPanelSource source)
        {
            // If this is a self post with no text we will handle this.
            if (source.IsSelf && string.IsNullOrWhiteSpace(source.SelfText))
            {
                return true;
            }

            // Check if this is a reddit content post, if so this is us
            if (string.IsNullOrWhiteSpace(source.Url)) return false;

            // Check the content
            var container = MiscellaneousHelper.TryToFindRedditContentInLink(source.Url);

            // If we got a container and it isn't a web site return it.
            return container != null && container.Type != RedditContentType.Website;
        }

        #region IContentPanel

        /// <summary>
        /// Indicates how large the panel is in memory.
        /// </summary>
        public PanelMemorySizes PanelMemorySize => PanelMemorySizes.Small;

        /// <summary>
        /// Fired when we should load the content.
        /// </summary>
        public async void OnPrepareContent()
        {
            // Defer so we give the UI time to work.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                var headerText = "";
                var minorText = "";

                if (_contentPanelBase.Source.IsSelf)
                {
                    headerText = "There's no content here!";
                    minorText = "Scroll down to view the discussion.";
                }
                else
                {
                    _content = MiscellaneousHelper.TryToFindRedditContentInLink(_contentPanelBase.Source.Url);

                    switch (_content.Type)
                    {
                        case RedditContentType.Subreddit:
                            headerText = "This post links to a subreddit";
                            minorText = $"Tap anywhere to view /r/{_content.Subreddit}";
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
                            minorText = $"Tap anywhere to view {_content.User}";
                            break;
                        case RedditContentType.Website:
                            // This shouldn't happen
                            App.BaconMan.MessageMan.DebugDia("Got website back when prepare on reddit content control");
                            TelemetryManager.ReportUnexpectedEvent(this, "GotWebsiteOnPrepareRedditContent");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                ui_headerText.Text = headerText;
                ui_minorText.Text = minorText;

                // Hide loading
                _contentPanelBase.FireOnLoading(false);
            });
        }

        /// <summary>
        /// Fired when we should destroy our content.
        /// </summary>
        public void OnDestroyContent()
        {
            _content = null;
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
            if (_content != null)
            {
                // If we have content pass it!
                App.BaconMan.ShowGlobalContent(_content);
            }
        }
    }
}
