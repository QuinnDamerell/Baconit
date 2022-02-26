using BaconBackend.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaconBackend.Helpers;

namespace BaconBackend.Managers
{
    /// <summary>
    /// Event args for the OnSubredditsUpdated event.
    /// </summary>
    public class SubredditsUpdatedArgs : EventArgs
    {
        public List<Subreddit> NewSubreddits;
    }

    public class SubredditManager
    {
        /// <summary>
        /// Fired when the subreddit list updates.
        /// </summary>
        public event EventHandler<SubredditsUpdatedArgs> OnSubredditsUpdated
        {
            add => _subredditsUpdated.Add(value);
            remove => _subredditsUpdated.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<SubredditsUpdatedArgs>> _subredditsUpdated = new SmartWeakEvent<EventHandler<SubredditsUpdatedArgs>>();

        //
        // Private Vars
        //
        private readonly BaconManager _baconMan;
        private readonly object _objectLock = new object();
        private bool _isUpdateRunning;

        /// <summary>
        /// This is used as a temp cache while the app is open to look up any subreddits
        /// the user wanted to look at temporally.
        /// </summary>
        private readonly List<Subreddit> _mTempSubredditCache = new List<Subreddit>();

        public SubredditManager(BaconManager baconMan)
        {
            _baconMan = baconMan;

            _baconMan.UserMan.OnUserUpdated += UserMan_OnUserUpdated;
        }

        /// <summary>
        /// Called when the reddit subreddit list should be updated.
        /// </summary>
        /// <param name="force">Forces the update</param>
        public bool Update(bool force = false)
        {
            var timeSinceLastUpdate = DateTime.Now - LastUpdate;
            if (!force && timeSinceLastUpdate.TotalMinutes < 300 && SubredditList.Count > 0)
            {
               return false;
            }

            lock(_objectLock)
            {
                if(_isUpdateRunning)
                {
                    return false;
                }
                _isUpdateRunning = true;
            }

            // Kick off an new task
            new Task(async () =>
            {
                try
                {
                    // Get the entire list of subreddits. We will give the helper a super high limit so it
                    // will return all it can find.
                    var baseUrl = _baconMan.UserMan.IsUserSignedIn ? "/subreddits/mine.json" : "/subreddits/default.json";
                    var maxLimit = _baconMan.UserMan.IsUserSignedIn ? 99999 : 100;
                    var listHelper = new RedditListHelper<Subreddit>(baseUrl, _baconMan.NetworkMan);

                    // Get the list
                    var elements = await listHelper.FetchElements(0, maxLimit);

                    // Create a json list from the wrappers.
                    var subredditList = elements.Select(element => element.Data).ToList();

                    // Update the subreddit list
                    HandleSubredditsFromWeb(subredditList);
                    LastUpdate = DateTime.Now;
                }
                catch(Exception e)
                {
                    _baconMan.MessageMan.DebugDia("Failed to get subreddit list", e);
                }

                // Indicate we aren't running anymore
                _isUpdateRunning = false;
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
            var subreddits = SubredditList;

            // Look for the subreddit
            foreach (var subreddit in subreddits.Where(subreddit => subreddit.DisplayName.Equals(subredditDisplayName)))
            {
                return subreddit;
            }

            // Check the temp cache
            lock (_mTempSubredditCache)
            {
                foreach (var subreddit in _mTempSubredditCache.Where(subreddit => subreddit.DisplayName.Equals(subredditDisplayName)))
                {
                    return subreddit;
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
                var jsonResponse = await _baconMan.NetworkMan.MakeRedditGetRequestAsString($"/r/{displayName}/about/.json");

                // Parse the new subreddit
                foundSubreddit = await MiscellaneousHelper.ParseOutRedditDataElement<Subreddit>(_baconMan, jsonResponse);
            }
            catch (Exception e)
            {
                TelemetryManager.ReportUnexpectedEvent(this, "failed to get subreddit", e);
                _baconMan.MessageMan.DebugDia("failed to get subreddit", e);
            }

            // If we found it add it to the cache.
            if (foundSubreddit == null) return foundSubreddit;
            lock(_mTempSubredditCache)
            {
                _mTempSubredditCache.Add(foundSubreddit);
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
            var subreddits = SubredditList;

            // If it exists in the local subreddit list it is subscribed to.
            return subreddits.Any(subreddit => subreddit.DisplayName.Equals(displayName));
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
                var postData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("action", subscribe ? "sub" : "unsub"),
                    new KeyValuePair<string, string>("sr", "t5_" + subredditId)
                };

                // Make the call
                var jsonResponse = await _baconMan.NetworkMan.MakeRedditPostRequestAsString("/api/subscribe", postData);

                // Validate the response
                if (jsonResponse.Contains("{}"))
                {
                    AddRecentlyChangedSubbedSubreddit(subredditId, subscribe);
                    return true;
                }

                // Report the error
                TelemetryManager.ReportUnexpectedEvent(this, "FailedToSubscribeToSubredditWebRequestFailed");
                _baconMan.MessageMan.DebugDia("failed to subscribe / unsub subreddit, reddit returned an expected value");
                return false;
            }
            catch (Exception e)
            {
                TelemetryManager.ReportUnexpectedEvent(this, "FailedToSubscribeToSubreddit", e);
                _baconMan.MessageMan.DebugDia("failed to subscribe / unsub subreddit", e);
            }
            return false;
        }

        private void AddRecentlyChangedSubbedSubreddit(string subredditId, bool subscribe)
        {
            if (!subscribe)
            {
                // Try to find it in the list
                var removeSuccess = false;
                var currentSubs = SubredditList;
                for (var i = 0; i < currentSubs.Count; i++)
                {
                    if (!currentSubs[i].Id.Equals(subredditId)) continue;
                    currentSubs.RemoveAt(i);
                    removeSuccess = true;
                    break;
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
                lock (_mTempSubredditCache)
                {
                    foreach (var searchSub in _mTempSubredditCache.Where(searchSub => searchSub.Id.Equals(subredditId)))
                    {
                        subreddit = searchSub;
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
                    var currentSubs = SubredditList;
                    currentSubs.Add(subreddit);
                    SetSubreddits(currentSubs);
                }
            }
        }

        /// <summary>
        /// This function handles subreddits that are being parsed from the web. It will add
        /// any subreddits that need to be added
        /// </summary>
        private void HandleSubredditsFromWeb(IList<Subreddit> subreddits)
        {
            subreddits = subreddits.OrderBy(p => p.DisplayName).ToList();

            // Add the defaults
            // #todo figure out what to add here
            subreddits.Insert(0, new Subreddit
            {
                DisplayName = "frontpage",
                Title = "Your front page",
                Id = "frontpage",
                IsArtificial = true
            });
            subreddits.Insert(0, new Subreddit
            {
                DisplayName = "all",
                Title = "The top of reddit",
                Id = "all",
                IsArtificial = true
            });

            if(!_baconMan.UserMan.IsUserSignedIn)
            {
                // If the user isn't signed in add baconit, windowsphone, and windows for free!
                subreddits.Add(new Subreddit
                {
                    DisplayName = "baconit",
                    Title = "The best reddit app ever!",
                    Id = "2rfk9"
                });
                subreddits.Add(new Subreddit
                {
                    DisplayName = "windowsphone",
                    Title = "Everything Windows Phone!",
                    Id = "2r71o"
                });
                subreddits.Add(new Subreddit
                {
                    DisplayName = "windows",
                    Title = "Windows",
                    Id = "2qh3k"
                });
            }
            else
            {
                // If the user is signed in, add the saved subreddit.
                subreddits.Insert(2, new Subreddit
                {
                    DisplayName = "saved",
                    Title = "Your saved posts",
                    Id = "saved",
                    IsArtificial = true
                });
            }

            // Send them on
            SetSubreddits(subreddits);
        }

        /// <summary>
        /// Used to sort and add fix up the subreddits before they are saved.
        /// </summary>
        /// <param name="subreddits"></param>
        private void SetSubreddits(IEnumerable<Subreddit> subreddits)
        {
            var newSubredditList = new List<Subreddit>();
            foreach(var subreddit in subreddits)
            {
                // Mark if it is a favorite
                subreddit.IsFavorite = FavoriteSubreddits.ContainsKey(subreddit.Id);

                // Do a simple inert sort, account for favorites
                var wasAdded = false;
                for (var i = 0; i < newSubredditList.Count; i++)
                {
                    var addHere = false;
                    // Is this list item a favorite
                    if (newSubredditList[i].IsFavorite)
                    {
                        // If the new item isn't then continue.
                        if (!subreddit.IsFavorite)
                        {
                            continue;
                        }

                        // If they are both favorites compare them.
                        if(string.Compare(newSubredditList[i].DisplayName, subreddit.DisplayName, StringComparison.Ordinal) > 0)
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
                        if (string.Compare(newSubredditList[i].DisplayName, subreddit.DisplayName, StringComparison.Ordinal) > 0)
                        {
                            addHere = true;
                        }
                    }

                    if (!addHere) continue;
                    newSubredditList.Insert(i, subreddit);
                    wasAdded = true;
                    break;
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
            _subredditsUpdated.Raise(this, new SubredditsUpdatedArgs { NewSubreddits = SubredditList });
        }

        /// <summary>
        /// Fired when the user is updated, we should update the subreddit list.
        /// </summary>
        private void UserMan_OnUserUpdated(object sender, UserUpdatedArgs args)
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
                if (_subredditList != null) return _subredditList;
                if(_baconMan.SettingsMan.LocalSettings.ContainsKey("SubredditManager.SubredditList"))
                {
                    _subredditList = 
                        _baconMan
                            .SettingsMan
                            .ReadFromLocalSettings<List<Subreddit>>("SubredditManager.SubredditList");
                }
                else
                {
                    _subredditList = new List<Subreddit>();

                    // We don't need this 100%, but when the app first
                    // opens we won't have the subreddit yet so this prevents
                    // us from grabbing the sub from the internet.
                    var subreddit = new Subreddit
                    {
                        DisplayName = "frontpage",
                        Title = "Your front page",
                        Id = "frontpage"
                    };
                    _subredditList.Add(subreddit);
                }
                return _subredditList;
            }
            private set
            {
                _subredditList = value;
                _baconMan.SettingsMan.WriteToLocalSettings("SubredditManager.SubredditList", _subredditList);
            }
        }
        private List<Subreddit> _subredditList;

        /// <summary>
        /// The last time the subreddit was updated
        /// </summary>
        public DateTime LastUpdate
        {
            get
            {
                if (!_lastUpdated.Equals(new DateTime(0))) return _lastUpdated;
                if (_baconMan.SettingsMan.LocalSettings.ContainsKey("SubredditManager.LastUpdate"))
                {
                    _lastUpdated = _baconMan.SettingsMan.ReadFromLocalSettings<DateTime>("SubredditManager.LastUpdate");
                }
                return _lastUpdated;
            }
            private set
            {
                _lastUpdated = value;
                _baconMan.SettingsMan.WriteToLocalSettings("SubredditManager.LastUpdate", _lastUpdated);
            }
        }
        private DateTime _lastUpdated = new DateTime(0);

        private void SaveSettings()
        {
            // For lists we need to set the list again so the setter is called.
            SubredditList = _subredditList;
            FavoriteSubreddits = _favoriteSubreddits;
        }

        /// <summary>
        /// A dictionary of all favorite subreddits
        /// </summary>
        private Dictionary<string, bool> FavoriteSubreddits
        {
            get
            {
                if (_favoriteSubreddits != null) return _favoriteSubreddits;
                if (_baconMan.SettingsMan.RoamingSettings.ContainsKey("SubredditManager.FavoriteSubreddits"))
                {
                    _favoriteSubreddits = _baconMan.SettingsMan.ReadFromRoamingSettings<Dictionary<string, bool>>("SubredditManager.FavoriteSubreddits");
                }
                else
                {
                    _favoriteSubreddits = new Dictionary<string, bool>
                    {
                        {"frontpage", true}, {"all", true}, {"2rfk9", true}, {"2r71o", true}
                    };
                    // Add the presets
                    // Baconit
                    // Windows phone
                }
                return _favoriteSubreddits;
            }
            set
            {
                _favoriteSubreddits = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("SubredditManager.FavoriteSubreddits", _favoriteSubreddits);
            }
        }
        private Dictionary<string, bool> _favoriteSubreddits;
    }
}

