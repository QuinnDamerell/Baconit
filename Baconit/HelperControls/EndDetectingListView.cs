using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Baconit.HelperControls
{
    /// <summary>
    /// Indicates which way the list is scrolling.
    /// </summary>
    public enum ScrollDirection
    {
        Null, // Indicates we haven't moved enough to have a direction.
        Up,
        Down
    }

    /// <summary>
    /// The args class for the OnListEndDetected event.
    /// </summary>
    public class ListEndDetected : EventArgs
    {
        public double ListScrollPercent;
        public double ListScrollTotalDistance;
        public ScrollDirection ScrollDirection;
    }

    public class EndDetectingListView : ListView
    {
        private ScrollBar _mListeningScrollBar;
        private double _mLastValue;
        private double _mLastDirectionChangeValue;

        public EndDetectingListView()
        {
            Loaded += EndDetectingListView_Loaded;
        }

        /// <summary>
        /// Fired when the end of the list is detected.
        /// </summary>
        public event EventHandler<ListEndDetected> OnListEndDetectedEvent
        {
            add => _mOnListEndDetectedEvent.Add(value);
            remove => _mOnListEndDetectedEvent.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<ListEndDetected>> _mOnListEndDetectedEvent = new SmartWeakEvent<EventHandler<ListEndDetected>>();

        /// <summary>
        /// Indicates at which threshold the event will be fired
        /// </summary>
        public double EndOfListDetectionThrehold { get; set; } = 0.95;

        /// <summary>
        /// Suppresses the event from being fired
        /// </summary>
        public bool SuppressEndOfListEvent { get; set; } = false;

        /// <summary>
        /// Fired the the object is ready, setup the scroll detection.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EndDetectingListView_Loaded(object sender, RoutedEventArgs e)
        {
            // Stop listening to the event. We only want to do this once.
            Loaded -= EndDetectingListView_Loaded;

            // Get the scroll bars
            var scrollBars = new List<DependencyObject>();
            UiControlHelpers<ScrollBar>.RecursivelyFindElement(this, ref scrollBars);

            // Find the scrollbar we want. Fun fact. Since in the scollviewer (which is in the list) the scrollContentPresenter is before the
            // main scrollbars we will find scrollbars of the header before ours. Ours should always be the last scrollbars in the list.
            // SO KEEP GOING DON'T BREAK until we find the last vertical scrollbar.
            foreach(var dObject in scrollBars)
            {
                if(((ScrollBar)dObject).Orientation == Orientation.Vertical)
                {
                    _mListeningScrollBar = (ScrollBar)dObject;
                }
            }

            // Make sure we found it
            if (_mListeningScrollBar == null)
            {
                throw new Exception("Failed to find the scroll bar!");
            }

            // Add the listener
            _mListeningScrollBar.ValueChanged += ScrollBar_ValueChanged;
        }

        /// <summary>
        /// Fired when the scroll bar is scrolled
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScrollBar_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            // Calculate the percent and fire when it meets the threshold
            var scrollPercentage = _mListeningScrollBar.Value / _mListeningScrollBar.Maximum;
            var direction = _mLastValue > _mListeningScrollBar.Value ? ScrollDirection.Up : ScrollDirection.Down;

            // Detect a small changes and make them null
            if(Math.Abs(_mLastValue - _mListeningScrollBar.Value) < 1)
            {
                direction = ScrollDirection.Null;
            }
            _mLastValue = _mListeningScrollBar.Value;

            // We need to account for some play in the numbers when it comes to this, don't report a
            // direction of up until we move up 50px from the last down position. This prevents us from
            // jump back and forth when on a small point.
            if (direction == ScrollDirection.Up && (_mLastDirectionChangeValue - _mLastValue) < 50)
            {
                direction = ScrollDirection.Null;
            }
            else if(direction == ScrollDirection.Down)
            {
                _mLastDirectionChangeValue = _mListeningScrollBar.Value;
            }

            if ((!SuppressEndOfListEvent  && scrollPercentage > EndOfListDetectionThrehold) || _mListeningScrollBar.Value == 0)
            {
                _mOnListEndDetectedEvent.Raise(this, new ListEndDetected { ListScrollPercent = scrollPercentage, ListScrollTotalDistance = _mListeningScrollBar.Value, ScrollDirection = direction });
            }
        }
    }
}
