using BaconBackend.Helpers;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.HelperControls
{
    public sealed partial class LoadingOverlay : UserControl
    {
        /// <summary>
        /// Fired when the loading overlay is closed
        /// </summary>
        public event EventHandler<EventArgs> OnHideComplete
        {
            add => _mOnHideComplete.Add(value);
            remove => _mOnHideComplete.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<EventArgs>> _mOnHideComplete = new SmartWeakEvent<EventHandler<EventArgs>>();

        public LoadingOverlay()
        {
            InitializeComponent();

            // Hide it
            VisualStateManager.GoToState(this, "HideLoading", false);
        }

        public void Show(bool showLoading = true, string loadingText = null)
        {
            // Set the progress ring
            ui_progressRing.IsActive = showLoading;
            ui_progressRing.Visibility = showLoading ? Visibility.Visible : Visibility.Collapsed;

            // Show or hide the text
            ui_loadingText.Visibility = loadingText == null ? Visibility.Collapsed : Visibility.Visible;
            if (loadingText != null)
            {
                ui_loadingText.Text = loadingText;
            }

            // Show it
            VisualStateManager.GoToState(this, "ShowLoading", true);
        }

        public void Hide()
        {
            // Hide it
            VisualStateManager.GoToState(this, "HideLoading", true);
        }

        /// <summary>
        /// Fired when the hide animation is complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HideDialog_Completed(object sender, object e)
        {
            ui_progressRing.IsActive = false;
            _mOnHideComplete.Raise(this, new EventArgs());
        }
    }
}
