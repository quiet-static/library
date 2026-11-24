using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>Resolves the interaction-bearing hierarchy under the center of the camera.</summary>
    internal sealed class InteractionTargetSelection
    {
        private readonly RaycastHit[] raycastHits = new RaycastHit[32];

        internal void Find(
            Interactor interactor,
            Camera camera,
            float range,
            LayerMask interactionMask,
            Transform heldRoot,
            Transform ignoredRoot,
            Transform interactionOrigin,
            float? maximumReach,
            out IInteractionTarget interactable,
            out HoldInteractable holdInteractable,
            out ActivatedProgressInteractable progressInteractable)
        {
            interactable = null;
            holdInteractable = null;
            progressInteractable = null;

            if (camera == null)
            {
                return;
            }

            Ray ray = camera.ViewportPointToRay(
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
            bool hasNonInteractiveOccluder = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = raycastHits[i];

                if (heldRoot != null &&
                    hit.collider.transform.IsChildOf(heldRoot))
                {
                    continue;
                }

                if (ignoredRoot != null &&
                    hit.collider.transform.IsChildOf(ignoredRoot))
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
                    if (!hit.collider.isTrigger && selectedOwner == null)
                    {
                        hasNonInteractiveOccluder = true;
                    }

                    continue;
                }

                if (!HasTargetableInteractionStage(
                        interactor,
                        hitInteractable,
                        hitHoldInteractable,
                        hitProgressInteractable))
                {
                    // Disabled interactions still have raycast colliders. Until they become
                    // targetable, treat them like ordinary geometry so they cannot claim owner
                    // selection and hide an explicitly pass-through target behind them.
                    if (!hit.collider.isTrigger && selectedOwner == null)
                    {
                        hasNonInteractiveOccluder = true;
                    }

                    continue;
                }

                if (hasNonInteractiveOccluder &&
                    !AllowsTargetingThroughOccluders(hitInteractable))
                {
                    continue;
                }

                if (maximumReach.HasValue &&
                    (interactionOrigin == null ||
                     !(Vector3.Distance(interactionOrigin.position, hit.point) <= maximumReach.Value)))
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

        private static bool AllowsTargetingThroughOccluders(
            IInteractionTarget interactionTarget)
        {
            return interactionTarget is Interactable interactable &&
                   interactable.AllowTargetingThroughOccluders;
        }

        private static bool HasTargetableInteractionStage(
            Interactor interactor,
            IInteractionTarget interactionTarget,
            HoldInteractable holdInteraction,
            ActivatedProgressInteractable progressInteraction)
        {
            if (progressInteraction != null &&
                (progressInteraction.IsAvailable || progressInteraction.IsRunning))
            {
                return true;
            }

            if (holdInteraction != null &&
                holdInteraction.IsEnabled &&
                holdInteraction.IsInteractorTargetingEnabled)
            {
                return true;
            }

            return interactionTarget != null &&
                   interactionTarget.IsInteractionAvailable(interactor);
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

    }
}
