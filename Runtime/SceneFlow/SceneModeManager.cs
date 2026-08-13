using System;
using QuietStatic.Toolkit.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.SceneFlow
{
    /// <summary>Publishes the mode declared by the active content scene.</summary>
    /// <remarks>
    /// This manager belongs in a persistent Systems scene. It changes the global game state
    /// when active content changes; InputModeManager then selects the appropriate controls.
    /// </remarks>
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Quiet Static Toolkit/Scene Flow/Scene Mode Manager")]
    public sealed class SceneModeManager : MonoBehaviour
    {
        /// <summary>Raised when the active content scene declares a different mode.</summary>
        public static event Action<SceneMode> OnSceneModeChanged;

        /// <summary>Gets the currently resolved content-scene mode.</summary>
        public static SceneMode CurrentMode { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CurrentMode = SceneMode.Unspecified;
            OnSceneModeChanged = null;
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        private void Start()
        {
            ApplySceneMode(SceneManager.GetActiveScene());
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene newScene)
        {
            ApplySceneMode(newScene);
        }

        private static void ApplySceneMode(Scene scene)
        {
            SceneModeDefinition definition = FindDefinition(scene);
            SceneMode newMode = definition != null
                ? definition.Mode
                : SceneMode.Unspecified;

            bool modeChanged = CurrentMode != newMode;
            CurrentMode = newMode;

            if (definition != null &&
                !string.IsNullOrWhiteSpace(definition.InitialGameState) &&
                GameStateManager.Instance != null)
            {
                GameStateManager.Instance.SetState(definition.InitialGameState);
            }

            if (modeChanged)
            {
                OnSceneModeChanged?.Invoke(CurrentMode);
            }
        }

        /// <summary>Finds the mode definition belonging to one loaded scene.</summary>
        public static SceneModeDefinition FindDefinition(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                SceneModeDefinition definition =
                    root.GetComponentInChildren<SceneModeDefinition>(true);
                if (definition != null)
                {
                    return definition;
                }
            }

            return null;
        }
    }
}
