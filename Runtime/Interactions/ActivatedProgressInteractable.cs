using System;
using System.Collections;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>
    /// Starts a timed world-space interaction on a single button press and completes
    /// independently after its attached progress meter fills.
    /// </summary>
    [AddComponentMenu("Quiet Static Toolkit/Interactions/Activated Progress Interactable")]
    public class ActivatedProgressInteractable : MonoBehaviour
    {
        [Serializable] public sealed class FloatUnityEvent : UnityEvent<float> { }

        /// <summary>Raised after the timed process starts.</summary>
        public event Action Started;

        /// <summary>Raised whenever normalized progress changes.</summary>
        public event Action<float> ProgressChanged;

        /// <summary>Raised when progress reaches one.</summary>
        public event Action Completed;

        [Header("Interaction Display")]
        [Tooltip("Prompt shown while the player looks at an available interaction.")]
        [SerializeField] private string hoverPrompt = "Press E to start";

        [Header("Timing")]
        [Tooltip("Real-time seconds required for the world-space meter to fill.")]
        [Min(0.01f)]
        [SerializeField] private float duration = 5f;

        [Tooltip("Continue filling while gameplay Time.timeScale is zero.")]
        [SerializeField] private bool useUnscaledTime;

        [Header("World-Space Progress Bar")]
        [Tooltip("Reusable world-space progress bar prefab instantiated for this object.")]
        [SerializeField] private WorldSpaceProgressBar progressBarPrefab;

        [Tooltip("Optional existing progress bar child. When assigned, no prefab is instantiated.")]
        [SerializeField] private WorldSpaceProgressBar progressBarInstance;

        [Tooltip("Parent used for an instantiated progress bar. Defaults to this object.")]
        [SerializeField] private Transform progressBarAnchor;

        [Header("Legacy World-Space UI")]
        [Tooltip("Child of a World Space Canvas containing this interaction's progress UI.")]
        [SerializeField] private GameObject progressRoot;

        [Tooltip("Slider on the World Space Canvas. It is driven from zero to one.")]
        [SerializeField] private Slider progressSlider;

        [Tooltip("Optional text displayed alongside the world-space progress bar.")]
        [SerializeField] private TMP_Text progressLabel;

        [Tooltip("Text written to the optional progress label when the process starts.")]
        [SerializeField] private string progressName = "Progress";

        [Tooltip("Hide the world-space progress bar after completion.")]
        [SerializeField] private bool hideProgressWhenComplete = true;

        [Tooltip("Seconds to leave the full bar visible before hiding it.")]
        [Min(0f)]
        [SerializeField] private float completionDisplayTime = 0.25f;

        [Header("Requirements")]
        [Tooltip("Optional flags required before this process can be started.")]
        [SerializeField] private FlagRequirement requirement;

        [Header("Completion")]
        [Tooltip("Prevent this interaction from being started again after success.")]
        [SerializeField] private bool disableAfterCompletion = true;

        [Tooltip("Flags set after the progress bar finishes.")]
        [FlagId]
        [SerializeField] private string[] flagsToSetOnCompletion;

        [Tooltip("Invoked when the timed process starts.")]
        [SerializeField] private UnityEvent onStarted;

        [Tooltip("Invoked as the meter fills, with a normalized value from zero to one.")]
        [SerializeField] private FloatUnityEvent onProgressChanged;

        [Tooltip("Invoked after completion flags have been set.")]
        [SerializeField] private UnityEvent onCompleted;

        [Tooltip("Invoked when the player attempts to start without meeting requirements.")]
        [SerializeField] private UnityEvent onRequirementFailed;

        private Coroutine hideProgressRoutine;
        private WorldSpaceProgressBar runtimeProgressBar;

        /// <summary>Gets the prompt displayed while this interaction is available.</summary>
        public string HoverPrompt => hoverPrompt;

        /// <summary>Gets normalized completion progress.</summary>
        public float Progress { get; private set; }

        /// <summary>Gets whether the timed process is currently advancing.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Gets whether the process has reached full progress.</summary>
        public bool IsComplete => Progress >= 1f;

        /// <summary>Gets whether this interaction is enabled at runtime.</summary>
        public bool IsEnabled { get; private set; } = true;

        /// <summary>Gets whether the process can currently be started.</summary>
        public bool IsAvailable => IsEnabled && !IsRunning && !IsComplete;

        private void Awake()
        {
            ResolveProgressBar();
            ConfigureProgressDisplay();
            ShowProgress(false);
        }

        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            float deltaTime = useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
            SetProgress(Progress + deltaTime / duration);
        }

        /// <summary>Attempts to start the timed process.</summary>
        public bool TryActivate()
        {
            if (!IsAvailable)
            {
                return false;
            }

            if (requirement != null && !requirement.IsMet())
            {
                onRequirementFailed?.Invoke();
                return false;
            }

            StopHideRoutine();
            IsRunning = true;
            ConfigureProgressDisplay();
            ShowProgress(true);
            Started?.Invoke();
            onStarted?.Invoke();
            return true;
        }

        /// <summary>Sets normalized progress, supporting save restoration and tests.</summary>
        public void SetProgress(float normalizedProgress)
        {
            float previous = Progress;
            Progress = Mathf.Clamp01(normalizedProgress);
            UpdateProgressDisplay();

            if (!Mathf.Approximately(previous, Progress))
            {
                ProgressChanged?.Invoke(Progress);
                onProgressChanged?.Invoke(Progress);
            }

            if (previous < 1f && IsComplete)
            {
                Finish();
            }
        }

        /// <summary>Returns the interaction and its meter to their initial state.</summary>
        public void ResetProgress()
        {
            StopHideRoutine();
            IsRunning = false;
            IsEnabled = true;
            Progress = 0f;
            UpdateProgressDisplay();
            ShowProgress(false);
        }

        /// <summary>Changes whether the interaction can be started.</summary>
        /// <param name="isEnabled">New runtime enabled state.</param>
        public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;

        private void Finish()
        {
            IsRunning = false;
            SetCompletionFlags();
            Completed?.Invoke();
            onCompleted?.Invoke();

            if (disableAfterCompletion)
            {
                IsEnabled = false;
            }

            if (hideProgressWhenComplete)
            {
                if (completionDisplayTime > 0f)
                {
                    hideProgressRoutine = StartCoroutine(
                        HideProgressAfterDelay());
                }
                else
                {
                    ShowProgress(false);
                }
            }
        }

        private void ConfigureProgressDisplay()
        {
            if (runtimeProgressBar != null)
            {
                runtimeProgressBar.Configure(progressName, Progress);
            }

            if (progressSlider != null)
            {
                progressSlider.minValue = 0f;
                progressSlider.maxValue = 1f;
            }

            if (progressLabel != null)
            {
                progressLabel.text = progressName ?? string.Empty;
            }

            UpdateProgressDisplay();
        }

        private void UpdateProgressDisplay()
        {
            if (runtimeProgressBar != null)
            {
                runtimeProgressBar.SetProgress(Progress);
            }

            if (progressSlider != null)
            {
                progressSlider.value = Progress;
            }
        }

        private void ShowProgress(bool visible)
        {
            if (runtimeProgressBar != null)
            {
                runtimeProgressBar.SetVisible(visible);
            }

            if (progressRoot != null && progressRoot != gameObject)
            {
                progressRoot.SetActive(visible);
            }
        }

        private void ResolveProgressBar()
        {
            runtimeProgressBar = progressBarInstance;
            if (runtimeProgressBar != null || progressBarPrefab == null)
            {
                return;
            }

            Transform parent = progressBarAnchor != null
                ? progressBarAnchor
                : transform;
            runtimeProgressBar = Instantiate(
                progressBarPrefab,
                parent,
                false);
        }

        private IEnumerator HideProgressAfterDelay()
        {
            if (useUnscaledTime)
            {
                yield return new WaitForSecondsRealtime(completionDisplayTime);
            }
            else
            {
                yield return new WaitForSeconds(completionDisplayTime);
            }

            hideProgressRoutine = null;
            ShowProgress(false);
        }

        private void StopHideRoutine()
        {
            if (hideProgressRoutine == null)
            {
                return;
            }

            StopCoroutine(hideProgressRoutine);
            hideProgressRoutine = null;
        }

        private void SetCompletionFlags()
        {
            if (FlagManager.Instance == null || flagsToSetOnCompletion == null)
            {
                return;
            }

            foreach (string flagId in flagsToSetOnCompletion)
            {
                if (!string.IsNullOrWhiteSpace(flagId))
                {
                    FlagManager.Instance.SetFlag(flagId);
                }
            }
        }
    }
}
