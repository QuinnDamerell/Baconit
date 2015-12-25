using BaconBackend.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using BaconBackend.Helpers;
using System.Net;

namespace BaconBackend.Managers
{
    /// <summary>
    /// Event args for the OnSubredditsUpdated event.
    /// </summary>
    public class OnSubredditsUpdatedArgs : EventArgs
    {
        public List<Subreddit> NewSubreddits;
    }

    public class SubredditManager
    {
        /// <summary>
        /// Fired when the subreddit list updates.
        /// </summary>
        public event EventHandler<OnSubredditsUpdatedArgs> OnSubredditsUpdated
        {
            add { m_onSubredditsUpdated.Add(value); }
            remove { m_onSubredditsUpdated.Remove(value); }
        }
        SmartWeakEvent<EventHandler<OnSubredditsUpdatedArgs>> m_onSubredditsUpdated = new SmartWeakEvent<EventHandler<OnSubredditsUpdatedArgs>>();

        //
        // Private Vars
        //
        BaconManager m_baconMan;
        object objectLock = new object();
        bool m_isUpdateRunning = false;

        /// <summary>
        /// This is used as a temp cache while the app is open to look up any subreddits
        /// the user wanted to look at temporally.
        /// </summary>
        List<Subreddit> m_tempSubredditCache = new List<Subreddit>();

        public SubredditManager(BaconManager baconMan)
        {
            m_baconMan = baconMan;

            m_baconMan.UserMan.OnUserUpdated += UserMan_OnUserUpdated;
        }

        /// <summary>
        /// Called when the reddit subreddit list should be updated.
        /// </summary>
        /// <param name="force">Forces the update</param>
        public bool Update(bool force = false)
        {
            TimeSpan timeSinceLastUpdate = DateTime.Now - LastUpdate;
            if (!force && timeSinceLastUpdate.TotalMinutes < 300 && SubredditList.Count > 0)
            {
               return false;
            }

            lock(objectLock)
            {
                if(m_isUpdateRunning)
                {
                    return false;
                }
                m_isUpdateRunning = true;
            }

            // Kick off an new task
            new Task(async () =>
            {
                try
                {
                    // Get the entire list of subreddits. We will give the helper a super high limit so it
                    // will return all it can find.
                    string baseUrl = m_baconMan.UserMan.IsUserSignedIn ? "/subreddits/mine.json" : "/subreddits/default.json";
                    int maxLimit = m_baconMan.UserMan.IsUserSignedIn ? 99999 : 100;
                    RedditListHelper <Subreddit> listHelper = new RedditListHelper<Subreddit>(baseUrl, m_baconMan.NetworkMan);

                    // Get the list
                    List<Element<Subreddit>> elements = await listHelper.FetchElements(0, maxLimit);

                    // Create a json list from the wrappers.
                    List<Subreddit> subredditList = new List<Subreddit>();
                    foreach(Element<Subreddit> element in elements)
                    {
                        subredditList.Add(element.Data);
                    }

                    // Update the subreddit list
                    HandleSubredditsFromWeb(subredditList);
                    LastUpdate = DateTime.Now;
                }
                catch(Exception e)
                {
                    m_baconMan.MessageMan.DebugDia("Failed to get subreddit list", e);
                }

                // Indicate we aren't running anymore
                m_isUpdateRunning = false;
            }).Start();
            return true;
        }

        public void SetFavorite(string redditId, bool isAdding)
        {
            if(isAdding && FavoriteSubreddits.ContainsKey(redditId))
            {
                // We already have it
                return;
            }

            if(!isAdding && !FavoriteSubreddits.ContainsKey(redditId))
            {
                // We already don't have it
                return;
            }

            if(isAdding)
            {
                // Add it to the map
                FavoriteSubreddits[redditId] = true;
            }
            else
            {
                FavoriteSubreddits.Remove(redditId);
            }

            // Force the settings to save
            SaveSettings();

            SetSubreddits(SubredditList);
        }

        /// <summary>
        /// Returns the given subreddit by display name
        /// </summary>
        /// <param name="subredditDisplayName"></param>
        /// <returns></returns>
        public Subreddit GetSubredditByDisplayName(string subredditDisplayName)
        {
            // Grab a local copy
            List<Subreddit> subreddits = SubredditList;

            // Look for the subreddit
            foreach (Subreddit subreddit in subreddits)
            {
                if (subreddit.DisplayName.Equals(subredditDisplayName))
                {
                    return subreddit;
                }
            }

            // Check the temp cache
            lock (m_tempSubredditCache)
            {
                foreach (Subreddit subreddit in m_tempSubredditCache)
                {
                    if (subreddit.DisplayName.Equals(subredditDisplayName))
                    {
                        return subreddit;
                    }
                }
            }

            // We don't have it.
            return null;
        }

        /// <summary>
        /// Tries to get a subreddit from the web by the display name.
        /// </summary>
        /// <returns>Returns null if the subreddit get fails.</returns>
        public async Task<Subreddit> GetSubredditFromWebByDisplayName(string displayName)
        {
            Subreddit foundSubreddit = null;
            try
            {
                // Make the call
                string jsonResponse = await m_baconMan.NetworkMan.MakeRedditGetRequestAsString($"/r/{displayName}/about/.json");

                // Try to parse out the subreddit
                string subredditData = MiscellaneousHelper.ParseOutRedditDataElement(jsonResponse);
                if(subredditData == null)
                {
                    throw new Exception("Failed to parse out data object");
                }

                // Parse the new subreddit
                foundSubreddit = await Task.Run(() => JsonConvert.DeserializeObject<Subreddit>(subredditData));
            }
            catch (Exception e)
            {
                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "failed to get subreddit", e);
                m_baconMan.MessageMan.DebugDia("failed to get subreddit", e);
            }

            // If we found it add it to the cache.
            if(foundSubreddit != null)
            {
                lock(m_tempSubredditCache)
                {
                    m_tempSubredditCache.Add(foundSubreddit);
                }

                // Format the subreddit
                foundSubreddit.Description = WebUtility.HtmlDecode(foundSubreddit.Description);
            }        

            return foundSubreddit;
        }

        /// <summary>
        /// Checks to see if a subreddit is subscribed to by the user or not.
        /// </summary>
        /// <param name="displayName"></param>
        /// <returns></returns>
        public bool IsSubredditSubscribedTo(string displayName)
        {
            // Grab a local copy
            List<Subreddit> subreddits = SubredditList;

            // If it exists in the local subreddit list it is subscribed to.
            foreach (Subreddit subreddit in subreddits)
            {
                if (subreddit.DisplayName.Equals(displayName))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Subscribes or unsubscribes to a subreddit
        /// </summary>
        /// <param name="subredditId"></param>
        /// <param name="subscribe"></param>
        public async Task<bool> ChangeSubscriptionStatus(string subredditId, bool subscribe)
        {
            try
            {
                // Build the data to send
                List<KeyValuePair<string, string>> postData = new List<KeyValuePair<string, string>>();
                postData.Add(new KeyValuePair<string, string>("action", subscribe ? "sub" : "unsub"));
                postData.Add(new KeyValuePair<string, string>("sr", "t5_"+subredditId));

                // Make the call
                string jsonResponse = await m_baconMan.NetworkMan.MakeRedditPostRequestAsString($"/api/subscribe", postData);

                // Validate the response
                if (jsonResponse.Contains("{}"))
                {
                    AddRecentlyChangedSubedSubreddit(subredditId, subscribe);
                    return true;
                }

                // Report the error
                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToSubscribeToSubredditWebRequestFailed");
                m_baconMan.MessageMan.DebugDia("failed to subscribe / unsub subreddit, reddit returned an expected value");
                return false;
            }
            catch (Exception e)
            {
                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "FailedToSubscribeToSubreddit", e);
                m_baconMan.MessageMan.DebugDia("failed to subscribe / unsub subreddit", e);
            }
            return false;
        }

        private void AddRecentlyChangedSubedSubreddit(string subredditId, bool subscribe)
        {
            if (!subscribe)
            {
                // Try to find it in the list
                bool removeSuccess = false;
                List<Subreddit> currentSubs = SubredditList;
                for (int i = 0; i < currentSubs.Count; i++)
                {
                    if (currentSubs[i].Id.Equals(subredditId))
                    {
                        currentSubs.RemoveAt(i);
                        removeSuccess = true;
                        break;
                    }
                }

                // If successful reset the new list
                if (removeSuccess)
                {
                    SetSubreddits(currentSubs);
                }
            }
            else
            {
                // If we just subscribed to a new subreddit...
                // Try to find the subreddit in our temp subreddits.
                Subreddit subreddit = null;
                lock (m_tempSubredditCache)
                {
                    foreach (Subreddit serachSub in m_tempSubredditCache)
                    {
                        if (serachSub.Id.Equals(subredditId))
                        {
                            subreddit = serachSub;
                        }
                    }
                }

                if (subreddit == null)
                {
                    // If we can't find it just force an update.
                    Update(true);
                }
                else
                {
                    // Otherwise add it locally
                    List<Subreddit> currentSubs = SubredditList;
                    currentSubs.Add(subreddit);
                    SetSubreddits(currentSubs);
                }
            }
        }

        /// <summary>
        /// This function handles subreddits that are being parsed from the web. It will add
        /// any subreddits that need to be added
        /// </summary>
        private void HandleSubredditsFromWeb(List<Subreddit> subreddits)
        {
            // Add the defaults
            // #todo figure out what to add here
            Subreddit subreddit = new Subreddit()
            {
                DisplayName = "all",
                Title = "The top of reddit",
                Id = "all",
                IsArtifical = true
            };
            subreddits.Add(subreddit);
            subreddit = new Subreddit()
            {
                DisplayName = "frontpage",
                Title = "Your front page",
                Id = "frontpage",
                IsArtifical = true
            };
            subreddits.Add(subreddit);

            if(!m_baconMan.UserMan.IsUserSignedIn)
            {
                // If the user isn't signed in add baconit, windowsphone, and windows for free!
                subreddit = new Subreddit()
                {
                    DisplayName = "baconit",
                    Title = "The best reddit app ever!",
                    Id = "2rfk9"
                };
                subreddits.Add(subreddit);
                subreddit = new Subreddit()
                {
                    DisplayName = "windowsphone",
                    Title = "Everything Windows Phone!",
                    Id = "2r71o"
                };
                subreddits.Add(subreddit);
                subreddit = new Subreddit()
                {
                    DisplayName = "windows",
                    Title = "Windows",
                    Id = "2qh3k"
                };
                subreddits.Add(subreddit);
            }
            else
            {
                // If the user is signed in, add the saved subreddit.
                subreddit = new Subreddit()
                {
                    DisplayName = "saved",
                    Title = "Your saved posts",
                    Id = "saved",
                    IsArtifical = true
                };
                subreddits.Add(subreddit);
            }

            // Send them on
            SetSubreddits(subreddits);
        }

        /// <summary>
        /// Used to sort and add fix up the subreddits before they are saved.
        /// </summary>
        /// <param name="subreddits"></param>
        private void SetSubreddits(List<Subreddit> subreddits)
        {
            List<Subreddit> newSubredditList = new List<Subreddit>();
            foreach(Subreddit subreddit in subreddits)
            {
                // Mark if it is a favorite
                subreddit.IsFavorite = FavoriteSubreddits.ContainsKey(subreddit.Id);

                // Escape the description, we need to do it twice because sometimes it is double escaped.
                subreddit.Description = WebUtility.HtmlDecode(subreddit.Description);

                // Do a simple inert sort, account for favorites
                bool wasAdded = false;
                for (int i = 0; i < newSubredditList.Count; i++)
                {
                    bool addHere = false;
                    // Is this list item a favorite
                    if (newSubredditList[i].IsFavorite)
                    {
                        // If the new item isn't then continue.
                        if (!subreddit.IsFavorite)
                        {
                            continue;
                        }

                        // If they are both favorites compare them.
                        if(newSubredditList[i].DisplayName.CompareTo(subreddit.DisplayName) > 0)
                        {
                            addHere = true;
                        }
                    }
                    // Or if the new one is a favorite only
                    else if (subreddit.IsFavorite)
                    {
                        addHere = true;
                    }
                    // If neither of them are favorites.
                    else
                    {
                        if (newSubredditList[i].DisplayName.CompareTo(subreddit.DisplayName) > 0)
                        {
                            addHere = true;
                        }
                    }

                    if(addHere)
                    {
                        newSubredditList.Insert(i, subreddit);
                        wasAdded = true;
                        break;

                    }
                }

                // If we didn't add it add it to the end.
                if (!wasAdded)
                {
                    newSubredditList.Add(subreddit);
                }
            }

            // Set the list
            SubredditList = newSubredditList;

            // Save the list
            SaveSettings();

            // Fire the callback for listeners
            m_onSubredditsUpdated.Raise(this, new OnSubredditsUpdatedArgs() { NewSubreddits = SubredditList });
        }

        /// <summary>
        /// Fired when the user is updated, we should update the subreddit list.
        /// </summary>
        private void UserMan_OnUserUpdated(object sender, OnUserUpdatedArgs args)
        {
            // Take action on everything but user updated
            if (args.Action != UserCallbackAction.Updated)
            {
                Update(true);
            }
        }

        /// <summary>
        /// The current list of subreddits.
        /// </summary>
        public List<Subreddit> SubredditList
        {
            get
            {
                if(m_subredditList == null)
                {
                    if(m_baconMan.SettingsMan.LocalSettings.ContainsKey("SubredditManager.SubredditList"))
                    {
                        m_subredditList = m_baconMan.SettingsMan.ReadFromLocalSettings<List<Subreddit>>("SubredditManager.SubredditList");
                    }
                    else
                    {
                        m_subredditList = new List<Subreddit>();

                        // We don't need this 100%, but when the app first
                        // opens we won't have the subreddit yet so this prevents
                        // us from grabbing the sub from the internet.
                        Subreddit subreddit = new Subreddit()
                        {
                            DisplayName = "frontpage",
                            Title = "Your front page",
                            Id = "frontpage"
                        };
                        m_subredditList.Add(subreddit);
                    }
                }
                return m_subredditList;
            }
            private set
            {
                m_subredditList = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<List<Subreddit>>("SubredditManager.SubredditList", m_subredditList);
            }
        }
        private List<Subreddit> m_subredditList = null;

        /// <summary>
        /// The last time the subreddit was updated
        /// </summary>
        public DateTime LastUpdate
        {
            get
            {
                if (m_lastUpdated.Equals(new DateTime(0)))
                {
                    if (m_baconMan.SettingsMan.LocalSettings.ContainsKey("SubredditManager.LastUpdate"))
                    {
                        m_lastUpdated = m_baconMan.SettingsMan.ReadFromLocalSettings<DateTime>("SubredditManager.LastUpdate");
                    }
                }
                return m_lastUpdated;
            }
            private set
            {
                m_lastUpdated = value;
                m_baconMan.SettingsMan.WriteToLocalSettings<DateTime>("SubredditManager.LastUpdate", m_lastUpdated);
            }
        }
        private DateTime m_lastUpdated = new DateTime(0);

        private void SaveSettings()
        {
            // For lists we need to set the list again so the setter is called.
            SubredditList = m_subredditList;
            FavoriteSubreddits = m_favoriteSubreddits;
        }

        /// <summary>
        /// A dictionary of all favorite subreddits
        /// </summary>
        private Dictionary<string, bool> FavoriteSubreddits
        {
            get
            {
                if (m_favoriteSubreddits == null)
                {
                    if (m_baconMan.SettingsMan.RoamingSettings.ContainsKey("SubredditManager.FavoriteSubreddits"))
                    {
                        m_favoriteSubreddits = m_baconMan.SettingsMan.ReadFromRoamingSettings<Dictionary<string, bool>>("SubredditManager.FavoriteSubreddits");
                    }
                    else
                    {
                        m_favoriteSubreddits = new Dictionary<string, bool>();
                        // Add the presets
                        m_favoriteSubreddits.Add("frontpage", true);
                        m_favoriteSubreddits.Add("all", true);
                        m_favoriteSubreddits.Add("2rfk9", true); // Baconit
                        m_favoriteSubreddits.Add("2r71o", true); // Windows phone
                    }
                }
                return m_favoriteSubreddits;
            }
            set
            {
                m_favoriteSubreddits = value;
                m_baconMan.SettingsMan.WriteToRoamingSettings<Dictionary<string, bool>>("SubredditManager.FavoriteSubreddits", m_favoriteSubreddits);
            }
        }
        private Dictionary<string, bool> m_favoriteSubreddits = null;
    }
}

