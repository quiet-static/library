using UnityEngine;

namespace QuietStatic.Toolkit.Spawning
{
    /// <summary>
    /// Identifies a transform in the scene as a reusable spawn location.
    /// </summary>
    /// <remarks>
    /// This component is intentionally lightweight. It stores a string identifier that
    /// other systems can use to find a meaningful spawn position and rotation, such as
    /// a player start point, checkpoint, scene entrance, or cutscene placement marker.
    ///
    /// The GameObject's own transform provides the actual position and rotation.
    /// </remarks>
    public class SpawnPoint : MonoBehaviour
    {
        [Header("Spawn Identity")]
        [Tooltip("Unique identifier used by spawning or scene-loading systems to find this spawn point.")]
        [SerializeField] private string id;

        /// <summary>
        /// Gets the identifier assigned to this spawn point.
        /// </summary>
        /// <remarks>
        /// Spawning systems can compare this value against a requested spawn id to decide
        /// which scene transform should be used.
        /// </remarks>
        public string Id => id;
    }
}
