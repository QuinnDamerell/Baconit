using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.Interfaces
{
    public interface IBackendActionListener
    {
        void ShowGlobalContent(string link);

        void ShowGlobalContent(RedditContentContainer container);

        void ShowMessageOfTheDay(string title, string markdownContent);

        void NavigateToLogin();

        bool NavigateBack();
    }
}
