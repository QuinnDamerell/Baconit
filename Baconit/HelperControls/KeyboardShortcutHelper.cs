using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Baconit.HelperControls
{
    class KeyboardShortcutHelper
    {
        /// <summary>
        /// Fired when a quick search key combo is detected
        /// </summary>
        public event EventHandler<EventArgs> OnQuickSearchActivation
        {
            add { m_onQuickSearchActivation.Add(value); }
            remove { m_onQuickSearchActivation.Remove(value); }
        }
        SmartWeakEvent<EventHandler<EventArgs>> m_onQuickSearchActivation = new SmartWeakEvent<EventHandler<EventArgs>>();

        // Private Vars
        bool m_isControlKeyDown = false;

        public KeyboardShortcutHelper()
        {
            // Register for handlers
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Window.Current.CoreWindow.KeyUp += CoreWindow_KeyUp;
        }

        /// <summary>
        /// Fired when a key is pressed down on the main window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CoreWindow_KeyDown(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.KeyEventArgs e)
        {
            if (e.VirtualKey == Windows.System.VirtualKey.Control)
            {
                m_isControlKeyDown = true;
            }
            else if (m_isControlKeyDown)
            {
                // I had this on the key down event but it didn't seem to fire 100%
                // reliably. So this place seems to work better.
                if (e.VirtualKey == Windows.System.VirtualKey.S)
                {
                    // Fire the event
                    m_onQuickSearchActivation.Raise(this, new EventArgs());
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Fired when a key is up on the main window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CoreWindow_KeyUp(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.KeyEventArgs e)
        {
            if (e.VirtualKey == Windows.System.VirtualKey.Control)
            {
                m_isControlKeyDown = false;
            }
        }
    }
}
