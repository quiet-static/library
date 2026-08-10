using TMPro;
using UnityEngine;

namespace QuietStatic.Toolkit.Objectives
{
    /// <summary>
    /// Presents the active objective without owning objective lifecycle state.
    /// </summary>
    [AddComponentMenu("Quiet Static Toolkit/Objectives/Objective Presenter")]
    public sealed class ObjectivePresenter : MonoBehaviour
    {
        [Tooltip("Optional child object shown only while an objective is active.")]
        [SerializeField] private GameObject contentRoot;

        [Tooltip("Optional label that displays the objective title.")]
        [SerializeField] private TMP_Text titleLabel;

        [Tooltip("Optional label that displays the objective description.")]
        [SerializeField] private TMP_Text descriptionLabel;

        [Tooltip("Hides Content Root when no objective is active.")]
        [SerializeField] private bool hideWhenNoObjective = true;

        private void Reset()
        {
            TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
            titleLabel = labels.Length > 0 ? labels[0] : null;
            descriptionLabel = labels.Length > 1 ? labels[1] : null;
        }

        private void OnEnable()
        {
            ObjectiveManager.OnObjectiveLifecycleChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            ObjectiveManager.OnObjectiveLifecycleChanged -= Refresh;
        }

        /// <summary>Refreshes labels from the authoritative objective manager.</summary>
        public void Refresh()
        {
            ObjectiveDefinition objective =
                ObjectiveManager.Instance != null
                    ? ObjectiveManager.Instance.ActiveObjective
                    : null;

            if (titleLabel != null)
            {
                titleLabel.text = objective?.Title ?? string.Empty;
            }

            if (descriptionLabel != null)
            {
                descriptionLabel.text = objective?.Description ?? string.Empty;
            }

            if (hideWhenNoObjective &&
                contentRoot != null &&
                contentRoot != gameObject)
            {
                contentRoot.SetActive(objective != null);
            }
        }
    }
}
