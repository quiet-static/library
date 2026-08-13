using System;
using System.Collections;
using QuietStatic.Toolkit.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace QuietStatic
{
    /// <summary>
    /// Displays lightweight interaction prompts and temporary gameplay messages.
    /// </summary>
    /// <remarks>
    /// This component is intentionally presentation-only.
    ///
    /// It does not know about interactables, dialogue trees, flags, characters,
    /// item pickups, or project-specific interaction event data.
    ///
    /// Other systems can call its public methods to show prompts such as:
    /// - "Press E to Open Door"
    /// - "The door is locked."
    /// - "You picked up the basement key."
    /// - "This switch does nothing."
    /// </remarks>
    public class InteractionUIManager : ToolkitSingleton<InteractionUIManager>
    {
        [Serializable]
        public class StringUnityEvent : UnityEvent<string>
        {
        }

        [Header("UI References")]
        [Tooltip("Text used for temporary gameplay feedback, such as pickup messages or locked-door messages.")]
        [SerializeField] private TMP_Text messageText;

        [Tooltip("Text used for a persistent interaction prompt, such as Press E to Interact.")]
        [SerializeField] private TMP_Text promptText;

        [Tooltip("Optional root shown while a hold interaction is progressing.")]
        [SerializeField] private GameObject progressRoot;

        [Tooltip("Optional label describing the hold interaction meter.")]
        [SerializeField] private TMP_Text progressLabel;

        [Tooltip("Optional normalized bar used by hold interactions.")]
        [SerializeField] private Slider progressSlider;

        [Header("Message Settings")]
        [Tooltip("Default number of seconds temporary messages remain visible.")]
        [Min(0f)]
        [SerializeField] private float defaultDisplayTime = 3f;

        [Tooltip("Whether temporary message timers should continue while Time.timeScale is zero.")]
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Unity Events")]
        [Tooltip("Invoked whenever a temporary interaction message is shown.")]
        [SerializeField] private StringUnityEvent onMessageShown;

        [Tooltip("Invoked whenever the persistent interaction prompt is shown or updated.")]
        [SerializeField] private StringUnityEvent onPromptShown;

        [Tooltip("Invoked whenever the temporary message is hidden.")]
        [SerializeField] private UnityEvent onMessageHidden;

        [Tooltip("Invoked whenever the persistent interaction prompt is hidden.")]
        [SerializeField] private UnityEvent onPromptHidden;

        /// <summary>
        /// Coroutine currently responsible for automatically hiding a temporary message.
        /// </summary>
        private Coroutine hideMessageRoutine;

        /// <summary>
        /// Gets whether a temporary message is currently visible.
        /// </summary>
        public bool IsMessageVisible =>
            messageText != null && messageText.gameObject.activeSelf;

        /// <summary>
        /// Gets whether the persistent interaction prompt is currently visible.
        /// </summary>
        public bool IsPromptVisible =>
            promptText != null && promptText.gameObject.activeSelf;

        /// <summary>
        /// Initializes the singleton and hides both UI elements.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
            {
                return;
            }

            HideMessage();
            HidePrompt();
            HideProgress();
        }

        /// <summary>
        /// Shows an interaction prompt until another system hides or replaces it.
        /// </summary>
        /// <param name="text">Prompt text to display.</param>
        public void ShowPrompt(string text)
        {
            if (promptText == null)
            {
                return;
            }

            promptText.text = text ?? string.Empty;
            promptText.gameObject.SetActive(!string.IsNullOrWhiteSpace(text));

            if (string.IsNullOrWhiteSpace(text))
            {
                onPromptHidden?.Invoke();
                return;
            }

            onPromptShown?.Invoke(text);
        }

        /// <summary>
        /// Hides and clears the persistent interaction prompt.
        /// </summary>
        public void HidePrompt()
        {
            if (promptText != null)
            {
                promptText.text = string.Empty;
                promptText.gameObject.SetActive(false);
            }

            onPromptHidden?.Invoke();
        }

        /// <summary>Shows a named, normalized hold-interaction meter.</summary>
        public void ShowProgress(string label, float normalizedProgress)
        {
            if (progressLabel != null)
            {
                progressLabel.text = label ?? string.Empty;
            }

            if (progressSlider != null)
            {
                progressSlider.minValue = 0f;
                progressSlider.maxValue = 1f;
                progressSlider.value = Mathf.Clamp01(normalizedProgress);
            }

            if (progressRoot != null)
            {
                progressRoot.SetActive(true);
            }
        }

        /// <summary>Hides the hold-interaction meter.</summary>
        public void HideProgress()
        {
            if (progressRoot != null)
            {
                progressRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Shows a temporary message using the configured default duration.
        /// </summary>
        /// <param name="text">Message text to display.</param>
        public void ShowMessage(string text)
        {
            ShowMessageForSeconds(text, defaultDisplayTime);
        }

        /// <summary>
        /// Shows a temporary message for a specific duration.
        /// </summary>
        /// <param name="text">Message text to display.</param>
        /// <param name="seconds">
        /// Number of seconds to show the message. Values less than or equal to zero
        /// keep the message visible until <see cref="HideMessage"/> is called.
        /// </param>
        public void ShowMessageForSeconds(string text, float seconds)
        {
            if (messageText == null)
            {
                return;
            }

            StopMessageHideRoutine();

            messageText.text = text ?? string.Empty;
            messageText.gameObject.SetActive(!string.IsNullOrWhiteSpace(text));

            if (string.IsNullOrWhiteSpace(text))
            {
                HideMessage();
                return;
            }

            onMessageShown?.Invoke(text);

            if (seconds > 0f)
            {
                hideMessageRoutine = StartCoroutine(
                    HideMessageAfterDelay(seconds)
                );
            }
        }

        /// <summary>
        /// Hides and clears the temporary interaction message.
        /// </summary>
        public void HideMessage()
        {
            StopMessageHideRoutine();

            if (messageText != null)
            {
                messageText.text = string.Empty;
                messageText.gameObject.SetActive(false);
            }

            onMessageHidden?.Invoke();
        }

        /// <summary>
        /// Hides both the prompt and temporary message.
        /// </summary>
        public void ClearAll()
        {
            HidePrompt();
            HideMessage();
            HideProgress();
        }

        /// <summary>
        /// Waits for the requested amount of time before hiding the temporary message.
        /// </summary>
        private IEnumerator HideMessageAfterDelay(float seconds)
        {
            if (useUnscaledTime)
            {
                yield return new WaitForSecondsRealtime(seconds);
            }
            else
            {
                yield return new WaitForSeconds(seconds);
            }

            hideMessageRoutine = null;
            HideMessage();
        }

        /// <summary>
        /// Stops the active auto-hide coroutine, if one exists.
        /// </summary>
        private void StopMessageHideRoutine()
        {
            if (hideMessageRoutine == null)
            {
                return;
            }

            StopCoroutine(hideMessageRoutine);
            hideMessageRoutine = null;
        }
    }
}
