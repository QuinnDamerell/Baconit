using BaconBackend.Helpers;
using BaconBackend.Managers;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
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

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.ContentPanels
{
    /// <summary>
    /// Args for toggle full screen
    /// </summary>
    public class OnToggleFullScreenEventArgs : EventArgs
    {
        public bool GoFullScreen { get; set; }
    }

    /// <summary>
    /// Args for OnContentLoadRequestArgs
    /// </summary>
    public class OnContentLoadRequestArgs : EventArgs
    {
        public string SourceId { get; set; }
    }


    public sealed partial class ContentPanelHost : UserControl, IContentPanelHost
    {
        /// <summary>
        /// The ID of the host, this must be unique per host.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Indicates if we are full screen.
        /// </summary>
        public bool IsFullscreen { get; private set; } = false;

        /// <summary>
        /// Holds the current panel if we have one.
        /// </summary>
        IContentPanelBase m_currentPanelBase = null;

        /// <summary>
        /// Indicates if we are showing loading.
        /// </summary>
        bool m_isLoadingShowing = false;

        /// <summary>
        /// Indicates if we are showing nsfw.
        /// </summary>
        bool m_isNsfwShowing = false;

        /// <summary>
        /// Indicates if the generic message is showing.
        /// </summary>
        bool m_isGenericMessageShowing = false;

        /// <summary>
        /// Indicates if we are showing the content load message.
        /// </summary>
        bool m_isShowingContentLoadBlock = false;

        /// <summary>
        /// Keeps track of when the host was created.
        /// </summary>
        DateTime m_hostCreationTime = DateTime.Now;

        /// <summary>
        /// This holds a list of all of the post where the block has been lowered.
        /// </summary>
        private static Dictionary<string, bool> s_previousLoweredNsfwBlocks = new Dictionary<string, bool>();

        /// <summary>
        /// This is a list of subreddits the block has been lowered for
        /// </summary>
        private static Dictionary<string, bool> s_previousLoweredNsfwSubreddits = new Dictionary<string, bool>();

        /// <summary>
        /// Fired when full screen is toggled.
        /// </summary>
        public event EventHandler<OnToggleFullScreenEventArgs> OnToggleFullscreen
        {
            add { m_onToggleFullscreen.Add(value); }
            remove { m_onToggleFullscreen.Remove(value); }
        }
        SmartWeakEvent<EventHandler<OnToggleFullScreenEventArgs>> m_onToggleFullscreen = new SmartWeakEvent<EventHandler<OnToggleFullScreenEventArgs>>();

        /// <summary>
        /// Fired when the user taps the content load request message.
        /// </summary>
        public event EventHandler<OnContentLoadRequestArgs> OnContentLoadRequest
        {
            add { m_onContentLoadRequest.Add(value); }
            remove { m_onContentLoadRequest.Remove(value); }
        }
        SmartWeakEvent<EventHandler<OnContentLoadRequestArgs>> m_onContentLoadRequest = new SmartWeakEvent<EventHandler<OnContentLoadRequestArgs>>();


        public ContentPanelHost()
        {
            this.InitializeComponent();

            // Note the time.
            m_hostCreationTime = DateTime.Now;

            // Do this now if we need to.
            ShowContentLoadBlockIfNeeded();
        }

        #region Panel Registration

        /// <summary>
        /// Registers this panel to get callbacks when the content is ready.
        /// </summary>
        /// <param name="id"></param>
        private async Task ResigerPanel(string sourceId)
        {
            // Create out unique id
            Id = sourceId + DateTime.Now.Ticks;

            if (!String.IsNullOrWhiteSpace(sourceId))
            {
                await Task.Run(() =>
                {
                    ContentPanelMaster.Current.RegisterForPanel(this, sourceId);
                });
            }

            // show content block if we need to.
            ShowContentLoadBlockIfNeeded();
        }

        /// <summary>
        /// Removes the panel from the master panel controller.
        /// </summary>
        private async Task UnRegisterPanel(string sourceId)
        {
            // Unregister
            if (!String.IsNullOrWhiteSpace(sourceId))
            {
                await Task.Run(() =>
                {
                    ContentPanelMaster.Current.UnRegisterForPanel(this, sourceId);
                });

                if(!String.IsNullOrWhiteSpace(SourceId) && sourceId.Equals(SourceId))
                {
                    SourceId = null;
                }
            }
        }

        #endregion

        #region Soruce Id Logic

        /// <summary>
        /// This it how we get the source form the xmal binding.
        /// </summary>
        public string SourceId
        {
            get { return (string)GetValue(FlipPostProperty); }
            set { SetValue(FlipPostProperty, value); }
        }

        public static readonly DependencyProperty FlipPostProperty =
            DependencyProperty.Register(
                "SourceId",
                typeof(string),
                typeof(ContentPanelHost),
                new PropertyMetadata(null, new PropertyChangedCallback(OnSoruceIdChangedStatic)));

        private static void OnSoruceIdChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as ContentPanelHost;
            if (instance != null)
            {
                // Send the post to the class.
                instance.OnSoruceIdChanged((string)e.OldValue, (string)e.NewValue);
            }
        }

        private async void OnSoruceIdChanged(string oldValue, string newId)
        {
            // Unregister
            if(!String.IsNullOrWhiteSpace(oldValue))
            {
                await UnRegisterPanel(oldValue);
            }

            // If we are full screen clear it.
            if(IsFullscreen)
            {
                OnFullscreenChanged(false);
            }

            // Register the new content
            if(!String.IsNullOrWhiteSpace(newId))
            {
                await ResigerPanel(newId);
            }
        }

        #endregion

        #region IContentPanelHost Callbacks

        /// <summary>
        /// Called when we have a panel.
        /// </summary>
        /// <param name="panel"></param>
        public void OnPanelAvailable(IContentPanelBase panelBase)
        {
            lock(this)
            {
                // Capture the panel
                m_currentPanelBase = panelBase;
            }

            // Setup loading.
            SetupLoading();

            // Setup error text
            SetupGenericMessage();

            // Setup NSFW
            SetupNsfwBlock();

            // Add the panel to the UI.
            UserControl userControl = (UserControl)panelBase.Panel;
            ui_contentRoot.Children.Add(userControl);
        }

        /// <summary>
        /// Fired when the current panel should be cleared.
        /// </summary>
        /// <param name="panel"></param>
        public void OnRemovePanel(IContentPanelBase panelBase)
        {
            lock(this)
            {
                // Release the panel
                m_currentPanelBase = null;
            }

            // Clear out the UI
            ui_contentRoot.Children.Clear();

            // Set loading, but disable the spinner.
            ToggleProgress(true, true);
        }

        /// <summary>
        /// Called when the content requested by this panel has begun loading.
        /// </summary>
        public void OnContentPreloading()
        {
            lock(this)
            {
                // If we don't have content yet show the loading screen.
                if(m_currentPanelBase == null)
                {
                    ui_contentRoot.Opacity = 0;
                    ToggleProgress(true);
                }
            }
        }

        /// <summary>
        /// Fired when we should toggle loading.
        /// </summary>
        /// <param name="show"></param>
        public void OnLoadingChanged()
        {
            IContentPanelBase panelBase = m_currentPanelBase;
            if(panelBase != null)
            {
                ToggleProgress(panelBase.IsLoading);
            }
        }

        /// <summary>
        /// Fired when we should toggle error
        /// </summary>
        /// <param name="show"></param>
        public void OnErrorChanged()
        {
            IContentPanelBase panelBase = m_currentPanelBase;
            if (panelBase != null)
            {
                ToggleGenericMessage(panelBase.HasError, panelBase.ErrorText);
            }
        }

        /// <summary>
        /// Fired when the panel has been unloaded.
        /// </summary>
        public void OnPanelUnloaded()
        {
            // Show an error message.
            ToggleGenericMessage(true, "Oh dear... this post is too big");
        }

        #endregion

        #region IsVisible Logic

        /// <summary>
        /// This it how we get the visibility from flip view
        /// </summary>
        public bool IsVisible
        {
            get { return (bool)GetValue(IsVisibleProperty); }
            set { SetValue(IsVisibleProperty, value); }
        }

        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register(
                "IsVisible",
                typeof(bool),
                typeof(ContentPanelHost),
                new PropertyMetadata(false, new PropertyChangedCallback(OnIsVisibleChangedStatic)));

        private static void OnIsVisibleChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as ContentPanelHost;
            if (instance != null)
            {
                instance.OnVisibleChanged((bool)e.NewValue);
            }
        }

        /// <summary>
        /// Fired when IsVisible changes. This indicates the post should play or show now.
        /// </summary>
        /// <param name="isVisible"></param>
        public async void OnVisibleChanged(bool isVisible)
        {
            if(isVisible)
            {
                // Confirm the NSFW block is correct. We need to do this again to check if the user has lowered
                // the nsfw block for this subreddit.
                SetNsfwBlock();
            }
            else
            {
                // If we are full screen clear it.
                if (IsFullscreen)
                {
                    OnFullscreenChanged(false);
                }
            }

            // Fire the event on the panel if we have one.
            IContentPanelBase panelBase = m_currentPanelBase;
            if(panelBase != null)
            {
                panelBase.OnVisibilityChanged(isVisible);
            }

            // We need to tell the master that our visibility has changed.
            // Sometimes we need to spin a bit because the source might not be set yet.
            int sanityCount = 0;
            while(SourceId == null && sanityCount < 50)
            {
                await Task.Delay(5);
                sanityCount++;
            }

            // Capture the source id.
            string sourceId = SourceId;
            if(sourceId != null)
            {
                await Task.Run(() =>
                {
                    ContentPanelMaster.Current.OnPanelVisibliltyChanged(sourceId, isVisible);
                });
            }
        }

        #endregion

        #region Loading

        /// <summary>
        /// Sets up loading for a new panel.
        /// </summary>
        private void SetupLoading()
        {
            IContentPanelBase panelBase = m_currentPanelBase;
            if (panelBase != null)
            {
                // If we are turning on loading make the content transparent.
                // Note is is important to not collapse, or the content might not load.
                if (panelBase.IsLoading)
                {
                    ui_contentRoot.Opacity = 0;
                }

                // Update the state of loading.
                ToggleProgress(panelBase.IsLoading);
            }
        }

        /// <summary>
        /// Fades in or out the progress UI.
        /// </summary>
        private void ToggleProgress(bool show, bool disableActive = false)
        {
            m_isLoadingShowing = show;

            if (show)
            {
                ui_progressRing.IsActive = !disableActive && show;
                VisualStateManager.GoToState(this, "ShowProgressHolder", true);
            }
            else
            {
                HideShowContentIfNeeded();

                // If we are trying to hide fast enough (the control was just made or the panel just added)
                // don't bother showing loading since it will cause a weird looking UI.
                TimeSpan timeSincePanelAdded = DateTime.Now - m_hostCreationTime;
                if (timeSincePanelAdded.TotalMilliseconds < 200)
                {
                    VisualStateManager.GoToState(this, "HideProgressHolderInstant", true);
                }
                else
                {
                    VisualStateManager.GoToState(this, "HideProgressHolder", true);
                }
            }
        }

        /// <summary>
        /// Fired when the progress holder is closed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HideProgressHolder_Completed(object sender, object e)
        {
            ui_progressRing.IsActive = false;
            m_isLoadingShowing = false;
        }

        #endregion

        #region NSFW block

        /// <summary>
        /// Prepares NSFW for the new panel.
        /// </summary>
        private void SetupNsfwBlock()
        {
            // If we showed the block hide the UI. Otherwise it will
            // show through when we animate out.
            // If we are hiding here we want it instant.

            if (SetNsfwBlock(true))
            {
                ui_contentRoot.Opacity = 0;
            }
        }

        /// <summary>
        /// Shows or hides the NSFW if it is needed.
        /// </summary>
        public bool SetNsfwBlock(bool instantOff = false)
        {
            IContentPanelBase panelBase = m_currentPanelBase;

            // If the post is over 18, and it hasn't been lowered, and we don't have block off, and we won't have per subreddit on and the subreddit has been lowered
            if (panelBase != null &&
                panelBase.Source.IsNSFW &&
                !s_previousLoweredNsfwBlocks.ContainsKey(panelBase.Source.Id) &&
                    (App.BaconMan.UiSettingsMan.FlipView_NsfwBlockingType == NsfwBlockType.Always ||
                        (App.BaconMan.UiSettingsMan.FlipView_NsfwBlockingType == NsfwBlockType.PerSubreddit &&
                         !String.IsNullOrWhiteSpace(panelBase.Source.Subreddit) &&
                         !s_previousLoweredNsfwSubreddits.ContainsKey(panelBase.Source.Subreddit))))
            {
                m_isNsfwShowing = true;
                VisualStateManager.GoToState(this, "ShowNsfwBlock", true);
                return true;
            }
            else
            {
                m_isNsfwShowing = false;

                if (instantOff)
                {
                    VisualStateManager.GoToState(this, "HideNsfwBlockInstant", true);
                }
                else
                {
                    VisualStateManager.GoToState(this, "HideNsfwBlock", true);
                }

                return false;
            }
        }

        /// <summary>
        /// Fired when the NSFW block is tapped.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NsfwBlockRoot_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Set our state.
            m_isNsfwShowing = false;

            // Show the content if we can
            HideShowContentIfNeeded();

            // Animate out the NSFW block.
            VisualStateManager.GoToState(this, "HideNsfwBlock", true);

            // Add the id so we don't block again.
            IContentPanelBase panelBase = m_currentPanelBase;
            if (panelBase == null)
            {
                return;
            }
            if (!String.IsNullOrWhiteSpace(panelBase.Source.Id))
            {
                // Add to our list so we won't block again.
                if (!s_previousLoweredNsfwBlocks.ContainsKey(panelBase.Source.Id))
                {
                    s_previousLoweredNsfwBlocks.Add(panelBase.Source.Id, true);
                }
            }
            if (!String.IsNullOrWhiteSpace(panelBase.Source.Subreddit))
            {
                // Add to our list so we won't block again.
                if (!s_previousLoweredNsfwSubreddits.ContainsKey(panelBase.Source.Subreddit))
                {
                    s_previousLoweredNsfwSubreddits.Add(panelBase.Source.Subreddit, true);
                }
                return;
            }
        }

        #endregion

        #region Full screen Logic

        /// <summary>
        /// This it how we get the visibility from flip view
        /// </summary>
        public bool CanGoFullscreen
        {
            get { return (bool)GetValue(CanGoFullScreenProperty); }
            set { SetValue(CanGoFullScreenProperty, value); }
        }

        public static readonly DependencyProperty CanGoFullScreenProperty =
            DependencyProperty.Register(
                "CanGoFullScreen",
                typeof(bool),
                typeof(ContentPanelHost),
                new PropertyMetadata(true, new PropertyChangedCallback(OnCanGoFullScreenChangedStatic)
                ));

        private static void OnCanGoFullScreenChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Do nothing.
        }

        public bool OnFullscreenChanged(bool goFullscreen)
        {
            // If we are disabled don't let them.
            if (!CanGoFullscreen)
            {
                return false;
            }

            // Ignore if we are already in this state
            if (goFullscreen == IsFullscreen)
            {
                return false;
            }
            IsFullscreen = goFullscreen;

            // Set the manipulation mode to steal all of the touch events
            ManipulationMode = goFullscreen ? ManipulationModes.All : ManipulationModes.System;

            // Fire the event
            OnToggleFullScreenEventArgs args = new OnToggleFullScreenEventArgs()
            {
                GoFullScreen = goFullscreen
            };
            m_onToggleFullscreen.Raise(this, args);
            return true;
        }

        #endregion

        #region Generic message UI

        /// <summary>
        /// Sets up the generic message when we are loading.
        /// </summary>
        private void SetupGenericMessage()
        {
            bool setError = false;
            IContentPanelBase panelBase = m_currentPanelBase;
            if (panelBase != null)
            {
                // If we are turning on collapse the content.
                // Note is is important to not collapse, or the content might not load.
                if (panelBase.HasError)
                {
                    ui_contentRoot.Opacity = 0;
                }

                // Update the state of the UI. If we are hiding here we want it instant.
                ToggleGenericMessage(panelBase.HasError, panelBase.ErrorText, null, true);
                setError = panelBase.HasError;
            }
        }

        /// <summary>
        /// Called if we should show the content block.
        /// </summary>
        private bool ShowContentLoadBlockIfNeeded()
        {
            // If we are blocking content show the request block.
            m_isShowingContentLoadBlock = !App.BaconMan.UiSettingsMan.FlipView_LoadPostContentWithoutAction;
            if (m_isShowingContentLoadBlock)
            {
                // Show the message
                ToggleGenericMessage(true, "Tap anywhere to load content", "You can change this behavior in the settings", true);

                // Hide loading.
                ToggleProgress(false, true);
            }
            return m_isShowingContentLoadBlock;
        }

        /// <summary>
        /// Fired when we should toggle the generic message.
        /// </summary>
        /// <param name="show"></param>
        /// <param name="message"></param>
        private void ToggleGenericMessage(bool show, string message = null, string subMessage = null, bool instantOff = false)
        {
            m_isGenericMessageShowing = show;

            // Set the text
            if (show)
            {
                ui_genericTextHeader.Text = message == null ? "Error loading post" : message;
                ui_genericTextSub.Text = subMessage == null ? "Tap anywhere to open in Edge" : subMessage;
            }

            if (show)
            {
                VisualStateManager.GoToState(this, "ShowGenericMessage", true);
            }
            else
            {
                HideShowContentIfNeeded();

                if (instantOff)
                {
                    VisualStateManager.GoToState(this, "HideGenericMessageInstant", true);
                }
                else
                {
                    VisualStateManager.GoToState(this, "HideGenericMessage", true);
                }
            }
        }

        /// <summary>
        /// Fired when the generic message UI is tapped.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void GenericMessage_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // If we are showing the content load fire it.
            if (m_isShowingContentLoadBlock)
            {
                OnContentLoadRequestArgs args = new OnContentLoadRequestArgs()
                {
                    SourceId = SourceId
                };
                m_onContentLoadRequest.Raise(this, args);

                // Set bars
                m_isShowingContentLoadBlock = false;
                ToggleGenericMessage(false);

                // Show loading
                ToggleProgress(true);
            }
            else
            {
                try
                {
                    // Get the url, when we are unloaded we will not have a panel base
                    // thus we have to get the source from the master.
                    string url;
                    if (m_currentPanelBase != null)
                    {
                        url = m_currentPanelBase.Source.Url;
                    }
                    else
                    {
                        // This could be null (but never should be), but if so we are fucked anyways.
                        url = ContentPanelMaster.Current.GetSource(SourceId).Url;
                    }

                    await Windows.System.Launcher.LaunchUriAsync(new Uri(url, UriKind.Absolute));
                }
                catch (Exception)
                { }
            }
        }

        #endregion

        /// <summary>
        /// Shows the content root if there is nothing blocking it.
        /// </summary>
        private void HideShowContentIfNeeded()
        {
            if(!m_isNsfwShowing && !m_isLoadingShowing && !m_isGenericMessageShowing)
            {
                ui_contentRoot.Opacity = 1;
            }
        }
    }
}
