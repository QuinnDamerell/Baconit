using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Baconit.Interfaces
{
    public interface IMainPage
    {
        /// <summary>
        /// Called when a panel wants to show or hide the menu.
        /// </summary>
        /// <param name="show">If we should show or hide</param>
        void ToggleMenu(bool show);
    }
}
