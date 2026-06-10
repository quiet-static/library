using UnityEngine;
using UnityEngine.UI;

namespace UnityStandardAssets.Utility
{
    /// <summary>
    /// Displays a simple frames-per-second counter using a legacy Unity UI Text component.
    /// </summary>
    /// <remarks>
    /// This component measures how many frames are rendered during a short sample window,
    /// converts that sample into an approximate FPS value, and writes the result into the
    /// assigned <see cref="Text"/> component.
    ///
    /// This script uses <see cref="Time.realtimeSinceStartup"/> instead of scaled game time,
    /// so the FPS display can continue updating correctly even if <see cref="Time.timeScale"/>
    /// is changed for pause menus, slow motion, or cutscenes.
    /// </remarks>
    [RequireComponent(typeof(Text))]
    public class FPSCounter : MonoBehaviour
    {
        [Header("Display")]
        [Tooltip("Text component that receives the formatted FPS value. Auto-filled from this GameObject if left empty.")]
        [SerializeField] private Text fpsText;

        [Tooltip("String format used when writing the FPS value. {0} is replaced with the measured FPS number.")]
        [SerializeField] private string displayFormat = "{0} FPS";

        [Header("Sampling")]
        [Tooltip("How often, in seconds, the FPS value should be recalculated and displayed. Smaller values update faster but fluctuate more.")]
        [Min(0.01f)]
        [SerializeField] private float fpsMeasurePeriod = 0.5f;

        /// <summary>
        /// Number of frames counted during the current FPS sampling period.
        /// </summary>
        private int fpsAccumulator;

        /// <summary>
        /// Realtime timestamp at which the next FPS sample should be finalized.
        /// </summary>
        private float fpsNextPeriod;

        /// <summary>
        /// Most recently calculated frames-per-second value.
        /// </summary>
        private int currentFps;

        /// <summary>
        /// Attempts to auto-fill the required UI Text reference when the component is
        /// first added or reset in the Inspector.
        /// </summary>
        private void Reset()
        {
            fpsText = GetComponent<Text>();
        }

        /// <summary>
        /// Initializes the first measurement window and resolves the target Text component.
        /// </summary>
        private void Start()
        {
            if (fpsText == null)
            {
                fpsText = GetComponent<Text>();
            }

            fpsNextPeriod = Time.realtimeSinceStartup + fpsMeasurePeriod;
        }

        /// <summary>
        /// Counts rendered frames and refreshes the displayed FPS value whenever the
        /// current measurement period has elapsed.
        /// </summary>
        private void Update()
        {
            fpsAccumulator++;

            if (Time.realtimeSinceStartup > fpsNextPeriod)
            {
                RefreshFpsDisplay();
            }
        }

        /// <summary>
        /// Calculates the average FPS for the completed measurement window and writes
        /// the formatted result to the assigned Text component.
        /// </summary>
        private void RefreshFpsDisplay()
        {
            currentFps = Mathf.RoundToInt(fpsAccumulator / fpsMeasurePeriod);
            fpsAccumulator = 0;
            fpsNextPeriod += fpsMeasurePeriod;

            if (fpsText != null)
            {
                fpsText.text = string.Format(displayFormat, currentFps);
            }
        }
    }
}
