using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace Baconit.ContentPanels.Panels
{
    class ContentPanelBase : IContentPanelBase, IContentPanelBaseInternal
    {
        ///
        /// Common Vars
        /// 
        
        /// <summary>
        /// If we are loading or not.
        /// </summary>
        public bool IsLoading { get; private set; } = true;

        /// <summary>
        /// If we are in error or not.
        /// </summary>
        public bool HasError { get; private set; } = false;

        /// <summary>
        /// The text of the error if we have one.
        /// </summary>
        public string ErrorText { get; private set; } = null;

        /// <summary>
        /// Holds a reference to the source we are currently showing.
        /// </summary>
        public ContentPanelSource Source { get; private set; }

        /// <summary>
        /// Indicates if the control is destroyed.
        /// </summary>
        public bool IsDestoryed { get; private set; } = false;

        /// <summary>
        /// The actual panel contained within.
        /// </summary>
        public IContentPanel Panel { get; private set; }

        //
        // Private vars
        //

        /// <summary>
        /// Holds the current panel host.
        /// </summary>
        IContentPanelHost m_host;

        /// <summary>
        /// Indicates if we have told the master we are done loading.
        /// </summary>
        bool m_hasDeclaredLoaded = false;

        #region IContentPanelBaseInternal

        /// <summary>
        /// Indicates if we are full screen.
        /// </summary>
        public bool IsFullscreen {
            get
            {
                IContentPanelHost host = m_host;
                if(host != null)
                {
                    return host.IsFullscreen;
                }
                return false;
            }
        }

        /// <summary>
        /// Indicates if we can go full screen.
        /// </summary>
        public bool CanGoFullscreen
        {
            get
            {
                IContentPanelHost host = m_host;
                if (host != null)
                {
                    return host.CanGoFullscreen;
                }
                return false;
            }
        }

        #region Fire Events

        /// <summary>
        /// Fires toggle loading.
        /// </summary>
        /// <param name="show"></param>
        public void FireOnLoading(bool isLoading)
        {
            // If is the same leave.
            if(isLoading == IsLoading)
            {
                return;
            }

            // Set the value
            IsLoading = isLoading;

            // Try to tell the host
            IContentPanelHost host = m_host;
            if (host != null)
            {
                host.OnLoadingChanged();
            }

            // When loading is done and we haven't before report it to the master
            if (!m_hasDeclaredLoaded && !IsLoading)
            {
                m_hasDeclaredLoaded = true;
                // Tell the manager that we are loaded.
                Task.Run(() =>
                {
                    ContentPanelMaster.Current.OnContentLoadComplete(Source.Id);
                });
            }
        }

        /// <summary>
        /// Fires show error
        /// </summary>
        /// <param name="show"></param>
        public void FireOnError(bool hasError, string errorText = null)
        {
            // Set the value
            HasError = hasError;
            ErrorText = errorText;

            // Try to tell the host
            IContentPanelHost host = m_host;
            if (host != null)
            {
                host.OnErrorChanged();
            }

            // When loading is done report it to the master
            if (!m_hasDeclaredLoaded && HasError)
            {
                m_hasDeclaredLoaded = true;
                // Tell the manager that we are loaded.
                Task.Run(() =>
                {
                    ContentPanelMaster.Current.OnContentLoadComplete(Source.Id);
                });
            }
        }

        /// <summary>
        /// Fires ToggleFullscreen
        /// </summary>
        /// <param name="show"></param>
        public bool FireOnFullscreenChanged(bool goFullscreen)
        {
            // Try to tell the host
            IContentPanelHost host = m_host;
            if (host != null)
            {
                return host.OnFullscreenChanged(goFullscreen);
            }
            return false;
        }

        /// <summary>
        /// Tells the content manager to show this as a web page instead of
        /// the current control.
        /// </summary>
        public void FireOnFallbackToBrowser()
        {
            Task.Run(() =>
            {
                ContentPanelMaster.Current.FallbackToWebrowser(Source);
            });
        }

        #endregion

        #endregion

        #region IContentPanelBase

        /// <summary>
        /// Fired when this post becomes visible
        /// </summary>
        public void OnVisibilityChanged(bool isVisible)
        {
            Panel.OnVisibilityChanged(isVisible);
        }

        /// <summary>
        /// Fired when we should destroy our content.
        /// </summary>
        public void OnDestroyContent()
        {
            // Make sure the panel isn't null, in the case where we 
            // can't load the panel we will be be destroyed before
            // the base is nulled.
            if(Panel != null)
            {
                Panel.OnDestroyContent();
            }
        }

        /// <summary>
        /// Fired when a new host has been added.
        /// </summary>
        /// <param name="host"></param>
        public void OnHostAdded(IContentPanelHost host)
        {
            m_host = host;
            Panel.OnHostAdded();

            // Also fire on visibility changed so the panel is in the correct state
            Panel.OnVisibilityChanged(host.IsVisible);
        }

        /// <summary>
        /// Fired when the host is removed.
        /// </summary>
        public void OnHostRemoved()
        {
            m_host = null;
        }

        /// <summary>
        /// Indicates how large the current panel is.
        /// </summary>
        public PanelMemorySizes PanelMemorySize
        {
            get
            {
                if(Panel != null)
                {
                    return Panel.PanelMemorySize;
                }
                return PanelMemorySizes.Small;
            }
        }

        #endregion

        #region Create Control

        public async Task<bool> CreateContentPanel(ContentPanelSource source, bool canLoadLargePanels)
        {
            // Indicates if the panel was loaded.
            bool loadedPanel = true;

            // Capture the source 
            Source = source;

            // We default to web page
            Type controlType = typeof(WebPageContentPanel);

            // If we are not forcing web find the control type.
            if (!source.ForceWeb)
            {
                // Try to figure out the type.
                try
                {
                    if (GifImageContentPanel.CanHandlePost(source))
                    {
                        controlType = typeof(GifImageContentPanel);
                    }
                    else if (YoutubeContentPanel.CanHandlePost(source))
                    {
                        controlType = typeof(YoutubeContentPanel);
                    }
                    else if (BasicImageContentPanel.CanHandlePost(source))
                    {
                        controlType = typeof(BasicImageContentPanel);
                    }
                    else if (MarkdownContentPanel.CanHandlePost(source))
                    {
                        controlType = typeof(MarkdownContentPanel);
                    }
                    else if (RedditContentPanel.CanHandlePost(source))
                    {
                        controlType = typeof(RedditContentPanel);
                    }
                    else if (CommentSpoilerContentPanel.CanHandlePost(source))
                    {
                        controlType = typeof(CommentSpoilerContentPanel);
                    }
                    else if (WindowsAppContentPanel.CanHandlePost(source))
                    {
                        controlType = typeof(WindowsAppContentPanel);
                    }
                    else
                    {
                        // This is a web browser

                        // If we are blocking large panels don't allow the
                        // browser.
                        if (!canLoadLargePanels)
                        {
                            loadedPanel = false;
                        }
                    }
                }
                catch (Exception e)
                {
                    // If we fail here we will fall back to the web browser.
                    App.BaconMan.MessageMan.DebugDia("Failed to query can handle post", e);
                    App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToQueryCanHandlePost", e);
                }
            }

            // Check if we should still load.
            if (loadedPanel)
            {
                // Make the control on the UI thread.
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    try
                    {
                        // Create the panel
                        Panel = (IContentPanel)Activator.CreateInstance(controlType, this);

                        // Fire OnPrepareContent 
                        Panel.OnPrepareContent();
                    }
                    catch (Exception e)
                    {
                        loadedPanel = false;
                        HasError = true;
                        App.BaconMan.MessageMan.DebugDia("failed to create content control", e);
                        App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToCreateContentPanel", e);
                    }
                });
            }

            // Indicate that we have loaded.
            return loadedPanel;
        }

        #endregion
    }
}
