﻿using Baconit.Interfaces;
using System;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using BaconBackend.Managers;


namespace Baconit.ContentPanels.Panels
{
    public sealed partial class WebPageContentPanel : UserControl, IContentPanel
    {
        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        private readonly IContentPanelBaseInternal _mBase;

        /// <summary>
        /// Holds a reference to the web view if there is one.
        /// </summary>
        private WebView _mWebView;

        public WebPageContentPanel(IContentPanelBaseInternal panelBase)
        {
            InitializeComponent();

            // Capture the base
            _mBase = panelBase;

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
        /// <param name="source"></param>
        public async void OnPrepareContent()
        {
            // Since some of this can be costly, delay the work load until we aren't animating.
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                lock (this)
                {
                    // If we are destroyed or already created return.
                    if (_mBase.IsDestoryed || _mWebView != null)
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
            if (_mWebView != null)
            {
                return;
            }

            // Make the webview
            _mWebView = new WebView(WebViewExecutionMode.SeparateThread);

            // Setup the listeners, we need all of these because some web pages don't trigger
            // some of them.
            _mWebView.FrameNavigationCompleted += NavigationCompleted;
            _mWebView.NavigationFailed += NavigationFailed;
            _mWebView.DOMContentLoaded += DomContentLoaded;
            _mWebView.ContentLoading += ContentLoading;
            _mWebView.ContainsFullScreenElementChanged += ContainsFullScreenElementChanged;

            // Navigate
            try
            {
                _mWebView.Navigate(new Uri(_mBase.Source.Url, UriKind.Absolute));
            }
            catch (Exception e)
            {
                TelemetryManager.ReportUnexpectedEvent(this, "FailedToMakeUriInWebControl", e);
                _mBase.FireOnError(true, "This web page failed to load");
            }

            // Now add an event for navigating.
            _mWebView.NavigationStarting += NavigationStarting;

            // Insert this before the full screen button.
            ui_contentRoot.Children.Insert(0, _mWebView);
        }

        /// <summary>
        /// Destroys a web view if there is one and sets the flag. This should be called under lock!
        /// </summary>
        private void DestroyWebView()
        {
            // Destroy if it exists
            if (_mWebView != null)
            {
                // Clear handlers
                _mWebView.FrameNavigationCompleted -= NavigationCompleted;
                _mWebView.NavigationFailed -= NavigationFailed;
                _mWebView.DOMContentLoaded -= DomContentLoaded;
                _mWebView.ContentLoading -= ContentLoading;
                _mWebView.NavigationStarting -= NavigationStarting;
                _mWebView.ContainsFullScreenElementChanged -= ContainsFullScreenElementChanged;

                // Clear the webview
                _mWebView.Stop();
                _mWebView.NavigateToString("");

                // Remove it from the UI.
                ui_contentRoot.Children.Remove(_mWebView);
            }

            // Null it
            _mWebView = null;
        }

        private void DomContentLoaded(WebView sender, WebViewDOMContentLoadedEventArgs args)
        {
            _mBase.FireOnLoading(false);
            HideReadingModeLoading();
        }

        private void ContentLoading(WebView sender, WebViewContentLoadingEventArgs args)
        {
            _mBase.FireOnLoading(false);
            HideReadingModeLoading();
        }

        private void NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            _mBase.FireOnLoading(false);
            HideReadingModeLoading();
        }
        private void NavigationFailed(object sender, WebViewNavigationFailedEventArgs e)
        {
            _mBase.FireOnError(true, "This web page is broken");
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
                _mWebView.Navigate(new Uri("http://www.readability.com/m?url=" + _mBase.Source.Url, UriKind.Absolute) );
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
            if (_mWebView != null)
            {
                // Go back
                if (_mWebView.CanGoBack)
                {
                    _mWebView.GoBack();
                }

                // Delay a little while so CanGoBack gets updated
                await Task.Delay(500);

                ToggleBackButton(_mWebView.CanGoBack);
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
                ui_fullScreenHolder.Visibility = (_mBase.CanGoFullscreen) ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        /// <summary>
        /// Fired when the user taps the full screen button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FullScreenHolder_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ToggleFullScreen(!_mBase.IsFullscreen);
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
            if (_mWebView == null)
            {
                return;
            }

            if (_mWebView.ContainsFullScreenElement)
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
            var didAction = false;

            // Set the state
            didAction = _mBase.FireOnFullscreenChanged(goFullScreen);

            // Update the icon
            ui_fullScreenIcon.Symbol = _mBase.IsFullscreen ? Symbol.BackToWindow : Symbol.FullScreen;

            // Set our manipulation mode to capture all input
            ManipulationMode = _mBase.IsFullscreen ? ManipulationModes.All : ManipulationModes.System;

            return didAction;
        }

        #endregion
    }
}
