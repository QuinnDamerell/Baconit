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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


namespace Baconit.Panels
{
    public sealed partial class LoginPanel : UserControl, IPanel
    {
        /// <summary>
        /// A reference to the host
        /// </summary>
        IPanelHost m_host;

        /// <summary>
        /// Indicates if this panel is visible or not.
        /// </summary>
        bool m_isVisible = false;

        public LoginPanel()
        {
            this.InitializeComponent();

            // Setup the image.
            List<Uri> uriList = new List<Uri>();
            uriList.Add(new Uri("ms-appx:///Assets/Welcome/QuinnImageMedium.jpg", UriKind.Absolute));
            ui_imageScrolBackground.SetImages(uriList);
        }

        public void PanelSetup(IPanelHost host, Dictionary<string, object> arguments)
        {
            m_host = host;
        }

        public async void OnNavigatingTo()
        {
            // Set the status bar color and get the size returned. If it is not 0 use that to move the
            // color of the page into the status bar.
            double statusBarHeight  = await m_host.SetStatusBar(null, 0);
            ui_contentRoot.Margin = new Thickness(0, -statusBarHeight, 0, 0);

            ui_imageScrolBackground.BeginAnimation();
            m_isVisible = true;
        }

        public void OnNavigatingFrom()
        {
            ui_imageScrolBackground.StopAnimation();
            m_isVisible = false;
        }

        public void OnPanelPulledToTop(Dictionary<string, object> arguments)
        {
            OnNavigatingTo();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            App.BaconMan.TelemetryMan.ReportEvent(this, "LoginButtonClicked");
            DateTime loginBegin = DateTime.Now;

            // Change the UI
            CrossfadeUi(true);

            // Make the call
            UserManager.SignInResult result = await App.BaconMan.UserMan.SignInNewUser();

            if(result.WasSuccess)
            {
                App.BaconMan.TelemetryMan.ReportEvent(this, "LoginSuccess");
                App.BaconMan.TelemetryMan.ReportPerfEvent(this,"LoginTime", loginBegin);
                ShowWelcomeAndLeave();
            }
            else
            {
                // We failed
                CrossfadeUi(false);

                if (result.WasErrorNetwork)
                {
                    App.BaconMan.TelemetryMan.ReportEvent(this, "LoginFailedNetworkError");
                    App.BaconMan.MessageMan.ShowMessageSimple("Check Your Connection", "We can't talk to reddit right now, check your internet connection.");
                }
                if(result.WasUserCanceled)
                {
                    // Don't do anything, they know what they did.
                    App.BaconMan.TelemetryMan.ReportEvent(this, "LoginFailedUserCancled");
                }
                else
                {
                    App.BaconMan.MessageMan.ShowMessageSimple("Something Went Wrong", "We can't log you in right now, try again later.");
                    App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "LoginFailedUnknown");
                }
            }
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

            if (m_isVisible)
            {
                // Tell the host to get us out of here.
                m_host.GoBack();
            }
        }

        private void CreateAccount_Click(object sender, RoutedEventArgs e)
        {
            App.BaconMan.ShowGlobalContent("https://www.reddit.com/register");
        }
    }
}
