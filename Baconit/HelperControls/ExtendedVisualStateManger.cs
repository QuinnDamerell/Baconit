using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Baconit.HelperControls
{
    public class ExtendedVisualStateManager : VisualStateManager
    {
        protected override bool GoToStateCore(Control control, FrameworkElement stateGroupsRoot, string stateName, VisualStateGroup group, VisualState state, bool useTransitions)
        {
            if ((group == null) || (state == null))
            {
                return false;
            }

            if (control == null)
            {
                control = new ContentControl();
            }

            return base.GoToStateCore(control, stateGroupsRoot, stateName, group, state, useTransitions);
        }

        public static bool GoToElementState(FrameworkElement root, string stateName, bool useTransitions)
        {
            var customVisualStateManager = GetCustomVisualStateManager(root) as ExtendedVisualStateManager;

            return ((customVisualStateManager != null) && customVisualStateManager.GoToStateInternal(root, stateName, useTransitions));
        }

        private bool GoToStateInternal(FrameworkElement stateGroupsRoot, string stateName, bool useTransitions)
        {
            return (TryGetState(stateGroupsRoot, stateName, out var @group, out var state) && GoToStateCore(null, stateGroupsRoot, stateName, group, state, useTransitions));
        }

        private static bool TryGetState(FrameworkElement element, string stateName, out VisualStateGroup group, out VisualState state)
        {
            group = null;
            state = null;

            foreach (var group2 in GetVisualStateGroups(element))
            {
                foreach (var state2 in group2.States)
                {
                    if (state2.Name == stateName)
                    {
                        group = group2;
                        state = state2;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
