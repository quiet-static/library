using QuietStatic.Toolkit.Characters.NPC;
using UnityEngine;

namespace QuietStatic
{
    /// <summary>
    /// Scene-local bridge for making an NPC look toward the persistent player.
    /// </summary>
    public class PlayerLookHandler : MonoBehaviour
    {
        [Tooltip("NPC look-at behavior that should target the active player.")]
        [SerializeField] private NPCLookAtBehaviour lookAt;

        /// <summary>Starts looking at the player registered with <see cref="PlayerManager"/>.</summary>
        public void LookAtPlayer()
        {
            if (lookAt == null)
            {
                Debug.LogWarning("PlayerLookHandler has no NPCLookAtBehaviour assigned.", this);
                return;
            }

            if (PlayerManager.Instance == null || PlayerManager.Instance.Player == null)
            {
                Debug.LogWarning("PlayerLookHandler could not find the active player.", this);
                return;
            }

            lookAt.StartLookingAt(PlayerManager.Instance.Player.transform);
        }

        /// <summary>Stops the configured NPC look-at behavior.</summary>
        public void StopLookingAtPlayer()
        {
            if (lookAt != null)
            {
                lookAt.StopLooking();
            }
        }
    }
}
