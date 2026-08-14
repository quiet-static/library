using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Flags;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Dialogue
{
    /// <summary>Exports an existing index-linked DialogueTree to ID-linked authorer JSON.</summary>
    public static class DialogueJsonExporter
    {
        private const string NullTargetSentinel =
            "__QUIET_STATIC_JSON_NULL_TARGET_C8D7B38E__";

        [Serializable]
        private sealed class Document
        {
            public int schemaVersion = 1;
            public string treeId;
            public string unityAssetPath;
            public string startNode;
            public Node[] nodes;
        }

        [Serializable]
        private sealed class LinkedDocument
        {
            public int schemaVersion = 1;
            public string treeId;
            public string unityAssetPath;
            public string flagCatalog;
            public string startNode;
            public Node[] nodes;
        }

        [Serializable]
        private sealed class Node
        {
            public string id;
            public string speaker;
            public string text;
            public string next;
            public string[] flagsToSetOnEnter;
            public Choice[] choices;
        }

        [Serializable]
        private sealed class Choice
        {
            public string text;
            public string next;
            public string[] flagsToSet;
            public Condition condition;
        }

        [Serializable]
        private sealed class Condition
        {
            public string mode;
            public string[] flags;
        }

        /// <summary>Validates a tree for a lossless authorer migration.</summary>
        public static IReadOnlyList<string> Validate(
            DialogueTree tree,
            string treeId = null,
            string flagCatalog = null)
        {
            var errors = new List<string>();
            if (tree == null)
            {
                errors.Add("A DialogueTree is required.");
                return errors.AsReadOnly();
            }

            string resolvedTreeId = NarrativeJsonPathUtility.ResolveAssetIdentity(tree, treeId);
            NarrativeJsonPathUtility.ValidateIdentity(resolvedTreeId, "treeId", errors);
            if (NarrativeJsonPathUtility.GetUnityAssetPath(tree) == null)
                errors.Add("The DialogueTree must be saved as an asset below Assets.");
            if (flagCatalog != null)
                ValidateFlagCatalogPath(flagCatalog, errors);

            DialogueTree.Node[] nodes = tree.Nodes;
            if (nodes == null || nodes.Length == 0)
            {
                errors.Add("The DialogueTree must contain at least one node.");
                return errors.AsReadOnly();
            }
            if (tree.StartNodeIndex < 0 || tree.StartNodeIndex >= nodes.Length)
                errors.Add($"Start node index {tree.StartNodeIndex} is outside the node array.");

            BuildNodeIds(nodes, errors);
            for (int nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex++)
            {
                DialogueTree.Node node = nodes[nodeIndex];
                string at = $"Node at index {nodeIndex}";
                if (node == null)
                {
                    errors.Add($"{at} is missing.");
                    continue;
                }

                ValidateTargetIndex(node.nextNodeIndex, nodes.Length, $"{at} nextNodeIndex", errors);
                ValidateIds(node.flagsToSetOnEnter, $"{at} flagsToSetOnEnter", errors);
                DialogueTree.Choice[] choices = node.choices ?? Array.Empty<DialogueTree.Choice>();
                for (int choiceIndex = 0; choiceIndex < choices.Length; choiceIndex++)
                {
                    DialogueTree.Choice choice = choices[choiceIndex];
                    string choiceAt = $"{at}, choice {choiceIndex}";
                    if (choice == null)
                    {
                        errors.Add($"{choiceAt} is missing.");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(choice.text))
                        errors.Add($"{choiceAt} text must be non-empty.");
                    ValidateTargetIndex(
                        choice.nextNodeIndex,
                        nodes.Length,
                        $"{choiceAt} nextNodeIndex",
                        errors);
                    ValidateIds(choice.flagsToSet, $"{choiceAt} flagsToSet", errors);
                    ValidateRequirement(
                        choice.availabilityRequirement,
                        $"{choiceAt} condition",
                        errors);
                }
            }
            return errors.AsReadOnly();
        }

        /// <summary>
        /// Builds version-one authorer JSON while preserving both linear fallback and choices.
        /// Missing legacy node IDs receive deterministic IDs without modifying the source asset.
        /// </summary>
        public static string BuildJson(
            DialogueTree tree,
            string treeId = null,
            string flagCatalog = null)
        {
            IReadOnlyList<string> errors = Validate(tree, treeId, flagCatalog);
            if (errors.Count > 0)
                throw new ArgumentException(string.Join(Environment.NewLine, errors));

            DialogueTree.Node[] sourceNodes = tree.Nodes;
            string[] ids = BuildNodeIds(sourceNodes, new List<string>());
            Node[] nodes = sourceNodes.Select((node, index) => new Node
            {
                id = ids[index],
                speaker = node.speaker ?? string.Empty,
                text = node.line ?? string.Empty,
                next = ResolveTargetId(node.nextNodeIndex, ids),
                flagsToSetOnEnter = node.flagsToSetOnEnter ?? Array.Empty<string>(),
                choices = (node.choices ?? Array.Empty<DialogueTree.Choice>())
                    .Select(choice => new Choice
                    {
                        text = choice.text,
                        next = ResolveTargetId(choice.nextNodeIndex, ids),
                        flagsToSet = choice.flagsToSet ?? Array.Empty<string>(),
                        condition = BuildCondition(choice.availabilityRequirement),
                    }).ToArray(),
            }).ToArray();
            string resolvedTreeId = NarrativeJsonPathUtility.ResolveAssetIdentity(tree, treeId);
            string unityAssetPath = NarrativeJsonPathUtility.GetUnityAssetPath(tree);
            object document = flagCatalog == null
                ? new Document
                {
                    treeId = resolvedTreeId,
                    unityAssetPath = unityAssetPath,
                    startNode = ids[tree.StartNodeIndex],
                    nodes = nodes,
                }
                : new LinkedDocument
                {
                    treeId = resolvedTreeId,
                    unityAssetPath = unityAssetPath,
                    flagCatalog = flagCatalog,
                    startNode = ids[tree.StartNodeIndex],
                    nodes = nodes,
                };
            string json = JsonUtility.ToJson(document, true)
                .Replace("\"" + NullTargetSentinel + "\"", "null") +
                Environment.NewLine;
            DialogueJsonImporter.ValidateJson(json, "exported dialogue");
            return json;
        }

        /// <summary>Exports one DialogueTree to an authorer JSON file.</summary>
        public static string Export(
            DialogueTree tree,
            string outputPath,
            string treeId = null,
            string flagCatalog = null) =>
            NarrativeJsonPathUtility.WriteJson(
                outputPath,
                BuildJson(tree, treeId, flagCatalog));

        /// <summary>Prompts for a destination and exports one DialogueTree.</summary>
        public static string ExportWithSavePanel(DialogueTree tree)
        {
            if (tree == null)
                throw new ArgumentNullException(nameof(tree));
            string id = NarrativeJsonPathUtility.ResolveAssetIdentity(tree, null);
            string path = EditorUtility.SaveFilePanel(
                "Export Dialogue Tree JSON",
                NarrativeJsonPathUtility.GetInitialFolder(tree),
                id + ".json",
                "json");
            return string.IsNullOrEmpty(path) ? null : Export(tree, path);
        }

        [MenuItem("Tools/Quiet Static/Dialogue/Export Selected Dialogue Tree JSON...")]
        private static void ExportSelected()
        {
            var tree = Selection.activeObject as DialogueTree;
            try
            {
                string path = ExportWithSavePanel(tree);
                if (path != null)
                    GameLogger.Log(nameof(DialogueJsonExporter), tree,
                        $"Exported dialogue authoring JSON to {path}.");
            }
            catch (Exception exception)
            {
                GameLogger.Error(nameof(DialogueJsonExporter), tree,
                    $"Dialogue export failed: {exception.Message}");
                EditorUtility.DisplayDialog("Dialogue Export Failed", exception.Message, "OK");
            }
        }

        [MenuItem("Tools/Quiet Static/Dialogue/Export Selected Dialogue Tree JSON...", true)]
        private static bool CanExportSelected() => Selection.activeObject is DialogueTree;

        private static string[] BuildNodeIds(
            IReadOnlyList<DialogueTree.Node> nodes,
            ICollection<string> errors)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            var ids = new string[nodes.Count];
            for (int index = 0; index < nodes.Count; index++)
            {
                string id = nodes[index]?.id;
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                if (!string.Equals(id, id.Trim(), StringComparison.Ordinal))
                    errors.Add($"Node at index {index} ID must not have surrounding whitespace.");
                else if (!used.Add(id))
                    errors.Add($"Node ID '{id}' is duplicated.");
                ids[index] = id;
            }

            for (int index = 0; index < nodes.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(ids[index]))
                    continue;
                string stem = $"node_{index + 1:000}";
                string candidate = stem;
                int suffix = 2;
                while (!used.Add(candidate))
                    candidate = stem + "_" + suffix++;
                ids[index] = candidate;
            }
            return ids;
        }

        private static string ResolveTargetId(int index, IReadOnlyList<string> ids) =>
            index < 0 ? NullTargetSentinel : ids[index];

        private static Condition BuildCondition(FlagRequirement requirement) => new()
        {
            mode = (requirement?.Mode ?? FlagRequirementMode.None).ToString(),
            flags = requirement?.Flags.ToArray() ?? Array.Empty<string>(),
        };

        private static void ValidateTargetIndex(
            int index,
            int nodeCount,
            string label,
            ICollection<string> errors)
        {
            if (index < -1 || index >= nodeCount)
                errors.Add($"{label} must be -1 or a valid node index.");
        }

        private static void ValidateRequirement(
            FlagRequirement requirement,
            string label,
            ICollection<string> errors)
        {
            FlagRequirementMode mode = requirement?.Mode ?? FlagRequirementMode.None;
            IReadOnlyList<string> flags = requirement?.Flags ?? Array.Empty<string>();
            ValidateIds(flags, label + " flags", errors);
            if (mode != FlagRequirementMode.None && flags.Count == 0)
                errors.Add($"{label} needs at least one flag for mode {mode}.");
        }

        private static void ValidateIds(
            IEnumerable<string> values,
            string label,
            ICollection<string> errors)
        {
            if (values == null)
                return;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int index = 0;
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    errors.Add($"{label}[{index}] must be non-empty.");
                else
                {
                    string normalized = value.Trim();
                    if (!string.Equals(value, normalized, StringComparison.Ordinal))
                        errors.Add($"{label}[{index}] must not have surrounding whitespace.");
                    if (!ids.Add(normalized))
                        errors.Add($"{label} contains duplicate ID '{normalized}'.");
                }
                index++;
            }
        }

        private static void ValidateFlagCatalogPath(
            string path,
            ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !string.Equals(path, path.Trim(), StringComparison.Ordinal) ||
                Path.IsPathRooted(path) ||
                path.IndexOf('\\') >= 0 ||
                !string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
                errors.Add("flagCatalog must be a portable relative JSON path.");
        }
    }
}
