using UnityEngine;

namespace QuietStatic.Animation.Environment
{
    /// <summary>
    /// Continuously rotates this GameObject every frame.
    /// </summary>
    /// <remarks>
    /// This component is useful for simple ambient animation such as spinning pickups,
    /// rotating props, fan blades, warning lights, puzzle objects, or decorative scene elements.
    ///
    /// The rotation speed is stored as degrees per second for each local axis.
    /// For example, a value of <c>(0, 90, 0)</c> rotates the object 90 degrees per second around its Y axis.
    /// </remarks>
    public class Rotator : MonoBehaviour
    {
        [Header("Rotation Settings")]
        [Tooltip("Rotation speed in degrees per second for each axis. X rotates around the local X axis, Y around the local Y axis, and Z around the local Z axis.")]
        [SerializeField] private Vector3 rotationSpeed = new Vector3(15f, 30f, 45f);

        [Tooltip("Determines whether the object rotates in local space or world space. Local space is usually best for spinning props and pickups.")]
        [SerializeField] private Space rotationSpace = Space.Self;

        /// <summary>
        /// Rotates the object once per rendered frame.
        /// </summary>
        /// <remarks>
        /// <see cref="Time.deltaTime"/> is used so the rotation speed remains consistent
        /// regardless of framerate. Without multiplying by delta time, the object would rotate
        /// faster on machines running at higher frames per second.
        /// </remarks>
        private void Update()
        {
            RotateObject();
        }

        /// <summary>
        /// Applies this frame's rotation using the configured rotation speed and rotation space.
        /// </summary>
        private void RotateObject()
        {
            transform.Rotate(rotationSpeed * Time.deltaTime, rotationSpace);
        }
    }
}
