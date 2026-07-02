using QuietStatic;
using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>
    /// Detects and interacts with nearby interactable objects using a camera-center raycast.
    /// Also updates interaction UI and optional highlighting for the current target.
    /// </summary>
    public class Interactor : MonoBehaviour
    {
        [Header("Raycast")]
        [Tooltip("Camera used to cast the interaction ray through the center of the screen.")]
        [SerializeField] private Camera interactionCamera;

        [Tooltip("Maximum distance, in world units, that this interactor can detect interactable objects.")]
        [Min(0f)]
        [SerializeField] private float range = 2.5f;

        [Tooltip("Physics layers that can be hit by the interaction raycast.")]
        [SerializeField] private LayerMask interactionMask = ~0;

        [Header("Feedback")]
        [Tooltip("Optional UI manager used to show the current interaction prompt.")]
        [SerializeField] private InteractionUIManager interactionUI;

        [Tooltip("Text shown before the interactable display name.")]
        [SerializeField] private string promptPrefix = "Press E to ";

        [SerializeField] private float interactionCooldown = 0.2f;

        private float nextInteractTime;

        /// <summary>
        /// Gets the interactable currently under the center crosshair.
        /// </summary>
        public Interactable CurrentTarget { get; private set; }

        /// <summary>
        /// Previously targeted object, used to clear UI and highlight state.
        /// </summary>
        private Interactable previousTarget;

        /// <summary>
        /// UnityEvent-friendly entry point for an interact input.
        /// </summary>
        public void HandleInteractInput()
        {
            TryInteract();
        }

        private void Awake()
        {
            if (interactionCamera == null)
            {
                interactionCamera = Camera.main;
            }

            if (interactionUI == null)
            {
                interactionUI = InteractionUIManager.Instance;
            }
        }

        private void Update()
        {
            RefreshTarget();
        }

        /// <summary>
        /// Raycasts from the center of the assigned camera and updates target feedback.
        /// </summary>
        public void RefreshTarget()
        {
            Interactable newTarget = GetTargetFromCrosshair();

            if (newTarget == CurrentTarget)
            {
                return;
            }

            previousTarget = CurrentTarget;
            CurrentTarget = newTarget;

            UpdateFeedback();
        }

        /// <summary>
        /// Attempts to interact with the object currently under the crosshair.
        /// </summary>
        public bool TryInteract()
        {
            if (Time.time < nextInteractTime)
                return false;

            nextInteractTime = Time.time + interactionCooldown;

            RefreshTarget();

            if (CurrentTarget == null)
                return false;

            bool succeeded = CurrentTarget.TryInteract(this);

            RefreshTarget();
            return succeeded;
        }

        /// <summary>
        /// Finds the interactable hit by a ray through the camera center.
        /// </summary>
        private Interactable GetTargetFromCrosshair()
        {
            if (interactionCamera == null)
            {
                return null;
            }

            Ray ray = interactionCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f)
            );

            UnityEngine.Debug.DrawRay(ray.origin, ray.direction * range, Color.green);

            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    range,
                    interactionMask,
                    QueryTriggerInteraction.Collide))
            {
                return null;
            }

            return hit.collider.GetComponentInParent<Interactable>();
        }

        /// <summary>
        /// Updates the interaction prompt and highlight when the viewed target changes.
        /// </summary>
        private void UpdateFeedback()
        {
            SetHighlight(previousTarget, false);

            if (CurrentTarget == null)
            {
                interactionUI?.HidePrompt();
                return;
            }

            if (!CurrentTarget.CanInteract())
            {
                interactionUI?.HidePrompt();
                return;
            }

            SetHighlight(CurrentTarget, true);

            interactionUI?.ShowPrompt(
                $"{promptPrefix}{CurrentTarget.DisplayName}"
            );
        }

        /// <summary>
        /// Enables or disables highlighting on an interactable when available.
        /// </summary>
        private static void SetHighlight(
            Interactable interactable,
            bool highlighted)
        {
            if (interactable == null)
            {
                return;
            }

            InteractionHighlighter highlighter =
                interactable.GetComponentInChildren<InteractionHighlighter>();

            highlighter?.SetHighlighted(highlighted);
        }
    }
}