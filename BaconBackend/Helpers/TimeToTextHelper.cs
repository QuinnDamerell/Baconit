using System;

namespace BaconBackend.Helpers
{
    /// <summary>
    /// Helper class to convert an absolute time to text that represents 
    /// elapsed time.
    /// </summary>
    public class TimeToTextHelper
    {
        /// <summary>
        /// Returns a string of the time elapsed that is given. This will use the lowest 
        /// units possible for the format.
        /// </summary>
        /// <param name="time">Time to convert</param>
        /// <returns>The time string</returns>
        public static string TimeElapseToText(DateTime time)
        {
            var elapsed = DateTime.Now - time;
         
            if(elapsed.TotalSeconds < 60)
            {
                if(elapsed.TotalSeconds < 0)
                {
                    return "0 seconds";
                }
                return elapsed.TotalSeconds < 2 ? $"{(int)elapsed.TotalSeconds} second" : $"{(int)elapsed.TotalSeconds} seconds";
            }

            if (elapsed.TotalMinutes < 60)
            {
                return elapsed.TotalMinutes < 2 ? $"{(int)elapsed.TotalMinutes} minute" : $"{(int)elapsed.TotalMinutes} minutes";
            }
            if (elapsed.TotalHours < 24)
            {
                return elapsed.TotalHours < 2 ? $"{(int)elapsed.TotalHours} hour" : $"{(int)elapsed.TotalHours} hours";
            }
            if(elapsed.TotalDays < 365)
            {
                return elapsed.TotalDays < 2 ? $"{(int)elapsed.TotalDays} day" : $"{(int)elapsed.TotalDays} days";
            }
            var years = elapsed.TotalDays / 365;
            years = Math.Round(years, 1);
            return years == 1 ? $"{years} year" : $"{years} years";
        }
    }
}
