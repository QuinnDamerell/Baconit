using BaconBackend;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace BaconBackground
{
    public sealed class BackgroundEntry : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            // Grab the deferral in case anything uses async.
            BackgroundTaskDeferral defferal = taskInstance.GetDeferral();

            // Create the baconit manager
            BaconManager baconManager = new BaconManager(true);

            // Fire off the update, but don't do it async. We want to block this thread.
            baconManager.FireOffUpdate(false);
        }
    }
}
