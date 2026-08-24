using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Validation
{
    /// <summary>Shared IMGUI presentation for navigable validation results.</summary>
    public abstract class ValidationWindowBase : EditorWindow
    {
        private IReadOnlyList<ValidationIssue> issues = new List<ValidationIssue>();
        private Vector2 scroll;
        private string search = string.Empty;

        protected abstract string EmptyMessage { get; }
        protected abstract IReadOnlyList<ValidationIssue> RunScan();

        protected virtual void OnEnable()
        {
            Refresh();
        }

        protected void Refresh()
        {
            issues = RunScan() ?? new List<ValidationIssue>();
            Repaint();
        }

        protected virtual void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Scan", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    Refresh();
                }

                GUILayout.FlexibleSpace();
                search = GUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField"),
                    GUILayout.MinWidth(180f));
            }

            int errors = issues.Count(issue => issue.Severity == ValidationSeverity.Error);
            int warnings = issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
            int infos = issues.Count(issue => issue.Severity == ValidationSeverity.Info);
            EditorGUILayout.HelpBox(
                $"{errors} error(s), {warnings} warning(s), {infos} info message(s). Scans never modify content.",
                errors > 0 ? MessageType.Error : warnings > 0 ? MessageType.Warning : MessageType.Info);

            IEnumerable<ValidationIssue> visible = issues.Where(MatchesSearch)
                .OrderBy(issue => issue.Severity)
                .ThenBy(issue => issue.Category)
                .ThenBy(issue => issue.Message);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            bool drewAny = false;
            foreach (ValidationIssue issue in visible)
            {
                drewAny = true;
                DrawIssue(issue);
            }

            if (!drewAny)
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrWhiteSpace(search) ? EmptyMessage : "No results match the search.",
                    MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
        }

        private bool MatchesSearch(ValidationIssue issue)
        {
            return string.IsNullOrWhiteSpace(search) ||
                   issue.Category.ToLowerInvariant().Contains(search.ToLowerInvariant()) ||
                   issue.Message.ToLowerInvariant().Contains(search.ToLowerInvariant());
        }

        private static void DrawIssue(ValidationIssue issue)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(issue.Severity.ToString(), EditorStyles.boldLabel, GUILayout.Width(65f));
                    GUILayout.Label(issue.Code, EditorStyles.miniBoldLabel, GUILayout.Width(65f));
                    GUILayout.Label(issue.Category, EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(issue.Context == null &&
                                                       string.IsNullOrEmpty(issue.AssetPath)))
                    {
                        if (GUILayout.Button("Select", GUILayout.Width(60f)))
                        {
                            Navigate(issue);
                        }
                    }
                }

                EditorGUILayout.LabelField(issue.Message, EditorStyles.wordWrappedLabel);
                if (!string.IsNullOrEmpty(issue.AssetPath))
                {
                    EditorGUILayout.LabelField(issue.AssetPath, EditorStyles.miniLabel);
                }
            }
        }

        private static void Navigate(ValidationIssue issue)
        {
            UnityEngine.Object target = issue.Context;
            if (target == null && !string.IsNullOrEmpty(issue.AssetPath))
            {
                target = AssetDatabase.LoadMainAssetAtPath(issue.AssetPath);
            }

            if (target != null)
            {
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            }
        }
    }

    /// <summary>Project narrative validation entry point.</summary>
    public sealed class NarrativeValidationWindow : ValidationWindowBase
    {
        protected override string EmptyMessage => "No narrative issues were found.";

        public static void Open()
        {
            GetWindow<NarrativeValidationWindow>("Narrative Validation");
        }

        protected override IReadOnlyList<ValidationIssue> RunScan()
        {
            return ToolkitValidation.ScanNarrative();
        }
    }

    /// <summary>Validation entry point for currently loaded scenes and build settings.</summary>
    public sealed class SceneSetupValidationWindow : ValidationWindowBase
    {
        protected override string EmptyMessage => "No open-scene setup issues were found.";

        public static void Open()
        {
            GetWindow<SceneSetupValidationWindow>("Scene Setup Validation");
        }

        protected override IReadOnlyList<ValidationIssue> RunScan()
        {
            return ToolkitValidation.ScanOpenScenes();
        }
    }

    /// <summary>Validation entry point for package paths and project build configuration.</summary>
    public sealed class ArchitectureValidationWindow : ValidationWindowBase
    {
        protected override string EmptyMessage => "No project architecture issues were found.";

        public static void Open()
        {
            GetWindow<ArchitectureValidationWindow>("Architecture Validation");
        }

        protected override IReadOnlyList<ValidationIssue> RunScan()
        {
            return ValidationIssueOrdering.Sort(
                ToolkitValidation.ScanOpenScenes()
                    .Concat(ArchitectureValidation.ScanProjectConfiguration()));
        }
    }
}
