using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace BaconBackend.Managers
{
    public class ServiceDownException : Exception { }

    public class NetworkManager
    {
        private readonly BaconManager _baconMan;

        public NetworkManager(BaconManager baconMan)
        {
            _baconMan = baconMan;
        }

        /// <summary>
        /// Returns the reddit post as a string.
        /// </summary>
        /// <param name="apiUrl"></param>
        /// <returns></returns>
        public async Task<string> MakeRedditGetRequestAsString(string apiUrl)
        {
            var content = await MakeRedditGetRequest(apiUrl);
            return await content.ReadAsStringAsync();
        }

        /// <summary>
        /// Makes a reddit request.
        /// </summary>
        /// <param name="apiUrl"></param>
        /// <returns></returns>
        public async Task<IHttpContent> MakeRedditGetRequest(string apiUrl)
        {
            if (!_baconMan.UserMan.IsUserSignedIn) return await MakeGetRequest("https://www.reddit.com/" + apiUrl);
            var accessToken = await _baconMan.UserMan.GetAccessToken();
            if(string.IsNullOrWhiteSpace(accessToken))
            {
                throw new Exception("Failed to get (most likely refresh) the access token");
            }
            var authHeader = "bearer " + accessToken;
            return await MakeGetRequest("https://oauth.reddit.com/" + apiUrl, authHeader);

        }

        /// <summary>
        /// Returns the reddit post as a string.
        /// </summary>
        /// <param name="apiUrl"></param>
        /// <param name="postData"></param>
        /// <returns></returns>
        public async Task<string> MakeRedditPostRequestAsString(string apiUrl, List<KeyValuePair<string, string>> postData)
        {
            var content = await MakeRedditPostRequest(apiUrl, postData);
            return await content.ReadAsStringAsync();
        }

        /// <summary>
        /// Makes a reddit post request
        /// </summary>
        /// <param name="apiUrl"></param>
        /// <param name="postData"></param>
        /// <returns></returns>
        public async Task<IHttpContent> MakeRedditPostRequest(string apiUrl, List<KeyValuePair<string, string>> postData)
        {
            if (!_baconMan.UserMan.IsUserSignedIn)
                return await MakePostRequest("https://www.reddit.com/" + apiUrl, postData);
            var accessToken = await _baconMan.UserMan.GetAccessToken();
            var authHeader = "bearer " + accessToken;
            return await MakePostRequest("https://oauth.reddit.com/" + apiUrl, postData, authHeader);

        }

        /// <summary>
        /// Makes a generic get request.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="authHeader"></param>
        /// <param name="userAgent"></param>
        /// <returns></returns>
        public static async Task<IHttpContent> MakeGetRequest(string url, string authHeader = "", string userAgent = "baconit")
        {
            if(string.IsNullOrWhiteSpace(url))
            {
                throw new Exception("The URL is null!");
            }
            var request = new HttpClient();
            var message = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Absolute));

            // Set the user agent
            message.Headers.Add("User-Agent", userAgent);

            // Set the auth header
            if (!string.IsNullOrWhiteSpace(authHeader))
            {
                message.Headers["Authorization"] = authHeader;
            }
            var response = await request.SendRequestAsync(message, HttpCompletionOption.ResponseHeadersRead);

            if(response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                response.StatusCode == HttpStatusCode.BadGateway ||
                response.StatusCode == HttpStatusCode.GatewayTimeout ||
                response.StatusCode == HttpStatusCode.InternalServerError)
            {
                throw new ServiceDownException();
            }
            return response.Content;
        }

        /// <summary>
        /// Makes a generic post request.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <param name="authHeader"></param>
        /// <returns></returns>
        public static async Task<IHttpContent> MakePostRequest(string url, List<KeyValuePair<string, string>> postData, string authHeader = "")
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new Exception("The URL is null!");
            }
            var request = new HttpClient();
            var message = new HttpRequestMessage(HttpMethod.Post, new Uri(url, UriKind.Absolute));
            message.Headers.Add("User-Agent", "Baconit/5.0 by u/quinbd");

            // Set the auth header
            if (!string.IsNullOrWhiteSpace(authHeader))
            {
                message.Headers["Authorization"] = authHeader;
            }

            // Set the post data
            message.Content = new HttpFormUrlEncodedContent(postData);

            // Send the request
            var response = await request.SendRequestAsync(message, HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable ||
              response.StatusCode == HttpStatusCode.BadGateway ||
              response.StatusCode == HttpStatusCode.GatewayTimeout ||
              response.StatusCode == HttpStatusCode.InternalServerError)
            {
                throw new ServiceDownException();
            }
            return response.Content;
        }

        /// <summary>
        /// Makes a raw get request and returns an IBuffer
        /// </summary>
        /// <param name="url">The url to request</param>
        /// <returns>IBuffer with the content</returns>
        public static async Task<IBuffer> MakeRawGetRequest(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new Exception("The URL is null!");
            }

            // Build the request
            var request = new HttpClient();
            var message = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Absolute));
            message.Headers.Add("User-Agent", "Baconit");

            // Send the request
            var response = await request.SendRequestAsync(message, HttpCompletionOption.ResponseHeadersRead);
            return await response.Content.ReadAsBufferAsync();
        }

        /// <summary>
        /// Deserializes an object from IHttpContent without using strings.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="content"></param>
        /// <returns></returns>
        public static async Task<T> DeserializeObject<T>(IHttpContent content)
        {
            // NOTE!! We are really careful not to use a string here so we don't have to allocate a huge string.
            var inputStream = await content.ReadAsInputStreamAsync();
            using (var reader = new StreamReader(inputStream.AsStreamForRead()))
            using (JsonReader jsonReader = new JsonTextReader(reader))
            {
                // Parse the Json as an object
                var serializer = new JsonSerializer();
                var jsonObject = await Task.Run(() => serializer.Deserialize<T>(jsonReader));
                return jsonObject;
            }
        }
    }
}
