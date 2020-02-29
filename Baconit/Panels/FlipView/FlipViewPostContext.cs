using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using Baconit.Interfaces;
using Windows.UI.Xaml;

namespace Baconit.Panels.FlipView
{
    /// <summary>
    /// Used by flip view to pass context to post items.
    /// </summary>
    public class FlipViewPostContext : BindableBase
    {
        public FlipViewPostContext(IPanelHost host, PostCollector collector, Post post, string targetComment)
        {
            Post = post;
            Collector = collector;
            Host = host;
            TargetComment = targetComment;
        }

        public Post Post { get; set; }

        public PostCollector Collector { get; }

        public IPanelHost Host { get; }

        public string TargetComment { get; }


        #region UI Vars

        /// <summary>
        /// The number of pixels the UI needs to display the post's header.
        /// </summary>
        public int HeaderSize
        {
            get => _mHeaderSize;
            set => SetProperty(ref _mHeaderSize, value);
        }

        private int _mHeaderSize = 500;

        /// <summary>
        /// Controls if the post menu icon is visible.
        /// </summary>
        public Visibility PostMenuIconVisibility
        {
            get => _mPostMenuIcon;
            set => SetProperty(ref _mPostMenuIcon, value);
        }

        private Visibility _mPostMenuIcon = Visibility.Collapsed;

        #endregion
    }
}
