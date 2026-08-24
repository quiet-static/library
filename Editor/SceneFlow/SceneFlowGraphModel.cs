using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Editor.Tooling;
using QuietStatic.Toolkit.SceneFlow;

namespace QuietStatic.Toolkit.Editor.SceneFlow
{
    /// <summary>One scene node projected from all unique connection endpoints.</summary>
    public sealed class SceneFlowGraphNode : IStableGraphNode
    {
        internal SceneFlowGraphNode(string id, bool isEntry, bool isDeadEnd, bool isBuilt, bool isReachable)
        {
            Id = id;
            IsEntry = isEntry;
            IsDeadEnd = isDeadEnd;
            IsBuilt = isBuilt;
            IsReachable = isReachable;
        }

        public string Id { get; }
        public bool IsEntry { get; }
        public bool IsDeadEnd { get; }
        public bool IsBuilt { get; }
        public bool IsReachable { get; }
    }

    /// <summary>One directed scene connection projected as a stable graph edge.</summary>
    public sealed class SceneFlowGraphEdge : IStableGraphEdge
    {
        internal SceneFlowGraphEdge(string id, string sourceId, string targetId, int connectionIndex)
        {
            Id = id;
            SourceId = sourceId;
            TargetId = targetId;
            ConnectionIndex = connectionIndex;
        }

        public string Id { get; }
        public string SourceId { get; }
        public string TargetId { get; }
        public int ConnectionIndex { get; }
    }

    public enum SceneFlowGraphIssueKind
    {
        EmptyConnectionId,
        DuplicateConnectionId,
        MissingSource,
        MissingTarget,
        SceneNotInBuild,
        UnreachableScene,
        SelfLoop,
        UnmatchedDestinationResponse
    }

    public sealed class SceneFlowGraphIssue
    {
        internal SceneFlowGraphIssue(SceneFlowGraphIssueKind kind, string subjectId, string message)
        {
            Kind = kind;
            SubjectId = subjectId ?? string.Empty;
            Message = message ?? string.Empty;
        }
        public SceneFlowGraphIssueKind Kind { get; }
        public string SubjectId { get; }
        public string Message { get; }
    }

    /// <summary>Immutable graph projection of a <see cref="SceneFlowMap"/>.</summary>
    public sealed class SceneFlowGraphModel
    {
        internal SceneFlowGraphModel(
            IReadOnlyList<SceneFlowGraphNode> nodes,
            IReadOnlyList<SceneFlowGraphEdge> edges,
            IReadOnlyList<SceneFlowGraphIssue> issues)
        {
            Nodes = nodes;
            Edges = edges;
            Issues = issues;
        }
        public IReadOnlyList<SceneFlowGraphNode> Nodes { get; }
        public IReadOnlyList<SceneFlowGraphEdge> Edges { get; }
        public IReadOnlyList<SceneFlowGraphIssue> Issues { get; }
    }

    /// <summary>Builds scene graph data without modifying the runtime map.</summary>
    public static class SceneFlowGraphModelBuilder
    {
        public static SceneFlowGraphModel Build(
            SceneFlowMap map,
            IEnumerable<string> builtScenes = null,
            IEnumerable<string> destinationResponseIds = null)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            var built = builtScenes == null
                ? null
                : new HashSet<string>(builtScenes.Where(Valid).Select(Normalize), StringComparer.Ordinal);
            var responses = destinationResponseIds == null
                ? null
                : new HashSet<string>(destinationResponseIds.Where(Valid).Select(Normalize), StringComparer.Ordinal);
            var edges = new List<SceneFlowGraphEdge>();
            var issues = new List<SceneFlowGraphIssue>();
            var sceneIds = new HashSet<string>(StringComparer.Ordinal);
            var connectionIds = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < map.Connections.Count; index++)
            {
                SceneFlowMap.Connection connection = map.Connections[index];
                string id = Normalize(connection?.Id);
                string source = Normalize(connection?.FromSceneName);
                string target = Normalize(connection?.ToSceneName);
                if (!Valid(id)) issues.Add(Issue(SceneFlowGraphIssueKind.EmptyConnectionId, id,
                    $"Connection {index} has an empty stable ID."));
                else if (!connectionIds.Add(id)) issues.Add(Issue(
                    SceneFlowGraphIssueKind.DuplicateConnectionId, id, $"Connection ID '{id}' is duplicated."));
                if (!Valid(source)) issues.Add(Issue(SceneFlowGraphIssueKind.MissingSource, id,
                    $"Connection '{id}' has no source scene."));
                else sceneIds.Add(source);
                if (!Valid(target)) issues.Add(Issue(SceneFlowGraphIssueKind.MissingTarget, id,
                    $"Connection '{id}' has no target scene."));
                else sceneIds.Add(target);
                if (Valid(source) && source == target) issues.Add(Issue(SceneFlowGraphIssueKind.SelfLoop, id,
                    $"Connection '{id}' loops back to scene '{source}'."));
                if (responses != null && Valid(id) && !responses.Contains(id))
                    issues.Add(Issue(SceneFlowGraphIssueKind.UnmatchedDestinationResponse, id,
                        $"Connection '{id}' has no serialized destination response."));
                edges.Add(new SceneFlowGraphEdge(id, source, target, index));
            }

            var inbound = sceneIds.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
            var outbound = sceneIds.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
            foreach (SceneFlowGraphEdge edge in edges)
            {
                if (!sceneIds.Contains(edge.SourceId) || !sceneIds.Contains(edge.TargetId)) continue;
                inbound[edge.TargetId]++;
                outbound[edge.SourceId].Add(edge.TargetId);
            }
            string[] entries = inbound.Where(pair => pair.Value == 0).Select(pair => pair.Key)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            foreach (string entry in entries) Visit(entry, outbound, reachable);

            var nodes = sceneIds.OrderBy(id => id, StringComparer.Ordinal).Select(id =>
            {
                bool isBuilt = built == null || built.Contains(id);
                bool isReachable = reachable.Contains(id);
                if (!isBuilt) issues.Add(Issue(SceneFlowGraphIssueKind.SceneNotInBuild, id,
                    $"Scene '{id}' is not enabled in Build Settings."));
                if (!isReachable) issues.Add(Issue(SceneFlowGraphIssueKind.UnreachableScene, id,
                    $"Scene '{id}' is unreachable from any entry scene."));
                return new SceneFlowGraphNode(id, inbound[id] == 0, outbound[id].Count == 0,
                    isBuilt, isReachable);
            }).ToArray();

            return new SceneFlowGraphModel(nodes, edges, issues);
        }

        private static void Visit(string id, IReadOnlyDictionary<string, List<string>> graph, ISet<string> visited)
        {
            if (!visited.Add(id)) return;
            foreach (string target in graph[id]) Visit(target, graph, visited);
        }

        private static SceneFlowGraphIssue Issue(SceneFlowGraphIssueKind kind, string id, string message) =>
            new(kind, id, message);
        private static bool Valid(string value) => !string.IsNullOrWhiteSpace(value);
        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
    }
}
