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
    }
}
