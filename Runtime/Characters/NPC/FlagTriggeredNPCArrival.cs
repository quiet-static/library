using System.Collections;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Flags;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>
    /// Transports an NPC to a scene anchor and optionally starts its dialogue when a
    /// gameplay flag becomes active.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlagTriggeredNPCArrival : MonoBehaviour
    {
        [Header("Trigger")]
        [Tooltip("Flag that causes this NPC to arrive.")]
        [FlagId]
        [SerializeField] private string requiredFlag;

        [Tooltip("When enabled, this arrival can occur only once while this component exists.")]
        [SerializeField] private bool triggerOnce = true;

        [Header("Arrival")]
        [Tooltip("NPC transported to this arrival object's position and rotation.")]
        [SerializeField] private NPCController npc;

        [Tooltip("NavMesh motor used to safely transport the NPC.")]
        [SerializeField] private NPCNavMeshMotor motor;

        [Tooltip("Radius used to project the arrival anchor onto the NavMesh.")]
        [Min(0f)]
        [SerializeField] private float navMeshSampleRadius = 2f;

        [Tooltip("When enabled, the NPC turns horizontally toward its current target or the active player after arriving.")]
        [SerializeField] private bool facePlayerOnArrival;

        [Header("Dialogue")]
        [Tooltip("Optional independent dialogue player started after arrival. This commonly lives on the destination anchor.")]
        [SerializeField] private DialogueEventPlayer dialoguePlayer;

        [Tooltip("Delay after arrival before dialogue begins. Zero waits one frame so transforms settle.")]
        [Min(0f)]
        [SerializeField] private float dialogueDelay;

        [Header("Events")]
        [Tooltip("Invoked after the NPC is transported and before dialogue begins.")]
        [SerializeField] private UnityEvent onArrived;

        private bool hasTriggered;
        private Coroutine dialogueRoutine;
        /// <summary>Gets whether this component has successfully transported its NPC.</summary>
        public bool HasTriggered => hasTriggered;

        private void Awake()
        {
            if (motor == null && npc != null)
            {
                motor = npc.GetComponent<NPCNavMeshMotor>();
            }
        }

        private FlagManager observedFlags;

        private void OnEnable()
        {
            observedFlags = FlagManager.Instance;
            if (observedFlags != null)
            {
                observedFlags.FlagSet += HandleFlagSet;
            }

            if (FlagManager.Instance != null && FlagManager.Instance.HasFlag(requiredFlag))
            {
                TryArrive();
            }
        }

        private void OnDisable()
        {
            if (observedFlags != null)
            {
                observedFlags.FlagSet -= HandleFlagSet;
                observedFlags = null;
            }

            if (dialogueRoutine != null)
            {
                StopCoroutine(dialogueRoutine);
                dialogueRoutine = null;
            }
        }

        private void Reset()
        {
            npc = null;
            motor = null;
        }

        /// <summary>Attempts the configured arrival immediately.</summary>
        /// <returns>True when the NPC was transported.</returns>
        public bool TryArrive()
        {
            if ((triggerOnce && hasTriggered) || npc == null || motor == null)
            {
                return false;
            }

            if (!motor.Warp(transform.position, transform.rotation, navMeshSampleRadius))
            {
                GameLogger.Warning(nameof(FlagTriggeredNPCArrival), this,
                    $"Could not transport {npc.DisplayName} to arrival '{name}' on the NavMesh.");
                return false;
            }

            hasTriggered = true;
            FacePlayerIfConfigured();
            onArrived?.Invoke();

            if (dialoguePlayer != null)
            {
                dialogueRoutine = StartCoroutine(BeginDialogueAfterArrival());
            }

            return true;
        }

        /// <summary>Allows a one-shot arrival to be triggered again.</summary>
        public void ResetArrival()
        {
            hasTriggered = false;
        }

        private void HandleFlagSet(string flagId)
        {
            if (!string.IsNullOrWhiteSpace(requiredFlag) && flagId == requiredFlag.Trim())
            {
                TryArrive();
            }
        }

        private IEnumerator BeginDialogueAfterArrival()
        {
            if (dialogueDelay > 0f)
            {
                yield return new WaitForSeconds(dialogueDelay);
            }
            else
            {
                yield return null;
            }

            dialogueRoutine = null;
            dialoguePlayer.StartDialogue();
        }

        private void FacePlayerIfConfigured()
        {
            if (!facePlayerOnArrival)
            {
                return;
            }

            Transform target = npc != null ? npc.Target : null;
            if (target == null && PlayerManager.Instance != null && PlayerManager.Instance.Player != null)
            {
                target = PlayerManager.Instance.Player.transform;
            }

            if (target == null)
            {
                GameLogger.Warning(nameof(FlagTriggeredNPCArrival), this,
                    $"Could not rotate {name} after arrival because no player target is available.");
                return;
            }

            Vector3 direction = target.position - npc.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            npc.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

    }
}
