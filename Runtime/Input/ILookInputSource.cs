using UnityEngine;

namespace QuietStatic.Input
{
    /// <summary>
    /// Defines a reusable source of two-dimensional look input.
    /// </summary>
    /// <remarks>
    /// Implement this interface on any component that can provide camera, aiming,
    /// or cursor-style look input. This keeps camera and aiming systems independent
    /// from a specific input implementation, such as Unity's old Input Manager,
    /// the newer Input System, AI-controlled look behavior, or scripted cutscene input.
    ///
    /// The returned value is intentionally generic. Implementations decide whether
    /// the vector represents mouse delta, controller stick input, touch movement,
    /// or another look source.
    /// </remarks>
    public interface ILookInputSource
    {
        /// <summary>
        /// Gets the current look input vector.
        /// </summary>
        /// <value>
        /// A two-dimensional look vector where X represents horizontal look input
        /// and Y represents vertical look input.
        /// </value>
        /// <remarks>
        /// Consumers usually read this once per frame and apply their own sensitivity,
        /// smoothing, clamping, or inversion settings. For example, a camera controller
        /// might use X to adjust yaw and Y to adjust pitch.
        /// </remarks>
        Vector2 Look { get; }
    }
}
