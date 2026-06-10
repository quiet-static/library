using UnityEngine;

namespace QuietStatic.Toolkit.Interactions
{
    /// <summary>
    /// Applies a simple material-swap highlight effect to one or more renderers.
    /// </summary>
    /// <remarks>
    /// This component is intentionally lightweight and reusable. It does not detect
    /// interactables on its own; another script should call <see cref="SetHighlighted"/>
    /// when the object becomes focused, hovered, targeted, or otherwise interactable.
    ///
    /// The highlighter stores each renderer's original material array during
    /// <see cref="Awake"/>. When highlighting is enabled, every material slot on each
    /// renderer is replaced with the configured highlight material. When highlighting
    /// is disabled, the original material arrays are restored.
    /// </remarks>
    public class InteractionHighlighter : MonoBehaviour
    {
        [Header("Renderer References")]
        [Tooltip("Renderers affected by the highlight effect. If left empty, all child renderers are found automatically on Awake.")]
        [SerializeField] private Renderer[] renderers;

        [Header("Highlight Material")]
        [Tooltip("Material used while highlighted. This material replaces every material slot on each configured renderer.")]
        [SerializeField] private Material highlightedMaterial;

        /// <summary>
        /// Original material arrays for each configured renderer.
        /// </summary>
        /// <remarks>
        /// The first array index matches the renderer index in <see cref="renderers"/>.
        /// The second array contains the renderer's original material slots, allowing the
        /// component to restore objects exactly after the highlight is removed.
        /// </remarks>
        private Material[][] originalMaterials;

        /// <summary>
        /// Attempts to auto-fill renderer references when the component is first added
        /// or reset in the Unity Inspector.
        /// </summary>
        private void Reset()
        {
            renderers = GetComponentsInChildren<Renderer>();
        }

        /// <summary>
        /// Initializes renderer references and caches the original material arrays.
        /// </summary>
        /// <remarks>
        /// If no renderers were manually assigned, this searches the GameObject and its
        /// children. The cached materials are later used by <see cref="SetHighlighted"/>
        /// to restore the object's normal appearance.
        /// </remarks>
        private void Awake()
        {
            CacheRenderersIfNeeded();
            CacheOriginalMaterials();
        }

        /// <summary>
        /// Enables or disables the highlight effect.
        /// </summary>
        /// <param name="highlighted">
        /// <c>true</c> to replace renderer materials with the highlight material;
        /// <c>false</c> to restore the original materials cached on Awake.
        /// </param>
        public void SetHighlighted(bool highlighted)
        {
            if (highlightedMaterial == null)
            {
                return;
            }

            if (renderers == null || originalMaterials == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];

                if (targetRenderer == null)
                {
                    continue;
                }

                if (!highlighted)
                {
                    RestoreOriginalMaterials(targetRenderer, i);
                    continue;
                }

                ApplyHighlightedMaterial(targetRenderer);
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
        /// Stores each renderer's original material array for later restoration.
        /// </summary>
        private void CacheOriginalMaterials()
        {
            originalMaterials = new Material[renderers.Length][];

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    originalMaterials[i] = null;
                    continue;
                }

                originalMaterials[i] = renderers[i].materials;
            }
        }

        /// <summary>
        /// Replaces every material slot on a renderer with the configured highlight material.
        /// </summary>
        /// <param name="targetRenderer">The renderer to highlight.</param>
        private void ApplyHighlightedMaterial(Renderer targetRenderer)
        {
            Material[] highlightedMaterials = new Material[targetRenderer.materials.Length];

            for (int i = 0; i < highlightedMaterials.Length; i++)
            {
                highlightedMaterials[i] = highlightedMaterial;
            }

            targetRenderer.materials = highlightedMaterials;
        }

        /// <summary>
        /// Restores the cached original material array for one renderer.
        /// </summary>
        /// <param name="targetRenderer">The renderer to restore.</param>
        /// <param name="rendererIndex">The renderer's index in the cached renderer array.</param>
        private void RestoreOriginalMaterials(Renderer targetRenderer, int rendererIndex)
        {
            if (rendererIndex < 0 || rendererIndex >= originalMaterials.Length)
            {
                return;
            }

            if (originalMaterials[rendererIndex] == null)
            {
                return;
            }

            targetRenderer.materials = originalMaterials[rendererIndex];
        }
    }
}
