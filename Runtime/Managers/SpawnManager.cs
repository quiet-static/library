using System.Collections.Generic;
using QuietStatic.Toolkit.Core;
using UnityEngine;
using UnityEngine.AI;

namespace QuietStatic
{
    /// <summary>
    /// Registers scene objects that can be repositioned at named spawn points.
    /// </summary>
    /// <remarks>
    /// This manager is useful for player characters, companions, enemies, cameras,
    /// vehicles, or any other object that needs safe repositioning after a scene load.
    ///
    /// Registered objects can live in additive scenes. They register when enabled
    /// and unregister when their scene unloads.
    /// </remarks>
    public class SpawnManager : ToolkitSingleton<SpawnManager>
    {
        [Header("Fallback")]
        [Tooltip("Optional spawn point ID used when a requested spawn point cannot be found.")]
        [SerializeField] private string fallbackSpawnId = "Default";

        /// <summary>
        /// Runtime registry of movable objects, grouped by a caller-defined ID.
        /// </summary>
        private readonly Dictionary<string, Transform> registeredTargets = new();

        /// <summary>
        /// Registers a target that can later be moved using <see cref="MoveRegisteredTargetToSpawn"/>.
        /// </summary>
        /// <param name="targetId">
        /// Unique identifier for this target, such as "Player", "Nerd", "Jock", or "Companion".
        /// </param>
        /// <param name="target">Transform to move when requested.</param>
        public void RegisterTarget(string targetId, Transform target)
        {
            if (string.IsNullOrWhiteSpace(targetId) || target == null)
            {
                return;
            }

            registeredTargets[targetId.Trim()] = target;
        }

        /// <summary>
        /// Removes a registered target from the runtime registry.
        /// </summary>
        /// <param name="targetId">Identifier used when the target was registered.</param>
        /// <param name="target">
        /// Optional target safety check. If supplied, the entry is removed only when it
        /// matches the currently registered transform.
        /// </param>
        public void UnregisterTarget(string targetId, Transform target = null)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return;
            }

            string normalizedId = targetId.Trim();

            if (!registeredTargets.TryGetValue(normalizedId, out Transform registeredTarget))
            {
                return;
            }

            if (target != null && registeredTarget != target)
            {
                return;
            }

            registeredTargets.Remove(normalizedId);
        }

        /// <summary>
        /// Moves a registered target to a named spawn point.
        /// </summary>
        /// <param name="targetId">Identifier of the previously registered target.</param>
        /// <param name="spawnId">Identifier of the spawn point to use.</param>
        /// <returns>True if the target was moved successfully.</returns>
        public bool MoveRegisteredTargetToSpawn(string targetId, string spawnId)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            if (!registeredTargets.TryGetValue(targetId.Trim(), out Transform target) ||
                target == null)
            {
                return false;
            }

            return MoveToSpawn(target, spawnId);
        }

        /// <summary>
        /// Moves any transform to a named spawn point.
        /// </summary>
        /// <param name="target">Transform to reposition.</param>
        /// <param name="spawnId">Identifier of the spawn point to use.</param>
        /// <returns>True if the target was moved successfully.</returns>
        public bool MoveToSpawn(Transform target, string spawnId)
        {
            if (target == null || string.IsNullOrWhiteSpace(spawnId))
            {
                return false;
            }

            SpawnPoint spawnPoint = FindSpawnPoint(spawnId);

            if (spawnPoint == null &&
                !string.IsNullOrWhiteSpace(fallbackSpawnId) &&
                spawnId.Trim() != fallbackSpawnId.Trim())
            {
                spawnPoint = FindSpawnPoint(fallbackSpawnId);
            }

            if (spawnPoint == null)
            {
                GameLogger.Warning(
                    "MoveToSpawn",
                    this,
                    $"{nameof(SpawnManager)} could not find spawn point '{spawnId}'."
                );
                return false;
            }

            MoveSafely(target, spawnPoint.transform);
            return true;
        }

        /// <summary>
        /// Finds the first loaded spawn point with a matching ID.
        /// </summary>
        public SpawnPoint FindSpawnPoint(string spawnId)
        {
            if (string.IsNullOrWhiteSpace(spawnId))
            {
                return null;
            }

            SpawnPoint[] spawnPoints =
                FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);

            foreach (SpawnPoint spawnPoint in spawnPoints)
            {
                if (spawnPoint != null &&
                    spawnPoint.Id == spawnId.Trim())
                {
                    return spawnPoint;
                }
            }

            return null;
        }

        /// <summary>
        /// Safely moves an object while accounting for CharacterControllers and NavMeshAgents.
        /// </summary>
        private static void MoveSafely(
            Transform target,
            Transform spawnTransform
        )
        {
            CharacterController characterController =
                target.GetComponent<CharacterController>();

            NavMeshAgent navMeshAgent =
                target.GetComponent<NavMeshAgent>();

            if (characterController != null)
            {
                characterController.enabled = false;
            }

            if (navMeshAgent != null &&
                navMeshAgent.enabled &&
                navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.Warp(spawnTransform.position);
            }
            else
            {
                target.position = spawnTransform.position;
            }

            target.rotation = spawnTransform.rotation;

            if (characterController != null)
            {
                characterController.enabled = true;
            }

            if (navMeshAgent != null && navMeshAgent.enabled)
            {
                navMeshAgent.ResetPath();
            }
        }
    }
}
