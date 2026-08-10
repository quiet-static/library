using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor.Validation;
using QuietStatic.Toolkit.Flags;
using UnityEditor;

namespace QuietStatic.Toolkit.Editor.Dialogue
{
    /// <summary>Read-only analysis of one dialogue node.</summary>
    public sealed class DialogueNodeReport
    {
        public DialogueNodeReport(
            int index,
            DialogueTree.Node node,
            bool reachable,
            IReadOnlyList<ValidationIssue> issues)
        {
            Index = index;
            Node = node;
            Reachable = reachable;
            Issues = issues;
        }

        public int Index { get; }
        public DialogueTree.Node Node { get; }
        public bool Reachable { get; }
        public IReadOnlyList<ValidationIssue> Issues { get; }

        public IEnumerable<string> Flags
        {
            get
            {
                if (Node == null)
                {
                    yield break;
                }

                foreach (string flag in Node.flagsToSetOnEnter ?? Array.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(flag))
                    {
                        yield return flag.Trim();
                    }
                }

                foreach (DialogueTree.Choice choice in Node.choices ?? Array.Empty<DialogueTree.Choice>())
                {
                    if (choice == null)
                    {
                        continue;
                    }

                    foreach (string flag in choice.flagsToSet ?? Array.Empty<string>())
                    {
                        if (!string.IsNullOrWhiteSpace(flag))
                        {
                            yield return flag.Trim();
                        }
                    }
                }
            }
        }
    }

    /// <summary>Read-only project report for one dialogue asset.</summary>
    public sealed class DialogueAssetReport
    {
        public DialogueAssetReport(
            DialogueTree tree,
            string path,
            IReadOnlyList<DialogueNodeReport> nodes,
            IReadOnlyList<ValidationIssue> issues,
            IReadOnlyList<string> referencePaths)
        {
            Tree = tree;
            Path = path;
            Nodes = nodes;
            Issues = issues;
            ReferencePaths = referencePaths;
        }

        public DialogueTree Tree { get; }
        public string Path { get; }
        public IReadOnlyList<DialogueNodeReport> Nodes { get; }
        public IReadOnlyList<ValidationIssue> Issues { get; }
        public IReadOnlyList<string> ReferencePaths { get; }
        public int ErrorCount => Issues.Count(issue => issue.Severity == ValidationSeverity.Error);
        public int WarningCount => Issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
    }

    /// <summary>
    /// Builds deterministic, non-mutating reports for the project's existing
    /// index-linked <see cref="DialogueTree"/> assets.
    /// </summary>
    public static class DialogueAnalysis
    {
        public static IReadOnlyList<DialogueAssetReport> ScanProject()
        {
            var knownFlags = new HashSet<string>(
                AssetDatabase.FindAssets("t:FlagDatabase")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<FlagDatabase>)
                    .Where(database => database != null)
                    .SelectMany(database => database.Flags ?? Array.Empty<FlagDatabase.FlagDefinition>())
                    .Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.id))
                    .Select(definition => definition.id.Trim()),
                StringComparer.Ordinal);

            bool validateFlags = AssetDatabase.FindAssets("t:FlagDatabase").Length > 0;
            string[] dialoguePaths = AssetDatabase.FindAssets("t:DialogueTree")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var referencesByDialogue = dialoguePaths.ToDictionary(
                path => path,
                _ => new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            string[] candidateReferencePaths = AssetDatabase.GetAllAssetPaths()
                .Where(IsReferenceCandidate)
                .ToArray();
            foreach (string candidate in candidateReferencePaths)
            {
                foreach (string dependency in AssetDatabase.GetDependencies(candidate, false))
                {
                    if (!string.Equals(candidate, dependency, StringComparison.OrdinalIgnoreCase) &&
                        referencesByDialogue.TryGetValue(dependency, out List<string> references))
                    {
                        references.Add(candidate);
                    }
                }
            }

            return dialoguePaths
                .Select(path => CreateReport(
                    AssetDatabase.LoadAssetAtPath<DialogueTree>(path),
                    path,
                    knownFlags,
                    validateFlags,
                    referencesByDialogue[path]))
                .Where(report => report != null)
                .ToArray();
        }

        private static DialogueAssetReport CreateReport(
            DialogueTree tree,
            string path,
            HashSet<string> knownFlags,
            bool validateFlags,
            IEnumerable<string> referencePaths)
        {
            if (tree == null)
            {
                return null;
            }

            DialogueTree.Node[] nodes = tree.Nodes ?? Array.Empty<DialogueTree.Node>();
            var reachable = new HashSet<int>();
            Visit(tree.StartNodeIndex, nodes, reachable);
            var assetIssues = new List<ValidationIssue>();
            var nodeReports = new List<DialogueNodeReport>();

            if (nodes.Length == 0)
            {
                assetIssues.Add(Issue(
                    ValidationSeverity.Error, "Dialogue has no nodes.", tree, path));
            }

            if (tree.StartNodeIndex < 0 || tree.StartNodeIndex >= nodes.Length)
            {
                assetIssues.Add(Issue(
                    ValidationSeverity.Error,
                    $"Start node index {tree.StartNodeIndex} is outside the node array.",
                    tree, path));
            }

            for (int index = 0; index < nodes.Length; index++)
            {
                var nodeIssues = new List<ValidationIssue>();
                DialogueTree.Node node = nodes[index];
                if (node == null)
                {
                    nodeIssues.Add(Issue(
                        ValidationSeverity.Error, $"Node {index} is null.", tree, path));
                }
                else
                {
                    ValidateNode(index, node, nodes.Length, reachable.Contains(index),
                        knownFlags, validateFlags, tree, path, nodeIssues);
                }

                assetIssues.AddRange(nodeIssues);
                nodeReports.Add(new DialogueNodeReport(
                    index, node, reachable.Contains(index), nodeIssues));
            }

            string[] references = referencePaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new DialogueAssetReport(tree, path, nodeReports, assetIssues, references);
        }

        private static void ValidateNode(
            int index,
            DialogueTree.Node node,
            int nodeCount,
            bool reachable,
            HashSet<string> knownFlags,
            bool validateFlags,
            DialogueTree tree,
            string path,
            ICollection<ValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(node.speaker))
            {
                issues.Add(Issue(
                    ValidationSeverity.Warning,
                    $"Node {index} has no speaker. Ignore this for intentional narration.",
                    tree, path));
            }

            if (string.IsNullOrWhiteSpace(node.line))
            {
                issues.Add(Issue(
                    ValidationSeverity.Warning, $"Node {index} has empty dialogue text.", tree, path));
            }

            if (!reachable)
            {
                issues.Add(Issue(
                    ValidationSeverity.Warning,
                    $"Node {index} is unreachable from start node {tree.StartNodeIndex}.",
                    tree, path));
            }

            ValidateTarget(node.nextNodeIndex, nodeCount, $"Node {index}", tree, path, issues);
            ValidateFlags(node.flagsToSetOnEnter, knownFlags, validateFlags,
                $"Node {index}", tree, path, issues);

            DialogueTree.Choice[] choices = node.choices ?? Array.Empty<DialogueTree.Choice>();
            for (int choiceIndex = 0; choiceIndex < choices.Length; choiceIndex++)
            {
                DialogueTree.Choice choice = choices[choiceIndex];
                if (choice == null)
                {
                    issues.Add(Issue(
                        ValidationSeverity.Error,
                        $"Node {index}, choice {choiceIndex} is null.",
                        tree, path));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(choice.text))
                {
                    issues.Add(Issue(
                        ValidationSeverity.Warning,
                        $"Node {index}, choice {choiceIndex} has empty text.",
                        tree, path));
                }

                ValidateTarget(choice.nextNodeIndex, nodeCount,
                    $"Node {index}, choice {choiceIndex}", tree, path, issues);
                ValidateFlags(choice.flagsToSet, knownFlags, validateFlags,
                    $"Node {index}, choice {choiceIndex}", tree, path, issues);
            }
        }

        private static void ValidateTarget(
            int target,
            int nodeCount,
            string owner,
            DialogueTree tree,
            string path,
            ICollection<ValidationIssue> issues)
        {
            if (target < -1 || target >= nodeCount)
            {
                issues.Add(Issue(
                    ValidationSeverity.Error,
                    $"{owner} points to invalid node index {target}.",
                    tree, path));
            }
        }

        private static void ValidateFlags(
            IEnumerable<string> flags,
            HashSet<string> knownFlags,
            bool validateFlags,
            string owner,
            DialogueTree tree,
            string path,
            ICollection<ValidationIssue> issues)
        {
            if (!validateFlags || flags == null)
            {
                return;
            }

            foreach (string flag in flags.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                if (!knownFlags.Contains(flag.Trim()))
                {
                    issues.Add(Issue(
                        ValidationSeverity.Error,
                        $"{owner} references unknown flag '{flag}'.",
                        tree, path));
                }
            }
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

        private static ValidationIssue Issue(
            ValidationSeverity severity,
            string message,
            DialogueTree context,
            string path)
        {
            return new ValidationIssue(severity, "Dialogue", message, context, path);
        }

        private static bool IsReferenceCandidate(string path)
        {
            return path.StartsWith("Assets/", StringComparison.Ordinal) &&
                   (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));
        }
    }
}
