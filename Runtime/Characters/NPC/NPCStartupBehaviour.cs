using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Characters.NPC
{
    /// <summary>
    /// Applies scene-configured startup state to an NPC after its components initialize.
    /// </summary>
    /// <remarks>
    /// Use the startup event to configure animation, behavior modes, targets, visibility,
    /// dialogue, or other scene-specific state without coupling those systems together.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NPCController))]
    public sealed class NPCStartupBehaviour : MonoBehaviour
    {
        [Tooltip("Optional delay before the NPC's startup state is applied.")]
        [Min(0f)]
        [SerializeField] private float delay;

        [Tooltip("Actions that describe this NPC's initial scene state.")]
        [SerializeField] private UnityEvent onStartup;

        private bool hasApplied;

        /// <summary>Gets whether this component has applied its startup state.</summary>
        public bool HasApplied => hasApplied;

        private void Start()
        {
            if (delay > 0f)
            {
                StartCoroutine(ApplyAfterDelay());
                return;
            }

            ApplyStartupState();
        }

        /// <summary>
        /// Invokes the configured startup actions once. This can also be called manually
        /// by scene initialization flows that need deterministic ordering.
        /// </summary>
        public void ApplyStartupState()
        {
            if (hasApplied)
            {
                return;
            }

            hasApplied = true;
            onStartup?.Invoke();
        }

        /// <summary>Allows the startup state to be applied again manually.</summary>
        public void ResetStartupState()
        {
            hasApplied = false;
        }

        private IEnumerator ApplyAfterDelay()
        {
            yield return new WaitForSeconds(delay);
            ApplyStartupState();
        }
    }
}
