using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.Interfaces
{
    /// <summary>
    /// Foreground object that can be directed by the backend.
    /// </summary>
    public interface IBackendActionListener
    {
        /// <summary>
        /// Show webpage or reddit content in the app.
        /// </summary>
        /// <param name="link">URL or reddit content reference.</param>
        void ShowGlobalContent(string link);

        /// <summary>
        /// Show webpage or reddit content in the app.
        /// </summary>
        /// <param name="container">Reference to the webpage or reddit content that should be shown.</param>
        void ShowGlobalContent(RedditContentContainer container);

        /// <summary>
        /// Show the message of the day as a pop up in the app.
        /// </summary>
        /// <param name="title">Title of the message of the day.</param>
        /// <param name="markdownContent">Body of the message of the day, in Markdown.</param>
        void ShowMessageOfTheDay(string title, string markdownContent);

        /// <summary>
        /// Navigate the app to a login form.
        /// </summary>
        void NavigateToLogin();

        /// <summary>
        /// Navigate the app back.
        /// </summary>
        /// <returns>If the back navigation was handled.</returns>
        bool NavigateBack();

        /// <summary>
        /// Reports a new value for the memory usage of the app.
        /// </summary>
        /// <param name="currentUsage"></param>
        /// <param name="maxLimit"></param>
        void ReportMemoryUsage(ulong currentUsage, ulong maxLimit);
    }
}
