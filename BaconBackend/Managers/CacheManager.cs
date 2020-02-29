namespace BaconBackend.Managers
{
    public class CacheManager
    {
        private BaconManager _baconMan;

        public CacheManager(BaconManager baconMan)
        {
            _baconMan = baconMan;
        }

        public static bool HasFileCached(string fileName)
        {
            return false;
        }
    }
}
