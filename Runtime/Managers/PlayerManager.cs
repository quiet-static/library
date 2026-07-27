using QuietStatic.Toolkit.Core;
using UnityEngine;

namespace QuietStatic
{
    /// <summary>
    /// Provides a stable reference to the active player root for persistent systems.
    /// </summary>
    /// <remarks>
    /// Assign the player in the Inspector. Scene objects should prefer player-facing handlers
    /// and events; this manager is intended for systems that genuinely need the shared root.
    /// </remarks>
    [AddComponentMenu("Quiet Static Toolkit/Managers/Player Manager")]
    public class PlayerManager : ToolkitSingleton<PlayerManager>
    {
        [Tooltip("Root GameObject representing the active player.")]
        [SerializeField] private GameObject player;

        /// <summary>Gets the configured player root, or null when none is assigned.</summary>
        public GameObject Player => player;

        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
            {
                return;
            }
        }
    }
}
