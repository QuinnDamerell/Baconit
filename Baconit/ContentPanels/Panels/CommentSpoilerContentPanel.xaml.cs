using Baconit.Interfaces;

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class CommentSpoilerContentPanel : IContentPanel
    {
        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        private readonly IContentPanelBaseInternal _baseContentPanel;

        public CommentSpoilerContentPanel(IContentPanelBaseInternal panelBaseContentPanel)
        {
            InitializeComponent();
            _baseContentPanel = panelBaseContentPanel;
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a post.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static bool CanHandlePost(ContentPanelSource source)
        {
            // Check if we have the spoiler tag.
            if (!string.IsNullOrWhiteSpace(source.Url) && source.Url.TrimStart().ToLower().StartsWith("/s"))
            {
                return true;
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
        public void OnPrepareContent()
        {
            var spoilerText = _baseContentPanel.Source.SelfText;

            // Parse out the spoiler
            var firstQuote = spoilerText.IndexOf('"');
            var lastQuote = spoilerText.LastIndexOf('"');
            if (firstQuote != -1 && lastQuote != -1 && firstQuote != lastQuote)
            {
                firstQuote++;
                spoilerText = spoilerText.Substring(firstQuote, lastQuote - firstQuote);
            }

            // Set the text
            ui_textBlock.Text = spoilerText;

            _baseContentPanel.FireOnLoading(false);
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
