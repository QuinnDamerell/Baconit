using BaconBackend;
using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using BaconBackend.Managers;

namespace Baconit
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public sealed partial class App
    {
        public const string AccentColorLevel1Resource = "BaconitAccentColorLevel1Brush";
        public const string AccentColorLevel2Resource = "BaconitAccentColorLevel2Brush";
        public const string AccentColorLevel3Resource = "BaconitAccentColorLevel3Brush";
        public const string AccentColorLevel4Resource = "BaconitAccentColorLevel4Brush";

        // Usage baconit://debug?preventcrashes=true
        private const string CProtocolPreventCrashesEnabled = "preventcrashes=true";
        private const string CProtocolPreventCrashesDisabled = "preventcrashes=false";

        /// <summary>
        /// The main reference in the app to the backend of Baconit
        /// </summary>
        public static BaconManager BaconMan;

        /// <summary>
        /// Indicates if we have already registered for back.
        /// </summary>
        private bool _mHasRegisteredForBack;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {

            //bool result = Windows.UI.ViewManagement.ApplicationViewScaling.TrySetDisableLayoutScaling(true);

            // Setup the exception handler first
            UnhandledException += OnUnhandledException;
            CoreApplication.UnhandledErrorDetected += OnUnhandledErrorDetected;

            // Now setup the baconman
            BaconMan = new BaconManager(false);

            // Now telemetry
            Microsoft.ApplicationInsights.WindowsAppInitializer.InitializeAsync(
                Microsoft.ApplicationInsights.WindowsCollectors.Metadata |
                Microsoft.ApplicationInsights.WindowsCollectors.UnhandledException |
                Microsoft.ApplicationInsights.WindowsCollectors.Session);

            // Init the app
            InitializeComponent();

            // Register for events.
            Suspending += OnSuspending_Fired;
            Resuming += OnResuming_Fired;
        }

        /// <summary>
        /// Fired when the app is opened from a toast message.
        /// </summary>
        /// <param name="args"></param>
        protected override void OnActivated(IActivatedEventArgs args)
        {
            base.OnActivated(args);

            switch (args)
            {
                case ToastNotificationActivatedEventArgs eventArgs:
                {
                    var toastArgs = eventArgs;
                    SetupAndALaunchApp(toastArgs.Argument);
                    break;
                }
                case ProtocolActivatedEventArgs eventArgs:
                {
                    var protocolArgs = eventArgs;
                    var argsString = protocolArgs.Uri.OriginalString;
                    var portEnd = argsString.IndexOf("://", StringComparison.Ordinal);
                    argsString = portEnd == -1 ? argsString : argsString.Substring(portEnd + 3);
                    SetupAndALaunchApp(argsString);
                    break;
                }
                default:
                    SetupAndALaunchApp(string.Empty);
                    break;
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
            if(!string.IsNullOrWhiteSpace(arguments))
            {
                var lowerArgs = arguments.ToLower();
                if(lowerArgs.Contains(CProtocolPreventCrashesDisabled))
                {
                    BaconMan.UiSettingsMan.DeveloperStopFatalCrashesAndReport = false;
                }
                else if (lowerArgs.Contains(CProtocolPreventCrashesEnabled))
                {
                    BaconMan.UiSettingsMan.DeveloperStopFatalCrashesAndReport = true;
                }
            }

            // If we are on Xbox disable the blank border around the app. Ideally we would give the user the option to re-enable this.
            if(DeviceHelper.CurrentDevice() == DeviceTypes.Xbox)
            {
                Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetDesiredBoundsMode(Windows.UI.ViewManagement.ApplicationViewBoundsMode.UseCoreWindow);
            }

            // Grab the accent color and make our custom accent color brushes.
            if (!Current.Resources.ContainsKey(AccentColorLevel1Resource))
            {
                var accentColor = ((SolidColorBrush)Current.Resources["SystemControlBackgroundAccentBrush"]).Color;
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
            if (!_mHasRegisteredForBack)
            {
                _mHasRegisteredForBack = true;
                SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
            }

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (!(Window.Current.Content is Frame rootFrame))
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            switch (rootFrame.Content)
            {
                case null:
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    rootFrame.Navigate(typeof(MainPage), arguments);
                    break;
                // If we have already navigated, we should tell the main page
                // we are being activated again.
                case MainPage main:
                    main.OnReActivated(arguments);
                    break;
            }

            // We have to get the screen res before we call activate or it will be wrong and include the system tray.
            var bounds = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().VisibleBounds;
            var scaleFactor = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
            BaconMan.BackgroundMan.ImageUpdaterMan.LastKnownScreenResolution = new Size(bounds.Width * scaleFactor, bounds.Height * scaleFactor);

            // Ensure the current window is active
            Window.Current.Activate();
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        private static void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
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
        private static void OnSuspending_Fired(object sender, SuspendingEventArgs e)
        {
            BaconMan.OnSuspending_Fired(sender, e);
        }

        /// <summary>
        /// Invoked when the app is resuming.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void OnResuming_Fired(object sender, object e)
        {
            BaconMan.OnResuming_Fired(sender, e);
        }

        /// <summary>
        /// Invoked when the back button is pressed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            var isHandled = false;
            BaconMan.OnBackButton_Fired(ref isHandled);
            e.Handled = isHandled;
        }

        /// <summary>
        /// Fired when an exception is thrown.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }

            if (BaconMan.UiSettingsMan.DeveloperStopFatalCrashesAndReport)
            {
                // Warning this will report the error but leave us in a very bad state. Only use this for debugging.
                e.Handled = true;
                BaconMan.MessageMan.ShowMessageSimple("Fatal Crash", "The app tried to crash. \n\nMessage: " + e.Message + "\n\nException Msg: " + e.Exception.Message + "\n\nStack Trace:\n"+e.Exception.StackTrace);
            }
        }

        private static void OnUnhandledErrorDetected(object sender, UnhandledErrorDetectedEventArgs e)
        {
            try
            {
                e.UnhandledError.Propagate();
            }
            catch (Exception ex)
            {
                var properties = new Dictionary<string, string>
                {
                    { "StackTrace", ex.StackTrace },
                    { "Message", ex.Message },
                };
                TelemetryManager.TrackCrash(ex, properties);
            }
        }
    }
}
