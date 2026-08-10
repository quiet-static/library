using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Spawning
{
    /// <summary>
    /// Compatibility adapter for the former scene-local spawning path.
    /// </summary>
    /// <remarks>
    /// New code should use the persistent <see cref="SpawnManager"/>. When no manager
    /// exists this adapter retains the former local lookup and placement behavior.
    /// </remarks>
    [Obsolete("Use SpawnManager as the authoritative spawning API.")]
    [AddComponentMenu("Quiet Static Toolkit/Compatibility/Spawn Service (Legacy)")]
    public class SpawnService : MonoBehaviour
    {
        /// <summary>Finds a loaded spawn point by stable ID.</summary>
        public SpawnPoint FindSpawnPoint(string id)
        {
            return SpawnManager.Instance != null
                ? SpawnManager.Instance.FindSpawnPoint(id)
                : SpawnPlacementUtility.FindSpawnPoint(id);
        }

        /// <summary>Moves an object through the authoritative spawning path.</summary>
        public void MoveToSpawn(GameObject target, string spawnId)
        {
            if (target == null)
            {
                return;
            }

            if (SpawnManager.Instance != null)
            {
                SpawnManager.Instance.MoveToSpawn(target.transform, spawnId);
                return;
            }

            SpawnPoint spawnPoint =
                SpawnPlacementUtility.FindSpawnPoint(spawnId);
            SpawnPlacementUtility.MoveSafely(
                target.transform,
                spawnPoint != null ? spawnPoint.transform : null);
        }

        /// <summary>Instantiates through the authoritative spawning path.</summary>
        public GameObject Spawn(GameObject prefab, string spawnId)
        {
            if (SpawnManager.Instance != null)
            {
                return SpawnManager.Instance.Spawn(prefab, spawnId);
            }

            return SpawnPlacementUtility.InstantiateAt(
                prefab,
                SpawnPlacementUtility.FindSpawnPoint(spawnId));
        }
    }
}
