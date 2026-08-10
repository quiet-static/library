using QuietStatic.Toolkit.SceneFlow;
using QuietStatic.Toolkit.Flags;
using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic
{
    /// <summary>
    /// UnityEvent-facing bridge for full scene transitions.
    /// </summary>
    /// <remarks>
    /// Place this handler beside a button, interactable, animation, or other
    /// scene-owned event source. It sends requests through the configured channel
    /// so scene content does not directly depend on a persistent manager.
    /// </remarks>
    [AddComponentMenu("Quiet Static Toolkit/Handlers/Scene Transition Handler")]
    public sealed class SceneTransitionHandler : MonoBehaviour
    {
        [Header("Requirements")]
        [Tooltip("Optional progression flags required before a transition request is sent.")]
        [SerializeField] private FlagRequirement requirement = new();

        [Header("Configured Connection")]
        [Tooltip("Optional scene-flow map containing the parameterless transition.")]
        [SerializeField] private SceneFlowMap sceneFlowMap;

        [Tooltip("Connection used by Transition().")]
        [SerializeField] private string connectionId;

        [Header("Direct Scene Fallback")]
        [Tooltip("Scene used by Transition() when no mapped connection is configured.")]
        [SerializeField] private SceneReference targetScene = new();

        [Header("Commands")]
        [Tooltip("Channel shared with the persistent Scene Flow Manager. Recommended for scene-owned handlers.")]
        [SerializeField] private SceneFlowRequestChannel requestChannel;

        [Header("Events")]
        [Tooltip("Invoked after a valid transition request is accepted by a scene-flow receiver.")]
        [SerializeField] private UnityEvent onTransitionStarted;

        /// <summary>
        /// Transitions using the configured map connection, or the direct target
        /// scene when no connection is assigned.
        /// </summary>
        public void Transition()
        {
            if (requirement != null && !requirement.IsMet())
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(connectionId))
            {
                TransitionToConnection(connectionId);
                return;
            }

            TransitionToScene(targetScene?.SceneName);
        }

        /// <summary>Transitions using a connection ID supplied by a UnityEvent.</summary>
        public void TransitionToConnection(string id)
        {
            if (sceneFlowMap == null ||
                !sceneFlowMap.TryGetConnection(id, out SceneFlowMap.Connection connection) ||
                string.IsNullOrWhiteSpace(connection.ToSceneName))
            {
                GameLogger.Warning(
                    nameof(TransitionToConnection),
                    this,
                    $"{nameof(SceneTransitionHandler)} cannot find a valid connection named '{id}'.");
                return;
            }

            string sourceScene = gameObject.scene.name;
            if (!string.IsNullOrWhiteSpace(connection.FromSceneName) &&
                connection.FromSceneName != sourceScene)
            {
                GameLogger.Warning(
                    nameof(TransitionToConnection),
                    this,
                    $"Connection '{id}' starts in '{connection.FromSceneName}', not '{sourceScene}'.");
                return;
            }

            Dispatch(connection.CreateRequest());
        }

        /// <summary>Transitions directly to a scene name supplied by a UnityEvent.</summary>
        public void TransitionToScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                GameLogger.Warning(
                    nameof(TransitionToScene),
                    this,
                    $"{nameof(SceneTransitionHandler)} needs a target scene.");
                return;
            }

            Dispatch(new SceneTransitionRequest(sceneName));
        }

        private void Dispatch(SceneTransitionRequest request)
        {
            if (requestChannel != null)
            {
                if (requestChannel.RequestTransition(request))
                {
                    onTransitionStarted?.Invoke();
                }
                else
                {
                    GameLogger.Warning(
                        nameof(Dispatch),
                        this,
                        $"No receiver is listening to {requestChannel.name}.");
                }
                return;
            }

            if (SceneFlowManager.Instance != null)
            {
                SceneFlowManager.Instance.TransitionToScene(request);
                onTransitionStarted?.Invoke();
                return;
            }

            GameLogger.Warning(
                nameof(Dispatch),
                this,
                $"{nameof(SceneTransitionHandler)} has no request channel or {nameof(SceneFlowManager)}.");
        }
    }
}
