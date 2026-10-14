using UnityEngine;

namespace QuietStatic.Toolkit.Audio
{
    /// <summary>Plays positional footsteps from actual NPC travel, including scripted movement.</summary>
    [DisallowMultipleComponent]
    public sealed class NPCFootsteps : MonoBehaviour
    {
        [RequiredCommandChannel]
        [Tooltip("Persistent audio channel receiving positional footstep requests.")]
        [SerializeField] private AudioRequestChannel requestChannel;
        [Tooltip("Footstep variations selected at random for each step.")]
        [SerializeField] private AudioClip[] footstepClips;
        [Tooltip("Optional sound origin. Defaults to the NPC root.")]
        [SerializeField] private Transform soundOrigin;
        [Range(0f, 1f)]
        [Tooltip("Volume of each footstep.")]
        [SerializeField] private float volume = 0.4f;
        [Min(0.01f)]
        [Tooltip("Horizontal distance between steps. 0.99 gives the player's 0.55-second cadence at 1.8 units per second.")]
        [SerializeField] private float stepDistance = 0.99f;
        [Min(0f)]
        [Tooltip("Movement below this speed is treated as stationary.")]
        [SerializeField] private float minimumSpeed = 0.1f;
        [Min(0.01f)]
        [Tooltip("Single-frame travel above this distance is treated as a teleport and makes no sound.")]
        [SerializeField] private float teleportDistance = 2f;

        private Vector3 previousPosition;
        private float travelledDistance;

        private void OnEnable()
        {
            previousPosition = transform.position;
            travelledDistance = 0f;
        }

        private void LateUpdate() => TrackMovement(Time.deltaTime);

        private void TrackMovement(float deltaTime)
        {
            Vector3 displacement = transform.position - previousPosition;
            previousPosition = transform.position;
            float distance = new Vector2(displacement.x, displacement.z).magnitude;
            if (deltaTime <= 0f || displacement.magnitude > teleportDistance ||
                distance <= minimumSpeed * deltaTime)
            {
                travelledDistance = 0f;
                return;
            }

            travelledDistance += distance;
            float stride = Mathf.Max(0.01f, stepDistance);
            if (travelledDistance < stride) return;
            travelledDistance %= stride;
            PlayFootstep();
        }

        private void PlayFootstep()
        {
            if (requestChannel == null || footstepClips == null || footstepClips.Length == 0)
                return;

            int start = Random.Range(0, footstepClips.Length);
            for (int offset = 0; offset < footstepClips.Length; offset++)
            {
                AudioClip clip = footstepClips[(start + offset) % footstepClips.Length];
                if (clip == null) continue;
                requestChannel.PlayAtPosition(clip,
                    soundOrigin != null ? soundOrigin.position : transform.position,
                    1f, 15f, volume);
                return;
            }
        }

        /// <summary>Assigns the persistent audio command channel.</summary>
        public void SetRequestChannel(AudioRequestChannel value) => requestChannel = value;
    }
}
