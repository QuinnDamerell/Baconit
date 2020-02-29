using BaconBackend.Helpers;
using System;
using System.Threading.Tasks;

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
    public class MemoryReportArgs : EventArgs
    {
        public MemoryPressureStates CurrentPressure;
        public ulong CurrentMemoryUsage;
        public ulong AvailableMemory;
        public double UsagePercentage;
    }

    /// <summary>
    /// Used to send memory clean up requests.
    /// </summary>
    public class MemoryCleanupRequestArgs : EventArgs
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
        private const double NoneMemoryPressureLimit = 0.3;
        private const double VeryLowMemoryPressureLimit = 0.5;
        private const double LowMemoryPressureLimit = 0.80;
        private const double MediumMemoryPressureLimit = .85;

        // For low just do it every now and then to free up pages we don't need.
        // For medium and high to it more frequently.
        private const int LowTickRollover = 200;
        private const int MediumAndHighTickRollover = 10;

        //
        // Events
        //

        /// <summary>
        /// Fired when for memory reports. These are used just to inform the app of the
        /// current memory state.
        /// </summary>
        public event EventHandler<MemoryReportArgs> OnMemoryReport
        {
            add => _memoryReport.Add(value);
            remove => _memoryReport.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<MemoryReportArgs>> _memoryReport = new SmartWeakEvent<EventHandler<MemoryReportArgs>>();

        /// <summary>
        /// Fired when we are asking people to cleanup up memory. This has a pressure that indicates
        /// how bad we need memory cleaned up.
        /// </summary>
        public event EventHandler<MemoryCleanupRequestArgs> OnMemoryCleanUpRequest
        {
            add => _memoryCleanUpRequest.Add(value);
            remove => _memoryCleanUpRequest.Remove(value);
        }

        private readonly SmartWeakEvent<EventHandler<MemoryCleanupRequestArgs>> _memoryCleanUpRequest = new SmartWeakEvent<EventHandler<MemoryCleanupRequestArgs>>();

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
        public double CurrentMemoryUsagePercentage;

        //
        // Private vars
        //

        /// <summary>
        /// Holds a ref to the manager
        /// </summary>
        private readonly BaconManager _baconMan;

        /// <summary>
        /// Indicates if we are running or not.
        /// </summary>
        private bool _isRunning;

        /// <summary>
        /// Used to keep track of how often we send a report.
        /// </summary>
        private int _reportTick;

        /// <summary>
        /// Used to keep track of how often we send a request to clean up
        /// memory.
        /// </summary>
        private int _cleanupTick;

        public MemoryManager(BaconManager baconMan)
        {
            _baconMan = baconMan;

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
                if(_isRunning)
                {
                    return;
                }
                _isRunning = true;
            }

            // Kick off a thread.
            Task.Run(async () =>
            {
                // Loop forever.
                while (true)
                {
                    // Sleep for some time, sleep longer if the memory pressure is lower.
                    var sleepTime = 100;
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
                        case MemoryPressureStates.VeryLow:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    await Task.Delay(sleepTime);

                    // Make sure we aren't stopped.
                    if (!_isRunning)
                    {
                        return;
                    }

                    // Calculate the current memory pressure.
                    var usedMemory = Windows.System.MemoryManager.AppMemoryUsage;
                    var memoryLimit = Windows.System.MemoryManager.AppMemoryUsageLimit;
                    CurrentMemoryUsagePercentage = (double)usedMemory / (double)memoryLimit;

                    // Set the pressure state.
                    var oldPressure = MemoryPressure;
                    if(CurrentMemoryUsagePercentage < NoneMemoryPressureLimit)
                    {
                        MemoryPressure = MemoryPressureStates.None;
                    }
                    else if (CurrentMemoryUsagePercentage < VeryLowMemoryPressureLimit)
                    {
                        MemoryPressure = MemoryPressureStates.VeryLow;
                    }
                    else if(CurrentMemoryUsagePercentage < LowMemoryPressureLimit)
                    {
                        MemoryPressure = MemoryPressureStates.Low;
                    }
                    else if (CurrentMemoryUsagePercentage < MediumMemoryPressureLimit)
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
                        _cleanupTick = 0;
                    }
                    // If our new state is lower than our old state.
                    else if(MemoryPressure < oldPressure)
                    {
                        // We did well, but we can still be at medium or low, reset the counter.
                        _cleanupTick = 0;
                    }
                    else
                    {
                        // Things are the same, if we are low or above take action.
                        if(MemoryPressure >= MemoryPressureStates.Low)
                        {
                            // Count
                            _cleanupTick++;

                            // Get the rollover count.
                            var tickRollover = MemoryPressure == MemoryPressureStates.Low ? LowTickRollover : MediumAndHighTickRollover;

                            // Check for roll over
                            if(_cleanupTick > tickRollover)
                            {
                                FireMemoryCleanup(MemoryPressure, oldPressure);
                                _cleanupTick = 0;
                            }
                        }
                    }

                    // For now since this is only used by developer settings, don't bother
                    // if it isn't enabled.
                    if (!_baconMan.UiSettingsMan.DeveloperShowMemoryOverlay) continue;
                    // Check if we need to send a report, we only want to send a report every
                    // 10 ticks so we don't spam too many. Also report if the state changed.
                    _reportTick++;
                    if (_reportTick <= 5 && oldPressure == MemoryPressure) continue;
                    FireMemoryReport(usedMemory, memoryLimit, CurrentMemoryUsagePercentage);
                    _reportTick = 0;
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
                _memoryReport.Raise(this, new MemoryReportArgs
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
                _baconMan.MessageMan.DebugDia("Memory report fire failed", e);
                TelemetryManager.ReportUnexpectedEvent(this, "MemoryReportFiredFailed", e);
            }
        }

        /// <summary>
        /// Fires a memory clean up request to those listening.
        /// </summary>
        /// <param name="current"></param>
        /// <param name="old"></param>
        public void FireMemoryCleanup(MemoryPressureStates current, MemoryPressureStates old)
        {
            try
            {
                _memoryCleanUpRequest.Raise(this, new MemoryCleanupRequestArgs
                    {
                    CurrentPressure = current,
                    LastPressure = old
                }
                );
            }
            catch (Exception e)
            {
                _baconMan.MessageMan.DebugDia("Memory cleanup fire failed", e);
                TelemetryManager.ReportUnexpectedEvent(this, "MemoryCleanupFiredFailed", e);
            }
        }
    }
}
