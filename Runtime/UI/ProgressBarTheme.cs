using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuietStatic.Toolkit.UI
{
    /// <summary>Reusable colors shared by world-space and screen-space progress bars.</summary>
    [CreateAssetMenu(
        fileName = "ProgressBarTheme",
        menuName = "Quiet Static Toolkit/UI/Progress Bar Theme")]
    public sealed class ProgressBarTheme : ScriptableObject
    {
        [Tooltip("Color drawn behind the progress fill.")]
        [SerializeField] private Color backgroundColor =
            new(0.035f, 0.035f, 0.045f, 0.9f);

        [Tooltip("Color used by the normalized progress fill.")]
        [SerializeField] private Color fillColor =
            new(0.88f, 0.68f, 0.2f, 1f);

        [Tooltip("Color used by an optional progress label.")]
        [SerializeField] private Color labelColor = Color.white;

        /// <summary>Applies this theme to the supplied optional UI elements.</summary>
        public void Apply(Image background, Image fill, TMP_Text label = null)
        {
            if (background != null)
            {
                background.color = backgroundColor;
            }

            if (fill != null)
            {
                fill.color = fillColor;
            }

            if (label != null)
            {
                label.color = labelColor;
            }
        }
    }
}
