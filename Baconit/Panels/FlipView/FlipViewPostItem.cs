using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baconit.Panels.FlipView
{
    /// <summary>
    /// This class is what is bound to the flip view for each item.
    /// It holds the properties that represent a post.
    /// </summary>
    class FlipViewPostItem : BindableBase
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
            get
            {
                return m_context;
            }
            set
            {
                SetProperty(ref m_context, value);
            }
        }
        FlipViewPostContext m_context;

        /// <summary>
        /// If the post is visible.
        /// </summary>
        public bool IsVisible
        {
            get
            {
                return m_isVisible;
            }
            set
            {
                SetProperty(ref m_isVisible, value);
            }
        }
        bool m_isVisible = false;

        /// <summary>
        /// If the post should load comments
        /// </summary>
        public bool LoadComments
        {
            get
            {
                return m_loadComments;
            }
            set
            {
                SetProperty(ref m_loadComments, value);
            }
        }
        bool m_loadComments = false;
    }
}
