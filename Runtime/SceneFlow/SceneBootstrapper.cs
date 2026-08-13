using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.SceneFlow
{
    /// <summary>
    /// Loads a bootstrap profile and hands initial content ownership to a persistent
    /// <see cref="SceneFlowManager"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Quiet Static Toolkit/Scene Flow/Scene Bootstrapper")]
    public sealed class SceneBootstrapper : MonoBehaviour
    {
        [Header("Profile")]
        [Tooltip("Scene-lifetime configuration used for this startup sequence.")]
        [SerializeField] private SceneBootstrapProfile profile;

        [Tooltip("Begin the bootstrap sequence automatically on Start.")]
        [SerializeField] private bool runOnStart = true;

        [Header("Initialization")]
        [Tooltip("Wait one frame after persistent scenes load so their Awake and OnEnable work can settle before setup callbacks run.")]
        [SerializeField] private bool waitOneFrameAfterPersistentScenes = true;

        [Header("Events")]
        [Tooltip("Invoked immediately before persistent scene loading begins.")]
        [SerializeField] private UnityEvent onBootstrapStarted;

        [Tooltip("Invoked after all persistent scenes initialize and before initial content is requested. Use this for general project setup hooks.")]
        [SerializeField] private UnityEvent onPersistentScenesReady;

        [Tooltip("Invoked when the initial transition is successfully handed to SceneFlowManager.")]
        [SerializeField] private UnityEvent onInitialTransitionRequested;

        [Tooltip("Invoked when the bootstrap cannot continue.")]
        [SerializeField] private UnityEvent onBootstrapFailed;

        /// <summary>Whether this component is currently loading its profile.</summary>
        public bool IsBootstrapping { get; private set; }

        /// <summary>The last failure description, or an empty string.</summary>
        public string FailureReason { get; private set; } = string.Empty;

        private IEnumerator Start()
        {
            if (runOnStart)
            {
                yield return BootstrapRoutine();
            }
        }

        /// <summary>Starts the configured bootstrap sequence from a UnityEvent.</summary>
        public void Bootstrap()
        {
            if (!IsBootstrapping)
            {
                StartCoroutine(BootstrapRoutine());
            }
        }

        /// <summary>Loads persistent scenes and requests the initial transition.</summary>
        public IEnumerator BootstrapRoutine()
        {
            if (IsBootstrapping)
            {
                yield break;
            }

            if (profile == null || !profile.IsValid)
            {
                Fail("A valid bootstrap profile with an initial scene is required.");
                yield break;
            }

            IsBootstrapping = true;
            FailureReason = string.Empty;
            onBootstrapStarted?.Invoke();

            foreach (string sceneName in profile.PersistentSceneNames)
            {
                if (IsSceneLoaded(sceneName))
                {
                    continue;
                }

                AsyncOperation operation = null;
                try
                {
                    operation = SceneManager.LoadSceneAsync(
                        sceneName,
                        LoadSceneMode.Additive);
                }
                catch (Exception exception)
                {
                    Fail($"Could not load persistent scene '{sceneName}': {exception.Message}");
                    yield break;
                }

                if (operation == null)
                {
                    Fail($"Unity did not start loading persistent scene '{sceneName}'.");
                    yield break;
                }

                yield return operation;
            }

            if (waitOneFrameAfterPersistentScenes)
            {
                yield return null;
            }

            SceneFlowManager manager = SceneFlowManager.Instance;
            if (manager == null)
            {
                manager = FindAnyObjectByType<SceneFlowManager>();
            }

            if (manager == null)
            {
                Fail("No SceneFlowManager was found after persistent scenes loaded.");
                yield break;
            }

            manager.ConfigurePersistentScenes(profile.PersistentSceneNames);
            onPersistentScenesReady?.Invoke();

            // The manager owns this coroutine. The bootstrap scene can therefore be
            // unloaded safely by the transition it just requested.
            manager.TransitionToScene(profile.CreateInitialTransitionRequest());
            onInitialTransitionRequested?.Invoke();
            IsBootstrapping = false;
        }

        private static bool IsSceneLoaded(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        private void Fail(string reason)
        {
            FailureReason = reason ?? "Bootstrap failed.";
            IsBootstrapping = false;
            GameLogger.Warning(nameof(SceneBootstrapper), this, FailureReason);
            onBootstrapFailed?.Invoke();
        }
    }
}
