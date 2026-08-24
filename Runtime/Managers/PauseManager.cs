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
        [Header("Services")]
        [Tooltip("Authoritative game-state service in the same persistent System scene.")]
        [SerializeField] private GameStateManager gameStateManager;

        [Header("State IDs")]
        [Tooltip("State required before the game can be paused.")]
        [GameStateId]
        [SerializeField] private string gameplayState = "Playing";

        [Tooltip("State assigned while the game is paused.")]
        [GameStateId]
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
            gameStateManager != null && gameStateManager.IsInState(pausedState);

        private Coroutine pauseSceneRoutine;

        /// <summary>Gets whether the additive pause scene is currently reconciling.</summary>
        public bool IsChangingPauseScene => pauseSceneRoutine != null;

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
            if (gameStateManager == null)
            {
                GameLogger.Warning(
                    "PauseGame",
                    this,
                    $"{nameof(PauseManager)} cannot pause because no {nameof(GameStateManager)} exists."
                );
                return;
            }

            if (!gameStateManager.IsInState(gameplayState))
            {
                return;
            }

            gameStateManager.SetState(pausedState);

            if (pauseTimeScale)
            {
                Time.timeScale = 0f;
            }

            ApplyPausedCursor();

            if (usePauseScene && !string.IsNullOrWhiteSpace(pauseSceneName))
            {
                ReconcilePauseScene();
            }
        }

        /// <summary>
        /// Resumes gameplay from the paused state.
        /// </summary>
        public void ResumeGame()
        {
            if (gameStateManager == null || !IsPaused)
            {
                return;
            }

            if (pauseTimeScale)
            {
                Time.timeScale = 1f;
            }

            gameStateManager.SetState(gameplayState);

            RestoreGameplayCursor();

            if (usePauseScene && !string.IsNullOrWhiteSpace(pauseSceneName))
            {
                ReconcilePauseScene();
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

        private void ReconcilePauseScene()
        {
            if (pauseSceneRoutine != null ||
                IsPaused == IsSceneLoaded(pauseSceneName))
            {
                return;
            }

            pauseSceneRoutine = StartCoroutine(ReconcilePauseSceneRoutine());
        }

        private IEnumerator ReconcilePauseSceneRoutine()
        {
            try
            {
                while (IsPaused != IsSceneLoaded(pauseSceneName))
                {
                    AsyncOperation operation = IsPaused
                        ? SceneManager.LoadSceneAsync(
                            pauseSceneName,
                            LoadSceneMode.Additive)
                        : SceneManager.UnloadSceneAsync(pauseSceneName);

                    if (operation == null)
                    {
                        yield break;
                    }

                    yield return operation;
                }
            }
            finally
            {
                pauseSceneRoutine = null;
            }
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
