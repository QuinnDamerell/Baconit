using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.Helpers
{
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
            TimeSpan elapsed = DateTime.Now - time;
         
            if(elapsed.TotalSeconds < 60)
            {
                if(elapsed.TotalSeconds < 0)
                {
                    return "0 seconds";
                }
                if (elapsed.TotalSeconds < 2)
                {
                    return $"{(int)elapsed.TotalSeconds} second";
                }
                else
                {
                    return $"{(int)elapsed.TotalSeconds} seconds";
                }
            }
            else if (elapsed.TotalMinutes < 60)
            {
                if (elapsed.TotalMinutes < 2)
                {
                    return $"{(int)elapsed.TotalMinutes} minute";
                }
                else
                {
                    return $"{(int)elapsed.TotalMinutes} minutes";
                }
            }
            else if (elapsed.TotalHours < 24)
            {
                if (elapsed.TotalHours < 2)
                {
                    return $"{(int)elapsed.TotalHours} hour";
                }
                else
                {
                    return $"{(int)elapsed.TotalHours} hours";
                }
            }
            else if(elapsed.TotalDays < 365)
            {
                if (elapsed.TotalDays < 2)
                {
                    return $"{(int)elapsed.TotalDays} day";
                }
                else
                {
                    return $"{(int)elapsed.TotalDays} days";
                }
            }
            else
            {
                double years = elapsed.TotalDays / 365;
                years = Math.Round(years, 1);
                if(years == 1)
                {
                    return $"{years} year";
                }
                else
                {
                    return $"{years} years";
                }
            }
        }
    }
}
