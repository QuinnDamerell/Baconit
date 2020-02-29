using BaconBackend.Helpers;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using BaconBackend.Managers;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.HelperControls
{
    public class RateAndFeedbackClosed : EventArgs
    {
        public bool WasFeedbackGiven;
        public bool WasReviewGiven;
    }

    public sealed partial class RateAndFeedbackPopUp : UserControl
    {
        /// <summary>
        /// Fired when the review box is closed
        /// </summary>
        public event EventHandler<RateAndFeedbackClosed> OnHideComplete
        {
            add => _mOnHideComplete.Add(value);
            remove => _mOnHideComplete.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<RateAndFeedbackClosed>> _mOnHideComplete = new SmartWeakEvent<EventHandler<RateAndFeedbackClosed>>();

        // Used to indicate what happened
        private bool _mWasReviewGiven;
        private readonly bool _mWasFeedbackGiven = false;

        public RateAndFeedbackPopUp()
        {
            InitializeComponent();

             // Hide the box
            VisualStateManager.GoToState(this, "HideDialog", false);
        }

        public void ShowPopUp()
        {
            // Show the box
            VisualStateManager.GoToState(this, "ShowDialog", true);

            // Show the close button
            VisualStateManager.GoToState(this, "ShowCloseButton", true);

            // Review and feedback shown
            TelemetryManager.ReportEvent(this, "ReviewAndFeedbackShown");
        }

        private void Close_OnIconTapped(object sender, EventArgs e)
        {
            // Hide it
            VisualStateManager.GoToState(this, "HideDialog", true);

            // Fire this if we have a sender.
            if(sender != null)
            {
                TelemetryManager.ReportEvent(this, "ClosedWithCloseButton");
            }
        }

        /// <summary>
        /// Fired when the hide animation is complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HideDialog_Completed(object sender, object e)
        {
            var args = new RateAndFeedbackClosed
            {
                WasFeedbackGiven = _mWasFeedbackGiven,
                WasReviewGiven = _mWasReviewGiven
            };
            _mOnHideComplete.Raise(this, args);
        }

        /// <summary>
        /// Open the store for a review.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Review_Clicked(object sender, RoutedEventArgs e)
        {
            // Open the store
            // #todo if our app package ever changes change this!!!
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?ProductId=9wzdncrfj0bc"));

            // Send telemetry
            TelemetryManager.ReportEvent(this, "StoreReviewOpened");
            _mWasReviewGiven = true;

            // Close the box
            Close_OnIconTapped(null, null);
        }
    }
}
