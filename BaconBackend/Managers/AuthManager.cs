using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Authentication.Web;

namespace BaconBackend.Managers
{
#pragma warning disable CS0649

    public class AuthManager
    {
        private const string BaconitAppId = "EU5cWoJCJ5HcPQ";
        private const string BaconitRedirectUrl = "http://www.quinndamerell.com/Baconit/OAuth/Auth.php";

        private class AccessTokenResult
        {
            [JsonProperty(PropertyName="access_token")]
            public string AccessToken;

            [JsonProperty(PropertyName = "token_type")]
            public string TokenType;

            [JsonProperty(PropertyName = "expires_in")]
            public string ExpiresIn;

            [JsonProperty(PropertyName = "refresh_token")]
            public string RefreshToken;

            [JsonProperty(PropertyName = "ExpiresAt")]
            public DateTime ExpiresAt;
        }

        private readonly BaconManager _mBaconMan;
        private readonly ManualResetEvent _mRefreshEvent = new ManualResetEvent(false);
        private bool _mIsTokenRefreshing;
        private bool _mTokenRefreshFailed;

        public AuthManager(BaconManager baconMan)
        {
            _mBaconMan = baconMan;
        }

        /// <summary>
        /// Authenticates a new user.
        /// </summary>
        /// <returns></returns>
        public async Task<UserManager.SignInResult> AuthNewUser()
        {
            // Try to get the request token
            var result = await GetRedditRequestToken();
            if(!result.WasSuccess)
            {
                return result;
            }

            // Try to get the access token
            var accessToken = await RefreshAccessToken(result.Message, false);
            if(accessToken == null)
            {
                return new UserManager.SignInResult
                {
                    Message = "Failed to get access token"
                };
            }

            return new UserManager.SignInResult
            {
                WasSuccess = true
            };
        }

        /// <summary>
        /// Get the access token. If there is no access token it will return null, if the token needs to be refreshed
        /// it will execute the logic to do so.
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetAccessToken()
        {
            // Make sure we have data
            if (AccessTokenData == null)
            {
                return null;
            }

            var shouldRefresh = false;
            var shouldBlock = false;
            lock (_mRefreshEvent)
            {
                var timeRemaining = AccessTokenData.ExpiresAt - DateTime.Now;

                // If it is already expired or will do so soon wait on the refresh before using it.
                if (timeRemaining.TotalSeconds < 30 || _mTokenRefreshFailed)
                {
                    // Check if someone else it refreshing
                    if (!_mIsTokenRefreshing)
                    {
                        _mIsTokenRefreshing = true;
                        shouldRefresh = true;
                    }
                    shouldBlock = true;
                }
                // If it is going to expire soon but not too soon refresh it async.
                else if (timeRemaining.TotalMinutes < 5)
                {
                    // Check if someone else it refreshing
                    if (!_mIsTokenRefreshing)
                    {
                        _mIsTokenRefreshing = true;
                        shouldRefresh = true;
                    }
                }
            }

            // If we should refresh kick off a task to do so.
            if (shouldRefresh)
            {
                new Task(async () =>
                {
                    // Try to refresh
                    try
                    {
                        var result = await RefreshAccessToken(AccessTokenData.RefreshToken, true);
                        _mTokenRefreshFailed = result == null;
                    }
                    catch (Exception e)
                    {
                        _mBaconMan.MessageMan.DebugDia("Failed to auto refresh token", e);
                        _mTokenRefreshFailed = true;
                    }

                    // Lock the
                    lock (_mRefreshEvent)
                    {
                        _mIsTokenRefreshing = false;
                        _mRefreshEvent.Set();
                    }

                }).Start();
            }

            // Block if we need to block
            if (shouldBlock)
            {
                // Wait on a task that will wait on the event.
                await Task.Run(() =>
                {
                    _mRefreshEvent.WaitOne();
                });
            }

            // Now return the key.
            return _mTokenRefreshFailed ? null : AccessTokenData.AccessToken;
        }

        /// <summary>
        /// Removes the current Auth.
        /// </summary>
        public void DeleteCurrentAuth()
        {
            AccessTokenData = null;
        }

        /// <summary>
        /// Gets a requests token from reddit
        /// </summary>
        /// <returns></returns>
        private async Task<UserManager.SignInResult> GetRedditRequestToken()
        {
            // Create a random number
            var nonce = GetNonce();

            // Create the nav string
            var tokenRequestString = "https://reddit.com/api/v1/authorize.compact?"
                                     + "client_id=" + BaconitAppId
                                     + "&response_type=code"
                                     + "&state=" + nonce
                                     + "&redirect_uri=" + BaconitRedirectUrl
                                     + "&duration=permanent"
                                     + "&scope=modcontributors,modconfig,subscribe,wikiread,wikiedit,vote,mysubreddits,submit,"
                                     + "modlog,modposts,modflair,save,modothers,read,privatemessages,report,identity,livemanage,"
                                     + "account,modtraffic,edit,modwiki,modself,history,flair";

            try
            {
                var start = new Uri(tokenRequestString, UriKind.Absolute);
                var end = new Uri(BaconitRedirectUrl, UriKind.Absolute);
                var result = await WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.None, start, end);

                switch (result.ResponseStatus)
                {
                    case WebAuthenticationStatus.Success:
                    {
                        // Woot we got it! Parse out the token
                        var startOfState = result.ResponseData.IndexOf("state=", StringComparison.Ordinal) + 6;
                        var endOfState = result.ResponseData.IndexOf("&", startOfState, StringComparison.Ordinal);
                        var startOfCode = result.ResponseData.IndexOf("code=", StringComparison.Ordinal) + 5;
                        var endOfCode = result.ResponseData.IndexOf("&", startOfCode, StringComparison.Ordinal);

                        // Make sure we found a code (result is -1 + 5 = 4)
                        if(startOfCode == 4)
                        {
                            return new UserManager.SignInResult
                            {
                                Message = "Reddit returned an error!"
                            };
                        }

                        // Check for the ends
                        endOfCode = endOfCode == -1 ? result.ResponseData.Length : endOfCode;
                        endOfState = endOfState == -1 ? result.ResponseData.Length : endOfState;

                        var state = result.ResponseData.Substring(startOfState, endOfState - startOfState);
                        var code = result.ResponseData.Substring(startOfCode, endOfCode - startOfCode);

                        // Check the state
                        if(nonce != state)
                        {
                            return new UserManager.SignInResult
                            {
                                Message = "The state is not the same!"
                            };
                        }

                        // Check the code
                        if(string.IsNullOrWhiteSpace(code))
                        {
                            return new UserManager.SignInResult
                            {
                                Message = "The code is empty!"
                            };
                        }

                        // Return the code!
                        return new UserManager.SignInResult
                        {
                            WasSuccess = true,
                            Message = code
                        };
                    }
                    case WebAuthenticationStatus.ErrorHttp:
                        return new UserManager.SignInResult
                        {
                            WasErrorNetwork = true
                        };
                    case WebAuthenticationStatus.UserCancel:
                        return new UserManager.SignInResult
                        {
                            WasUserCanceled = true
                        };
                    default:
                        return new UserManager.SignInResult
                        {
                            WasUserCanceled = true
                        };
                }
            }
            catch(Exception e)
            {
                _mBaconMan.MessageMan.DebugDia("Failed to get request token", e);
                return new UserManager.SignInResult
                {
                    WasErrorNetwork = true
                };
            }
        }

        /// <summary>
        /// Gets a access token from reddit
        /// </summary>
        /// <returns></returns>
        private async Task<AccessTokenResult> GetAccessToken(string code, bool isRefresh)
        {
            // Create the nav string
            const string accessTokenRequest = "https://www.reddit.com/api/v1/access_token";
            try
            {
                // Create the post data
                var postData = new List<KeyValuePair<string, string>>();
                postData.Add(new KeyValuePair<string, string>("grant_type", isRefresh ? "refresh_token" : "authorization_code"));
                postData.Add(new KeyValuePair<string, string>(isRefresh ? "refresh_token" : "code", code));
                postData.Add(new KeyValuePair<string, string>("redirect_uri", BaconitRedirectUrl));

                // Create the auth header
                var byteArray = Encoding.UTF8.GetBytes(BaconitAppId+":");
                var base64String = "Basic " + Convert.ToBase64String(byteArray);
                var response = await NetworkManager.MakePostRequest(accessTokenRequest, postData, base64String);
                var responseString = await response.ReadAsStringAsync();

                // Parse the response.
                return JsonConvert.DeserializeObject<AccessTokenResult>(responseString);
            }
            catch (Exception e)
            {
                _mBaconMan.MessageMan.DebugDia("Failed to get access token", e);
                return null;
            }
        }

        private static string GetNonce()
        {
            var rand = new Random((int)DateTime.Now.Ticks);
            var nonce = rand.Next(1000000000);
            return nonce.ToString();
        }

        private async Task<AccessTokenResult> RefreshAccessToken(string code, bool isRefresh)
        {
            // Try to get the new token
            var data = await GetAccessToken(code, isRefresh);
            if(data == null)
            {
                return null;
            }

            // Set the expires time
            data.ExpiresAt = DateTime.Now.AddSeconds(int.Parse(data.ExpiresIn));

            // If this was a refresh the refresh token won't be given again.
            // So set it to the current token.
            if(string.IsNullOrWhiteSpace(data.RefreshToken) && AccessTokenData != null)
            {
                data.RefreshToken = AccessTokenData.RefreshToken;
            }

            // Set it as the new data. Is is super important to remember that setting a string
            // on the access token won't set it into the roaming settings again because it doesn't
            // trigger the setter for the object!
            AccessTokenData = data;

            return data;
        }

        /// <summary>
        /// Returns if the user is signed in.
        /// </summary>
        public bool IsUserSignedIn =>
            AccessTokenData != null
            && !string.IsNullOrWhiteSpace(AccessTokenData.AccessToken)
            && !string.IsNullOrWhiteSpace(AccessTokenData.RefreshToken);

        /// <summary>
        /// Holds the current access token data
        /// </summary>
        private AccessTokenResult AccessTokenData
        {
            get
            {
                if (_accessTokenData != null) return _accessTokenData;
                _accessTokenData = _mBaconMan.SettingsMan.RoamingSettings.ContainsKey("AuthManager.AccessToken") ? _mBaconMan.SettingsMan.ReadFromRoamingSettings<AccessTokenResult>("AuthManager.AccessToken") : null;
                return _accessTokenData;
            }
            set
            {
                _accessTokenData = value;

                // #todo remove
                if (_accessTokenData != null && string.IsNullOrWhiteSpace(_accessTokenData.RefreshToken))
                {
                    Debugger.Break();
                }

                _mBaconMan.SettingsMan.WriteToRoamingSettings("AuthManager.AccessToken", _accessTokenData);
            }
        }
        private AccessTokenResult _accessTokenData;
    }
}
