using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>Controls which solid collisions are reported during a deliberate throw.</summary>
    public enum ThrowImpactEventPolicy
    {
        /// <summary>Report only the first valid impact of each throw.</summary>
        FirstImpactOnly,

        /// <summary>Report each new impact until the Rigidbody settles or the object is grabbed again.</summary>
        EveryImpactUntilSleeping
    }

    /// <summary>Snapshot of a deliberately thrown object's impact for scene and game-specific listeners.</summary>
    public readonly struct ThrowImpactEvent
    {
        internal ThrowImpactEvent(
            Grabbable grabbable,
            Interactable interactable,
            GameObject thrower,
            Collision collision)
        {
            ContactPoint contact = collision.GetContact(0);
            Grabbable = grabbable;
            Interactable = interactable;
            ObjectIdentifier = interactable.DisplayName;
            Thrower = thrower;
            HitObject = collision.gameObject;
            HitCollider = collision.collider;
            Point = contact.point;
            Normal = contact.normal;
            RelativeVelocity = collision.relativeVelocity;
            Impulse = collision.impulse;
        }

        /// <summary>Gets the object that was deliberately thrown.</summary>
        public Grabbable Grabbable { get; }

        /// <summary>Gets the originating interaction for exact object identity.</summary>
        public Interactable Interactable { get; }

        /// <summary>Gets the interaction display name at the time of impact.</summary>
        public string ObjectIdentifier { get; }

        /// <summary>Gets the actor responsible for the throw.</summary>
        public GameObject Thrower { get; }

        /// <summary>Gets the GameObject reported by Unity for the object struck.</summary>
        public GameObject HitObject { get; }

        /// <summary>Gets the specific collider struck, useful for identifying a character in its hierarchy.</summary>
        public Collider HitCollider { get; }

        /// <summary>Gets the first contact position in world space.</summary>
        public Vector3 Point { get; }

        /// <summary>Gets the surface normal of the first contact in world space.</summary>
        public Vector3 Normal { get; }

        /// <summary>Gets the relative linear velocity at impact.</summary>
        public Vector3 RelativeVelocity { get; }

        /// <summary>Gets the total physics impulse applied to resolve the collision.</summary>
        public Vector3 Impulse { get; }
    }
}
