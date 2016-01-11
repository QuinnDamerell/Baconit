using BaconBackend.DataObjects;
using BaconBackend.Managers;
using Baconit.ContentPanels;
using Baconit.ContentPanels.Panels;
using Baconit.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace Baconit.ContentPanels
{

    public sealed class ContentPanelMaster
    {
        // Singleton
        private static readonly ContentPanelMaster s_instance = new ContentPanelMaster();
        /// <summary>
        /// Singleton for the master
        /// </summary>
        public static ContentPanelMaster Current
        {
            get { return s_instance; }
        }

        //
        // Private vars
        //

        /// <summary>
        /// The states our content can be in.
        /// </summary>
        enum ContentState
        {
            NotAllowed,
            PendingCreation,
            Created,
            Unloaded,
        }

        /// <summary>
        /// Internal class used to hold content elements in the content list.
        /// </summary>
        private class ContentListElement
        {
            public ContentState State = ContentState.NotAllowed;
            public IContentPanelHost Host = null;
            public IContentPanelBase PanelBase = null;
            public ContentPanelSource Source;
            public string Group;
            public bool IsVisible = false;
        }

        /// <summary>
        /// Holds the list of allowed content and the id.
        /// </summary>
        Dictionary<string, ContentListElement> m_currentPanelList = new Dictionary<string, ContentListElement>();

        /// <summary>
        /// This indicates which post we are waiting on to load if we are delay loading
        /// posts.
        /// </summary>
        string m_currentLoadingContent = null;

        /// <summary>
        /// The list of posts waiting to be loaded.
        /// </summary>
        List<ContentPanelSource> m_delayLoadQueue = new List<ContentPanelSource>();

        public ContentPanelMaster()
        {
            // When created register to listen for memory updates.
            App.BaconMan.MemoryMan.OnMemoryCleanUpRequest += MemoryMan_OnMemoryCleanUpRequest;
        }

        #region Content Control

        /// <summary>
        /// Adds the content to the list of content that is allowed. If someone requested or request this
        /// content they will get a control.
        /// </summary>
        /// <param name="source"></param>
        public void AddAllowedContent(ContentPanelSource source, string groupId = "default", bool delayLoad = false)
        {
            bool isSourceVisible = false;

            // First add an entry that we are making the control.
            lock (m_currentPanelList)
            {
                ContentListElement element;
                // Check to see if it already exists.
                if (m_currentPanelList.ContainsKey(source.Id))
                {
                    element = m_currentPanelList[source.Id];
                    if (element.State != ContentState.NotAllowed)
                    {
                        // We already have this element and it is already loading.
                        // just leave.
                        return;
                    }
                    element.State = ContentState.PendingCreation;
                    element.Source = source;
                    element.Group = groupId;

                    // If we have a host tell them we are starting to load their
                    // content (even though it might be delay loaded). This is safe
                    // to call under lock because it will async to the UI thread.
                    if(element.Host != null)
                    {
                        FireOnContentPreloading(element.Host);
                    }
                }
                else
                {
                    // No one is listening, just make the object.
                    element = new ContentListElement()
                    {
                        State = ContentState.PendingCreation,
                        Group = groupId,
                        Source = source
                    };
                    m_currentPanelList.Add(source.Id, element);
                }

                // Set if we are visible or not
                isSourceVisible = element.IsVisible;

                // Check if we are delay load.
                if (delayLoad)
                {
                    // If so, check if there is content loading.
                    if(String.IsNullOrWhiteSpace(m_currentLoadingContent))
                    {
                        // If not then load us now.
                        m_currentLoadingContent = source.Id;
                    }
                    else
                    {
                        // Otherwise add use to the list and return
                        // #todo add a timeout to the waiting.
                        m_delayLoadQueue.Add(source);
                        return;
                    }
                }
                else
                {
                    // If we are not delay loading set us to be the current content
                    // we are waiting on. Even if there is someone else, people should
                    // wait on this content.
                    m_currentLoadingContent = source.Id;
                }
            }

            // If we got here we need to load some content!
            BeginLoadContent(source, isSourceVisible);
        }

        /// <summary>
        /// Removes content from the list of approved content. Anyone using that content will have it removed.
        /// </summary>
        /// <param name="id"></param>
        public async void RemoveAllowedContent(string id)
        {
            // First we need to see if it has a host and a panel.
            IContentPanelHost host = null;
            IContentPanelBase panelBase = null;
            lock (m_currentPanelList)
            {
                // Make sure we have the element.
                if(!m_currentPanelList.ContainsKey(id))
                {
                    return;
                }

                // Get the element.
                ContentListElement element = m_currentPanelList[id];

                // Get the host and panel if there are any.
                host = element.Host;
                panelBase = element.PanelBase;

                // Null the panel and set the state to now allowed.
                element.PanelBase = null;
                element.State = ContentState.NotAllowed;

                // If we don't have a host delete it.
                if (element.Host == null)
                {
                    m_currentPanelList.Remove(id);
                }

                // Ensure it isn't in our delay load list
                m_delayLoadQueue.Remove(element.Source);
            }

            // Call on load complete in case this was the delay loaded
            // panels were waiting on this panel
            OnContentLoadComplete(id);

            // If we have a host remove the panel from them
            if (host != null && panelBase != null)
            {
                await FireOnRemovePanel(host, panelBase);
            }

            // If we have a panel destroy it
            if(panelBase != null)
            {
                await FireOnDestroyContent(panelBase);
            }
        }

        /// <summary>
        /// Removes all content for a given group id.
        /// </summary>
        /// <param name="groupId"></param>
        public void RemoveAllAllowedContent(string groupId)
        {
            List<string> removeList = GetAllAllowedContentForGroup(groupId);

            foreach (string id in removeList)
            {
                RemoveAllowedContent(id);
            }
        }

        /// <summary>
        /// Gets all of the currently allowed content for the group.
        /// </summary>
        /// <param name="groupId"></param>
        public List<string> GetAllAllowedContentForGroup(string groupId)
        {
            List<string> idList = new List<string>();
            lock (m_currentPanelList)
            {
                foreach (KeyValuePair<string, ContentListElement> pair in m_currentPanelList)
                {
                    if (pair.Value.State != ContentState.NotAllowed && groupId.Equals(pair.Value.Group))
                    {
                        idList.Add(pair.Key);
                    }
                }
            }
            return idList;
        }

        #endregion

        #region Content Load Complete Logic

        /// <summary>
        /// This is called by every piece of content when it is done loading or errors out.
        /// </summary>
        /// <param name="sourceId"></param>
        public async void OnContentLoadComplete(string sourceId)
        {
            // Delay a little bit since this might kick off a background load
            // and it isn't super important. 
            await Task.Delay(100);

            bool isSourceVisible = false;
            ContentPanelSource nextSource = null;
            lock(m_currentPanelList)
            {
                // Check if we are waiting on us
                if(m_currentLoadingContent != null && m_currentLoadingContent.Equals(sourceId))
                {
                    if(m_delayLoadQueue.Count == 0)
                    {
                        // If the queue is empty set current to null and
                        // return.
                        m_currentLoadingContent = null;
                        return;
                    }
                    else
                    {
                        // Otherwise, grab the next element
                        nextSource = m_delayLoadQueue.First();
                        m_delayLoadQueue.RemoveAt(0);

                        // Get if the source is visible.
                        if(m_currentPanelList.ContainsKey(nextSource.Id))
                        {
                            isSourceVisible = m_currentPanelList[nextSource.Id].IsVisible;
                        }

                        // Set it loading
                        m_currentLoadingContent = nextSource.Id;
                    }
                }
                else
                {
                    // Not waiting on us. Get out of here.
                    return;
                }
            }

            // And out of lock start loading.
            if (nextSource != null)
            {
                BeginLoadContent(nextSource, isSourceVisible);
            }
        }

        /// <summary>
        /// Called when a panel changes visibility.
        /// </summary>
        /// <param name="sourcdId"></param>
        public void OnPanelVisibliltyChanged(string sourceId, bool isVisible)
        {
            ContentPanelSource loadNowSource = null;
            lock (m_currentPanelList)
            {
                // See if the panel exists.
                if (m_currentPanelList.ContainsKey(sourceId))
                {
                    // Get the element
                    ContentListElement element = m_currentPanelList[sourceId];

                    // Set the new visibility
                    element.IsVisible = isVisible;

                    // If we aren't visible get out of here now.
                    if(!isVisible)
                    {
                        return;
                    }

                    // If we are unloaded start loading right now.
                    if (element.State == ContentState.Unloaded)
                    {
                        // Grab the source
                        loadNowSource = element.Source;

                        // Set our state to pending.
                        element.State = ContentState.PendingCreation;

                        // Make the delay loading id this so everyone will wait on us.
                        m_currentLoadingContent = loadNowSource.Id;
                    }
                    // If we are pending make sure we aren't being delayed.
                    else if(element.State == ContentState.PendingCreation)
                    {
                        // See if this post is in the delay load post list.
                        foreach (ContentPanelSource source in m_delayLoadQueue)
                        {
                            if (source.Id.Equals(sourceId))
                            {
                                loadNowSource = source;
                                break;
                            }
                        }

                        // If we found the element clean it out of the queue
                        if (loadNowSource != null)
                        {
                            // Remove us from it and start loading now!
                            m_delayLoadQueue.Remove(loadNowSource);

                            // We want to load this now. No matter if we were currently waiting on someone or not
                            // all delay loaded post should wait on this one.
                            m_currentLoadingContent = loadNowSource.Id;
                        }
                    }
                }
            }

            // Now that we are out of lock load the source if we have 
            // one to load.
            if (loadNowSource != null)
            {
                BeginLoadContent(loadNowSource, true);
            }
        }

        /// <summary>
        /// This will actually do the loading of content. This starts the load but
        /// the load isn't actually done until the app fires the loading function above.
        /// </summary>
        /// <param name="source"></param>
        private void BeginLoadContent(ContentPanelSource source, bool isVisible)
        {
            // Next make the control, spin this off to keep the UI thread going.
            Task.Run(async () =>
            {
                // Create a new base and panel.
                ContentPanelBase panelBase = new ContentPanelBase();
                bool panelLoaded = await panelBase.CreateContentPanel(source, CanLoadLaregePanel(isVisible));

                bool destoryPanel = true;
                IContentPanelHost hostToGivePanel = null;
                IContentPanelHost hostToInformUnlaoded = null;

                // Update the list with the control
                lock (m_currentPanelList)
                {
                    // Make sure it is still there.
                    if (m_currentPanelList.ContainsKey(source.Id))
                    {
                        ContentListElement element = m_currentPanelList[source.Id];

                        // Make sure we still have a good state.
                        if (element.State == ContentState.PendingCreation)
                        {
                            // Make sure the panel loaded
                            if (panelLoaded)
                            {
                                // Set the panel and state, if we have a host grab it.
                                destoryPanel = false;
                                element.PanelBase = panelBase;
                                element.State = ContentState.Created;
                                hostToGivePanel = element.Host;                               
                            }
                            else
                            {
                                // If we didn't load it was probably due to low memory.
                                // Set our state to unloaded and tell the host.
                                element.State = ContentState.Unloaded;
                                hostToInformUnlaoded = element.Host;
                                destoryPanel = true;
                            }
                        }                                        
                    }
                }

                // If the entry is now gone or whatever, destroy the post.
                if (destoryPanel)
                {
                    await FireOnDestroyContent(panelBase);
                }
                else
                {
                    // If we have a host inform them the panel is now ready.
                    if (hostToGivePanel != null)
                    {
                        FireOnPanelAvailable(hostToGivePanel, panelBase);
                    }
                }

                // If we have a host to tell that we unloaded the panel tell them.
                if(hostToInformUnlaoded != null)
                {
                    FireOnPanelUnloaded(hostToInformUnlaoded);
                }
            });
        }

        #endregion

        #region Host Logic

        /// <summary>
        /// Called by hosts when they want to get a panel
        /// </summary>
        /// <param name="panelId"></param>
        public async void RegisterForPanel(IContentPanelHost host, string panelId)
        {
            // Indicates if we should fire content loading.
            IContentPanelHost fireContentLoadingHost = null;

            // Used to fire the on panel available event.
            IContentPanelBase returnPanelBase = null;

            // Used to hold a past host if there is one.
            IContentPanelHost pastHost = null;

            // Used to hold the host we will tell is unloaded.
            IContentPanelHost fireUnloadedHost = null;


            // Check to see if the panel exists
            lock (m_currentPanelList)
            {
                // We already have it
                if(m_currentPanelList.ContainsKey(panelId))
                {
                    ContentListElement element = m_currentPanelList[panelId];

                    if(element.Host == null)
                    {
                        // We don't have a host, add ourselves.
                        element.Host = host;
                        if(element.State == ContentState.Created)
                        {
                            // If the control is already created fire the on control available right now.
                            returnPanelBase = element.PanelBase;
                        }
                        else if(element.State == ContentState.PendingCreation)
                        {
                            // If the content is being created tell the panel that.
                            fireContentLoadingHost = element.Host;
                        }
                        else if(element.State == ContentState.Unloaded)
                        {
                            fireUnloadedHost = element.Host;
                        }
                    }
                    else
                    {
                        // We already have a host, grab the host so we can kill it
                        // but make sure to null it so the post doesn't load while we are killing it.
                        pastHost = element.Host;
                        element.Host = null;

                        // If the control is already created remove it from the old host.
                        if (element.State == ContentState.Created)
                        {
                            // If the control is already created fire the on control available right now.
                            returnPanelBase = element.PanelBase;
                        }
                    }
                }
                else
                {
                    // We don't have it, make an entry so when we get it
                    // we will be called back if one is created.
                    ContentListElement element = new ContentListElement()
                    {
                        Host = host,
                        State = ContentState.NotAllowed
                    };
                    m_currentPanelList.Add(panelId, element);
                }
            }

            // If we have a past host we are transferring.
            if(pastHost != null)
            {
                // If we have a panel already we need to remove it.
                if(returnPanelBase != null)
                {
                    await FireOnRemovePanel(pastHost, returnPanelBase);
                }

                // Now that the old host is gone, register again.
                RegisterForPanel(host, panelId);
                return;
            }

            // Out of lock, fire the event if we have a panel
            if(returnPanelBase != null)
            {
                FireOnPanelAvailable(host, returnPanelBase);
            }

            // Or if we have content loading.
            if(fireContentLoadingHost != null)
            {
                FireOnContentPreloading(fireContentLoadingHost);
            }

            // Or if we have unloaded content.
            if (fireUnloadedHost != null)
            {
                FireOnPanelUnloaded(fireUnloadedHost);
            }
        }

        public async void UnRegisterForPanel(IContentPanelHost host, string panelId)
        {
            // Fist we need to see if there was a panel.
            IContentPanelBase removePanelBase = null;
            lock (m_currentPanelList)
            {
                // Make sure we have an entry.
                if (!m_currentPanelList.ContainsKey(panelId))
                {
                    return;
                }

                ContentListElement element = m_currentPanelList[panelId];

                // Important! Make sure this host is the correct host for the panel!
                // This can happen if two hosts register for the same id, but the newer will
                // replace the older one. But we don't want the older to unregister and kill
                // the entry for the newer host.
                // The host can also be null if we are in the process of switching.
                if (element.Host == null || !element.Host.Id.Equals(host.Id))
                {
                    return;
                }

                removePanelBase = m_currentPanelList[panelId].PanelBase;
            }

            // If we got a panel back clear it out
            if (removePanelBase != null)
            {
                await FireOnRemovePanel(host, removePanelBase);
            }

            // Now actually clear the host
            lock (m_currentPanelList)
            {
                // Make sure we have an entry.
                if (!m_currentPanelList.ContainsKey(panelId))
                {
                    return;
                }

                ContentListElement element = m_currentPanelList[panelId];
                element.Host = null;

                // If we the state isn't allowed delete this entry because
                // no one wants it.
                if(element.State == ContentState.NotAllowed)
                {
                    m_currentPanelList.Remove(panelId);
                }
            }
        }

        /// <summary>
        /// Returns a content source for an id if we have it.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ContentPanelSource GetSource(string sourceId)
        {
            lock(m_currentPanelList)
            {
                if(m_currentPanelList.ContainsKey(sourceId))
                {
                    return m_currentPanelList[sourceId].Source;
                }
            }
            return null;
        }

        #endregion

        #region Web fallback logic

        /// <summary>
        /// When fired the source given should fall back to a web
        /// browser instead of a complex control.
        /// </summary>
        public void FallbackToWebrowser(ContentPanelSource source)
        {
            
        }

        #endregion

        #region Unload Logic

        /// <summary>
        /// This will unload any sources that are in this group.
        /// </summary>
        /// <param name="groupId"></param>
        public void UnloadContentForGroup(string groupId)
        {
            List<string> idsInGroup = GetAllAllowedContentForGroup(groupId);
            foreach(string id in idsInGroup)
            {
                UnloadContent(id);
            }
        }

        /// <summary>
        /// If the content is allowed it will be unloaded. It will remain in this state until
        /// a panel that wasn't it becomes visible.
        /// </summary>
        public async void UnloadContent(string sourceId)
        {
            string idToCallLoadCompleteOn = null;
            IContentPanelHost hostToTakeFrom = null;
            IContentPanelBase panelToDestory = null;

            // Lock the list.
            lock (m_currentPanelList)
            {
                // Make sure we have the id
                if (!m_currentPanelList.ContainsKey(sourceId))
                {
                    return;
                }

                // Get the element.
                ContentListElement element = m_currentPanelList[sourceId];

                // Make sure we have something to do here.
                if (element.State == ContentState.NotAllowed || element.State == ContentState.Unloaded)
                {
                    return;
                }

                if (element.State == ContentState.PendingCreation)
                {
                    // The object is either being created or delay loaded. If we set the status to
                    // unloaded this will kill the object when created.
                    element.State = ContentState.Unloaded;

                    // Make sure it isn't in the delay load list
                    m_delayLoadQueue.Remove(element.Source);

                    // Grab the id so we can call load complete on the id.
                    // This will ensure if this is the post we are waiting on
                    // the delay load will move on.
                    idToCallLoadCompleteOn = element.Source.Id;
                }
                else
                {
                    // We are created, grab the panel
                    panelToDestory = element.PanelBase;
                    element.PanelBase = null;

                    // Set our state to unloaded.
                    element.State = ContentState.Unloaded;

                    // If we have a host we need to remove it from the host. It is safe to steal
                    // the panel and host from the list because this panel will not be reset into
                    // the UI until it is created again (which won't be this panel)
                    if (element.Host != null)
                    {
                        hostToTakeFrom = element.Host;
                    }
                }
            }

            // If we have an id call on load complete. This will cause delay load
            // to move on if this was the elemet it was waiting on.
            if(idToCallLoadCompleteOn != null)
            {
                OnContentLoadComplete(idToCallLoadCompleteOn);
            }

            // If we have a source and a panel, take it from the host.
            if(hostToTakeFrom != null && panelToDestory != null)
            {
                await FireOnRemovePanel(hostToTakeFrom, panelToDestory);

                // #todo tell the host it was unloaded so they can put up UI.
            }

            // If we have a panel destroy it.
            if(panelToDestory != null)
            {
                await FireOnDestroyContent(panelToDestory);
            }
        }

        #endregion

        #region Memory Pressure Logic

        /// <summary>
        /// Fired by the memory manager when we have memory pressure.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void MemoryMan_OnMemoryCleanUpRequest(object sender, OnMemoryCleanupRequestArgs e)
        {
            // Only care if we are medium or higher
            if(e.CurrentPressure >= MemoryPressureStates.Medium)
            {
                // A list of panels to destroy and hosts to tell.
                List<Tuple<IContentPanelBase, IContentPanelHost>> destroyList = new List<Tuple<IContentPanelBase, IContentPanelHost>>();

                lock(m_currentPanelList)
                {
                    // Loop through the list and see if we have any large panels.
                    foreach(KeyValuePair<string, ContentListElement> pair in m_currentPanelList)
                    {
                        // Make sure we have a panel.
                        if(pair.Value.PanelBase == null)
                        {
                            continue;
                        }

                        bool destroyPanel = false;

                        if(pair.Value.IsVisible)
                        { 
                            // If we are visible only destroy the panel if we are at high memory pressure
                            if (e.CurrentPressure == MemoryPressureStates.HighNoAllocations &&
                                pair.Value.PanelBase.PanelMemorySize > PanelMemorySizes.Small)
                            {
                                destroyPanel = true;
                            }                        
                        }
                        else
                        {
                            // The memory pressure is medium or high. If it is hidden and larger 
                            // than small destroy it.
                            if (pair.Value.PanelBase.PanelMemorySize > PanelMemorySizes.Small)
                            {
                                destroyPanel = true;
                            }
                        }

                        // If we need to destroy
                        if(destroyPanel)
                        {
                            // Add to our list.
                            destroyList.Add(new Tuple<IContentPanelBase, IContentPanelHost>(pair.Value.PanelBase, pair.Value.Host));

                            // Null the panel
                            pair.Value.PanelBase = null;

                            // Update the state
                            pair.Value.State = ContentState.Unloaded;
                        }
                    }
                }

                // Now out of lock, kill the panels.
                foreach(Tuple<IContentPanelBase, IContentPanelHost> tuple in destroyList)
                {
                    // If we have a host...
                    if (tuple.Item2 != null)
                    {
                        // Remove from the host
                        await FireOnRemovePanel(tuple.Item2, tuple.Item1);

                        // Tell the panel it was unloaded.
                        FireOnPanelUnloaded(tuple.Item2);
                    }

                    // And Destroy the panel
                    await FireOnDestroyContent(tuple.Item1);
                }
            }
        }

        /// <summary>
        /// Indicates if we can load a large panel.
        /// </summary>
        /// <returns></returns>
        private bool CanLoadLaregePanel(bool isVisible)
        {
            // If we are visible only don't load if we are on high, if not visible only load if not medium or high.
            return App.BaconMan.MemoryMan.MemoryPressure < (isVisible ? MemoryPressureStates.HighNoAllocations : MemoryPressureStates.Medium);
        }

        #endregion

        #region Fire Events

        /// <summary>
        /// Fires OnPanelAvailable on the UI thread.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="panel"></param>
        private async void FireOnPanelAvailable(IContentPanelHost host, IContentPanelBase panelBase)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                try
                {
                    // First tell the post it has a host
                    panelBase.OnHostAdded(host);

                    // Then tell the host it has a panel.
                    host.OnPanelAvailable(panelBase);
                }
                catch (Exception e)
                {
                    App.BaconMan.MessageMan.DebugDia("FireOnPanelAvailable failed", e);
                    App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FireOnPanelAvailableFailed", e);
                }
            });
        }

        /// <summary>
        /// Fires FireOnPanelStolen on the UI thread.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="panel"></param>
        private async Task FireOnRemovePanel(IContentPanelHost host, IContentPanelBase panelBase)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                try
                {
                    // Tell the panel the host is gone
                    panelBase.OnHostRemoved();

                    // And remove the panel.
                    host.OnRemovePanel(panelBase);
                }
                catch (Exception e)
                {
                    App.BaconMan.MessageMan.DebugDia("FireOnRemovePanel failed", e);
                    App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FireOnRemovePanelFailed", e);
                }
            });
        }

        /// <summary>
        /// Fires FireOnDestroyContnet on the UI thread.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="panel"></param>
        private async Task FireOnDestroyContent(IContentPanelBase panelBase)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                try
                {
                    panelBase.OnDestroyContent();
                }
                catch (Exception e)
                {
                    App.BaconMan.MessageMan.DebugDia("FireOnRemovePanel failed", e);
                    App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FireOnRemovePanelFailed", e);
                }
            });
        }

        /// <summary>
        /// Fires OnContentPreloading on the UI thread.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="panel"></param>
        private async void FireOnContentPreloading(IContentPanelHost host)
        {
            // Do this on a high pri so the loading indicator will show up ASAP.
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                try
                {
                    // Then tell the content has begun loading.
                    host.OnContentPreloading();
                }
                catch (Exception e)
                {
                    App.BaconMan.MessageMan.DebugDia("FireOnContentPreloading failed", e);
                    App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FireOnContentPreloadingFailed", e);
                }
            });
        }

        /// <summary>
        /// Fires OnPanelUnloaded on the UI thread.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="panel"></param>
        private async void FireOnPanelUnloaded(IContentPanelHost host)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                try
                {
                    // Then tell the pane has been unloaded.
                    host.OnPanelUnloaded();
                }
                catch (Exception e)
                {
                    App.BaconMan.MessageMan.DebugDia("FireOnPanelUnloaded failed", e);
                    App.BaconMan.TelemetryMan.ReportUnExpectedEvent(this, "FireOnPanelUnloadedFailed", e);
                }
            });
        }

        #endregion
    }
}
