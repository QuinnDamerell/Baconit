using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.Helpers
{
    /// <summary>
    /// A generic reddit response to data submission.
    /// TODO: Figure out what this really is.
    /// </summary>
    public class RedditGenericResponse
    {
        /// <summary>
        /// Whether there is no error in the response received from submitting new content.
        /// </summary>
        /// <param name="responseString">Response received from submitting new content.</param>
        /// <returns>Whether there is no error.</returns>
        public static bool LookForError(string responseString)
        {
            return true;
        }
    }
}
