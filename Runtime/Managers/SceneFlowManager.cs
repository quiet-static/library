using System;
using System.Collections;
using System.Collections.Generic;
using QuietStatic.Toolkit.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic
{
    /// <summary>
    /// Handles generic additive scene loading, unloading, and scene transitions.
    /// </summary>
    /// <remarks>
    /// This manager does not know about gameplay scenes, cutscenes, title screens,
    /// dialogue, player spawning, game states, or project-specific scene enums.
    ///
    /// It only:
    /// - Loads configured persistent scenes
    /// - Loads scenes additively
    /// - Transitions to a target scene
    /// - Unloads non-persistent scenes during transitions
    /// - Sets the active Unity scene
    /// - Raises events when transitions begin and end
    /// </remarks>
    public class SceneFlowManager : ToolkitSingleton<SceneFlowManager>
    {
        /// <summary>
        /// Raised when a transition begins.
        /// The string parameter is the target scene name.
        /// </summary>
        public static event Action<string> OnTransitionStarted;

        /// <summary>
        /// Raised after a transition completes.
        /// The string parameter is the newly active scene name.
        /// </summary>
        public static event Action<string> OnTransitionCompleted;

        /// <summary>
        /// Raised when an additive scene finishes loading.
        /// </summary>
        public static event Action<string> OnSceneLoaded;

        /// <summary>
        /// Raised when a loaded scene finishes unloading.
        /// </summary>
        public static event Action<string> OnSceneUnloaded;

        [Header("Persistent Scenes")]
        [Tooltip("Scenes that should remain loaded during normal transitions, such as Systems, UI, Audio, or Player scenes.")]
        [SerializeField] private string[] persistentSceneNames;

        [Header("Startup")]
        [Tooltip("Optional scene loaded automatically after persistent scenes are loaded.")]
        [SerializeField] private string startupScene;

        [Tooltip("Whether startupScene should automatically load when this manager awakens.")]
        [SerializeField] private bool loadStartupSceneOnAwake = true;

        /// <summary>
        /// Tracks scenes that are currently loading so duplicate requests can wait
        /// instead of starting multiple additive loads.
        /// </summary>
        private readonly HashSet<string> scenesCurrentlyLoading = new();

        /// <summary>
        /// Prevents multiple scene transitions from running simultaneously.
        /// </summary>
        private bool isTransitioning;

        /// <summary>
        /// Gets whether a full scene transition is currently running.
        /// </summary>
        public bool IsTransitioning => isTransitioning;

        protected override void Awake()
        {
            base.Awake();

            LoadPersistentScenes();

            if (loadStartupSceneOnAwake && !string.IsNullOrWhiteSpace(startupScene))
            {
                TransitionToScene(startupScene);
            }
        }

        /// <summary>
        /// Loads every configured persistent scene additively if needed.
        /// </summary>
        public void LoadPersistentScenes()
        {
            if (persistentSceneNames == null)
            {
                return;
            }

            foreach (string sceneName in persistentSceneNames)
            {
                LoadSceneAdditiveIfNeeded(sceneName);
            }
        }

        /// <summary>
        /// Starts a transition to a target scene.
        /// </summary>
        /// <remarks>
        /// The target scene becomes active after loading. All currently loaded
        /// non-persistent scenes are unloaded afterward.
        /// </remarks>
        /// <param name="targetSceneName">Scene name configured in Build Settings.</param>
        public void TransitionToScene(string targetSceneName)
        {
            StartCoroutine(
                TransitionToSceneRoutine(
                    targetSceneName,
                    unloadOtherScenes: true
                )
            );
        }

        /// <summary>
        /// Starts a transition to a target scene while optionally keeping other
        /// non-persistent scenes loaded.
        /// </summary>
        /// <param name="targetSceneName">Scene name configured in Build Settings.</param>
        /// <param name="unloadOtherScenes">
        /// Whether non-persistent scenes should be unloaded after loading the target.
        /// </param>
        public void TransitionToScene(
            string targetSceneName,
            bool unloadOtherScenes
        )
        {
            StartCoroutine(
                TransitionToSceneRoutine(
                    targetSceneName,
                    unloadOtherScenes
                )
            );
        }

        /// <summary>
        /// Loads one scene additively without unloading other scenes.
        /// </summary>
        /// <param name="sceneName">Scene name configured in Build Settings.</param>
        public void LoadSceneAdditive(string sceneName)
        {
            StartCoroutine(LoadSceneAdditiveIfNeededRoutine(sceneName));
        }

        /// <summary>
        /// Unloads a loaded scene if it is not configured as persistent.
        /// </summary>
        /// <param name="sceneName">Scene to unload.</param>
        public void UnloadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            if (IsPersistentScene(sceneName))
            {
                GameLogger.Warning(
                    "UnloadScene",
                    this,
                    $"{nameof(SceneFlowManager)} will not unload persistent scene '{sceneName}'."
                );
                return;
            }

            StartCoroutine(UnloadSceneIfLoadedRoutine(sceneName));
        }

        /// <summary>
        /// Sets a loaded scene as Unity's active scene.
        /// </summary>
        /// <param name="sceneName">Loaded scene to activate.</param>
        /// <returns>True if the active scene was changed.</returns>
        public bool SetActiveScene(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);

            if (!scene.IsValid() || !scene.isLoaded)
            {
                GameLogger.Warning(
                    "SetActiveScene",
                    this,
                    $"{nameof(SceneFlowManager)} could not set '{sceneName}' as active because it is not loaded."
                );
                return false;
            }

            return SceneManager.SetActiveScene(scene);
        }

        /// <summary>
        /// Returns whether a scene is currently loaded.
        /// </summary>
        public bool IsSceneLoaded(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        /// <summary>
        /// Pauses Unity time.
        /// </summary>
        public void PauseTime()
        {
            Time.timeScale = 0f;
        }

        /// <summary>
        /// Restores Unity time to normal speed.
        /// </summary>
        public void ResumeTime()
        {
            Time.timeScale = 1f;
        }

        /// <summary>
        /// Loads the target scene, sets it active, and optionally unloads all
        /// non-persistent scenes afterward.
        /// </summary>
        private IEnumerator TransitionToSceneRoutine(
            string targetSceneName,
            bool unloadOtherScenes
        )
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                GameLogger.Warning(
                    "TransitionToSceneRoutine",
                    this,
                    $"{nameof(SceneFlowManager)} cannot transition to an empty scene name."
                );
                yield break;
            }

            if (isTransitioning)
            {
                GameLogger.Warning(
                    "TransitionToSceneRoutine",
                    this,
                    $"{nameof(SceneFlowManager)} is already transitioning. Ignoring request for '{targetSceneName}'."
                );
                yield break;
            }

            isTransitioning = true;
            OnTransitionStarted?.Invoke(targetSceneName);

            yield return LoadSceneAdditiveIfNeededRoutine(targetSceneName);

            if (!SetActiveScene(targetSceneName))
            {
                isTransitioning = false;
                yield break;
            }

            if (unloadOtherScenes)
            {
                yield return UnloadScenesExceptRoutine(targetSceneName);
            }

            isTransitioning = false;
            OnTransitionCompleted?.Invoke(targetSceneName);
        }

        /// <summary>
        /// Loads a scene additively only when it is not already loaded.
        /// </summary>
        private IEnumerator LoadSceneAdditiveIfNeededRoutine(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                yield break;
            }

            if (IsSceneLoaded(sceneName))
            {
                yield break;
            }

            if (scenesCurrentlyLoading.Contains(sceneName))
            {
                while (scenesCurrentlyLoading.Contains(sceneName))
                {
                    yield return null;
                }

                yield break;
            }

            scenesCurrentlyLoading.Add(sceneName);

            AsyncOperation operation = SceneManager.LoadSceneAsync(
                sceneName,
                LoadSceneMode.Additive
            );

            if (operation == null)
            {
                scenesCurrentlyLoading.Remove(sceneName);

                GameLogger.Warning(
                    "LoadSceneAdditiveIfNeededRoutine",
                    this,
                    $"{nameof(SceneFlowManager)} could not begin loading scene '{sceneName}'."
                );
                yield break;
            }

            yield return operation;

            scenesCurrentlyLoading.Remove(sceneName);

            OnSceneLoaded?.Invoke(sceneName);
        }

        /// <summary>
        /// Loads a scene additively without waiting for it to finish.
        /// </summary>
        private void LoadSceneAdditiveIfNeeded(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName) ||
                IsSceneLoaded(sceneName) ||
                scenesCurrentlyLoading.Contains(sceneName))
            {
                return;
            }

            StartCoroutine(LoadSceneAdditiveIfNeededRoutine(sceneName));
        }

        /// <summary>
        /// Unloads every loaded scene except persistent scenes and the target scene.
        /// </summary>
        private IEnumerator UnloadScenesExceptRoutine(string targetSceneName)
        {
            List<Scene> scenesToUnload = new();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loadedScene = SceneManager.GetSceneAt(i);

                if (!loadedScene.isLoaded ||
                    loadedScene.name == targetSceneName ||
                    IsPersistentScene(loadedScene.name))
                {
                    continue;
                }

                scenesToUnload.Add(loadedScene);
            }

            foreach (Scene scene in scenesToUnload)
            {
                yield return UnloadSceneIfLoadedRoutine(scene.name);
            }
        }

        /// <summary>
        /// Unloads a scene when it is currently loaded.
        /// </summary>
        private IEnumerator UnloadSceneIfLoadedRoutine(string sceneName)
        {
            if (!IsSceneLoaded(sceneName))
            {
                yield break;
            }

            AsyncOperation operation = SceneManager.UnloadSceneAsync(sceneName);

            if (operation == null)
            {
                yield break;
            }

            yield return operation;

            OnSceneUnloaded?.Invoke(sceneName);
        }

        /// <summary>
        /// Returns whether a scene is configured to survive normal transitions.
        /// </summary>
        private bool IsPersistentScene(string sceneName)
        {
            if (persistentSceneNames == null)
            {
                return false;
            }

            foreach (string persistentSceneName in persistentSceneNames)
            {
                if (persistentSceneName == sceneName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}