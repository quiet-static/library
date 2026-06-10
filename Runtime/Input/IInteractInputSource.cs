using UnityEngine;

namespace QuietStatic.Input
{
    /// <summary>
    /// Defines a reusable input source for interaction actions.
    /// </summary>
    /// <remarks>
    /// Implement this interface on any component that can report whether the player
    /// requested an interaction during the current frame. This keeps interaction
    /// systems independent from a specific input implementation, such as Unity's
    /// legacy input manager, the new Input System, AI-driven input, or test input.
    /// </remarks>
    public interface IInteractInputSource
    {
        /// <summary>
        /// Gets a value indicating whether the interact input was pressed during
        /// the current frame.
        /// </summary>
        /// <value>
        /// <c>true</c> only on the frame the interaction input is pressed;
        /// otherwise, <c>false</c>.
        /// </value>
        /// <remarks>
        /// This should behave like a button-down check rather than a held-button
        /// check. For example, a keyboard implementation would usually return the
        /// equivalent of <c>GetKeyDown</c>, not <c>GetKey</c>.
        /// </remarks>
        bool Interact { get; }
    }
}
