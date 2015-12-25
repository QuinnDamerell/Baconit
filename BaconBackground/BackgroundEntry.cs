using BaconBackend;
using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace BaconBackground
{
    public sealed class BackgroundEntry : IBackgroundTask
    {
        BaconManager m_baconMan;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            // Create the baconit manager
            m_baconMan = new BaconManager(true);

            // Setup the ref counted deferral
            RefCountedDeferral refDeferral = new RefCountedDeferral(taskInstance.GetDeferral(), OnDeferralCleanup);

            // Add a ref so everyone in this call is protected
            refDeferral.AddRef();

            // Fire off the update
            await m_baconMan.BackgroundMan.RunUpdate(refDeferral);

            // After this returns the deferral will call complete unless someone else took a ref on it.
            refDeferral.ReleaseRef();           
        }

        /// <summary>
        /// Fired just before the deferral is about to complete. We need to flush the settings here.
        /// </summary>
        public void OnDeferralCleanup()
        {
            // We need to flush the settings here. We need to block this function
            // until the settings are flushed.
            using (AutoResetEvent are = new AutoResetEvent(false))
            {
                Task.Run(async () =>
                {
                    // Flush out the local settings
                    await m_baconMan.SettingsMan.FlushLocalSettings();
                    are.Set();
                });
                are.WaitOne();
            }                
        }
    }
}
