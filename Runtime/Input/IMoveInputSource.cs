using UnityEngine;

namespace QuietStatic.Input
{
    /// <summary>
    /// Defines a reusable source of movement-related player input.
    /// </summary>
    /// <remarks>
    /// Implement this interface on any component that can provide movement controls
    /// to gameplay systems such as character motors, player controllers, or AI/input
    /// adapters.
    ///
    /// The purpose of this interface is to keep movement code independent from a
    /// specific input implementation. For example, one implementation could read
    /// from Unity's Input System, another could read from old Input Manager axes,
    /// and another could provide scripted input for cutscenes or tests.
    /// </remarks>
    public interface IMoveInputSource
    {
        /// <summary>
        /// Gets the current two-axis movement input.
        /// </summary>
        /// <remarks>
        /// By convention, <see cref="Vector2.x"/> represents horizontal movement
        /// such as left/right or strafe input, while <see cref="Vector2.y"/>
        /// represents vertical movement such as forward/back input.
        ///
        /// Implementations may return raw input, normalized input, or processed input
        /// depending on the needs of the controller consuming this interface.
        /// </remarks>
        Vector2 Move { get; }

        /// <summary>
        /// Gets whether the sprint input is currently being held.
        /// </summary>
        /// <remarks>
        /// This should represent a held state rather than a one-frame press.
        /// Character movement systems can use this value to choose between walk,
        /// run, or sprint speeds.
        /// </remarks>
        bool Sprint { get; }

        /// <summary>
        /// Consumes the current jump press so the jump action is handled only once.
        /// </summary>
        /// <remarks>
        /// This method is useful for one-shot actions such as jumping, where a held
        /// button should not repeatedly trigger the action every frame.
        ///
        /// Implementations should usually return <c>true</c> once for a pending jump
        /// press, then clear that pending press so later calls return <c>false</c>
        /// until the player presses jump again.
        /// </remarks>
        /// <returns>
        /// <c>true</c> if a jump press was available and has now been consumed;
        /// otherwise, <c>false</c>.
        /// </returns>
        bool ConsumeJump();
    }
}
