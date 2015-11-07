using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.Managers
{
    public class TelemetryManager
    {    
        /// <summary>
        /// Reports an event to the telemetry manager.
        /// </summary>
        /// <param name="component">The object reporting the event, this will be logged</param>
        /// <param name="eventName"></param>
        public void ReportEvent(object component, string eventName)
        {
            TelemetryClient client = new TelemetryClient();
            client.TrackEvent(component.GetType().Name + ":" +eventName);
        }

        /// <summary>
        /// Reports an event with a string data to the telemetry system.
        /// </summary>
        /// <param name="eventName"></param>
        public void ReportEvent(object component, string eventName, string data)
        {
            TelemetryClient client = new TelemetryClient();
            EventTelemetry eventT = new EventTelemetry();
            eventT.Name = component.GetType().Name + ":" + eventName;
            eventT.Properties.Add("data", data);
            client.TrackEvent(eventName);
        }

        /// <summary>
        /// Reports an event that might need to be looked at, an unexpected event.
        /// </summary>
        /// <param name="eventName"></param>
        public void ReportUnExpectedEvent(object component, string eventName, Exception excpetion = null)
        {
            TelemetryClient client = new TelemetryClient();
            EventTelemetry eventT = new EventTelemetry();
            eventT.Name = component.GetType().Name + ":" + eventName;
            eventT.Properties.Add("error", "unexpected");
            if(excpetion != null)
            {
                eventT.Properties.Add("exception", excpetion.Message);
            }
            client.TrackEvent(eventName);
        }

        /// <summary>
        /// Reports an perf event on how long something took.
        /// </summary>
        /// <param name="eventName"></param>
        public void ReportPerfEvent(object component, string eventName, TimeSpan timeTaken)
        {
            TelemetryClient client = new TelemetryClient();
            client.TrackMetric(component.GetType().Name + ":" + eventName, timeTaken.TotalMilliseconds);
        }

        /// <summary>
        /// Reports an perf event on how long something took. Here you pass the begin
        /// time and the delta will be computed
        /// </summary>
        /// <param name="eventName"></param>
        public void ReportPerfEvent(object component, string eventName, DateTime startTime)
        {
            TelemetryClient client = new TelemetryClient();
            client.TrackMetric(component.GetType().Name + ":" + eventName, (DateTime.Now - startTime).TotalMilliseconds);
        }

        /// <summary>
        /// Reports a metric event to the telemetry system.
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="metric"></param>
        public void ReportMetric(object component, string eventName, double metric)
        {
            TelemetryClient client = new TelemetryClient();
            client.TrackMetric(component.GetType().Name + ":" + eventName + ":" + eventName, metric);
        }

        /// <summary>
        /// Track page view
        /// </summary>
        /// <param name="pageName"></param>
        public void ReportPageView(string pageName)
        {
            TelemetryClient client = new TelemetryClient();
            client.TrackPageView(pageName);
        }
    }
}
