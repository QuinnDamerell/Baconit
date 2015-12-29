using BaconBackend.Helpers;
using BaconBackend.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    public class OnCollectorStateChangeArgs : EventArgs
    {
        /// <summary>
        /// The collector's new state.
        /// </summary>
        public CollectorState State;
        public CollectorErrorState ErrorState = CollectorErrorState.Unknown;
        public int NewPostCount = 0;
    }

    /// <summary>
    /// The args for the OnCollectionUpdated event.
    /// </summary>
    public class OnCollectionUpdatedArgs<T> : EventArgs
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
        QA
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
        private static Dictionary<string, WeakReference<Collector<T>>> s_collectors = new Dictionary<string, WeakReference<Collector<T>>>();

        /// <summary>
        /// Returns a collector for the given type. If the collector doesn't exist one will be created.
        /// </summary>
        /// <param name="subreddit"></param>
        /// <returns></returns>
        protected static Collector<T> GetCollector(Type objectType, string collectorId, object initObject, BaconManager baconMan)
        {
            lock (s_collectors)
            {
                // See if the collector exists and if it does try to get it.
                Collector<T> collector = null;
                if (s_collectors.ContainsKey(collectorId)
                    && s_collectors[collectorId].TryGetTarget(out collector)
                    && collector != null)
                {
                    return collector;
                }
                else
                {
                    object[] args = { initObject, baconMan };
                    collector = (Collector<T>)Activator.CreateInstance(objectType, args);
                    s_collectors[collectorId] = new WeakReference<Collector<T>>(collector);
                    return collector;
                }
            }
        }

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

        public CollectorState State
        {
            get { return m_state; }
        }

        public CollectorErrorState ErrorState
        {
            get { return m_errorState; }
        }

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
        /// <param name="posts"></param>
        protected abstract List<T> ParseElementList(List<Element<T>> elements);

        //
        // Private vars
        //

        CollectorState m_state = CollectorState.Idle;
        CollectorErrorState m_errorState = CollectorErrorState.Unknown;
        RedditListHelper<T> m_listHelper;
        BaconManager m_baconMan;
        string m_uniqueId;

        protected Collector(BaconManager manager, string uniqueId)
        {
            m_baconMan = manager;
            m_uniqueId = uniqueId;

            // Sub to user changes so we can update the list.
            m_baconMan.UserMan.OnUserUpdated += OnUserUpdated;
        }

        /// <summary>
        /// Sets the unique ID again if it needs to be changed. The subreddit collector does this
        /// for force loading posts.
        /// </summary>
        /// <param name="newId"></param>
        protected void SetUniqueId(string newId)
        {
            m_uniqueId = newId;
        }

        /// <summary>
        /// Sets up the list helper
        /// </summary>
        /// <param name="baseUrl"></param>
        /// <param name="hasEmptyArrrayRoot"></param>
        protected void InitListHelper(string baseUrl, bool hasEmptyArrrayRoot = false, bool takeFirstArrayRoot = false, string optionalGetArgs = "")
        {
            m_listHelper = new RedditListHelper<T>(baseUrl, m_baconMan.NetworkMan, hasEmptyArrrayRoot, takeFirstArrayRoot, optionalGetArgs);
        }

        /// <summary>
        /// Called by consumers when the collection should be updated. If the force flag is set we should always do it.
        /// </summary>
        /// <param name="force"></param>
        /// <returns>If an update was kicked off or not</returns>
        public bool Update(bool force = false, int updateCount = 50)
        {
            // #todo add caching
            // #todo #bug If we are refreshing we will grab 50 new post but listeners might already have 100
            // we need to indicate to them they should remove the rest of the posts that are old.
            lock (m_listHelper)
            {
                if (m_state == CollectorState.Updating || m_state == CollectorState.Extending)
                {
                    return false;
                }

                TimeSpan timeSinceLastUpdate = DateTime.Now - LastUpdateTime;
                if (timeSinceLastUpdate.TotalHours < 2                // Check the time has been longer than 2 hours
                    && m_listHelper.GetCurrentElements().Count != 0   // Check that we have elements
                    && !force)                                        // Check that it isn't a force
                {
                    return false;
                }

                // Otherwise do the update
                m_state = CollectorState.Updating;
            }

            // Fire this not under lock
            FireStateChanged();

            // Kick off a new task to get the posts
            new Task(async () =>
            {
                try
                {
                    // First clear out the helper to remove any current posts.
                    m_listHelper.Clear();

                    // Get the next elements
                    // #todo make this lower when we have endless scrolling.
                    List<T> posts = ParseElementList(await m_listHelper.FetchElements(0, updateCount));

                    // Fire the notification that the list has updated.
                    FireCollectionUpdated(0, posts, true, false);

                    // Update the update time
                    LastUpdateTime = DateTime.Now;

                    // Update the state
                    lock (m_listHelper)
                    {
                        m_state = CollectorState.Idle;
                    }

                    FireStateChanged(posts.Count);
                }
                catch (Exception e)
                {
                    m_baconMan.MessageMan.DebugDia("Collector failed to update id:"+ m_uniqueId, e);

                    // Update the state
                    lock (m_listHelper)
                    {
                        m_state = CollectorState.Error;
                        m_errorState = e is ServiceDownException ? CollectorErrorState.ServiceDown : CollectorErrorState.Unknown;
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
            lock (m_listHelper)
            {
                if (m_state == CollectorState.Updating || m_state == CollectorState.Extending || m_state == CollectorState.FullyExtended)
                {
                    return;
                }

                // Otherwise do the extension
                m_state = CollectorState.Extending;
            }

            // Fire this not under lock
            FireStateChanged();

            // Kick off a new task to get the posts
            Task.Run(async () =>
            {
                try
                {
                    int previousCollectionSize = GetCurrentPostsInternal().Count;

                    // Get the next elements
                    // #todo make this lower when we have endless scrolling.
                    List<T> posts = ParseElementList(await m_listHelper.FetchNext(extendCount));

                    // Fire the notification that the list has updated.
                    FireCollectionUpdated(previousCollectionSize, posts, false, false);

                    // Update the state
                    lock (m_listHelper)
                    {
                        if (posts.Count == 0)
                        {
                            // If we don't get anything back we are fully extended.
                            m_state = CollectorState.FullyExtended;
                        }
                        else
                        {
                            m_state = CollectorState.Idle;
                        }
                    }
                    FireStateChanged(posts.Count);
                }
                catch (Exception e)
                {
                    m_baconMan.MessageMan.DebugDia("Subreddit extension failed", e);

                    // Update the state
                    lock (m_listHelper)
                    {
                        m_state = CollectorState.Error;
                        m_errorState = e is ServiceDownException ? CollectorErrorState.ServiceDown : CollectorErrorState.Unknown;
                    }
                    FireStateChanged();
                }
            });
        }


        /// <summary>
        /// Fired when the current user is updated.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnUserUpdated(object sender, OnUserUpdatedArgs args)
        {
            // If a user is added or removed update the subreddit to reflect the new user.
            if (args.Action != UserCallbackAction.Updated)
            {
                Update(true);
            }
        }


        /// <summary>
        /// Returns the current post that are cached in the object.
        /// </summary>
        /// <returns></returns>
        public List<T> GetCurrentPosts()
        {
            // Grab the lock
            lock (m_listHelper)
            {
                if (m_state == CollectorState.Updating || m_state == CollectorState.Extending)
                {
                    // If we are updating we don't want to mess with the list, so return any local list we have
                    // (a cache). We don't have a cache so just return empty.
                    return new List<T>();
                }
                else
                {
                    // Get the list and return it.
                    return ParseElementList(m_listHelper.GetCurrentElements());
                }
            }
        }

        /// <summary>
        /// Used to get the guts of the list helper. This is needed to add fake comments.
        /// </summary>
        protected List<Element<T>> GetListHelperElements()
        {
            return m_listHelper.GetCurrentElements();
        }

        /// <summary>
        /// This will return the current post regardless of update status.
        /// </summary>
        /// <returns></returns>
        protected List<T> GetCurrentPostsInternal()
        {
            // Grab the lock
            return ParseElementList(m_listHelper.GetCurrentElements());
        }

        /// <summary>
        /// Fire the state changed event.
        /// </summary>
        protected void FireStateChanged(int newPostCount = 0)
        {
            try
            {
                m_onCollectorStateChange.Raise(this, new OnCollectorStateChangeArgs() { State = m_state, ErrorState = m_errorState, NewPostCount = newPostCount });
            }
            catch (Exception e)
            {
                m_baconMan.MessageMan.DebugDia("Exception during OnCollectorStateChange", e);
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
                m_onCollectionUpdated.Raise(this, new OnCollectionUpdatedArgs<T>() { StartingPosition = startingIndex, ChangedItems = updatedPosts, IsFreshUpdate = isFreshUpdate, IsInsert = isInsert });
            }
            catch (Exception e)
            {
                m_baconMan.MessageMan.DebugDia("Exception during OnCollectionUpdated", e);
            }
        }

        /// <summary>
        /// The last time the subreddit was updated, note this changes per subreddit!!
        /// </summary>
        private DateTime LastUpdateTime
        {
            get
            {
                if (m_lastUpdateTime.Equals(new DateTime(0)))
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("Collector.LastUpdateTime_" + m_uniqueId))
                    {
                        m_lastUpdateTime = m_baconMan.SettingsMan.ReadFromLocalSettings<DateTime>("Collector.LastUpdateTime_" + m_uniqueId);
                    }
                }
                return m_lastUpdateTime;
            }
            set
            {
                m_lastUpdateTime = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<DateTime>(("Collector.LastUpdateTime_" + m_uniqueId), m_lastUpdateTime);
            }
        }
        private DateTime m_lastUpdateTime = new DateTime(0);
    }
}
