using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.System;

namespace BaconBackend.Managers
{
    public class MemoryManager
    {
        BaconManager m_baconMan;
        bool m_isRunning = false;

        public MemoryManager(BaconManager baconMan)
        {
            m_baconMan = baconMan;

            // Start the thread if we should
            StartMemoryWatch();
        }

        /// <summary>
        /// Starts the memory thread if needed.
        /// </summary>
        public void StartMemoryWatch()
        {
            // If we should run or not.
            if(m_baconMan.UiSettingsMan.Developer_ShowMemoryOverlay)
            {
                // If we should run.
                lock(this)
                {
                    if(m_isRunning)
                    {
                        return;
                    }
                    m_isRunning = true;
                }

                // Kick off a thread.
                Task.Run(async () =>
                {
                    // Loop forever.
                    while (true)
                    {
                        // Sleep for a second.
                        await Task.Delay(1000);

                        // Leave is not running.
                        if(!m_isRunning)
                        {
                            return;
                        }

                        // Send an update.
                        ulong usage = Windows.System.MemoryManager.AppMemoryUsage;
                        ulong limit = Windows.System.MemoryManager.AppMemoryUsageLimit;
                        m_baconMan.ReportMemoryUsage(usage, limit);
                    }
                });
            }
        }

        /// <summary>
        ///  Stops the memory UI thread.
        /// </summary>
        public void StopMemoryWatch()
        {
            lock(this)
            {
                m_isRunning = false;
            }
        }
    }
}
