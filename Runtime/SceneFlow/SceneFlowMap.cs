using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuietStatic.Toolkit.SceneFlow
{
    /// <summary>
    /// Project-owned catalog of directed scene connections.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SceneFlowMap",
        menuName = "Quiet Static Toolkit/Scene Flow/Scene Flow Map")]
    public sealed class SceneFlowMap : ScriptableObject
    {
        /// <summary>One directed connection between two content scenes.</summary>
        [Serializable]
        public sealed class Connection
        {
            [Tooltip("Stable identifier used by triggers and UnityEvents.")]
            [SerializeField] private string id;

            [Tooltip("Scene from which this connection can be taken.")]
            [SerializeField] private SceneReference fromScene = new();

            [Tooltip("Destination scene loaded and made active by this connection.")]
            [SerializeField] private SceneReference toScene = new();

            [Tooltip("Support scenes loaded before the old content scene is unloaded.")]
            [SerializeField] private SceneReference[] additionalScenesToLoad;

            [Tooltip("Nonpersistent scenes allowed to remain loaded across this connection.")]
            [SerializeField] private SceneReference[] additionalScenesToKeep;

            [Tooltip("Unload other nonpersistent scenes after the destination is ready.")]
            [SerializeField] private bool unloadOtherScenes = true;

            /// <summary>Gets the stable connection ID.</summary>
            public string Id => id;
            /// <summary>Gets the source scene name.</summary>
            public string FromSceneName => fromScene?.SceneName ?? string.Empty;
            /// <summary>Gets the destination scene name.</summary>
            public string ToSceneName => toScene?.SceneName ?? string.Empty;
            /// <summary>Gets whether unrelated nonpersistent scenes unload after transition.</summary>
            public bool UnloadOtherScenes => unloadOtherScenes;

            /// <summary>Builds the runtime request represented by this connection.</summary>
            public SceneTransitionRequest CreateRequest()
            {
                return new SceneTransitionRequest(
                    ToSceneName,
                    GetNames(additionalScenesToLoad),
                    GetNames(additionalScenesToKeep),
                    unloadOtherScenes);
            }

            private static IEnumerable<string> GetNames(
                IEnumerable<SceneReference> references)
            {
                if (references == null)
                {
                    yield break;
                }

                foreach (SceneReference reference in references)
                {
                    if (reference != null && reference.IsValid)
                    {
                        yield return reference.SceneName;
                    }
                }
            }
        }

        [Tooltip("Directed connections available to scene transition components.")]
        [SerializeField] private Connection[] connections;

        /// <summary>Gets all directed connections in authoring order.</summary>
        public IReadOnlyList<Connection> Connections =>
            connections ?? Array.Empty<Connection>();

        /// <summary>Finds a connection by its stable identifier.</summary>
        public bool TryGetConnection(string connectionId, out Connection connection)
        {
            connection = null;
            if (string.IsNullOrWhiteSpace(connectionId) || connections == null)
            {
                return false;
            }

            foreach (Connection candidate in connections)
            {
                if (candidate != null && string.Equals(
                        candidate.Id,
                        connectionId.Trim(),
                        StringComparison.Ordinal))
                {
                    connection = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns all connections leaving the named scene.</summary>
        public IEnumerable<Connection> GetConnectionsFrom(string sceneName)
        {
            if (connections == null || string.IsNullOrWhiteSpace(sceneName))
            {
                yield break;
            }

            foreach (Connection connection in connections)
            {
                if (connection != null && string.Equals(
                        connection.FromSceneName,
                        sceneName.Trim(),
                        StringComparison.Ordinal))
                {
                    yield return connection;
                }
            }
        }

        /// <summary>Creates a transition request for a configured connection.</summary>
        public bool TryCreateRequest(
            string connectionId,
            out SceneTransitionRequest request)
        {
            request = null;
            if (!TryGetConnection(connectionId, out Connection connection) ||
                string.IsNullOrWhiteSpace(connection.ToSceneName))
            {
                return false;
            }

            request = connection.CreateRequest();
            return true;
        }
    }
}
