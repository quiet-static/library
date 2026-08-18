#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using QuietStatic;
using QuietStatic.Toolkit.SceneFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace QuietStatic.Toolkit.DebugTools
{
    /// <summary>Session keys shared by the edit-mode guard and its Play Mode bootstrap scene.</summary>
    /// <remarks>
    /// Unity's editor-only <see cref="SessionState"/> survives the Edit Mode to Play Mode switch
    /// without serializing test-only data into a scene or asset. The companion guard writes these
    /// values immediately before Play Mode; <see cref="DevelopmentPlayModeBootstrapper"/> then
    /// reads them from the temporary bootstrap scene.
    /// </remarks>
    public static class DevelopmentPlayModeSession
    {
        /// <summary>
        /// Key for the project-relative path of the saved scene selected for the next isolated
        /// Play Mode session.
        /// </summary>
        public const string TargetScenePathKey =
            "Stolen.DevelopmentPlayMode.TargetScenePath";

        /// <summary>
        /// Key for whether the selected scene needs the project's persistent support scenes.
        /// </summary>
        public const string LoadPersistentScenesKey =
            "Stolen.DevelopmentPlayMode.LoadPersistentScenes";
    }

    /// <summary>
    /// Editor-only startup scene that loads one requested test scene plus the configured
    /// persistent support-scene list, without initializing unrelated open editor scenes.
    /// </summary>
    /// <remarks>
    /// This component is compiled only in the Unity Editor, and its scene is intentionally absent
    /// from Build Settings. Standalone players continue to start through SceneBootstrapper. At
    /// startup it optionally loads every support scene additively, configures the scene-flow
    /// manager, loads and activates the requested test scene, and finally unloads its own temporary
    /// bootstrap scene.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DevelopmentPlayModeBootstrapper : MonoBehaviour
    {
        [Tooltip("Authoritative profile whose persistent scenes support direct content-scene tests.")]
        [SerializeField] private SceneBootstrapProfile profile;

        [Tooltip("Give persistent managers one frame to initialize before loading the test scene.")]
        [SerializeField] private bool waitOneFrameAfterPersistentScenes = true;

        // Coroutines share this latch so the first failure can stop every nested load operation.
        private bool failed;

        /// <summary>
        /// Builds the isolated Play Mode scene set prepared by the edit-mode guard.
        /// </summary>
        /// <remarks>
        /// Unity treats an <c>IEnumerator Start</c> method as a coroutine. Each yielded scene load
        /// therefore completes in order before activation and bootstrap-scene cleanup continue.
        /// </remarks>
        private IEnumerator Start()
        {
            string targetScenePath = SessionState.GetString(
                DevelopmentPlayModeSession.TargetScenePathKey,
                string.Empty);

            if (!IsSceneAssetPath(targetScenePath))
            {
                Fail("No valid saved target scene was prepared for isolated Play Mode.");
                yield break;
            }

            bool loadPersistentScenes = SessionState.GetBool(
                DevelopmentPlayModeSession.LoadPersistentScenesKey,
                false);

            if (loadPersistentScenes)
            {
                yield return LoadAndConfigurePersistentScenes();
                if (failed)
                {
                    yield break;
                }
            }

            yield return LoadSceneIfNeeded(targetScenePath);
            if (failed)
            {
                yield break;
            }

            Scene targetScene = SceneManager.GetSceneByPath(targetScenePath);
            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                Fail($"The requested test scene did not finish loading: {targetScenePath}");
                yield break;
            }

            if (!SceneManager.SetActiveScene(targetScene))
            {
                Fail($"Unity could not make '{targetScene.name}' the active test scene.");
                yield break;
            }

            // Unload this temporary host last. Destroying the component earlier would stop Start
            // before the requested scene became the authoritative active scene.
            Scene bootstrapScene = gameObject.scene;
            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(bootstrapScene);
            if (unloadOperation != null)
            {
                yield return unloadOperation;
            }
        }

        /// <summary>
        /// Loads the support scenes declared by <see cref="profile"/> and configures scene flow to
        /// treat them as persistent.
        /// </summary>
        private IEnumerator LoadAndConfigurePersistentScenes()
        {
            if (profile == null)
            {
                Fail("The development bootstrap scene has no SceneBootstrapProfile.");
                yield break;
            }

            foreach (string sceneName in profile.PersistentSceneNames)
            {
                // Profiles store portable scene names, while the editor loading API needs an asset path.
                string scenePath = ResolveScenePath(sceneName);
                if (string.IsNullOrEmpty(scenePath))
                {
                    Fail($"Could not resolve persistent scene '{sceneName}' to a scene asset.");
                    yield break;
                }

                yield return LoadSceneIfNeeded(scenePath);
                if (failed)
                {
                    yield break;
                }
            }

            if (waitOneFrameAfterPersistentScenes)
            {
                // Awake/OnEnable run during the load; this extra frame allows Start-based manager
                // initialization to finish before the target content scene begins loading.
                yield return null;
            }

            SceneFlowManager manager = SceneFlowManager.Instance;
            if (manager == null)
            {
                // Direct test startup may occur before a manager has assigned its singleton.
                manager = FindAnyObjectByType<SceneFlowManager>();
            }

            if (manager == null)
            {
                Fail("No SceneFlowManager was found after persistent scenes loaded.");
                yield break;
            }

            manager.ConfigurePersistentScenes(profile.PersistentSceneNames);
        }

        /// <summary>Loads one scene additively unless it is already present and loaded.</summary>
        /// <param name="scenePath">Project-relative path to a Unity scene asset.</param>
        private IEnumerator LoadSceneIfNeeded(string scenePath)
        {
            Scene existingScene = SceneManager.GetSceneByPath(scenePath);
            if (existingScene.IsValid() && existingScene.isLoaded)
            {
                yield break;
            }

            Scene loadingScene = BeginSceneLoad(scenePath);
            if (!loadingScene.IsValid())
            {
                if (!failed)
                {
                    Fail($"Unity did not start loading scene '{scenePath}'.");
                }

                yield break;
            }

            // LoadSceneInPlayMode returns the Scene handle immediately; poll its live loaded state
            // instead of starting a second load or assuming the scene is ready this frame.
            while (!loadingScene.isLoaded && !failed)
            {
                yield return null;
            }
        }

        /// <summary>Starts an additive editor Play Mode scene load and converts exceptions to failure state.</summary>
        /// <param name="scenePath">Project-relative path to a Unity scene asset.</param>
        /// <returns>The scene handle returned by Unity, or an invalid default handle after failure.</returns>
        private Scene BeginSceneLoad(string scenePath)
        {
            try
            {
                return EditorSceneManager.LoadSceneInPlayMode(
                    scenePath,
                    new LoadSceneParameters(LoadSceneMode.Additive));
            }
            catch (Exception exception)
            {
                Fail($"Could not load scene '{scenePath}': {exception.Message}");
                return default;
            }
        }

        /// <summary>Resolves a profile scene name to its project-relative asset path.</summary>
        /// <param name="sceneName">Scene name without the <c>.unity</c> extension.</param>
        /// <returns>The matching scene path, or an empty string if no exact-name match exists.</returns>
        /// <remarks>
        /// Build Settings are searched first because they are deterministic project configuration.
        /// The Asset Database fallback also supports editor-only persistent scenes.
        /// </remarks>
        private static string ResolveScenePath(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return string.Empty;
            }

            foreach (EditorBuildSettingsScene configuredScene in EditorBuildSettings.scenes)
            {
                if (string.Equals(
                        Path.GetFileNameWithoutExtension(configuredScene.path),
                        sceneName,
                        StringComparison.Ordinal))
                {
                    return configuredScene.path;
                }
            }

            // FindAssets is a fuzzy search, so retain only an exact filename match.
            return AssetDatabase.FindAssets($"{sceneName} t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => string.Equals(
                    Path.GetFileNameWithoutExtension(path),
                    sceneName,
                    StringComparison.Ordinal)) ?? string.Empty;
        }

        /// <summary>Checks whether a prepared value has the basic shape of a Unity scene path.</summary>
        /// <remarks>
        /// Existence is intentionally left to Unity's loading API, whose error is reported by the
        /// normal failure path. This check only rejects missing values and non-scene extensions.
        /// </remarks>
        private static bool IsSceneAssetPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Reports the first bootstrap failure and schedules a clean exit from Play Mode.</summary>
        /// <param name="reason">Human-readable explanation included in the Unity Console error.</param>
        private void Fail(string reason)
        {
            if (failed)
            {
                return;
            }

            failed = true;
            enabled = false;
            GameLogger.Error($"Development Play Mode isolation failed: {reason}", this);
            // Delay the editor state change until Unity finishes the current lifecycle/load callback.
            EditorApplication.delayCall += EditorApplication.ExitPlaymode;
        }
    }
}
#endif
