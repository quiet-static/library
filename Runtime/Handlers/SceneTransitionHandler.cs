using System;
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
        [Tooltip("Scene-flow map containing the parameterless transition.")]
        [SerializeField] private SceneFlowMap sceneFlowMap;

        [Tooltip("Connection used by Transition().")]
        [SerializeField] private string connectionId;

        [Header("Commands")]
        [Tooltip("Channel shared with the persistent Scene Flow Manager. Recommended for scene-owned handlers.")]
        [RequiredCommandChannel]
        [SerializeField] private SceneFlowRequestChannel requestChannel;

        [Header("Events")]
        [Tooltip("Invoked after a valid transition request is accepted for execution. Immediate rejections do not invoke this event.")]
        [SerializeField] private UnityEvent onTransitionStarted;

        [Tooltip("Invoked when this handler's accepted request later fails, or when it is rejected immediately by the scene-flow manager.")]
        [SerializeField] private UnityEvent onTransitionFailed;

        private SceneTransitionRequest pendingRequest;
        private SceneFlowRequestChannel pendingChannel;
        private bool isSubmitting;
        private SceneTransitionResult? synchronousResult;
        private SceneTransitionResult? deferredResult;

        /// <summary>
        /// Raised when the request submitted by this handler reaches a terminal
        /// success or failure result.
        /// </summary>
        public event Action<SceneTransitionResult> TransitionFinished;

        /// <summary>Whether this handler is awaiting its submitted request's result.</summary>
        public bool IsTransitionPending => pendingRequest != null;

        private void OnEnable()
        {
            if (!deferredResult.HasValue)
            {
                return;
            }

            SceneTransitionResult result = deferredResult.Value;
            deferredResult = null;
            NotifyTransitionFinished(result);
        }

        private void OnDestroy()
        {
            ClearPendingRequest();
            synchronousResult = null;
            deferredResult = null;
            isSubmitting = false;
        }

        /// <summary>
        /// Transitions using the configured map connection.
        /// </summary>
        public void Transition()
        {
            TryTransition();
        }

        /// <summary>
        /// Attempts the configured transition and reports whether it was accepted
        /// for execution or completed successfully during dispatch.
        /// </summary>
        public bool TryTransition()
        {
            if (requirement != null && !requirement.IsMet())
            {
                return false;
            }

            return TryTransitionToConnection(connectionId);
        }

        /// <summary>Transitions using a connection ID supplied by a UnityEvent.</summary>
        public void TransitionToConnection(string id)
        {
            TryTransitionToConnection(id);
        }

        /// <summary>
        /// Attempts a mapped transition and reports whether it was accepted for
        /// execution or completed successfully during dispatch.
        /// </summary>
        public bool TryTransitionToConnection(string id)
        {
            if (sceneFlowMap == null ||
                !sceneFlowMap.TryGetConnection(id, out SceneFlowMap.Connection connection) ||
                string.IsNullOrWhiteSpace(connection.ToSceneName))
            {
                GameLogger.Warning(
                    nameof(TransitionToConnection),
                    this,
                    $"{nameof(SceneTransitionHandler)} cannot find a valid connection named '{id}'.");
                return false;
            }

            string sourceScene = gameObject.scene.name;
            if (!string.IsNullOrWhiteSpace(connection.FromSceneName) &&
                connection.FromSceneName != sourceScene)
            {
                GameLogger.Warning(
                    nameof(TransitionToConnection),
                    this,
                    $"Connection '{id}' starts in '{connection.FromSceneName}', not '{sourceScene}'.");
                return false;
            }

            return Dispatch(connection.CreateRequest());
        }

        private bool Dispatch(SceneTransitionRequest request)
        {
            if (requestChannel == null)
            {
                GameLogger.Warning(
                    nameof(Dispatch),
                    this,
                    $"{nameof(SceneTransitionHandler)} requires a scene-flow request channel.");
                return false;
            }

            if (pendingRequest != null || isSubmitting)
            {
                GameLogger.Warning(
                    nameof(Dispatch),
                    this,
                    $"{nameof(SceneTransitionHandler)} already has a pending transition request.");
                return false;
            }

            pendingRequest = request;
            pendingChannel = requestChannel;
            pendingChannel.TransitionFinished += HandleTransitionFinished;
            synchronousResult = null;
            bool dispatched;
            isSubmitting = true;
            try
            {
                dispatched = pendingChannel.RequestTransition(request);
            }
            catch
            {
                ClearPendingRequest();
                synchronousResult = null;
                throw;
            }
            finally
            {
                isSubmitting = false;
            }

            if (!dispatched)
            {
                ClearPendingRequest();
                synchronousResult = null;
                GameLogger.Warning(
                    nameof(Dispatch),
                    this,
                    $"No receiver is listening to {requestChannel.name}.");
                return false;
            }

            if (synchronousResult.HasValue)
            {
                SceneTransitionResult result = synchronousResult.Value;
                synchronousResult = null;
                if (result.Succeeded)
                {
                    onTransitionStarted?.Invoke();
                }

                DeliverOrDefer(result);
                return result.Succeeded;
            }

            if (!ReferenceEquals(pendingRequest, request))
            {
                return false;
            }

            onTransitionStarted?.Invoke();
            return true;
        }

        private void HandleTransitionFinished(SceneTransitionResult result)
        {
            if (!ReferenceEquals(result.Request, pendingRequest))
            {
                return;
            }

            ClearPendingRequest();
            if (isSubmitting)
            {
                synchronousResult = result;
                return;
            }

            DeliverOrDefer(result);
        }

        private void DeliverOrDefer(SceneTransitionResult result)
        {
            if (!isActiveAndEnabled)
            {
                deferredResult = result;
                return;
            }

            NotifyTransitionFinished(result);
        }

        private void NotifyTransitionFinished(SceneTransitionResult result)
        {
            TransitionFinished?.Invoke(result);
            if (!result.Succeeded)
            {
                onTransitionFailed?.Invoke();
            }
        }

        private void ClearPendingRequest()
        {
            SceneFlowRequestChannel channel = pendingChannel;
            pendingChannel = null;
            pendingRequest = null;
            if (channel != null)
            {
                channel.TransitionFinished -= HandleTransitionFinished;
            }
        }
    }
}
