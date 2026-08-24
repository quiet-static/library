using System;
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
        [Tooltip("Root GameObject currently registered as the active player.")]
        [SerializeField] private GameObject player;

        /// <summary>
        /// Raised when the active player reference changes.
        /// </summary>
        /// <remarks>
        /// The first argument is the previously registered player and the second
        /// argument is the newly registered player. Either argument can be null.
        /// Reassigning the current player does not raise this event.
        /// </remarks>
        public event Action<GameObject, GameObject> PlayerChanged;

        /// <summary>
        /// Gets the configured active player root, or null when none is assigned.
        /// </summary>
        public GameObject Player => player;

        /// <summary>
        /// Registers the active player root.
        /// </summary>
        /// <param name="newPlayer">
        /// Player root to register, or null to clear the active player.
        /// </param>
        /// <remarks>
        /// Setting the already registered reference is idempotent and does not
        /// notify listeners. Reference identity is used so a destroyed Unity
        /// object can still be replaced with an actual null reference.
        /// </remarks>
        public void SetPlayer(GameObject newPlayer)
        {
            if (ReferenceEquals(player, newPlayer))
            {
                return;
            }

            GameObject previousPlayer = player;
            player = newPlayer;

            PlayerChanged?.Invoke(previousPlayer, player);
        }
    }
}
