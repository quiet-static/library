using UnityEngine;

namespace QuietStatic.Animation.Environment
{
    /// <summary>
    /// Continuously rotates the active scene skybox over time.
    ///
    /// This component is useful for creating subtle environmental movement, such as
    /// drifting clouds, shifting skies, dreamlike backgrounds, or other atmospheric
    /// effects where the skybox should feel alive rather than static.
    ///
    /// Requirements:
    /// - The active skybox material must support the "_Rotation" shader property.
    /// - Most built-in Unity skybox shaders include this property, but custom shaders may not.
    /// </summary>
    public class SkyboxRotation : MonoBehaviour
    {
        [Header("Rotation Settings")]
        [Tooltip("How quickly the skybox rotates over time. Higher values rotate the skybox faster. Negative values rotate it in the opposite direction.")]
        [SerializeField] private float rotationSpeed = 1f;

        /// <summary>
        /// Current accumulated skybox rotation value.
        ///
        /// This value increases over time using <see cref="rotationSpeed"/> and is applied
        /// to the active skybox material's "_Rotation" property each frame.
        /// </summary>
        private float currentRotation;

        /// <summary>
        /// Updates the skybox rotation once per frame.
        ///
        /// The rotation amount is multiplied by <see cref="Time.deltaTime"/> so the effect
        /// remains consistent regardless of framerate.
        /// </summary>
        private void Update()
        {
            RotateSkybox();
        }

        /// <summary>
        /// Advances the stored rotation value and applies it to the active skybox material.
        /// </summary>
        private void RotateSkybox()
        {
            if (RenderSettings.skybox == null)
            {
                return;
            }

            currentRotation += rotationSpeed * Time.deltaTime;
            RenderSettings.skybox.SetFloat("_Rotation", currentRotation);
        }
    }
}
