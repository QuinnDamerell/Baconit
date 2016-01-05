using BaconBackend.DataObjects;
using BaconBackend.Managers;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


namespace Baconit.FlipViewControls
{
    public sealed partial class WebPageFlipControl : UserControl, IFlipViewContentControl
    {
        /// <summary>
        /// Reference to the host
        /// </summary>
        IFlipViewContentHost m_host;

        /// <summary>
        /// Holds a reference to the webview.
        /// </summary>
        WebView m_webView = null;

        /// <summary>
        /// Indicates if we have called hide or not.
        /// </summary>
        bool m_loadingHidden = false;

        /// <summary>
        /// Indicates if we should be destroyed
        /// </summary>
        bool m_isDestroyed = false;

        /// <summary>
        /// The url of the post we are viewing.
        /// </summary>
        string m_postUrl = String.Empty;

        /// <summary>
        /// Indicate is reading mode is enabled or not.
        /// </summary>
        bool m_isReadigModeEnabled = false;

        public WebPageFlipControl(IFlipViewContentHost host)
        {
            this.InitializeComponent();
            m_host = host;

            // Hide the full screen icon if we can't go full screen.
            ui_fullScreenHolder.Visibility = m_host.CanGoFullScreen ? Visibility.Visible : Visibility.Collapsed;

            // Listen for back presses
            App.BaconMan.OnBackButton += BaconMan_OnBackButton;

            // Listen for memory pressure
            App.BaconMan.MemoryMan.OnMemoryCleanUpRequest += MemoryMan_OnMemoryCleanUpRequest;
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a post.
        /// </summary>
        /// <param name="post"></param>
        /// <returns></returns>
        static public bool CanHandlePost(Post post)
        {
            // Web view is the fall back, we should be handle just about anything.
            return true;
        }

        /// <summary>
        /// Called by the host when we should show content.
        /// </summary>
        /// <param name="post"></param>
        public async void OnPrepareContent(Post post)
        {
            // So the loading UI
            m_host.ShowLoading();
            m_loadingHidden = false;

            // Get the post url
            m_postUrl = post.Url;


            // Since some of this can be costly, delay the work load until we aren't animating.
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                lock (this)
                {
                    // If we are destroyed or already created return.
                    if (m_isDestroyed || m_webView != null)
                    {
                        return;
                    }

                    // Make sure we have resources to make a not visible control.
                    if (CanMakeNotVisibleWebHost())
                    {
                        MakeWebView();
                    }

                    // If not we will try again when we become visible
                }
            });
        }


        /// <summary>
        /// Called when the  post actually becomes visible
        /// </summary>
        public async void OnVisible()
        {
            // Jump to the UI thread.
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (this)
                {
                    // If we are destroyed or already created return.
                    if (m_isDestroyed || m_webView != null)
                    {
                        return;
                    }

                    // Make sure we have resources to make a web view at all.
                    if (CanMakeVisibleWebHost())
                    {
                        MakeWebView();
                    }
                    else
                    {
                        // If we don't have the resources show the error
                        ShowLowResourcesError();
                        HideLoading();
                    }
                }
            });
        }

        /// <summary>
        /// Called by the host when the content should be destroyed.
        /// </summary>
        public void OnDestroyContent()
        {
            lock (this)
            {
                DestroyWebView();
            }
        }

        /// <summary>
        /// Creates a new webview, this should be called under lock!
        /// </summary>
        private void MakeWebView()
        {
            if(m_webView != null)
            {
                return;
            }

            // Make the webview
            m_webView = new WebView(WebViewExecutionMode.SeparateThread);

            // Setup the listeners, we need all of these because some web pages don't trigger
            // some of them.
            m_webView.FrameNavigationCompleted += NavigationCompleted;
            m_webView.NavigationFailed += NavigationFailed;
            m_webView.DOMContentLoaded += DOMContentLoaded;
            m_webView.ContentLoading += ContentLoading;
            m_webView.ContainsFullScreenElementChanged += WebView_ContainsFullScreenElementChanged;

            // Navigate
            try
            {
                m_webView.Navigate(new Uri(m_postUrl, UriKind.Absolute));
            }
            catch (Exception e)
            {
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToMakeUriInWebControl", e);
                m_host.ShowError();
            }

            // Now add an event for navigating.
            m_webView.NavigationStarting += NavigationStarting;

            // Insert this before the full screen button.
            ui_contentRoot.Children.Insert(0, m_webView);

            // Show the overlays
            ui_webviewOverlays.Visibility = Visibility.Visible;
            // Hide the error UI.
            ui_lowResourcesError.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Destroys a web view if there is one and sets the flag. This should be called under lock!
        /// </summary>
        private void DestroyWebView()
        {
            // Set the flag
            m_isDestroyed = true;

            // Destroy if it exists
            if (m_webView != null)
            {
                // Clear handlers
                m_webView.FrameNavigationCompleted -= NavigationCompleted;
                m_webView.NavigationFailed -= NavigationFailed;
                m_webView.DOMContentLoaded -= DOMContentLoaded;
                m_webView.ContentLoading -= ContentLoading;

                // Clear the webview
                m_webView.Stop();
                m_webView.NavigateToString("");

                // Remove it from the UI.
                ui_contentRoot.Children.Remove(m_webView);
            }

            // Null it
            m_webView = null;

            // Once destroyed we will never be shown again. So we can stop listening for memory pressure.
            App.BaconMan.MemoryMan.OnMemoryCleanUpRequest -= MemoryMan_OnMemoryCleanUpRequest;
        }

        private void DOMContentLoaded(WebView sender, WebViewDOMContentLoadedEventArgs args)
        {
            HideLoading();
            HideReadingModeLoading();
        }

        private void ContentLoading(WebView sender, WebViewContentLoadingEventArgs args)
        {
            HideLoading();
            HideReadingModeLoading();
        }

        private void NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            HideLoading();
            HideReadingModeLoading();
        }
        private void NavigationFailed(object sender, WebViewNavigationFailedEventArgs e)
        {
            m_host.ShowError();
        }

        private void NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            ToggleBackButton(true);
        }

        /// <summary>
        /// Calls hide loading on the host if we haven't already.
        /// </summary>
        private void HideLoading()
        {
            // Make sure we haven't been called before.
            lock (m_host)
            {
                if (m_loadingHidden)
                {
                    return;
                }
                m_loadingHidden = true;
            }

            // Hide it.
            m_host.HideLoading();
        }

        #region Full Screen Logic

        /// <summary>
        /// Fired when the user taps the full screen button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FullScreenHolder_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ToggleFullScreen(!m_host.IsFullScreen());
        }

        /// <summary>
        /// Fire when the user presses back.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BaconMan_OnBackButton(object sender, BaconBackend.OnBackButtonArgs e)
        {
            if (e.IsHandled)
            {
                return;
            }

            // Kill it if we are.
            e.IsHandled = ToggleFullScreen(false);
        }

        /// <summary>
        /// Fired when something in the website goes full screen.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void WebView_ContainsFullScreenElementChanged(WebView sender, object args)
        {
            if (m_webView == null)
            {
                return;
            }

            if (m_webView.ContainsFullScreenElement)
            {
                // Go full screen
                ToggleFullScreen(true);

                // Hide the full screen button, let the webcontrol take care of it (we don't want to overlap
                ui_fullScreenHolder.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Jump out of full screen
                ToggleFullScreen(false);

                // Restore the button
                ui_fullScreenHolder.Visibility = m_host.CanGoFullScreen ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private bool ToggleFullScreen(bool goFullScreen)
        {
            // Set the state
            bool didAction = m_host.ToggleFullScreen(goFullScreen);

            // Update the icon
            ui_fullScreenIcon.Symbol = m_host.IsFullScreen() ? Symbol.BackToWindow : Symbol.FullScreen;

            // Set our manipulation mode to capture all input
            ManipulationMode = m_host.IsFullScreen() ? ManipulationModes.All : ManipulationModes.System;

            return didAction;
        }

        #endregion

        #region Reading Mode Logic

        private void ReadingMode_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Show loading
            ui_readingModeLoading.Visibility = Visibility.Visible;
            ui_readingModeLoading.IsActive = true;
            ui_readingModeIconHolder.Visibility = Visibility.Collapsed;

            // Flip
            m_isReadigModeEnabled = !m_isReadigModeEnabled;

            // Get the URI
            Uri webLink = null;
            if (m_isReadigModeEnabled)
            {
                webLink = new Uri("http://www.readability.com/m?url=" + m_postUrl, UriKind.Absolute);
            }
            else
            {
                webLink = new Uri(m_postUrl, UriKind.Absolute);
            }

            // Navigate.
            try
            {
                m_webView.Navigate(webLink);
            }
            catch (Exception ex)
            {
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToNavReadingMode", ex);
                m_host.ShowError();
            }

            App.BaconMan.TelemetryMan.ReportEvent(this, "ReadingModeEnabled");
        }

        private void HideReadingModeLoading()
        {
            ui_readingModeLoading.Visibility = Visibility.Collapsed;
            ui_readingModeLoading.IsActive = false;
            ui_readingModeIconHolder.Visibility = Visibility.Visible;
        }

        #endregion

        #region Back Button

        private async void BackButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (m_webView != null)
            {
                // Go back
                if (m_webView.CanGoBack)
                {
                    m_webView.GoBack();
                }

                // Delay a little while so CanGoBack gets updated
                await Task.Delay(500);

                ToggleBackButton(m_webView.CanGoBack);
            }
        }

        public void ToggleBackButton(bool show)
        {
            if ((show && ui_backButton.Opacity == 1) ||
                (!show && ui_backButton.Opacity == 0))
            {
                return;
            }

            if (show)
            {
                ui_backButton.Visibility = Visibility.Visible;
            }
            anim_backButtonFade.To = show ? 1 : 0;
            anim_backButtonFade.From = show ? 0 : 1;
            story_backButtonFade.Begin();
        }

        private void BackButtonFade_Completed(object sender, object e)
        {
            if (ui_backButton.Opacity == 0)
            {
                ui_backButton.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        #region Memory Pressure Logic

        /// <summary>
        /// Returns if we can currently make the web control that is visible.
        /// </summary>
        /// <returns></returns>
        private bool CanMakeVisibleWebHost()
        {
            // If we are not at high we can do this.
            return App.BaconMan.MemoryMan.MemoryPressure < MemoryPressureStates.HighNoAllocations;
        }

        /// <summary>
        /// Returns if we can currently make the web control that is not visible (pre loading).
        /// </summary>
        /// <returns></returns>
        private bool CanMakeNotVisibleWebHost()
        {
            // If we are not at medium we can do this.
            return App.BaconMan.MemoryMan.MemoryPressure < MemoryPressureStates.Medium;
        }

        /// <summary>
        /// Fired when the memory manager requests cleanup.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MemoryMan_OnMemoryCleanUpRequest(object sender, BaconBackend.Managers.OnMemoryCleanupRequestArgs e)
        {
            // Make sure it is medium or high.
            if (e.CurrentPressure > MemoryPressureStates.Low)
            {
                // Jump to the UI thread, use high because we might need to get this web view out of here.
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    lock (this)
                    {
                        // If we are already gone get out of here.
                        if(m_isDestroyed)
                        {
                            return;
                        }

                        bool shouldDestroy = true;

                        // If we are at medium...
                        if(e.CurrentPressure == MemoryPressureStates.Medium)
                        {
                            // Don't destroy if we are visible
                            if(m_host.IsVisible)
                            {
                                shouldDestroy = false;
                            }
                            else
                            {
                                // Or if we aren't visible and we haven't make the web control yet.
                                // If we destory here we will never make the control even when visible.
                                if(m_webView == null)
                                {
                                    shouldDestroy = false;
                                }
                            }
                        }

                        // Do it if we should.
                        if(shouldDestroy)
                        {
                            // Destroy the control
                            DestroyWebView();

                            // Show the error.
                            ShowLowResourcesError();
                            HideLoading();
                        }
                    }
                });
            }
        }

        private void ShowLowResourcesError()
        {
            // Hide the overlays
            ui_webviewOverlays.Visibility = Visibility.Collapsed;

            // Show the error
            ui_lowResourcesError.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Fired when the user taps the error screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void LowResourcesError_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }

            // Navigate to the web browser.
            try
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(m_postUrl, UriKind.Absolute));
            }
            catch (Exception ex)
            {
                App.BaconMan.MessageMan.DebugDia("failed to open IE", ex);
            }
        }

        /// <summary>
        /// Fired if the user taps the help icon.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LowResourcesHelp_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;
            App.BaconMan.MessageMan.ShowMessageSimple("Out of Resources", $"Windows apps have a limited amount of memory (RAM) they are allowed to use. Currently Baconit is using { Math.Round(App.BaconMan.MemoryMan.CurrentMemoryUsagePercentage * 100) }% of its allocation; if it exceeds 100% the app will crash. This website would push Baconit over the limit, so we stopped loading it.\n\nApp memory limits are determined by Windows depending on how much memory your device has. Reddit content is often quite large (images, gifs, websites, etc) so lower end devices might have trouble showing content. Baconit has an advance memory manager that will attempt to free up space, but it isn’t always possible.\n\nIf you see this error often, please post on /r/Baconit and we will take a look.");
        }

        #endregion
    }
}
