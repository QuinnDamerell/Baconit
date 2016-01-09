using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baconit.Interfaces
{
    public interface IContentPanelHost
    {
        /// <summary>
        /// Indicates if this control can go full screen.
        /// </summary>
        bool CanGoFullscreen { get; }

        /// <summary>
        /// Indicates if the control is currently full screen.
        /// </summary>
        bool IsFullscreen { get; }

        /// <summary>
        /// Indicates if the control is currently visible
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// A unique id created per host.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Fired when the requested panel becomes available for the host.
        /// </summary>
        /// <param name="panel">The panel</param>
        void OnPanelAvailable(IContentPanelBase panelBase);

        /// <summary>
        /// Fired when the host should release the panel because it is being taken away.
        /// </summary>
        /// <param name="panel"></param>
        void OnRemovePanel(IContentPanelBase panelBase);

        /// <summary>
        /// Called when the content requested by this panel has begun loading.
        /// </summary>
        void OnContentPreloading();

        /// <summary>
        /// Toggles the loading UI over the control.
        /// </summary>
        /// <param name="show"></param>
        void OnLoadingChanged();

        /// <summary>
        /// Toggles if the error UI is shown
        /// </summary>
        void OnErrorChanged();

        /// <summary>
        /// Attempts to toggle full screen on the host.
        /// </summary>
        /// <param name="goFullscreen"></param>
        /// <returns>True if success, false if failed.</returns>
        bool OnFullscreenChanged(bool goFullscreen);
    }
}
