using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Editor.Validation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace QuietStatic.Toolkit.Editor.Tooling
{
    /// <summary>One navigable serialized reference displayed by shared tooling.</summary>
    public sealed class ReferenceResult
    {
        public ReferenceResult(string message, UnityEngine.Object context = null, string assetPath = null)
        {
            Message = message ?? string.Empty;
            Context = context;
            AssetPath = assetPath ?? string.Empty;
        }

        public string Message { get; }
        public UnityEngine.Object Context { get; }
        public string AssetPath { get; }
    }

    /// <summary>Reusable UI Toolkit list for references with consistent navigation behavior.</summary>
    public sealed class ReferenceResultsPanel : VisualElement
    {
        private readonly Label heading = new("References");
        private readonly VisualElement rows = new();

        public ReferenceResultsPanel()
        {
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            Add(heading);
            Add(rows);
        }

        public void SetResults(string title, IEnumerable<ReferenceResult> results)
        {
            heading.text = title ?? "References";
            rows.Clear();
            foreach (ReferenceResult result in results ?? Array.Empty<ReferenceResult>())
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                row.Add(new Label(result.Message) { style = { flexGrow = 1f } });
                var select = new Button(() => Select(result)) { text = "Select" };
                select.SetEnabled(result.Context != null || result.AssetPath.Length > 0);
                row.Add(select);
                rows.Add(row);
            }
            if (!rows.Children().Any()) rows.Add(new Label("No serialized references found."));
        }

        private static void Select(ReferenceResult result)
        {
            UnityEngine.Object target = result.Context;
            if (target == null && result.AssetPath.Length > 0)
                target = AssetDatabase.LoadMainAssetAtPath(result.AssetPath);
            if (target == null) return;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }
    }

    /// <summary>Reusable validation list ordered by the package's shared issue ordering.</summary>
    public sealed class ValidationResultsPanel : VisualElement
    {
        private readonly Label summary = new();
        private readonly VisualElement rows = new();

        public ValidationResultsPanel()
        {
            Add(summary);
            Add(rows);
        }

        public void SetIssues(IEnumerable<ValidationIssue> issues)
        {
            ValidationIssue[] values = (issues ?? Array.Empty<ValidationIssue>())
                .OrderByDescending(issue => issue.Severity)
                .ThenBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                .ToArray();
            int errors = values.Count(issue => issue.Severity == ValidationSeverity.Error);
            int warnings = values.Count(issue => issue.Severity == ValidationSeverity.Warning);
            summary.text = $"{errors} error(s), {warnings} warning(s), {values.Length} total";
            rows.Clear();
            foreach (ValidationIssue issue in values)
            {
                var button = new Button(() => Select(issue))
                {
                    text = $"[{issue.Code}] {issue.Severity}: {issue.Message}"
                };
                button.SetEnabled(issue.Context != null || issue.AssetPath.Length > 0);
                rows.Add(button);
            }
        }

        private static void Select(ValidationIssue issue)
        {
            UnityEngine.Object target = issue.Context;
            if (target == null && issue.AssetPath.Length > 0)
                target = AssetDatabase.LoadMainAssetAtPath(issue.AssetPath);
            if (target == null) return;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }
    }
}
