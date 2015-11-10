using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Store;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.HelperControls
{
    public class RateAndFeedbackClosed : EventArgs
    {
        public bool WasFeedbackGiven = false;
        public bool WasReviewGiven = false;
    }

    public sealed partial class RateAndFeedbackPopUp : UserControl
    {
        /// <summary>
        /// Fired when the review box is closed
        /// </summary>
        public event EventHandler<RateAndFeedbackClosed> OnHideComplete
        {
            add { m_onHideComplete.Add(value); }
            remove { m_onHideComplete.Remove(value); }
        }
        SmartWeakEvent<EventHandler<RateAndFeedbackClosed>> m_onHideComplete = new SmartWeakEvent<EventHandler<RateAndFeedbackClosed>>();

        // Used to indicate what happened
        bool m_wasReviewGiven = false;
        bool m_wasFeedbackGiven = false;

        public RateAndFeedbackPopUp()
        {
            this.InitializeComponent();

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
            App.BaconMan.TelemetryMan.ReportEvent(this, "ReviewAndFeedbackShown");
        }

        private void Close_OnIconTapped(object sender, EventArgs e)
        {
            // Hide it
            VisualStateManager.GoToState(this, "HideDialog", true);
        }

        /// <summary>
        /// Fired when the hide animation is complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HideDialog_Completed(object sender, object e)
        {
            RateAndFeedbackClosed args = new RateAndFeedbackClosed()
            {
                WasFeedbackGiven = m_wasFeedbackGiven,
                WasReviewGiven = m_wasReviewGiven
            };
            m_onHideComplete.Raise(this, args);
        }

        /// <summary>
        /// Open the store for a review.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Review_Clicked(object sender, RoutedEventArgs e)
        {
            // Open the store
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store:reviewapp?appid=" + CurrentApp.AppId));

            // Send telemetry
            App.BaconMan.TelemetryMan.ReportEvent(this, "StoreReviewOpened");
            m_wasReviewGiven = true;

            // Close the box
            Close_OnIconTapped(null, null);
        }

        /// <summary>
        /// Open the user voice page for features
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Feedback_Clicked(object sender, RoutedEventArgs e)
        {
            // Go to the subreddit.
            App.BaconMan.ShowGlobalContent("/r/baconit");

            // Report
            App.BaconMan.TelemetryMan.ReportEvent(this, "FeedbackSubredditOpened");
            m_wasFeedbackGiven = true;

            // Close the box
            Close_OnIconTapped(null, null);
        }
    }
}
