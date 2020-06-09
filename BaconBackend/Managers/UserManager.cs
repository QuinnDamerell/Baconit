using BaconBackend.DataObjects;
using BaconBackend.Helpers;
using System;
using System.Threading.Tasks;

namespace BaconBackend.Managers
{
    // Indicates what action happened.
    public enum UserCallbackAction
    {
        Added,
        Updated,
        Removed
    }

    /// <summary>
    /// The event args for the OnUserUpdated event.
    /// </summary>
    public class UserUpdatedArgs : EventArgs
    {
        public UserCallbackAction Action;
    }

    public class UserManager
    {        
        /// <summary>
        /// Fired when the user is updated, added, or removed
        /// </summary>
        public event EventHandler<UserUpdatedArgs> OnUserUpdated
        {
            add => _userUpdated.Add(value);
            remove => _userUpdated.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<UserUpdatedArgs>> _userUpdated = new SmartWeakEvent<EventHandler<UserUpdatedArgs>>();

        /// <summary>
        /// This class is used to represent a sign in request. 
        /// </summary>
        public class SignInResult
        {
            public bool WasSuccess;
            public bool WasErrorNetwork = false;
            public bool WasUserCanceled = false;
            public string Message = "";
        }

        private readonly BaconManager _baconMan;
        public readonly AuthManager AuthManager;

        public UserManager(BaconManager baconMan)
        {
            _baconMan = baconMan;
            AuthManager = new AuthManager(_baconMan);
        }

        /// <summary>
        /// Returns the current user's access token
        /// </summary>
        public async Task<string> GetAccessToken()
        {
            return await AuthManager.GetAccessToken();
        }

        public string GetAuthUrl(string nonce)
        {
            return AuthManager.GetAuthRequestString(nonce);
        }

        public async Task<SignInResult> SignInNewUser()
        {
            // Start the process by trying to auth a new user
            var result = await AuthManager.AuthNewUser();

            // Check the result
            if(!result.WasSuccess)
            {
                return result;
            }

            // Try to get the new user
            result = await InternalUpdateUser();

            // Return the result
            return result;
        }

        public bool UpdateUser(bool forceUpdate = false)
        {
            var timeSinceLateUpdate = DateTime.Now - LastUpdate;
            if(timeSinceLateUpdate.TotalHours < 24 && !forceUpdate)
            {
                return false;
            }

            // Kick of a new task to update the user.
            new Task(async ()=> { await InternalUpdateUser(); }).Start();

            return true;
        }

        public void SignOut()
        {
            // Remove the auth
            AuthManager.DeleteCurrentAuth();

            // Rest the current user object
            CurrentUser = null;
            LastUpdate = new DateTime(0);

            // Fire the user changed callback
            FireOnUserUpdated(UserCallbackAction.Removed);
        }

        public async Task<SignInResult> InternalUpdateUser()
        {
            try
            {
                // Record if we had a user
                var lastUserName = CurrentUser == null ? "" : CurrentUser.Name;

                // Make the web call
                var response = await _baconMan.NetworkMan.MakeRedditGetRequest("/api/v1/me/.json");

                // Parse the user
                var user = await NetworkManager.DeserializeObject<User>(response);

                // Set the new user
                CurrentUser = user;

                // Tell our listeners, but ignore any errors from them
                FireOnUserUpdated(lastUserName == CurrentUser.Name ? UserCallbackAction.Updated : UserCallbackAction.Added);
            }
            catch (Exception e)
            {
                _baconMan.MessageMan.DebugDia("Failed to parse user", e);
                return new SignInResult
                {
                    Message = "Failed to parse user"
                };
            }

            return new SignInResult
            {
                WasSuccess = true
            };
        }

        public void UpdateUnReadMessageCount(int unreadMessages)
        {
            if (CurrentUser == null) return;
            // Update
            CurrentUser.HasMail = unreadMessages != 0;
            CurrentUser.InboxCount = unreadMessages;
            // Force a save
            CurrentUser = CurrentUser;
            // Fire the callback
            FireOnUserUpdated(UserCallbackAction.Updated);
        }

        private void FireOnUserUpdated(UserCallbackAction action)
        {
            // Tell our listeners, but ignore any errors from them
            try
            {
                _userUpdated.Raise(this, new UserUpdatedArgs { Action = action });
            }
            catch (Exception ex)
            {
                _baconMan.MessageMan.DebugDia("Failed to notify user listener of update", ex);
                TelemetryManager.ReportUnexpectedEvent(this, "failed to fire OnUserUpdated", ex);
            }
        }

        /// <summary>
        /// Returns is a current user exists
        /// </summary>
        public bool IsUserSignedIn => AuthManager.IsUserSignedIn;

        /// <summary>
        /// Holds the current user information
        /// </summary>
        public User CurrentUser
        {
            get
            {
                if (_currentUser != null) return _currentUser;
                _currentUser = _baconMan.SettingsMan.RoamingSettings.ContainsKey("UserManager.CurrentUser") 
                    ? _baconMan.SettingsMan.ReadFromRoamingSettings<User>("UserManager.CurrentUser") 
                    : null;
                return _currentUser;
            }
            private set
            {
                _currentUser = value;
                _baconMan.SettingsMan.WriteToRoamingSettings("UserManager.CurrentUser", _currentUser);
            }
        }
        private User _currentUser;

        /// <summary>
        /// The last time the user was updated
        /// </summary>
        private DateTime LastUpdate
        {
            get
            {
                if (!_lastUpdated.Equals(new DateTime(0))) return _lastUpdated;
                if (_baconMan.SettingsMan.LocalSettings.ContainsKey("UserManager.LastUpdate"))
                {
                    _lastUpdated = _baconMan.SettingsMan.ReadFromLocalSettings<DateTime>("UserManager.LastUpdate");
                }
                return _lastUpdated;
            }
            set
            {
                _lastUpdated = value;
                _baconMan.SettingsMan.WriteToLocalSettings("UserManager.LastUpdate", _lastUpdated);
            }
        }
        private DateTime _lastUpdated = new DateTime(0);
    }
}
