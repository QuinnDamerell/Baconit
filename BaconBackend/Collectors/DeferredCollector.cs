using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using Windows.UI.Core;

namespace BaconBackend.Collectors
{

    /// <summary>
    /// Indicates the state of this collector
    /// </summary>
    public enum DeferredLoadState
    {
        Subset,
        All
    };

    /// <summary>
    /// Used to support deferred loading of items from a collector. We get all of the items at once,
    /// but only return a subset of them at a time.
    /// NOTE! This collector will not work for collections that support extending!
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DeferredCollector<T>
    {
        private object _lock = new object();

        /// <summary>
        /// Fired when the state of the collector is changing.
        /// </summary>
        public event EventHandler<CollectorStateChangeArgs> OnCollectorStateChange
        {
            add => _collectorStateChange.Add(value);
            remove => _collectorStateChange.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<CollectorStateChangeArgs>> _collectorStateChange = new SmartWeakEvent<EventHandler<CollectorStateChangeArgs>>();


        /// <summary>
        /// Fired when the state of the collector is changing.
        /// </summary>
        public event EventHandler<CollectionUpdatedArgs<T>> OnCollectionUpdated
        {
            add => _collectionUpdated.Add(value);
            remove => _collectionUpdated.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<CollectionUpdatedArgs<T>>> _collectionUpdated = new SmartWeakEvent<EventHandler<CollectionUpdatedArgs<T>>>();

        // The collector we are holding.
        private readonly Collector<T> _collector;

        /// <summary>
        /// The current number of items we will return from a pre-load.
        /// </summary>
        private readonly int _preLoadCount = 10;

        /// <summary>
        /// Gets the current state of loading.
        /// </summary>
        private DeferredLoadState _state = DeferredLoadState.Subset;

        /// <summary>
        /// Creates a new deferred collector given a collector.
        /// </summary>
        /// <param name="collector"></param>
        /// <param name="preLoadReturnCount"></param>
        public DeferredCollector(Collector<T> collector, int preLoadReturnCount = 10)
        {
            _preLoadCount = preLoadReturnCount;
            _collector = collector;
            collector.OnCollectionUpdated += Collector_OnCollectionUpdated;
            collector.OnCollectorStateChange += Collector_OnCollectorStateChange;
        }

        /// <summary>
        /// Begins the pre-cache of the items, this will get all of the items but only return some.
        /// </summary>
        /// <param name="forceUpdate"></param>
        /// <param name="requestCount"></param>
        public bool PreLoadItems(bool forceUpdate = false, int requestCount = 50)
        {
            // Tell the collector to update, when it returns we will only take a subset of
            // the results.
            return _collector.Update(forceUpdate, requestCount);
        }

        /// <summary>
        /// Gets the current state of the deferred collector.
        /// </summary>
        /// <returns></returns>
        public DeferredLoadState GetState()
        {
            lock (_lock)
            {
                return _state;
            }
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
            var shouldUpdate = false;

            lock(_lock)
            {
                // Update our state
                var oldState = _state;
                _state = DeferredLoadState.All;

                // Check the state of the collector.
                if (_collector.State == CollectorState.Extending || _collector.State == CollectorState.Updating)
                {
                    // If it is already updating just let it do it's thing.
                    // #todo bug: this will bug out if we are extending because the consumer might not have the full base list.
                    return true;
                }

                // The collector is idle. Check if already has loaded items or not.
                // This will only work for collections that don't extend.
                if (_collector.GetCurrentPosts().Count == 0)
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

            // Update if we should.
            if(forceUpdate || shouldUpdate)
            {
                return _collector.Update(forceUpdate, requestCount);
            }
            return false;
        }

        /// <summary>
        /// Returns the current posts the collector has.
        /// </summary>
        /// <param name="getAllItems"></param>
        /// <returns></returns>
        public List<T> GetCurrentItems(bool getAllItems)
        {
            var items = _collector.GetCurrentPosts();

            if (getAllItems) return items;
            if(items.Count > _preLoadCount)
            {
                items = items.GetRange(0, _preLoadCount);
            }
            return items;
        }

        /// <summary>
        /// Returns the collector this deferred collector is holding.
        /// </summary>
        /// <returns></returns>
        public Collector<T> GetCollector()
        {
            return _collector;
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
                var currentPosts = _collector.GetCurrentPosts();

                if (currentPosts.Count <= _preLoadCount) return;

                // Only send the range we haven't sent already
                currentPosts = currentPosts.GetRange(_preLoadCount, currentPosts.Count - _preLoadCount);

                // Make the args for the event
                var e = new CollectionUpdatedArgs<T>
                {
                    StartingPosition = _preLoadCount,
                    ChangedItems = currentPosts,
                    IsFreshUpdate = false,
                    IsInsert = false
                };

                // Fire the event
                _collectionUpdated.Raise(this, e);
            });
        }

        /// <summary>
        /// Fired when the collector's state is changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectorStateChange(object sender, CollectorStateChangeArgs e)
        {
            // Forward this along.
            _collectorStateChange.Raise(this, e);
        }

        /// <summary>
        /// Fired when the collector is updated.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectionUpdated(object sender, CollectionUpdatedArgs<T> e)
        {
            // Make our own args
            var ourArgs = new CollectionUpdatedArgs<T>
            {
                StartingPosition = e.StartingPosition,
                ChangedItems = e.ChangedItems,
                IsFreshUpdate = e.IsFreshUpdate,
                IsInsert = e.IsInsert
            };

            lock (_lock)
            {
                // Check if we are deferred.
                if (_state == DeferredLoadState.Subset)
                {
                    // If the starting index is higher than our loaded count ignore
                    if (ourArgs.StartingPosition > _preLoadCount)
                    {
                        return;
                    }

                    // Get the range we should send.
                    var rangeLength = _preLoadCount - ourArgs.StartingPosition;
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
            _collectionUpdated.Raise(this, ourArgs);
        }
    }
}
