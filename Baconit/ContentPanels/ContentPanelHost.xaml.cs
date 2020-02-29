using BaconBackend.Helpers;
using BaconBackend.Managers;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Baconit.ContentPanels
{
    /// <summary>
    /// Args for toggle full screen
    /// </summary>
    public class ToggleFullScreenEventArgs : EventArgs
    {
        public bool GoFullScreen { get; set; }
    }

    /// <summary>
    /// Args for OnContentLoadRequestArgs
    /// </summary>
    public class ContentLoadRequestArgs : EventArgs
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
        public bool IsFullscreen { get; private set; }

        /// <summary>
        /// Holds the current panel if we have one.
        /// </summary>
        private IContentPanelBase _mCurrentPanelBase;

        /// <summary>
        /// Indicates if we are showing loading.
        /// </summary>
        private bool _mIsLoadingShowing;

        /// <summary>
        /// Indicates if we are showing nsfw.
        /// </summary>
        private bool _mIsNsfwShowing;

        /// <summary>
        /// Indicates if the generic message is showing.
        /// </summary>
        private bool _mIsGenericMessageShowing;

        /// <summary>
        /// Indicates if we are showing the content load message.
        /// </summary>
        private bool _mIsShowingContentLoadBlock;

        /// <summary>
        /// Keeps track of when the host was created.
        /// </summary>
        private readonly DateTime _mHostCreationTime = DateTime.Now;

        /// <summary>
        /// This holds a list of all of the post where the block has been lowered.
        /// </summary>
        private static readonly Dictionary<string, bool> PreviousLoweredNsfwBlocks = new Dictionary<string, bool>();

        /// <summary>
        /// This is a list of subreddits the block has been lowered for
        /// </summary>
        private static readonly Dictionary<string, bool> PreviousLoweredNsfwSubreddits = new Dictionary<string, bool>();

        /// <summary>
        /// Fired when full screen is toggled.
        /// </summary>
        public event EventHandler<ToggleFullScreenEventArgs> OnToggleFullscreen
        {
            add => _mOnToggleFullscreen.Add(value);
            remove => _mOnToggleFullscreen.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<ToggleFullScreenEventArgs>> _mOnToggleFullscreen = new SmartWeakEvent<EventHandler<ToggleFullScreenEventArgs>>();

        /// <summary>
        /// Fired when the user taps the content load request message.
        /// </summary>
        public event EventHandler<ContentLoadRequestArgs> OnContentLoadRequest
        {
            add => _mOnContentLoadRequest.Add(value);
            remove => _mOnContentLoadRequest.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<ContentLoadRequestArgs>> _mOnContentLoadRequest = new SmartWeakEvent<EventHandler<ContentLoadRequestArgs>>();


        public ContentPanelHost()
        {
            InitializeComponent();

            // Note the time.
            _mHostCreationTime = DateTime.Now;

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

            if (!string.IsNullOrWhiteSpace(sourceId))
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
            if (!string.IsNullOrWhiteSpace(sourceId))
            {
                await Task.Run(() =>
                {
                    ContentPanelMaster.Current.UnRegisterForPanel(this, sourceId);
                });

                if(!string.IsNullOrWhiteSpace(SourceId) && sourceId.Equals(SourceId))
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
            get => (string)GetValue(FlipPostProperty);
            set => SetValue(FlipPostProperty, value);
        }

        public static readonly DependencyProperty FlipPostProperty =
            DependencyProperty.Register(
                "SourceId",
                typeof(string),
                typeof(ContentPanelHost),
                new PropertyMetadata(null, OnSoruceIdChangedStatic));

        private static void OnSoruceIdChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as ContentPanelHost;
            // Send the post to the class.
            instance?.OnSoruceIdChanged((string)e.OldValue, (string)e.NewValue);
        }

        private async void OnSoruceIdChanged(string oldValue, string newId)
        {
            // Unregister
            if(!string.IsNullOrWhiteSpace(oldValue))
            {
                await UnRegisterPanel(oldValue);
            }

            // If we are full screen clear it.
            if(IsFullscreen)
            {
                OnFullscreenChanged(false);
            }

            // Register the new content
            if(!string.IsNullOrWhiteSpace(newId))
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
                _mCurrentPanelBase = panelBase;
            }

            // Setup loading.
            SetupLoading();

            // Setup error text
            SetupGenericMessage();

            // Setup NSFW
            SetupNsfwBlock();

            // Add the panel to the UI.
            var userControl = (UserControl)panelBase.Panel;
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
                _mCurrentPanelBase = null;
            }

            // Clear out the UI
            ui_contentRoot.Children.Clear();

            // Set loading.
            ToggleProgress(true, false);
        }

        /// <summary>
        /// Called when the content requested by this panel has begun loading.
        /// </summary>
        public void OnContentPreloading()
        {
            lock(this)
            {
                // If we don't have content yet show the loading screen.
                if(_mCurrentPanelBase == null)
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
            var panelBase = _mCurrentPanelBase;
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
            var panelBase = _mCurrentPanelBase;
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
            get => (bool)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register(
                "IsVisible",
                typeof(bool),
                typeof(ContentPanelHost),
                new PropertyMetadata(false, OnIsVisibleChangedStatic));

        private static void OnIsVisibleChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var instance = d as ContentPanelHost;
            instance?.OnVisibleChanged((bool)e.NewValue);
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
            var panelBase = _mCurrentPanelBase;
            panelBase?.OnVisibilityChanged(isVisible);

            // We need to tell the master that our visibility has changed.
            // Sometimes we need to spin a bit because the source might not be set yet.
            var sanityCount = 0;
            while(SourceId == null && sanityCount < 50)
            {
                await Task.Delay(5);
                sanityCount++;
            }

            // Capture the source id.
            var sourceId = SourceId;
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
            var panelBase = _mCurrentPanelBase;
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
            _mIsLoadingShowing = show;

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
                var timeSincePanelAdded = DateTime.Now - _mHostCreationTime;
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
            _mIsLoadingShowing = false;
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
            var panelBase = _mCurrentPanelBase;

            // If the post is over 18, and it hasn't been lowered, and we don't have block off, and we won't have per subreddit on and the subreddit has been lowered
            if (panelBase != null &&
                panelBase.Source.IsNsfw &&
                !PreviousLoweredNsfwBlocks.ContainsKey(panelBase.Source.Id) &&
                    (App.BaconMan.UiSettingsMan.FlipViewNsfwBlockingType == NsfwBlockType.Always ||
                        (App.BaconMan.UiSettingsMan.FlipViewNsfwBlockingType == NsfwBlockType.PerSubreddit &&
                         !string.IsNullOrWhiteSpace(panelBase.Source.Subreddit) &&
                         !PreviousLoweredNsfwSubreddits.ContainsKey(panelBase.Source.Subreddit))))
            {
                _mIsNsfwShowing = true;
                VisualStateManager.GoToState(this, "ShowNsfwBlock", true);
                return true;
            }

            _mIsNsfwShowing = false;

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

        /// <summary>
        /// Fired when the NSFW block is tapped.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NsfwBlockRoot_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Set our state.
            _mIsNsfwShowing = false;

            // Show the content if we can
            HideShowContentIfNeeded();

            // Animate out the NSFW block.
            VisualStateManager.GoToState(this, "HideNsfwBlock", true);

            // Add the id so we don't block again.
            var panelBase = _mCurrentPanelBase;
            if (panelBase == null)
            {
                return;
            }
            if (!string.IsNullOrWhiteSpace(panelBase.Source.Id))
            {
                // Add to our list so we won't block again.
                if (!PreviousLoweredNsfwBlocks.ContainsKey(panelBase.Source.Id))
                {
                    PreviousLoweredNsfwBlocks.Add(panelBase.Source.Id, true);
                }
            }
            if (!string.IsNullOrWhiteSpace(panelBase.Source.Subreddit))
            {
                // Add to our list so we won't block again.
                if (!PreviousLoweredNsfwSubreddits.ContainsKey(panelBase.Source.Subreddit))
                {
                    PreviousLoweredNsfwSubreddits.Add(panelBase.Source.Subreddit, true);
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
            get => (bool)GetValue(CanGoFullScreenProperty);
            set => SetValue(CanGoFullScreenProperty, value);
        }

        public static readonly DependencyProperty CanGoFullScreenProperty =
            DependencyProperty.Register(
                "CanGoFullScreen",
                typeof(bool),
                typeof(ContentPanelHost),
                new PropertyMetadata(true, OnCanGoFullScreenChangedStatic
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
            var args = new ToggleFullScreenEventArgs
            {
                GoFullScreen = goFullscreen
            };
            _mOnToggleFullscreen.Raise(this, args);
            return true;
        }

        #endregion

        #region Generic message UI

        /// <summary>
        /// Sets up the generic message when we are loading.
        /// </summary>
        private void SetupGenericMessage()
        {
            var setError = false;
            var panelBase = _mCurrentPanelBase;
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
            _mIsShowingContentLoadBlock = !App.BaconMan.UiSettingsMan.FlipViewLoadPostContentWithoutAction;
            if (_mIsShowingContentLoadBlock)
            {
                // Show the message
                ToggleGenericMessage(true, "Tap anywhere to load content", "You can change this behavior in the settings", true);

                // Hide loading.
                ToggleProgress(false, true);
            }
            return _mIsShowingContentLoadBlock;
        }

        /// <summary>
        /// Fired when we should toggle the generic message.
        /// </summary>
        /// <param name="show"></param>
        /// <param name="message"></param>
        private void ToggleGenericMessage(bool show, string message = null, string subMessage = null, bool instantOff = false)
        {
            _mIsGenericMessageShowing = show;

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
            if (_mIsShowingContentLoadBlock)
            {
                var args = new ContentLoadRequestArgs
                {
                    SourceId = SourceId
                };
                _mOnContentLoadRequest.Raise(this, args);

                // Set bars
                _mIsShowingContentLoadBlock = false;
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
                    if (_mCurrentPanelBase != null)
                    {
                        url = _mCurrentPanelBase.Source.Url;
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
            if(!_mIsNsfwShowing && !_mIsLoadingShowing && !_mIsGenericMessageShowing)
            {
                ui_contentRoot.Opacity = 1;
            }
        }
    }
}
