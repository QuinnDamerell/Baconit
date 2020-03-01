using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BaconBackend.Managers;
using Newtonsoft.Json;

namespace BaconBackend.Helpers.YouTube
{
    public static class YouTubeHelper
    {
        private const string BotUserAgent = "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";
        private const string GetInfoUrl = "https://www.youtube.com/get_video_info?video_id=";

        public static async Task<YouTubeVideoInfo> GetVideoInfoAsync(string youTubeId)
        {
            var response = await HttpGetAsync($"{GetInfoUrl}{youTubeId}");
            if (string.IsNullOrWhiteSpace(response))
            {
                throw new Exception($"Could not find video info for youtube id {youTubeId}");
            }

            var segments = response.Split('&');
            var playerResponse = string.Empty;

            foreach (var segment in segments)
            {
                if(string.IsNullOrWhiteSpace(segment)) continue;
                if (!segment.StartsWith("player_response")) continue;
                playerResponse = segment.Replace("player_response=", "");
                playerResponse = WebUtility.UrlDecode(Regex.Unescape(playerResponse));
                break;
            }

            if (string.IsNullOrWhiteSpace(playerResponse))
            {
                throw new Exception($"Could not find video info for youtube id {youTubeId}");
            }

            return JsonConvert.DeserializeObject<YouTubeVideoInfo>(playerResponse);
        }

        private static async Task<string> HttpGetAsync(string uri)
        {
            var response = await NetworkManager.MakeGetRequest(uri, string.Empty, BotUserAgent);
            return await response.ReadAsStringAsync();
        }
    }
}