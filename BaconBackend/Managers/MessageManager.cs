using System;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Popups;

namespace BaconBackend.Managers
{
#pragma warning disable CS4014
    public class MessageManager
    {
        private readonly BaconManager _baconMan;

        public MessageManager(BaconManager baconMan)
        {
            _baconMan = baconMan;
        }

        public void ShowMessageSimple(string title, string content)
        {
            ShowMessage(content, title);
        }

        public async void ShowRedditDownMessage()
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    var showStatus = await ShowYesNoMessage("Reddit is Down", "It looks like reddit is down right now. Go outside for a while and try again in a few minutes.", "Check Reddit's Status", "Go Outside");
                    if(showStatus.HasValue && showStatus.Value)
                    {
                        _baconMan.ShowGlobalContent("http://www.redditstatus.com/");
                    }
                }
                catch (Exception e)
                {
                    TelemetryManager.ReportUnexpectedEvent(this, "FailedToShowMessage", e);
                }
            });
        }

        public async void ShowSigninMessage(string toDoWhat)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                // Add the buttons
                var response = await ShowYesNoMessage("Login Required", $"You must be logged into to a reddit account to {toDoWhat}. Do you want to login or create a new account now?");

                if(response.HasValue && response.Value)
                {
                    _baconMan.NavigateToLogin();
                }
            });
        }


        public void DebugDia(string str, Exception ex = null)
        {
            if (!_baconMan.UiSettingsMan.DeveloperDebug) return;
            System.Diagnostics.Debug.WriteLine("Error, " + str + " Message: " + (ex == null ? "" : ex.Message));
            ShowMessage("DebugDia: str " + str + " \n\nMessage: " + (ex == null ? "" : ex.Message) + "\n\nCall Stack:\n"+(ex== null? "" : ex.StackTrace), "DebugDia");
        }

        private async void ShowMessage(string content, string title)
        {
            // Don't show messages if we are in the background.
            if(_baconMan.IsBackgroundTask)
            {
                return;
            }

            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    var message = new MessageDialog(content, title);
                    await message.ShowAsync();
                }
                catch(Exception e)
                {
                    TelemetryManager.ReportUnexpectedEvent(this, "FailedToShowMessage",e);
                }
            });
        }

        /// <summary>
        /// Shows a yes no dialog with a message.
        /// MUST BE CALLED FROM THE UI THREAD!
        /// </summary>
        /// <param name="title"></param>
        /// <param name="content"></param>
        /// <param name="positiveButton"></param>
        /// <param name="negativeButton"></param>
        /// <returns></returns>
        public async Task<bool?> ShowYesNoMessage(string title, string content, string positiveButton = "Yes", string negativeButton = "No")
        {
            // Don't show messages if we are in the background.
            if (_baconMan.IsBackgroundTask)
            {
                return null;
            }

            bool? response = null;

            // Add the buttons
            var message = new MessageDialog(content, title);
            message.Commands.Add(new UICommand(
                positiveButton,
                command=> {
                    response = true; }));
            message.Commands.Add(new UICommand(
                negativeButton,
                command => { response = false; }));
            message.DefaultCommandIndex = 0;
            message.CancelCommandIndex = 1;

            // Show the dialog
            await message.ShowAsync();

            return response;
        }
    }
}
