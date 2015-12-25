using BaconBackend.Collectors;
using BaconBackend.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            add { m_onTrendingSubReady.Add(value); }
            remove { m_onTrendingSubReady.Remove(value); }
        }
        SmartWeakEvent<EventHandler<TrendingSubsReadyEvent>> m_onTrendingSubReady = new SmartWeakEvent<EventHandler<TrendingSubsReadyEvent>>();

        //
        //  Private vars
        //
        BaconManager m_baconMan;
        PostCollector m_collector;

        /// <summary>
        /// Construct a new trending subreddits helper.
        /// </summary>
        /// <param name="baconMan">The reddit connection manager used to get the trending subreddits.</param>
        public TrendingSubredditsHelper(BaconManager baconMan)
        {
            m_baconMan = baconMan;
        }

        /// <summary>
        /// Called when we should get the trending subs
        /// </summary>
        public void GetTrendingSubreddits()
        {
            // Check to see if we should update.
            DateTime now = DateTime.Now;
            if(LastTrendingSubs.Count == 0 || now.Day != LastUpdate.Day || now.Month != LastUpdate.Month)
            {
                // Make the subreddit
                Subreddit trendingSub = new Subreddit()
                {
                    DisplayName = "trendingsubreddits",
                    Id = "311a2",
                    Title = "Trending Subreddits",
                    PublicDescription = "Trending Subreddits",
                };

                // Get the collector
                m_collector = PostCollector.GetCollector(trendingSub, m_baconMan, SortTypes.New);
                m_collector.OnCollectionUpdated += Collector_OnCollectionUpdated;
                m_collector.OnCollectorStateChange += Collector_OnCollectorStateChange;

                // Force an update, get only one story.
                m_collector.Update(true, 1);
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
        private void Collector_OnCollectorStateChange(object sender, OnCollectorStateChangeArgs e)
        {
            // If we go into an error state fire the event indicate we failed.
            if(e.State == CollectorState.Error)
            {
                FireReadyEvent(new List<string>());
            }
        }

        private void Collector_OnCollectionUpdated(object sender, OnCollectionUpdatedArgs<Post> e)
        {
            List<string> newTrendingSubs = new List<string>();
            if (e.ChangedItems.Count > 0)
            {
                // We got it!
                Post todaysPost = e.ChangedItems[0];
                string selfText = e.ChangedItems[0].Selftext;

                // Parse out the subreddits. This isn't going to be pretty.
                // There inst any api to get these right now (that I can find)
                // so this is the best we have.
                try
                {
                    // This is so bad. The only way to really find them is to look for the ## and then **
                    // I hope they never change this or this will explode so quickly
                    int nextHash = selfText.IndexOf("##");
                    while (nextHash != -1)
                    {
                        // Find the bold indicator
                        int nextBold = selfText.IndexOf("**", nextHash);
                        if(nextBold == -1)
                        {
                            break;
                        }
                        nextBold += 2;

                        // Find the last bold indicator
                        int endBold = selfText.IndexOf("**", nextBold);
                        if (endBold == -1)
                        {
                            break;
                        }

                        // Get the subreddit
                        string subreddit = selfText.Substring(nextBold, endBold - nextBold);
                        newTrendingSubs.Add(subreddit);

                        // Update the index
                        nextHash = selfText.IndexOf("##", endBold + 2);
                    }
                }
                catch(Exception ex)
                {
                    m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "failedtoParseTrendingPost", ex);
                    m_baconMan.MessageMan.DebugDia("failed to parse trending subs post", ex);
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
                TrendingSubsReadyEvent eventArg = new TrendingSubsReadyEvent()
                {
                    TrendingSubredditsDisplayNames = newSubreddits
                };
                m_onTrendingSubReady.Raise(this, eventArg);
            }
            catch(Exception e)
            {
                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "failedToFireReadyEvent", e);
                m_baconMan.MessageMan.DebugDia("failed to fire trending subs ready event", e);
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
                if (m_lastUpdate.Equals(new DateTime(0)))
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("TrendingSubredditsHelper.LastUpdate"))
                    {
                        m_lastUpdate = m_baconMan.SettingsMan.ReadFromLocalSettings<DateTime>("TrendingSubredditsHelper.LastUpdate");
                    }
                }
                return m_lastUpdate;
            }
            set
            {
                m_lastUpdate = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<DateTime>("TrendingSubredditsHelper.LastUpdate", m_lastUpdate);
            }
        }
        private DateTime m_lastUpdate = new DateTime(0);


        /// <summary>
        /// The most recent trending subs we got
        /// </summary>
        private List<string> LastTrendingSubs
        {
            get
            {
                if (m_lastTrendingSubs == null)
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("MessageOfTheDayManager.LastTrendingSubs"))
                    {
                        m_lastTrendingSubs = m_baconMan.SettingsMan.ReadFromLocalSettings<List<string>>("MessageOfTheDayManager.LastTrendingSubs");
                    }
                    else
                    {
                        m_lastTrendingSubs = new List<string>();
                    }
                }
                return m_lastTrendingSubs;
            }
            set
            {
                m_lastTrendingSubs = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<List<string>>("MessageOfTheDayManager.LastTrendingSubs", m_lastTrendingSubs);
            }
        }
        private List<string> m_lastTrendingSubs = null;

        #endregion
    }
}
