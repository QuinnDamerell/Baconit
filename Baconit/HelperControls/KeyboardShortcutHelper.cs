using BaconBackend.Helpers;
using System;
using Windows.UI.Xaml;

namespace Baconit.HelperControls
{
    internal class KeyboardShortcutHelper
    {
        /// <summary>
        /// Fired when a quick search key combo is detected
        /// </summary>
        public event EventHandler<EventArgs> OnQuickSearchActivation
        {
            add => _mOnQuickSearchActivation.Add(value);
            remove => _mOnQuickSearchActivation.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<EventArgs>> _mOnQuickSearchActivation = new SmartWeakEvent<EventHandler<EventArgs>>();

        /// <summary>
        /// Fired when a escape key press is detected
        /// </summary>
        public event EventHandler<EventArgs> OnGoBackActivation
        {
            add => _mOnGoBackActivation.Add(value);
            remove => _mOnGoBackActivation.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<EventArgs>> _mOnGoBackActivation = new SmartWeakEvent<EventHandler<EventArgs>>();

        // Private Vars
        private bool _mIsControlKeyDown;

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
                _mIsControlKeyDown = true;
            }
            else if (_mIsControlKeyDown)
            {
                // I had this on the key down event but it didn't seem to fire 100%
                // reliably. So this place seems to work better.
                if (e.VirtualKey == Windows.System.VirtualKey.S)
                {
                    // Disable for mobile, for some reason this can trip with the mobile keyboard.
                    if (DeviceHelper.CurrentDevice() != DeviceTypes.Mobile)
                    {
                        // Fire the event
                        _mOnQuickSearchActivation.Raise(this, new EventArgs());
                        e.Handled = true;
                    }
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
                _mIsControlKeyDown = false;
            }
            else if(e.VirtualKey == Windows.System.VirtualKey.Escape)
            {
                // If we have an escape key hit fire go back.
                _mOnGoBackActivation.Raise(this, new EventArgs());
            }
        }
    }
}
