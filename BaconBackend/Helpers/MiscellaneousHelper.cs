using BaconBackend.DataObjects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.Helpers
{
    public enum RedditContentType
    {
        Subreddit,
        Post,
        Comment
    }

    public class RedditContentContainer
    {
        public RedditContentType Type;
        public string Subreddit;
        public string Post;
        public string Comment;
    }

    public class MiscellaneousHelper
    {
        /// <summary>
        /// Called when the user is trying to comment on something.
        /// </summary>
        /// <returns>Returns the json returned or a null string if failed.</returns>
        public static async Task<string> SendRedditComment(BaconManager baconMan, string redditIdCommentingOn, string comment)
        {
            string returnString = null;
            try
            {
                // Build the data to send
                List<KeyValuePair<string, string>> postData = new List<KeyValuePair<string, string>>();
                postData.Add(new KeyValuePair<string, string>("thing_id", redditIdCommentingOn));
                postData.Add(new KeyValuePair<string, string>("text", comment));

                // Make the call
                returnString = await baconMan.NetworkMan.MakeRedditPostRequest("api/comment", postData);
            }
            catch (Exception e)
            {
                baconMan.TelemetryMan.ReportUnExpectedEvent("MisHelper", "failed to send comment", e);
                baconMan.MessageMan.DebugDia("failed to send message", e);
            }
            return returnString;
        }

        /// <summary>
        /// Gets a reddit user.
        /// </summary>
        /// <returns>Returns null if it fails or the user doesn't exist.</returns>
        public static async Task<User> GetRedditUser(BaconManager baconMan, string userName)
        {
            User foundUser = null;
            try
            {
                // Make the call
                string jsonResponse = await baconMan.NetworkMan.MakeRedditGetRequest($"user/{userName}/about/.json");

                // Try to parse out the user
                int dataPos = jsonResponse.IndexOf("\"data\":");
                if (dataPos == -1) return null;
                int dataStartPos = jsonResponse.IndexOf('{', dataPos + 7);
                if (dataPos == -1) return null;
                int dataEndPos = jsonResponse.IndexOf("}", dataStartPos);
                if (dataPos == -1) return null;

                string userData = jsonResponse.Substring(dataStartPos, (dataEndPos - dataStartPos + 1));

                // Parse the new user
                foundUser = await Task.Run(() => JsonConvert.DeserializeObject<User>(userData));
            }
            catch (Exception e)
            {
                baconMan.TelemetryMan.ReportUnExpectedEvent("MisHelper", "failed to search for user", e);
                baconMan.MessageMan.DebugDia("failed to search for user", e);
            }
            return foundUser;
        }


        /// <summary>
        /// Attempts to parse out a reddit object from a reddit data object.
        /// </summary>
        /// <param name="orgionalJson"></param>
        /// <returns></returns>
        public static string ParseOutRedditDataElement(string orgionalJson)
        {
            try
            {
                // Try to parse out the object
                int dataPos = orgionalJson.IndexOf("\"data\":");
                if (dataPos == -1) return null;
                int dataStartPos = orgionalJson.IndexOf('{', dataPos + 7);
                if (dataPos == -1) return null;
                int dataEndPos = orgionalJson.IndexOf("}", dataStartPos);
                if (dataPos == -1) return null;

                return orgionalJson.Substring(dataStartPos, (dataEndPos - dataStartPos + 1));
            }
            catch(Exception)
            {

            }

            return null;
        }

        /// <summary>
        /// Attempts to find some reddit content in a link. A subreddit, post or comments.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static RedditContentContainer TryToFindRedditContentInLink(string url)
        {
            string urlLower = url.ToLower();
            RedditContentContainer containter = null;

            // Try to find /r/ or r/ links
            if (urlLower.StartsWith("/r/") || urlLower.StartsWith("r/"))
            {
                // Get the display name
                int subStart = urlLower.IndexOf("r/");
                subStart += 2;

                // Make sure we don't have a trailing slash.
                int subEnd = urlLower.Length;
                if (urlLower.Length > 0 && urlLower[urlLower.Length - 1] == '/')
                {
                    subEnd--;
                }

                // Get the name.
                string displayName = urlLower.Substring(subStart, subEnd - subStart).Trim();
                containter = new RedditContentContainer()
                {
                    Type = RedditContentType.Subreddit,
                    Subreddit = displayName
                };
            }
            // Try to find any other reddit link
            else if(urlLower.Contains("reddit.com/"))
            {
                // Try to find the start of the subreddit
                int startSub = urlLower.IndexOf("r/");
                if(startSub != -1)
                {
                    startSub += 2;

                    // Try to find the end of the subreddit.
                    int endSub = FindNextUrlBreak(urlLower, startSub);

                    if (endSub > startSub)
                    {
                        // We found a subreddit!
                        containter = new RedditContentContainer();
                        containter.Subreddit = urlLower.Substring(startSub, endSub - startSub);
                        containter.Type = RedditContentType.Subreddit;

                        // See if we have a post
                        int postStart = url.IndexOf("comments/");
                        if(postStart != -1)
                        {
                            postStart += 9;

                            // Try to find the end
                            int postEnd = FindNextUrlBreak(urlLower, postStart);

                            if(postEnd > postStart)
                            {
                                // We found a post! Build on top of the subreddit
                                containter.Post = urlLower.Substring(postStart, postEnd - postStart);
                                containter.Type = RedditContentType.Post;

                                // Try to find a comment, for there to be a comment this should have a / after it.
                                if(urlLower.Length > postEnd && urlLower[postEnd] == '/')
                                {
                                    postEnd++;
                                    // Now try to find the / after the post title
                                    int commentStart = urlLower.IndexOf('/', postEnd);
                                    if(commentStart != -1)
                                    {
                                        commentStart++;

                                        // Try to find the end of the comment
                                        int commentEnd = FindNextUrlBreak(urlLower, commentStart);

                                        if(commentEnd > commentStart )
                                        {
                                            // We found a comment!
                                            containter.Comment = urlLower.Substring(commentStart, commentEnd - commentStart);
                                            containter.Type = RedditContentType.Comment;
                                        }
                                    }
                                }                               
                            }
                        }
                    }
                }
            }

            return containter;
        }

        private static int FindNextUrlBreak(string url, int startingPos)
        {
            int nextBreak = startingPos;
            while (url.Length > nextBreak && (Char.IsLetterOrDigit(url[nextBreak]) || url[nextBreak] == '_'))
            {
                nextBreak++;
            }
            return nextBreak;
        }
    }
}
