/*
 * DialogueUIManager.cs
 * 
 * Persistent UI manager for displaying dialogue content.
 * 
 * This component owns presentation only:
 * - Canvas visibility.
 * - Speaker text.
 * - Dialogue body text.
 * - Choice buttons and labels.
 * - Cursor behavior while dialogue is visible.
 * 
 * It does not know about DialogueTree traversal, flags, scene objects,
 * interactables, or game-specific dialogue rules.
 */

using System;
using QuietStatic.Toolkit.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>
    /// Displays dialogue text and response choices.
    /// </summary>
    public class DialogueUIManager : ToolkitSingleton<DialogueUIManager>
    {
        /// <summary>
        /// Raised when the player selects a dialogue choice.
        /// </summary>
        public event Action<int> OnChoiceSelected;

        /// <summary>
        /// Raised whenever dialogue UI visibility changes.
        /// </summary>
        public event Action<bool> OnDialogueVisibilityChanged;

        [Serializable]
        public class ChoiceSelectedEvent : UnityEvent<int>
        {
        }

        [Header("Canvas")]
        [Tooltip("Canvas containing the dialogue UI.")]
        [SerializeField] private Canvas dialogueCanvas;

        [Tooltip("Optional root GameObject for the dialogue UI. If assigned, this is activated and deactivated with the canvas.")]
        [SerializeField] private GameObject dialogueRoot;

        [Header("Text")]
        [Tooltip("Text field used for the current speaker name.")]
        [SerializeField] private TMP_Text speakerLabel;

        [Tooltip("Text field used when the current dialogue node has no response choices.")]
        [SerializeField] private TMP_Text dialogueNoOptionsText;

        [Tooltip("Text field used when the current dialogue node has response choices.")]
        [SerializeField] private TMP_Text dialogueWithOptionsText;

        [Header("Choices")]
        [Tooltip("Buttons used for response choices.")]
        [SerializeField] private Button[] choiceButtons;

        [Tooltip("Text labels matching each response choice button.")]
        [SerializeField] private TMP_Text[] choiceLabels;

        [Header("Cursor Behavior")]
        [Tooltip("Whether this UI should unlock and show the cursor while dialogue is visible.")]
        [SerializeField] private bool controlCursor = true;

        [Tooltip("Cursor lock mode restored when dialogue UI is hidden.")]
        [SerializeField] private CursorLockMode hiddenCursorLockMode = CursorLockMode.Locked;

        [Tooltip("Whether the cursor should remain visible when dialogue UI is hidden.")]
        [SerializeField] private bool cursorVisibleWhenHidden;

        [Header("Unity Events")]
        [Tooltip("Invoked when the player selects a response choice.")]
        [SerializeField] private ChoiceSelectedEvent onChoiceSelected;

        [Tooltip("Invoked whenever dialogue UI visibility changes.")]
        [SerializeField] private UnityEvent<bool> onDialogueVisibilityChanged;

        /// <summary>
        /// Gets whether the dialogue UI is currently visible.
        /// </summary>
        public bool IsVisible { get; private set; }

        /// <summary>
        /// Initializes the singleton, wires choice buttons, and hides the UI.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
            {
                return;
            }

            DontDestroyOnLoad(gameObject);
            WireChoiceButtons();
            HideDialogueUI();
        }

        /// <summary>
        /// Keeps the cursor available while dialogue is visible.
        /// </summary>
        private void LateUpdate()
        {
            if (!IsVisible || !controlCursor)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// Shows a dialogue line without response choices.
        /// </summary>
        /// <param name="speakerName">Speaker name to display.</param>
        /// <param name="dialogueText">Dialogue body text to display.</param>
        public void ShowLine(string speakerName, string dialogueText)
        {
            SetSpeakerName(speakerName);
            SetNoOptionsMode();

            if (dialogueNoOptionsText != null)
            {
                dialogueNoOptionsText.text = dialogueText ?? string.Empty;
            }

            ShowDialogueUI();
        }

        /// <summary>
        /// Shows a dialogue line with response choices.
        /// </summary>
        /// <param name="speakerName">Speaker name to display.</param>
        /// <param name="dialogueText">Dialogue body text to display.</param>
        /// <param name="choices">Choice labels to display.</param>
        public void ShowChoices(
            string speakerName,
            string dialogueText,
            string[] choices
        )
        {
            SetSpeakerName(speakerName);
            SetOptionsMode();

            if (dialogueWithOptionsText != null)
            {
                dialogueWithOptionsText.text = dialogueText ?? string.Empty;
            }

            SetChoiceTexts(choices);
            ShowDialogueUI();
        }

        /// <summary>
        /// Shows the dialogue UI.
        /// </summary>
        public void ShowDialogueUI()
        {
            SetDialogueVisible(true);
        }

        /// <summary>
        /// Hides the dialogue UI and clears current text.
        /// </summary>
        public void HideDialogueUI()
        {
            ClearDialogueUI();
            SetDialogueVisible(false);
        }

        /// <summary>
        /// Clears all dialogue text and hides choices.
        /// </summary>
        public void ClearDialogueUI()
        {
            if (speakerLabel != null)
            {
                speakerLabel.text = string.Empty;
            }

            if (dialogueNoOptionsText != null)
            {
                dialogueNoOptionsText.text = string.Empty;
            }

            if (dialogueWithOptionsText != null)
            {
                dialogueWithOptionsText.text = string.Empty;
            }

            SetChoiceTexts(null);
        }

        /// <summary>
        /// Updates the speaker name label.
        /// </summary>
        /// <param name="speakerName">Speaker name to display.</param>
        public void SetSpeakerName(string speakerName)
        {
            if (speakerLabel == null)
            {
                return;
            }

            speakerLabel.text = speakerName ?? string.Empty;
        }

        /// <summary>
        /// Displays and labels the available choice buttons.
        /// </summary>
        /// <param name="choices">Choice labels to display, or null to hide all choices.</param>
        public void SetChoiceTexts(string[] choices)
        {
            int choiceCount = choices?.Length ?? 0;
            int buttonCount = choiceButtons?.Length ?? 0;

            for (int i = 0; i < buttonCount; i++)
            {
                bool shouldShow = i < choiceCount;

                if (choiceButtons[i] != null)
                {
                    choiceButtons[i].gameObject.SetActive(shouldShow);
                }

                if (choiceLabels == null || i >= choiceLabels.Length || choiceLabels[i] == null)
                {
                    continue;
                }

                choiceLabels[i].text = shouldShow
                    ? choices[i] ?? string.Empty
                    : string.Empty;
            }
        }

        /// <summary>
        /// Selects a response choice by index.
        /// </summary>
        /// <param name="choiceIndex">Index of the selected choice.</param>
        public void SelectChoice(int choiceIndex)
        {
            OnChoiceSelected?.Invoke(choiceIndex);
            onChoiceSelected?.Invoke(choiceIndex);
        }

        /// <summary>
        /// Connects each configured choice button to its matching choice index.
        /// </summary>
        private void WireChoiceButtons()
        {
            if (choiceButtons == null)
            {
                return;
            }

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                Button button = choiceButtons[i];

                if (button == null)
                {
                    continue;
                }

                int capturedIndex = i;

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectChoice(capturedIndex));
            }
        }

        /// <summary>
        /// Configures the UI for dialogue without choices.
        /// </summary>
        private void SetNoOptionsMode()
        {
            if (dialogueNoOptionsText != null)
            {
                dialogueNoOptionsText.gameObject.SetActive(true);
            }

            if (dialogueWithOptionsText != null)
            {
                dialogueWithOptionsText.gameObject.SetActive(false);
            }

            SetChoiceTexts(null);
        }

        /// <summary>
        /// Configures the UI for dialogue with choices.
        /// </summary>
        private void SetOptionsMode()
        {
            if (dialogueNoOptionsText != null)
            {
                dialogueNoOptionsText.gameObject.SetActive(false);
            }

            if (dialogueWithOptionsText != null)
            {
                dialogueWithOptionsText.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Applies dialogue UI visibility and cursor behavior.
        /// </summary>
        /// <param name="visible">Whether the dialogue UI should be visible.</param>
        private void SetDialogueVisible(bool visible)
        {
            IsVisible = visible;

            if (dialogueRoot != null)
            {
                dialogueRoot.SetActive(visible);
            }

            if (dialogueCanvas != null)
            {
                dialogueCanvas.enabled = visible;
            }

            ApplyCursorState(visible);

            OnDialogueVisibilityChanged?.Invoke(visible);
            onDialogueVisibilityChanged?.Invoke(visible);
        }

        /// <summary>
        /// Applies configured cursor behavior for the current visibility state.
        /// </summary>
        /// <param name="visible">Whether dialogue is visible.</param>
        private void ApplyCursorState(bool visible)
        {
            if (!controlCursor)
            {
                return;
            }

            Cursor.lockState = visible
                ? CursorLockMode.None
                : hiddenCursorLockMode;

            Cursor.visible = visible || cursorVisibleWhenHidden;
        }
    }
}
