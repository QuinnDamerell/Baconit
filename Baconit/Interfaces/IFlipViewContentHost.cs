using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baconit.Interfaces
{
    public interface IFlipViewContentHost
    {
        /// <summary>
        /// Shows the loading UI on over content
        /// </summary>
        void ShowLoading();

        /// <summary>
        /// Hides the loading UI over the content
        /// </summary>
        void HideLoading();

        /// <summary>
        /// Shows an error
        /// </summary>
        void ShowError();
    }
}
