using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
    public sealed partial class TipPopUp : UserControl
    {

        /// <summary>
        /// Fired when the tip is hidden.
        /// </summary>
        public event EventHandler<EventArgs> TipHideComplete
        {
            add { m_tipHideComplete.Add(value); }
            remove { m_tipHideComplete.Remove(value); }
        }
        SmartWeakEvent<EventHandler<EventArgs>> m_tipHideComplete = new SmartWeakEvent<EventHandler<EventArgs>>();


        public TipPopUp()
        {
            this.InitializeComponent();
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
            m_tipHideComplete.Raise(this, new EventArgs());
        }
    }
}
