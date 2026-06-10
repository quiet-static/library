using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>
    /// Applies a continuous Perlin-noise flicker effect to a Unity <see cref="Light" />.
    /// </summary>
    /// <remarks>
    /// This component is useful for cinematic or atmospheric lights that should feel unstable
    /// without fully turning on and off. Unlike a timed flicker, this uses smooth noise so the
    /// intensity drifts naturally between the configured minimum and maximum values.
    /// </remarks>
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The light whose intensity should be animated. If left empty, the Light on this GameObject will be used.")]
        [SerializeField] private Light targetLight;

        [Header("Intensity Range")]
        [Tooltip("The lowest intensity the light can reach while flickering.")]
        [SerializeField] private float minIntensity = 0.4f;

        [Tooltip("The highest intensity the light can reach while flickering.")]
        [SerializeField] private float maxIntensity = 1.2f;

        [Header("Noise Settings")]
        [Tooltip("How quickly the Perlin-noise flicker changes over time. Higher values flicker faster.")]
        [Min(0f)]
        [SerializeField] private float noiseSpeed = 10f;

        [Tooltip("If true, each instance starts at a different noise offset so multiple lights do not flicker in sync.")]
        [SerializeField] private bool randomizeOffset = true;

        /// <summary>
        /// Offset used as one axis of the Perlin-noise sample.
        /// </summary>
        /// <remarks>
        /// Randomizing this value lets multiple lights use the same settings while producing
        /// different flicker patterns.
        /// </remarks>
        private float offset;

        /// <summary>
        /// Auto-fills the target light when this component is added or reset in the Inspector.
        /// </summary>
        private void Reset()
        {
            targetLight = GetComponent<Light>();
        }

        /// <summary>
        /// Ensures a target light is assigned and initializes the noise offset.
        /// </summary>
        private void Awake()
        {
            if (targetLight == null)
            {
                targetLight = GetComponent<Light>();
            }

            offset = randomizeOffset ? Random.Range(0f, 1000f) : 0f;
        }

        /// <summary>
        /// Updates the light intensity each frame using a smooth Perlin-noise value.
        /// </summary>
        private void Update()
        {
            if (targetLight == null)
            {
                return;
            }

            ApplyFlickerIntensity();
        }

        /// <summary>
        /// Samples Perlin noise and applies the resulting intensity to the target light.
        /// </summary>
        private void ApplyFlickerIntensity()
        {
            float noise = Mathf.PerlinNoise(offset, Time.time * noiseSpeed);
            targetLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
        }
    }
}
