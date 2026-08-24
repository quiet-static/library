using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Validation
{
    /// <summary>Non-interactive validation entry point for local automation and CI.</summary>
    public static class ArchitectureValidationBatch
    {
        [Serializable]
        private sealed class Summary
        {
            public int errors;
            public int warnings;
            public SummaryIssue[] issues;
        }

        [Serializable]
        private sealed class SummaryIssue
        {
            public string code;
            public string severity;
            public string category;
            public string message;
            public string assetPath;
        }

        /// <summary>
        /// Runs all currently available project checks, logs stable diagnostics, and
        /// exits Unity with a failure code when any error is present.
        /// </summary>
        public static void Run()
        {
            RunWithPolicy(development: true);
        }

        /// <summary>Runs validation with release severity and exits nonzero on promoted errors.</summary>
        public static void RunRelease()
        {
            RunWithPolicy(development: false);
        }

        private static void RunWithPolicy(bool development)
        {
            IReadOnlyList<ValidationIssue> issues =
                ArchitectureBuildPreflight.EvaluateForBuild(
                    ScanAllBuildInputs(),
                    development);

            foreach (ValidationIssue issue in issues)
            {
                string message =
                    $"[{issue.Code}] {issue.Severity} | {issue.Category} | {issue.Message}" +
                    (string.IsNullOrEmpty(issue.AssetPath)
                        ? string.Empty
                        : $" | {issue.AssetPath}");
                if (issue.Severity == ValidationSeverity.Error)
                {
                    Debug.LogError(message, issue.Context);
                }
                else if (issue.Severity == ValidationSeverity.Warning)
                {
                    Debug.LogWarning(message, issue.Context);
                }
                else
                {
                    Debug.Log(message, issue.Context);
                }
            }

            int errors = issues.Count(issue => issue.Severity == ValidationSeverity.Error);
            int warnings = issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
            WriteSummary(issues, errors, warnings);
            Debug.Log($"Architecture validation completed: {errors} error(s), {warnings} warning(s), {issues.Count} total issue(s).");
            EditorApplication.Exit(ArchitectureValidation.GetExitCode(issues));
        }

        /// <summary>Returns the shared deterministic issue set used by batch mode and builds.</summary>
        public static IReadOnlyList<ValidationIssue> ScanAllBuildInputs() =>
            ValidationIssueOrdering.Sort(
                ToolkitValidation.ScanNarrative()
                    .Concat(NarrativeSynchronization.ScanProject())
                    .Concat(ScanBuildScenes())
                    .Concat(ArchitectureValidation.ScanProjectConfiguration()));

        private static void WriteSummary(
            IReadOnlyList<ValidationIssue> issues,
            int errors,
            int warnings)
        {
            string logsDirectory = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../Logs"));
            Directory.CreateDirectory(logsDirectory);
            var summary = new Summary
            {
                errors = errors,
                warnings = warnings,
                issues = issues.Select(issue => new SummaryIssue
                {
                    code = issue.Code,
                    severity = issue.Severity.ToString(),
                    category = issue.Category,
                    message = issue.Message,
                    assetPath = issue.AssetPath,
                }).ToArray(),
            };
            File.WriteAllText(
                Path.Combine(logsDirectory, "ArchitectureValidationSummary.json"),
                JsonUtility.ToJson(summary, true));
        }

        /// <summary>
        /// Loads every enabled build scene additively, validates the resulting composition, and
        /// restores the editor's previous scene setup without saving or dirtying content.
        /// </summary>
        internal static IReadOnlyList<ValidationIssue> ScanBuildScenes()
        {
            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            string[] paths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => AssetDatabase.GUIDToAssetPath(scene.guid))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct()
                .ToArray();

            try
            {
                for (int index = 0; index < paths.Length; index++)
                {
                    EditorSceneManager.OpenScene(
                        paths[index],
                        index == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
                }

                return ToolkitValidation.ScanOpenScenes();
            }
            finally
            {
                if (previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
                else
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                }
            }
        }
    }
}
