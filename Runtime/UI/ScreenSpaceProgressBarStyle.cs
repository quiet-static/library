using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.UI
{
    /// <summary>
    /// Applies a reusable progress-bar theme to a conventional screen-space Slider.
    /// The Slider continues to own its value while this component owns presentation.
    /// </summary>
    [AddComponentMenu("Quiet Static Toolkit/UI/Screen Space Progress Bar Style")]
    public sealed class ScreenSpaceProgressBarStyle : MonoBehaviour
    {
        [Tooltip("Slider driven by the interaction UI manager.")]
        [SerializeField] private Slider slider;

        [Tooltip("Image drawn behind the fill.")]
        [SerializeField] private Image backgroundImage;

        [Tooltip("Image whose RectTransform is driven by the Slider.")]
        [SerializeField] private Image fillImage;

        [Tooltip("Optional progress label.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Shared visual theme. Assign the same asset used by the world-space bar.")]
        [SerializeField] private ProgressBarTheme theme;

        [Tooltip("Optional Slider handle object hidden for a read-only progress meter.")]
        [SerializeField] private GameObject handleRoot;

        private void Awake() => ApplyStyle();

#if UNITY_EDITOR
        private void OnValidate() => ApplyStyle();
#endif

        /// <summary>Reapplies the configured visual theme.</summary>
        public void ApplyStyle()
        {
            theme?.Apply(backgroundImage, fillImage, label);
            ConfigureImage(backgroundImage);
            ConfigureImage(fillImage);

            if (slider != null)
            {
                slider.transition = Selectable.Transition.None;
                slider.targetGraphic = null;
                slider.handleRect = null;
            }

            if (handleRoot != null)
            {
                handleRoot.SetActive(false);
            }
        }

        private static void ConfigureImage(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = null;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
        }
    }
}
