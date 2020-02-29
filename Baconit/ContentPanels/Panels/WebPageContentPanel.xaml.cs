using Baconit.Interfaces;
using System;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using BaconBackend.Managers;

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class WebPageContentPanel : IContentPanel
    {
        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        private readonly IContentPanelBaseInternal _contentPanelBase;

        /// <summary>
        /// Holds a reference to the web view if there is one.
        /// </summary>
        private WebView _webView;

        private readonly object _lockObject = new object();

        public WebPageContentPanel(IContentPanelBaseInternal panelBase)
        {
            InitializeComponent();

            // Capture the base
            _contentPanelBase = panelBase;

            // Listen for back presses
            App.BaconMan.OnBackButton += BaconMan_OnBackButton;
        }

        #region IContentPanel

        /// <summary>
        /// Indicates how large the panel is in memory.
        /// </summary>
        public PanelMemorySizes PanelMemorySize => PanelMemorySizes.Large;

        /// <summary>
        /// Fired when we should load the content.
        /// </summary>
        public async void OnPrepareContent()
        {
            // Since some of this can be costly, delay the work load until we aren't animating.
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                lock (_lockObject)
                {
                    // If we are destroyed or already created return.
                    if (_contentPanelBase.IsDestroyed || _webView != null)
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
            lock(_lockObject)
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
            if (_webView != null)
            {
                return;
            }

            // Make the web-view
            _webView = new WebView(WebViewExecutionMode.SeparateThread);

            // Setup the listeners, we need all of these because some web pages don't trigger
            // some of them.
            _webView.FrameNavigationCompleted += NavigationCompleted;
            _webView.NavigationFailed += NavigationFailed;
            _webView.DOMContentLoaded += DomContentLoaded;
            _webView.ContentLoading += ContentLoading;
            _webView.ContainsFullScreenElementChanged += ContainsFullScreenElementChanged;

            // Navigate
            try
            {
                _webView.Navigate(new Uri(_contentPanelBase.Source.Url, UriKind.Absolute));
            }
            catch (Exception e)
            {
                TelemetryManager.ReportUnexpectedEvent(this, "FailedToMakeUriInWebControl", e);
                _contentPanelBase.FireOnError(true, "This web page failed to load");
            }

            // Now add an event for navigating.
            _webView.NavigationStarting += NavigationStarting;

            // Insert this before the full screen button.
            ui_contentRoot.Children.Insert(0, _webView);
        }

        /// <summary>
        /// Destroys a web view if there is one and sets the flag. This should be called under lock!
        /// </summary>
        private void DestroyWebView()
        {
            // Destroy if it exists
            if (_webView != null)
            {
                // Clear handlers
                _webView.FrameNavigationCompleted -= NavigationCompleted;
                _webView.NavigationFailed -= NavigationFailed;
                _webView.DOMContentLoaded -= DomContentLoaded;
                _webView.ContentLoading -= ContentLoading;
                _webView.NavigationStarting -= NavigationStarting;
                _webView.ContainsFullScreenElementChanged -= ContainsFullScreenElementChanged;

                // Clear the webview
                _webView.Stop();
                _webView.NavigateToString("");

                // Remove it from the UI.
                ui_contentRoot.Children.Remove(_webView);
            }

            // Null it
            _webView = null;
        }

        private void DomContentLoaded(WebView sender, WebViewDOMContentLoadedEventArgs args)
        {
            _contentPanelBase.FireOnLoading(false);
            HideReadingModeLoading();
        }

        private void ContentLoading(WebView sender, WebViewContentLoadingEventArgs args)
        {
            _contentPanelBase.FireOnLoading(false);
            HideReadingModeLoading();
        }

        private void NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            _contentPanelBase.FireOnLoading(false);
            HideReadingModeLoading();
        }
        private void NavigationFailed(object sender, WebViewNavigationFailedEventArgs e)
        {
            _contentPanelBase.FireOnError(true, "This web page is broken");
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
            try
            {
                lock (_lockObject)
                {
                    ui_readingModeLoading.Visibility = Visibility.Visible;
                    ui_readingModeLoading.IsActive = true;
                    ui_readingModeIconHolder.Visibility = Visibility.Collapsed;
                    _webView.Navigate(new Uri($"http://www.readability.com/m?url={_contentPanelBase.Source.Url}", UriKind.Absolute) );
                }
            }
            catch (Exception ex)
            {
                TelemetryManager.ReportUnexpectedEvent(this, "FailedToNavReadingMode", ex);
            }

            TelemetryManager.ReportEvent(this, "ReadingModeEnabled");
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
            if (_webView == null) return;

            // Go back
            lock (_lockObject)
            {
                if (_webView.CanGoBack)
                {
                    _webView.GoBack();
                }
            }


            // Delay a little while so CanGoBack gets updated
            await Task.Delay(500);

            lock (_lockObject)
            {
                ToggleBackButton(_webView.CanGoBack);
            }
        }

        private void ToggleBackButton(bool show)
        {
            if (show && Math.Abs(ui_backButton.Opacity - 1) < 1 ||
                !show && Math.Abs(ui_backButton.Opacity) < 1)
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
            lock (_lockObject)
            {
                if (Math.Abs(ui_backButton.Opacity) < 1)
                {
                    ui_backButton.Visibility = Visibility.Collapsed;
                }
            }
        }

        #endregion

        #region Full Screen Logic

        /// <summary>
        /// Shows or hides the full screen button.
        /// </summary>
        private async void SetupFullscreenButton()
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                ui_fullScreenHolder.Visibility = _contentPanelBase.CanGoFullscreen ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        /// <summary>
        /// Fired when the user taps the full screen button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FullScreenHolder_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ToggleFullScreen(!_contentPanelBase.IsFullscreen);
        }

        /// <summary>
        /// Fire when the user presses back.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BaconMan_OnBackButton(object sender, BaconBackend.BackButtonArgs e)
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
            if (_webView == null)
            {
                return;
            }

            if (_webView.ContainsFullScreenElement)
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
            bool didAction;

            // Set the state
            didAction = _contentPanelBase.FireOnFullscreenChanged(goFullScreen);

            // Update the icon
            ui_fullScreenIcon.Symbol = _contentPanelBase.IsFullscreen ? Symbol.BackToWindow : Symbol.FullScreen;

            // Set our manipulation mode to capture all input
            ManipulationMode = _contentPanelBase.IsFullscreen ? ManipulationModes.All : ManipulationModes.System;

            return didAction;
        }

        #endregion
    }
}
