using UnityEngine;

namespace QuietStatic.Input
{
    /// <summary>
    /// Provides movement-related input for gameplay systems.
    /// </summary>
    public interface IMoveInputSource
    {
        /// <summary>
        /// Gets the current movement input vector.
        /// X = horizontal input, Y = vertical input.
        /// </summary>
        Vector2 Move { get; }

        /// <summary>
        /// Gets whether sprint input is currently being held.
        /// </summary>
        bool Sprint { get; }

        /// <summary>
        /// Consumes the current jump press so it is only handled once.
        /// </summary>
        /// <returns>
        /// True if jump was pressed and consumed; otherwise, false.
        /// </returns>
        bool ConsumeJump();
    }
}