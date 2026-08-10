using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.UI
{
    /// <summary>
    /// Reusable view for a world-space progress bar. The view owns presentation
    /// only; an interaction or other gameplay component supplies its progress.
    /// </summary>
    [AddComponentMenu("Quiet Static Toolkit/UI/World Space Progress Bar")]
    public class WorldSpaceProgressBar : MonoBehaviour
    {
        [Tooltip("RectTransform whose right anchor is moved as progress changes.")]
        [SerializeField] private RectTransform fillRect;

        [Tooltip("Optional label shown above or beside the meter.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Optional shared theme used by both world-space and screen-space meters.")]
        [SerializeField] private ProgressBarTheme theme;

        [Tooltip("Image drawn behind the progress fill.")]
        [SerializeField] private Image backgroundImage;

        [Tooltip("Image used for the normalized progress fill.")]
        [SerializeField] private Image fillImage;

        [Tooltip("Rotate the meter toward the main camera while it is visible.")]
        [SerializeField] private bool faceMainCamera = true;

        private void Awake() => ApplyTheme();

#if UNITY_EDITOR
        private void OnValidate() => ApplyTheme();
#endif

        /// <summary>Sets the optional label and normalized fill amount.</summary>
        public void Configure(string displayName, float normalizedProgress)
        {
            if (label != null)
            {
                label.text = displayName ?? string.Empty;
            }

            SetProgress(normalizedProgress);
        }

        /// <summary>Sets the visible fill from zero to one.</summary>
        public void SetProgress(float normalizedProgress)
        {
            if (fillRect == null)
            {
                return;
            }

            Vector2 anchorMax = fillRect.anchorMax;
            anchorMax.x = Mathf.Clamp01(normalizedProgress);
            fillRect.anchorMax = anchorMax;
        }

        /// <summary>Shows or hides the entire prefab instance.</summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void ApplyTheme() =>
            theme?.Apply(backgroundImage, fillImage, label);

        private void LateUpdate()
        {
            if (!faceMainCamera || Camera.main == null)
            {
                return;
            }

            Vector3 towardCamera = Camera.main.transform.position -
                transform.position;
            if (towardCamera.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(
                    towardCamera,
                    Vector3.up);
            }
        }
    }
}
