using System.Collections;
using UnityEngine;

namespace QuietStatic.Toolkit.SceneFlow
{
    /// <summary>
    /// Loads a configured set of scenes additively when this GameObject starts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This component is intended for reusable editor and bootstrap workflows where a
    /// gameplay scene may need shared support scenes loaded automatically, such as UI,
    /// player, systems, lighting, audio, or animation scenes.
    /// </para>
    /// <para>
    /// Scene loading is delegated to <see cref="SceneLoadService"/> so the bootstrapper
    /// stays small and does not need to know the details of how scenes are loaded,
    /// de-duplicated, or tracked.
    /// </para>
    /// </remarks>
    public class BootstrapScenes : MonoBehaviour
    {
        [Header("Scenes")]
        [Tooltip("Scene names to load additively when this bootstrapper starts. These names must match scenes included in the Unity Build Settings.")]
        [SerializeField] private string[] additiveScenes;

        /// <summary>
        /// Starts the additive scene loading sequence when this component enters play mode.
        /// </summary>
        /// <remarks>
        /// If no <see cref="SceneLoadService"/> instance exists, this coroutine exits safely
        /// without attempting to load any scenes. Each scene is yielded one at a time so
        /// the configured load order is preserved.
        /// </remarks>
        /// <returns>
        /// An enumerator used by Unity to run additive scene loading across frames.
        /// </returns>
        private IEnumerator Start()
        {
            if (SceneLoadService.Instance == null)
            {
                yield break;
            }

            foreach (string sceneName in additiveScenes)
            {
                yield return SceneLoadService.Instance.LoadAdditiveRoutine(sceneName);
            }
        }
    }
}
