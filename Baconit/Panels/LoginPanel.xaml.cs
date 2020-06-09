using BaconBackend.Managers;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace Baconit.Panels
{
    public sealed partial class LoginPanel : UserControl, IPanel
    {
        /// <summary>
        /// A reference to the host
        /// </summary>
        private IPanelHost _mHost;

        /// <summary>
        /// Indicates if this panel is visible or not.
        /// </summary>
        private bool _mIsVisible;

        private string _nonce;

        public LoginPanel()
        {
            InitializeComponent();

            // Setup the image.
            var uriList = new List<Uri>();
            uriList.Add(new Uri("ms-appx:///Assets/Welcome/QuinnImageMedium.jpg", UriKind.Absolute));
            ui_imageScrolBackground.SetImages(uriList);
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            _mHost = host;
        }

        public async void OnNavigatingTo()
        {
            // Set the status bar color and get the size returned. If it is not 0 use that to move the
            // color of the page into the status bar.
            var statusBarHeight  = await _mHost.SetStatusBar(null, 0);
            ui_contentRoot.Margin = new Thickness(0, -statusBarHeight, 0, 0);

            ui_imageScrolBackground.BeginAnimation();
            _mIsVisible = true;
            ui_loginUI.Visibility = Visibility.Visible;
            AuthWebViewUi.Visibility = Visibility.Collapsed;
        }

        public void OnNavigatingFrom()
        {
            ui_imageScrolBackground.StopAnimation();
            _mIsVisible = false;
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            OnNavigatingTo();
        }

        public void OnCleanupPanel()
        {
            // Ignore for now.
        }

        /// <summary>
        /// Fired when the panel should try to reduce memory if possible. This will only be called
        /// while the panel isn't visible.
        /// </summary>
        public void OnReduceMemory()
        {
            // Ignore for now.
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            TelemetryManager.ReportEvent(this, "LoginButtonClicked");
            var loginBegin = DateTime.Now;

            // Change the UI
            //CrossfadeUi(true);

            // Make the call
            //var result = await App.BaconMan.UserMan.SignInNewUser();

            _nonce = Guid.NewGuid().ToString("N");
            AuthWebView.Source = new Uri(App.BaconMan.UserMan.AuthManager.GetAuthRequestString(_nonce));
            AuthWebViewUi.Visibility = Visibility.Visible;

            //if(result.WasSuccess)
            //{
            //    TelemetryManager.ReportEvent(this, "LoginSuccess");
            //    TelemetryManager.ReportPerfEvent(this,"LoginTime", loginBegin);
            //    ShowWelcomeAndLeave();
            //}
            //else
            //{
            //    // We failed
            //    CrossfadeUi(false);

            //    if (result.WasErrorNetwork)
            //    {
            //        TelemetryManager.ReportEvent(this, "LoginFailedNetworkError");
            //        App.BaconMan.MessageMan.ShowMessageSimple("Check Your Connection", "We can't talk to reddit right now, check your internet connection.");
            //    }
            //    if(result.WasUserCanceled)
            //    {
            //        // Don't do anything, they know what they did.
            //        TelemetryManager.ReportEvent(this, "LoginFailedUserCancled");
            //    }
            //    else
            //    {
            //        App.BaconMan.MessageMan.ShowMessageSimple("Something Went Wrong", "We can't log you in right now, try again later.");
            //        TelemetryManager.ReportUnexpectedEvent(this, "LoginFailedUnknown");
            //    }
            //}
        }

        private void CrossfadeUi(bool fadeInProgressUi)
        {
            // Assume the animations might be running, so grab the current values.
            ui_animLoginUi.From = ui_loginUI.Opacity;
            ui_animLoginUi.To = fadeInProgressUi ? 0 : 1;
            ui_animProgressUI.From = ui_progressUI.Opacity;
            ui_animProgressUI.To = fadeInProgressUi ? 1 : 0;

            // Stop any running animation
            ui_storyProgressUI.Stop();
            ui_storyLoginUi.Stop();

            // Show the panel needed
            if (fadeInProgressUi)
            {
                // Prepare to fade in the progress UI.
                ui_progressUI.Visibility = Visibility.Visible;
                ui_progressRing.IsActive = true;
            }
            else
            {
                ui_loginUI.Visibility = Visibility.Visible;
            }

            // Set the button
            ui_loginButton.IsEnabled = !fadeInProgressUi;

            // Start the animations
            ui_storyLoginUi.Begin();
            ui_storyProgressUI.Begin();
        }

        private void ShowWelcomeAndLeave()
        {
            // Assume the animations might be running, so grab the current values.
            ui_animProgressUI.From = ui_progressUI.Opacity;
            ui_animProgressUI.To = 0;
            ui_animWelcome.From = 0;
            ui_animWelcome.To = 1;

            // Stop any running animation
            ui_storyProgressUI.Stop();

            // Some animation polish
            ui_animWelcome.BeginTime = new TimeSpan(0, 0, 1);
            ui_animWelcome.Duration = new TimeSpan(0, 0, 2);

            // Prepare to fade in the welcome UI.
            ui_welcome.Opacity = 0;
            ui_welcome.Visibility = Visibility.Visible;

            // Set the new text
            ui_welcomeText.Text = "Welcome " + App.BaconMan.UserMan.CurrentUser.Name;

            // Start the animations
            ui_storyWelcome.Begin();
            ui_storyProgressUI.Begin();
        }

        private async void AnimWelcome_Completed(object sender, object e)
        {
            // Give it some screen time
            await Task.Delay(1000);

            if (_mIsVisible)
            {
                // Tell the host to get us out of here.
                _mHost.GoBack();
            }
        }

        private void CreateAccount_Click(object sender, RoutedEventArgs e)
        {
            App.BaconMan.ShowGlobalContent("https://www.reddit.com/register");
        }

        private bool _startingAuth = false;
        private async void AuthWebViewNavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            var url = args.Uri.ToString();
            var redirectPath = new Uri(AuthManager.BaconitRedirectUrl);

            if (args.Uri.AbsolutePath.Equals(redirectPath.AbsolutePath) && !_startingAuth)
            {
                _startingAuth = true;
                CrossfadeUi(true);
                AuthWebViewUi.Visibility = Visibility.Collapsed;

                var result = ParseState(url);

                if (!result.WasSuccess) return;

                // Try to get the access token
                var accessToken = await App.BaconMan.UserMan.AuthManager.RefreshAccessToken(result.Message, false);
                if (accessToken == null)
                {
                    CrossfadeUi(false);
                    return;
                }

                // Try to get the new user
                result = await App.BaconMan.UserMan.InternalUpdateUser();

                if (result.WasSuccess)
                {
                    TelemetryManager.ReportEvent(this, "LoginSuccess");
                    ShowWelcomeAndLeave();
                    _startingAuth = false;
                }
            }
        }

        private UserManager.SignInResult ParseState(string url)
        {
            var startOfState = url.IndexOf("state=", StringComparison.Ordinal) + 6;
            var endOfState = url.IndexOf("&", startOfState, StringComparison.Ordinal);
            var startOfCode = url.IndexOf("code=", StringComparison.Ordinal) + 5;
            var endOfCode = url.IndexOf("&", startOfCode, StringComparison.Ordinal);

            if (startOfCode == 4)
            {
                return new UserManager.SignInResult
                {
                    Message = "Reddit returned an error!"
                };
            }

            endOfCode = endOfCode == -1 ? url.Length : endOfCode;
            endOfState = endOfState == -1 ? url.Length : endOfState;

            var state = url.Substring(startOfState, endOfState - startOfState);
            var code = url.Substring(startOfCode, endOfCode - startOfCode);

            // Check the state
            if (_nonce != state)
            {
                return new UserManager.SignInResult
                {
                    Message = "The state is not the same!"
                };
            }

            // Check the code
            if (string.IsNullOrWhiteSpace(code))
            {
                return new UserManager.SignInResult
                {
                    Message = "The code is empty!"
                };
            }

            // Return the code!
            return new UserManager.SignInResult
            {
                WasSuccess = true,
                Message = code
            };
        }
    }
}
