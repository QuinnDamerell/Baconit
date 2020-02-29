using BaconBackend;
using BaconBackend.Helpers;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace BaconBackground
{
    public sealed class BackgroundEntry : IBackgroundTask
    {
        private BaconManager _mBaconMan;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            // Create the baconit manager
            _mBaconMan = new BaconManager(true);

            // Setup the ref counted deferral
            var refDeferral = new RefCountedDeferral(taskInstance.GetDeferral(), OnDeferralCleanup);

            // Add a ref so everyone in this call is protected
            refDeferral.AddRef();

            // Fire off the update
            await _mBaconMan.BackgroundMan.RunUpdate(refDeferral);

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
            using (var are = new AutoResetEvent(false))
            {
                Task.Run(async () =>
                {
                    // Flush out the local settings
                    await _mBaconMan.SettingsMan.FlushLocalSettings();
                    are.Set();
                });
                are.WaitOne();
            }                
        }
    }
}
