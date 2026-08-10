using UnityEngine;

namespace QuietStatic.Toolkit.SceneFlow
{
    /// <summary>Operation carried by a <see cref="SceneFlowCommand"/>.</summary>
    public enum SceneFlowCommandType
    {
        Transition,
        LoadAdditive,
        Unload,
        SetActive
    }

    /// <summary>Typed cross-scene scene-flow command.</summary>
    public readonly struct SceneFlowCommand : ICrossSceneCommand
    {
        /// <summary>Creates a scene-flow command.</summary>
        public SceneFlowCommand(
            SceneFlowCommandType type,
            string sceneName = "",
            SceneTransitionRequest transition = null)
        {
            Type = type;
            SceneName = sceneName ?? string.Empty;
            Transition = transition;
        }

        /// <summary>Requested scene-flow operation.</summary>
        public SceneFlowCommandType Type { get; }

        /// <summary>Normalized scene name targeted by the command.</summary>
        public string SceneName { get; }

        /// <summary>Detailed transition request for transition commands.</summary>
        public SceneTransitionRequest Transition { get; }
    }

    /// <summary>
    /// Relays content-scene loading commands to a persistent scene-flow manager.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SceneFlowRequestChannel",
        menuName = "Quiet Static Toolkit/Scene Flow/Scene Flow Request Channel")]
    public sealed class SceneFlowRequestChannel :
        CrossSceneCommandChannel<SceneFlowCommand>
    {
        /// <summary>Requests a full content transition from a UnityEvent.</summary>
        public void TransitionToScene(string sceneName)
        {
            TryTransitionToScene(sceneName);
        }

        /// <summary>Requests a full content transition and reports whether a receiver exists.</summary>
        public bool TryTransitionToScene(string sceneName)
        {
            return RequestTransition(new SceneTransitionRequest(sceneName));
        }

        /// <summary>Requests a full transition described by a reusable request object.</summary>
        public bool RequestTransition(SceneTransitionRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.TargetSceneName))
            {
                GameLogger.Warning(
                    nameof(RequestTransition),
                    this,
                    "Scene flow cannot transition to an empty scene name.");
                return false;
            }

            return Dispatch(new SceneFlowCommand(
                SceneFlowCommandType.Transition,
                request.TargetSceneName,
                request));
        }

        /// <summary>Requests an additive load from a UnityEvent.</summary>
        public void LoadAdditive(string sceneName)
        {
            TryLoadAdditive(sceneName);
        }

        /// <summary>Requests an additive load and reports whether a receiver exists.</summary>
        public bool TryLoadAdditive(string sceneName)
        {
            return TryDispatchSceneName(
                SceneFlowCommandType.LoadAdditive,
                sceneName);
        }

        /// <summary>Requests that a nonpersistent scene unload.</summary>
        public void Unload(string sceneName)
        {
            TryUnload(sceneName);
        }

        /// <summary>Requests an unload and reports whether a receiver exists.</summary>
        public bool TryUnload(string sceneName)
        {
            return TryDispatchSceneName(
                SceneFlowCommandType.Unload,
                sceneName);
        }

        /// <summary>Requests that a loaded scene become active.</summary>
        public void SetActive(string sceneName)
        {
            TrySetActive(sceneName);
        }

        /// <summary>Requests active-scene selection and reports whether a receiver exists.</summary>
        public bool TrySetActive(string sceneName)
        {
            return TryDispatchSceneName(
                SceneFlowCommandType.SetActive,
                sceneName);
        }

        private bool TryDispatchSceneName(
            SceneFlowCommandType type,
            string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                GameLogger.Warning(
                    type.ToString(),
                    this,
                    "Scene flow cannot use an empty scene name.");
                return false;
            }

            return Dispatch(new SceneFlowCommand(
                type,
                sceneName.Trim()));
        }
    }
}
