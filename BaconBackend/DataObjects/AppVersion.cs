using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.DataObjects
{
    /// <summary>
    /// A helper class to represent the app version.
    /// </summary>
    public class AppVersion
    {
        public ushort Major;
        public ushort Minor;
        public ushort Build;
        public ushort Revision;
    }
}
