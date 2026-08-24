using System;
using System.Collections.Generic;

namespace QuietStatic.Toolkit.SceneFlow
{
    /// <summary>
    /// Describes one generic additive scene transition.
    /// </summary>
    /// <remarks>
    /// The request contains scene-lifetime mechanics plus an optional opaque
    /// condition ID. The destination scene owns the meaning of that condition;
    /// the scene-flow system only carries and delivers it.
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
        /// Optional support scenes that must be loaded before cleanup and remain
        /// loaded for this transition, such as a player, lighting, or local UI scene.
        /// </param>
        /// <param name="additionalScenesToKeep">
        /// Optional nonpersistent scenes that should survive this transition.
        /// </param>
        /// <param name="unloadOtherScenes">
        /// Whether other nonpersistent scenes should be unloaded.
        /// </param>
        /// <param name="conditionId">
        /// Optional transient condition used by the destination scene to select
        /// its entry behavior.
        /// </param>
        public SceneTransitionRequest(
            string targetSceneName,
            IEnumerable<string> additionalScenesToLoad = null,
            IEnumerable<string> additionalScenesToKeep = null,
            bool unloadOtherScenes = true,
            string conditionId = "")
        {
            TargetSceneName = Normalize(targetSceneName);
            this.additionalScenesToLoad =
                CopyDistinctSceneNames(additionalScenesToLoad);
            this.additionalScenesToKeep =
                CopyDistinctSceneNames(additionalScenesToKeep);
            UnloadOtherScenes = unloadOtherScenes;
            ConditionId = Normalize(conditionId);
        }

        /// <summary>
        /// Gets the scene that becomes active.
        /// </summary>
        public string TargetSceneName { get; }

        /// <summary>
        /// Gets required support scenes loaded before, and retained through, cleanup.
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
        /// Gets the transient condition interpreted by the destination scene.
        /// </summary>
        /// <remarks>
        /// This is route context, not a persistent gameplay flag. An empty value
        /// represents an ordinary, unconditioned transition.
        /// </remarks>
        public string ConditionId { get; }

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
