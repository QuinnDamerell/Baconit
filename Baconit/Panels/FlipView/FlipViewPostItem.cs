using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using Baconit.ContentPanels;
using Baconit.ContentPanels.Panels;
using Baconit.Interfaces;
using Windows.UI.Xaml;

namespace Baconit.Panels.FlipView
{
    /// <summary>
    /// This class is what is bound to the flip view for each item.
    /// It holds the properties that represent a post.
    /// </summary>
    internal class FlipViewPostItem : BindableBase
    {
        public FlipViewPostItem(IPanelHost host, PostCollector collector, Post post, string targetComment)
        {
            Context = new FlipViewPostContext(host, collector, post, targetComment);
            IsVisible = false;
        }

        /// <summary>
        /// The context for the post.
        /// </summary>
        public FlipViewPostContext Context
        {
            get => _context;
            set => SetProperty(ref _context, value);
        }

        private FlipViewPostContext _context;

        /// <summary>
        /// If the post is visible.
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        private bool _isVisible;

        /// <summary>
        /// If the post should load comments
        /// </summary>
        public bool LoadComments
        {
            get => _loadComments;
            set => SetProperty(ref _loadComments, value);
        }

        private bool _loadComments;

        /// <summary>
        /// Check if post would be rendered as Markdown
        /// </summary>
        public Visibility RenderedAsMarkdown
        {
            get
            {
                var source = ContentPanelSource.CreateFromPost(Context.Post);
                return ContentPanelBase.GetControlType(source, this) == typeof(MarkdownContentPanel) ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
