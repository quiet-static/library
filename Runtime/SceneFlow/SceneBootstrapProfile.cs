using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuietStatic.Toolkit.SceneFlow
{
    /// <summary>
    /// Reusable definition of the scenes required to start a game or application.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SceneBootstrapProfile",
        menuName = "Quiet Static Toolkit/Scene Flow/Bootstrap Profile")]
    public sealed class SceneBootstrapProfile : ScriptableObject
    {
        [Header("Persistent Scenes")]
        [Tooltip("Scenes loaded additively, in order, before initial content. They survive normal transitions.")]
        [SerializeField] private SceneReference[] persistentScenes;

        [Header("Initial Content")]
        [Tooltip("First content scene loaded and made active after persistent scenes initialize.")]
        [SerializeField] private SceneReference initialScene = new();

        [Tooltip("Support scenes loaded with the initial content scene.")]
        [SerializeField] private SceneReference[] additionalInitialScenes;

        [Tooltip("Other nonpersistent scenes retained during the initial transition.")]
        [SerializeField] private SceneReference[] initialScenesToKeep;

        [Tooltip("Optional condition used by the initial scene to select its entry behavior.")]
        [SerializeField] private string initialConditionId;

        [Tooltip("Unload the bootstrap scene and unrelated nonpersistent scenes after initial content is ready.")]
        [SerializeField] private bool unloadOtherScenes = true;

        /// <summary>Ordered, normalized persistent scene names.</summary>
        public IReadOnlyList<string> PersistentSceneNames =>
            GetDistinctNames(persistentScenes);

        /// <summary>Name of the initial active content scene.</summary>
        public string InitialSceneName => initialScene?.SceneName ?? string.Empty;

        /// <summary>Whether the profile can start an initial transition.</summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(InitialSceneName);

        /// <summary>All scenes referenced by this profile, without duplicates.</summary>
        public IReadOnlyList<string> ReferencedSceneNames =>
            GetDistinctNames(
                EnumerateAllReferences().ToArray());

        /// <summary>Creates the initial content transition described by this profile.</summary>
        public SceneTransitionRequest CreateInitialTransitionRequest()
        {
            return new SceneTransitionRequest(
                InitialSceneName,
                GetDistinctNames(additionalInitialScenes),
                GetDistinctNames(initialScenesToKeep),
                unloadOtherScenes,
                initialConditionId);
        }

        private IEnumerable<SceneReference> EnumerateAllReferences()
        {
            if (persistentScenes != null)
            {
                foreach (SceneReference scene in persistentScenes)
                {
                    yield return scene;
                }
            }

            yield return initialScene;

            if (additionalInitialScenes != null)
            {
                foreach (SceneReference scene in additionalInitialScenes)
                {
                    yield return scene;
                }
            }

            if (initialScenesToKeep != null)
            {
                foreach (SceneReference scene in initialScenesToKeep)
                {
                    yield return scene;
                }
            }
        }

        private static IReadOnlyList<string> GetDistinctNames(
            IEnumerable<SceneReference> references)
        {
            if (references == null)
            {
                return Array.Empty<string>();
            }

            return references
                .Where(reference => reference != null && reference.IsValid)
                .Select(reference => reference.SceneName.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }
}
