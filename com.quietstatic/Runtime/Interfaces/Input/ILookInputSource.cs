using UnityEngine;

namespace QuietStatic.Input
{
    /// <summary>
    /// Provides look input for camera or aiming systems.
    /// </summary>
    public interface ILookInputSource
    {
        /// <summary>
        /// Gets the current look input vector.
        /// X = horizontal look, Y = vertical look.
        /// </summary>
        Vector2 Look { get; }
    }
}