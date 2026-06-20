using System.Collections;
using UnityEngine;

namespace QuietStatic.Toolkit.Animation.Environment
{
    /// <summary>
    /// Creates an atmospheric, randomized flickering effect for a Unity <see cref="Light"/>.
    /// </summary>
    /// <remarks>
    /// This component is intended for horror/environmental lighting effects such as
    /// failing fluorescent lights, unstable emergency lights, haunted hallways, or
    /// other props that should occasionally dim or blink out.
    ///
    /// The flicker loop waits a random amount of time, performs one short flicker,
    /// then restores the light back to its previous enabled/intensity state.
    ///
    /// A <see cref="Light"/> component is required on the same GameObject, but a
    /// different light can also be assigned manually through <see cref="targetLight"/>.
    /// </remarks>
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        [Header("Light Reference")]
        [Tooltip("The light that will flicker. If left empty, this script will automatically use the Light component on the same GameObject.")]
        [SerializeField] private Light targetLight;

        [Header("Base Intensity")]
        [Tooltip("The intensity the light should return to when flickering stops. This is automatically overwritten in Awake using the target light's starting intensity.")]
        [SerializeField] private float normalIntensity = 1f;

        [Header("Dim Flicker Intensity")]
        [Tooltip("The lowest intensity the light can randomly dim to during a non-off flicker.")]
        [SerializeField] private float minimumDimIntensity = 0.2f;

        [Tooltip("The highest intensity the light can randomly dim to during a non-off flicker.")]
        [SerializeField] private float maximumDimIntensity = 0.7f;

        [Header("Time Between Flickers")]
        [Tooltip("The shortest amount of time, in seconds, to wait before the next flicker can happen.")]
        [SerializeField] private float minimumTimeBetweenFlickers = 1f;

        [Tooltip("The longest amount of time, in seconds, to wait before the next flicker can happen.")]
        [SerializeField] private float maximumTimeBetweenFlickers = 5f;

        [Header("Flicker Duration")]
        [Tooltip("The shortest amount of time, in seconds, that a single flicker can last.")]
        [SerializeField] private float minimumFlickerDuration = 0.03f;

        [Tooltip("The longest amount of time, in seconds, that a single flicker can last.")]
        [SerializeField] private float maximumFlickerDuration = 0.2f;

        [Header("Off Flicker Behavior")]
        [Tooltip("When enabled, some flickers can disable the light completely instead of only dimming it.")]
        [SerializeField] private bool canTurnOffCompletely = true;

        [Tooltip("The chance that a flicker will fully turn the light off instead of dimming it. 0 means never, 1 means always.")]
        [Range(0f, 1f)]
        [SerializeField] private float turnOffChance = 0.35f;

        [Header("Startup Behavior")]
        [Tooltip("When enabled, the light begins flickering automatically whenever this component is enabled.")]
        [SerializeField] private bool flickerOnStart = true;

        /// <summary>
        /// Stores the currently running flicker coroutine.
        /// </summary>
        /// <remarks>
        /// This is used as a guard so multiple flicker loops cannot be started at
        /// the same time on the same component.
        /// </remarks>
        private Coroutine flickerRoutine;

        /// <summary>
        /// Initializes the light reference and records the light's starting intensity.
        /// </summary>
        /// <remarks>
        /// If <see cref="targetLight"/> has not been assigned in the Inspector, this
        /// method falls back to the <see cref="Light"/> component on the same
        /// GameObject. Because the class uses <see cref="RequireComponent"/>, that
        /// fallback should normally be available.
        ///
        /// The current light intensity is stored in <see cref="normalIntensity"/> so
        /// the light can be restored when flickering stops or when the component is
        /// disabled.
        /// </remarks>
        private void Awake()
        {
            if (targetLight == null)
            {
                targetLight = GetComponent<Light>();
            }

            normalIntensity = targetLight.intensity;
        }

        /// <summary>
        /// Starts the flicker loop when the component becomes enabled, if automatic
        /// startup is allowed.
        /// </summary>
        private void OnEnable()
        {
            if (flickerOnStart)
            {
                StartFlickering();
            }
        }

        /// <summary>
        /// Stops any active flicker loop and restores the light when the component is
        /// disabled.
        /// </summary>
        /// <remarks>
        /// This prevents the light from remaining stuck in a dimmed or disabled
        /// state if the GameObject or component is turned off in the middle of a
        /// flicker.
        /// </remarks>
        private void OnDisable()
        {
            StopFlickering();
        }

        /// <summary>
        /// Starts the randomized flickering loop.
        /// </summary>
        /// <remarks>
        /// Calling this method while the loop is already running has no effect. This
        /// makes it safe for other scripts, UnityEvents, or trigger volumes to call
        /// this method repeatedly without creating duplicate coroutines.
        /// </remarks>
        public void StartFlickering()
        {
            if (flickerRoutine != null)
            {
                return;
            }

            flickerRoutine = StartCoroutine(FlickerRoutine());
        }

        /// <summary>
        /// Stops the randomized flickering loop and restores the light to its normal
        /// state.
        /// </summary>
        /// <remarks>
        /// This method is safe to call even when the light is not currently
        /// flickering. It always attempts to restore the light afterward.
        /// </remarks>
        public void StopFlickering()
        {
            if (flickerRoutine != null)
            {
                StopCoroutine(flickerRoutine);
                flickerRoutine = null;
            }

            RestoreLight();
        }

        /// <summary>
        /// Runs the main flicker loop.
        /// </summary>
        /// <returns>
        /// An enumerator used by Unity's coroutine system.
        /// </returns>
        /// <remarks>
        /// Each loop iteration waits a randomized amount of time between
        /// <see cref="minimumTimeBetweenFlickers"/> and
        /// <see cref="maximumTimeBetweenFlickers"/>, then performs a single flicker
        /// through <see cref="FlickerOnce"/>.
        /// </remarks>
        private IEnumerator FlickerRoutine()
        {
            while (true)
            {
                float waitTime = Random.Range(
                    minimumTimeBetweenFlickers,
                    maximumTimeBetweenFlickers
                );

                yield return new WaitForSeconds(waitTime);

                yield return FlickerOnce();
            }
        }

        /// <summary>
        /// Performs one short flicker.
        /// </summary>
        /// <returns>
        /// An enumerator used by Unity's coroutine system.
        /// </returns>
        /// <remarks>
        /// A flicker can either:
        /// - Disable the light completely, if <see cref="canTurnOffCompletely"/> is
        /// enabled and the random chance succeeds.
        /// - Dim the light to a random intensity between
        /// <see cref="minimumDimIntensity"/> and <see cref="maximumDimIntensity"/>.
        ///
        /// After the randomized duration passes, the light is enabled again and its
        /// previous intensity is restored.
        /// </remarks>
        private IEnumerator FlickerOnce()
        {
            float originalIntensity = targetLight.intensity;

            bool shouldTurnOff = canTurnOffCompletely && Random.value <= turnOffChance;

            if (shouldTurnOff)
            {
                targetLight.enabled = false;
            }
            else
            {
                targetLight.intensity = Random.Range(
                    minimumDimIntensity,
                    maximumDimIntensity
                );
            }

            float flickerDuration = Random.Range(
                minimumFlickerDuration,
                maximumFlickerDuration
            );

            yield return new WaitForSeconds(flickerDuration);

            targetLight.enabled = true;
            targetLight.intensity = originalIntensity;
        }

        /// <summary>
        /// Restores the light to its normal enabled state and recorded base
        /// intensity.
        /// </summary>
        /// <remarks>
        /// This is used when flickering stops or the component is disabled. If the
        /// target light reference is missing, this method exits safely.
        /// </remarks>
        private void RestoreLight()
        {
            if (targetLight == null)
            {
                return;
            }

            targetLight.enabled = true;
            targetLight.intensity = normalIntensity;
        }
    }
}
