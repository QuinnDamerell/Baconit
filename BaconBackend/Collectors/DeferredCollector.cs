using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace BaconBackend.Collectors
{
    /// <summary>
    /// Used to support deferred loading of items from a collector. We get all of the items at once,
    /// but only return a subset of them at a time.
    /// NOTE! This collector will not work for collections that support extending!
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DeferredCollector<T>
    {
        /// <summary>
        /// Indicates the state of this collector
        /// </summary>
        enum DeferredLoadState
        {
            Subset,
            All
        };

        /// <summary>
        /// Fired when the state of the collector is changing.
        /// </summary>
        public event EventHandler<OnCollectorStateChangeArgs> OnCollectorStateChange
        {
            add { m_onCollectorStateChange.Add(value); }
            remove { m_onCollectorStateChange.Remove(value); }
        }
        SmartWeakEvent<EventHandler<OnCollectorStateChangeArgs>> m_onCollectorStateChange = new SmartWeakEvent<EventHandler<OnCollectorStateChangeArgs>>();


        /// <summary>
        /// Fired when the state of the collector is changing.
        /// </summary>
        public event EventHandler<OnCollectionUpdatedArgs<T>> OnCollectionUpdated
        {
            add { m_onCollectionUpdated.Add(value); }
            remove { m_onCollectionUpdated.Remove(value); }
        }
        SmartWeakEvent<EventHandler<OnCollectionUpdatedArgs<T>>> m_onCollectionUpdated = new SmartWeakEvent<EventHandler<OnCollectionUpdatedArgs<T>>>();

        // The collector we are holding.
        Collector<T> m_collector;

        /// <summary>
        /// The current number of items we will return from a prelaod.
        /// </summary>
        int m_preLoadCount = 10;

        /// <summary>
        /// Gets the current state of loading.
        /// </summary>
        DeferredLoadState m_state = DeferredLoadState.Subset;

        /// <summary>
        /// Creates a new deferred collector given a collector.
        /// </summary>
        /// <param name="collector"></param>
        /// <param name="preLoadReturnCount"></param>
        public DeferredCollector(Collector<T> collector, int preLoadReturnCount = 10)
        {
            m_preLoadCount = preLoadReturnCount;
            m_collector = collector;
            collector.OnCollectionUpdated += Collector_OnCollectionUpdated;
            collector.OnCollectorStateChange += Collector_OnCollectorStateChange;
        }

        /// <summary>
        /// Begins the precache of the items, this will get all of the items but only return some.
        /// </summary>
        /// <param name="count"></param>
        public bool PreLoadItems(bool forceUpdate = false, int requestCount = 50)
        {
            // Tell the collector to update, when it returns we will only take a subset of
            // the results.
            return m_collector.Update(forceUpdate, requestCount);
        }

        /// <summary>
        /// Loads all of the items the collector has. If we already have the items loaded we will just send them,
        /// if they aren't loaded we will wait for the collector to load them.
        /// </summary>
        /// <param name="forceUpdate"></param>
        /// <param name="requestCount"></param>
        /// <returns></returns>
        public bool LoadAllItems(bool forceUpdate = false, int requestCount = 50)
        {
            bool shouldUpdate = false;

            lock(this)
            {
                // Update our state
                DeferredLoadState oldState = m_state;
                m_state = DeferredLoadState.All;

                // Check the state of the collector.
                if (m_collector.State == CollectorState.Extending || m_collector.State == CollectorState.Updating)
                {
                    // If it is already updating just let it do it's thing.
                    // #todo bug: this will bug out if we are extending because the consumer might not have the full base list.
                    return true;
                }
                else
                {
                    // The collector is idle. Check if already has loaded items or not.
                    // This will only work for collections that don't extend.
                    if (m_collector.GetCurrentPosts().Count == 0)
                    {
                        // We need to update the collection.
                        shouldUpdate = true;
                    }
                    else
                    {
                        // The collector is idle and has posts, so if we haven't given them all out to so now.
                        // Otherwise, do nothing.
                        if(oldState == DeferredLoadState.Subset)
                        {
                            SendDeferredItems();
                            return true;
                        }
                    }
                }
            }

            // Update if we should.
            if(forceUpdate || shouldUpdate)
            {
                return m_collector.Update(forceUpdate, requestCount);
            }
            return false;
        }

        /// <summary>
        /// Returns the current posts the collector has.
        /// </summary>
        /// <param name="getAll"></param>
        /// <returns></returns>
        public List<T> GetCurrentItems(bool getAllItems)
        {
            List<T> items = m_collector.GetCurrentPosts();

            if (!getAllItems)
            {
                if(items.Count > m_preLoadCount)
                {
                    items = items.GetRange(0, m_preLoadCount);
                }
            }
            return items;
        }

        /// <summary>
        /// Returns the collector this deferred collector is holding.
        /// </summary>
        /// <returns></returns>
        public Collector<T> GetCollector()
        {
            return m_collector;
        }


        /// <summary>
        /// Sends any items that were being held back. This should only be called when the collector is idle to
        /// prevent conflicts of updating. Note we also don't call the collection state changed events so we don't
        /// show any loading UI.
        /// </summary>
        private async void SendDeferredItems()
        {
            // Async this to defer it a little bit.
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                // Get the items
                List<T> curentItems = m_collector.GetCurrentPosts();

                if(curentItems.Count > m_preLoadCount)
                {
                    // Only send the range we haven't sent already
                    curentItems = curentItems.GetRange(m_preLoadCount, curentItems.Count - m_preLoadCount);

                    // Make the args for the event
                    OnCollectionUpdatedArgs<T> e = new OnCollectionUpdatedArgs<T>()
                    {
                        StartingPosition = m_preLoadCount,
                        ChangedItems = curentItems,
                        IsFreshUpdate = false,
                        IsInsert = false
                    };

                    // Fire the event
                    m_onCollectionUpdated.Raise(this, e);
                }
            });
        }

        /// <summary>
        /// Fired when the collector's state is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectorStateChange(object sender, OnCollectorStateChangeArgs e)
        {
            // Forward this along.
            m_onCollectorStateChange.Raise(this, e);
        }

        /// <summary>
        /// Fired when the collector is updated.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectionUpdated(object sender, OnCollectionUpdatedArgs<T> e)
        {
            // Make our own args
            OnCollectionUpdatedArgs<T> ourArgs = new OnCollectionUpdatedArgs<T>()
            {
                StartingPosition = e.StartingPosition,
                ChangedItems = e.ChangedItems,
                IsFreshUpdate = e.IsFreshUpdate,
                IsInsert = e.IsInsert
            };

            lock (this)
            {
                // Check if we are deferred.
                if (m_state == DeferredLoadState.Subset)
                {
                    // If the starting index is higher than our loaded count ignore
                    if (ourArgs.StartingPosition > m_preLoadCount)
                    {
                        return;
                    }

                    // Get the range we should send.
                    int rangeLength = m_preLoadCount - ourArgs.StartingPosition;
                    rangeLength = Math.Min(rangeLength, ourArgs.ChangedItems.Count);

                    // If we are in the range send it.
                    if (rangeLength > 0)
                    {
                        ourArgs.ChangedItems = ourArgs.ChangedItems.GetRange(0, rangeLength);
                    }
                    else
                    {
                        // If the update range is outside of our subset ignore.
                        return;
                    }
                }
            }

            // Fire the event
            m_onCollectionUpdated.Raise(this, ourArgs);
        }
    }
}
