using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace Baconit.HelperControls
{
    /// <summary>
    /// The args class for the OnListEndDetected event.
    /// </summary>
    public class OnListEndDetected : EventArgs
    {
        public double ListScrollPercent;
        public double ListScrollTotalDistance;
    }

    public class EndDetectingListView : ListView
    {
        ScrollBar m_listeningScrollBar = null;

        public EndDetectingListView()
        {
            Loaded += EndDetectingListView_Loaded;
        }

        /// <summary>
        /// Fired when the end of the list is detected.
        /// </summary>
        public event EventHandler<OnListEndDetected> OnListEndDetectedEvent
        {
            add { m_onListEndDetectedEvent.Add(value); }
            remove { m_onListEndDetectedEvent.Remove(value); }
        }
        SmartWeakEvent<EventHandler<OnListEndDetected>> m_onListEndDetectedEvent = new SmartWeakEvent<EventHandler<OnListEndDetected>>();

        /// <summary>
        /// Indicates at which threshold the event will be fired
        /// </summary>
        public double EndOfListDetectionThrehold
        {
            get { return m_endOfListDetectionThreshold; }
            set { m_endOfListDetectionThreshold = value; }
        }
        double m_endOfListDetectionThreshold = 0.95;

        /// <summary>
        /// Suppresses the event from being fired
        /// </summary>
        public bool SuppressEndOfListEvent
        {
            get { return m_supressEndOfListEvent; }
            set { m_supressEndOfListEvent = value; }
        }
        bool m_supressEndOfListEvent = false;

        /// <summary>
        /// Fired the the object is ready, setup the scroll detection.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EndDetectingListView_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // Get the scroll bars
            List<DependencyObject> scrollBars = new List<DependencyObject>();
            UiControlHelpers<ScrollBar>.RecursivelyFindElement(this, ref scrollBars);

            // Find the scrollbar we want. Fun fact. Since in the scollviewer (which is in the list) the scrollContentPresenter is before the
            // main scrollbars we will find scrollbars of the header before ours. Ours should always be the last scrollbars in the list.
            // SO KEEP GOING DON'T BREAK until we find the last vertical scrollbar.
            foreach(DependencyObject dObject in scrollBars)
            {
                if(((ScrollBar)dObject).Orientation == Orientation.Vertical)
                {
                    m_listeningScrollBar = (ScrollBar)dObject;
                }
            }

            // Make sure we found it
            if (m_listeningScrollBar == null)
            {
                throw new Exception("Failed to find the scroll bar!");
            }

            // Add the listener
            m_listeningScrollBar.ValueChanged += ScrollBar_ValueChanged;
        }

        /// <summary>
        /// Fired when the scroll bar is scrolled
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScrollBar_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            // Calculate the percent and fire when it meets the threshold
            double scrollPercentage = m_listeningScrollBar.Value / m_listeningScrollBar.Maximum;
            if (!m_supressEndOfListEvent && scrollPercentage > m_endOfListDetectionThreshold)
            {
                m_onListEndDetectedEvent.Raise(this, new OnListEndDetected() { ListScrollPercent = scrollPercentage, ListScrollTotalDistance = m_listeningScrollBar.Value });
            }
        }
    }
}
