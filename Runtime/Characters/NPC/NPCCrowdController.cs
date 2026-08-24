using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>Starts, stops, and staggers a small set of ambient NPC route behaviours.</summary>
    /// <remarks>
    /// Each member remains responsible for its own navigation and animation. This scene-owned
    /// coordinator intentionally contains no spawning, interaction, dialogue, or global-manager
    /// responsibilities.
    /// </remarks>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class NPCCrowdController : MonoBehaviour
    {
        /// <summary>One independently moving member managed by this crowd.</summary>
        [Serializable]
        public sealed class CrowdMember
        {
            [Tooltip("Per-NPC authored route behavior controlled by this crowd.")]
            [SerializeField] private NPCWaypointRouteBehaviour routeBehaviour;

            [Tooltip("Additional delay before this member starts after the crowd's normal initial delay or stagger.")]
            [Min(0f)]
            [SerializeField] private float additionalStartDelay;

            /// <summary>Gets the route behavior controlled for this member.</summary>
            public NPCWaypointRouteBehaviour RouteBehaviour => routeBehaviour;

            /// <summary>Gets the member-specific additional start delay.</summary>
            public float AdditionalStartDelay => Mathf.Max(0f, additionalStartDelay);
        }

        [Header("Members")]
        [Tooltip("Ambient NPC route behaviours started in this authored order.")]
        [SerializeField] private CrowdMember[] members = Array.Empty<CrowdMember>();

        [Header("Startup")]
        [Tooltip("Start the crowd after all NPC behavior and mode components have initialized.")]
        [SerializeField] private bool startOnStart = true;

        [Tooltip("Restart every member from its configured first waypoint when this crowd starts. Disable to resume paused routes.")]
        [SerializeField] private bool restartRoutesWhenStarted = true;

        [Tooltip("Delay before the first valid crowd member begins its route.")]
        [Min(0f)]
        [SerializeField] private float initialDelay;

        [Tooltip("Base delay between successive valid crowd members beginning their routes.")]
        [Min(0f)]
        [SerializeField] private float staggerInterval = 0.25f;

        [Tooltip("Maximum extra random delay added to each member start to reduce synchronized movement.")]
        [Min(0f)]
        [SerializeField] private float maximumStaggerJitter = 0.15f;

        [Header("Lifecycle")]
        [Tooltip("Stop every assigned route if this crowd coordinator is disabled.")]
        [SerializeField] private bool stopMembersOnDisable = true;

        [Header("Events")]
        [Tooltip("Invoked when crowd startup begins, before the initial delay.")]
        [SerializeField] private UnityEvent onCrowdStarted = new();

        [Tooltip("Invoked after every valid member has been started.")]
        [SerializeField] private UnityEvent onAllMembersStarted = new();

        [Tooltip("Invoked when Stop Crowd is called while the crowd is running.")]
        [SerializeField] private UnityEvent onCrowdStopped = new();

        private Coroutine startupRoutine;
        private int nextMemberIndex;
        private int startedMemberCount;
        private bool resumeStartupAfterEnable;
        private bool startupInProgress;

        /// <summary>Gets the authored crowd membership.</summary>
        public IReadOnlyList<CrowdMember> Members => members ?? Array.Empty<CrowdMember>();

        /// <summary>Gets whether the crowd has been started and has not subsequently been stopped.</summary>
        public bool IsRunning { get; private set; }

        private void Start()
        {
            if (startOnStart)
            {
                StartCrowd();
            }
            else
            {
                DeactivateMembers();
            }
        }

        private void OnDisable()
        {
            bool startupWasPending = startupInProgress;
            CancelStartupRoutine();
            if (stopMembersOnDisable)
            {
                StopCrowdInternal(false);
            }
            else if (IsRunning)
            {
                resumeStartupAfterEnable = startupWasPending;
            }
        }

        private void OnEnable()
        {
            if (!resumeStartupAfterEnable || !IsRunning || startupRoutine != null)
            {
                return;
            }

            resumeStartupAfterEnable = false;
            StartStartupRoutine();
        }

        /// <summary>Starts all valid crowd members with the configured delays.</summary>
        public void StartCrowd()
        {
            if (IsRunning)
            {
                return;
            }

            if (!isActiveAndEnabled)
            {
                GameLogger.Warning(
                    $"Cannot start NPC crowd '{name}' while its coordinator is disabled.",
                    this);
                return;
            }

            CancelStartupRoutine();
            DeactivateMembers();
            nextMemberIndex = 0;
            startedMemberCount = 0;
            resumeStartupAfterEnable = false;
            IsRunning = true;
            startupInProgress = true;
            onCrowdStarted?.Invoke();
            if (isActiveAndEnabled && startupInProgress)
            {
                StartStartupRoutine();
            }
        }

        /// <summary>Stops pending starts and pauses every assigned crowd route.</summary>
        public void StopCrowd()
        {
            StopCrowdInternal(true);
        }

        /// <summary>Stops the crowd, resets each route, and begins the configured stagger again.</summary>
        public void RestartCrowd()
        {
            StopCrowdInternal(false);
            StartCrowd();
        }

        private IEnumerator StartMembersInOrder()
        {
            CrowdMember[] configuredMembers = members ?? Array.Empty<CrowdMember>();
            while (nextMemberIndex < configuredMembers.Length)
            {
                if (!IsRunning || !isActiveAndEnabled)
                {
                    startupRoutine = null;
                    startupInProgress = false;
                    yield break;
                }

                int memberIndex = nextMemberIndex;
                CrowdMember member = configuredMembers[memberIndex];
                NPCWaypointRouteBehaviour routeBehaviour = member?.RouteBehaviour;
                if (routeBehaviour == null)
                {
                    nextMemberIndex++;
                    continue;
                }

                float baseDelay = startedMemberCount == 0 ? initialDelay : staggerInterval;
                float jitter = maximumStaggerJitter > 0f
                    ? UnityEngine.Random.Range(0f, maximumStaggerJitter)
                    : 0f;
                float delay = Mathf.Max(0f, baseDelay) + member.AdditionalStartDelay + jitter;
                if (delay > 0f)
                {
                    yield return new WaitForSeconds(delay);
                }

                if (!IsRunning || !isActiveAndEnabled)
                {
                    startupRoutine = null;
                    startupInProgress = false;
                    yield break;
                }

                // Advance before invoking route callbacks so a callback that disables this
                // coordinator resumes at the following member rather than starting this one twice.
                nextMemberIndex = memberIndex + 1;
                startedMemberCount++;

                if (restartRoutesWhenStarted)
                {
                    routeBehaviour.RestartRoute();
                }
                else
                {
                    routeBehaviour.StartRoute();
                }

                if (!IsRunning || !isActiveAndEnabled)
                {
                    startupRoutine = null;
                    startupInProgress = false;
                    resumeStartupAfterEnable = IsRunning;
                    yield break;
                }
            }

            startupRoutine = null;
            startupInProgress = false;
            if (IsRunning)
            {
                onAllMembersStarted?.Invoke();
            }
        }

        private void StopCrowdInternal(bool notify)
        {
            bool wasRunning = IsRunning;
            IsRunning = false;
            resumeStartupAfterEnable = false;
            CancelStartupRoutine();
            DeactivateMembers();

            if (notify && wasRunning)
            {
                onCrowdStopped?.Invoke();
            }
        }

        private void StartStartupRoutine()
        {
            if (!IsRunning || !isActiveAndEnabled)
            {
                return;
            }

            startupInProgress = true;
            Coroutine startedRoutine = StartCoroutine(StartMembersInOrder());
            startupRoutine = startupInProgress ? startedRoutine : null;
        }

        private void DeactivateMembers()
        {
            foreach (CrowdMember member in members ?? Array.Empty<CrowdMember>())
            {
                member?.RouteBehaviour?.StopRoute();
            }
        }

        private void CancelStartupRoutine()
        {
            startupInProgress = false;
            if (startupRoutine == null)
            {
                return;
            }

            StopCoroutine(startupRoutine);
            startupRoutine = null;
        }

        private void OnValidate()
        {
            initialDelay = Mathf.Max(0f, initialDelay);
            staggerInterval = Mathf.Max(0f, staggerInterval);
            maximumStaggerJitter = Mathf.Max(0f, maximumStaggerJitter);
        }
    }
}
