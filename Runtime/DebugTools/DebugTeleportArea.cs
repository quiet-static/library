using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.DebugTools
{
    /// <summary>
    /// Defines a scene-local group of debug teleport destinations using its child transforms.
    /// </summary>
    /// <remarks>
    /// Add this component to a parent object in a gameplay scene, then create one directly
    /// nested child for each destination. The child name is used as its dashboard label. Only
    /// enabled areas are registered in <see cref="ActiveAreas"/>, so unloading or disabling a
    /// scene immediately removes its destinations from the dashboard.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Quiet Static/Debug Tools/Debug Teleport Area")]
    public sealed class DebugTeleportArea : MonoBehaviour
    {
        private static readonly List<DebugTeleportArea> activeAreas = new();

        [Tooltip("Optional dashboard heading. The GameObject name is used when left blank.")]
        [SerializeField] private string displayName;

        [Tooltip("Apply each destination's rotation as well as its world position.")]
        [SerializeField] private bool applyRotation = true;

        /// <summary>
        /// Gets a live, read-only view of all enabled teleport areas in currently loaded scenes.
        /// </summary>
        public static IReadOnlyList<DebugTeleportArea> ActiveAreas => activeAreas;

        /// <summary>Gets the label displayed for this group in the debug dashboard.</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();

        /// <summary>Gets the name of the scene that owns these destinations.</summary>
        public string SceneName
        {
            get
            {
                Scene scene = gameObject.scene;
                return scene.IsValid() ? scene.name : "(persistent)";
            }
        }

        /// <summary>Gets the number of direct child transforms exposed as destinations.</summary>
        public int DestinationCount => transform.childCount;

        /// <summary>Registers this area's destinations while the component is usable.</summary>
        private void OnEnable()
        {
            // The containment guard also protects against accidental duplicate lifecycle callbacks.
            if (!activeAreas.Contains(this))
            {
                activeAreas.Add(this);
            }
        }

        /// <summary>Stops advertising destinations as soon as the component is disabled.</summary>
        private void OnDisable() => activeAreas.Remove(this);

        /// <summary>
        /// Removes the final static reference during destruction, including unusual teardown paths
        /// where Unity may destroy the object after its normal disable cleanup.
        /// </summary>
        private void OnDestroy() => activeAreas.Remove(this);

        /// <summary>Gets a destination by its direct-child index.</summary>
        /// <param name="index">Zero-based index among this transform's direct children.</param>
        /// <returns>The selected child, or <see langword="null"/> when the index is invalid.</returns>
        public Transform GetDestination(int index)
        {
            return index >= 0 && index < transform.childCount ? transform.GetChild(index) : null;
        }

        /// <summary>
        /// Moves a target to one of this area's destinations. A directly attached
        /// <see cref="CharacterController"/> is disabled during the move and then enabled;
        /// a directly attached <see cref="Rigidbody"/> has both velocities cleared.
        /// </summary>
        /// <remarks>
        /// The controller's prior enabled state is not preserved: a controller found on the target
        /// is enabled after teleporting even when it was disabled before this call.
        /// </remarks>
        /// <param name="target">Transform to move.</param>
        /// <param name="destinationIndex">Zero-based direct-child destination index.</param>
        /// <returns>True when both the target and destination were valid.</returns>
        public bool Teleport(Transform target, int destinationIndex)
        {
            Transform destination = GetDestination(destinationIndex);
            if (target == null || destination == null)
            {
                if (target == null)
                {
                    Debug.LogWarning($"[DebugTools] Teleport ignored because target was missing for '{SceneName}/{DisplayName}'.", this);
                }
                else
                {
                    Debug.LogWarning(
                        $"Teleport destination index {destinationIndex} is missing for '{SceneName}/{DisplayName}'.",
                        this);
                }

                return false;
            }

            CharacterController characterController = target.GetComponent<CharacterController>();
            Rigidbody rigidbody = target.GetComponent<Rigidbody>();

            try
            {
                if (characterController != null)
                {
                    // Temporarily remove the controller from collision resolution so it cannot fight
                    // the explicit pose change or immediately snap back to its previous native pose.
                    characterController.enabled = false;
                }

                if (rigidbody != null)
                {
                    // Move through the Rigidbody API so its internal physics pose stays in sync.
                    rigidbody.position = destination.position;
                    // A teleport starts from rest instead of carrying pre-teleport momentum onward.
                    rigidbody.linearVelocity = Vector3.zero;
                    rigidbody.angularVelocity = Vector3.zero;
                    if (applyRotation)
                    {
                        rigidbody.rotation = destination.rotation;
                    }
                }
                else
                {
                    // Non-physics targets can be moved directly in world space.
                    target.position = destination.position;
                    if (applyRotation)
                    {
                        target.rotation = destination.rotation;
                    }
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogError(
                    $"[DebugTools] Teleport failed while moving '{target?.name}' to destination index {destinationIndex}.",
                    this);
                Debug.LogException(exception, this);
                return false;
            }

            if (characterController != null)
            {
                try
                {
                    // Re-enabling rebuilds the controller around its newly assigned world pose.
                    characterController.enabled = true;
                }
                catch (System.Exception exception)
                {
                    Debug.LogError(
                        $"[DebugTools] Teleport failed while re-enabling CharacterController for '{target.name}'.",
                        this);
                    Debug.LogException(exception, this);
                    return false;
                }
            }

            return true;
        }
    }
}
