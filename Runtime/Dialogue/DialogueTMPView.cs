using Febucci.TextAnimatorForUnity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>
    /// Displays dialogue from a <see cref="DialogueRunner"/> using TextMeshPro labels,
    /// Text Animator typewriter effects, and Unity UI buttons.
    /// </summary>
    public class DialogueTMPView : MonoBehaviour
    {
        [Header("Dialogue Source")]
        [Tooltip("Optional dialogue runner this view should listen to. Leave empty to bind to the active runner when dialogue starts.")]
        [SerializeField] private DialogueRunner runner;

        [Header("UI References")]
        [Tooltip("Root GameObject for the dialogue UI. This is enabled when dialogue starts and disabled when dialogue ends.")]
        [SerializeField] private GameObject root;

        [Tooltip("TextMeshPro label used to display the current speaker name.")]
        [SerializeField] private TMP_Text speakerLabel;

        [Tooltip("TextMeshPro label used to display the current dialogue line.")]
        [SerializeField] private TMP_Text lineLabel;

        [Tooltip("Text Animator's typewriter component attached to the same object as the dialogue line TMP text.")]
        [SerializeField] private TypewriterComponent lineTypewriter;

        [Tooltip("Buttons used to display dialogue choices. Extra buttons are hidden when the current node has fewer choices.")]
        [SerializeField] private Button[] choiceButtons;

        /// <summary>
        /// Tracks whether the current dialogue line is still being revealed.
        /// </summary>
        private bool isTyping;

        /// <summary>
        /// Attempts to auto-fill common UI references when the component is added or reset in the Inspector.
        /// </summary>
        private void Reset()
        {
            root = gameObject;
            runner = GetComponentInParent<DialogueRunner>();

            TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);

            if (labels.Length > 0)
            {
                speakerLabel = labels[0];
            }

            if (labels.Length > 1)
            {
                lineLabel = labels[1];
                lineTypewriter = lineLabel.GetComponent<TypewriterComponent>();
            }

            choiceButtons = GetComponentsInChildren<Button>(true);
        }

        /// <summary>
        /// Subscribes this view to dialogue runner events while the component is active.
        /// </summary>
        private void OnEnable()
        {
            DialogueRunner.OnNodeChanged += HandleNodeChanged;
            DialogueRunner.OnDialogueStarted += HandleDialogueStarted;
            DialogueRunner.OnDialogueEnded += HandleDialogueEnded;
        }

        /// <summary>
        /// Unsubscribes from dialogue runner events to avoid callbacks after this view is disabled.
        /// </summary>
        private void OnDisable()
        {
            DialogueRunner.OnNodeChanged -= HandleNodeChanged;
            DialogueRunner.OnDialogueStarted -= HandleDialogueStarted;
            DialogueRunner.OnDialogueEnded -= HandleDialogueEnded;
        }

        /// <summary>
        /// Starts hidden so the dialogue UI only appears while dialogue is actively running.
        /// </summary>
        private void Awake()
        {
            SetVisible(false);
        }

        /// <summary>
        /// Advances dialogue or instantly reveals the active line.
        /// </summary>
        public void ContinueOrSkip()
        {
            if (runner == null)
            {
                return;
            }

            if (isTyping)
            {
                FinishTyping();
                return;
            }

            runner.Advance();
        }

        /// <summary>
        /// Shows the dialogue UI when the observed runner starts dialogue.
        /// </summary>
        private void HandleDialogueStarted(DialogueRunner activeRunner)
        {
            if (!ShouldHandleRunner(activeRunner))
            {
                return;
            }

            runner = activeRunner;
            SetVisible(true);
        }

        /// <summary>
        /// Hides the dialogue UI when the observed runner finishes dialogue.
        /// </summary>
        private void HandleDialogueEnded(DialogueRunner activeRunner)
        {
            if (!ShouldHandleRunner(activeRunner))
            {
                return;
            }

            isTyping = false;
            SetVisible(false);
        }

        /// <summary>
        /// Updates the displayed dialogue node.
        /// </summary>
        private void HandleNodeChanged(DialogueRunner activeRunner, DialogueTree.Node node)
        {
            if (!ShouldHandleRunner(activeRunner))
            {
                return;
            }

            runner = activeRunner;

            SetSpeaker(node);
            StartTypingLine(node.line);
            BuildChoices(node);
        }

        /// <summary>
        /// Sends the complete dialogue line to Text Animator so it can handle
        /// character visibility, tags, pauses, and appearance effects.
        /// </summary>
        private void StartTypingLine(string line)
        {
            string fullLine = line ?? string.Empty;

            if (lineTypewriter == null)
            {
                GameLogger.Warning(
                    "StartTypingLine",
                    this,
                    $"{nameof(DialogueTMPView)} has no {nameof(TypewriterComponent)} assigned. " +
                    "Falling back to showing dialogue instantly.",
                );

                if (lineLabel != null)
                {
                    lineLabel.text = fullLine;
                }

                isTyping = false;
                return;
            }

            isTyping = true;
            lineTypewriter.ShowText(fullLine);
        }

        /// <summary>
        /// Immediately reveals the remainder of the active dialogue line.
        /// </summary>
        private void FinishTyping()
        {
            if (lineTypewriter != null)
            {
                lineTypewriter.SkipTypewriter();
            }

            isTyping = false;
        }

        /// <summary>
        /// Shows, hides, labels, and wires choice buttons for the current dialogue node.
        /// </summary>
        private void BuildChoices(DialogueTree.Node node)
        {
            if (choiceButtons == null)
            {
                return;
            }

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                ConfigureChoiceButton(choiceButtons[i], node, i);
            }
        }

        /// <summary>
        /// Configures one choice button for a matching choice index,
        /// or hides it when no choice exists.
        /// </summary>
        private void ConfigureChoiceButton(Button button, DialogueTree.Node node, int choiceIndex)
        {
            if (button == null)
            {
                return;
            }

            bool hasChoice = node.choices != null && choiceIndex < node.choices.Length;

            button.gameObject.SetActive(hasChoice);
            button.onClick.RemoveAllListeners();

            if (!hasChoice)
            {
                return;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();

            if (label != null)
            {
                label.text = node.choices[choiceIndex].text;
            }

            int capturedIndex = choiceIndex;
            button.onClick.AddListener(() => runner.Choose(capturedIndex));
        }

        /// <summary>
        /// Updates the speaker label for the current dialogue node.
        /// </summary>
        private void SetSpeaker(DialogueTree.Node node)
        {
            if (speakerLabel != null)
            {
                speakerLabel.text = node.speaker;
            }
        }

        /// <summary>
        /// Determines whether this view should react to an event from a specific dialogue runner.
        /// </summary>
        private bool ShouldHandleRunner(DialogueRunner activeRunner)
        {
            return runner == null || activeRunner == runner;
        }

        /// <summary>
        /// Shows or hides the configured dialogue root object.
        /// </summary>
        private void SetVisible(bool visible)
        {
            if (root != null)
            {
                root.SetActive(visible);
            }
        }
    }
}