using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>Reusable text content displayed by a readable overlay.</summary>
    [CreateAssetMenu(
        fileName = "ReadableContent",
        menuName = "Quiet Static Toolkit/Interactions/Readable Content")]
    public sealed class ReadableContentDefinition : ScriptableObject
    {
        [Tooltip("Optional heading displayed above the body.")]
        [SerializeField] private string title;

        [Tooltip("Long-form text shown on the readable overlay.")]
        [TextArea(6, 30)]
        [SerializeField] private string body;

        [Tooltip("Optional label for the close control. The handler default is used when empty.")]
        [SerializeField] private string closeLabel = "Close";

        /// <summary>Readable heading.</summary>
        public string Title => title ?? string.Empty;

        /// <summary>Readable body text.</summary>
        public string Body => body ?? string.Empty;

        /// <summary>Optional close-control label.</summary>
        public string CloseLabel => closeLabel ?? string.Empty;
    }
}
