using QuietStatic.Toolkit.Interactions;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Interactions
{
    /// <summary>
    /// Evaluates whether an interaction target owns a 3D Collider that the
    /// camera-based <see cref="Interactor"/> can resolve back to that target.
    /// </summary>
    public static class InteractionTargetColliderUtility
    {
        /// <summary>
        /// Checks the target and its descendants for a solid or trigger Collider whose
        /// nearest interaction-bearing ancestor is the supplied target.
        /// </summary>
        /// <remarks>
        /// Inactive and disabled Colliders count as authoring-time configuration because
        /// staged interactions may enable them at runtime. Parent Colliders do not count.
        /// </remarks>
        public static bool HasRaycastCollider(Component interactionTarget)
        {
            if (interactionTarget == null)
            {
                return false;
            }

            Transform targetTransform = interactionTarget.transform;
            foreach (Collider collider in
                     interactionTarget.GetComponentsInChildren<Collider>(true))
            {
                if (IsOwnedBy(collider, targetTransform))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOwnedBy(Collider collider, Transform targetTransform)
        {
            Transform current = collider != null ? collider.transform : null;
            while (current != null)
            {
                if (IsInteractionOwner(current))
                {
                    return current == targetTransform;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool IsInteractionOwner(Transform candidate) =>
            candidate.GetComponent<IInteractionTarget>() != null ||
            candidate.GetComponent<HoldInteractable>() != null ||
            candidate.GetComponent<ActivatedProgressInteractable>() != null;
    }
}
