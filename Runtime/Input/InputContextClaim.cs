using UnityEngine;

namespace QuietStatic.Toolkit.Input
{
    /// <summary>
    /// UnityEvent-facing owner of a composable input block claim.
    /// </summary>
    [AddComponentMenu("Quiet Static Toolkit/Input/Input Context Claim")]
    public sealed class InputContextClaim : MonoBehaviour
    {
        [Tooltip("Input groups suppressed while this claim is held.")]
        [SerializeField] private InputBlockGroups blockedGroups =
            InputBlockGroups.Gameplay;

        [Tooltip("Acquire the claim whenever this component enables.")]
        [SerializeField] private bool acquireOnEnable;

        private InputBlockHandle handle;

        /// <summary>Whether this component currently owns a claim.</summary>
        public bool IsClaimed => handle != null && handle.IsActive;

        private void OnEnable()
        {
            if (acquireOnEnable)
            {
                Acquire();
            }
        }

        private void OnDisable()
        {
            Release();
        }

        /// <summary>Acquires this component's configured block. Suitable for UnityEvents.</summary>
        public void Acquire()
        {
            if (IsClaimed || InputModeManager.Instance == null)
            {
                return;
            }

            handle = InputModeManager.Instance.AcquireInputBlock(
                blockedGroups,
                name);
        }

        /// <summary>Releases this component's block. Suitable for UnityEvents.</summary>
        public void Release()
        {
            handle?.Dispose();
            handle = null;
        }
    }
}
