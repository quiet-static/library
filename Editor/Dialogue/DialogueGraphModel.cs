using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor.Tooling;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Dialogue
{
    /// <summary>One immutable node in the editor dialogue graph projection.</summary>
    public sealed class DialogueGraphNode : IStableGraphNode
    {
        public DialogueGraphNode(int index, string stableId, string speaker, string line, bool isEntry)
        {
            Index = index;
            StableId = stableId;
            Speaker = speaker;
            Line = line;
            IsEntry = isEntry;
        }

        public int Index { get; }
        public string StableId { get; }
        public string Id => StableId;
        public string Speaker { get; }
        public string Line { get; }
        public bool IsEntry { get; }
    }

    /// <summary>One transition in the editor dialogue graph projection.</summary>
    public sealed class DialogueGraphEdge : IStableGraphEdge
    {
        public DialogueGraphEdge(
            string id,
            int sourceIndex,
            int targetIndex,
            string sourceId,
            string targetId,
            string label,
            int choiceIndex,
            bool isBroken)
        {
            Id = id;
            SourceIndex = sourceIndex;
            TargetIndex = targetIndex;
            SourceId = sourceId;
            TargetId = targetId;
            Label = label;
            ChoiceIndex = choiceIndex;
            IsBroken = isBroken;
        }

        public string Id { get; }
        public int SourceIndex { get; }
        public int TargetIndex { get; }
        public string SourceId { get; }
        public string TargetId { get; }
        public string Label { get; }
        public int ChoiceIndex { get; }
        public bool IsLinear => ChoiceIndex < 0;
        public bool IsBroken { get; }
    }

    /// <summary>Deterministic graph projection of the existing index-linked runtime model.</summary>
    public sealed class DialogueGraphModel
    {
        public DialogueGraphModel(
            IReadOnlyList<DialogueGraphNode> nodes,
            IReadOnlyList<DialogueGraphEdge> edges,
            IReadOnlyCollection<int> unreachableNodeIndexes,
            IReadOnlyCollection<string> duplicateStableIds)
        {
            Nodes = nodes;
            Edges = edges;
            UnreachableNodeIndexes = unreachableNodeIndexes;
            DuplicateStableIds = duplicateStableIds;
        }

        public IReadOnlyList<DialogueGraphNode> Nodes { get; }
        public IReadOnlyList<DialogueGraphEdge> Edges { get; }
        public IReadOnlyCollection<int> UnreachableNodeIndexes { get; }
        public IReadOnlyCollection<string> DuplicateStableIds { get; }
    }

    /// <summary>Builds graph data without modifying or reserializing a dialogue asset.</summary>
    public static class DialogueGraphModelBuilder
    {
        public static DialogueGraphModel Build(DialogueTree tree)
        {
            if (tree == null)
            {
                throw new ArgumentNullException(nameof(tree));
            }

            DialogueTree.Node[] source = tree.Nodes ?? Array.Empty<DialogueTree.Node>();
            var nodes = new List<DialogueGraphNode>(source.Length);
            var edges = new List<DialogueGraphEdge>();
            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            var duplicates = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < source.Length; index++)
            {
                DialogueTree.Node node = source[index];
                string id = string.IsNullOrWhiteSpace(node?.id)
                    ? $"<missing-id-{index}>"
                    : node.id.Trim();
                nodes.Add(new DialogueGraphNode(
                    index, id, node?.speaker ?? string.Empty, node?.line ?? string.Empty,
                    index == tree.StartNodeIndex));

                if (node != null && !string.IsNullOrWhiteSpace(node.id))
                {
                    if (!ids.TryAdd(id, index))
                    {
                        duplicates.Add(id);
                    }
                }

            }

            for (int index = 0; index < source.Length; index++)
            {
                DialogueTree.Node node = source[index];
                if (node == null) continue;
                AddEdge(edges, nodes, index, node.nextNodeIndex, "Next", -1, source.Length);
                DialogueTree.Choice[] choices = node.choices ?? Array.Empty<DialogueTree.Choice>();
                for (int choiceIndex = 0; choiceIndex < choices.Length; choiceIndex++)
                {
                    DialogueTree.Choice choice = choices[choiceIndex];
                    if (choice != null)
                    {
                        AddEdge(edges, nodes, index, choice.nextNodeIndex,
                            string.IsNullOrWhiteSpace(choice.text) ? $"Choice {choiceIndex}" : choice.text,
                            choiceIndex, source.Length);
                    }
                }
            }

            var reachable = new HashSet<int>();
            Visit(tree.StartNodeIndex, source, reachable);
            var unreachable = new List<int>();
            for (int index = 0; index < source.Length; index++)
            {
                if (!reachable.Contains(index))
                {
                    unreachable.Add(index);
                }
            }

            return new DialogueGraphModel(nodes, edges, unreachable, duplicates);
        }

        private static void AddEdge(
            ICollection<DialogueGraphEdge> edges,
            IReadOnlyList<DialogueGraphNode> nodes,
            int source,
            int target,
            string label,
            int choiceIndex,
            int nodeCount)
        {
            if (target == -1)
            {
                return;
            }

            bool broken = target < 0 || target >= nodeCount;
            string sourceId = nodes[source].StableId;
            string targetId = broken ? $"<missing-index-{target}>" : nodes[target].StableId;
            string edgeId = choiceIndex < 0
                ? $"{sourceId}:next"
                : $"{sourceId}:choice:{choiceIndex}";
            edges.Add(new DialogueGraphEdge(
                edgeId, source, target, sourceId, targetId, label, choiceIndex, broken));
        }

        private static void Visit(int index, DialogueTree.Node[] nodes, ISet<int> visited)
        {
            if (index < 0 || index >= nodes.Length || !visited.Add(index) || nodes[index] == null)
            {
                return;
            }

            DialogueTree.Node node = nodes[index];
            Visit(node.nextNodeIndex, nodes, visited);
            foreach (DialogueTree.Choice choice in node.choices ?? Array.Empty<DialogueTree.Choice>())
            {
                if (choice != null)
                {
                    Visit(choice.nextNodeIndex, nodes, visited);
                }
            }
        }
    }
}
