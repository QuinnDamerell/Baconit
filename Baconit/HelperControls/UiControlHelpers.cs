using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Baconit.HelperControls
{
    class UiControlHelpers<T>
    {
        static public void RecursivelyFindElement(DependencyObject root, ref List<DependencyObject> foundList, int maxListCount = 9999999)
        {
            // Check for null
            if (root == null || foundList == null)
            {
                return;
            }

            // Get the count
            int maxCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < maxCount; i++)
            {
                // Grab the current object
                DependencyObject curretnObject = VisualTreeHelper.GetChild(root, i);

                // See if is what we are looking for
                if (curretnObject.GetType() == typeof(T))
                {
                    foundList.Add(curretnObject);
                }

                // Check the limit
                if(foundList.Count > maxListCount)
                {
                    return;
                }

                // Check it's children
                RecursivelyFindElement(curretnObject, ref foundList);
            }

            // Return null if we failed
            return;
        }   
    }
}
