using QuietStatic.Toolkit.Spawning;
using UnityEngine;

namespace QuietStatic
{
    /// <summary>
    /// Trigger- and UnityEvent-facing bridge for authoritative spawn commands.
    /// </summary>
    [AddComponentMenu("Quiet Static Toolkit/Handlers/Spawn Handler")]
    public sealed class SpawnHandler : MonoBehaviour
    {
        [Tooltip("Registered target moved by parameterless commands.")]
        [SerializeField] private string targetId = "Player";

        [Tooltip("Spawn point used by parameterless commands.")]
        [SerializeField] private string spawnId = "Default";

        [Tooltip("Optional prefab instantiated by Spawn Configured Prefab.")]
        [SerializeField] private GameObject prefab;

        [Tooltip("Move the configured target when this scene object starts. Enable this for scenes that support direct Play Mode entry without a scene-flow arrival condition.")]
        [SerializeField] private bool moveOnStart;

        /// <summary>
        /// Applies the configured spawn during direct scene entry when requested.
        /// </summary>
        /// <remarks>
        /// Spawn targets can register later in the same initialization pass, so this
        /// runs at Start rather than Awake. Scene-flow arrivals may safely move the
        /// same target again through <see cref="MoveConfiguredTarget"/>.
        /// </remarks>
        private void Start()
        {
            if (moveOnStart)
            {
                MoveConfiguredTarget();
            }
        }

        /// <summary>Moves the configured target to the configured spawn point.</summary>
        public void MoveConfiguredTarget()
        {
            SpawnManager.Instance?.MoveRegisteredTargetToSpawn(
                targetId,
                spawnId);
        }

        /// <summary>Moves the configured target to a supplied spawn point.</summary>
        public void MoveConfiguredTargetTo(string requestedSpawnId)
        {
            SpawnManager.Instance?.MoveRegisteredTargetToSpawn(
                targetId,
                requestedSpawnId);
        }

        /// <summary>Instantiates the configured prefab at the configured point.</summary>
        public void SpawnConfiguredPrefab()
        {
            SpawnManager.Instance?.Spawn(prefab, spawnId);
        }

        /// <summary>Instantiates a supplied prefab at the configured point.</summary>
        public void SpawnPrefab(GameObject requestedPrefab)
        {
            SpawnManager.Instance?.Spawn(requestedPrefab, spawnId);
        }
    }
}
