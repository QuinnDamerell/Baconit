using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.Managers
{
    public class CacheManager
    {
        BaconManager m_baconMan;

        public CacheManager(BaconManager baconMan)
        {
            m_baconMan = baconMan;
        }

        public bool HasFileCached(string fileName)
        {
            return false;
        }
    }
}
