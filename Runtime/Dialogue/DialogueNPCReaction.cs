using QuietStatic.Toolkit.Characters.NPC;
using UnityEngine;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>Applies scene-authored NPC presentation for sessions started by this dialogue player.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DialogueEventPlayer))]
    [AddComponentMenu("Quiet Static Toolkit/Dialogue/Dialogue NPC Reaction")]
    public sealed class DialogueNPCReaction : MonoBehaviour
    {
        [Tooltip("NPC with the look, animation, or mode components used by this reaction.")]
        [SerializeField] private NPCController npc;

        [Tooltip("Temporarily looks toward the initiating player or the registered player when no actor is supplied.")]
        [SerializeField] private bool lookAtPlayer;

        [Tooltip("Optional Animator trigger fired through the NPC's NPC Animation Trigger when dialogue starts.")]
        [SerializeField] private string animationOnStart;

        [Tooltip("Optional Animator trigger fired when the dialogue ends or is stopped.")]
        [SerializeField] private string animationOnEnd;

        [Tooltip("Optional named mode to apply through the NPC's NPC Mode Controller when dialogue starts.")]
        [SerializeField] private string modeOnStart;

        [Tooltip("Optional named mode to apply when dialogue ends. When blank, restores the previous mode if the start mode is still active.")]
        [SerializeField] private string modeOnEnd;

        private DialogueEventPlayer dialoguePlayer;
        private DialogueSession activeSession;
        private NPCLookAtBehaviour activeLook;
        private NPCAnimationTrigger activeAnimation;
        private NPCModeController activeMode;
        private int previousMode = -1;
        private int appliedMode = -1;

        private void OnEnable()
        {
            dialoguePlayer = GetComponent<DialogueEventPlayer>();
            dialoguePlayer.SessionStarted += HandleStart;
            dialoguePlayer.SessionEnded += HandleEnd;
        }

        private void OnDisable()
        {
            if (dialoguePlayer != null)
            {
                dialoguePlayer.SessionStarted -= HandleStart;
                dialoguePlayer.SessionEnded -= HandleEnd;
            }

            if (activeSession != null) FinishReaction(false);
        }

        private void HandleStart(DialogueSession session, GameObject actor)
        {
            if (!isActiveAndEnabled || npc == null) return;

            activeSession = session;
            activeAnimation = npc.GetComponent<NPCAnimationTrigger>();
            activeMode = npc.GetComponent<NPCModeController>();
            ApplyStartMode();
            if (activeSession != session) return;

            ApplyPlayerLook(session, actor);
            if (activeSession == session && activeAnimation != null)
                activeAnimation.SetTrigger(animationOnStart);
        }

        private void ApplyStartMode()
        {
            if (activeMode == null || string.IsNullOrWhiteSpace(modeOnStart)) return;

            string modeName = modeOnStart.Trim();
            int index = activeMode.FindModeIndex(modeName);
            if (index < 0)
            {
                activeMode.SetModeByName(modeName);
                return;
            }

            previousMode = activeMode.CurrentModeIndex;
            appliedMode = index;
            activeMode.SetMode(index);
        }

        private void ApplyPlayerLook(DialogueSession session, GameObject actor)
        {
            if (!lookAtPlayer) return;

            PlayerManager manager = PlayerManager.Instance;
            GameObject player = actor != null ? actor : manager != null ? manager.Player : null;
            if (player == null) return;

            activeLook = npc.GetComponent<NPCLookAtBehaviour>();
            if (activeLook == null) return;

            PlayerLookHandler.ResolvePlayerAim(player, PlayerLookHandler.DefaultEyeHeight, out Transform target, out Vector3 offset);
            activeLook.StartTemporaryLookAt(target, offset, session);
        }

        private void HandleEnd(DialogueSession session)
        {
            if (activeSession == session) FinishReaction(true);
        }

        private void FinishReaction(bool ended)
        {
            DialogueSession session = activeSession;
            NPCLookAtBehaviour look = activeLook;
            NPCAnimationTrigger animation = activeAnimation;
            NPCModeController mode = activeMode;
            int restoreMode = previousMode;
            int startMode = appliedMode;

            activeSession = null;
            activeLook = null;
            activeAnimation = null;
            activeMode = null;
            previousMode = -1;
            appliedMode = -1;

            if (look != null) look.StopTemporaryLook(session);
            if (mode != null)
            {
                if (ended && !string.IsNullOrWhiteSpace(modeOnEnd))
                    mode.SetModeByName(modeOnEnd.Trim());
                else if (restoreMode >= 0 && startMode != restoreMode && mode.CurrentModeIndex == startMode)
                    mode.SetMode(restoreMode);
            }

            if (ended && activeSession == null && animation != null)
                animation.SetTrigger(animationOnEnd);
        }
    }
}
