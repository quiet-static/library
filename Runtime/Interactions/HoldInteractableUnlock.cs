using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>Applies a hold meter's normalized progress continuously to a visual.</summary>
    [AddComponentMenu("Quiet Static Toolkit/Interactions/Hold Interactable Unlock")]
    public class HoldInteractableUnlock : MonoBehaviour
    {
        private enum ProgressVisualMode
        {
            AnimatorFloat,
            Shrink
        }

        [Header("Source")]
        [Tooltip("Hold interaction whose progress drives this visual.")]
        [SerializeField] private HoldInteractable source;

        [Header("Visual Mode")]
        [Tooltip("Drive an Animator float parameter or shrink a Transform.")]
        [SerializeField] private ProgressVisualMode mode = ProgressVisualMode.Shrink;
        [Tooltip("Animator used by Animator Float mode.")]
        [SerializeField] private Animator animator;
        [Tooltip("Float parameter receiving normalized progress in Animator Float mode.")]
        [SerializeField] private string progressParameter = "Progress";
        [Tooltip("Transform scaled down in Shrink mode. Defaults to this transform.")]
        [SerializeField] private Transform shrinkTarget;
        [Tooltip("Disable the shrink target after progress reaches one.")]
        [SerializeField] private bool disableWhenComplete = true;

        private Vector3 initialScale;

        private void Awake()
        {
            if (source == null)
            {
                source = GetComponentInParent<HoldInteractable>();
            }
            if (shrinkTarget == null)
            {
                shrinkTarget = transform;
            }
            initialScale = shrinkTarget.localScale;
        }

        private void OnEnable()
        {
            if (source != null)
            {
                source.ProgressChanged += ApplyProgress;
                ApplyProgress(source.Progress);
            }
        }

        private void OnDisable()
        {
            if (source != null)
            {
                source.ProgressChanged -= ApplyProgress;
            }
        }

        /// <summary>Applies a normalized meter value immediately.</summary>
        public void ApplyProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (mode == ProgressVisualMode.AnimatorFloat)
            {
                if (animator != null && !string.IsNullOrWhiteSpace(progressParameter))
                {
                    animator.SetFloat(progressParameter, progress);
                }
                return;
            }

            if (shrinkTarget == null)
            {
                return;
            }

            shrinkTarget.localScale = Vector3.Lerp(initialScale, Vector3.zero, progress);
            if (disableWhenComplete && progress >= 1f)
            {
                shrinkTarget.gameObject.SetActive(false);
            }
        }
    }
}
