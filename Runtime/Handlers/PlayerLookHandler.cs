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

        [Tooltip("Vertical offset used when the player has no humanoid head bone.")]
        [SerializeField, Min(0f)] private float fallbackEyeHeight = 1.6f;

        /// <summary>Starts looking at the player registered with <see cref="PlayerManager"/>.</summary>
        public void LookAtPlayer()
        {
            if (!TryResolvePlayer(out Transform target, out Vector3 offset))
            {
                return;
            }

            lookAt.StartLookingAt(target, offset);
        }

        /// <summary>
        /// Temporarily looks at the player without replacing the NPC's ordinary look target.
        /// Intended for dialogue and cutscenes.
        /// </summary>
        public void LookAtPlayerTemporarily()
        {
            if (!TryResolvePlayer(out Transform target, out Vector3 offset))
            {
                return;
            }

            lookAt.StartTemporaryLookAt(target, offset);
        }

        /// <summary>Ends a dialogue or cutscene look override.</summary>
        public void StopLookingAtPlayerTemporarily()
        {
            lookAt?.StopTemporaryLook();
        }

        /// <summary>Stops the configured NPC look-at behavior.</summary>
        public void StopLookingAtPlayer()
        {
            if (lookAt != null)
            {
                lookAt.StopLooking();
            }
        }

        private bool TryResolvePlayer(out Transform target, out Vector3 offset)
        {
            target = null;
            offset = Vector3.zero;

            if (lookAt == null)
            {
                GameLogger.Warning(nameof(PlayerLookHandler), this,
                    "No NPCLookAtBehaviour is assigned.");
                return false;
            }

            if (PlayerManager.Instance == null || PlayerManager.Instance.Player == null)
            {
                GameLogger.Warning(nameof(PlayerLookHandler), this,
                    "Could not find the active player.");
                return false;
            }

            GameObject player = PlayerManager.Instance.Player;
            Animator playerAnimator = player.GetComponentInChildren<Animator>();
            if (playerAnimator != null && playerAnimator.isHuman)
            {
                Transform head = playerAnimator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    target = head;
                    return true;
                }
            }

            target = player.transform;
            offset = Vector3.up * fallbackEyeHeight;
            return true;
        }
    }
}
