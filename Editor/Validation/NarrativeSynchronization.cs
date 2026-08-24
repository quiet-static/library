using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using QuietStatic.Toolkit.Editor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Validation
{
    /// <summary>Read-only semantic synchronization checks for authoring and generated JSON.</summary>
    public static class NarrativeSynchronization
    {
        public const string MissingAuthoringSourceCode = "QS1101";
        public const string StaleGeneratedSourceCode = "QS1102";
        public const string MissingGeneratedSourceCode = "QS1103";
        public const string OrphanGeneratedSourceCode = "QS1104";

        public static IReadOnlyList<ValidationIssue> ScanProject()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string authoringRoot = Path.Combine(projectRoot, "Authoring", "Narrative");
            string generatedRoot = Path.Combine(projectRoot, "Assets", "Generated", "NarrativeSources");
            string manifestPath = Path.Combine(
                generatedRoot,
                NarrativeBatchJsonImporter.ManifestFileName);
            var issues = new List<ValidationIssue>();
            if (!File.Exists(manifestPath) || !Directory.Exists(authoringRoot))
            {
                return issues;
            }

            NarrativeBatchJsonImporter.Plan plan = NarrativeBatchJsonImporter.Preflight(manifestPath);
            var expected = new HashSet<string>(
                plan.Documents.Select(document => Normalize(document.RelativePath)),
                StringComparer.OrdinalIgnoreCase);
            foreach (NarrativeBatchJsonImporter.Document document in plan.Documents)
            {
                string relative = Normalize(document.RelativePath);
                string authoringPath = Path.Combine(authoringRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(authoringPath))
                {
                    issues.Add(Issue(
                        MissingAuthoringSourceCode,
                        $"Generated narrative source has no authoring counterpart: {relative}",
                        relative));
                    continue;
                }

                if (!SemanticJsonEquals(File.ReadAllText(authoringPath), File.ReadAllText(document.SourcePath)))
                {
                    issues.Add(Issue(
                        StaleGeneratedSourceCode,
                        $"Generated narrative source differs from authoring: {relative}",
                        relative));
                }
            }

            foreach (string sourcePath in Directory.GetFiles(authoringRoot, "*.json", SearchOption.AllDirectories))
            {
                string relative = Normalize(Path.GetRelativePath(authoringRoot, sourcePath));
                if (relative == "narrative-project.json" || expected.Contains(relative)) continue;
                issues.Add(Issue(
                    MissingGeneratedSourceCode,
                    $"Authoring narrative source is absent from the generated manifest: {relative}",
                    relative));
            }

            foreach (string generatedPath in Directory.GetFiles(generatedRoot, "*.json", SearchOption.AllDirectories))
            {
                string relative = Normalize(Path.GetRelativePath(generatedRoot, generatedPath));
                if (relative == NarrativeBatchJsonImporter.ManifestFileName || expected.Contains(relative)) continue;
                issues.Add(Issue(
                    OrphanGeneratedSourceCode,
                    $"Generated narrative source is not declared by the manifest: {relative}",
                    relative));
            }

            return ValidationIssueOrdering.Sort(issues);
        }

        /// <summary>Compares JSON while ignoring whitespace outside quoted strings.</summary>
        public static bool SemanticJsonEquals(string left, string right) =>
            string.Equals(Compact(left), Compact(right), StringComparison.Ordinal);

        private static string Compact(string json)
        {
            var result = new StringBuilder(json?.Length ?? 0);
            bool inString = false;
            bool escaped = false;
            foreach (char character in json ?? string.Empty)
            {
                if (inString)
                {
                    result.Append(character);
                    if (escaped) escaped = false;
                    else if (character == '\\') escaped = true;
                    else if (character == '"') inString = false;
                }
                else if (character == '"')
                {
                    inString = true;
                    result.Append(character);
                }
                else if (!char.IsWhiteSpace(character))
                {
                    result.Append(character);
                }
            }
            return result.ToString();
        }

        private static string Normalize(string path) => path.Replace('\\', '/');

        private static ValidationIssue Issue(string code, string message, string path) =>
            new(ValidationSeverity.Error, "Narrative Synchronization", message, null, path, code);
    }
}
