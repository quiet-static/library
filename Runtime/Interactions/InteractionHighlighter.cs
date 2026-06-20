using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>
    /// Adjusts renderer brightness for interaction feedback without replacing materials.
    /// </summary>
    /// <remarks>
    /// This component uses <see cref="MaterialPropertyBlock"/> so each renderer can appear brighter
    /// without changing the original shared material asset or creating runtime material instances.
    /// </remarks>
    public class InteractionHighlighter : MonoBehaviour
    {
        [Header("Renderer References")]
        [Tooltip("Renderers affected by the brightness effect. If left empty, all child renderers are found automatically on Awake.")]
        [SerializeField] private Renderer[] renderers;

        [Header("Brightness Settings")]
        [Tooltip("Multiplier applied to the material color while highlighted. 1 means unchanged; values above 1 make the object brighter.")]
        [Min(1f)]
        [SerializeField] private float highlightedBrightness = 1.35f;

        [Tooltip("Adds emission while highlighted. This can make the object stand out more clearly in darker scenes.")]
        [SerializeField] private bool useEmission = true;

        [Tooltip("Emission brightness added while highlighted. Keep this low unless you want a strong glow.")]
        [Min(0f)]
        [SerializeField] private float emissionBrightness = 0.15f;

        /// <summary>
        /// Cached property block reused for renderer shader overrides.
        /// </summary>
        private MaterialPropertyBlock propertyBlock;

        /// <summary>
        /// Shader property IDs for common Unity render pipelines.
        /// </summary>
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// Attempts to auto-fill renderer references when the component is reset in the Inspector.
        /// </summary>
        private void Reset()
        {
            renderers = GetComponentsInChildren<Renderer>();
        }

        /// <summary>
        /// Initializes renderer references and reusable shader property storage.
        /// </summary>
        private void Awake()
        {
            CacheRenderersIfNeeded();
            propertyBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Enables or disables the interaction brightness effect.
        /// </summary>
        /// <param name="highlighted">
        /// <c>true</c> to brighten the object; <c>false</c> to clear the shader overrides.
        /// </param>
        public void SetHighlighted(bool highlighted)
        {
            if (renderers == null)
            {
                return;
            }

            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                ApplyBrightness(targetRenderer, highlighted);
            }
        }

        /// <summary>
        /// Finds child renderers automatically if no renderers were assigned manually.
        /// </summary>
        private void CacheRenderersIfNeeded()
        {
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>();
            }
        }

        /// <summary>
        /// Applies a brightness override to all material slots on one renderer.
        /// </summary>
        /// <param name="targetRenderer">Renderer receiving the brightness effect.</param>
        /// <param name="highlighted">Whether the effect should be active.</param>
        private void ApplyBrightness(Renderer targetRenderer, bool highlighted)
        {
            Material[] materials = targetRenderer.sharedMaterials;

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];

                if (material == null)
                {
                    continue;
                }

                if (!highlighted)
                {
                    targetRenderer.SetPropertyBlock(null, materialIndex);
                    continue;
                }

                propertyBlock.Clear();

                if (material.HasProperty(BaseColorId))
                {
                    Color baseColor = material.GetColor(BaseColorId);
                    propertyBlock.SetColor(BaseColorId, baseColor * highlightedBrightness);
                }
                else if (material.HasProperty(ColorId))
                {
                    Color baseColor = material.GetColor(ColorId);
                    propertyBlock.SetColor(ColorId, baseColor * highlightedBrightness);
                }

                if (useEmission && material.HasProperty(EmissionColorId))
                {
                    Color emissionColor = material.GetColor(EmissionColorId);
                    propertyBlock.SetColor(
                        EmissionColorId,
                        emissionColor + Color.white * emissionBrightness
                    );
                }

                targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
            }
        }
    }
}
