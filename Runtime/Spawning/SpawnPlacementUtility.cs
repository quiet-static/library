using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace QuietStatic.Toolkit.Spawning
{
    /// <summary>
    /// Shared low-level spawn-point lookup and placement operations.
    /// </summary>
    internal static class SpawnPlacementUtility
    {
        /// <summary>Finds the active scene spawn point with an exact normalized ID.</summary>
        /// <param name="spawnId">Spawn-point identifier.</param>
        /// <returns>The matching spawn point, or null when none is active.</returns>
        public static SpawnPoint FindSpawnPoint(string spawnId)
        {
            if (string.IsNullOrWhiteSpace(spawnId))
            {
                return null;
            }

            string normalizedId = spawnId.Trim();
            return UnityEngine.Object
                .FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None)
                .FirstOrDefault(
                    spawnPoint =>
                        spawnPoint != null &&
                        string.Equals(
                            spawnPoint.Id,
                            normalizedId,
                            StringComparison.Ordinal));
        }

        /// <summary>
        /// Moves a transform while temporarily accommodating CharacterController and
        /// NavMeshAgent ownership of its position.
        /// </summary>
        /// <param name="target">Transform to move.</param>
        /// <param name="destination">Transform supplying the destination pose.</param>
        public static void MoveSafely(
            Transform target,
            Transform destination)
        {
            if (target == null || destination == null)
            {
                return;
            }

            CharacterController characterController =
                target.GetComponent<CharacterController>();
            NavMeshAgent navMeshAgent =
                target.GetComponent<NavMeshAgent>();
            bool restoreCharacterController =
                characterController != null && characterController.enabled;
            bool canWarpAgent =
                navMeshAgent != null &&
                navMeshAgent.enabled &&
                navMeshAgent.isOnNavMesh;

            if (restoreCharacterController)
            {
                characterController.enabled = false;
            }

            if (canWarpAgent)
            {
                navMeshAgent.Warp(destination.position);
            }
            else
            {
                target.position = destination.position;
            }

            target.rotation = destination.rotation;

            if (restoreCharacterController)
            {
                characterController.enabled = true;
            }

            if (canWarpAgent)
            {
                navMeshAgent.ResetPath();
            }
        }

        /// <summary>Instantiates a prefab at a spawn point, or at its prefab pose when none is supplied.</summary>
        /// <param name="prefab">Prefab to instantiate.</param>
        /// <param name="spawnPoint">Optional spawn point supplying position and rotation.</param>
        /// <returns>The created instance, or null when the prefab is missing.</returns>
        public static GameObject InstantiateAt(
            GameObject prefab,
            SpawnPoint spawnPoint)
        {
            if (prefab == null)
            {
                return null;
            }

            return spawnPoint == null
                ? UnityEngine.Object.Instantiate(prefab)
                : UnityEngine.Object.Instantiate(
                    prefab,
                    spawnPoint.transform.position,
                    spawnPoint.transform.rotation);
        }
    }
}
