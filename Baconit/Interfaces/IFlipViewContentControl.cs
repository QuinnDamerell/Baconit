using BaconBackend.DataObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baconit.Interfaces
{
    interface IFlipViewContentControl
    {
        /// <summary>
        /// Called when the control should prepare to show the content.
        /// </summary>
        /// <param name="url"></param>
        void OnPrepareContent(Post post);

        /// <summary>
        /// Called when the control is actually visible and should show content.
        /// </summary>
        void OnVisible();


        /// <summary>
        /// Called when the flip view content should be destroyed.
        /// </summary>
        void OnDestroyContent();
    }
}
