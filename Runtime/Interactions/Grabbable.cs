using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>Holds, drops, and throws a physical object picked up through an Interactable.</summary>
    [AddComponentMenu("Quiet Static Toolkit/Interactions/Grabbable")]
    [DisallowMultipleComponent]
    public sealed class Grabbable : MonoBehaviour
    {
        private const float HolderCollisionGrace = 0.15f;

        [Header("Hold Pose")]
        [Tooltip("Position relative to the player's hold transform.")]
        [SerializeField] private Vector3 holdPositionOffset;

        [Tooltip("Euler rotation in degrees relative to the player's hold transform.")]
        [SerializeField] private Vector3 holdRotationOffset;

        [Header("Throw")]
        [Tooltip("Impulse at zero charge, in newton-seconds.")]
        [Min(0f)]
        [SerializeField] private float minThrowForce = 2f;

        [Tooltip("Impulse at full charge, or the fixed impulse when charging is off, in newton-seconds.")]
        [Min(0f)]
        [SerializeField] private float maxThrowForce = 8f;

        [Tooltip("Hold the throw input to build strength and release it to throw.")]
        [SerializeField] private bool useChargeThrow = true;

        [Tooltip("Seconds to reach full strength. Zero makes a charged throw reach full strength immediately.")]
        [Min(0f)]
        [SerializeField] private float chargeDuration = 0.75f;

        [Tooltip("Euler rotation in degrees applied relative to the aim direction.")]
        [SerializeField] private Vector3 throwDirectionOffset;

        [Header("Throw Impacts")]
        [Tooltip("Publish typed impact notifications for deliberate throws.")]
        [SerializeField] private bool publishThrowCollisionEvents = true;

        [Tooltip("Control how many collisions may be reported for one throw.")]
        [SerializeField] private ThrowImpactEventPolicy impactEventPolicy;

        private readonly List<(Collider Item, Collider Holder)> ignoredColliders = new();
        private Interactable interaction;
        private Rigidbody body;
        private Rigidbody heldBody;
        private Rigidbody thrownBody;
        private RigidbodyInterpolation originalInterpolation;
        private CollisionDetectionMode originalCollisionDetection;
        private Transform holdTransform;
        private Transform originalParent;
        private Scene originalScene;
        private GameObject holder;
        private GameObject thrower;
        private float restoreHolderCollisionsAt;
        private bool reportedImpact;

        /// <summary>Raised after the object is released, including when it is disabled while held.</summary>
        public event Action<Grabbable> Released;

        /// <summary>Raised for impacts allowed by this object's throw collision settings.</summary>
        public event Action<ThrowImpactEvent> ThrowImpacted;

        /// <summary>Gets whether this object is attached to a player's hold transform.</summary>
        public bool IsHeld { get; private set; }

        /// <summary>Gets whether this object is still in motion from a deliberate throw.</summary>
        public bool IsThrown => thrownBody != null;

        /// <summary>Gets whether the player is currently charging a throw.</summary>
        public bool IsCharging { get; private set; }

        /// <summary>Gets the current throw strength on a scale from zero to one.</summary>
        public float ChargeProgress { get; private set; }

        /// <summary>Gets whether the throw input charges before release.</summary>
        public bool UseChargeThrow => useChargeThrow;

        private float MinThrowForce => NonnegativeFinite(minThrowForce);
        private float MaxThrowForce => Mathf.Max(MinThrowForce, NonnegativeFinite(maxThrowForce));
        private float ChargeDuration => NonnegativeFinite(chargeDuration);

        private bool IsAvailable => isActiveAndEnabled && interaction != null &&
            interaction.isActiveAndEnabled && interaction.IsGrabbable;

        private bool HasValidHold => IsHeld && IsAvailable && heldBody != null &&
            holdTransform != null && transform.parent == holdTransform;

        private void Awake() => TryGetComponent(out interaction);

        private void OnValidate()
        {
            minThrowForce = MinThrowForce;
            maxThrowForce = MaxThrowForce;
            chargeDuration = ChargeDuration;
        }

        /// <summary>Checks whether an active hold transform and a dynamic Rigidbody are available for a pickup.</summary>
        public bool CanGrab(Transform anchor)
        {
            if (interaction == null) TryGetComponent(out interaction);
            if (body == null) TryGetComponent(out body);

            return IsAvailable && !IsHeld && anchor != null &&
                anchor.gameObject.activeInHierarchy && anchor != transform && !anchor.IsChildOf(transform) &&
                body != null && !body.isKinematic && body.detectCollisions;
        }

        /// <summary>Attaches the object to the supplied hold transform and suspends its physics.</summary>
        /// <param name="anchor">Player hand or another transform used to carry the object.</param>
        /// <param name="actor">The player's root GameObject used for attribution and collision exclusion.</param>
        /// <returns>True if the object was picked up.</returns>
        public bool TryGrab(Transform anchor, GameObject actor)
        {
            if (!CanGrab(anchor)) return false;
            if (actor != null && actor.transform.IsChildOf(transform)) return false;

            RestoreAttachment();
            RestoreHolderCollisions();
            ClearThrow();

            holder = actor != null ? actor : anchor.gameObject;
            holdTransform = anchor;
            originalParent = transform.parent;
            if (originalParent != null && originalParent.IsChildOf(holder.transform))
            {
                originalParent = null;
            }
            originalScene = gameObject.scene;
            heldBody = body;
            originalInterpolation = body.interpolation;
            originalCollisionDetection = body.collisionDetectionMode;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            body.detectCollisions = false;

            IsHeld = true;
            transform.SetParent(anchor, true);
            if (!HasValidHold)
            {
                Drop();
                return false;
            }

            transform.SetLocalPositionAndRotation(holdPositionOffset, Quaternion.Euler(holdRotationOffset));
            return IsHeld;
        }

        /// <summary>Starts charging a held object and reports whether a new charge began.</summary>
        public bool BeginCharge()
        {
            if (!HasValidHold || !UseChargeThrow || IsCharging) return false;

            IsCharging = true;
            ChargeProgress = ChargeDuration > 0f ? 0f : 1f;
            return true;
        }

        /// <summary>Advances an active charge by the supplied number of gameplay seconds.</summary>
        public void AdvanceCharge(float deltaTime)
        {
            if (!IsCharging) return;

            if (!HasValidHold)
            {
                Drop();
                return;
            }

            float duration = ChargeDuration;
            ChargeProgress = duration > 0f
                ? Mathf.Clamp01(ChargeProgress + NonnegativeFinite(deltaTime) / duration)
                : 1f;
        }

        /// <summary>Clears the pending charge without releasing the object.</summary>
        public void CancelCharge()
        {
            IsCharging = false;
            ChargeProgress = 0f;
        }

        /// <summary>Releases the held object without applying a throw impulse.</summary>
        /// <returns>True if an object was released.</returns>
        public bool Drop() => Release(false, Vector3.zero);

        /// <summary>Releases the held object with a physics impulse based on its current charge settings.</summary>
        /// <param name="forward">World-space direction from which to apply the configured aim offset.</param>
        /// <returns>True if the object was thrown.</returns>
        public bool Throw(Vector3 forward)
        {
            if (!HasValidHold || NonnegativeFinite(forward.sqrMagnitude) == 0f) return false;

            Vector3 direction = Quaternion.LookRotation(forward) *
                Quaternion.Euler(throwDirectionOffset) * Vector3.forward;
            float force = UseChargeThrow
                ? Mathf.Lerp(MinThrowForce, MaxThrowForce, ChargeProgress)
                : MaxThrowForce;
            return Release(true, direction * force);
        }

        /// <summary>Clears throw state and releases any held object when the owning interaction is disabled.</summary>
        internal void Deactivate()
        {
            ClearThrow();
            Drop();
        }

        private bool Release(bool isThrow, Vector3 impulse)
        {
            if (!IsHeld) return false;

            Rigidbody releasedBody = heldBody;
            GameObject releasingActor = holder;
            IsHeld = false;
            heldBody = null;
            holder = null;
            CancelCharge();
            RestoreAttachment();

            bool didThrow = false;
            if (releasedBody != null)
            {
                releasedBody.position = transform.position;
                releasedBody.rotation = transform.rotation;
                releasedBody.isKinematic = false;
                releasedBody.collisionDetectionMode = originalCollisionDetection;
                releasedBody.interpolation = originalInterpolation;
                releasedBody.detectCollisions = true;
                IgnoreHolderCollisions(releasedBody, releasingActor);

                if (isThrow && gameObject.activeInHierarchy)
                {
                    thrownBody = releasedBody;
                    thrower = releasingActor;
                    reportedImpact = false;
                    releasedBody.AddForce(impulse, ForceMode.Impulse);
                    didThrow = true;
                }
            }

            Released?.Invoke(this);
            return !isThrow || didThrow;
        }

        private void RestoreAttachment()
        {
            if (holdTransform != null && transform.parent == holdTransform)
            {
                // Unity disallows changing parents during an ancestor's activation callback.
                // If the holder was deactivated, detach on the first physics tick after reactivation.
                if (!holdTransform.gameObject.activeInHierarchy) return;

                Transform parent = originalParent;
                if (parent != null && (!parent.gameObject.activeInHierarchy || parent.IsChildOf(transform)))
                {
                    parent = null;
                }

                transform.SetParent(parent, true);
                if (parent == null && originalScene.IsValid() && originalScene.isLoaded &&
                    gameObject.scene != originalScene)
                {
                    SceneManager.MoveGameObjectToScene(gameObject, originalScene);
                }
            }

            holdTransform = null;
            originalParent = null;
            originalScene = default;
        }

        private void FixedUpdate()
        {
            if (IsHeld && !HasValidHold) Drop();
            if (!IsHeld && holdTransform != null) RestoreAttachment();
            if (IsThrown && thrownBody.IsSleeping()) ClearThrow();
            if (ignoredColliders.Count != 0 && Time.fixedTime >= restoreHolderCollisionsAt)
            {
                RestoreHolderCollisions(waitForSeparation: true);
            }
        }

        private void OnDisable()
        {
            Deactivate();
            RestoreHolderCollisions();
        }

        private void IgnoreHolderCollisions(Rigidbody releasedBody, GameObject actor)
        {
            if (actor == null || !gameObject.activeInHierarchy) return;

            Collider[] actorColliders = actor.GetComponentsInChildren<Collider>();
            foreach (Collider item in GetComponentsInChildren<Collider>())
            {
                if (!IsActiveCollider(item) || item.attachedRigidbody != releasedBody || item.isTrigger) continue;

                foreach (Collider actorCollider in actorColliders)
                {
                    if (!IsActiveCollider(actorCollider) || actorCollider.isTrigger ||
                        actorCollider.transform.IsChildOf(transform) ||
                        Physics.GetIgnoreCollision(item, actorCollider)) continue;

                    Physics.IgnoreCollision(item, actorCollider);
                    ignoredColliders.Add((item, actorCollider));
                }
            }

            restoreHolderCollisionsAt = Time.fixedTime + HolderCollisionGrace;
        }

        private void RestoreHolderCollisions(bool waitForSeparation = false)
        {
            for (int i = ignoredColliders.Count - 1; i >= 0; i--)
            {
                (Collider item, Collider actor) = ignoredColliders[i];
                if (IsActiveCollider(item) && IsActiveCollider(actor))
                {
                    if (waitForSeparation && item.bounds.Intersects(actor.bounds)) continue;
                    Physics.IgnoreCollision(item, actor, false);
                }

                ignoredColliders.RemoveAt(i);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsAvailable || !IsThrown || !publishThrowCollisionEvents ||
                (reportedImpact && impactEventPolicy != ThrowImpactEventPolicy.EveryImpactUntilSleeping) ||
                collision.contactCount == 0) return;

            Collider hitCollider = collision.collider;
            if (hitCollider == null || (thrower != null && hitCollider.transform.IsChildOf(thrower.transform))) return;

            reportedImpact = true;
            ThrowImpacted?.Invoke(new ThrowImpactEvent(this, interaction, thrower, collision));
        }

        private void ClearThrow()
        {
            thrownBody = null;
            thrower = null;
            reportedImpact = false;
        }

        private static bool IsActiveCollider(Collider collider) =>
            collider != null && collider.enabled && collider.gameObject.activeInHierarchy;

        private static float NonnegativeFinite(float value) =>
            value > 0f && !float.IsInfinity(value) ? value : 0f;
    }
}
