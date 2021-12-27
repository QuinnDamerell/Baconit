using System;
using System.Collections.Generic;
using System.Linq;

namespace BaconBackend
{
    public static class Extensions
    {
        static readonly char[] QueryStringSeparator1 = "#".ToCharArray();
        static readonly char[] QueryStringSeparator3 = "&".ToCharArray();
        static readonly char[] QueryStringSeparator4 = "=".ToCharArray();

        public static Dictionary<string, string> QueryDictionary(this Uri uri)
        {
            return uri.Query
                       .Split(QueryStringSeparator1, StringSplitOptions.RemoveEmptyEntries)
                       .Select(a => a.Substring(1)
                           .Split(QueryStringSeparator3, StringSplitOptions.RemoveEmptyEntries)
                           .Select(c => c.Split(QueryStringSeparator4))
                           .Where(c => c[0].Length > 0)
                           .ToDictionary(c => Uri.UnescapeDataString(c[0]), c => c.Length > 1 ? Uri.UnescapeDataString(c[1]) : ""))
                       .FirstOrDefault()// before #
                   ?? new Dictionary<string, string>();
        }

        public static string UseOldReddit(this string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            var tempUrl = url.ToLowerInvariant();
            if (tempUrl.Contains("www.reddit") && tempUrl.Contains("comments/"))
            {
                url = url.Replace("www.reddit", "old.reddit");
            }

            return url;
        }
    }
}
