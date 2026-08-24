using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Dialogue;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Dialogue
{
    /// <summary>One incoming stable-ID edge affected by deleting a dialogue node.</summary>
    public sealed class DialogueNodeDeleteReference
    {
        internal DialogueNodeDeleteReference(string sourceId, string edgeId, string label)
        {
            SourceId = sourceId;
            EdgeId = edgeId;
            Label = label;
        }
        public string SourceId { get; }
        public string EdgeId { get; }
        public string Label { get; }
    }

    public sealed class DialogueNodeDeletePreview
    {
        internal DialogueNodeDeletePreview(
            string treePath, string nodeId, int version,
            IReadOnlyList<DialogueNodeDeleteReference> incoming)
        {
            TreePath = treePath;
            NodeId = nodeId;
            Version = version;
            Incoming = incoming;
        }
        internal string TreePath { get; }
        internal int Version { get; }
        public string NodeId { get; }
        public IReadOnlyList<DialogueNodeDeleteReference> Incoming { get; }
    }

    /// <summary>
    /// Atomic, Undo-aware dialogue mutations. Structural commands capture targets by stable ID and
    /// rewrite runtime indexes after array changes.
    /// </summary>
    public static class DialogueGraphCommands
    {
        public static void AddNode(DialogueTree tree, string nodeId, string speaker = "", string line = "")
        {
            RequireEditable(tree);
            string id = RequireUniqueId(tree, nodeId);
            Mutate(tree, "Add Dialogue Node", serialized =>
            {
                SerializedProperty nodes = serialized.FindProperty("nodes");
                int index = nodes.arraySize;
                nodes.InsertArrayElementAtIndex(index);
                SerializedProperty node = nodes.GetArrayElementAtIndex(index);
                node.FindPropertyRelative("id").stringValue = id;
                node.FindPropertyRelative("speaker").stringValue = speaker ?? string.Empty;
                node.FindPropertyRelative("line").stringValue = line ?? string.Empty;
                node.FindPropertyRelative("choices").arraySize = 0;
                node.FindPropertyRelative("nextNodeIndex").intValue = -1;
                node.FindPropertyRelative("flagsToSetOnEnter").arraySize = 0;
                if (nodes.arraySize == 1) serialized.FindProperty("startNodeIndex").intValue = 0;
            });
        }

        public static void DuplicateNode(DialogueTree tree, string sourceId, string newNodeId)
        {
            RequireEditable(tree);
            RequireValidStructure(tree);
            int sourceIndex = FindIndex(tree, sourceId);
            string id = RequireUniqueId(tree, newNodeId);
            Dictionary<string, EdgeTargets> targets = CaptureTargets(tree);
            string sourceKey = Normalize(sourceId);
            targets[id] = new EdgeTargets
            {
                Linear = targets[sourceKey].Linear,
                Choices = targets[sourceKey].Choices.ToArray()
            };
            string entryId = EntryId(tree);
            Mutate(tree, "Duplicate Dialogue Node", serialized =>
            {
                SerializedProperty nodes = serialized.FindProperty("nodes");
                nodes.InsertArrayElementAtIndex(sourceIndex);
                SerializedProperty destination = nodes.GetArrayElementAtIndex(sourceIndex);
                destination.FindPropertyRelative("id").stringValue = id;
                RewriteTargets(nodes, targets, deletedId: null);
                serialized.FindProperty("startNodeIndex").intValue = FindIndex(nodes, entryId);
            });
        }

        public static DialogueNodeDeletePreview PreviewDeleteNode(DialogueTree tree, string nodeId)
        {
            RequireEditable(tree);
            DialogueGraphModel model = RequireValidStructure(tree);
            string id = Normalize(nodeId);
            if (!model.Nodes.Any(node => node.StableId == id))
                throw new InvalidOperationException($"Dialogue node '{id}' was not found.");
            DialogueNodeDeleteReference[] incoming = model.Edges
                .Where(edge => !edge.IsBroken && edge.TargetId == id)
                .Select(edge => new DialogueNodeDeleteReference(edge.SourceId, edge.Id, edge.Label))
                .ToArray();
            return new DialogueNodeDeletePreview(
                AssetDatabase.GetAssetPath(tree), id, ComputeVersion(tree), incoming);
        }

        public static void DeleteNode(DialogueTree tree, DialogueNodeDeletePreview preview)
        {
            RequireEditable(tree);
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            if (ComputeVersion(tree) != preview.Version)
                throw new InvalidOperationException("The dialogue tree changed after preview.");
            Dictionary<string, EdgeTargets> targets = CaptureTargets(tree);
            int removedIndex = FindIndex(tree, preview.NodeId);
            string entryId = EntryId(tree);
            Mutate(tree, "Delete Dialogue Node", serialized =>
            {
                SerializedProperty nodes = serialized.FindProperty("nodes");
                nodes.DeleteArrayElementAtIndex(removedIndex);
                RewriteTargets(nodes, targets, preview.NodeId);
                serialized.FindProperty("startNodeIndex").intValue =
                    entryId == preview.NodeId ? (nodes.arraySize == 0 ? -1 : 0) : FindIndex(nodes, entryId);
            });
        }

        public static void ReorderNode(DialogueTree tree, string nodeId, int newIndex)
        {
            RequireEditable(tree);
            RequireValidStructure(tree);
            Dictionary<string, EdgeTargets> targets = CaptureTargets(tree);
            string entryId = EntryId(tree);
            int oldIndex = FindIndex(tree, nodeId);
            int clamped = Mathf.Clamp(newIndex, 0, tree.Nodes.Length - 1);
            Mutate(tree, "Reorder Dialogue Node", serialized =>
            {
                SerializedProperty nodes = serialized.FindProperty("nodes");
                nodes.MoveArrayElement(oldIndex, clamped);
                RewriteTargets(nodes, targets, deletedId: null);
                serialized.FindProperty("startNodeIndex").intValue = FindIndex(nodes, entryId);
            });
        }

        public static void SetEntryNode(DialogueTree tree, string nodeId)
        {
            RequireEditable(tree);
            int index = FindIndex(tree, nodeId);
            Mutate(tree, "Set Dialogue Entry", serialized =>
                serialized.FindProperty("startNodeIndex").intValue = index);
        }

        public static void ReconnectLinear(DialogueTree tree, string sourceId, string targetId) =>
            SetTarget(tree, sourceId, targetId, -1);

        public static void ReconnectChoice(DialogueTree tree, string sourceId, int choiceIndex, string targetId) =>
            SetTarget(tree, sourceId, targetId, choiceIndex);

        public static void ReorderChoice(DialogueTree tree, string nodeId, int oldIndex, int newIndex)
        {
            RequireEditable(tree);
            int nodeIndex = FindIndex(tree, nodeId);
            DialogueTree.Choice[] choices = tree.Nodes[nodeIndex].choices ?? Array.Empty<DialogueTree.Choice>();
            if (oldIndex < 0 || oldIndex >= choices.Length)
                throw new ArgumentOutOfRangeException(nameof(oldIndex));
            int destination = Mathf.Clamp(newIndex, 0, choices.Length - 1);
            if (oldIndex == destination) return;

            Undo.RegisterCompleteObjectUndo(tree, "Reorder Dialogue Choice");
            DialogueTree.Choice moved = choices[oldIndex];
            if (oldIndex < destination)
                Array.Copy(choices, oldIndex + 1, choices, oldIndex, destination - oldIndex);
            else
                Array.Copy(choices, destination, choices, destination + 1, oldIndex - destination);
            choices[destination] = moved;
            EditorUtility.SetDirty(tree);
        }

        private static void SetTarget(DialogueTree tree, string sourceId, string targetId, int choiceIndex)
        {
            RequireEditable(tree);
            int source = FindIndex(tree, sourceId);
            int target = string.IsNullOrWhiteSpace(targetId) ? -1 : FindIndex(tree, targetId);
            Mutate(tree, "Reconnect Dialogue Edge", serialized =>
            {
                SerializedProperty node = serialized.FindProperty("nodes").GetArrayElementAtIndex(source);
                if (choiceIndex < 0) node.FindPropertyRelative("nextNodeIndex").intValue = target;
                else
                {
                    SerializedProperty choices = node.FindPropertyRelative("choices");
                    if (choiceIndex >= choices.arraySize) throw new ArgumentOutOfRangeException(nameof(choiceIndex));
                    choices.GetArrayElementAtIndex(choiceIndex)
                        .FindPropertyRelative("nextNodeIndex").intValue = target;
                }
            });
        }

        private sealed class EdgeTargets
        {
            public string Linear;
            public string[] Choices;
        }

        private static Dictionary<string, EdgeTargets> CaptureTargets(DialogueTree tree)
        {
            DialogueTree.Node[] nodes = tree.Nodes;
            string Target(int index) => index >= 0 && index < nodes.Length ? Normalize(nodes[index]?.id) : null;
            return nodes.ToDictionary(node => Normalize(node.id), node => new EdgeTargets
            {
                Linear = Target(node.nextNodeIndex),
                Choices = (node.choices ?? Array.Empty<DialogueTree.Choice>())
                    .Select(choice => Target(choice?.nextNodeIndex ?? -1)).ToArray()
            }, StringComparer.Ordinal);
        }

        private static void RewriteTargets(
            SerializedProperty nodes,
            IReadOnlyDictionary<string, EdgeTargets> targets,
            string deletedId)
        {
            var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < nodes.arraySize; index++)
                indexes[nodes.GetArrayElementAtIndex(index).FindPropertyRelative("id").stringValue.Trim()] = index;
            for (int index = 0; index < nodes.arraySize; index++)
            {
                SerializedProperty node = nodes.GetArrayElementAtIndex(index);
                string id = node.FindPropertyRelative("id").stringValue.Trim();
                EdgeTargets semantic = targets[id];
                node.FindPropertyRelative("nextNodeIndex").intValue = Resolve(semantic.Linear, deletedId, indexes);
                SerializedProperty choices = node.FindPropertyRelative("choices");
                for (int choice = 0; choice < choices.arraySize; choice++)
                    choices.GetArrayElementAtIndex(choice).FindPropertyRelative("nextNodeIndex").intValue =
                        Resolve(semantic.Choices[choice], deletedId, indexes);
            }
        }

        private static int Resolve(string id, string deletedId, IReadOnlyDictionary<string, int> indexes) =>
            string.IsNullOrEmpty(id) || id == deletedId || !indexes.TryGetValue(id, out int index) ? -1 : index;

        private static DialogueGraphModel RequireValidStructure(DialogueTree tree)
        {
            DialogueGraphModel model = DialogueGraphModelBuilder.Build(tree);
            if (model.DuplicateStableIds.Count > 0 || model.Nodes.Any(node => node.StableId.StartsWith("<missing-id-", StringComparison.Ordinal)))
                throw new InvalidOperationException("Structural dialogue edits require unique, non-empty node IDs.");
            return model;
        }

        private static string RequireUniqueId(DialogueTree tree, string value)
        {
            string id = Normalize(value);
            if (id.Length == 0) throw new ArgumentException("Node ID is required.", nameof(value));
            if ((tree.Nodes ?? Array.Empty<DialogueTree.Node>()).Any(node => Normalize(node?.id) == id))
                throw new InvalidOperationException($"Dialogue node ID '{id}' already exists.");
            return id;
        }

        private static int FindIndex(DialogueTree tree, string id)
        {
            string value = Normalize(id);
            int index = Array.FindIndex(tree.Nodes ?? Array.Empty<DialogueTree.Node>(), node => Normalize(node?.id) == value);
            if (index < 0) throw new InvalidOperationException($"Dialogue node '{value}' was not found.");
            return index;
        }

        private static int FindIndex(SerializedProperty nodes, string id)
        {
            for (int index = 0; index < nodes.arraySize; index++)
                if (nodes.GetArrayElementAtIndex(index).FindPropertyRelative("id").stringValue.Trim() == id) return index;
            return nodes.arraySize == 0 ? -1 : 0;
        }

        private static string EntryId(DialogueTree tree) =>
            tree.StartNodeIndex >= 0 && tree.StartNodeIndex < (tree.Nodes?.Length ?? 0)
                ? Normalize(tree.Nodes[tree.StartNodeIndex]?.id) : string.Empty;

        private static void RequireEditable(DialogueTree tree)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            if (tree.GeneratedFromJson) throw new InvalidOperationException(
                "Generated dialogue is read-only. Create an editable local copy before changing it.");
        }

        private static void Mutate(DialogueTree tree, string label, Action<SerializedObject> change)
        {
            Undo.RegisterCompleteObjectUndo(tree, label);
            var serialized = new SerializedObject(tree);
            serialized.Update();
            change(serialized);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(tree);
        }

        private static int ComputeVersion(DialogueTree tree)
        {
            unchecked
            {
                int hash = tree.StartNodeIndex;
                foreach (DialogueTree.Node node in tree.Nodes ?? Array.Empty<DialogueTree.Node>())
                {
                    hash = hash * 31 + Normalize(node?.id).GetHashCode();
                    hash = hash * 31 + (node?.nextNodeIndex ?? -1);
                    foreach (DialogueTree.Choice choice in node?.choices ?? Array.Empty<DialogueTree.Choice>())
                        hash = hash * 31 + (choice?.nextNodeIndex ?? -1);
                }
                return hash;
            }
        }

        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
    }
}
