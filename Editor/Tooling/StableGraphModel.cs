using System;
using System.Collections.Generic;
using System.Linq;

namespace QuietStatic.Toolkit.Editor.Tooling
{
    /// <summary>Identifies a graph node independently of its visual position or array index.</summary>
    public interface IStableGraphNode
    {
        /// <summary>Gets the node's non-empty, document-local identifier.</summary>
        string Id { get; }
    }

    /// <summary>Identifies a directed graph edge and its stable endpoint IDs.</summary>
    public interface IStableGraphEdge
    {
        /// <summary>Gets the edge's document-local identifier.</summary>
        string Id { get; }

        /// <summary>Gets the source node ID.</summary>
        string SourceId { get; }

        /// <summary>Gets the destination node ID.</summary>
        string TargetId { get; }
    }

    /// <summary>Classifies a structural graph problem without depending on Unity GUI APIs.</summary>
    public enum StableGraphIssueKind
    {
        EmptyNodeId,
        DuplicateNodeId,
        EmptyEdgeId,
        DuplicateEdgeId,
        BrokenSource,
        BrokenTarget,
        MissingEntry,
        UnreachableNode,
        ProhibitedCycle
    }

    /// <summary>One deterministic structural diagnostic produced by a stable graph.</summary>
    public sealed class StableGraphIssue
    {
        internal StableGraphIssue(StableGraphIssueKind kind, string subjectId, string message)
        {
            Kind = kind;
            SubjectId = subjectId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public StableGraphIssueKind Kind { get; }
        public string SubjectId { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Immutable, ID-based graph projection shared by editor documents. Runtime asset ordering and
    /// editor layout are deliberately outside this type.
    /// </summary>
    public sealed class StableGraphModel<TNode, TEdge>
        where TNode : IStableGraphNode
        where TEdge : IStableGraphEdge
    {
        private readonly Dictionary<string, TNode> nodesById;
        private readonly IReadOnlyList<StableGraphIssue> issues;

        public StableGraphModel(
            IEnumerable<TNode> nodes,
            IEnumerable<TEdge> edges,
            string entryNodeId,
            bool allowCycles = true)
        {
            Nodes = (nodes ?? throw new ArgumentNullException(nameof(nodes))).ToArray();
            Edges = (edges ?? throw new ArgumentNullException(nameof(edges))).ToArray();
            EntryNodeId = Normalize(entryNodeId);

            nodesById = new Dictionary<string, TNode>(StringComparer.Ordinal);
            issues = Validate(allowCycles);
        }

        public IReadOnlyList<TNode> Nodes { get; }
        public IReadOnlyList<TEdge> Edges { get; }
        public string EntryNodeId { get; }
        public IReadOnlyList<StableGraphIssue> Issues => issues;
        public bool IsValid => issues.Count == 0;

        public bool TryGetNode(string id, out TNode node) =>
            nodesById.TryGetValue(Normalize(id), out node);

        private IReadOnlyList<StableGraphIssue> Validate(bool allowCycles)
        {
            var result = new List<StableGraphIssue>();
            foreach (TNode node in Nodes)
            {
                string id = Normalize(node?.Id);
                if (id.Length == 0)
                {
                    result.Add(Issue(StableGraphIssueKind.EmptyNodeId, string.Empty,
                        "A graph node has an empty stable ID."));
                }
                else if (!nodesById.TryAdd(id, node))
                {
                    result.Add(Issue(StableGraphIssueKind.DuplicateNodeId, id,
                        $"Node ID '{id}' is duplicated."));
                }
            }

            var edgeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TEdge edge in Edges)
            {
                string id = Normalize(edge?.Id);
                string source = Normalize(edge?.SourceId);
                string target = Normalize(edge?.TargetId);
                if (id.Length == 0)
                {
                    result.Add(Issue(StableGraphIssueKind.EmptyEdgeId, string.Empty,
                        "A graph edge has an empty stable ID."));
                }
                else if (!edgeIds.Add(id))
                {
                    result.Add(Issue(StableGraphIssueKind.DuplicateEdgeId, id,
                        $"Edge ID '{id}' is duplicated."));
                }

                if (!nodesById.ContainsKey(source))
                {
                    result.Add(Issue(StableGraphIssueKind.BrokenSource, id,
                        $"Edge '{id}' references missing source node '{source}'."));
                }
                if (!nodesById.ContainsKey(target))
                {
                    result.Add(Issue(StableGraphIssueKind.BrokenTarget, id,
                        $"Edge '{id}' references missing target node '{target}'."));
                }
            }

            if (!nodesById.ContainsKey(EntryNodeId))
            {
                result.Add(Issue(StableGraphIssueKind.MissingEntry, EntryNodeId,
                    $"Entry node '{EntryNodeId}' does not exist."));
                return result;
            }

            Dictionary<string, List<string>> adjacency = BuildAdjacency();
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            VisitReachable(EntryNodeId, adjacency, reachable);
            foreach (string id in nodesById.Keys.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!reachable.Contains(id))
                {
                    result.Add(Issue(StableGraphIssueKind.UnreachableNode, id,
                        $"Node '{id}' is unreachable from entry node '{EntryNodeId}'."));
                }
            }

            if (!allowCycles && HasCycle(adjacency))
            {
                result.Add(Issue(StableGraphIssueKind.ProhibitedCycle, EntryNodeId,
                    "The graph contains a cycle, but this document requires an acyclic flow."));
            }

            return result;
        }

        private Dictionary<string, List<string>> BuildAdjacency()
        {
            var adjacency = nodesById.Keys.ToDictionary(
                id => id, _ => new List<string>(), StringComparer.Ordinal);
            foreach (TEdge edge in Edges)
            {
                string source = Normalize(edge?.SourceId);
                string target = Normalize(edge?.TargetId);
                if (adjacency.TryGetValue(source, out List<string> targets) &&
                    nodesById.ContainsKey(target))
                {
                    targets.Add(target);
                }
            }
            return adjacency;
        }

        private static void VisitReachable(
            string id,
            IReadOnlyDictionary<string, List<string>> adjacency,
            ISet<string> visited)
        {
            if (!visited.Add(id) || !adjacency.TryGetValue(id, out List<string> targets)) return;
            foreach (string target in targets) VisitReachable(target, adjacency, visited);
        }

        private static bool HasCycle(IReadOnlyDictionary<string, List<string>> adjacency)
        {
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in adjacency.Keys)
            {
                if (HasCycleFrom(id, adjacency, visiting, visited)) return true;
            }
            return false;
        }

        private static bool HasCycleFrom(
            string id,
            IReadOnlyDictionary<string, List<string>> adjacency,
            ISet<string> visiting,
            ISet<string> visited)
        {
            if (visited.Contains(id)) return false;
            if (!visiting.Add(id)) return true;
            foreach (string target in adjacency[id])
            {
                if (HasCycleFrom(target, adjacency, visiting, visited)) return true;
            }
            visiting.Remove(id);
            visited.Add(id);
            return false;
        }

        private static StableGraphIssue Issue(
            StableGraphIssueKind kind, string subjectId, string message) =>
            new(kind, subjectId, message);

        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
    }
}
