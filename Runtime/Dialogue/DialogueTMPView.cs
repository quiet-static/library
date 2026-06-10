using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.Dialogue
{
    /// <summary>
    /// Displays dialogue from a <see cref="DialogueRunner"/> using TextMeshPro labels and Unity UI buttons.
    /// </summary>
    /// <remarks>
    /// This view is intentionally lightweight and reusable. It listens to the static events raised by
    /// <see cref="DialogueRunner"/>, shows or hides a dialogue root object, types dialogue text over time,
    /// and wires available choices to UI buttons.
    ///
    /// Assign a specific runner to make this view respond only to that runner. Leave the runner reference
    /// empty if the view should bind to whichever runner starts sending dialogue events first.
    /// </remarks>
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

        [Tooltip("Buttons used to display dialogue choices. Extra buttons are hidden when the current node has fewer choices.")]
        [SerializeField] private Button[] choiceButtons;

        [Header("Typing")]
        [Tooltip("How many characters are revealed per second. Set to 0 or below to display each line instantly.")]
        [Min(0f)]
        [SerializeField] private float charactersPerSecond = 45f;

        /// <summary>
        /// Active coroutine used for the current typewriter effect.
        /// </summary>
        private Coroutine typingRoutine;

        /// <summary>
        /// Tracks whether the current line is still being typed out.
        /// </summary>
        private bool isTyping;

        /// <summary>
        /// Full text for the current line, cached so the typewriter effect can be skipped instantly.
        /// </summary>
        private string fullLine;

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
        /// Advances dialogue or skips the active typewriter effect.
        /// </summary>
        /// <remarks>
        /// Call this from input, a Continue button, or a UI input event. If a line is currently typing,
        /// this method finishes the line immediately. Otherwise, it asks the current runner to advance.
        /// </remarks>
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
        /// <param name="activeRunner">The runner that started dialogue.</param>
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
        /// <param name="activeRunner">The runner that ended dialogue.</param>
        private void HandleDialogueEnded(DialogueRunner activeRunner)
        {
            if (!ShouldHandleRunner(activeRunner))
            {
                return;
            }

            SetVisible(false);
        }

        /// <summary>
        /// Updates labels, restarts the typewriter effect, and rebuilds choice buttons for a new node.
        /// </summary>
        /// <param name="activeRunner">The runner that changed nodes.</param>
        /// <param name="node">The newly active dialogue node.</param>
        private void HandleNodeChanged(DialogueRunner activeRunner, DialogueTree.Node node)
        {
            if (!ShouldHandleRunner(activeRunner))
            {
                return;
            }

            runner = activeRunner;
            SetSpeaker(node);
            StartTypingLine(node);
            BuildChoices(node);
        }

        /// <summary>
        /// Types a line into the line label one character at a time.
        /// </summary>
        /// <param name="line">The complete line to reveal.</param>
        /// <returns>Coroutine yield instructions for the typewriter effect.</returns>
        private IEnumerator TypeLine(string line)
        {
            isTyping = true;

            if (lineLabel != null)
            {
                lineLabel.text = string.Empty;
            }

            float delay = charactersPerSecond <= 0f ? 0f : 1f / charactersPerSecond;

            for (int i = 0; i < line.Length; i++)
            {
                if (lineLabel != null)
                {
                    lineLabel.text = line.Substring(0, i + 1);
                }

                if (delay > 0f)
                {
                    yield return new WaitForSeconds(delay);
                }
            }

            isTyping = false;
        }

        /// <summary>
        /// Immediately completes the active typewriter effect and displays the full current line.
        /// </summary>
        private void FinishTyping()
        {
            if (typingRoutine != null)
            {
                StopCoroutine(typingRoutine);
                typingRoutine = null;
            }

            if (lineLabel != null)
            {
                lineLabel.text = fullLine;
            }

            isTyping = false;
        }

        /// <summary>
        /// Shows, hides, labels, and wires choice buttons for the current dialogue node.
        /// </summary>
        /// <param name="node">The dialogue node containing choices to display.</param>
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
        /// Configures one choice button for a matching choice index, or hides it when no choice exists.
        /// </summary>
        /// <param name="button">The button to configure.</param>
        /// <param name="node">The active dialogue node.</param>
        /// <param name="choiceIndex">The choice index represented by the button.</param>
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
        /// <param name="node">The dialogue node containing the speaker name.</param>
        private void SetSpeaker(DialogueTree.Node node)
        {
            if (speakerLabel != null)
            {
                speakerLabel.text = node.speaker;
            }
        }

        /// <summary>
        /// Starts typing the line from the current dialogue node.
        /// </summary>
        /// <param name="node">The dialogue node containing the line text.</param>
        private void StartTypingLine(DialogueTree.Node node)
        {
            fullLine = node.line ?? string.Empty;

            if (typingRoutine != null)
            {
                StopCoroutine(typingRoutine);
            }

            typingRoutine = StartCoroutine(TypeLine(fullLine));
        }

        /// <summary>
        /// Determines whether this view should react to an event from a specific dialogue runner.
        /// </summary>
        /// <param name="activeRunner">The runner that raised the event.</param>
        /// <returns>
        /// <c>true</c> if this view has no assigned runner yet or the event came from the assigned runner;
        /// otherwise, <c>false</c>.
        /// </returns>
        private bool ShouldHandleRunner(DialogueRunner activeRunner)
        {
            return runner == null || activeRunner == runner;
        }

        /// <summary>
        /// Shows or hides the configured dialogue root object.
        /// </summary>
        /// <param name="visible">Whether the dialogue UI should be visible.</param>
        private void SetVisible(bool visible)
        {
            if (root != null)
            {
                root.SetActive(visible);
            }
        }
    }
}
