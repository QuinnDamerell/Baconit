using Baconit.ContentPanels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baconit.Interfaces
{
    public interface IContentPanelBaseInternal
    {
        /// <summary>
        /// Indicates if the control is loading.
        /// </summary>
        bool IsLoading { get; }

        /// <summary>
        /// Indicates if the control has an error.
        /// </summary>
        bool HasError { get; }

        /// <summary>
        /// Indicates if the panel is destroyed.
        /// </summary>
        bool IsDestoryed { get; }

        /// <summary>
        /// Indicates if we are full screen.
        /// </summary>
        bool IsFullscreen { get; }

        /// <summary>
        /// Indicates if we can go full screen.
        /// </summary>
        bool CanGoFullscreen { get; }

        /// <summary>
        /// The source for the panel.
        /// </summary>
        ContentPanelSource Source { get; }

        /// <summary>
        /// Called by the panel when we should show or hide loading.
        /// </summary>
        /// <param name="isShowing"></param>
        void FireOnLoading(bool isShowing);

        /// <summary>
        /// Called by the panel when we should show or hide error.
        /// </summary>
        /// <param name="isShowing"></param>
        void FireOnError(bool isShowing, string errorText = null);

        /// <summary>
        /// Fired by the panel when we should attempt to go full screen.
        /// </summary>
        /// <param name="goFullscreen"></param>
        /// <returns></returns>
        bool FireOnFullscreenChanged(bool goFullscreen);

        /// <summary>
        /// Tells the content manager to show this as a web page instead of
        /// the current control.
        /// </summary>
        void FireOnFallbackToBrowser();
    }
}
