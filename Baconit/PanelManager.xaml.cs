using BaconBackend.Helpers;
using Baconit.Interfaces;
using Baconit.Panels;
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
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit
{
    /// <summary>
    /// Defines the screen mode we are currently in
    /// </summary>
    public enum ScreenMode
    {
        Split,
        Single,
        FullScreen
    }

    public class OnScreenModeChangedArgs : EventArgs
    {
        public ScreenMode NewScreenMode;
    }

    public sealed partial class PanelManager : UserControl, IPanelHost
    {
        /// <summary>
        /// Common arguments that are used for some panels
        /// </summary>
        public const string NAV_ARGS_SUBREDDIT_NAME = "Pane.SubredditName";
        public const string NAV_ARGS_SUBREDDIT_SORT = "Pane.SubredditSort";
        public const string NAV_ARGS_SUBREDDIT_SORT_TIME = "Pane.SubredditSortTime";
        public const string NAV_ARGS_POST_ID = "Pane.PostId";
        public const string NAV_ARGS_FORCE_POST_ID = "Pane.ForcePostId";
        public const string NAV_ARGS_FORCE_COMMENT_ID = "Pane.ForceCommentId";
        public const string NAV_ARGS_SEARCH_QUERY = "Pane.SearchQuery";
        public const string NAV_ARGS_SEARCH_SUBREDDIT_NAME = "Pane.SearchSubredditName";
        public const string NAV_ARGS_SUBMIT_POST_SUBREDDIT = "Pane.SubmitPostSubreddit";
        public const string NAV_ARGS_USER_NAME = "Pane.UserName";

        /// <summary>
        /// Fired when the screen mode changes
        /// </summary>
        public event EventHandler<OnScreenModeChangedArgs> OnScreenModeChanged
        {
            add { m_onScreenModeChanged.Add(value); }
            remove { m_onScreenModeChanged.Remove(value); }
        }
        SmartWeakEvent<EventHandler<OnScreenModeChangedArgs>> m_onScreenModeChanged = new SmartWeakEvent<EventHandler<OnScreenModeChangedArgs>>();

        /// <summary>
        /// Fired when the navigation is complete
        /// </summary>
        public event EventHandler<EventArgs> OnNavigationComplete
        {
            add { m_onNavigationComplete.Add(value); }
            remove { m_onNavigationComplete.Remove(value); }
        }
        SmartWeakEvent<EventHandler<EventArgs>> m_onNavigationComplete = new SmartWeakEvent<EventHandler<EventArgs>>();

        //
        // Private Vars
        //
        private const int MAX_PANEL_SIZE = 400;

        private enum State
        {
            Idle,
            FadingIn,
            FadingOut,
        }

        private enum PanelType
        {
            None,
            SubredditList,
            ContentPanel
        }

        private class StackItem
        {
            public IPanel Panel;
            public string Id;
        }

        /// <summary>
        /// The current state of the panel manager
        /// </summary>
        State m_state = State.Idle;

        /// <summary>
        /// The current state we are running in.
        /// </summary>
        ScreenMode m_screenMode = ScreenMode.Split;

        /// <summary>
        /// Main reference back to the host
        /// </summary>
        IMainPage m_mainPage;

        /// <summary>
        /// The list of previous panels.
        /// </summary>
        List<StackItem> m_panelStack = new List<StackItem>();

        /// <summary>
        /// Indicates if we are pending a screen size update
        /// </summary>
        ScreenMode? m_deferedScreenUpdate = null;

        /// <summary>
        /// On the last back button press before we leave the app we want to
        /// show the menu. This bool indicates if we have done that or not.
        /// </summary>
        bool m_finalNavigateHasShownMenu = false;

        public PanelManager(IMainPage main, IPanel startingPanel = null)
        {
            this.InitializeComponent();

            // Create
            m_mainPage = main;

            // Set the initial panel size size
            OnScreenSizeChanged((int)Window.Current.Bounds.Width, true);
            Window.Current.SizeChanged += Windows_SizeChanged;

            // Set the starting panel
            if (startingPanel != null)
            {
                startingPanel.PanelSetup(this, new Dictionary<string, object>());
                FireOnNavigateTo(startingPanel);
                m_panelStack.Add(new StackItem() { Panel = startingPanel, Id = "StartingPanel" });
                ui_contentRoot.Children.Add((UserControl)startingPanel);
            }

            // Set the back button
            UpdateBackButton();

            // Register for app suspend commands
            App.BaconMan.OnSuspending += OnSuspending;
            App.BaconMan.OnResuming += OnResuming;
        }

        #region Suspend and Resume

        /// <summary>
        /// Fired when the app is resuming.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnResuming(object sender, object e)
        {
            FireOnSuspendOrResumeEvents(false);
        }

        /// <summary>
        /// Fired when the app is suspending
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSuspending(object sender, EventArgs e)
        {
            FireOnSuspendOrResumeEvents(true);
        }

        /// <summary>
        /// Fires the events to the correct panels when suspending or resuming.
        /// </summary>
        /// <param name="isSuspend"></param>
        private void FireOnSuspendOrResumeEvents(bool isSuspend)
        {
            IPanel subredditPanel = null;
            IPanel contentPanel = null;

            lock (m_panelStack)
            {
                if (m_screenMode == ScreenMode.Single)
                {
                    if (m_panelStack.Count > 0)
                    {
                        // It doesn't really matter which we use
                        subredditPanel = m_panelStack.Last().Panel;
                    }
                }
                else
                {
                    // Find the most recent sub and content
                    foreach (StackItem item in m_panelStack.Reverse<StackItem>())
                    {
                        IPanel panel = item.Panel;
                        // If the are both subreddit panels or both not, we found the panel.
                        if (GetPanelType(panel) == PanelType.ContentPanel)
                        {
                            if (contentPanel == null)
                            {
                                contentPanel = panel;
                            }
                        }
                        else
                        {
                            if (subredditPanel == null)
                            {
                                subredditPanel = panel;
                            }
                        }

                        if (contentPanel != null && subredditPanel != null)
                        {
                            break;
                        }
                    }
                }
            }

            if (subredditPanel != null)
            {
                if(isSuspend)
                {
                    FireOnNavigateFrom(subredditPanel);
                }
                else
                {
                    FireOnNavigateTo(subredditPanel);
                }
            }
            if (contentPanel != null)
            {
                if (isSuspend)
                {
                    FireOnNavigateFrom(contentPanel);
                }
                else
                {
                    FireOnNavigateTo(contentPanel);
                }
            }
        }

        #endregion

        #region Navigation Logic

        /// <summary>
        /// Indicates if it is possible to go back.
        /// </summary>
        /// <returns></returns>
        public bool CanGoBack()
        {
            // Why is it 2?!!? Because when we first load we add a welcome panel
            // and a subreddit. We never want to navigate them out.
            return m_panelStack.Count > 2;
        }

        /// <summary>
        /// Navigates back to the previous page
        /// </summary>
        private bool GoBack_Internal()
        {
            IPanel leavingPanel = null;
            lock(m_panelStack)
            {
                if(m_state != State.Idle)
                {
                    // We can't do anything if we are already animating.
                    return false;
                }

                if(m_panelStack.Count <= 1)
                {
                    // We can't go back, there is nothing to go back to.
                    return false;
                }

                // Get the panel we are removing.
                leavingPanel = m_panelStack.Last().Panel;

                // Remove the panel
                m_panelStack.Remove(m_panelStack.Last());

                // Report the new panel being shown.
                App.BaconMan.TelemetryMan.ReportPageView(m_panelStack.Last().Panel.GetType().Name);

                // Get the type of the leaving panel
                PanelType leavingPanelType = GetPanelType(leavingPanel);

                // Start to fade out the current panel.
                PlayFadeAnimation(leavingPanelType, leavingPanelType, State.FadingOut);
            }

            // While not under lock inform the panel it is leaving.
            FireOnNavigateFrom(leavingPanel);

            return true;
        }

        /// <summary>
        /// Fired when the user presses the back button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public bool GoBack()
        {
            bool handled = false;

            // If we can go back, do it.
            if(CanGoBack())
            {
                // Call go back but this might not work, we can't go back while something else is navigating.
                // If we can't go back right now just silently ignore the request.
                handled = GoBack_Internal();
            }
            
            if(!handled)
            {
                // If we can't go back anymore for the last back show the menu.
                // After that let the user leave.
                if(!m_finalNavigateHasShownMenu)
                {
                    m_finalNavigateHasShownMenu = true;
                    handled = true;
                    ToggleMenu(true);
                }
            }

            return handled;
        }

        /// <summary>
        /// Navigates to a panel. If a panel already exist with the same panelId instead or creating a new
        /// panel the old panel will be shown and passed the new arguments.
        /// </summary>
        /// <param name="panelType">The type of panel to be created</param>
        /// <param name="panelId">A unique identifier for the panel, the id should be able to differeincae between two panels of the same type</param>
        /// <param name="arguments">Arguments to be sent to the panel</param>
        /// <returns></returns>
        public bool Navigate(Type panelType, string panelId, Dictionary<string, object> arguments = null)
        {
            if (panelType == null)
            {
                throw new Exception("Panel type can't be null!");
            }

            // Make sure we send args along.
            if (arguments == null)
            {
                arguments = new Dictionary<string, object>();
            }

            // Clear this value
            m_finalNavigateHasShownMenu = false;

            // Report to telemetry
            //App.TelemetryClient.TrackPageView(panelType.Name);

            bool isExitingPanel = false;
            StackItem navigateFromPanel = null;
            lock (m_panelStack)
            {
                // For now we can only do one animation at a time. So if we are doing something we
                // must ignore this
                if (m_state != State.Idle)
                {
                    return false;
                }

                // The panel we will navigate to
                StackItem navigateToPanel = null;

                // First, check to see if we already have the panel
                foreach (StackItem item in m_panelStack)
                {
                    if(item.Panel.GetType() == panelType && item.Id == panelId)
                    {
                        // We found it.
                        isExitingPanel = true;
                        navigateToPanel = item;
                        break;
                    }
                }

                // If we didn't find it make a new panel.
                if(navigateToPanel == null)
                {
                    navigateToPanel = new StackItem();
                    navigateToPanel.Panel = (IPanel)Activator.CreateInstance(panelType);
                    navigateToPanel.Id = panelId;
                }

                // Check the type
                PanelType newPanelType = GetPanelType(navigateToPanel.Panel);

                // Second, Figure out what panel will be leaving.
                if (m_screenMode == ScreenMode.Single)
                {
                    // If we are in single mode it will be the panel from the top of the stack
                    if (m_panelStack.Count > 0)
                    {
                        navigateFromPanel = m_panelStack.Last();
                    }
                }
                else
                {
                    // If we are in split mode it will be the panel we will replace.
                    // So go through the list backwards and find it.
                    foreach (StackItem item in m_panelStack.Reverse<StackItem>())
                    {
                        // If the are both subreddit panels or both not, we found the panel.
                        if (GetPanelType(item.Panel) == newPanelType)
                        {
                            navigateFromPanel = item;
                            break;
                        }
                    }
                }

                // We need to type of the leaving panel. If the panel is null this
                // is the first navigation of a type, so we will just call it the content panel.
                PanelType navigateFromPanelType = PanelType.ContentPanel;
                if (navigateFromPanel != null)
                {
                    navigateFromPanelType = GetPanelType(navigateFromPanel.Panel);
                }

                // Third, Setup the panel. If it already exits call activate instead of setup.
                if (isExitingPanel)
                {
                    navigateToPanel.Panel.OnPanelPulledToTop(arguments);
                }
                else
                {
                    navigateToPanel.Panel.PanelSetup(this, arguments);
                }

                // Special case! If the UI is already shown then we should just return here.
                if(navigateFromPanel != null && navigateFromPanel.Panel.GetType() == navigateToPanel.Panel.GetType() && navigateFromPanel.Id == navigateToPanel.Id)
                {
                    return true;
                }

                // Forth, if the panel exist remove it from the stack
                if(isExitingPanel)
                {
                    m_panelStack.Remove(navigateToPanel);
                }

                // Last, add the panel to the bottom of the list.
                m_panelStack.Add(navigateToPanel);

                // Report the view
                App.BaconMan.TelemetryMan.ReportPageView(navigateToPanel.Panel.GetType().Name);

                // Animate the current panel out
                PlayFadeAnimation(newPanelType, navigateFromPanelType, State.FadingOut);
            }

            // If we have a panel tell it we are leaving no under lock.
            if (navigateFromPanel != null)
            {
                FireOnNavigateFrom(navigateFromPanel.Panel);
            }

            return true;
        }

        #endregion

        #region Animation

        /// <summary>
        /// Fired when the storyboard animation has finished.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PanelAnimation_Completed(object sender, object e)
        {
            PanelType newPanelType = PanelType.None;
            IPanel newPanel = null;
            bool fireNavCompleteAndReturn = false;

            // Grab a lock
            lock (m_panelStack)
            {
                // Check the state
                if (m_state == State.Idle)
                {
                    return;
                }
                else if (m_state == State.FadingIn)
                {
                    // We are done with the fade in, go back to idle
                    m_state = State.Idle;

                    // Update the back button
                    UpdateBackButton();

                    fireNavCompleteAndReturn = true;

                    // If we are pending a screen change do it now.
                    if (m_deferedScreenUpdate.HasValue)
                    {
                        ExecuteOnScreenSizeChagnedLogic(m_deferedScreenUpdate.Value);
                        m_deferedScreenUpdate = null;
                    }
                }
                else
                {
                    // State.FadingOut

                    if (m_screenMode == ScreenMode.Single)
                    {
                        // If we are in single mode we want to animate in whatever is on the top of the stack
                        newPanel = m_panelStack.Last().Panel;
                    }
                    else
                    {
                        // If we are in split mode we want to animate in whatever is the most recent panel for this space.
                        // First figure out what content space this came from
                        PanelType animationPanelType = (sender as DoubleAnimation).Equals(ui_animSubList) ? PanelType.SubredditList : PanelType.ContentPanel;

                        // Now find the most recent panel for content space.
                        foreach (StackItem item in m_panelStack.Reverse<StackItem>())
                        {
                            IPanel panel = item.Panel;
                            // If the are both subreddit panels or both not, we found the panel.
                            if (GetPanelType(panel) == animationPanelType)
                            {
                                newPanel = panel;
                                break;
                            }
                        }

                        // If newPanel type is null we don't have anything and we should clean up and leave.
                        if (newPanel == null)
                        {
                            // Set the state
                            m_state = State.Idle;

                            // Get the root
                            Grid paneRoot = animationPanelType == PanelType.ContentPanel ? ui_contentRoot : ui_subListRoot;
                            paneRoot.Children.Clear();

                            // Update the back button
                            UpdateBackButton();

                            fireNavCompleteAndReturn = true;
                        }
                    }

                    if (!fireNavCompleteAndReturn)
                    {
                        // Get the panel state of the top panel, this is the one we want to animate back in
                        newPanelType = GetPanelType(newPanel);

                        // Get the correct root
                        Grid root = newPanelType == PanelType.ContentPanel ? ui_contentRoot : ui_subListRoot;

                        // Take clear the current panel
                        root.Children.Clear();
                        root.Children.Add((UserControl)newPanel);

                        // Update the panel sizes
                        UpdatePanelSizes();
                    }
                }               
            }

            // Do this if needed and get out of here.
            if(fireNavCompleteAndReturn)
            {
                FireOnNavigateComplete();
                return;
            }

            // Inform the panel we are navigating to it.
            FireOnNavigateTo(newPanel);

            lock (m_panelStack)
            {
                // Play the animation, note the second type doesn't matter for fade in.
                PlayFadeAnimation(newPanelType, PanelType.None, State.FadingIn);
            }
        }

        /// <summary>
        /// Fades in our out the current panel
        /// </summary>
        /// <param name="newState"></param>
        private void PlayFadeAnimation(PanelType newPanelType, PanelType lastPanelType, State newState)
        {
            PanelType panelToFade = PanelType.None;

            if (newState == State.FadingIn)
            {
                // If we are fading in we know which panel we want to fade in.
                panelToFade = newPanelType;
            }
            else
            {
                // Figure out what panel to fade out, if we are in single mode we want to fade out
                // whatever was on the screen last.
                if (m_screenMode == ScreenMode.Single)
                {
                    panelToFade = lastPanelType;
                }
                // If we are in split mode we want to fade out which ever panel we are replacing.
                else
                {
                    panelToFade = newPanelType;
                }
            }

            // Get the correct vars
            Storyboard story = panelToFade == PanelType.SubredditList ? ui_storySubList : ui_storyContent;
            DoubleAnimation anim = panelToFade == PanelType.SubredditList ? ui_animSubList : ui_animContent;
            Grid root = panelToFade == PanelType.SubredditList ? ui_subListRoot : ui_contentRoot;

            // Setup
            anim.To = newState == State.FadingIn ? 1 : 0;
            /// #todo, use the current opacity in the animation.
            anim.From = newState == State.FadingIn ? 0 : 1;
            /// #todo make this better, why do i have set set opacity?
            root.Opacity = newState == State.FadingIn ? 0 : 1;

            // Stop any existing animation
            story.Stop();

            // Set the new state
            m_state = newState;

            // Start a new one
            story.Begin();
        }

        #endregion

        #region Screen Size Changed

        /// <summary>
        /// Fired when the window size changes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Windows_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            OnScreenSizeChanged((int)e.Size.Width);
        }

        private void OnScreenSizeChanged(int newSize, bool forceSet = false, bool? toggleFullScreen = null)
        {
            // Figure what mode we should be in.
            ScreenMode newMode = newSize > (MAX_PANEL_SIZE *2) ? ScreenMode.Split : ScreenMode.Single;

            // Enter full screen if we should.
            if(toggleFullScreen.HasValue && toggleFullScreen.Value)
            {
                newMode = ScreenMode.FullScreen;
            }

            // If we are in full screen...
            if(m_screenMode == ScreenMode.FullScreen)
            {
                // and we don't have a value for toggle or the value it true...
                if(!toggleFullScreen.HasValue || toggleFullScreen.Value)
                {
                    // Make sure we stay in full screen.
                    newMode = ScreenMode.FullScreen;
                }
            }
            
            if (newMode != m_screenMode || forceSet)
            {
                // If we are animating we can't update. So set the deferral and it will
                // be taken care of when the animation is done.
                lock (m_panelStack)
                {
                    if (m_state != State.Idle)
                    {
                        m_deferedScreenUpdate = newMode;
                        return;
                    }

                    // Otherwise do the logic
                    ExecuteOnScreenSizeChagnedLogic(newMode);
                }
            }
        }

        /// <summary>
        /// Does the screen size changed logic, must be called under lock.
        /// </summary>
        /// <param name="newMode"></param>
        private void ExecuteOnScreenSizeChagnedLogic(ScreenMode newMode)
        {
            // Set the mode
            m_screenMode = newMode;

            // Update the panel sizes.
            UpdatePanelSizes();

            // We either showed a window or hide one, we need to tell that window.
            // If we are full screen don't tell anyone anything, the world will be restored
            // when we leave full screen.
            if(m_panelStack.Count > 1)
            {
                PanelType topPanelType = GetPanelType(m_panelStack.Last().Panel);

                IPanel mostRecentOtherPanel = null;
                // Find the most recent panel of a different type.
                foreach (StackItem item in m_panelStack.Reverse<StackItem>())
                {
                    IPanel panel = item.Panel;
                    // If the are both subreddit panels or both not, we found the panel.
                    if (GetPanelType(panel) != topPanelType)
                    {
                        mostRecentOtherPanel = panel;
                        break;
                    }
                }

                // Make sure we got one.
                if(mostRecentOtherPanel != null)
                {
                    if(m_screenMode == ScreenMode.Single)
                    {
                        // We hide it
                        FireOnNavigateFrom(mostRecentOtherPanel);
                    }
                    else
                    {
                        // We showed it
                        FireOnNavigateTo(mostRecentOtherPanel);
                    }
                }
            }

            FireOnScreenModeChanged();
        }

        /// <summary>
        /// Updates the panel sizes so they are correct for what's showing. Note, this needs
        /// to be called under lock.
        /// </summary>
        private void UpdatePanelSizes()
        {
            // Set the size of the panel. If we are in single mode we want to set the max size
            // to show or hide the panel, if we are in split mode it should always be static.
            if (m_screenMode == ScreenMode.Split)
            {
                // Set the sub list column to auto with a max width
                ui_subListColumnDef.Width = GridLength.Auto;
                ui_subListRoot.Width = MAX_PANEL_SIZE;

                // Set the content to * to fill the rest
                ui_contentColumnDef.Width = new GridLength(999999, GridUnitType.Star);

                // Set the visibility and opacity
                ui_subListRoot.Visibility = Visibility.Visible;
                ui_contentRoot.Visibility = Visibility.Visible;
                ui_subListRoot.Opacity = 1;
                ui_contentRoot.Opacity = 1;
            }
            else
            {
                // Get the type of the top object
                PanelType currentType = m_panelStack.Count == 0 ? PanelType.SubredditList : GetPanelType(m_panelStack.Last().Panel);

                // Reset the with of the sub list
                ui_subListRoot.Width = double.NaN;

                // We the column widths
                ui_subListColumnDef.Width = currentType == PanelType.SubredditList ? new GridLength(999999, GridUnitType.Star) : new GridLength(0);
                ui_contentColumnDef.Width = currentType == PanelType.ContentPanel ? new GridLength(999999, GridUnitType.Star) : new GridLength(0);

                // Set the visibility
                ui_subListRoot.Visibility = currentType == PanelType.SubredditList ? Visibility.Visible : Visibility.Collapsed;
                ui_contentRoot.Visibility = currentType == PanelType.ContentPanel ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Fires on screen mode changed.
        /// </summary>
        private void FireOnScreenModeChanged()
        {
            try
            {
                m_onScreenModeChanged.Raise(this, new OnScreenModeChangedArgs() { NewScreenMode = m_screenMode });
            }
            catch(Exception e)
            {
                App.BaconMan.MessageMan.DebugDia("Exception in FireOnScreenModeChanged", e);
            }
        }

        /// <summary>
        /// Returns the current screen mode
        /// </summary>
        /// <returns></returns>
        public ScreenMode CurrentScreenMode()
        {
            return m_screenMode;
        }

        #endregion

        #region Full Screen Logic

        /// <summary>
        /// Enters or exits full screen mode
        /// </summary>
        /// <param name="goFullScreen"></param>
        public void ToggleFullScreen(bool goFullScreen)
        {
            // #todo check locking
            // #todo a lot more logic here
            throw new Exception("This logic insn't done yet. Don't call this.");
            //OnScreenSizeChanged((int)Window.Current.Bounds.Width, false, goFullScreen);
        }

        #endregion

        /// <summary>
        /// Toggles the main side menu
        /// </summary>
        /// <param name="show"></param>
        public void ToggleMenu(bool show)
        {
            m_mainPage.ToggleMenu(show);
        }

        /// <summary>
        /// Returns the type of panel this is.
        /// </summary>
        /// <param name="panel"></param>
        /// <returns></returns>
        private PanelType GetPanelType(IPanel panel)
        {
            return (panel as SubredditPanel) == null ? PanelType.ContentPanel : PanelType.SubredditList;
        }

        /// <summary>
        /// Updates the current state of the back button
        /// </summary>
        private void UpdateBackButton()
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = CanGoBack() ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;
        }

        /// <summary>
        /// Fires OnNavigationComplete
        /// </summary>
        /// <param name="panel"></param>
        private void FireOnNavigateComplete()
        {
            try
            {
                m_onNavigationComplete.Raise(this, new EventArgs());
            }
            catch (Exception e)
            {
                App.BaconMan.MessageMan.DebugDia("OnNavigationComplete failed!", e);
            }
        }

        /// <summary>
        /// Fires OnNavigateFrom for the panel
        /// </summary>
        /// <param name="panel"></param>
        private void FireOnNavigateFrom(IPanel panel)
        {
            try
            {
                // Tell the panel it is leaving
                panel.OnNavigatingFrom();
            }
            catch (Exception e)
            {
                App.BaconMan.MessageMan.DebugDia("OnNavigatingFrom failed!", e);
            }
        }

        /// <summary>
        /// Fires OnNavigateTo for the panel
        /// </summary>
        /// <param name="panel"></param>
        private void FireOnNavigateTo(IPanel panel)
        {
            try
            {
                // Tell the panel it is prime time!
                panel.OnNavigatingTo();
            }
            catch (Exception e)
            {
                App.BaconMan.MessageMan.DebugDia("OnNavigatingTo failed!", e);
            }
        }

        /// <summary>
        /// Sets the status bar color for mobile.
        /// </summary>
        /// <param name="color"></param>
        public async Task<double> SetStatusBar(Color? color = null, double opacity = 1)
        { 
            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                StatusBar statusbar = StatusBar.GetForCurrentView();
                if (statusbar != null)
                {
                    if (color.HasValue)
                    {
                        statusbar.BackgroundColor = color.Value;
                    }                                      
                    statusbar.BackgroundOpacity = opacity;
                    await statusbar.ShowAsync();
                    return statusbar.OccludedRect.Height;
                }          
            }
            return 0;
        }
    }
}
