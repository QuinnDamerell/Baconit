using Baconit.ContentPanels;

namespace Baconit.Interfaces
{
    /// <summary>
    /// Indicates how large the panel is in memory.
    /// </summary>
    public enum PanelMemorySizes
    {
        Small,
        Medium,
        Large
    }

    public interface IContentPanelBase
    {
        /// <summary>
        /// Indicates if the control is loading.
        /// </summary>
        bool IsLoading { get; }

        /// <summary>
        /// Indicates if the control has an error.
        /// </summary>
        bool HasError { get; }

        /// <summary>
        /// The error text if we have it.
        /// </summary>
        string ErrorText { get; }

        /// <summary>
        /// Indicates how large the panel is in memory.
        /// </summary>
        PanelMemorySizes PanelMemorySize { get; }

        /// <summary>
        /// The source for the panel.
        /// </summary>
        ContentPanelSource Source { get; }

        /// <summary>
        /// The actual panel contained within.
        /// </summary>
        IContentPanel Panel { get; }

        /// <summary>
        /// Called when the control's visibility is changed.
        /// </summary>
        void OnVisibilityChanged(bool isVisible);

        /// <summary>
        /// Called when the flip view content should be destroyed.
        /// </summary>
        void OnDestroyContent();

        /// <summary>
        /// Called when a new host for the control has been added.
        /// </summary>
        /// <param name="host"></param>
        void OnHostAdded(IContentPanelHost host);

        /// <summary>
        /// Called when the old host is removed.
        /// </summary>
        void OnHostRemoved();
    }
}
