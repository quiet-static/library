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

            triggered = true;
            onTriggered?.Invoke();
            LoadTargetScene();
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
        private void LoadTargetScene()
        {
            if (SceneLoadService.Instance == null)
            {
                Debug.LogWarning(
                    $"{nameof(SceneTransitionTrigger)} could not load scene '{targetScene}' because no {nameof(SceneLoadService)} instance exists.",
                    this
                );

                return;
            }

            if (additive)
            {
                SceneLoadService.Instance.LoadAdditive(targetScene);
            }
            else
            {
                SceneLoadService.Instance.LoadSingle(targetScene);
            }
        }
    }
}
