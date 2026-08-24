using System.Text;
using TMPro;
using UnityEngine;

namespace QuietStatic.Toolkit.Minigames
{
    /// <summary>
    /// Displays an input sequence and visually marks completed and current steps.
    /// </summary>
    public sealed class InputSequenceView : MonoBehaviour
    {
        [Tooltip("Root shown while the minigame is active. Defaults to this GameObject.")]
        [SerializeField] private GameObject displayRoot;

        [Tooltip("Text element used to display the required inputs.")]
        [SerializeField] private TMP_Text sequenceText;

        [Tooltip("Short instruction displayed above the active input sequence. Leave empty to hide it.")]
        [SerializeField] private string instructionText = "Enter the sequence below";

        [Tooltip("Text color used for already completed steps.")]
        [SerializeField] private Color completedColor = new Color(0.45f, 0.8f, 0.45f);

        [Tooltip("Text color used for the input currently expected.")]
        [SerializeField] private Color currentColor = Color.white;

        [Tooltip("Text color used for upcoming inputs.")]
        [SerializeField] private Color upcomingColor = new Color(0.6f, 0.6f, 0.6f);

        [Tooltip("Text placed between sequence inputs.")]
        [SerializeField] private string separator = "  ";

        /// <summary>Shows the configured sequence at the supplied progress index.</summary>
        public void Show(InputSequenceDefinition definition, int currentIndex)
        {
            SetVisible(true);

            if (sequenceText == null || definition == null)
            {
                return;
            }

            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(instructionText))
            {
                builder.Append("<size=70%>");
                AppendEscaped(builder, instructionText.Trim());
                builder.Append("</size>\n");
            }

            for (int i = 0; i < definition.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(separator);
                }

                Color color = i < currentIndex
                    ? completedColor
                    : i == currentIndex ? currentColor : upcomingColor;

                builder.Append("<color=#");
                builder.Append(ColorUtility.ToHtmlStringRGBA(color));
                builder.Append('>');
                AppendEscaped(builder, definition.Steps[i].DisplayName);
                builder.Append("</color>");
            }

            sequenceText.text = builder.ToString();
        }

        /// <summary>Hides the sequence display.</summary>
        public void Hide() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            GameObject root = displayRoot != null ? displayRoot : gameObject;
            root.SetActive(visible);
        }

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            builder.Append((value ?? "?")
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;"));
        }
    }
}
