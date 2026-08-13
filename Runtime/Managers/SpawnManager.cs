using System.Collections.Generic;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.Spawning;
using UnityEngine;

namespace QuietStatic
{
    /// <summary>
    /// Resolves spawn points, moves registered targets, and instantiates prefabs.
    /// </summary>
    /// <remarks>
    /// This manager is useful for player characters, companions, enemies, cameras,
    /// vehicles, or any other object that needs safe repositioning after a scene load.
    ///
    /// Registered objects can live in additive scenes. They register when enabled
    /// and unregister when their scene unloads.
    /// </remarks>
    [AddComponentMenu("Quiet Static Toolkit/Managers/Spawn Manager")]
    public class SpawnManager : ToolkitSingleton<SpawnManager>
    {
        [Header("Fallback")]
        [Tooltip("Optional spawn point ID used when a requested spawn point cannot be found.")]
        [SerializeField] private string fallbackSpawnId = "Default";

        /// <summary>
        /// Runtime registry of movable objects, grouped by a caller-defined ID.
        /// </summary>
        private readonly Dictionary<string, Transform> registeredTargets = new();

        /// <summary>Gets the number of live registered targets.</summary>
        public int RegisteredTargetCount
        {
            get
            {
                RemoveDestroyedTargets();
                return registeredTargets.Count;
            }
        }

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
            if (!TryGetRegisteredTarget(targetId, out Transform target))
            {
                return false;
            }

            return MoveToSpawn(target, spawnId);
        }

        /// <summary>
        /// Resolves a registered target, discovering a matching enabled
        /// <see cref="SpawnTarget"/> when necessary.
        /// </summary>
        public bool TryGetRegisteredTarget(
            string targetId,
            out Transform target)
        {
            target = null;

            if (string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            string normalizedId = targetId.Trim();

            if (registeredTargets.TryGetValue(normalizedId, out target) &&
                target != null)
            {
                return true;
            }

            registeredTargets.Remove(normalizedId);

            SpawnTarget[] sceneTargets =
                FindObjectsByType<SpawnTarget>(FindObjectsSortMode.None);

            foreach (SpawnTarget sceneTarget in sceneTargets)
            {
                if (sceneTarget == null ||
                    sceneTarget.TargetId != normalizedId ||
                    sceneTarget.Target == null)
                {
                    continue;
                }

                target = sceneTarget.Target;
                registeredTargets[normalizedId] = target;
                return true;
            }

            return false;
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

            SpawnPoint spawnPoint = ResolveSpawnPoint(spawnId);

            if (spawnPoint == null)
            {
                GameLogger.Warning(
                    "MoveToSpawn",
                    this,
                    $"{nameof(SpawnManager)} could not find spawn point '{spawnId}'."
                );
                return false;
            }

            SpawnPlacementUtility.MoveSafely(target, spawnPoint.transform);
            return true;
        }

        /// <summary>
        /// Instantiates a prefab at a named spawn point, using the configured fallback
        /// point when the requested point is unavailable.
        /// </summary>
        /// <remarks>
        /// If neither point exists, the prefab is still instantiated at its authored
        /// position and rotation so an optional spawn point cannot prevent spawning.
        /// </remarks>
        public GameObject Spawn(GameObject prefab, string spawnId)
        {
            if (prefab == null)
            {
                return null;
            }

            SpawnPoint spawnPoint = string.IsNullOrWhiteSpace(spawnId)
                ? null
                : ResolveSpawnPoint(spawnId);

            if (spawnPoint == null && !string.IsNullOrWhiteSpace(spawnId))
            {
                GameLogger.Warning(
                    "Spawn",
                    this,
                    $"{nameof(SpawnManager)} could not find spawn point '{spawnId}'. " +
                    "The prefab will use its authored transform.");
            }

            return SpawnPlacementUtility.InstantiateAt(prefab, spawnPoint);
        }

        /// <summary>
        /// Finds the first loaded spawn point with a matching ID.
        /// </summary>
        public SpawnPoint FindSpawnPoint(string spawnId)
        {
            return SpawnPlacementUtility.FindSpawnPoint(spawnId);
        }

        private SpawnPoint ResolveSpawnPoint(string spawnId)
        {
            SpawnPoint spawnPoint = FindSpawnPoint(spawnId);

            if (spawnPoint == null &&
                !string.IsNullOrWhiteSpace(fallbackSpawnId) &&
                !string.Equals(
                    spawnId?.Trim(),
                    fallbackSpawnId.Trim(),
                    System.StringComparison.Ordinal))
            {
                spawnPoint = FindSpawnPoint(fallbackSpawnId);
            }

            return spawnPoint;
        }

        private void RemoveDestroyedTargets()
        {
            var destroyedIds = new List<string>();

            foreach (KeyValuePair<string, Transform> entry in registeredTargets)
            {
                if (entry.Value == null)
                {
                    destroyedIds.Add(entry.Key);
                }
            }

            foreach (string destroyedId in destroyedIds)
            {
                registeredTargets.Remove(destroyedId);
            }
        }
    }
}
