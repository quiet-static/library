using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.SceneFlow;
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
    /// - Delivers optional entry conditions to the destination scene
    /// - Raises events when transitions begin and end
    /// </remarks>
    [DefaultExecutionOrder(-1000)]
    public class SceneFlowManager : ToolkitSingleton<SceneFlowManager>
    {
        /// <summary>
        /// Raised when a transition begins.
        /// The string parameter is the target scene name.
        /// </summary>
        public event Action<string> TransitionStarted;

        /// <summary>
        /// Raised after a transition completes.
        /// The string parameter is the newly active scene name.
        /// </summary>
        public event Action<string> TransitionCompleted;

        /// <summary>Raised after every request reaches a terminal success or failure.</summary>
        public event Action<SceneTransitionResult> TransitionFinished;

        /// <summary>Raised after a request cannot complete.</summary>
        public event Action<SceneTransitionResult> TransitionFailed;

        /// <summary>
        /// Raised when an additive scene finishes loading.
        /// </summary>
        public event Action<string> SceneLoaded;

        /// <summary>
        /// Raised when a loaded scene finishes unloading.
        /// </summary>
        public event Action<string> SceneUnloaded;

        [Header("Persistent Scenes")]
        [Tooltip("Scenes that should remain loaded during normal transitions, such as Systems, UI, Audio, or Player scenes.")]
        [SerializeField] private string[] persistentSceneNames;

        [Header("Startup")]
        [Tooltip("Optional scene loaded automatically after persistent scenes are loaded.")]
        [SerializeField] private string startupScene;

        [Tooltip("Whether startupScene should automatically load when this manager awakens.")]
        [SerializeField] private bool loadStartupSceneOnAwake = true;

        [Header("Cross-Scene Commands")]
        [Tooltip("Optional channel through which content scenes request scene-flow operations.")]
        [RequiredCommandChannel(isReceiver: true)]
        [SerializeField] private SceneFlowRequestChannel requestChannel;

        [Header("Transition Fade")]
        [Tooltip("Persistent full-screen fader used around content transitions. If empty, an active fader is discovered when needed.")]
        [SerializeField] private ScreenFader screenFader;

        [Tooltip("Optional cross-scene fade channel. Preferred over the direct fader reference when it has an active handler.")]
        [SerializeField] private ScreenFadeChannel screenFadeChannel;

        [Tooltip("Fade to black before changing the loaded content scene, then fade clear after cleanup.")]
        [SerializeField] private bool fadeDuringTransitions = true;

        [Tooltip("Fade duration used for channel-driven fades.")]
        [Min(0f)]
        [SerializeField] private float transitionFadeDuration = 0.25f;

        [Tooltip("Optional unscaled delay while fully black after scene cleanup and before fading clear.")]
        [Min(0f)]
        [SerializeField] private float blackHoldDuration;

        /// <summary>
        /// Tracks scenes that are currently loading so duplicate requests can wait
        /// instead of starting multiple additive loads.
        /// </summary>
        private readonly HashSet<string> scenesCurrentlyLoading = new();

        /// <summary>
        /// Prevents multiple scene transitions from running simultaneously.
        /// </summary>
        private bool isTransitioning;
        private CrossSceneChannelSubscription<SceneFlowRequestChannel>
            requestSubscription;

        private CrossSceneChannelSubscription<SceneFlowRequestChannel>
            RequestSubscription =>
                requestSubscription ??=
                    new CrossSceneChannelSubscription<SceneFlowRequestChannel>(
                        SubscribeToRequests,
                        UnsubscribeFromRequests);

        /// <summary>
        /// Gets whether a full scene transition is currently running.
        /// </summary>
        public bool IsTransitioning => isTransitioning;

        /// <summary>Most recent terminal transition result, if a request has finished.</summary>
        public SceneTransitionResult? LastTransitionResult { get; private set; }

        /// <summary>Configured scenes that survive normal transitions.</summary>
        public IReadOnlyList<string> PersistentSceneNames =>
            persistentSceneNames ?? Array.Empty<string>();

        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
            {
                return;
            }

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
        /// Replaces persistent-scene policy with normalized scene names supplied by
        /// a bootstrap profile or other general startup authority.
        /// </summary>
        public void ConfigurePersistentScenes(IEnumerable<string> sceneNames)
        {
            if (sceneNames == null)
            {
                persistentSceneNames = Array.Empty<string>();
                return;
            }

            persistentSceneNames = sceneNames
                .Where(sceneName => !string.IsNullOrWhiteSpace(sceneName))
                .Select(sceneName => sceneName.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private void OnEnable()
        {
            RequestSubscription.Bind(requestChannel);
        }

        private void OnDisable()
        {
            RequestSubscription.Unbind();
        }

        /// <summary>Changes the command channel and updates its live subscription.</summary>
        public void SetRequestChannel(SceneFlowRequestChannel value)
        {
            requestChannel = value;
            if (isActiveAndEnabled)
            {
                RequestSubscription.Bind(requestChannel);
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
            TransitionToScene(
                new SceneTransitionRequest(targetSceneName)
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
            TransitionToScene(
                new SceneTransitionRequest(
                    targetSceneName,
                    unloadOtherScenes: unloadOtherScenes
                )
            );
        }

        /// <summary>
        /// Starts a transition described by a reusable request.
        /// </summary>
        /// <param name="request">
        /// Target scene plus optional support scenes and retention rules.
        /// </param>
        public void TransitionToScene(SceneTransitionRequest request)
        {
            StartCoroutine(TransitionToSceneRoutine(
                request,
                requestChannel));
        }

        /// <summary>
        /// Loads one scene additively without unloading other scenes.
        /// </summary>
        /// <param name="sceneName">Scene name configured in Build Settings.</param>
        public void LoadSceneAdditive(string sceneName)
        {
            StartCoroutine(LoadSceneAdditiveRoutine(sceneName));
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

        private void SubscribeToRequests(SceneFlowRequestChannel value)
        {
            value.CommandRequested += HandleCommand;
        }

        private void UnsubscribeFromRequests(SceneFlowRequestChannel value)
        {
            value.CommandRequested -= HandleCommand;
        }

        private void HandleCommand(SceneFlowCommand command)
        {
            switch (command.Type)
            {
                case SceneFlowCommandType.Transition:
                    StartCoroutine(TransitionToSceneRoutine(
                        command.Transition,
                        RequestSubscription.Channel));
                    break;
                case SceneFlowCommandType.LoadAdditive:
                    LoadSceneAdditive(command.SceneName);
                    break;
                case SceneFlowCommandType.Unload:
                    UnloadScene(command.SceneName);
                    break;
                case SceneFlowCommandType.SetActive:
                    SetActiveScene(command.SceneName);
                    break;
            }
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
        /// Loads the target scene, sets it active, and optionally unloads all
        /// non-persistent scenes afterward.
        /// </summary>
        public IEnumerator TransitionToSceneRoutine(
            SceneTransitionRequest request)
        {
            return TransitionToSceneRoutine(request, requestChannel);
        }

        private IEnumerator TransitionToSceneRoutine(
            SceneTransitionRequest request,
            SceneFlowRequestChannel resultChannel)
        {
            string targetSceneName =
                request != null ? request.TargetSceneName : string.Empty;

            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                RejectTransition(
                    SceneTransitionResult.Failed(
                        targetSceneName,
                        SceneTransitionFailure.EmptyTarget,
                        "The target scene name is empty.",
                        request),
                    resultChannel);
                GameLogger.Warning(
                    "TransitionToSceneRoutine",
                    this,
                    $"{nameof(SceneFlowManager)} cannot transition to an empty scene name."
                );
                yield break;
            }

            if (isTransitioning)
            {
                RejectTransition(
                    SceneTransitionResult.Failed(
                        targetSceneName,
                        SceneTransitionFailure.AlreadyTransitioning,
                        $"A transition is already running; '{targetSceneName}' was rejected.",
                        request),
                    resultChannel);
                GameLogger.Warning(
                    "TransitionToSceneRoutine",
                    this,
                    $"{nameof(SceneFlowManager)} is already transitioning. Ignoring request for '{targetSceneName}'."
                );
                yield break;
            }

            isTransitioning = true;
            TransitionStarted?.Invoke(targetSceneName);

            ScreenFader transitionFader = ResolveScreenFader();
            if (CanFade(transitionFader))
            {
                yield return FadeRoutine(
                    ScreenFadeTarget.Black,
                    transitionFader);
            }

            yield return LoadSceneAdditiveRoutine(targetSceneName);

            if (!IsSceneLoaded(targetSceneName))
            {
                yield return FinishFailedTransition(
                    transitionFader,
                    SceneTransitionResult.Failed(
                        targetSceneName,
                        SceneTransitionFailure.LoadFailed,
                        $"Scene '{targetSceneName}' could not be loaded.",
                        request),
                    resultChannel);
                yield break;
            }

            HashSet<string> loadedForRequest =
                new HashSet<string>(StringComparer.Ordinal)
                {
                    targetSceneName,
                };

            foreach (string sceneName
                     in request.AdditionalScenesToLoad)
            {
                if (!loadedForRequest.Add(sceneName))
                {
                    continue;
                }

                yield return LoadSceneAdditiveRoutine(sceneName);

                if (!IsSceneLoaded(sceneName))
                {
                    yield return FinishFailedTransition(
                        transitionFader,
                        SceneTransitionResult.Failed(
                            targetSceneName,
                            SceneTransitionFailure.LoadFailed,
                            $"Required support scene '{sceneName}' could not be loaded.",
                            request),
                        resultChannel);
                    yield break;
                }
            }

            if (!SetActiveScene(targetSceneName))
            {
                yield return FinishFailedTransition(
                    transitionFader,
                    SceneTransitionResult.Failed(
                        targetSceneName,
                        SceneTransitionFailure.ActivationFailed,
                        $"Scene '{targetSceneName}' could not be made active.",
                        request),
                    resultChannel);
                yield break;
            }

            if (request.UnloadOtherScenes)
            {
                yield return UnloadScenesExceptRoutine(
                    request,
                    loadedForRequest);
            }

            ApplyDestinationDefinition(request);

            if (CanFade(transitionFader))
            {
                if (blackHoldDuration > 0f)
                {
                    yield return new WaitForSecondsRealtime(blackHoldDuration);
                }

                yield return FadeRoutine(
                    ScreenFadeTarget.Clear,
                    transitionFader);
            }

            FinishTransition(
                SceneTransitionResult.Success(
                    targetSceneName,
                    request),
                resultChannel);
        }

        /// <summary>
        /// Applies destination-owned entry behavior while the transition remains
        /// covered by its fade.
        /// </summary>
        private static void ApplyDestinationDefinition(
            SceneTransitionRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.ConditionId))
            {
                return;
            }

            Scene destination = SceneManager.GetSceneByName(
                request.TargetSceneName);
            SceneTransitionDefinition definition =
                SceneTransitionDefinition.FindInScene(destination);
            if (definition == null)
            {
                return;
            }

            try
            {
                definition.Apply(request.ConditionId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, definition);
            }
        }

        private ScreenFader ResolveScreenFader()
        {
            if (!fadeDuringTransitions ||
                (screenFadeChannel != null && screenFadeChannel.HasReceiver))
            {
                return null;
            }

            if (screenFader == null)
            {
                screenFader = FindAnyObjectByType<ScreenFader>();
            }

            return screenFader;
        }

        private bool CanFade(ScreenFader directFader)
        {
            return fadeDuringTransitions &&
                   ((screenFadeChannel != null && screenFadeChannel.HasReceiver) ||
                    directFader != null);
        }

        private IEnumerator FadeRoutine(
            ScreenFadeTarget target,
            ScreenFader directFader)
        {
            if (screenFadeChannel != null && screenFadeChannel.HasReceiver)
            {
                yield return screenFadeChannel.FadeRoutine(
                    target,
                    transitionFadeDuration);
                yield break;
            }

            if (directFader == null) yield break;
            directFader.StopActiveFade();
            yield return target == ScreenFadeTarget.Black
                ? directFader.FadeToBlackRoutine()
                : directFader.FadeToClearRoutine();
        }

        private IEnumerator FinishFailedTransition(
            ScreenFader transitionFader,
            SceneTransitionResult result,
            SceneFlowRequestChannel resultChannel)
        {
            if (CanFade(transitionFader))
            {
                yield return FadeRoutine(
                    ScreenFadeTarget.Clear,
                    transitionFader);
            }

            FinishTransition(result, resultChannel);
        }

        private void FinishTransition(
            SceneTransitionResult result,
            SceneFlowRequestChannel resultChannel)
        {
            isTransitioning = false;
            PublishTransitionResult(result, resultChannel);
        }

        private void RejectTransition(
            SceneTransitionResult result,
            SceneFlowRequestChannel resultChannel)
        {
            PublishTransitionResult(result, resultChannel);
        }

        private void PublishTransitionResult(
            SceneTransitionResult result,
            SceneFlowRequestChannel resultChannel)
        {
            LastTransitionResult = result;
            if (result.Succeeded)
            {
                TransitionCompleted?.Invoke(result.Destination);
            }
            else
            {
                TransitionFailed?.Invoke(result);
            }

            resultChannel?.PublishTransitionResult(result);
            TransitionFinished?.Invoke(result);
        }

        /// <summary>
        /// Loads a scene additively only when it is not already loaded.
        /// </summary>
        public IEnumerator LoadSceneAdditiveRoutine(string sceneName)
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
            try
            {
                AsyncOperation operation = null;
                Exception loadException = null;
                try
                {
                    operation = SceneManager.LoadSceneAsync(
                        sceneName,
                        LoadSceneMode.Additive
                    );
                }
                catch (Exception exception)
                {
                    loadException = exception;
                }

                if (loadException != null)
                {
                    GameLogger.Warning(
                        nameof(LoadSceneAdditiveRoutine),
                        this,
                        $"{nameof(SceneFlowManager)} could not begin loading scene '{sceneName}'. " +
                        loadException.Message
                    );
                    yield break;
                }

                if (operation == null)
                {
                    GameLogger.Warning(
                        nameof(LoadSceneAdditiveRoutine),
                        this,
                        $"{nameof(SceneFlowManager)} could not begin loading scene '{sceneName}'."
                    );
                    yield break;
                }

                yield return operation;

                SceneLoaded?.Invoke(sceneName);
            }
            finally
            {
                scenesCurrentlyLoading.Remove(sceneName);
            }
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

            StartCoroutine(LoadSceneAdditiveRoutine(sceneName));
        }

        /// <summary>
        /// Unloads every loaded scene except persistent, target, required support,
        /// and explicitly retained scenes.
        /// </summary>
        private IEnumerator UnloadScenesExceptRoutine(
            SceneTransitionRequest request,
            ISet<string> loadedForRequest)
        {
            List<Scene> scenesToUnload = new();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loadedScene = SceneManager.GetSceneAt(i);

                if (!loadedScene.isLoaded ||
                    loadedScene.name == request.TargetSceneName ||
                    (loadedForRequest != null &&
                     loadedForRequest.Contains(loadedScene.name)) ||
                    IsPersistentScene(loadedScene.name) ||
                    request.KeepsScene(loadedScene.name))
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

            SceneUnloaded?.Invoke(sceneName);
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
