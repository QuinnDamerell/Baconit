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
