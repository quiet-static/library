using TMPro;
using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>Bottom-screen text presentation with a rich-text-safe typewriter reveal.</summary>
    [DisallowMultipleComponent]
    public sealed class CinematicTextOverlay : MonoBehaviour
    {
        [Tooltip("Bottom-centred text styled like interaction messages.")]
        [SerializeField] private TMP_Text textLabel;
        [Tooltip("Group used to hide text without disabling the player or this component.")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("Continue revealing while gameplay is paused.")]
        [SerializeField] private bool useUnscaledTime = true;
        private float revealed;
        private float speed;
        private int count;
        public bool IsRevealing { get; private set; }
        public TMP_Text TextLabel => textLabel;

        private void Awake() => Clear();
        private void OnDisable() => Clear();
        private void Update()
        {
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
            if (!IsRevealing || textLabel == null) return;
            revealed += speed * (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
            textLabel.maxVisibleCharacters = Mathf.Min(count, Mathf.FloorToInt(revealed));
            if (revealed >= count) RevealAll();
        }

        /// <summary>Begins revealing a beat; returns false when the label is missing.</summary>
        public bool ShowText(string text, float charactersPerSecond)
        {
            if (textLabel == null) return false;
            textLabel.text = text ?? string.Empty;
            textLabel.maxVisibleCharacters = int.MaxValue;
            textLabel.ForceMeshUpdate();
            count = textLabel.textInfo.characterCount;
            revealed = 0;
            speed = charactersPerSecond;
            IsRevealing = count > 0 && speed > 0;
            textLabel.maxVisibleCharacters = IsRevealing ? 0 : count;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            return true;
        }

        /// <summary>Completes the current reveal without advancing its beat.</summary>
        public void RevealAll()
        {
            IsRevealing = false;
            if (textLabel != null) textLabel.maxVisibleCharacters = count;
        }

        /// <summary>Clears stale text on finish, cancellation or scene disable.</summary>
        public void Clear()
        {
            IsRevealing = false;
            count = 0;
            if (textLabel != null) { textLabel.text = string.Empty; textLabel.maxVisibleCharacters = 0; }
            if (canvasGroup != null) canvasGroup.alpha = 0;
        }
    }
}
