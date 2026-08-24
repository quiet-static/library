using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.Objectives
{
    /// <summary>
    /// Presents the active objective without owning objective lifecycle state.
    /// </summary>
    [AddComponentMenu("Quiet Static Toolkit/Objectives/Objective Presenter")]
    public sealed class ObjectivePresenter : MonoBehaviour
    {
        [Serializable]
        public sealed class StringUnityEvent : UnityEvent<string>
        {
        }

        [Tooltip("Optional child object shown only while an objective is active.")]
        [SerializeField] private GameObject contentRoot;

        [Tooltip("Optional label that displays the objective title.")]
        [SerializeField] private TMP_Text titleLabel;

        [Tooltip("Optional label that displays the objective description.")]
        [SerializeField] private TMP_Text descriptionLabel;

        [Tooltip("Optional single label that displays the objective description, falling back to its title.")]
        [SerializeField] private TMP_Text displayLabel;

        [Tooltip("Text shown in Display Label when no objective is active.")]
        [TextArea(2, 5)]
        [SerializeField] private string fallbackText;

        [Tooltip("Hides Content Root when no objective is active.")]
        [SerializeField] private bool hideWhenNoObjective = true;

        [Tooltip("Invoked when the text displayed by this presenter changes.")]
        [SerializeField] private StringUnityEvent onObjectiveChanged;

        private string currentDisplayText;

        private void Reset()
        {
            TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
            titleLabel = labels.Length > 0 ? labels[0] : null;
            descriptionLabel = labels.Length > 1 ? labels[1] : null;
            displayLabel = labels.Length == 1 ? labels[0] : null;
        }

        private ObjectiveManager observedManager;

        private void OnEnable()
        {
            ObserveActiveManager();
            Refresh();
        }

        private void Start()
        {
            ObserveActiveManager();
            Refresh();
        }

        private void OnDisable()
        {
            if (observedManager != null)
            {
                observedManager.ObjectiveLifecycleChanged -= Refresh;
                observedManager = null;
            }
        }

        private void ObserveActiveManager()
        {
            ObjectiveManager activeManager = ObjectiveManager.Instance;
            if (observedManager == activeManager)
            {
                return;
            }

            if (observedManager != null)
            {
                observedManager.ObjectiveLifecycleChanged -= Refresh;
            }

            observedManager = activeManager;
            if (observedManager != null)
            {
                observedManager.ObjectiveLifecycleChanged += Refresh;
            }
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

            string displayText = objective != null
                ? objective.DisplayText
                : fallbackText ?? string.Empty;

            if (displayLabel != null)
            {
                displayLabel.text = displayText;
            }

            if (hideWhenNoObjective &&
                contentRoot != null &&
                contentRoot != gameObject)
            {
                contentRoot.SetActive(objective != null);
            }

            if (currentDisplayText == displayText)
            {
                return;
            }

            currentDisplayText = displayText;
            onObjectiveChanged?.Invoke(displayText);
        }
    }
}
