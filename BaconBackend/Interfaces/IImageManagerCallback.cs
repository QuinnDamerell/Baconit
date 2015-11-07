using BaconBackend.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaconBackend.Interfaces
{
    public interface IImageManagerCallback
    {
        void OnRequestComplete(ImageManager.ImageManagerResponse response);
    }
}
