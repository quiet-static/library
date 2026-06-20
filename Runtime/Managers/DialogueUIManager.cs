using System;
using QuietStatic.Toolkit.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>
    /// Reusable UI view for displaying dialogue text and selectable responses.
    /// </summary>
    /// <remarks>
    /// This class only controls presentation:
    /// - Showing and hiding the dialogue canvas
    /// - Displaying speaker names and dialogue text
    /// - Displaying selectable response buttons
    /// - Raising events when a response is chosen
    ///
    /// It does not know about DialogueTree, DialoguePrompt, GameFlag, GameStateManager,
    /// scene loading, branching rules, or any specific game characters.
    /// </remarks>
    public class DialogueUIManager : ToolkitSingleton<DialogueUIManager>
    {
        /// <summary>
        /// Raised when the player selects a dialogue choice.
        /// The integer is the selected choice index.
        /// </summary>
        public event Action<int> OnChoiceSelected;

        /// <summary>
        /// Raised whenever the dialogue UI becomes visible or hidden.
        /// The bool is true when visible.
        /// </summary>
        public event Action<bool> OnDialogueVisibilityChanged;

        [Serializable]
        public class ChoiceSelectedEvent : UnityEvent<int>
        {
        }

        [Header("References")]
        [Tooltip("Canvas containing the dialogue UI.")]
        [SerializeField] private Canvas dialogueCanvas;

        [Tooltip("Text field used for the current speaker name.")]
        [SerializeField] private TMP_Text speakerLabel;

        [Tooltip("Text field used for dialogue lines without response options.")]
        [SerializeField] private TMP_Text dialogueNoOptionsText;

        [Tooltip("Text field used for dialogue lines with response options.")]
        [SerializeField] private TMP_Text dialogueWithOptionsText;

        [Tooltip("Buttons used for dialogue response choices.")]
        [SerializeField] private Button[] choiceButtons;

        [Tooltip("Text labels corresponding to each dialogue response button.")]
        [SerializeField] private TMP_Text[] choiceLabels;

        [Header("Cursor Behavior")]
        [Tooltip("Whether this UI should unlock and show the cursor while dialogue is visible.")]
        [SerializeField] private bool controlCursor = true;

        [Tooltip("Cursor lock mode restored when dialogue UI is hidden.")]
        [SerializeField] private CursorLockMode hiddenCursorLockMode = CursorLockMode.Locked;

        [Tooltip("Whether the cursor should be visible when dialogue UI is hidden.")]
        [SerializeField] private bool cursorVisibleWhenHidden;

        [Header("Unity Events")]
        [Tooltip("Invoked when the player selects a response button.")]
        [SerializeField] private ChoiceSelectedEvent onChoiceSelected;

        [Tooltip("Invoked whenever dialogue UI becomes visible or hidden.")]
        [SerializeField] private UnityEvent<bool> onDialogueVisibilityChanged;

        /// <summary>
        /// Gets whether the dialogue UI is currently visible.
        /// </summary>
        public bool IsVisible { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            DontDestroyOnLoad(gameObject);
            WireChoiceButtons();
            HideDialogueUI();
        }

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
        /// Shows the dialogue UI.
        /// </summary>
        public void ShowDialogueUI()
        {
            SetDialogueVisible(true);
        }

        /// <summary>
        /// Hides the dialogue UI and clears all currently displayed content.
        /// </summary>
        public void HideDialogueUI()
        {
            ClearDialogueUI();
            SetDialogueVisible(false);
        }

        /// <summary>
        /// Displays a dialogue line without response choices.
        /// </summary>
        /// <param name="speakerName">Optional speaker name to display.</param>
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
        /// Displays a dialogue line with selectable response choices.
        /// </summary>
        /// <param name="speakerName">Optional speaker name to display.</param>
        /// <param name="dialogueText">Dialogue body text to display.</param>
        /// <param name="choices">Text for each available response choice.</param>
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
        /// Updates the speaker label.
        /// </summary>
        public void SetSpeakerName(string speakerName)
        {
            if (speakerLabel != null)
            {
                speakerLabel.text = speakerName ?? string.Empty;
            }
        }

        /// <summary>
        /// Clears all displayed dialogue content and hides response buttons.
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
        /// Displays and labels response buttons for the provided choices.
        /// Extra buttons are hidden automatically.
        /// </summary>
        /// <param name="choices">Choice text to display, or null to hide all choices.</param>
        public void SetChoiceTexts(string[] choices)
        {
            int choiceCount = choices?.Length ?? 0;

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                bool shouldShow = i < choiceCount;

                if (choiceButtons[i] != null)
                {
                    choiceButtons[i].gameObject.SetActive(shouldShow);
                }

                if (i >= choiceLabels.Length || choiceLabels[i] == null)
                {
                    continue;
                }

                choiceLabels[i].text = shouldShow
                    ? choices[i] ?? string.Empty
                    : string.Empty;
            }
        }

        /// <summary>
        /// Allows external systems to trigger a choice as though the player selected it.
        /// </summary>
        /// <param name="choiceIndex">Index of the chosen response.</param>
        public void SelectChoice(int choiceIndex)
        {
            OnChoiceSelected?.Invoke(choiceIndex);
            onChoiceSelected?.Invoke(choiceIndex);
        }

        /// <summary>
        /// Finds and wires each response button to its matching choice index.
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
                button.onClick.AddListener(() => SelectChoice(capturedIndex));
            }
        }

        /// <summary>
        /// Shows the no-options dialogue text and hides the options text.
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
        /// Shows the options dialogue text and hides the no-options text.
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
        /// Changes UI visibility and applies configured cursor behavior.
        /// </summary>
        private void SetDialogueVisible(bool visible)
        {
            IsVisible = visible;

            if (dialogueCanvas != null)
            {
                dialogueCanvas.enabled = visible;
            }

            if (controlCursor)
            {
                Cursor.lockState = visible
                    ? CursorLockMode.None
                    : hiddenCursorLockMode;

                Cursor.visible = visible || cursorVisibleWhenHidden;
            }

            OnDialogueVisibilityChanged?.Invoke(visible);
            onDialogueVisibilityChanged?.Invoke(visible);
        }
    }
}