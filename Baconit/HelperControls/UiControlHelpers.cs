using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Baconit.HelperControls
{
    internal class UiControlHelpers<T>
    {
        static public void RecursivelyFindElement(DependencyObject root, ref List<DependencyObject> foundList, int maxListCount = 9999999)
        {
            // Check for null
            if (root == null || foundList == null)
            {
                return;
            }

            // Get the count
            var maxCount = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < maxCount; i++)
            {
                // Grab the current object
                var curretnObject = VisualTreeHelper.GetChild(root, i);

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
