using Baconit.Interfaces;
using System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using BaconBackend.Managers;

namespace Baconit.ContentPanels.Panels
{
    public sealed partial class WindowsAppContentPanel : IContentPanel
    {
        /// <summary>
        /// Holds a reference to our base.
        /// </summary>
        private readonly IContentPanelBaseInternal _contentPanelBase;

        /// <summary>
        /// A hidden webview used if we can't parse the string.
        /// </summary>
        private WebView _hiddenWebView;

        public WindowsAppContentPanel(IContentPanelBaseInternal panelBase)
        {
            InitializeComponent();
            _contentPanelBase = panelBase;
        }

        /// <summary>
        /// Called by the host when it queries if we can handle a post.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static bool CanHandlePost(ContentPanelSource source)
        {
            var urlLower = source.Url.ToLower();
            return urlLower.Contains("microsoft.com") && (urlLower.Contains("/store/apps/") || urlLower.Contains("/store/games/"));
        }

        #region IContentPanel

        /// <summary>
        /// Indicates how large the panel is in memory.
        /// </summary>
        public PanelMemorySizes PanelMemorySize => PanelMemorySizes.Small;

        /// <summary>
        /// Fired when we should load the content.
        /// </summary>
        public void OnPrepareContent()
        {
            // Hide loading
            _contentPanelBase.FireOnLoading(false);
        }

        /// <summary>
        /// Fired when we should destroy our content.
        /// </summary>
        public void OnDestroyContent()
        {
            // Kill the web view
            if (_hiddenWebView != null)
            {
                _hiddenWebView.NavigationCompleted -= HiddenWebView_NavigationCompleted;
            }
            _hiddenWebView = null;
        }

        /// <summary>
        /// Fired when a new host has been added.
        /// </summary>
        public void OnHostAdded()
        {
            // Ignore for now.
        }

        /// <summary>
        /// Fired when this post becomes visible
        /// </summary>
        public void OnVisibilityChanged(bool isVisible)
        {
            // Ignore for now
        }

        #endregion

        #region Tapped events

        /// <summary>
        /// Call the global content viewer to show the content.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ContentRoot_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_contentPanelBase.Source.Url))
            {
                // Try to parse out the app id if we can, loading the webpage can take a long
                // time so we want to avoid it if possible
                var successfullyParsedAppId = false;
                try
                {
                    // We are looking to parse 9nblggh58t2h out of something like this
                    // https://www.microsoft.com/en-us/store/apps/device-diagnostics-hub/9nblggh58t2h?cid=appraisin
                    // or this
                    // https://www.microsoft.com/store/apps/9wzdncrfhw68
                    //
                    // Note the /apps/ changes also

                    // Find the last / this should be just before the app id.
                    var appIdStart = _contentPanelBase.Source.Url.LastIndexOf('/') + 1;

                    // Make sure we found one.
                    if (appIdStart != 0)
                    {
                        // Find the ending, look for a ? if there is one.
                        var appIdEnd = _contentPanelBase.Source.Url.IndexOf('?', appIdStart);
                        if (appIdEnd == -1)
                        {
                            appIdEnd = _contentPanelBase.Source.Url.Length;
                        }

                        // Get the app id
                        var appId = _contentPanelBase.Source.Url.Substring(appIdStart, appIdEnd - appIdStart);

                        // Do a quick sanity check
                        if (appId.Length > 4)
                        {
                            successfullyParsedAppId = await Windows.System.Launcher.LaunchUriAsync(new Uri($"ms-windows-store://pdp/?ProductId={appId}"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.BaconMan.MessageMan.DebugDia("failed to parse app id", ex);
                    TelemetryManager.ReportEvent(this, "FailedToParseAppId");
                }

                // If we failed use the web browser
                if (successfullyParsedAppId) return;

                // Show our loading overlay
                ui_loadingOverlay.Show();

                // If we have a link open it in our hidden webview, this will cause the store
                // to redirect us.
                _hiddenWebView = new WebView(WebViewExecutionMode.SeparateThread);
                _hiddenWebView.NavigationCompleted += HiddenWebView_NavigationCompleted;
                _hiddenWebView.Navigate(new Uri(_contentPanelBase.Source.Url, UriKind.Absolute));
            }
        }

        #endregion

        #region Web View Events

        /// <summary>
        /// Fired when the webview is loaded.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void HiddenWebView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            // Stop the callbacks
            _hiddenWebView.NavigationCompleted -= HiddenWebView_NavigationCompleted;

            // Hide the overlay
            ui_loadingOverlay.Hide();
        }

        #endregion

    }
}
