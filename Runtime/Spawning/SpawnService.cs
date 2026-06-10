using System.Linq;
using UnityEngine;

namespace QuietStatic.Toolkit.Spawning
{
    /// <summary>
    /// Provides simple scene-based spawning helpers for moving objects to named
    /// <see cref="SpawnPoint"/> instances or instantiating prefabs at those points.
    /// </summary>
    /// <remarks>
    /// This service searches the currently loaded scene objects whenever a spawn point
    /// lookup is requested. That keeps the component lightweight and easy to reuse, but
    /// for very large scenes or frequent spawning, a cached spawn-point registry may be
    /// more efficient.
    /// </remarks>
    public class SpawnService : MonoBehaviour
    {
        /// <summary>
        /// Finds the first loaded <see cref="SpawnPoint"/> whose <see cref="SpawnPoint.Id"/>
        /// matches the supplied identifier.
        /// </summary>
        /// <param name="id">
        /// The spawn point id to search for.
        /// This should match the id configured on a <see cref="SpawnPoint"/> in the scene.
        /// </param>
        /// <returns>
        /// The first matching spawn point if one exists; otherwise, <c>null</c>.
        /// </returns>
        public SpawnPoint FindSpawnPoint(string id)
        {
            return FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None)
                .FirstOrDefault(spawn => spawn.Id == id);
        }

        /// <summary>
        /// Moves an existing GameObject to the position and rotation of a named spawn point.
        /// </summary>
        /// <param name="target">
        /// The object to move. If this is <c>null</c>, no action is taken.
        /// </param>
        /// <param name="spawnId">
        /// The id of the spawn point to move the target to.
        /// If no matching spawn point exists, no action is taken.
        /// </param>
        public void MoveToSpawn(GameObject target, string spawnId)
        {
            if (target == null)
            {
                return;
            }

            SpawnPoint spawn = FindSpawnPoint(spawnId);

            if (spawn == null)
            {
                return;
            }

            target.transform.SetPositionAndRotation(
                spawn.transform.position,
                spawn.transform.rotation
            );
        }

        /// <summary>
        /// Instantiates a prefab, optionally placing it at a named spawn point.
        /// </summary>
        /// <param name="prefab">
        /// The prefab to instantiate. If this is <c>null</c>, no object is created.
        /// </param>
        /// <param name="spawnId">
        /// The id of the spawn point to use for placement.
        /// If no matching spawn point exists, the prefab is instantiated at Unity's default
        /// position and rotation.
        /// </param>
        /// <returns>
        /// The newly instantiated GameObject, or <c>null</c> if the prefab was missing.
        /// </returns>
        public GameObject Spawn(GameObject prefab, string spawnId)
        {
            if (prefab == null)
            {
                return null;
            }

            SpawnPoint spawn = FindSpawnPoint(spawnId);

            if (spawn == null)
            {
                return Instantiate(prefab);
            }

            return Instantiate(
                prefab,
                spawn.transform.position,
                spawn.transform.rotation
            );
        }
    }
}
