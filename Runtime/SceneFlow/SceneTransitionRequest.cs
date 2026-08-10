using System;
using System.Collections.Generic;

namespace QuietStatic.Toolkit.SceneFlow
{
    /// <summary>
    /// Describes one generic additive scene transition.
    /// </summary>
    /// <remarks>
    /// The request contains only scene-lifetime mechanics. Project-specific
    /// behavior such as player spawning, game-state changes, story flags, and
    /// named level catalogs should run before or after the request.
    /// </remarks>
    public sealed class SceneTransitionRequest
    {
        private readonly List<string> additionalScenesToLoad;
        private readonly List<string> additionalScenesToKeep;

        /// <summary>
        /// Creates a transition request.
        /// </summary>
        /// <param name="targetSceneName">
        /// Scene that becomes active when loading completes.
        /// </param>
        /// <param name="additionalScenesToLoad">
        /// Optional support scenes that must be loaded before cleanup, such as
        /// a player, lighting, or local UI scene.
        /// </param>
        /// <param name="additionalScenesToKeep">
        /// Optional nonpersistent scenes that should survive this transition.
        /// </param>
        /// <param name="unloadOtherScenes">
        /// Whether other nonpersistent scenes should be unloaded.
        /// </param>
        public SceneTransitionRequest(
            string targetSceneName,
            IEnumerable<string> additionalScenesToLoad = null,
            IEnumerable<string> additionalScenesToKeep = null,
            bool unloadOtherScenes = true)
        {
            TargetSceneName = Normalize(targetSceneName);
            this.additionalScenesToLoad =
                CopyDistinctSceneNames(additionalScenesToLoad);
            this.additionalScenesToKeep =
                CopyDistinctSceneNames(additionalScenesToKeep);
            UnloadOtherScenes = unloadOtherScenes;
        }

        /// <summary>
        /// Gets the scene that becomes active.
        /// </summary>
        public string TargetSceneName { get; }

        /// <summary>
        /// Gets support scenes that are loaded before cleanup begins.
        /// </summary>
        public IReadOnlyList<string> AdditionalScenesToLoad =>
            additionalScenesToLoad;

        /// <summary>
        /// Gets nonpersistent scenes retained for this transition.
        /// </summary>
        public IReadOnlyList<string> AdditionalScenesToKeep =>
            additionalScenesToKeep;

        /// <summary>
        /// Gets whether unrelated nonpersistent scenes are unloaded.
        /// </summary>
        public bool UnloadOtherScenes { get; }

        /// <summary>
        /// Returns whether this request explicitly retains a scene.
        /// </summary>
        public bool KeepsScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            for (int index = 0;
                 index < additionalScenesToKeep.Count;
                 index++)
            {
                if (string.Equals(
                        additionalScenesToKeep[index],
                        sceneName,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<string> CopyDistinctSceneNames(
            IEnumerable<string> sceneNames)
        {
            List<string> result = new List<string>();

            if (sceneNames == null)
            {
                return result;
            }

            HashSet<string> seen =
                new HashSet<string>(StringComparer.Ordinal);

            foreach (string sceneName in sceneNames)
            {
                string normalized = Normalize(sceneName);

                if (!string.IsNullOrEmpty(normalized) &&
                    seen.Add(normalized))
                {
                    result.Add(normalized);
                }
            }

            return result;
        }

        private static string Normalize(string sceneName)
        {
            return string.IsNullOrWhiteSpace(sceneName)
                ? string.Empty
                : sceneName.Trim();
        }
    }
}
