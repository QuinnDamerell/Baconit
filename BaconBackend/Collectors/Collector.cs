using BaconBackend.Helpers;
using BaconBackend.Managers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BaconBackend.Collectors
{
    /// <summary>
    /// Defines the possible states of the collector
    /// </summary>
    public enum CollectorState
    {
        /// <summary>
        /// If the collector is not doing any operation, and is in a correct state.
        /// </summary>
        Idle,
        /// <summary>
        /// If the collector is requesting updated versions of the data it has.
        /// </summary>
        Updating,
        /// <summary>
        /// If the collector is requesting new information to extend its data.
        /// </summary>
        Extending,
        /// <summary>
        /// If there is no more content to extend the collector with.
        /// </summary>
        FullyExtended,
        /// <summary>
        /// If an error occurred in the collector's operation.
        /// </summary>
        Error
    }

    /// <summary>
    /// Defines the possible error states of the collector
    /// </summary>
    public enum CollectorErrorState
    {
        Unknown,
        ServiceDown,
    }

    /// <summary>
    /// The args class for the OnCollectorStateChange event.
    /// </summary>
    public class CollectorStateChangeArgs : EventArgs
    {
        /// <summary>
        /// The collector's new state.
        /// </summary>
        public CollectorState State;
        public CollectorErrorState ErrorState = CollectorErrorState.Unknown;
        public int NewPostCount;
    }

    /// <summary>
    /// The args for the OnCollectionUpdated event.
    /// </summary>
    public class CollectionUpdatedArgs<T> : EventArgs
    {
        /// <summary>
        /// If the information in the collector was just fully updated.
        /// </summary>
        public bool IsFreshUpdate;
        /// <summary>
        /// If the collection was changed by inserting something user created (and not from a web request).
        /// </summary>
        public bool IsInsert;
        /// <summary>
        /// The position of the first updated item in the collector's items.
        /// </summary>
        public int StartingPosition;
        /// <summary>
        /// The list of items in the collector that have changed.
        /// </summary>
        public List<T> ChangedItems;
    }

    /// <summary>
    /// Possible vote actions.
    /// </summary>
    public enum PostVoteAction
    {
        UpVote,
        DownVote,
    }

    /// <summary>
    /// Types of sort
    /// </summary>
    public enum SortTypes
    {
        Hot,
        New,
        Rising,
        Controversial,
        Top
    }

    /// <summary>
    /// Types of sort for comments
    /// </summary>
    public enum CommentSortTypes
    {
        Best,
        New,
        Top,
        Controversial,
        Old,
        Qa
    }

    /// <summary>
    /// Types of sort times.
    /// </summary>
    public enum SortTimeTypes
    {
        Hour,
        Day,
        Week,
        Month,
        Year,
        AllTime
    }

    public abstract class Collector<T>
    {
        /// <summary>
        /// The static dictionary that holds all known instances of the collectors
        /// </summary>
        private static readonly Dictionary<string, WeakReference<Collector<T>>> Collectors = new Dictionary<string, WeakReference<Collector<T>>>();

        /// <summary>
        /// Returns a collector for the given type. If the collector doesn't exist one will be created.
        /// </summary>
        /// <param name="objectType"></param>
        /// <param name="collectorId"></param>
        /// <param name="initObject"></param>
        /// <param name="baconMan"></param>
        /// <returns></returns>
        protected static Collector<T> GetCollector(Type objectType, string collectorId, object initObject, BaconManager baconMan)
        {
            lock (Collectors)
            {
                // See if the collector exists and if it does try to get it.
                if (Collectors.ContainsKey(collectorId)
                    && Collectors[collectorId].TryGetTarget(out var collector))
                {
                    return collector;
                }

                object[] args = { initObject, baconMan };
                collector = (Collector<T>)Activator.CreateInstance(objectType, args);
                Collectors[collectorId] = new WeakReference<Collector<T>>(collector);
                return collector;
            }
        }

        /// <summary>
        /// Fired when the state of the collector is changing.
        /// </summary>
        public event EventHandler<CollectorStateChangeArgs> OnCollectorStateChange
        {
            add =>  _collectorStateChange.Add(value);
            remove =>  _collectorStateChange.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<CollectorStateChangeArgs>>  _collectorStateChange = new SmartWeakEvent<EventHandler<CollectorStateChangeArgs>>();


        /// <summary>
        /// Fired when the state of the collector is changing.
        /// </summary>
        public event EventHandler<CollectionUpdatedArgs<T>> OnCollectionUpdated
        {
            add =>  _collectionUpdated.Add(value);
            remove =>  _collectionUpdated.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<CollectionUpdatedArgs<T>>>  _collectionUpdated = new SmartWeakEvent<EventHandler<CollectionUpdatedArgs<T>>>();

        public CollectorState State { get; private set; } = CollectorState.Idle;

        public CollectorErrorState ErrorState { get; private set; } = CollectorErrorState.Unknown;

        //
        // Abstract Functions
        //

        /// <summary>
        /// The derived collector must implement this function.
        /// </summary>
        /// <param name="posts"></param>
        protected abstract void ApplyCommonFormatting(ref List<T> posts);

        /// <summary>
        /// The derived collector must implement this function.
        /// </summary>
        /// <param name="elements"></param>
        protected abstract List<T> ParseElementList(List<Element<T>> elements);

        //
        // Private vars
        //

        private RedditListHelper<T> _listHelper;
        private readonly BaconManager _baconMan;
        private string _uniqueId;

        protected Collector(BaconManager manager, string uniqueId)
        {
            _baconMan = manager;
            _uniqueId = uniqueId;
        }

        /// <summary>
        /// Sets the unique ID again if it needs to be changed. The subreddit collector does this
        /// for force loading posts.
        /// </summary>
        /// <param name="newId"></param>
        protected void SetUniqueId(string newId)
        {
            _uniqueId = newId;
        }

        /// <summary>
        /// Sets up the list helper
        /// </summary>
        /// <param name="baseUrl"></param>
        /// <param name="hasEmptyArrayRoot"></param>
        /// <param name="takeFirstArrayRoot"></param>
        /// <param name="optionalGetArgs"></param>
        protected void InitListHelper(string baseUrl, bool hasEmptyArrayRoot = false, bool takeFirstArrayRoot = false, string optionalGetArgs = "")
        {
            _listHelper = new RedditListHelper<T>(baseUrl, _baconMan.NetworkMan, hasEmptyArrayRoot, takeFirstArrayRoot, optionalGetArgs);
        }

        /// <summary>
        /// Called by consumers when the collection should be updated. If the force flag is set we should always do it.
        /// </summary>
        /// <param name="force"></param>
        /// <param name="updateCount"></param>
        /// <returns>If an update was kicked off or not</returns>
        public virtual bool Update(bool force = false, int updateCount = 50)
        {
            // #todo add caching
            // #todo #bug If we are refreshing we will grab 50 new post but listeners might already have 100
            // we need to indicate to them they should remove the rest of the posts that are old.
            lock ( _listHelper)
            {
                if (State == CollectorState.Updating || State == CollectorState.Extending)
                {
                    return false;
                }

                var timeSinceLastUpdate = DateTime.Now - LastUpdateTime;
                if (timeSinceLastUpdate.TotalHours < 2                // Check the time has been longer than 2 hours
                    && _listHelper.GetCurrentElements().Count != 0   // Check that we have elements
                    && !force)                                        // Check that it isn't a force
                {
                    return false;
                }

                // Otherwise do the update
                State = CollectorState.Updating;
            }

            // Fire this not under lock
            FireStateChanged();

            // Kick off a new task to get the posts
            new Task(async () =>
            {
                try
                {
                    // First clear out the helper to remove any current posts.
                    _listHelper.Clear();

                    // Get the next elements
                    // #todo make this lower when we have endless scrolling.
                    var posts = ParseElementList(await _listHelper.FetchElements(0, updateCount));

                    // Fire the notification that the list has updated.
                    FireCollectionUpdated(0, posts, true, false);

                    // Update the update time
                    LastUpdateTime = DateTime.Now;

                    // Update the state
                    lock ( _listHelper)
                    {
                        State = CollectorState.Idle;
                    }

                    FireStateChanged(posts.Count);
                }
                catch (Exception e)
                {
                    _baconMan.MessageMan.DebugDia("Collector failed to update id:"+ _uniqueId, e);

                    // Update the state
                    lock ( _listHelper)
                    {
                        State = CollectorState.Error;
                        ErrorState = e is ServiceDownException ? CollectorErrorState.ServiceDown : CollectorErrorState.Unknown;
                    }
                    FireStateChanged();
                }
            }).Start();

            return true;
        }

        /// <summary>
        /// Extends the amount of post shown.
        /// </summary>
        /// <param name="extendCount"></param>
        public void ExtendCollection(int extendCount = 50)
        {
            // #todo #bug If we are refreshing we will grab 50 new post but listeners might already have 100
            // we need to indicate to them they should remove the rest of the posts that are old.
            lock ( _listHelper)
            {
                if (State == CollectorState.Updating || State == CollectorState.Extending || State == CollectorState.FullyExtended)
                {
                    return;
                }

                // Otherwise do the extension
                State = CollectorState.Extending;
            }

            // Fire this not under lock
            FireStateChanged();

            // Kick off a new task to get the posts
            Task.Run(async () =>
            {
                try
                {
                    var previousCollectionSize = GetCurrentPostsInternal().Count;

                    // Get the next elements
                    // #todo make this lower when we have endless scrolling.
                    var posts = ParseElementList(await _listHelper.FetchNext(extendCount));

                    // Fire the notification that the list has updated.
                    FireCollectionUpdated(previousCollectionSize, posts, false, false);

                    // Update the state
                    lock ( _listHelper)
                    {
                        if (posts.Count == 0)
                        {
                            // If we don't get anything back we are fully extended.
                            State = CollectorState.FullyExtended;
                        }
                        else
                        {
                            State = CollectorState.Idle;
                        }
                    }
                    FireStateChanged(posts.Count);
                }
                catch (Exception e)
                {
                    _baconMan.MessageMan.DebugDia("Subreddit extension failed", e);

                    // Update the state
                    lock ( _listHelper)
                    {
                        State = CollectorState.Error;
                        ErrorState = e is ServiceDownException ? CollectorErrorState.ServiceDown : CollectorErrorState.Unknown;
                    }
                    FireStateChanged();
                }
            });
        }

        /// <summary>
        /// Returns the current post that are cached in the object.
        /// </summary>
        /// <returns></returns>
        public List<T> GetCurrentPosts()
        {
            // Grab the lock
            lock ( _listHelper)
            {
                if (State == CollectorState.Updating || State == CollectorState.Extending)
                {
                    // If we are updating we don't want to mess with the list, so return any local list we have
                    // (a cache). We don't have a cache so just return empty.
                    return new List<T>();
                }

                // Get the list and return it.
                return ParseElementList( _listHelper.GetCurrentElements());
            }
        }

        /// <summary>
        /// Used to get the guts of the list helper. This is needed to add fake comments.
        /// </summary>
        protected List<Element<T>> GetListHelperElements()
        {
            return _listHelper.GetCurrentElements();
        }

        /// <summary>
        /// This will return the current post regardless of update status.
        /// </summary>
        /// <returns></returns>
        protected List<T> GetCurrentPostsInternal()
        {
            // Grab the lock
            return ParseElementList( _listHelper.GetCurrentElements());
        }

        /// <summary>
        /// Fire the state changed event.
        /// </summary>
        protected void FireStateChanged(int newPostCount = 0)
        {
            try
            {
                 _collectorStateChange.Raise(this, new CollectorStateChangeArgs { State = State, ErrorState = ErrorState, NewPostCount = newPostCount });
            }
            catch (Exception e)
            {
                _baconMan.MessageMan.DebugDia("Exception during OnCollectorStateChange", e);
            }
        }

        /// <summary>
        /// Fire the list updated event
        /// </summary>
        protected void FireCollectionUpdated(int startingIndex, List<T> updatedPosts, bool isFreshUpdate, bool isInsert)
        {
            // Format the list before we send it
            ApplyCommonFormatting(ref updatedPosts);

            try
            {
                 _collectionUpdated.Raise(this, new CollectionUpdatedArgs<T> { StartingPosition = startingIndex, ChangedItems = updatedPosts, IsFreshUpdate = isFreshUpdate, IsInsert = isInsert });
            }
            catch (Exception e)
            {
                _baconMan.MessageMan.DebugDia("Exception during OnCollectionUpdated", e);
            }
        }

        /// <summary>
        /// The last time the subreddit was updated, note this changes per subreddit!!
        /// </summary>
        private DateTime LastUpdateTime
        {
            get
            {
                if (!_lastUpdateTime.Equals(new DateTime(0))) return _lastUpdateTime;
                if (_baconMan.SettingsMan.LocalSettings.ContainsKey("Collector.LastUpdateTime_" + _uniqueId))
                {
                    _lastUpdateTime = _baconMan.SettingsMan.ReadFromLocalSettings<DateTime>("Collector.LastUpdateTime_" + _uniqueId);
                }
                return _lastUpdateTime;
            }
            set
            {
                _lastUpdateTime = value;
                _baconMan.SettingsMan.WriteToLocalSettings(("Collector.LastUpdateTime_" + _uniqueId), _lastUpdateTime);
            }
        }
        private DateTime _lastUpdateTime = new DateTime(0);
    }
}
