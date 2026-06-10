namespace QuietStatic.Characters
{
    /// <summary>
    /// Defines shared character state types used by character movement, animation,
    /// and gameplay systems.
    /// </summary>
    /// <remarks>
    /// This class is intentionally static because it acts as a lightweight namespace
    /// for reusable state enums. Keeping these values in one place helps different
    /// systems agree on the same movement-state names without each component needing
    /// to define its own duplicate enum.
    /// </remarks>
    public static class EntityState
    {
        /// <summary>
        /// Represents the current high-level movement state of a character.
        /// </summary>
        /// <remarks>
        /// These values are usually set by a movement or state controller and then
        /// read by other systems, such as animation controllers, camera helpers,
        /// sound effects, or gameplay logic.
        /// </remarks>
        public enum MovementState
        {
            /// <summary>
            /// The character is grounded and not moving.
            /// </summary>
            Idle,

            /// <summary>
            /// The character is grounded and currently moving.
            /// </summary>
            Moving,

            /// <summary>
            /// The character has started a jump or is moving upward through the air.
            /// </summary>
            Jumping,

            /// <summary>
            /// The character is airborne and moving downward or otherwise not grounded.
            /// </summary>
            Falling
        }
    }
}
