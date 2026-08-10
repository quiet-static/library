using UnityEngine;
using UnityEngine.Events;

namespace QuietStatic.Toolkit.SceneFlow
{
    /// <summary>
    /// Trigger-volume helper that starts a scene transition when a matching collider enters.
    /// </summary>
    /// <remarks>
    /// This component is intended for simple doorway, hallway, portal, or level-exit triggers.
    /// When an object enters the trigger, the component can optionally validate the object's tag,
    /// invoke a UnityEvent, and then ask <see cref="SceneLoadService"/> to load the configured scene.
    ///
    /// The target scene must still be included in Unity's Build Settings for runtime scene loading
    /// to succeed.
    /// </remarks>
    public class SceneTransitionTrigger : MonoBehaviour
    {
        [Header("Scene Target")]
        [Tooltip("Name of the scene to load when this trigger is activated. The scene must be included in Build Settings.")]
        [SerializeField] private string targetScene;

        [Tooltip("If true, the target scene is loaded additively. If false, the target scene replaces the current scene.")]
        [SerializeField] private bool additive;

        [Tooltip("Optional channel used to reach the persistent Scene Flow Manager.")]
        [SerializeField] private SceneFlowRequestChannel requestChannel;

        [Tooltip("Optional scene map. When assigned with a Connection Id, the configured connection replaces Target Scene and Additive.")]
        [SerializeField] private SceneFlowMap sceneFlowMap;

        [Tooltip("Stable connection identifier from the Scene Flow Map.")]
        [SerializeField] private string connectionId;

        [Header("Trigger Rules")]
        [Tooltip("If true, this trigger can only activate once. If false, it may activate every time a valid collider enters.")]
        [SerializeField] private bool onlyOnce = true;

        [Tooltip("Optional tag required to activate this trigger. Leave blank to allow any entering collider.")]
        [SerializeField] private string requiredTag = "Player";

        [Header("Events")]
        [Tooltip("Invoked after the trigger is accepted, before the scene load request is sent.")]
        [SerializeField] private UnityEvent onTriggered;

        /// <summary>
        /// Tracks whether this trigger has already fired.
        /// </summary>
        private bool triggered;

        /// <summary>
        /// Handles Unity trigger-enter messages and attempts to start the configured scene transition.
        /// </summary>
        /// <param name="other">The collider that entered this trigger volume.</param>
        private void OnTriggerEnter(Collider other)
        {
            if (!CanTrigger(other))
            {
                return;
            }

            if (!CanDispatchTarget())
            {
                return;
            }

            triggered = true;
            onTriggered?.Invoke();
            TryLoadTargetScene();
        }

        /// <summary>
        /// Determines whether the entering collider is allowed to activate this trigger.
        /// </summary>
        /// <param name="other">The collider being checked.</param>
        /// <returns>
        /// <c>true</c> if the trigger is allowed to fire; otherwise, <c>false</c>.
        /// </returns>
        private bool CanTrigger(Collider other)
        {
            if (onlyOnce && triggered)
            {
                return false;
            }

            if (other == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(requiredTag) && !other.CompareTag(requiredTag))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Requests the configured scene load through the shared <see cref="SceneLoadService"/>.
        /// </summary>
        private bool TryLoadTargetScene()
        {
            if (TryGetMappedRequest(out SceneTransitionRequest mappedRequest))
            {
                if (requestChannel != null)
                {
                    return requestChannel.RequestTransition(mappedRequest);
                }

                if (SceneFlowManager.Instance != null)
                {
                    SceneFlowManager.Instance.TransitionToScene(mappedRequest);
                    return true;
                }
            }

            if (string.IsNullOrWhiteSpace(targetScene))
            {
                GameLogger.Warning(
                    nameof(TryLoadTargetScene),
                    this,
                    $"{nameof(SceneTransitionTrigger)} needs a target scene."
                );
                return false;
            }

            if (requestChannel != null)
            {
                return additive
                    ? requestChannel.TryLoadAdditive(targetScene)
                    : requestChannel.TryTransitionToScene(targetScene);
            }

            if (SceneFlowManager.Instance != null)
            {
                if (additive)
                {
                    SceneFlowManager.Instance.LoadSceneAdditive(targetScene);
                }
                else
                {
                    SceneFlowManager.Instance.TransitionToScene(targetScene);
                }

                return true;
            }

            // Compatibility fallback for projects using the smaller legacy
            // scene-loading service without a content-stack manager.
            if (SceneLoadService.Instance != null)
            {
                if (additive)
                {
                    SceneLoadService.Instance.LoadAdditive(targetScene);
                }
                else
                {
                    SceneLoadService.Instance.LoadSingle(targetScene);
                }

                return true;
            }

            GameLogger.Warning(
                nameof(TryLoadTargetScene),
                this,
                $"{nameof(SceneTransitionTrigger)} could not load scene '{targetScene}' because no scene-flow receiver exists."
            );
            return false;
        }

        private bool CanDispatchTarget()
        {
            bool usesMappedConnection =
                sceneFlowMap != null && !string.IsNullOrWhiteSpace(connectionId);
            if (usesMappedConnection)
            {
                if (!sceneFlowMap.TryGetConnection(connectionId, out SceneFlowMap.Connection connection) ||
                    string.IsNullOrWhiteSpace(connection.ToSceneName))
                {
                    GameLogger.Warning(
                        nameof(CanDispatchTarget),
                        this,
                        $"{nameof(SceneTransitionTrigger)} cannot find a valid connection named '{connectionId}'.");
                    return false;
                }

                string activeScene = gameObject.scene.name;
                if (!string.IsNullOrWhiteSpace(connection.FromSceneName) &&
                    connection.FromSceneName != activeScene)
                {
                    GameLogger.Warning(
                        nameof(CanDispatchTarget),
                        this,
                        $"Connection '{connectionId}' starts in '{connection.FromSceneName}', not '{activeScene}'.");
                    return false;
                }

                if (requestChannel != null)
                {
                    return requestChannel.HasReceivers;
                }

                if (SceneFlowManager.Instance != null)
                {
                    return true;
                }

                GameLogger.Warning(
                    nameof(CanDispatchTarget),
                    this,
                    $"Connection '{connectionId}' needs a scene-flow channel or {nameof(SceneFlowManager)}.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(targetScene))
            {
                GameLogger.Warning(
                    nameof(CanDispatchTarget),
                    this,
                    $"{nameof(SceneTransitionTrigger)} needs a target scene."
                );
                return false;
            }

            if (requestChannel != null)
            {
                if (requestChannel.HasReceivers)
                {
                    return true;
                }

                GameLogger.Warning(
                    nameof(CanDispatchTarget),
                    this,
                    $"{nameof(SceneTransitionTrigger)} has no receiver for its scene-flow channel."
                );
                return false;
            }

            if (SceneFlowManager.Instance != null ||
                SceneLoadService.Instance != null)
            {
                return true;
            }

            GameLogger.Warning(
                nameof(CanDispatchTarget),
                this,
                $"{nameof(SceneTransitionTrigger)} could not load scene '{targetScene}' because no scene-flow receiver exists."
            );
            return false;
        }

        private bool TryGetMappedRequest(out SceneTransitionRequest request)
        {
            request = null;
            return sceneFlowMap != null &&
                   !string.IsNullOrWhiteSpace(connectionId) &&
                   sceneFlowMap.TryCreateRequest(connectionId, out request);
        }
    }
}
