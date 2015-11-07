using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace BaconBackend.Managers
{
    public class NetworkManager
    {
        BaconManager m_baconMan;

        public NetworkManager(BaconManager baconMan)
        {
            m_baconMan = baconMan;
        }

        /// <summary>
        /// Makes a reddit request.
        /// </summary>
        /// <param name="apiUrl"></param>
        /// <returns></returns>
        public async Task<string> MakeRedditGetRequest(string apiUrl)
        {
            if(m_baconMan.UserMan.IsUserSignedIn)
            {
                string accessToken = await m_baconMan.UserMan.GetAccessToken();
                var byteArray = Encoding.UTF8.GetBytes(accessToken);
                var authHeader = "bearer " + accessToken;
                return await MakeGetRequest("https://oauth.reddit.com/" + apiUrl, authHeader);
            }
            else
            {
                return await MakeGetRequest("https://www.reddit.com/" + apiUrl);
            }
        }

        /// <summary>
        /// Makes a reddit post request
        /// </summary>
        /// <param name="apiUrl"></param>
        /// <param name="postData"></param>
        /// <returns></returns>
        public async Task<string> MakeRedditPostRequest(string apiUrl, List<KeyValuePair<string, string>> postData)
        {
            if (m_baconMan.UserMan.IsUserSignedIn)
            {
                string accessToken = await m_baconMan.UserMan.GetAccessToken();
                var byteArray = Encoding.UTF8.GetBytes(accessToken);
                var authHeader = "bearer " + accessToken;
                return await MakePostRequest("https://oauth.reddit.com/" + apiUrl, postData, authHeader);
            }
            else
            {
                return await MakePostRequest("https://www.reddit.com/" + apiUrl, postData);
            }
        }

        /// <summary>
        /// Makes a generic get request.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public async Task<string> MakeGetRequest(string url, string authHeader = "")
        {
            if(url == "")
            {
                throw new Exception("The URL is null!");
            }
            HttpClient request = new HttpClient();
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Absolute));
            // Set the user agent
            message.Headers.Add("User-Agent", "Baconit");
            // Set the auth header
            if (!String.IsNullOrWhiteSpace(authHeader))
            {
                message.Headers["Authorization"] = authHeader;
            }
            HttpResponseMessage response = await request.SendRequestAsync(message);
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Makes a generic post request.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <param name="authHeader"></param>
        /// <returns></returns>
        public async Task<string> MakePostRequest(string url, List<KeyValuePair<string, string>> postData, string authHeader = "")
        {
            if (url == "")
            {
                throw new Exception("The URL is null!");
            }
            HttpClient request = new HttpClient();
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, new Uri(url, UriKind.Absolute));
            message.Headers.Add("User-Agent", "Baconit");

            // Set the auth header
            if (!String.IsNullOrWhiteSpace(authHeader))
            {
                message.Headers["Authorization"] = authHeader;
            }

            // Set the post data
            message.Content = new HttpFormUrlEncodedContent(postData);

            // Send the request
            HttpResponseMessage response = await request.SendRequestAsync(message);
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Makes a raw get request and returns an IBuffer
        /// </summary>
        /// <param name="url">The url to request</param>
        /// <returns>IBuffer with the content</returns>
        public async Task<IBuffer> MakeRawGetRequest(string url)
        {
            if (url == "")
            {
                throw new Exception("The URL is null!");
            }

            // Build the request
            HttpClient request = new HttpClient();
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Absolute));
            message.Headers.Add("User-Agent", "Baconit");

            // Send the request
            HttpResponseMessage response = await request.SendRequestAsync(message);
            return await response.Content.ReadAsBufferAsync();
        }
    }
}
