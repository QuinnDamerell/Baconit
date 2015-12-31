using BaconBackend;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Baconit
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        public const string AccentColorLevel1Resource = "BaconitAccentColorLevel1Brush";
        public const string AccentColorLevel2Resource = "BaconitAccentColorLevel2Brush";
        public const string AccentColorLevel3Resource = "BaconitAccentColorLevel3Brush";
        public const string AccentColorLevel4Resource = "BaconitAccentColorLevel4Brush";

        // Usage baconit://debug?preventcrashes=true
        private const string c_protocolPreventCrashesEnabled = "preventcrashes=true";
        private const string c_protocolPreventCrashesDisabled = "preventcrashes=false";

        /// <summary>
        /// The main reference in the app to the backend of Baconit
        /// </summary>
        public static BaconManager BaconMan;

        /// <summary>
        /// Indicates if we have already registered for back.
        /// </summary>
        private bool m_hasRegisteredForBack = false;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            // Setup the exception handler first
            this.UnhandledException += OnUnhandledException;

            // Now setup the baconman
            BaconMan = new BaconManager(false);

            // Now telemetry
            Microsoft.ApplicationInsights.WindowsAppInitializer.InitializeAsync(
                Microsoft.ApplicationInsights.WindowsCollectors.Metadata |
                Microsoft.ApplicationInsights.WindowsCollectors.UnhandledException |
                Microsoft.ApplicationInsights.WindowsCollectors.Session);

            // Init the app
            this.InitializeComponent();

            // Register for events.
            this.Suspending += OnSuspending_Fired;
            this.Resuming += OnResuming_Fired;
        }

        /// <summary>
        /// Fired when the app is opened from a toast message.
        /// </summary>
        /// <param name="args"></param>
        protected override void OnActivated(IActivatedEventArgs args)
        {
            base.OnActivated(args);

            if (args is ToastNotificationActivatedEventArgs)
            {
                ToastNotificationActivatedEventArgs toastArgs = (ToastNotificationActivatedEventArgs)args;
                SetupAndALaunchApp(toastArgs.Argument);
            }
            else if(args is ProtocolActivatedEventArgs)
            {
                ProtocolActivatedEventArgs protcolArgs = (ProtocolActivatedEventArgs)args;
                string argsString = protcolArgs.Uri.OriginalString;
                int protEnd = argsString.IndexOf("://");
                argsString = protEnd == -1 ? argsString : argsString.Substring(protEnd + 3);
                SetupAndALaunchApp(argsString);
            }
            else
            {
                SetupAndALaunchApp(String.Empty);
            }
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            SetupAndALaunchApp(e.Arguments);
        }

        /// <summary>
        /// Does the work necessary to setup and launch the app.
        /// </summary>
        /// <param name="arguments"></param>
        private void SetupAndALaunchApp(string arguments)
        {
            // Check the args for prevent crash
            if(!String.IsNullOrWhiteSpace(arguments))
            {
                string lowerArgs = arguments.ToLower();
                if(lowerArgs.Contains(c_protocolPreventCrashesDisabled))
                {
                    BaconMan.UiSettingsMan.Developer_StopFatalCrashesAndReport = false;
                }
                else if (lowerArgs.Contains(c_protocolPreventCrashesEnabled))
                {
                    BaconMan.UiSettingsMan.Developer_StopFatalCrashesAndReport = true;
                }
            }

            // Grab the accent color and make our custom accent color brushes.
            if (!Current.Resources.ContainsKey(AccentColorLevel1Resource))
            {
                Color accentColor = ((SolidColorBrush)Current.Resources["SystemControlBackgroundAccentBrush"]).Color;
                accentColor.A = 200;
                Current.Resources[AccentColorLevel1Resource] = new SolidColorBrush(accentColor);
                accentColor.A = 137;
                Current.Resources[AccentColorLevel2Resource] = new SolidColorBrush(accentColor);
                accentColor.A = 75;
                Current.Resources[AccentColorLevel3Resource] = new SolidColorBrush(accentColor);
                accentColor.A = 50;
                Current.Resources[AccentColorLevel4Resource] = new SolidColorBrush(accentColor);
            }

            // Register for back, if we haven't already.
            if (!m_hasRegisteredForBack)
            {
                m_hasRegisteredForBack = true;
                SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            }

            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                // When the navigation stack isn't restored navigate to the first page,
                // configuring the new page by passing required information as a navigation
                // parameter
                rootFrame.Navigate(typeof(MainPage), arguments);
            }
            else
            {
                // If we have already navigated, we should tell the main page
                // we are being activated again.
                if (rootFrame.Content.GetType() == typeof(MainPage))
                {
                    MainPage main = (MainPage)rootFrame.Content;
                    main.OnReActivated(arguments);
                }
            }

            // We have to get the screen res before we call activate or it will be wrong and include the system tray.
            var bounds = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().VisibleBounds;
            var scaleFactor = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
            BaconMan.BackgroundMan.ImageUpdaterMan.LastKnownScreenResoultion = new Size(bounds.Width * scaleFactor, bounds.Height * scaleFactor);

            // Ensure the current window is active
            Window.Current.Activate();
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending_Fired(object sender, SuspendingEventArgs e)
        {
            BaconMan.OnSuspending_Fired(sender, e);
        }

        /// <summary>
        /// Invoked when the app is resuming.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnResuming_Fired(object sender, object e)
        {
            BaconMan.OnResuming_Fired(sender, e);
        }

        /// <summary>
        /// Invoked when the back button is pressed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            bool isHandled = false;
            BaconMan.OnBackButton_Fired(ref isHandled);
            e.Handled = isHandled;
        }

        /// <summary>
        /// Fired when an exception is thrown.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }

            if (App.BaconMan.UiSettingsMan.Developer_StopFatalCrashesAndReport)
            {
                // Warning this will report the error but leave us in a very bad state. Only use this for debugging.
                e.Handled = true;
                BaconMan.MessageMan.ShowMessageSimple("Fatal Crash", "The app tried to crash. Message (" + e.Message + ")\n\n Exception Msg (" + e.Exception.Message + ")");
            }
        }
    }
}
