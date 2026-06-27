using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>
    /// Detects and interacts with nearby <see cref="Interactable"/> objects using a forward raycast.
    /// </summary>
    /// <remarks>
    /// Attach this component to the player or to another object that needs to trigger
    /// interactable objects. The component continuously refreshes <see cref="CurrentTarget"/>
    /// each frame, and <see cref="TryInteract"/> can be called by an input system when the
    /// player presses the interact button.
    ///
    /// The raycast uses <see cref="QueryTriggerInteraction.Collide"/> so trigger colliders
    /// can be used as interaction volumes.
    /// </remarks>
    public class Interactor : MonoBehaviour
    {
        [Header("Raycast")]
        [Tooltip("Transform used as the starting point and forward direction for interaction checks. If left empty, this object's transform is used.")]
        [SerializeField] private Transform rayOrigin;

        [Tooltip("Maximum distance, in world units, that this interactor can detect interactable objects.")]
        [Min(0f)]
        [SerializeField] private float range = 2.5f;

        [Tooltip("Physics layers that can be hit by the interaction raycast.")]
        [SerializeField] private LayerMask interactionMask = ~0;

        [Tooltip("The main camera the player uses")]
        [SerializeField] private Camera interactionCamera;

        /// <summary>
        /// Gets the interactable currently detected by the interaction raycast.
        /// </summary>
        /// <remarks>
        /// This value is refreshed during <see cref="Update"/> and again immediately before
        /// <see cref="TryInteract"/> attempts an interaction.
        /// </remarks>
        public Interactable CurrentTarget { get; private set; }

        /// <summary>
        /// UnityEvent-friendly entry point for an interact input.
        /// </summary>
        public void HandleInteractInput()
        {
            TryInteract();
        }

        /// <summary>
        /// Auto-fills the ray origin when the component is added or reset in the Inspector.
        /// </summary>
        private void Reset()
        {
            rayOrigin = transform;
        }

        /// <summary>
        /// Refreshes the current interaction target once per frame.
        /// </summary>
        private void Update()
        {
            RefreshTarget();
        }

        /// <summary>
        /// Updates <see cref="CurrentTarget"/> by raycasting forward from the configured origin.
        /// </summary>
        /// <remarks>
        /// If the raycast hits a collider, this method searches that collider and its parents
        /// for an <see cref="Interactable"/> component. If no valid interactable is found,
        /// <see cref="CurrentTarget"/> remains <c>null</c>.
        /// </remarks>
        public void RefreshTarget()
        {
            CurrentTarget = null;

            if (interactionCamera == null)
            {
                interactionCamera = Camera.main;
            }

            if (interactionCamera == null)
            {
                return;
            }

            Ray ray = interactionCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f)
            );

            if (Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    range,
                    interactionMask,
                    QueryTriggerInteraction.Collide))
            {
                CurrentTarget = hit.collider.GetComponentInParent<Interactable>();
            }
        }

        /// <summary>
        /// Attempts to interact with the currently detected target.
        /// </summary>
        /// <returns>
        /// <c>true</c> if an interactable was found and its interaction succeeded;
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method refreshes the target immediately before interacting so the result
        /// reflects the latest player/camera position.
        /// </remarks>
        public bool TryInteract()
        {
            RefreshTarget();

            if (CurrentTarget == null)
            {
                return false;
            }

            return CurrentTarget.TryInteract(this);
        }

        /// <summary>
        /// Gets the transform used for raycast origin and direction.
        /// </summary>
        /// <returns>
        /// The assigned ray origin, or this object's transform if no custom origin is assigned.
        /// </returns>
        private Transform GetRayOrigin()
        {
            return rayOrigin != null ? rayOrigin : transform;
        }
    }
}
