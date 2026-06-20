using System.Collections;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic
{
    /// <summary>
    /// Controls pausing, pause UI loading, cursor state, and global game-state changes.
    /// </summary>
    /// <remarks>
    /// This class does not read input directly. Call <see cref="TogglePause"/>,
    /// <see cref="PauseGame"/>, or <see cref="ResumeGame"/> from an input reader,
    /// button, UnityEvent, or project-specific input bridge.
    /// </remarks>
    public class PauseManager : ToolkitSingleton<PauseManager>
    {
        [Header("State IDs")]
        [Tooltip("State required before the game can be paused.")]
        [SerializeField] private string gameplayState = "Playing";

        [Tooltip("State assigned while the game is paused.")]
        [SerializeField] private string pausedState = "Paused";

        [Header("Pause UI Scene")]
        [Tooltip("Optional additive scene containing pause menu UI.")]
        [SerializeField] private string pauseSceneName;

        [Tooltip("Whether this manager should load and unload the configured pause UI scene.")]
        [SerializeField] private bool usePauseScene = true;

        [Header("Time")]
        [Tooltip("Whether pausing should set Time.timeScale to zero.")]
        [SerializeField] private bool pauseTimeScale = true;

        [Header("Cursor")]
        [Tooltip("Whether the cursor should be unlocked and visible while paused.")]
        [SerializeField] private bool manageCursor = true;

        [Tooltip("Cursor lock mode restored when gameplay resumes.")]
        [SerializeField]
        private CursorLockMode gameplayCursorLockMode =
            CursorLockMode.Locked;

        [Tooltip("Whether the cursor should be visible while gameplay is active.")]
        [SerializeField] private bool cursorVisibleDuringGameplay;

        /// <summary>
        /// Gets whether this manager currently considers the game paused.
        /// </summary>
        public bool IsPaused =>
            GameStateManager.Instance != null &&
            GameStateManager.Instance.IsInState(pausedState);

        /// <summary>
        /// Prevents duplicate pause-scene load or unload operations.
        /// </summary>
        private bool isChangingPauseScene;

        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
            {
                return;
            }

            RestoreGameplayCursor();
        }

        /// <summary>
        /// Toggles between gameplay and paused states.
        /// </summary>
        public void TogglePause()
        {
            if (IsPaused)
            {
                ResumeGame();
                return;
            }

            PauseGame();
        }

        /// <summary>
        /// Pauses the game when the current state allows pausing.
        /// </summary>
        public void PauseGame()
        {
            if (GameStateManager.Instance == null)
            {
                GameLogger.Warning(
                    "PauseGame",
                    this,
                    $"{nameof(PauseManager)} cannot pause because no {nameof(GameStateManager)} exists."
                );
                return;
            }

            if (!GameStateManager.Instance.IsInState(gameplayState))
            {
                return;
            }

            GameStateManager.Instance.SetState(pausedState);

            if (pauseTimeScale)
            {
                Time.timeScale = 0f;
            }

            ApplyPausedCursor();

            if (usePauseScene && !string.IsNullOrWhiteSpace(pauseSceneName))
            {
                StartCoroutine(LoadPauseSceneRoutine());
            }
        }

        /// <summary>
        /// Resumes gameplay from the paused state.
        /// </summary>
        public void ResumeGame()
        {
            if (GameStateManager.Instance == null || !IsPaused)
            {
                return;
            }

            if (pauseTimeScale)
            {
                Time.timeScale = 1f;
            }

            GameStateManager.Instance.SetState(gameplayState);

            RestoreGameplayCursor();

            if (usePauseScene && !string.IsNullOrWhiteSpace(pauseSceneName))
            {
                StartCoroutine(UnloadPauseSceneRoutine());
            }
        }

        /// <summary>
        /// Forces normal time flow and gameplay cursor behavior.
        /// Useful for title-menu transitions or fail-safe cleanup.
        /// </summary>
        public void ForceResumeTime()
        {
            Time.timeScale = 1f;
            RestoreGameplayCursor();
        }

        private IEnumerator LoadPauseSceneRoutine()
        {
            if (isChangingPauseScene || IsSceneLoaded(pauseSceneName))
            {
                yield break;
            }

            isChangingPauseScene = true;

            AsyncOperation operation = SceneManager.LoadSceneAsync(
                pauseSceneName,
                LoadSceneMode.Additive
            );

            if (operation != null)
            {
                yield return operation;
            }

            isChangingPauseScene = false;
        }

        private IEnumerator UnloadPauseSceneRoutine()
        {
            if (isChangingPauseScene || !IsSceneLoaded(pauseSceneName))
            {
                yield break;
            }

            isChangingPauseScene = true;

            AsyncOperation operation = SceneManager.UnloadSceneAsync(
                pauseSceneName
            );

            if (operation != null)
            {
                yield return operation;
            }

            isChangingPauseScene = false;
        }

        private void ApplyPausedCursor()
        {
            if (!manageCursor)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestoreGameplayCursor()
        {
            if (!manageCursor)
            {
                return;
            }

            Cursor.lockState = gameplayCursorLockMode;
            Cursor.visible = cursorVisibleDuringGameplay;
        }

        private static bool IsSceneLoaded(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);

            return scene.IsValid() && scene.isLoaded;
        }
    }
}