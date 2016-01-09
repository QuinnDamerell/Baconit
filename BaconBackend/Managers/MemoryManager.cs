using BaconBackend.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.System;

namespace BaconBackend.Managers
{
    /// <summary>
    /// Indicates the current memory pressure state of the app.
    ///
    /// On none we will do nothing, we are good.
    /// On low pressure we will ask the panel manager to free up very old pages.
    /// On medium we will ask panel manager to free up anything it can.
    /// On high we will stop any loading web page (and ideally images).
    /// </summary>
    public enum MemoryPressureStates
    {
        None = 0,
        VeryLow = 1,
        Low = 2,
        Medium = 3,
        HighNoAllocations = 4
    }

    /// <summary>
    /// Used to send out memory reports.
    /// </summary>
    public class OnMemoryReportArgs : EventArgs
    {
        public MemoryPressureStates CurrentPressure;
        public ulong CurrentMemoryUsage;
        public ulong AvailableMemory;
        public double UsagePercentage;
    }

    /// <summary>
    /// Used to send memory clean up requests.
    /// </summary>
    public class OnMemoryCleanupRequestArgs : EventArgs
    {
        public MemoryPressureStates LastPressure;
        public MemoryPressureStates CurrentPressure;
    }

    /// <summary>
    /// Used by Baconit to manage the memory state of the app.
    /// </summary>
    public class MemoryManager
    {
        //
        // Const
        //

        // These are the limits we use to define the memory pressure.
        // Expressed as % of memory available.
        const double c_noneMemoryPressueLimit = 0.3;
        const double c_veryLowMemoryPressueLimit = 0.5;
        const double c_lowMemoryPressueLimit = 0.80;
        const double c_mediumMemoryPressueLimit = .85;

        // For low just do it every now and then to free up pages we don't need.
        // For medium and high to it more frequently.
        const int c_lowTickRollover = 200;
        const int c_mediumAndHighTickRollover = 10;

        //
        // Events
        //

        /// <summary>
        /// Fired when for memory reports. These are used just to inform the app of the
        /// current memory state.
        /// </summary>
        public event EventHandler<OnMemoryReportArgs> OnMemoryReport
        {
            add { m_onMemoryReport.Add(value); }
            remove { m_onMemoryReport.Remove(value); }
        }
        SmartWeakEvent<EventHandler<OnMemoryReportArgs>> m_onMemoryReport = new SmartWeakEvent<EventHandler<OnMemoryReportArgs>>();

        /// <summary>
        /// Fired when we are asking people to cleanup up memory. This has a pressure that indicates
        /// how bad we need memory cleaned up.
        /// </summary>
        public event EventHandler<OnMemoryCleanupRequestArgs> OnMemoryCleanUpRequest
        {
            add { m_onMemoryCleanUpRequest.Add(value); }
            remove { m_onMemoryCleanUpRequest.Remove(value); }
        }
        SmartWeakEvent<EventHandler<OnMemoryCleanupRequestArgs>> m_onMemoryCleanUpRequest = new SmartWeakEvent<EventHandler<OnMemoryCleanupRequestArgs>>();

        //
        // Public vars
        //

        /// <summary>
        /// Indicates the current memory pressure state of the app.
        /// </summary>
        public MemoryPressureStates MemoryPressure;

        /// <summary>
        /// How much memory we are currently using.
        /// </summary>
        public double CurrentMemoryUsagePercentage = 0.0;

        //
        // Private vars
        //

        /// <summary>
        /// Holds a ref to the manager
        /// </summary>
        BaconManager m_baconMan;

        /// <summary>
        /// Indicates if we are running or not.
        /// </summary>
        bool m_isRunning = false;

        /// <summary>
        /// Used to keep track of how often we send a report.
        /// </summary>
        int m_reportTick = 0;

        /// <summary>
        /// Used to keep track of how often we send a request to clean up
        /// memory.
        /// </summary>
        int m_cleanupTick = 0;

        public MemoryManager(BaconManager baconMan)
        {
            m_baconMan = baconMan;

            // Start the watching thread.
            StartMemoryWatch();
        }

        /// <summary>
        /// Starts the memory thread.
        /// </summary>
        public void StartMemoryWatch()
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
                    // Sleep for some time, sleep longer if the memory pressure is lower.
                    int sleepTime = 100;
                    switch(MemoryPressure)
                    {
                        case MemoryPressureStates.None:
                            sleepTime = 500;
                            break;
                        case MemoryPressureStates.Low:
                            sleepTime = 300;
                            break;
                        case MemoryPressureStates.Medium:
                        case MemoryPressureStates.HighNoAllocations:
                            sleepTime = 100;
                            break;
                    }
                    await Task.Delay(sleepTime);

                    // Make sure we aren't stopped.
                    if (!m_isRunning)
                    {
                        return;
                    }

                    // Calculate the current memory pressure.
                    ulong usedMemory = Windows.System.MemoryManager.AppMemoryUsage;
                    ulong memoryLimit = Windows.System.MemoryManager.AppMemoryUsageLimit;
                    CurrentMemoryUsagePercentage = (double)usedMemory / (double)memoryLimit;

                    // Set the pressure state.
                    MemoryPressureStates oldPressure = MemoryPressure;
                    if(CurrentMemoryUsagePercentage < c_noneMemoryPressueLimit)
                    {
                        MemoryPressure = MemoryPressureStates.None;
                    }
                    else if (CurrentMemoryUsagePercentage < c_veryLowMemoryPressueLimit)
                    {
                        MemoryPressure = MemoryPressureStates.VeryLow;
                    }
                    else if(CurrentMemoryUsagePercentage < c_lowMemoryPressueLimit)
                    {
                        MemoryPressure = MemoryPressureStates.Low;
                    }
                    else if (CurrentMemoryUsagePercentage < c_mediumMemoryPressueLimit)
                    {
                        MemoryPressure = MemoryPressureStates.Medium;
                    }
                    else
                    {
                        MemoryPressure = MemoryPressureStates.HighNoAllocations;
                    }

                    // If our new state is higher than our old state
                    if (MemoryPressure > oldPressure)
                    {
                        // We went up a state, Fire the cleanup request since we are at least low.
                        FireMemoryCleanup(MemoryPressure, oldPressure);

                        // Set the count
                        m_cleanupTick = 0;
                    }
                    // If our new state is lower than our old state.
                    else if(MemoryPressure < oldPressure)
                    {
                        // We did well, but we can still be at medium or low, reset the counter.
                        m_cleanupTick = 0;
                    }
                    else
                    {
                        // Things are the same, if we are low or above take action.
                        if(MemoryPressure >= MemoryPressureStates.Low)
                        {
                            // Count
                            m_cleanupTick++;

                            // Get the rollover count.
                            int tickRollover = MemoryPressure == MemoryPressureStates.Low ? c_lowTickRollover : c_mediumAndHighTickRollover;

                            // Check for roll over
                            if(m_cleanupTick > tickRollover)
                            {
                                FireMemoryCleanup(MemoryPressure, oldPressure);
                                m_cleanupTick = 0;
                            }
                        }
                    }

                    // For now since this is only used by developer settings, don't bother
                    // if it isn't enabled.
                    if (m_baconMan.UiSettingsMan.Developer_ShowMemoryOverlay)
                    {
                        // Check if we need to send a report, we only want to send a report every
                        // 10 ticks so we don't spam too many. Also report if the state changed.
                        m_reportTick++;
                        if (m_reportTick > 5 || oldPressure != MemoryPressure)
                        {
                            FireMemoryReport(usedMemory, memoryLimit, CurrentMemoryUsagePercentage);
                            m_reportTick = 0;
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Fires a memory report off to all who are listening.
        /// </summary>
        /// <param name="usedMemory"></param>
        /// <param name="memoryLimit"></param>
        /// <param name="usedPercentage"></param>
        public void FireMemoryReport(ulong usedMemory, ulong memoryLimit, double usedPercentage)
        {
            try
            {
                m_onMemoryReport.Raise(this, new OnMemoryReportArgs()
                {
                    AvailableMemory = memoryLimit,
                    CurrentMemoryUsage = usedMemory,
                    CurrentPressure = MemoryPressure,
                    UsagePercentage = usedPercentage
                }
                );
            }
            catch(Exception e)
            {
                m_baconMan.MessageMan.DebugDia("Memory report fire failed", e);
                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "MemeoryReportFiredFailed", e);
            }
        }

        /// <summary>
        /// Fires a memory clean up request to those listening.
        /// </summary>
        /// <param name="usedMemory"></param>
        /// <param name="memoryLimit"></param>
        /// <param name="usedPercentage"></param>
        public void FireMemoryCleanup(MemoryPressureStates current, MemoryPressureStates old)
        {
            try
            {
                m_onMemoryCleanUpRequest.Raise(this, new OnMemoryCleanupRequestArgs()
                {
                    CurrentPressure = current,
                    LastPressure = old
                }
                );
            }
            catch (Exception e)
            {
                m_baconMan.MessageMan.DebugDia("Memory cleanup fire failed", e);
                m_baconMan.TelemetryMan.ReportUnExpectedEvent(this, "MemeoryCleanupFiredFailed", e);
            }
        }
    }
}
