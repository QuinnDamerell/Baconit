using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;

namespace BaconBackend.Helpers
{
    public class RefCountedDeferral
    {
        SuspendingDeferral m_suspendingDeferral;
        BackgroundTaskDeferral m_deferral;
        Action m_cleanUpAction;
        int m_refCount;

        /// <summary>
        /// Note the deferral can be null.
        /// </summary>
        /// <param name="deferral"></param>
        public RefCountedDeferral(BackgroundTaskDeferral deferral)
        {
            m_deferral = deferral;
        }

        /// <summary>
        /// Note the deferral can be null.
        /// </summary>
        /// <param name="deferral"></param>
        public RefCountedDeferral(BackgroundTaskDeferral deferral, Action cleanUpAction)
        {
            m_deferral = deferral;
            m_cleanUpAction = cleanUpAction;
        }

        /// <summary>
        /// Note the deferral can be null.
        /// </summary>
        /// <param name="deferral"></param>
        public RefCountedDeferral(SuspendingDeferral deferral, Action cleanUpAction)
        {
            m_suspendingDeferral = deferral;
            m_cleanUpAction = cleanUpAction;
        }

        /// <summary>
        /// Adds a ref to the deferral
        /// </summary>
        public void AddRef()
        {
            lock(this)
            {
                m_refCount++;
            }
        }

        /// <summary>
        /// Release a ref to the deferral. If this is the last ref the deferral will be complete.
        /// </summary>
        public void ReleaseRef()
        {
            lock (this)
            {
                m_refCount--;

                if(m_refCount == 0)
                {
                    // If we have a cleanup action fire it now.
                    if (m_cleanUpAction != null)
                    {
                        m_cleanUpAction.Invoke();
                    }

                    // Note the deferral can be null, this will happen when we update while
                    // the app is updating.
                    if (m_deferral != null)
                    {
                        m_deferral.Complete();
                    }

                    if (m_suspendingDeferral != null)
                    {
                        m_suspendingDeferral.Complete();
                    }
                }
            }
        }
    }
}
