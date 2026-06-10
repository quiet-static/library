using UnityEngine;

namespace QuietStatic.Cinematic
{
    /// <summary>
    /// Scrolls a UI <see cref="RectTransform"/> upward to create a simple credits roll.
    /// </summary>
    /// <remarks>
    /// This component is intended for lightweight end-credit sequences where a block of UI
    /// content moves from a starting anchored Y position to an ending anchored Y position.
    ///
    /// When the scroll reaches the end position, the component can ask the
    /// <see cref="GameSceneManager"/> to return the player to the title menu.
    ///
    /// Attach this component to a UI object in the credits scene. If no credits content
    /// reference is assigned, the component will try to use the <see cref="RectTransform"/>
    /// on the same GameObject.
    /// </remarks>
    public class CreditsScroller : MonoBehaviour
    {
        [Header("Credits Movement")]
        [Tooltip("The RectTransform containing the credits text or credits layout that should scroll upward.")]
        [SerializeField] private RectTransform creditsContent;

        [Tooltip("How quickly the credits move upward in anchored-position units per second.")]
        [Min(0f)]
        [SerializeField] private float scrollSpeed = 60f;

        [Tooltip("The anchored Y position where the credits content starts when the scene begins.")]
        [SerializeField] private float startY = -700f;

        [Tooltip("The anchored Y position that counts as the end of the credits roll.")]
        [SerializeField] private float endY = 1200f;

        [Header("Finish Behavior")]
        [Tooltip("If enabled, finishing the credits returns to the title menu through GameSceneManager.")]
        [SerializeField] private bool loadSceneWhenFinished = true;

        [Tooltip("Legacy scene-name field kept for older Inspector setups. The current script returns through GameSceneManager instead of loading this scene directly.")]
        [SerializeField] private string sceneToLoad = "TitleScene";

        [Header("Skip")]
        [Tooltip("Reserved for skip behavior. The current script does not read skip input because skipping is commented out in Update.")]
        [SerializeField] private bool allowSkip = true;

        [Tooltip("Reserved key for skip behavior if skip input is re-enabled later.")]
        [SerializeField] private KeyCode skipKey = KeyCode.Escape;

        /// <summary>
        /// Tracks whether the credits sequence has already finished.
        /// </summary>
        /// <remarks>
        /// This prevents the finish behavior from running multiple times if the credits
        /// continue updating or if multiple finish conditions happen in the same frame.
        /// </remarks>
        private bool hasFinished;

        /// <summary>
        /// Attempts to automatically assign the credits content reference in the Inspector.
        /// </summary>
        /// <remarks>
        /// Unity calls this when the component is first added or when Reset is selected from
        /// the component context menu.
        /// </remarks>
        private void Reset()
        {
            creditsContent = GetComponent<RectTransform>();
        }

        /// <summary>
        /// Initializes the credits content reference and places the content at its start position.
        /// </summary>
        private void Start()
        {
            if (creditsContent == null)
            {
                creditsContent = GetComponent<RectTransform>();
            }

            if (creditsContent == null)
            {
                Debug.LogWarning(
                    $"{nameof(CreditsScroller)} could not find a RectTransform to scroll.",
                    this
                );

                enabled = false;
                return;
            }

            SetCreditsYPosition(startY);
        }

        /// <summary>
        /// Advances the credits roll each frame until it reaches the configured end position.
        /// </summary>
        private void Update()
        {
            if (hasFinished)
            {
                return;
            }

            ScrollCredits();

            // Skipping is intentionally left disabled for now.
            // Re-enable this block if credits should be skippable again.
            // if (allowSkip && Input.GetKeyDown(skipKey))
            // {
            //     FinishCredits();
            // }
        }

        /// <summary>
        /// Moves the credits content upward and finishes the sequence once the end position is reached.
        /// </summary>
        private void ScrollCredits()
        {
            Vector2 position = creditsContent.anchoredPosition;
            position.y += scrollSpeed * Time.deltaTime;
            creditsContent.anchoredPosition = position;

            if (position.y >= endY)
            {
                FinishCredits();
            }
        }

        /// <summary>
        /// Sets the credits content to a specific anchored Y position.
        /// </summary>
        /// <param name="yPosition">The anchored Y position to apply to the credits content.</param>
        private void SetCreditsYPosition(float yPosition)
        {
            Vector2 position = creditsContent.anchoredPosition;
            position.y = yPosition;
            creditsContent.anchoredPosition = position;
        }

        /// <summary>
        /// Marks the credits as finished and optionally returns to the title menu.
        /// </summary>
        /// <remarks>
        /// The return-to-title behavior depends on <see cref="GameSceneManager.Instance"/>.
        /// If the scene manager is missing, a warning is logged and no scene transition occurs.
        /// </remarks>
        private void FinishCredits()
        {
            if (hasFinished)
            {
                return;
            }

            hasFinished = true;

            if (!loadSceneWhenFinished)
            {
                return;
            }

            if (GameSceneManager.Instance == null)
            {
                Debug.LogWarning("Credits finished, but GameSceneManager.Instance was null.", this);
                return;
            }

            GameSceneManager.Instance.ReturnToTitleMenu();
        }
    }
}
