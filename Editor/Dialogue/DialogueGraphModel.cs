using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Dialogue;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Dialogue
{
    /// <summary>One immutable node in the editor's read-only dialogue graph.</summary>
    public sealed class DialogueGraphNode
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
        public string Speaker { get; }
        public string Line { get; }
        public bool IsEntry { get; }
    }

    /// <summary>One transition in the editor's read-only dialogue graph.</summary>
    public sealed class DialogueGraphEdge
    {
        public DialogueGraphEdge(int sourceIndex, int targetIndex, string label, bool isBroken)
        {
            SourceIndex = sourceIndex;
            TargetIndex = targetIndex;
            Label = label;
            IsBroken = isBroken;
        }

        public int SourceIndex { get; }
        public int TargetIndex { get; }
        public string Label { get; }
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
                    ? $"legacy-index-{index}"
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

                if (node == null)
                {
                    continue;
                }

                AddEdge(edges, index, node.nextNodeIndex, "Next", source.Length);
                DialogueTree.Choice[] choices = node.choices ?? Array.Empty<DialogueTree.Choice>();
                for (int choiceIndex = 0; choiceIndex < choices.Length; choiceIndex++)
                {
                    DialogueTree.Choice choice = choices[choiceIndex];
                    if (choice != null)
                    {
                        AddEdge(edges, index, choice.nextNodeIndex,
                            string.IsNullOrWhiteSpace(choice.text) ? $"Choice {choiceIndex}" : choice.text,
                            source.Length);
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
            int source,
            int target,
            string label,
            int nodeCount)
        {
            if (target == -1)
            {
                return;
            }

            edges.Add(new DialogueGraphEdge(
                source, target, label, target < 0 || target >= nodeCount));
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
