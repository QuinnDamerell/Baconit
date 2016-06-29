using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.System.Profile;

namespace BaconBackend.Helpers
{
    public enum DeviceTypes
    {
        Desktop,
        Mobile,
        Xbox,
        Other
    }

    public class DeviceHelper
    {
        public static DeviceTypes CurrentDevice()
        {
            string deviceFamily = AnalyticsInfo.VersionInfo.DeviceFamily;
            switch(deviceFamily)
            {
                case "Windows.Desktop":
                    return DeviceTypes.Desktop;
                case "Windows.Mobile":
                    return DeviceTypes.Mobile;
                case "Windows.Xbox":
                    return DeviceTypes.Xbox;
                default:
                    return DeviceTypes.Other;

            }
        }
    }
}
