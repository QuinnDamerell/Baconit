using BaconBackend.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baconit.Interfaces
{
    public interface IFlipViewContentHost
    {
        /// <summary>
        /// Shows the loading UI on over content
        /// </summary>
        void ShowLoading();

        /// <summary>
        /// Hides the loading UI over the content
        /// </summary>
        void HideLoading();

        /// <summary>
        /// Shows an error
        /// </summary>
        void ShowError();

        /// <summary>
        /// If the control fails to load fallback to the browser.
        /// </summary>
        /// <param name="post"></param>
        void FallbackToWebBrowser(Post post);

        /// <summary>
        /// Tries to enter or exit full screen. Returns if successful.
        /// </summary>
        /// <param name="goFullScreen"></param>
        /// <returns></returns>
        bool ToggleFullScreen(bool goFullScreen);

        /// <summary>
        /// Indicates if we are currently full screen or not.
        /// </summary>
        /// <returns></returns>
        bool IsFullScreen();

        /// <summary>
        /// Indicates if we can go full screen or not.
        /// </summary>
        /// <returns></returns>
        bool CanGoFullScreen { get; }
    }
}
