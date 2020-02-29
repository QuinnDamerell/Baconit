using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using System;
using System.Collections.Generic;
using BaconBackend.Managers;

namespace BaconBackend.Helpers
{
    /// <summary>
    /// Provides data for the event of having the list of trending subreddits ready.
    /// </summary>
    public class TrendingSubsReadyEvent : EventArgs
    {
        /// <summary>
        /// The list of subreddits found that are trending.
        /// </summary>
        public List<string> TrendingSubredditsDisplayNames;
    }

    /// <summary>
    /// A helper class to determine which subreddits are trending.
    /// </summary>
    public class TrendingSubredditsHelper
    {
        /// <summary>
        /// Fired when the trending subreddits are ready.
        /// </summary>
        public event EventHandler<TrendingSubsReadyEvent> OnTrendingSubReady
        {
            add => _trendingSubReady.Add(value);
            remove => _trendingSubReady.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<TrendingSubsReadyEvent>> _trendingSubReady = new SmartWeakEvent<EventHandler<TrendingSubsReadyEvent>>();

        //
        //  Private vars
        //
        private readonly BaconManager _baconMan;
        private PostCollector _collector;

        /// <summary>
        /// Construct a new trending subreddits helper.
        /// </summary>
        /// <param name="baconMan">The reddit connection manager used to get the trending subreddits.</param>
        public TrendingSubredditsHelper(BaconManager baconMan)
        {
            _baconMan = baconMan;
        }

        /// <summary>
        /// Called when we should get the trending subs
        /// </summary>
        public void GetTrendingSubreddits()
        {
            // Check to see if we should update.
            var now = DateTime.Now;
            if(LastTrendingSubs.Count == 0 || now.Day != LastUpdate.Day || now.Month != LastUpdate.Month)
            {
                // Make the subreddit
                var trendingSub = new Subreddit
                {
                    DisplayName = "trendingsubreddits",
                    Id = "311a2",
                    Title = "Trending Subreddits",
                    PublicDescription = "Trending Subreddits",
                };

                // Get the collector
                _collector = PostCollector.GetCollector(trendingSub, _baconMan, SortTypes.New);
                _collector.OnCollectionUpdated += Collector_OnCollectionUpdated;
                _collector.OnCollectorStateChange += Collector_OnCollectorStateChange;

                // Force an update, get only one story.
                _collector.Update(true, 1);
            }
            else
            {
                // If not just fire the event now with the cached subs
                FireReadyEvent(LastTrendingSubs);
            }
        }

        /// <summary>
        /// Fired when the collector state has changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collector_OnCollectorStateChange(object sender, CollectorStateChangeArgs e)
        {
            // If we go into an error state fire the event indicate we failed.
            if(e.State == CollectorState.Error)
            {
                FireReadyEvent(new List<string>());
            }
        }

        private void Collector_OnCollectionUpdated(object sender, CollectionUpdatedArgs<Post> e)
        {
            var newTrendingSubs = new List<string>();
            if (e.ChangedItems.Count > 0)
            {
                // We got it!
                var todaysPost = e.ChangedItems[0];
                var selfText = e.ChangedItems[0].Selftext;

                // Parse out the subreddits. This isn't going to be pretty.
                // There inst any api to get these right now (that I can find)
                // so this is the best we have.
                try
                {
                    // This is so bad. The only way to really find them is to look for the ## and then **
                    // I hope they never change this or this will explode so quickly
                    var nextHash = selfText.IndexOf("##", StringComparison.Ordinal);
                    while (nextHash != -1)
                    {
                        // Find the bold indicator
                        var nextBold = selfText.IndexOf("**", nextHash, StringComparison.Ordinal);
                        if(nextBold == -1)
                        {
                            break;
                        }
                        nextBold += 2;

                        // Find the last bold indicator
                        var endBold = selfText.IndexOf("**", nextBold, StringComparison.Ordinal);
                        if (endBold == -1)
                        {
                            break;
                        }

                        // Get the subreddit
                        var subreddit = selfText.Substring(nextBold, endBold - nextBold);
                        newTrendingSubs.Add(subreddit);

                        // Update the index
                        nextHash = selfText.IndexOf("##", endBold + 2, StringComparison.Ordinal);
                    }
                }
                catch(Exception ex)
                {
                    TelemetryManager.ReportUnexpectedEvent(this, "failedtoParseTrendingPost", ex);
                    _baconMan.MessageMan.DebugDia("failed to parse trending subs post", ex);
                }
            }

            // If we get this far we are going to say this is good enough and set the
            // time and list. If our parser breaks we don't want to make a ton of requests
            // constantly.
            LastTrendingSubs = newTrendingSubs;
            LastUpdate = DateTime.Now;

            // Fire the event if we are good or not. If the list is empty that's fine
            FireReadyEvent(newTrendingSubs);
        }

        private void FireReadyEvent(List<string> newSubreddits)
        {
            try
            {
                var eventArg = new TrendingSubsReadyEvent
                {
                    TrendingSubredditsDisplayNames = newSubreddits
                };
                _trendingSubReady.Raise(this, eventArg);
            }
            catch(Exception e)
            {
                TelemetryManager.ReportUnexpectedEvent(this, "failedToFireReadyEvent", e);
                _baconMan.MessageMan.DebugDia("failed to fire trending subs ready event", e);
            }
        }

        #region Vars

        /// <summary>
        /// The last time we updated
        /// </summary>
        private DateTime LastUpdate
        {
            get
            {
                if (!_mLastUpdate.Equals(new DateTime(0))) return _mLastUpdate;
                if (_baconMan.SettingsMan.LocalSettings.ContainsKey("TrendingSubredditsHelper.LastUpdate"))
                {
                    _mLastUpdate = _baconMan.SettingsMan.ReadFromLocalSettings<DateTime>("TrendingSubredditsHelper.LastUpdate");
                }
                return _mLastUpdate;
            }
            set
            {
                _mLastUpdate = value;
                _baconMan.SettingsMan.WriteToLocalSettings("TrendingSubredditsHelper.LastUpdate", _mLastUpdate);
            }
        }
        private DateTime _mLastUpdate = new DateTime(0);


        /// <summary>
        /// The most recent trending subs we got
        /// </summary>
        private List<string> LastTrendingSubs
        {
            get
            {
                if (_mLastTrendingSubs != null) return _mLastTrendingSubs;
                _mLastTrendingSubs = _baconMan.SettingsMan.LocalSettings.ContainsKey("MessageOfTheDayManager.LastTrendingSubs") 
                    ? _baconMan.SettingsMan.ReadFromLocalSettings<List<string>>("MessageOfTheDayManager.LastTrendingSubs") 
                    : new List<string>();
                return _mLastTrendingSubs;
            }
            set
            {
                _mLastTrendingSubs = value;
                _baconMan.SettingsMan.WriteToLocalSettings("MessageOfTheDayManager.LastTrendingSubs", _mLastTrendingSubs);
            }
        }
        private List<string> _mLastTrendingSubs;

        #endregion
    }
}
