using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Dialogue
{
    /// <summary>
    /// Project-wide, read-only browser for the toolkit's existing dialogue assets.
    /// </summary>
    public sealed class DialogueBrowserWindow : EditorWindow
    {
        private IReadOnlyList<DialogueAssetReport> reports = Array.Empty<DialogueAssetReport>();
        private DialogueAssetReport selected;
        private Vector2 assetScroll;
        private Vector2 detailScroll;
        private string search = string.Empty;
        private string speakerFilter = string.Empty;
        private string flagFilter = string.Empty;
        private bool issuesOnly;
        private bool showReferences = true;
        private readonly HashSet<string> expandedNodes = new(StringComparer.Ordinal);

        [MenuItem("Tools/Narrative/Dialogue Browser")]
        public static void Open()
        {
            GetWindow<DialogueBrowserWindow>("Dialogue Browser");
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.HelpBox(
                "Read-only browser. Node flow uses serialized array indexes; this tool never reorders or rewrites them.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawAssetList();
                DrawDetails();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    Refresh();
                }

                GUILayout.Label("Search", GUILayout.Width(45f));
                search = GUILayout.TextField(
                    search, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MinWidth(160f));
                GUILayout.Label("Speaker", GUILayout.Width(50f));
                speakerFilter = GUILayout.TextField(speakerFilter, EditorStyles.toolbarTextField, GUILayout.Width(110f));
                GUILayout.Label("Flag", GUILayout.Width(28f));
                flagFilter = GUILayout.TextField(flagFilter, EditorStyles.toolbarTextField, GUILayout.Width(110f));
                issuesOnly = GUILayout.Toggle(issuesOnly, "Issues only", EditorStyles.toolbarButton, GUILayout.Width(80f));
            }
        }

        private void DrawAssetList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(250f)))
            {
                IReadOnlyList<DialogueAssetReport> visible = reports.Where(AssetMatches).ToArray();
                EditorGUILayout.LabelField(
                    $"Dialogue Assets ({visible.Count}/{reports.Count})", EditorStyles.boldLabel);
                assetScroll = EditorGUILayout.BeginScrollView(assetScroll, EditorStyles.helpBox);
                foreach (DialogueAssetReport report in visible)
                {
                    DrawAssetButton(report);
                }

                if (visible.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        reports.Count == 0
                            ? "No DialogueTree assets were found."
                            : "No dialogue assets match the current filters.",
                        MessageType.Info);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawAssetButton(DialogueAssetReport report)
        {
            Color previous = GUI.backgroundColor;
            if (selected == report)
            {
                GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);
            }

            string issueSuffix = report.ErrorCount > 0 || report.WarningCount > 0
                ? $"  [{report.ErrorCount}E/{report.WarningCount}W]"
                : string.Empty;
            if (GUILayout.Button(
                    new GUIContent($"{report.Tree.name}{issueSuffix}", report.Path),
                    EditorStyles.miniButton,
                    GUILayout.Height(24f)))
            {
                selected = report;
            }
            GUI.backgroundColor = previous;
        }

        private void DrawDetails()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                if (selected == null || !reports.Contains(selected))
                {
                    EditorGUILayout.HelpBox(
                        "Select a dialogue asset to inspect its nodes, issues, and references.",
                        MessageType.Info);
                    return;
                }

                DrawSelectedHeader();
                detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                DrawReferences();

                DialogueNodeReport[] nodes = selected.Nodes.Where(NodeMatches).ToArray();
                EditorGUILayout.LabelField(
                    $"Nodes ({nodes.Length}/{selected.Nodes.Count})", EditorStyles.boldLabel);
                foreach (DialogueNodeReport node in nodes)
                {
                    DrawNode(node);
                }

                if (nodes.Length == 0)
                {
                    EditorGUILayout.HelpBox("No nodes match the current filters.", MessageType.Info);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSelectedHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(selected.Tree.name, EditorStyles.largeLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Select Asset", GUILayout.Width(85f)))
                {
                    Selection.activeObject = selected.Tree;
                    EditorGUIUtility.PingObject(selected.Tree);
                }
                if (GUILayout.Button("Open Asset", GUILayout.Width(75f)))
                {
                    AssetDatabase.OpenAsset(selected.Tree);
                }
            }

            EditorGUILayout.LabelField(selected.Path, EditorStyles.miniLabel);
            MessageType summaryType = selected.ErrorCount > 0
                ? MessageType.Error
                : selected.WarningCount > 0 ? MessageType.Warning : MessageType.Info;
            EditorGUILayout.HelpBox(
                $"{selected.Nodes.Count} node(s), {selected.ErrorCount} error(s), " +
                $"{selected.WarningCount} warning(s), {selected.ReferencePaths.Count} definite asset reference(s).",
                summaryType);
        }

        private void DrawReferences()
        {
            showReferences = EditorGUILayout.Foldout(
                showReferences,
                $"Definite Asset References ({selected.ReferencePaths.Count})",
                true);
            if (!showReferences)
            {
                return;
            }

            if (selected.ReferencePaths.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No serialized Asset Database dependencies reference this dialogue. Runtime-created references are not detectable.",
                    MessageType.Warning);
                return;
            }

            foreach (string path in selected.ReferencePaths)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(path);
                    if (GUILayout.Button("Select", GUILayout.Width(55f)))
                    {
                        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                        Selection.activeObject = asset;
                        if (asset != null)
                        {
                            EditorGUIUtility.PingObject(asset);
                        }
                    }
                }
            }
        }

        private void DrawNode(DialogueNodeReport report)
        {
            string key = $"{selected.Tree.GetInstanceID()}:{report.Index}";
            bool expanded = expandedNodes.Contains(key);
            string speaker = report.Node == null || string.IsNullOrWhiteSpace(report.Node.speaker)
                ? "<No Speaker>"
                : report.Node.speaker;
            int errors = report.Issues.Count(issue => issue.Severity == ValidationSeverity.Error);
            int warnings = report.Issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
            string status = errors > 0 || warnings > 0 ? $" [{errors}E/{warnings}W]" : string.Empty;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool newExpanded = EditorGUILayout.Foldout(
                    expanded,
                    $"Node {report.Index}: {speaker}{status}",
                    true);
                if (newExpanded != expanded)
                {
                    if (newExpanded)
                    {
                        expandedNodes.Add(key);
                    }
                    else
                    {
                        expandedNodes.Remove(key);
                    }
                }

                if (!newExpanded)
                {
                    if (report.Node != null)
                    {
                        EditorGUILayout.LabelField(
                            Shorten(report.Node.line, 110), EditorStyles.miniLabel);
                    }
                    return;
                }

                if (report.Node == null)
                {
                    EditorGUILayout.HelpBox("This node entry is null.", MessageType.Error);
                    return;
                }

                EditorGUILayout.LabelField("Speaker", speaker);
                EditorGUILayout.LabelField("Text", report.Node.line ?? string.Empty, EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("Linear Next", FormatTarget(report.Node.nextNodeIndex));
                EditorGUILayout.LabelField(
                    "Enter Flags",
                    FormatFlags(report.Node.flagsToSetOnEnter));

                DialogueTree.Choice[] choices =
                    report.Node.choices ?? Array.Empty<DialogueTree.Choice>();
                if (choices.Length > 0)
                {
                    EditorGUILayout.Space(2f);
                    EditorGUILayout.LabelField($"Choices ({choices.Length})", EditorStyles.miniBoldLabel);
                    for (int index = 0; index < choices.Length; index++)
                    {
                        DialogueTree.Choice choice = choices[index];
                        if (choice == null)
                        {
                            EditorGUILayout.LabelField($"  {index}. <Null Choice>");
                            continue;
                        }

                        EditorGUILayout.LabelField(
                            $"  {index}. {choice.text}  →  {FormatTarget(choice.nextNodeIndex)}",
                            EditorStyles.wordWrappedMiniLabel);
                        if (choice.flagsToSet != null && choice.flagsToSet.Length > 0)
                        {
                            EditorGUILayout.LabelField(
                                $"     Flags: {FormatFlags(choice.flagsToSet)}",
                                EditorStyles.miniLabel);
                        }
                    }
                }

                foreach (ValidationIssue issue in report.Issues)
                {
                    EditorGUILayout.HelpBox(issue.Message, ToMessageType(issue.Severity));
                }
            }
        }

        private bool AssetMatches(DialogueAssetReport report)
        {
            if (issuesOnly && report.Issues.Count == 0)
            {
                return false;
            }

            bool assetNameMatches = string.IsNullOrWhiteSpace(search) ||
                                    TextContains(report.Tree.name, search);
            return report.Nodes.Any(node =>
                       NodeMatchesSecondaryFilters(node) &&
                       (assetNameMatches || NodeMatchesSearch(node, search))) ||
                   report.Nodes.Count == 0 && assetNameMatches &&
                   string.IsNullOrWhiteSpace(speakerFilter) &&
                   string.IsNullOrWhiteSpace(flagFilter);
        }

        private bool NodeMatches(DialogueNodeReport report)
        {
            if (issuesOnly && report.Issues.Count == 0)
            {
                return false;
            }

            bool searchMatchesAsset = selected != null && TextContains(selected.Tree.name, search);
            return (string.IsNullOrWhiteSpace(search) ||
                    searchMatchesAsset ||
                    NodeMatchesSearch(report, search)) &&
                   NodeMatchesSecondaryFilters(report);
        }

        private bool NodeMatchesSecondaryFilters(DialogueNodeReport report)
        {
            return (!issuesOnly || report.Issues.Count > 0) &&
                   (string.IsNullOrWhiteSpace(speakerFilter) ||
                    TextContains(report.Node?.speaker, speakerFilter)) &&
                   (string.IsNullOrWhiteSpace(flagFilter) ||
                    report.Flags.Any(flag => TextContains(flag, flagFilter)));
        }

        private static bool NodeMatchesSearch(DialogueNodeReport report, string value)
        {
            return string.IsNullOrWhiteSpace(value) ||
                   TextContains(report.Node?.speaker, value) ||
                   TextContains(report.Node?.line, value) ||
                   (report.Node?.choices ?? Array.Empty<DialogueTree.Choice>())
                   .Any(choice => TextContains(choice?.text, value)) ||
                   report.Flags.Any(flag => TextContains(flag, value));
        }

        private static bool TextContains(string source, string value)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                   !string.IsNullOrWhiteSpace(value) &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FormatTarget(int target)
        {
            return target == -1 ? "<End Dialogue>" : $"Node {target}";
        }

        private static string FormatFlags(IEnumerable<string> flags)
        {
            string value = string.Join(", ",
                (flags ?? Array.Empty<string>())
                .Where(flag => !string.IsNullOrWhiteSpace(flag)));
            return string.IsNullOrEmpty(value) ? "<None>" : value;
        }

        private static string Shorten(string value, int length)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "<Empty Text>";
            }
            return value.Length <= length ? value : value.Substring(0, length - 1) + "…";
        }

        private static MessageType ToMessageType(ValidationSeverity severity)
        {
            return severity == ValidationSeverity.Error
                ? MessageType.Error
                : severity == ValidationSeverity.Warning ? MessageType.Warning : MessageType.Info;
        }

        private void Refresh()
        {
            int selectedId = selected?.Tree != null ? selected.Tree.GetInstanceID() : 0;
            reports = DialogueAnalysis.ScanProject();
            selected = reports.FirstOrDefault(report => report.Tree.GetInstanceID() == selectedId)
                       ?? reports.FirstOrDefault(report => Selection.activeObject == report.Tree)
                       ?? reports.FirstOrDefault();
            Repaint();
        }
    }
}
