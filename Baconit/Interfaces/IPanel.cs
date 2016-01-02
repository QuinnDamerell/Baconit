using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace Baconit.Interfaces
{
    public interface IPanel
    {
        /// <summary>
        /// Fired when the panel is being created.
        /// </summary>
        /// <param name="host">A reference to the host.</param>
        /// <param name="arguments">Arguments for the panel</param>
        void PanelSetup(IPanelHost host, Dictionary<string, object> arguments);

        /// <summary>
        /// Fired when the panel is already in the stack, but a new navigate has been made to it.
        /// Instead of creating a new panel, this same panel is used and given the navigation arguments.
        /// </summary>
        /// <param name="arguments">The argumetns passed when navigate was called</param>
        void OnPanelPulledToTop(Dictionary<string, object> arguments);

        /// <summary>
        /// Fired before the panel is shown but when it is just about to be shown.
        /// </summary>
        void OnNavigatingTo();

        /// <summary>
        /// Fired just before the panel is going to be hidden.
        /// </summary>
        void OnNavigatingFrom();

        /// <summary>
        /// Fired when the panel is being removed from the navigation stack and will
        /// never be shown again.
        /// </summary>
        void OnCleanupPanel();
    }
}
