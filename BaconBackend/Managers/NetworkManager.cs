using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace BaconBackend.Managers
{
    public class ServiceDownException : Exception { }

    public class NetworkManager
    {
        BaconManager m_baconMan;

        public NetworkManager(BaconManager baconMan)
        {
            m_baconMan = baconMan;
        }

        /// <summary>
        /// Returns the reddit post as a string.
        /// </summary>
        /// <param name="apiUrl"></param>
        /// <param name="postData"></param>
        /// <returns></returns>
        public async Task<string> MakeRedditGetRequestAsString(string apiUrl)
        {
            IHttpContent content = await MakeRedditGetRequest(apiUrl);
            return await content.ReadAsStringAsync();
        }

        /// <summary>
        /// Makes a reddit request.
        /// </summary>
        /// <param name="apiUrl"></param>
        /// <returns></returns>
        public async Task<IHttpContent> MakeRedditGetRequest(string apiUrl)
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
        /// Returns the reddit post as a string.
        /// </summary>
        /// <param name="apiUrl"></param>
        /// <param name="postData"></param>
        /// <returns></returns>
        public async Task<string> MakeRedditPostRequestAsString(string apiUrl, List<KeyValuePair<string, string>> postData)
        {
            IHttpContent content = await MakeRedditPostRequest(apiUrl, postData);
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
        public async Task<IHttpContent> MakeGetRequest(string url, string authHeader = "")
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
            HttpResponseMessage response = await request.SendRequestAsync(message, HttpCompletionOption.ResponseHeadersRead);
            if(response.StatusCode == Windows.Web.Http.HttpStatusCode.ServiceUnavailable || 
                response.StatusCode == Windows.Web.Http.HttpStatusCode.BadGateway ||
                response.StatusCode == Windows.Web.Http.HttpStatusCode.GatewayTimeout ||
                response.StatusCode == Windows.Web.Http.HttpStatusCode.InternalServerError)
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
        public async Task<IHttpContent> MakePostRequest(string url, List<KeyValuePair<string, string>> postData, string authHeader = "")
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
            HttpResponseMessage response = await request.SendRequestAsync(message, HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode == Windows.Web.Http.HttpStatusCode.ServiceUnavailable ||
              response.StatusCode == Windows.Web.Http.HttpStatusCode.BadGateway ||
              response.StatusCode == Windows.Web.Http.HttpStatusCode.GatewayTimeout ||
              response.StatusCode == Windows.Web.Http.HttpStatusCode.InternalServerError)
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
            HttpResponseMessage response = await request.SendRequestAsync(message, HttpCompletionOption.ResponseHeadersRead);
            return await response.Content.ReadAsBufferAsync();
        }

        /// <summary>
        /// Deserializes an object from IHttpContent without using strings.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="content"></param>
        /// <returns></returns>
        public async Task<T> DeseralizeObject<T>(IHttpContent content)
        {
            // NOTE!! We are really careful not to use a string here so we don't have to allocate a huge string.
            IInputStream inputStream = await content.ReadAsInputStreamAsync();
            using (StreamReader reader = new StreamReader(inputStream.AsStreamForRead()))
            using (JsonReader jsonReader = new JsonTextReader(reader))
            {
                // Parse the Json as an object
                JsonSerializer serializer = new JsonSerializer();
                T jsonObject = await Task.Run(() => serializer.Deserialize<T>(jsonReader));
                return jsonObject;
            }
        }
    }
}
