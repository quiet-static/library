using UnityEngine;

namespace QuietStatic.Input
{
    /// <summary>
    /// Provides interaction-related input.
    /// </summary>
    public interface IInteractInputSource
    {
        /// <summary>
        /// Gets whether the interact input was pressed this frame.
        /// </summary>
        bool Interact { get; }
    }
}