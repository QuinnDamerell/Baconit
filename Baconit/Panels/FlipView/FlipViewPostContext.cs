using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Baconit.Panels.FlipView
{
    /// <summary>
    /// Used by flip view to pass context to post items.
    /// </summary>
    public class FlipViewPostContext : BindableBase
    {
        public FlipViewPostContext(IPanelHost host, PostCollector collector, Post post)
        {
            Post = post;
            Collector = collector;
            Host = host;
        }

        public Post Post { get; set; }

        public PostCollector Collector { get; set; }

        public IPanelHost Host { get; set; }


        #region UI Vars

        /// <summary>
        /// The number of pixels the UI needs to display the post's header.
        /// </summary>
        public int HeaderSize
        {
            get
            {
                return m_headerSize;
            }
            set
            {
                SetProperty(ref m_headerSize, value);
            }
        }
        int m_headerSize = 500;

        /// <summary>
        /// Controls if the post menu icon is visible.
        /// </summary>
        public Visibility PostMenuIconVisibility
        {
            get
            {
                return m_postMenuIcon;
            }
            set
            {
                SetProperty(ref m_postMenuIcon, value);
            }
        }
        Visibility m_postMenuIcon = Visibility.Collapsed;        

        #endregion
    }
}
