using System.Collections;
using QuietStatic.Toolkit.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.SceneFlow
{
    /// <summary>
    /// Provides a reusable scene-loading service for single-scene transitions,
    /// additive scene loading, and additive scene unloading.
    /// </summary>
    /// <remarks>
    /// This service is intentionally generic and does not make assumptions about
    /// project-specific scene names, player spawning, UI setup, or game state.
    ///
    /// It is useful for toolkit-style projects where multiple systems may need to
    /// request scene loads without duplicating safety checks.
    ///
    /// Duplicate additive loads are prevented by checking whether the requested
    /// scene is already loaded before attempting to load it again.
    /// </remarks>
    public class SceneLoadService : ToolkitSingleton<SceneLoadService>
    {
        [Header("Scene Load Events")]
        [Tooltip("Invoked when a single or additive scene load begins.")]
        [SerializeField] private UnityEvent onLoadStarted;

        [Tooltip("Invoked when a single or additive scene load finishes.")]
        [SerializeField] private UnityEvent onLoadFinished;

        /// <summary>
        /// Gets whether this service is currently running a single or additive scene load.
        /// </summary>
        /// <remarks>
        /// While this value is true, additional single or additive load requests are ignored.
        /// Unload requests are not blocked by this value in the current implementation.
        /// </remarks>
        public bool IsLoading { get; private set; }

        /// <summary>
        /// Checks whether a scene with the given name is currently loaded.
        /// </summary>
        /// <param name="sceneName">
        /// Name of the scene to check. Empty or whitespace scene names return false.
        /// </param>
        /// <returns>
        /// True if the scene exists in the loaded scene list and is currently loaded;
        /// otherwise, false.
        /// </returns>
        public bool IsSceneLoaded(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            return SceneManager.GetSceneByName(sceneName).isLoaded;
        }

        /// <summary>
        /// Starts loading a scene in <see cref="LoadSceneMode.Single"/> mode.
        /// </summary>
        /// <param name="sceneName">
        /// Name of the scene to load. Empty or whitespace scene names are ignored.
        /// </param>
        /// <remarks>
        /// Single-scene loading replaces the currently loaded scenes with the requested scene.
        /// Use <see cref="LoadAdditive"/> when the scene should be layered on top of the
        /// current scene setup instead.
        /// </remarks>
        public void LoadSingle(string sceneName)
        {
            StartCoroutine(LoadSingleRoutine(sceneName));
        }

        /// <summary>
        /// Starts loading a scene additively.
        /// </summary>
        /// <param name="sceneName">
        /// Name of the scene to load. Empty, whitespace, currently loading, or already-loaded
        /// scenes are ignored by the coroutine.
        /// </param>
        /// <remarks>
        /// This is useful for persistent Systems, UI, Player, lighting, or gameplay-area
        /// scenes that should coexist in the active scene stack.
        /// </remarks>
        public void LoadAdditive(string sceneName)
        {
            StartCoroutine(LoadAdditiveRoutine(sceneName));
        }

        /// <summary>
        /// Starts unloading a currently loaded scene.
        /// </summary>
        /// <param name="sceneName">
        /// Name of the scene to unload. Empty, whitespace, or unloaded scenes are ignored
        /// by the coroutine.
        /// </param>
        public void Unload(string sceneName)
        {
            StartCoroutine(UnloadRoutine(sceneName));
        }

        /// <summary>
        /// Loads a scene in <see cref="LoadSceneMode.Single"/> mode.
        /// </summary>
        /// <param name="sceneName">
        /// Name of the scene to load.
        /// </param>
        /// <returns>
        /// Coroutine enumerator that yields until Unity finishes the async scene load.
        /// </returns>
        public IEnumerator LoadSingleRoutine(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || IsLoading)
            {
                yield break;
            }

            IsLoading = true;
            onLoadStarted?.Invoke();

            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            onLoadFinished?.Invoke();
            IsLoading = false;
        }

        /// <summary>
        /// Loads a scene additively if it is not already loaded.
        /// </summary>
        /// <param name="sceneName">
        /// Name of the scene to load.
        /// </param>
        /// <returns>
        /// Coroutine enumerator that yields until Unity finishes the async additive scene load.
        /// </returns>
        public IEnumerator LoadAdditiveRoutine(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || IsLoading || IsSceneLoaded(sceneName))
            {
                yield break;
            }

            IsLoading = true;
            onLoadStarted?.Invoke();

            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            onLoadFinished?.Invoke();
            IsLoading = false;
        }

        /// <summary>
        /// Unloads a currently loaded scene by name.
        /// </summary>
        /// <param name="sceneName">
        /// Name of the loaded scene to unload.
        /// </param>
        /// <returns>
        /// Coroutine enumerator that yields until Unity finishes the async unload operation.
        /// </returns>
        public IEnumerator UnloadRoutine(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName) || !IsSceneLoaded(sceneName))
            {
                yield break;
            }

            yield return SceneManager.UnloadSceneAsync(sceneName);
        }
    }
}
