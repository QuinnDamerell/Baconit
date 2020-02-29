using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;

namespace BaconBackend.Helpers
{
    public class RefCountedDeferral
    {
        private readonly SuspendingDeferral _suspendingDeferral;
        private readonly BackgroundTaskDeferral _deferral;
        private readonly Action _cleanUpAction;
        private int _refCount;

        /// <summary>
        /// Note the deferral can be null.
        /// </summary>
        /// <param name="deferral"></param>
        public RefCountedDeferral(BackgroundTaskDeferral deferral)
        {
            _deferral = deferral;
        }

        /// <summary>
        /// Note the deferral can be null.
        /// </summary>
        /// <param name="deferral"></param>
        public RefCountedDeferral(BackgroundTaskDeferral deferral, Action cleanUpAction)
        {
            _deferral = deferral;
            _cleanUpAction = cleanUpAction;
        }

        /// <summary>
        /// Note the deferral can be null.
        /// </summary>
        /// <param name="deferral"></param>
        public RefCountedDeferral(SuspendingDeferral deferral, Action cleanUpAction)
        {
            _suspendingDeferral = deferral;
            _cleanUpAction = cleanUpAction;
        }

        /// <summary>
        /// Adds a ref to the deferral
        /// </summary>
        public void AddRef()
        {
            lock(this)
            {
                _refCount++;
            }
        }

        /// <summary>
        /// Release a ref to the deferral. If this is the last ref the deferral will be complete.
        /// </summary>
        public void ReleaseRef()
        {
            lock (this)
            {
                _refCount--;

                if (_refCount != 0) return;
                // If we have a cleanup action fire it now.
                _cleanUpAction?.Invoke();

                // Note the deferral can be null, this will happen when we update while
                // the app is updating.
                _deferral?.Complete();

                _suspendingDeferral?.Complete();
            }
        }
    }
}
