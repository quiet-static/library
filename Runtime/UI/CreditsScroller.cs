using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace QuietStatic.Toolkit.UI
{
    /// <summary>
    /// Scrolls a UI <see cref="RectTransform"/> upward until it reaches a target Y position.
    /// </summary>
    /// <remarks>
    /// This component is intended for simple credits rolls or any other UI panel that should
    /// continuously move upward and fire an event when it reaches the end of its scroll.
    ///
    /// The component does not load scenes or handle skipping by itself. Instead, use the
    /// <see cref="onCreditsFinished"/> UnityEvent to call whatever should happen after the
    /// credits finish, such as returning to the title screen or loading another scene.
    /// </remarks>
    public class CreditsScroller : MonoBehaviour
    {
        [Header("Scroll Target")]
        [Tooltip("The UI RectTransform that should move upward. If left empty, Reset tries to use this object's RectTransform.")]
        [FormerlySerializedAs("creditsContent")]
        [SerializeField] private RectTransform content;

        [Header("Scroll Settings")]
        [Tooltip("How quickly the credits content moves upward in anchored-position units per second.")]
        [Min(0f)]
        [FormerlySerializedAs("scrollSpeed")]
        [SerializeField] private float speed = 40f;

        [Tooltip("The anchored Y position assigned when this component starts.")]
        [SerializeField] private float startY = -700f;

        [Tooltip("The anchored Y position where the credits are considered finished.")]
        [SerializeField] private float endY = 1200f;

        [Header("Skip")]
        [Tooltip("Allow the configured key to finish the credits immediately.")]
        [SerializeField] private bool allowSkip;

        [Tooltip("Key that finishes the credits when skipping is enabled.")]
        [SerializeField] private KeyCode skipKey = KeyCode.Escape;

        [Header("Events")]
        [Tooltip("Invoked once when the credits content reaches or passes the configured end Y position.")]
        [SerializeField] private UnityEvent onCreditsFinished;

        /// <summary>
        /// Tracks whether the credits have already reached the end.
        /// </summary>
        /// <remarks>
        /// This prevents <see cref="onCreditsFinished"/> from being invoked more than once.
        /// </remarks>
        private bool finished;

        /// <summary>
        /// Gets whether this credits roll has already completed.
        /// </summary>
        public bool IsFinished => finished;

        /// <summary>
        /// Attempts to auto-fill the content reference when the component is added or reset
        /// in the Unity Inspector.
        /// </summary>
        private void Reset()
        {
            content = transform as RectTransform;
        }

        protected virtual void Start()
        {
            if (content == null)
            {
                content = transform as RectTransform;
            }

            if (content != null)
            {
                Vector2 position = content.anchoredPosition;
                position.y = startY;
                content.anchoredPosition = position;
            }
        }

        /// <summary>
        /// Advances the credits scroll each frame until the content reaches the end position.
        /// </summary>
        protected virtual void Update()
        {
            if (finished || content == null)
            {
                return;
            }

            if (allowSkip && Input.KeyboardShortcut.WasPressedThisFrame(skipKey))
            {
                FinishCredits();
                return;
            }

            ScrollContent();

            if (HasReachedEnd())
            {
                FinishCredits();
            }
        }

        /// <summary>
        /// Moves the assigned credits content upward based on the configured scroll speed.
        /// </summary>
        private void ScrollContent()
        {
            content.anchoredPosition += Vector2.up * speed * Time.deltaTime;
        }

        /// <summary>
        /// Checks whether the credits content has reached or passed the target end position.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the content's anchored Y position is greater than or equal to
        /// <see cref="endY"/>; otherwise, <c>false</c>.
        /// </returns>
        private bool HasReachedEnd()
        {
            return content.anchoredPosition.y >= endY;
        }

        /// <summary>
        /// Marks the credits as finished and invokes the completion event once.
        /// </summary>
        protected virtual void FinishCredits()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            onCreditsFinished?.Invoke();
        }
    }
}
