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
        private const int MaxRaycastHits = 32;

        [Header("Raycast")]
        [Tooltip("Camera used to cast the interaction ray through the center of the screen.")]
        [SerializeField] private Camera interactionCamera;

        [Tooltip("Maximum distance, in world units, that this interactor can detect interactable objects.")]
        [Min(0f)]
        [SerializeField] private float range = 2.5f;

        [Tooltip("Physics layers that can be hit by the interaction raycast.")]
        [SerializeField] private LayerMask interactionMask = ~0;

        [Header("Third-Person Reach")]
        [Tooltip("Optional transform used to validate physical interaction reach independently from the camera ray.")]
        [SerializeField] private Transform interactionOrigin;

        [Tooltip("Use PlayerManager's active player as the reach origin when no explicit origin is assigned.")]
        [SerializeField] private bool useActivePlayerAsOrigin;

        [Tooltip("Require the aimed hit point to be within reach of the resolved interaction origin.")]
        [SerializeField] private bool requireInteractionOriginInRange;

        [Tooltip("Maximum distance from the resolved interaction origin to the aimed collider hit point.")]
        [Min(0f)]
        [SerializeField] private float maximumInteractionOriginDistance = 2.5f;

        [Tooltip("Optional hierarchy ignored by the camera ray, such as a third-person player body.")]
        [SerializeField] private Transform ignoredRoot;

        [Tooltip("Ignore colliders below PlayerManager's active player so a third-person body cannot block the camera ray.")]
        [SerializeField] private bool ignoreActivePlayerColliders;

        [Header("Feedback")]
        [Tooltip("Optional UI manager used to show the current interaction prompt.")]
        [SerializeField] private InteractionUIManager interactionUI;

        [Tooltip("Text shown before the interactable display name.")]
        [SerializeField] private string promptPrefix = "Press E to ";

        [Tooltip("Minimum unscaled seconds between accepted interaction input requests.")]
        [SerializeField] private float interactionCooldown = 0.2f;

        private float nextInteractTime;

        /// <summary>
        /// Gets the interactable currently under the center crosshair.
        /// </summary>
        public Interactable CurrentTarget { get; private set; }

        /// <summary>
        /// Gets the toolkit or project-owned interaction target under the crosshair.
        /// </summary>
        public IInteractionTarget CurrentInteractionTarget { get; private set; }

        /// <summary>Gets the hold interactable currently under the center crosshair.</summary>
        public HoldInteractable CurrentHoldTarget { get; private set; }

        /// <summary>Gets the activated progress target under the crosshair.</summary>
        public ActivatedProgressInteractable CurrentProgressTarget { get; private set; }

        /// <summary>
        /// Previously targeted object, used to clear UI and highlight state.
        /// </summary>
        private IInteractionTarget previousInteractionTarget;
        private HoldInteractable previousHoldTarget;
        private ActivatedProgressInteractable previousProgressTarget;
        private HoldInteractable activeHoldTarget;
        private int holdStartedFrame = -1;
        private readonly RaycastHit[] raycastHits =
            new RaycastHit[MaxRaycastHits];

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
            UpdateHoldInteraction();
        }

        /// <summary>
        /// Raycasts from the center of the assigned camera and updates target feedback.
        /// </summary>
        public void RefreshTarget()
        {
            GetTargetsFromCrosshair(
                out IInteractionTarget newTarget,
                out HoldInteractable newHoldTarget,
                out ActivatedProgressInteractable newProgressTarget);

            // A progress interaction owns the target while it can be started or is
            // running. Once complete or disabled, it yields to the next interaction
            // stage on the same object.
            if (newProgressTarget != null &&
                (newProgressTarget.IsAvailable || newProgressTarget.IsRunning))
            {
                newTarget = null;
                newHoldTarget = null;
            }
            else
            {
                newProgressTarget = null;

                // Disabled staged holds yield to the one-shot interaction that enables them.
                if (newHoldTarget != null &&
                    (!newHoldTarget.IsEnabled ||
                     !newHoldTarget.IsInteractorTargetingEnabled))
                {
                    newHoldTarget = null;
                }

                if (newHoldTarget != null)
                {
                    newTarget = null;
                }
            }

            if (newTarget != null &&
                !newTarget.IsInteractionAvailable(this))
            {
                newTarget = null;
            }

            if (ReferenceEquals(newTarget, CurrentInteractionTarget) &&
                newHoldTarget == CurrentHoldTarget &&
                newProgressTarget == CurrentProgressTarget)
            {
                return;
            }

            previousInteractionTarget = CurrentInteractionTarget;
            previousHoldTarget = CurrentHoldTarget;
            previousProgressTarget = CurrentProgressTarget;
            CurrentInteractionTarget = newTarget;
            CurrentTarget = newTarget as Interactable;
            CurrentHoldTarget = newHoldTarget;
            CurrentProgressTarget = newProgressTarget;

            if (activeHoldTarget != null && activeHoldTarget != CurrentHoldTarget)
            {
                StopHold();
            }

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

            if (CurrentProgressTarget != null)
            {
                bool started = CurrentProgressTarget.TryActivate();
                RefreshTarget();
                return started;
            }

            if (CurrentHoldTarget != null)
            {
                activeHoldTarget = CurrentHoldTarget;
                holdStartedFrame = Time.frameCount;
                return CurrentHoldTarget.CanInteract();
            }

            if (CurrentInteractionTarget == null ||
                !CurrentInteractionTarget.IsInteractionAvailable(this))
                return false;

            bool succeeded = CurrentInteractionTarget.TryInteract(this);

            RefreshTarget();
            return succeeded;
        }

        /// <summary>
        /// Finds the interactable hit by a ray through the camera center.
        /// </summary>
        private void GetTargetsFromCrosshair(
            out IInteractionTarget interactable,
            out HoldInteractable holdInteractable,
            out ActivatedProgressInteractable progressInteractable)
        {
            interactable = null;
            holdInteractable = null;
            progressInteractable = null;

            if (interactionCamera == null)
            {
                return;
            }

            Ray ray = interactionCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f)
            );

            UnityEngine.Debug.DrawRay(ray.origin, ray.direction * range, Color.green);

            int hitCount = Physics.RaycastNonAlloc(
                ray,
                raycastHits,
                range,
                interactionMask,
                QueryTriggerInteraction.Collide);

            if (hitCount == 0)
            {
                return;
            }

            SortHitsByDistance(raycastHits, hitCount);

            Transform selectedOwner = null;
            Transform resolvedIgnoredRoot = ResolveIgnoredRoot();
            Transform resolvedInteractionOrigin = ResolveInteractionOrigin();

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = raycastHits[i];

                if (resolvedIgnoredRoot != null &&
                    hit.collider.transform.IsChildOf(resolvedIgnoredRoot))
                {
                    continue;
                }

                if (!TryGetInteractionTargets(
                        hit.collider,
                        out IInteractionTarget hitInteractable,
                        out HoldInteractable hitHoldInteractable,
                        out ActivatedProgressInteractable hitProgressInteractable,
                        out Transform hitOwner))
                {
                    // The nearest collider still occludes everything behind it.
                    if (selectedOwner == null)
                    {
                        return;
                    }

                    continue;
                }

                if (requireInteractionOriginInRange &&
                    !IsHitWithinInteractionOriginRange(
                        hit,
                        resolvedInteractionOrigin))
                {
                    if (selectedOwner == null)
                    {
                        return;
                    }

                    continue;
                }

                if (selectedOwner != null &&
                    !hitOwner.IsChildOf(selectedOwner))
                {
                    continue;
                }

                interactable = hitInteractable;
                holdInteractable = hitHoldInteractable;
                progressInteractable = hitProgressInteractable;
                selectedOwner = hitOwner;
            }
        }

        private static void SortHitsByDistance(
            RaycastHit[] hits,
            int hitCount)
        {
            for (int i = 1; i < hitCount; i++)
            {
                RaycastHit current = hits[i];
                int insertionIndex = i - 1;

                while (insertionIndex >= 0 &&
                    hits[insertionIndex].distance > current.distance)
                {
                    hits[insertionIndex + 1] = hits[insertionIndex];
                    insertionIndex--;
                }

                hits[insertionIndex + 1] = current;
            }
        }

        /// <summary>
        /// Finds the nearest interaction-bearing transform above a collider. Resolving all
        /// interaction types at that same hierarchy level prevents a child interaction
        /// from being mixed with a different interaction on one of its parents.
        /// </summary>
        private static bool TryGetInteractionTargets(
            Collider hitCollider,
            out IInteractionTarget interactable,
            out HoldInteractable holdInteractable,
            out ActivatedProgressInteractable progressInteractable,
            out Transform owner)
        {
            interactable = null;
            holdInteractable = null;
            progressInteractable = null;
            owner = null;

            if (hitCollider == null)
            {
                return false;
            }

            Transform current = hitCollider.transform;
            while (current != null)
            {
                interactable = current.GetComponent<IInteractionTarget>();
                holdInteractable = current.GetComponent<HoldInteractable>();
                progressInteractable =
                    current.GetComponent<ActivatedProgressInteractable>();

                if (interactable != null ||
                    holdInteractable != null ||
                    progressInteractable != null)
                {
                    owner = current;
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// Updates the interaction prompt and highlight when the viewed target changes.
        /// </summary>
        private void UpdateFeedback()
        {
            SetHighlight(previousInteractionTarget, false);
            SetHighlight(previousHoldTarget, false);
            SetHighlight(previousProgressTarget, false);

            if (CurrentInteractionTarget == null &&
                CurrentHoldTarget == null &&
                CurrentProgressTarget == null)
            {
                interactionUI?.HidePrompt();
                interactionUI?.HideProgress();
                return;
            }

            if (CurrentProgressTarget != null)
            {
                if (CurrentProgressTarget.IsAvailable)
                {
                    SetHighlight(CurrentProgressTarget, true);
                    interactionUI?.ShowPrompt(
                        CurrentProgressTarget.HoverPrompt);
                }
                else
                {
                    interactionUI?.HidePrompt();
                }
                return;
            }

            if (CurrentHoldTarget != null)
            {
                SetHighlight(CurrentHoldTarget, true);
                interactionUI?.ShowPrompt(CurrentHoldTarget.HoverPrompt);
                return;
            }

            SetHighlight(CurrentInteractionTarget, true);

            if (CurrentInteractionTarget is IInteractionFocusReceiver)
            {
                // Project-owned focus receivers own their complete focus
                // presentation, including any specialized prompt.
                interactionUI?.HidePrompt();
                return;
            }

            interactionUI?.ShowPrompt(
                $"{promptPrefix}{CurrentInteractionTarget.DisplayName}"
            );
        }

        private Transform ResolveInteractionOrigin()
        {
            if (interactionOrigin != null)
            {
                return interactionOrigin;
            }

            if (!useActivePlayerAsOrigin ||
                PlayerManager.Instance == null ||
                PlayerManager.Instance.Player == null)
            {
                return null;
            }

            return PlayerManager.Instance.Player.transform;
        }

        private Transform ResolveIgnoredRoot()
        {
            if (ignoredRoot != null)
            {
                return ignoredRoot;
            }

            if (!ignoreActivePlayerColliders ||
                PlayerManager.Instance == null ||
                PlayerManager.Instance.Player == null)
            {
                return null;
            }

            return PlayerManager.Instance.Player.transform;
        }

        private bool IsHitWithinInteractionOriginRange(
            RaycastHit hit,
            Transform resolvedInteractionOrigin)
        {
            if (resolvedInteractionOrigin == null)
            {
                return false;
            }

            return Vector3.Distance(
                resolvedInteractionOrigin.position,
                hit.point
            ) <= maximumInteractionOriginDistance;
        }

        private void UpdateHoldInteraction()
        {
            if (activeHoldTarget == null)
            {
                return;
            }

            bool isHeld = GameInputManager.Instance != null &&
                GameInputManager.Instance.InteractHeld;

            bool isStartingThisFrame = holdStartedFrame == Time.frameCount;
            if ((!isHeld && !isStartingThisFrame) ||
                activeHoldTarget != CurrentHoldTarget)
            {
                StopHold();
                return;
            }

            if (!activeHoldTarget.Advance(Time.deltaTime))
            {
                StopHold();
                return;
            }

            interactionUI?.ShowProgress(
                activeHoldTarget.ProgressName,
                activeHoldTarget.Progress);

            if (activeHoldTarget.IsComplete)
            {
                StopHold();
                RefreshTarget();
            }
        }

        private void StopHold()
        {
            activeHoldTarget?.Cancel();
            activeHoldTarget = null;
            holdStartedFrame = -1;
            interactionUI?.HideProgress();
        }

        /// <summary>
        /// Enables or disables highlighting on an interactable when available.
        /// </summary>
        private static void SetHighlight(
            IInteractionTarget interactable,
            bool highlighted)
        {
            if (interactable == null)
            {
                return;
            }

            if (interactable is IInteractionFocusReceiver focusReceiver)
            {
                focusReceiver.SetInteractionFocused(highlighted);
                return;
            }

            Transform targetTransform = interactable.InteractionTransform;
            if (targetTransform == null)
            {
                return;
            }

            InteractionHighlighter highlighter =
                targetTransform.GetComponentInChildren<InteractionHighlighter>();

            highlighter?.SetHighlighted(highlighted);
        }

        private static void SetHighlight(
            HoldInteractable interactable,
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

        private static void SetHighlight(
            ActivatedProgressInteractable interactable,
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

        private void OnDisable()
        {
            StopHold();
            SetHighlight(CurrentInteractionTarget, false);
            SetHighlight(CurrentHoldTarget, false);
            SetHighlight(CurrentProgressTarget, false);
            CurrentInteractionTarget = null;
            CurrentTarget = null;
            CurrentHoldTarget = null;
            CurrentProgressTarget = null;
            interactionUI?.HidePrompt();
            interactionUI?.HideProgress();
        }
    }
}
