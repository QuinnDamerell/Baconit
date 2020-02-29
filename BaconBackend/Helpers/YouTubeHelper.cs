using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BaconBackend.Managers;

#if WP8 || WP7 || WP8
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
using MyToolkit.Paging;
using MyToolkit.UI;
using Microsoft.Phone.Controls;
#elif WINRT
using System.Net.Http.Headers;
using System.Net.Http;
#endif
namespace BaconBackend.Helpers
{
    // SOURCE: https://github.com/RicoSuter/MyToolkit
    // LICENSE: Microsoft Public License (Ms-PL)
    // This license governs use of the accompanying software. If you use the software, you accept this license. If you do not accept
    //      the license, do not use the software.

    // 1. Definitions
    // The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here as
    // under U.S. copyright law.
    // A "contribution" is the original software, or any additions or changes to the software.
    // A "contributor" is any person that distributes its contribution under this license.
    // "Licensed patents" are a contributor's patent claims that read directly on its contribution.

    // 2. Grant of Rights
    // (A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3,
    //      each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution,
    //      prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.
    // (B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3,
    //      each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make,
    //      have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or
    //      derivative works of the contribution in the software.

    // 3. Conditions and Limitations
    // (A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.
    // (B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software,
    //      your patent license from such contributor to the software ends automatically.
    // (C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution
    //      notices that are present in the software.
    // (D) If you distribute any portion of the software in source code form, you may do so only under this license by including
    //      a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or
    //      object code form, you may only do so under a license that complies with this license.
    // (E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties,
    //      guarantees or conditions. You may have additional consumer rights under your local laws which this license cannot
    //      change. To the extent permitted under your local laws, the contributors exclude the implied warranties of
    //      merchantability, fitness for a particular purpose and non-infringement.

    public static class YouTubeHelper
    {
        #region Video and audio stream retrieval

        private const string BotUserAgent = "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";

        /// <summary>Gets the default minimum quality. </summary>
        public const YouTubeQuality DefaultMinQuality = YouTubeQuality.Quality144P;

        /// <summary>Returns the best matching YouTube stream URI which has an audio and video channel and is not 3D. </summary>
        /// <returns>The best matching <see cref="YouTubeUri"/> of the video. </returns>
        /// <exception cref="YouTubeUriNotFoundException">The <see cref="YouTubeUri"/> could not be found. </exception>
        /// <exception cref="WebException">An error occurred while downloading the resource. </exception>
        public static async Task<YouTubeUri> GetVideoUriAsync(string youTubeId, YouTubeQuality minQuality, YouTubeQuality maxQuality, CancellationToken token)
        {
            var uris = await GetUrisAsync(youTubeId, token);
            var uri = TryFindBestVideoUri(uris, minQuality, maxQuality);
            if (uri == null)
                throw new YouTubeUriNotFoundException();
            return uri;
        }

        /// <summary>Returns the best matching YouTube stream URI which has an audio and video channel and is not 3D. </summary>
        /// <returns>The best matching <see cref="YouTubeUri"/> of the video. </returns>
        /// <exception cref="YouTubeUriNotFoundException">The <see cref="YouTubeUri"/> could not be found. </exception>
        /// <exception cref="WebException">An error occurred while downloading the resource. </exception>
        public static Task<YouTubeUri> GetVideoUriAsync(string youTubeId, YouTubeQuality maxQuality)
        {
            return GetVideoUriAsync(youTubeId, DefaultMinQuality, maxQuality, CancellationToken.None);
        }

        /// <summary>Returns the best matching YouTube stream URI which has an audio and video channel and is not 3D. </summary>
        /// <returns>The best matching <see cref="YouTubeUri"/> of the video. </returns>
        /// <exception cref="YouTubeUriNotFoundException">The <see cref="YouTubeUri"/> could not be found. </exception>
        /// <exception cref="WebException">An error occurred while downloading the resource. </exception>
        public static Task<YouTubeUri> GetVideoUriAsync(string youTubeId, YouTubeQuality maxQuality, CancellationToken token)
        {
            return GetVideoUriAsync(youTubeId, DefaultMinQuality, maxQuality, token);
        }

        /// <summary>Returns the best matching YouTube stream URI which has an audio and video channel and is not 3D. </summary>
        /// <returns>The best matching <see cref="YouTubeUri"/> of the video. </returns>
        /// <exception cref="YouTubeUriNotFoundException">The <see cref="YouTubeUri"/> could not be found. </exception>
        /// <exception cref="WebException">An error occurred while downloading the resource. </exception>
        public static Task<YouTubeUri> GetVideoUriAsync(string youTubeId, YouTubeQuality minQuality, YouTubeQuality maxQuality)
        {
            return GetVideoUriAsync(youTubeId, minQuality, maxQuality, CancellationToken.None);
        }

        /// <summary>Returns all available URIs (audio-only and video) for the given YouTube ID. </summary>
        /// <returns>The <see cref="YouTubeUri"/>s of the video. </returns>
        /// <exception cref="WebException">An error occurred while downloading the resource. </exception>
        public static Task<YouTubeUri[]> GetUrisAsync(string youTubeId)
        {
            return GetUrisAsync(youTubeId, CancellationToken.None);
        }

        /// <summary>Returns all available URIs (audio-only and video) for the given YouTube ID. </summary>
        /// <returns>The <see cref="YouTubeUri"/>s of the video. </returns>
        /// <exception cref="WebException">An error occurred while downloading the resource. </exception>
        public static async Task<YouTubeUri[]> GetUrisAsync(string youTubeId, CancellationToken token)
        {
            var urls = new List<YouTubeUri>();
            string javaScriptCode = null;

            var response = await HttpGetAsync("https://www.youtube.com/watch?v=" + youTubeId + "&nomobile=1");
            var match = Regex.Match(response, "url_encoded_fmt_stream_map\": ?\"(.*?)\"");
            var data = Uri.UnescapeDataString(match.Groups[1].Value);
            match = Regex.Match(response, "adaptive_fmts\": ?\"(.*?)\"");
            var data2 = Uri.UnescapeDataString(match.Groups[1].Value);

            var arr = Regex.Split(data + "," + data2, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)"); // split by comma but outside quotes
            foreach (var d in arr)
            {
                var url = "";
                var signature = "";
                var tuple = new YouTubeUri();
                foreach (var p in d.Replace("\\u0026", "\t").Split('\t'))
                {
                    var index = p.IndexOf('=');
                    if (index == -1 || index >= p.Length) continue;

                    try
                    {
                        var key = p.Substring(0, index);
                        var value = Uri.UnescapeDataString(p.Substring(index + 1));

                        switch (key)
                        {
                            case "url":
                                url = value;
                                break;
                            case "itag":
                                tuple.Itag = int.Parse(value);
                                break;
                            case "type" when (value.Contains("video/mp4") || value.Contains("audio/mp4")):
                                tuple.Type = value;
                                break;
                            case "sig":
                                signature = value;
                                break;
                            case "s":
                                {
                                    if (javaScriptCode == null)
                                    {
                                        string javaScriptUri;
                                        var urlMatch = Regex.Match(response, "\"\\\\/\\\\/s.ytimg.com\\\\/yts\\\\/jsbin\\\\/html5player-(.+?)\\.js\"");
                                        if (urlMatch.Success)
                                            javaScriptUri = "http://s.ytimg.com/yts/jsbin/html5player-" + urlMatch.Groups[1] + ".js";
                                        else
                                        {
                                            // new format
                                            javaScriptUri = "https://s.ytimg.com/yts/jsbin/player-" +
                                                            Regex.Match(response, "\"\\\\/\\\\/s.ytimg.com\\\\/yts\\\\/jsbin\\\\/player-(.+?)\\.js\"").Groups[1] + ".js";
                                        }
                                        javaScriptCode = await HttpGetAsync(javaScriptUri);
                                    }

                                    signature = YouTubeDecipher.Decipher(value, javaScriptCode);
                                    break;
                                }
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.WriteLine("YouTube parse exception: " + exception.Message);
                    }
                }

                if (string.IsNullOrEmpty(url)) continue;

                if (url.Contains("&signature=") || url.Contains("?signature="))
                    tuple.Uri = new Uri(url, UriKind.Absolute);
                else
                    tuple.Uri = new Uri(url + "&signature=" + signature, UriKind.Absolute);

                if (tuple.IsValid)
                    urls.Add(tuple);
            }

            return urls.ToArray();
        }

        private static YouTubeUri TryFindBestVideoUri(IEnumerable<YouTubeUri> uris, YouTubeQuality minQuality, YouTubeQuality maxQuality)
        {
            return uris
                .Where(u => u.HasVideo && u.HasAudio && !u.Is3DVideo && u.VideoQuality >= minQuality && u.VideoQuality <= maxQuality)
                .OrderByDescending(u => u.VideoQuality)
                .FirstOrDefault();
        }

#if WINRT

        /// <exception cref="WebException">An error occurred while downloading the resource. </exception>
        private static async Task<string> HttpGetAsync(string uri)
        {
            var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip };
            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.Add("User-Agent", BotUserAgent);
                var response = await client.GetAsync(new Uri(uri, UriKind.Absolute));
                return await response.Content.ReadAsStringAsync();
            }
        }

#else

        /// <exception cref="WebException">An error occurred while downloading the resource. </exception>
        private static async Task<string> HttpGetAsync(string uri)
        {
            var response = await NetworkManager.MakeGetRequest(uri, string.Empty, BotUserAgent);
            return await response.ReadAsStringAsync();
        }

#endif

        #endregion

        #region Retrieving title or thumbnail

#if WINRT || WINDOWS_UAP

        /// <summary>Returns the title of the YouTube video. </summary>
        /// <exception cref="WebException">An error occurred while downloading the resource. </exception>
        public static Task<string> GetVideoTitleAsync(string youTubeId)
        {
            return GetVideoTitleAsync(youTubeId, CancellationToken.None);
        }

        /// <summary>Returns the title of the YouTube video. </summary>
        /// <exception cref="WebException">An error occurred while downloading the resource. </exception>
        public static async Task<string> GetVideoTitleAsync(string youTubeId, CancellationToken token)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", BotUserAgent);
                var response = await client.GetAsync("http://www.youtube.com/watch?v=" + youTubeId + "&nomobile=1", token);
                var html = await response.Content.ReadAsStringAsync();
                var startIndex = html.IndexOf(" title=\"");
                if (startIndex != -1)
                {
                    startIndex = html.IndexOf(" title=\"", startIndex + 1);
                    if (startIndex != -1)
                    {
                        startIndex += 8;
                        var endIndex = html.IndexOf("\">", startIndex);
                        if (endIndex != -1)
                            return html.Substring(startIndex, endIndex - startIndex);
                    }
                }
                return null;
            }
        }

#elif WP8 || WP7 || SL5

        /// <summary>
        /// Returns the title of the YouTube video. 
        /// </summary>
        public static Task<string> GetVideoTitleAsync(string youTubeId) // should be improved
        {
            var source = new TaskCompletionSource<string>();
            var web = new WebClient();
            web.OpenReadCompleted += (sender, args) =>
            {
                if (args.Error != null)
                    source.SetException(args.Error);
                else if (args.Cancelled)
                    source.SetCanceled();
                else
                {
                    string result = null;
                    var bytes = args.Result.ReadToEnd();
                    var html = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
                    var startIndex = html.IndexOf(" title=\"");
                    if (startIndex != -1)
                    {
                        startIndex = html.IndexOf(" title=\"", startIndex + 1);
                        if (startIndex != -1)
                        {
                            startIndex += 8;
                            var endIndex = html.IndexOf("\">", startIndex);
                            if (endIndex != -1)
                                result = html.Substring(startIndex, endIndex - startIndex);
                        }
                    }
                    source.SetResult(result);
                }
            };
            web.Headers[HttpRequestHeader.UserAgent] = BotUserAgent;
            web.OpenReadAsync(new Uri("http://www.youtube.com/watch?v=" + youTubeId + "&nomobile=1"));
            return source.Task;
        }

#endif

        /// <summary>Returns a thumbnail for the given YouTube ID. </summary>
        /// <exception cref="ArgumentException">The value of 'size' is not defined. </exception>
        public static Uri GetThumbnailUri(string youTubeId, YouTubeThumbnailSize size = YouTubeThumbnailSize.Medium)
        {
            switch (size)
            {
                case YouTubeThumbnailSize.Small:
                    return new Uri("http://img.youtube.com/vi/" + youTubeId + "/default.jpg", UriKind.Absolute);
                case YouTubeThumbnailSize.Medium:
                    return new Uri("http://img.youtube.com/vi/" + youTubeId + "/hqdefault.jpg", UriKind.Absolute);
                case YouTubeThumbnailSize.Large:
                    return new Uri("http://img.youtube.com/vi/" + youTubeId + "/maxresdefault.jpg", UriKind.Absolute);
            }
            throw new ArgumentException("The value of 'size' is not defined.");
        }

        #endregion

        #region Phone playback

#if WP7 || WP8


        /// <summary>Plays the YouTube video in the Windows Phone's external player. </summary>
        /// <param name="youTubeId">The YouTube ID</param>
        /// <param name="maxQuality">The maximum allowed video quality. </param>
        /// <returns>Awaitable task. </returns>
        public static Task PlayAsync(string youTubeId, YouTubeQuality maxQuality)
        {
            return PlayAsync(youTubeId, maxQuality, CancellationToken.None);
        }

        /// <summary>Plays the YouTube video in the Windows Phone's external player. </summary>
        /// <param name="youTubeId">The YouTube ID</param>
        /// <param name="maxQuality">The maximum allowed video quality. </param>
        /// <param name="token">The cancellation token</param>
        /// <returns>Awaitable task. </returns>
        public static async Task PlayAsync(string youTubeId, YouTubeQuality maxQuality, CancellationToken token)
        {
            var uri = await GetVideoUriAsync(youTubeId, maxQuality, token);
            if (uri != null)
            {
                var launcher = new MediaPlayerLauncher
                {
                    Controls = MediaPlaybackControls.All,
                    Media = uri.Uri
                };
                launcher.Show();
            }
            else
                throw new Exception("no_video_urls_found");
        }

        private static CancellationTokenSource _cancellationToken;
        private static PageDeactivator _oldState;

        /// <summary>Plays the YouTube video in the Windows Phone's external player. 
        /// Disables the current page and shows a progress indicator until 
        /// the YouTube movie URI has been loaded and the video playback starts. </summary>
        /// <param name="youTubeId">The YouTube ID</param>
        /// <param name="manualActivatePage">if true add YouTube.CancelPlay() in OnNavigatedTo() of the page (better)</param>
        /// <param name="maxQuality">The maximum allowed video quality. </param>
        /// <returns>Awaitable task. </returns>
        public static async Task PlayWithPageDeactivationAsync(string youTubeId, bool manualActivatePage, YouTubeQuality maxQuality)
        {
            PhoneApplicationPage page;

            lock (typeof(YouTube))
            {
                if (_oldState != null)
                    return;

                if (SystemTray.ProgressIndicator == null)
                    SystemTray.ProgressIndicator = new ProgressIndicator();

                SystemTray.ProgressIndicator.IsVisible = true;
                SystemTray.ProgressIndicator.IsIndeterminate = true;

                page = Environment.PhoneApplication.CurrentPage;

                _oldState = PageDeactivator.Inactivate();
                _cancellationToken = new CancellationTokenSource();
            }

            try
            {
                await PlayAsync(youTubeId, maxQuality, _cancellationToken.Token);
                if (page == Environment.PhoneApplication.CurrentPage)
                    CancelPlay(manualActivatePage);
            }
            catch (Exception)
            {
                if (page == Environment.PhoneApplication.CurrentPage)
                    CancelPlay(false);
                throw;
            }
        }

        /// <summary>Call this method in OnBackKeyPress() or in OnNavigatedTo() when 
        /// using PlayWithDeactivationAsync() and manualActivatePage = true like this 
        /// e.Cancel = YouTube.CancelPlay(); </summary>
        /// <returns></returns>
        public static bool CancelPlay()
        {
            return CancelPlay(false);
        }

        /// <summary>Should be called when using PlayWithDeactivationAsync() when the back key has been pressed. </summary>
        /// <param name="args"></param>
        public static void HandleBackKeyPress(CancelEventArgs args)
        {
            if (CancelPlay())
                args.Cancel = true;
        }

        private static bool CancelPlay(bool manualActivatePage)
        {
            lock (typeof(YouTube))
            {
                if (_oldState == null && _cancellationToken == null)
                    return false;

                if (_cancellationToken != null)
                {
                    _cancellationToken.Cancel();
                    _cancellationToken.Dispose();
                    _cancellationToken = null;
                }

                if (!manualActivatePage && _oldState != null)
                {
                    _oldState.Revert();
                    SystemTray.ProgressIndicator.IsVisible = false;
                    _oldState = null;
                }

                return true;
            }
        }

        /// <summary>Obsolete: Use PlayAsync() instead. </summary>
        [Obsolete("Use PlayAsync instead. 5/17/2014")]
        public static async void Play(string youTubeId, YouTubeQuality maxQuality, Action<Exception> completed)
        {
            try
            {
                await PlayAsync(youTubeId, maxQuality);
                if (completed != null)
                    completed(null);
            }
            catch (Exception exception)
            {
                if (completed != null)
                    completed(exception);
            }
        }

        /// <summary>Obsolete: Use PlayWithPageDeactivationAsync() instead. </summary>
        [Obsolete("Use PlayWithPageDeactivationAsync instead. 5/17/2014")]
        public static async void Play(string youTubeId, bool manualActivatePage, YouTubeQuality maxQuality, Action<Exception> completed)
        {
            try
            {
                await PlayWithPageDeactivationAsync(youTubeId, manualActivatePage, maxQuality);
                if (completed != null)
                    completed(null);
            }
            catch (Exception exception)
            {
                if (completed != null)
                    completed(exception);
            }
        }

#endif
        #endregion
    }

    /// <summary>Exception which occurs when no <see cref="YouTubeUri"/> could be found. </summary>
    public class YouTubeUriNotFoundException : Exception
    {
        internal YouTubeUriNotFoundException()
            : base("No matching YouTube video or audio stream URI could be found. " +
                   "The video may not be available in your country, " +
                   "is private or uses RTMPE (protected streaming).")
        { }
    }

    /// <summary>An URI to a YouTube MP4 video or audio stream. </summary>
    public class YouTubeUri
    {
        /// <summary>Gets the Itag of the stream. </summary>
        public int Itag { get; internal set; }

        /// <summary>Gets the <see cref="Uri"/> of the stream. </summary>
        public Uri Uri { get; internal set; }

        /// <summary>Gets the stream type. </summary>
        public string Type { get; internal set; }

        /// <summary>Gets a value indicating whether the stream has audio. </summary>
        public bool HasAudio
        {
            get { return AudioQuality != YouTubeQuality.Unknown && AudioQuality != YouTubeQuality.NotAvailable; }
        }

        /// <summary>Gets a value indicating whether the stream has video. </summary>
        public bool HasVideo
        {
            get { return VideoQuality != YouTubeQuality.Unknown && VideoQuality != YouTubeQuality.NotAvailable; }
        }

        /// <summary>Gets a value indicating whether the stream has 3D video. </summary>
        public bool Is3DVideo
        {
            get
            {
                if (VideoQuality == YouTubeQuality.Unknown)
                    return false;

                return Itag >= 82 && Itag <= 85;
            }
        }

        /// <summary>Gets stream's video quality. </summary>
        public YouTubeQuality VideoQuality
        {
            get
            {
                switch (Itag)
                {
                    // video & audio
                    case 5: return YouTubeQuality.Quality240P;
                    case 6: return YouTubeQuality.Quality270P;
                    case 17: return YouTubeQuality.Quality144P;
                    case 18: return YouTubeQuality.Quality360P;
                    case 22: return YouTubeQuality.Quality720P;
                    case 34: return YouTubeQuality.Quality360P;
                    case 35: return YouTubeQuality.Quality480P;
                    case 36: return YouTubeQuality.Quality240P;
                    case 37: return YouTubeQuality.Quality1080P;
                    case 38: return YouTubeQuality.Quality2160P;

                    // 3d video & audio
                    case 82: return YouTubeQuality.Quality360P;
                    case 83: return YouTubeQuality.Quality480P;
                    case 84: return YouTubeQuality.Quality720P;
                    case 85: return YouTubeQuality.Quality520P;

                    // video only
                    case 133: return YouTubeQuality.Quality240P;
                    case 134: return YouTubeQuality.Quality360P;
                    case 135: return YouTubeQuality.Quality480P;
                    case 136: return YouTubeQuality.Quality720P;
                    case 137: return YouTubeQuality.Quality1080P;
                    case 138: return YouTubeQuality.Quality2160P;
                    case 160: return YouTubeQuality.Quality144P;

                    // audio only
                    case 139: return YouTubeQuality.NotAvailable;
                    case 140: return YouTubeQuality.NotAvailable;
                    case 141: return YouTubeQuality.NotAvailable;
                }

                return YouTubeQuality.Unknown;
            }
        }

        /// <summary>Gets stream's audio quality. </summary>
        public YouTubeQuality AudioQuality
        {
            get
            {
                switch (Itag)
                {
                    // video & audio
                    case 5: return YouTubeQuality.QualityLow;
                    case 6: return YouTubeQuality.QualityLow;
                    case 17: return YouTubeQuality.QualityLow;
                    case 18: return YouTubeQuality.QualityMedium;
                    case 22: return YouTubeQuality.QualityHigh;
                    case 34: return YouTubeQuality.QualityMedium;
                    case 35: return YouTubeQuality.QualityMedium;
                    case 36: return YouTubeQuality.QualityLow;
                    case 37: return YouTubeQuality.QualityHigh;
                    case 38: return YouTubeQuality.QualityHigh;

                    // 3d video & audio
                    case 82: return YouTubeQuality.QualityMedium;
                    case 83: return YouTubeQuality.QualityMedium;
                    case 84: return YouTubeQuality.QualityHigh;
                    case 85: return YouTubeQuality.QualityHigh;

                    // video only
                    case 133: return YouTubeQuality.NotAvailable;
                    case 134: return YouTubeQuality.NotAvailable;
                    case 135: return YouTubeQuality.NotAvailable;
                    case 136: return YouTubeQuality.NotAvailable;
                    case 137: return YouTubeQuality.NotAvailable;
                    case 138: return YouTubeQuality.NotAvailable;
                    case 160: return YouTubeQuality.NotAvailable;

                    // audio only
                    case 139: return YouTubeQuality.QualityLow;
                    case 140: return YouTubeQuality.QualityMedium;
                    case 141: return YouTubeQuality.QualityHigh;
                }
                return YouTubeQuality.Unknown;
            }
        }

        internal bool IsValid
        {
            get { return Uri != null && Uri.ToString().StartsWith("http") && Itag > 0 && Type != null; }
        }
    }

    /// <summary>Enumeration of stream qualities. </summary>
    public enum YouTubeQuality : short
    {
        // video
        Quality144P,
        Quality240P,
        Quality270P,
        Quality360P,
        Quality480P,
        Quality520P,
        Quality720P,
        Quality1080P,
        Quality2160P,

        // audio
        QualityLow,
        QualityMedium,
        QualityHigh,

        NotAvailable,
        Unknown,
    }

    /// <summary>Enumeration of thumbnail sizes. </summary>
    public enum YouTubeThumbnailSize : short
    {
        Small,
        Medium,
        Large
    }

    internal static class YouTubeDecipher
    {
        public static string Decipher(string cipher, string javaScript)
        {
            //Find "C" in this: var A = B.sig||C (B.s)
            const string funcNamePattern = @"\.sig\s*\|\|([a-zA-Z0-9\$]+)\("; //Regex Formed To Find Word or DollarSign

            var funcName = Regex.Match(javaScript, funcNamePattern).Groups[1].Value;

            if (funcName.Contains("$"))
            {
                funcName = "\\" + funcName; //Due To Dollar Sign Introduction, Need To Escape
            }

            var funcPattern = funcName + @"=function\(\w+\)\{.*?\},"; //Escape funcName string
            var funcBody = Regex.Match(javaScript, funcPattern, RegexOptions.Singleline).Value; //Entire sig function
            var lines = funcBody.Split(';'); //Each line in sig function

            string idReverse = "", idSlice = "", idCharSwap = ""; //Hold name for each cipher method
            string functionIdentifier;
            var operations = "";

            foreach (var line in lines.Skip(1).Take(lines.Length - 2)) //Matches the funcBody with each cipher method. Only runs till all three are defined.
            {
                if (!string.IsNullOrEmpty(idReverse) && !string.IsNullOrEmpty(idSlice) &&
                    !string.IsNullOrEmpty(idCharSwap))
                {
                    break; //Break loop if all three cipher methods are defined
                }

                functionIdentifier = GetFunctionFromLine(line);
                var reReverse = $@"{functionIdentifier}:\bfunction\b\(\w+\)"; //Regex for reverse (one parameter)
                var reSlice = $@"{functionIdentifier}:\bfunction\b\([a],b\).(\breturn\b)?.?\w+\."; //Regex for slice (return or not)
                var reSwap = $@"{functionIdentifier}:\bfunction\b\(\w+\,\w\).\bvar\b.\bc=a\b"; //Regex for the char swap.

                if (Regex.Match(javaScript, reReverse).Success)
                {
                    idReverse = functionIdentifier; //If def matched the regex for reverse then the current function is a defined as the reverse
                }

                if (Regex.Match(javaScript, reSlice).Success)
                {
                    idSlice = functionIdentifier; //If def matched the regex for slice then the current function is defined as the slice.
                }

                if (Regex.Match(javaScript, reSwap).Success)
                {
                    idCharSwap = functionIdentifier; //If def matched the regex for charSwap then the current function is defined as swap.
                }
            }

            foreach (var line in lines.Skip(1).Take(lines.Length - 2))
            {
                Match m;
                functionIdentifier = GetFunctionFromLine(line);

                if ((m = Regex.Match(line, @"\(\w+,(?<index>\d+)\)")).Success && functionIdentifier == idCharSwap)
                {
                    operations += "w" + m.Groups["index"].Value + " "; //operation is a swap (w)
                }

                if ((m = Regex.Match(line, @"\(\w+,(?<index>\d+)\)")).Success && functionIdentifier == idSlice)
                {
                    operations += "s" + m.Groups["index"].Value + " "; //operation is a slice
                }

                if (functionIdentifier == idReverse) //No regex required for reverse (reverse method has no parameters)
                {
                    operations += "r "; //operation is a reverse
                }
            }

            operations = operations.Trim();

            return DecipherWithOperations(cipher, operations);
        }

        private static string ApplyOperation(string cipher, string op)
        {
            switch (op[0])
            {
                case 'r':
                    return new string(cipher.ToCharArray().Reverse().ToArray());

                case 'w':
                    {
                        var index = GetOpIndex(op);
                        return SwapFirstChar(cipher, index);
                    }

                case 's':
                    {
                        var index = GetOpIndex(op);
                        return cipher.Substring(index);
                    }

                default:
                    throw new NotImplementedException("Couldn't find cipher operation.");
            }
        }

        private static string DecipherWithOperations(string cipher, string operations)
        {
            return operations.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries)
                .Aggregate(cipher, ApplyOperation);
        }

        private static string GetFunctionFromLine(string currentLine)
        {
            var matchFunctionReg = new Regex(@"\w+\.(?<functionID>\w+)\("); //lc.ac(b,c) want the ac part.
            var rgMatch = matchFunctionReg.Match(currentLine);
            var matchedFunction = rgMatch.Groups["functionID"].Value;
            return matchedFunction; //return 'ac'
        }

        private static int GetOpIndex(string op)
        {
            var parsed = new Regex(@".(\d+)").Match(op).Result("$1");
            var index = int.Parse(parsed);

            return index;
        }

        private static string SwapFirstChar(string cipher, int index)
        {
            var builder = new StringBuilder(cipher) { [0] = cipher[index], [index] = cipher[0] };

            return builder.ToString();
        }
    }
}