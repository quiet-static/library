using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.NightwatchTheatre.Deductions
{
    /// <summary>Presents a deduction result without owning evaluation or ending flow.</summary>
    [AddComponentMenu("Quiet Static/Nightwatch Theatre/Deductions/Result Presenter")]
    public sealed class DeductionResultPresenter : MonoBehaviour
    {
        [Tooltip("Optional root shown when a result is presented.")]
        [SerializeField] private GameObject contentRoot;

        [Tooltip("Label displaying the result title.")]
        [SerializeField] private TMP_Text titleLabel;

        [Tooltip("Label displaying the result flavor text.")]
        [SerializeField] private TMP_Text flavorTextLabel;

        [Tooltip("Optional label displaying the reasoning hint.")]
        [SerializeField] private TMP_Text reasoningHintLabel;

        [Tooltip("Invoked after the labels and root are updated.")]
        [SerializeField] private UnityEvent onPresented;

        private void Reset()
        {
            TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
            titleLabel = labels.Length > 0 ? labels[0] : null;
            flavorTextLabel = labels.Length > 1 ? labels[1] : null;
            reasoningHintLabel = labels.Length > 2 ? labels[2] : null;
        }

        /// <summary>Updates the configured UI from a selected result.</summary>
        public void Present(DeductionResultDefinition result)
        {
            if (titleLabel != null) titleLabel.text = result?.Title ?? string.Empty;
            if (flavorTextLabel != null) flavorTextLabel.text = result?.FlavorText ?? string.Empty;
            if (reasoningHintLabel != null) reasoningHintLabel.text = result?.ReasoningHint ?? string.Empty;
            if (contentRoot != null && contentRoot != gameObject) contentRoot.SetActive(result != null);
            if (result != null) onPresented?.Invoke();
        }

        /// <summary>Hides the optional result root.</summary>
        public void Hide()
        {
            if (contentRoot != null && contentRoot != gameObject) contentRoot.SetActive(false);
        }
    }
}
