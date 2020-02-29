using BaconBackend.Helpers;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.HelperControls
{
    public sealed partial class TipPopUp : UserControl
    {

        /// <summary>
        /// Fired when the tip is hidden.
        /// </summary>
        public event EventHandler<EventArgs> OnTipHideComplete
        {
            add => _mTipHideComplete.Add(value);
            remove => _mTipHideComplete.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<EventArgs>> _mTipHideComplete = new SmartWeakEvent<EventHandler<EventArgs>>();


        public TipPopUp()
        {
            InitializeComponent();
            VisualStateManager.GoToState(this, "HideTipBox", false);
        }

        /// <summary>
        /// Shows the tip box
        /// </summary>
        public void ShowTip()
        {
            VisualStateManager.GoToState(this, "ShowTipBox", true);
        }

        /// <summary>
        /// Hides the tip box
        /// </summary>
        public void HideTip()
        {
            VisualStateManager.GoToState(this, "HideTipBox", true);
        }

        /// <summary>
        /// Fired when hide is complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HideStoryboard_Completed(object sender, object e)
        {
            _mTipHideComplete.Raise(this, new EventArgs());
        }
    }
}
