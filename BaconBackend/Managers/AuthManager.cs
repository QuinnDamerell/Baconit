using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Authentication.Web;
using Windows.Web.Http;

namespace BaconBackend.Managers
{
#pragma warning disable CS0649

    public class AuthManager
    {
        private const string BACONIT_APP_ID = "EU5cWoJCJ5HcPQ";
        private const string BACONIT_REDIRECT_URL = "http://www.quinndamerell.com/Baconit/OAuth/Auth.php";

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

        private BaconManager m_baconMan;
        ManualResetEvent m_refreshEvent = new ManualResetEvent(false);
        bool m_isTokenRefreshing = false;
        bool m_tokenRefreshFailed = false;

        public AuthManager(BaconManager baconMan)
        {
            m_baconMan = baconMan;
        }

        /// <summary>
        /// Authenticates a new user.
        /// </summary>
        /// <returns></returns>
        public async Task<UserManager.SignInResult> AuthNewUser()
        {
            // Try to get the request token
            UserManager.SignInResult result = await GetRedditRequestToken();
            if(!result.WasSuccess)
            {
                return result;
            }

            // Try to get the access token
            AccessTokenResult accessToken = await RefreshAccessToken(result.Message, false);
            if(accessToken == null)
            {
                return new UserManager.SignInResult()
                {
                    Message = "Failed to get access token"
                };
            }

            return new UserManager.SignInResult()
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

            bool shouldRefresh = false;
            bool shouldBlock = false;
            lock (m_refreshEvent)
            {
                TimeSpan timeRemaining = AccessTokenData.ExpiresAt - DateTime.Now;

                // If it is already expired or will do so soon wait on the refresh before using it.
                if (timeRemaining.TotalSeconds < 30 || m_tokenRefreshFailed)
                {
                    // Check if someone else it refreshing
                    if (!m_isTokenRefreshing)
                    {
                        m_isTokenRefreshing = true;
                        shouldRefresh = true;
                    }
                    shouldBlock = true;
                }
                // If it is going to expire soon but not too soon refresh it async.
                else if (timeRemaining.TotalMinutes < 5)
                {
                    // Check if someone else it refreshing
                    if (!m_isTokenRefreshing)
                    {
                        m_isTokenRefreshing = true;
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
                        await RefreshAccessToken(AccessTokenData.RefreshToken, true);
                        m_tokenRefreshFailed = false;
                    }
                    catch (Exception e)
                    {
                        m_baconMan.MessageMan.DebugDia("Failed to auto refresh token", e);
                        m_tokenRefreshFailed = true;
                    }

                    // Lock the
                    lock (m_refreshEvent)
                    {
                        m_isTokenRefreshing = false;
                        m_refreshEvent.Set();
                    }

                }).Start();
            }

            // Block if we need to block
            if (shouldBlock)
            {
                // Wait on a task that will wait on the event.
                await Task.Run(() =>
                {
                    m_refreshEvent.WaitOne();
                });
            }

            // Now return the key.
            return m_tokenRefreshFailed ? null : AccessTokenData.AccessToken;
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
            string nonce = GetNonce();

            // Create the nav string
            string tokenRequestString = "https://reddit.com/api/v1/authorize.compact?"
                + "client_id=" + BACONIT_APP_ID
                + "&response_type=code"
                + "&state=" + nonce
                + "&redirect_uri=" + BACONIT_REDIRECT_URL
                + "&duration=permanent"
                + "&scope=modcontributors,modconfig,subscribe,wikiread,wikiedit,vote,mysubreddits,submit,"
                + "modlog,modposts,modflair,save,modothers,read,privatemessages,report,identity,livemanage,"
                + "account,modtraffic,edit,modwiki,modself,history,flair";

            try
            {
                Uri start = new Uri(tokenRequestString, UriKind.Absolute);
                Uri end = new Uri(BACONIT_REDIRECT_URL, UriKind.Absolute);
                WebAuthenticationResult result = await WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.None, start, end);

                if(result.ResponseStatus == WebAuthenticationStatus.Success)
                {
                    // Woot we got it! Parse out the token
                    int startOfState = result.ResponseData.IndexOf("state=") + 6;
                    int endOfState = result.ResponseData.IndexOf("&", startOfState);
                    int startOfCode = result.ResponseData.IndexOf("code=") + 5;
                    int endOfCode = result.ResponseData.IndexOf("&", startOfCode);

                    // Make sure we found a code (result is -1 + 5 = 4)
                    if(startOfCode == 4)
                    {
                        return new UserManager.SignInResult()
                        {
                            Message = "Reddit returned an error!"
                        };
                    }

                    // Check for the ends
                    endOfCode = endOfCode == -1 ? result.ResponseData.Length : endOfCode;
                    endOfState = endOfState == -1 ? result.ResponseData.Length : endOfState;

                    string state = result.ResponseData.Substring(startOfState, endOfState - startOfState);
                    string code = result.ResponseData.Substring(startOfCode, endOfCode - startOfCode);

                    // Check the state
                    if(nonce != state)
                    {
                        return new UserManager.SignInResult()
                        {
                            Message = "The state is not the same!"
                        };
                    }

                    // Check the code
                    if(code == "")
                    {
                        return new UserManager.SignInResult()
                        {
                            Message = "The code is empty!"
                        };
                    }

                    // Return the code!
                    return new UserManager.SignInResult()
                    {
                        WasSuccess = true,
                        Message = code
                    };
                }
                else if(result.ResponseStatus == WebAuthenticationStatus.ErrorHttp)
                {
                    return new UserManager.SignInResult()
                    {
                        WasErrorNetwork = true
                    };
                }
                else
                {
                    return new UserManager.SignInResult()
                    {
                        WasUserCanceled = true
                    };
                }
            }
            catch(Exception e)
            {
                m_baconMan.MessageMan.DebugDia("Failed to get request token", e);
                return new UserManager.SignInResult()
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
            string accessTokenRequest = "https://www.reddit.com/api/v1/access_token";
            try
            {
                // Create the post data
                List<KeyValuePair<string, string>> postData = new List<KeyValuePair<string, string>>();
                postData.Add(new KeyValuePair<string, string>("grant_type", isRefresh ? "refresh_token" : "authorization_code"));
                postData.Add(new KeyValuePair<string, string>(isRefresh ? "refresh_token" : "code", code));
                postData.Add(new KeyValuePair<string, string>("redirect_uri", BACONIT_REDIRECT_URL));

                // Create the auth header
                var byteArray = Encoding.UTF8.GetBytes(BACONIT_APP_ID+":");
                var base64String = "Basic " + Convert.ToBase64String(byteArray);
                IHttpContent response = await m_baconMan.NetworkMan.MakePostRequest(accessTokenRequest, postData, base64String);
                string responseString = await response.ReadAsStringAsync();

                // Parse the response.
                return JsonConvert.DeserializeObject<AccessTokenResult>(responseString);
            }
            catch (Exception e)
            {
                m_baconMan.MessageMan.DebugDia("Failed to get access token", e);
                return null;
            }
        }

        private string GetNonce()
        {
            Random rand = new Random((int)DateTime.Now.Ticks);
            int nonce = rand.Next(1000000000);
            return nonce.ToString();
        }

        private async Task<AccessTokenResult> RefreshAccessToken(string code, bool isRefresh)
        {
            // Try to get the new token
            AccessTokenResult data = await GetAccessToken(code, isRefresh);
            if(data == null)
            {
                return null;
            }

            // Set the expires time
            data.ExpiresAt = DateTime.Now.AddSeconds(int.Parse(data.ExpiresIn));

            // If this was a refresh the refresh token won't be given again.
            // So set it to the current token.
            if(String.IsNullOrWhiteSpace(data.RefreshToken) && AccessTokenData != null)
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
        public bool IsUserSignedIn
        {
            get
            {
                return AccessTokenData != null
                    && !String.IsNullOrWhiteSpace(AccessTokenData.AccessToken)
                    && !String.IsNullOrWhiteSpace(AccessTokenData.RefreshToken);
            }
        }

        /// <summary>
        /// Holds the current access token data
        /// </summary>
        private AccessTokenResult AccessTokenData
        {
            get
            {
                if (m_accessTokenData == null)
                {
                    if (m_baconMan.SettingsMan.RoamingSettings.ContainsKey("AuthManager.AccessToken"))
                    {
                        m_accessTokenData = m_baconMan.SettingsMan.ReadFromRoamingSettings<AccessTokenResult>("AuthManager.AccessToken");
                    }
                    else
                    {
                        m_accessTokenData = null;
                    }
                }
                return m_accessTokenData;
            }
            set
            {
                m_accessTokenData = value;

                // #todo remove
                if (m_accessTokenData != null && String.IsNullOrWhiteSpace(m_accessTokenData.RefreshToken))
                {
                    Debugger.Break();
                }

                m_baconMan.SettingsMan.WriteToRoamingSettings<AccessTokenResult>("AuthManager.AccessToken", m_accessTokenData);
            }
        }
        private AccessTokenResult m_accessTokenData = null;
    }
}
