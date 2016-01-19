using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


namespace Baconit.ContentPanels.Panels
{
    public sealed partial class WebPageContentPanel : UserControl, IContentPanel
    {
        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        IContentPanelBaseInternal m_base;

        /// <summary>
        /// Holds a reference to the web view if there is one.
        /// </summary>
        WebView m_webView = null;

        public WebPageContentPanel(IContentPanelBaseInternal panelBase)
        {
            this.InitializeComponent();

            // Capture the base
            m_base = panelBase;

            // Listen for back presses
            App.BaconMan.OnBackButton += BaconMan_OnBackButton;
        }

        #region IContentPanel

        /// <summary>
        /// Indicates how large the panel is in memory.
        /// </summary>
        public PanelMemorySizes PanelMemorySize
        {
            get
            {
                return PanelMemorySizes.Large;
            }
        }

        /// <summary>
        /// Fired when we should load the content.
        /// </summary>
        /// <param name="source"></param>
        public async void OnPrepareContent()
        {
            // Since some of this can be costly, delay the work load until we aren't animating.
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                lock (this)
                {
                    // If we are destroyed or already created return.
                    if (m_base.IsDestoryed || m_webView != null)
                    {
                        return;
                    }

                    // Make sure we have resources to make a not visible control.
                    //if (CanMakeNotVisibleWebHost())
                    {
                        MakeWebView();
                    }
                }
            });
        }

        /// <summary>
        /// Fired when this post becomes visible
        /// </summary>
        public void OnVisibilityChanged(bool isVisible)
        {
            // Do nothing for now.
        }

        /// <summary>
        /// Fired when we should destroy our content.
        /// </summary>
        public void OnDestroyContent()
        {
            lock(this)
            {
                DestroyWebView();
            }
        }

        /// <summary>
        /// Called when a new host is added.
        /// </summary>
        public void OnHostAdded()
        {
            // Setup the full screen UI.
            SetupFullscreenButton();
        }

        #endregion

        #region Webview Logic

        /// <summary>
        /// Creates a new webview, this should be called under lock!
        /// </summary>
        private void MakeWebView()
        {
            if (m_webView != null)
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
            m_webView.ContainsFullScreenElementChanged += ContainsFullScreenElementChanged;

            // Navigate
            try
            {
                m_webView.Navigate(new Uri(m_base.Source.Url, UriKind.Absolute));
            }
            catch (Exception e)
            {
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToMakeUriInWebControl", e);
                m_base.FireOnError(true, "This web page failed to load");
            }

            // Now add an event for navigating.
            m_webView.NavigationStarting += NavigationStarting;

            // Insert this before the full screen button.
            ui_contentRoot.Children.Insert(0, m_webView);
        }

        /// <summary>
        /// Destroys a web view if there is one and sets the flag. This should be called under lock!
        /// </summary>
        private void DestroyWebView()
        {
            // Destroy if it exists
            if (m_webView != null)
            {
                // Clear handlers
                m_webView.FrameNavigationCompleted -= NavigationCompleted;
                m_webView.NavigationFailed -= NavigationFailed;
                m_webView.DOMContentLoaded -= DOMContentLoaded;
                m_webView.ContentLoading -= ContentLoading;
                m_webView.NavigationStarting -= NavigationStarting;
                m_webView.ContainsFullScreenElementChanged -= ContainsFullScreenElementChanged;

                // Clear the webview
                m_webView.Stop();
                m_webView.NavigateToString("");

                // Remove it from the UI.
                ui_contentRoot.Children.Remove(m_webView);
            }

            // Null it
            m_webView = null;
        }

        private void DOMContentLoaded(WebView sender, WebViewDOMContentLoadedEventArgs args)
        {
            m_base.FireOnLoading(false);
            HideReadingModeLoading();
        }

        private void ContentLoading(WebView sender, WebViewContentLoadingEventArgs args)
        {
            m_base.FireOnLoading(false);
            HideReadingModeLoading();
        }

        private void NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            m_base.FireOnLoading(false);
            HideReadingModeLoading();
        }
        private void NavigationFailed(object sender, WebViewNavigationFailedEventArgs e)
        {
            m_base.FireOnError(true, "This web page is broken");
        }

        private void NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            ToggleBackButton(true);
        }

        #endregion

        #region Reading Mode Logic

        private void ReadingMode_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Show loading
            ui_readingModeLoading.Visibility = Visibility.Visible;
            ui_readingModeLoading.IsActive = true;
            ui_readingModeIconHolder.Visibility = Visibility.Collapsed;

            // Navigate.
            try
            {
                m_webView.Navigate(new Uri("http://www.readability.com/m?url=" + m_base.Source.Url, UriKind.Absolute) );
            }
            catch (Exception ex)
            {
                App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToNavReadingMode", ex);
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

        #region Full Screen Logic

        /// <summary>
        /// Shows or hides the full screen button.
        /// </summary>
        public async void SetupFullscreenButton()
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                ui_fullScreenHolder.Visibility = (m_base.CanGoFullscreen) ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        /// <summary>
        /// Fired when the user taps the full screen button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FullScreenHolder_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ToggleFullScreen(!m_base.IsFullscreen);
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
        private void ContainsFullScreenElementChanged(WebView sender, object args)
        {
            if (m_webView == null)
            {
                return;
            }

            if (m_webView.ContainsFullScreenElement)
            {
                // Go full screen
                ToggleFullScreen(true);

                // Hide the overlays, let the webcontrol take care of it (we don't want to overlap videos)
                ui_webviewOverlays.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Jump out of full screen
                ToggleFullScreen(false);

                // Restore the overlays
                ui_webviewOverlays.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Asks the host to toggle full screen.
        /// </summary>
        /// <param name="goFullScreen"></param>
        /// <returns></returns>

        private bool ToggleFullScreen(bool goFullScreen)
        {
            bool didAction = false;

            // Set the state
            didAction = m_base.FireOnFullscreenChanged(goFullScreen);

            // Update the icon
            ui_fullScreenIcon.Symbol = m_base.IsFullscreen ? Symbol.BackToWindow : Symbol.FullScreen;

            // Set our manipulation mode to capture all input
            ManipulationMode = m_base.IsFullscreen ? ManipulationModes.All : ManipulationModes.System;

            return didAction;
        }

        #endregion
    }
}
