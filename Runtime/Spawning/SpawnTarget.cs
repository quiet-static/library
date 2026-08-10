using UnityEngine;

namespace QuietStatic.Toolkit.Spawning
{
    /// <summary>
    /// Registers a scene transform with the persistent <see cref="SpawnManager"/>.
    /// </summary>
    /// <remarks>
    /// Add this component to players or other objects that must be repositioned by
    /// scene transitions, checkpoints, or save restoration.
    /// </remarks>
    [DefaultExecutionOrder(100)]
    [AddComponentMenu("Quiet Static Toolkit/Spawning/Spawn Target")]
    public sealed class SpawnTarget : MonoBehaviour
    {
        [Tooltip("Stable target ID used by spawn requests, such as Player or Companion.")]
        [SerializeField] private string targetId = "Player";

        [Tooltip("Transform moved by SpawnManager. Uses this component's transform when unassigned.")]
        [SerializeField] private Transform target;

        /// <summary>Gets the normalized registration ID.</summary>
        public string TargetId =>
            string.IsNullOrWhiteSpace(targetId) ? string.Empty : targetId.Trim();

        /// <summary>Gets the transform registered for movement.</summary>
        public Transform Target => target != null ? target : transform;

        private void Reset()
        {
            target = transform;
        }

        private void OnEnable()
        {
            Register();
        }

        private void Start()
        {
            // A second attempt covers managers initialized later in the same scene.
            Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        /// <summary>Registers this target with the active spawn manager.</summary>
        public void Register()
        {
            SpawnManager.Instance?.RegisterTarget(TargetId, Target);
        }

        /// <summary>Removes this target's matching registration.</summary>
        public void Unregister()
        {
            SpawnManager.Instance?.UnregisterTarget(TargetId, Target);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            targetId = targetId?.Trim();
        }
#endif
    }
}
