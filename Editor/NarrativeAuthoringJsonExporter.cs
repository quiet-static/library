using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Editor.Dialogue;
using QuietStatic.Toolkit.Editor.Flags;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Interactions;
using QuietStatic.Toolkit.Objectives;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor
{
    /// <summary>Coordinates a reviewable authoring snapshot of existing narrative assets.</summary>
    public static class NarrativeAuthoringJsonExporter
    {
        /// <summary>Paths written or deliberately preserved by one snapshot export.</summary>
        public sealed class SnapshotResult
        {
            internal SnapshotResult(
                IReadOnlyList<string> writtenPaths,
                IReadOnlyList<string> preservedPaths)
            {
                WrittenPaths = writtenPaths;
                PreservedPaths = preservedPaths;
            }

            /// <summary>Absolute JSON paths written by the export.</summary>
            public IReadOnlyList<string> WrittenPaths { get; }

            /// <summary>Existing source-of-truth paths left unchanged.</summary>
            public IReadOnlyList<string> PreservedPaths { get; }
        }

        private sealed class PendingJson
        {
            public PendingJson(string path, string json)
            {
                Path = path;
                Json = json;
            }

            public string Path { get; }
            public string Json { get; }
        }

        /// <summary>
        /// Exports supplied assets, or discovers their project-wide counterparts when omitted.
        /// </summary>
        /// <remarks>
        /// All JSON is built and importer-validated before the first file is written. Existing
        /// JSON is preserved by default so app-side authoring edits remain the source of
        /// truth. Dialogue is written below dialogues/legacy and linked back to flags.json.
        /// </remarks>
        public static SnapshotResult ExportProjectSnapshot(
            string outputFolder,
            FlagDatabase flagDatabase = null,
            ObjectiveDatabase objectiveDatabase = null,
            IEnumerable<ReadableContentDefinition> readables = null,
            IEnumerable<DialogueTree> dialogues = null,
            bool overwriteExisting = false)
        {
            string folder = ResolveOutputFolder(outputFolder);
            flagDatabase ??= FindSingleOrNull<FlagDatabase>();
            objectiveDatabase ??= FindSingleOrNull<ObjectiveDatabase>();
            ReadableContentDefinition[] readableAssets = readables?.ToArray() ??
                FindAssets<ReadableContentDefinition>();
            DialogueTree[] dialogueAssets = dialogues?.ToArray() ?? FindAssets<DialogueTree>();

            string flagsPath = Path.Combine(folder, "flags.json");
            var pending = new List<PendingJson>();
            var preserved = new List<string>();
            void Queue(string path, Func<string> buildJson)
            {
                if (File.Exists(path) && !overwriteExisting)
                    preserved.Add(path);
                else
                    pending.Add(new PendingJson(path, buildJson()));
            }
            if (flagDatabase != null)
                Queue(flagsPath, () => FlagCatalogJsonExporter.BuildJson(flagDatabase));

            if (objectiveDatabase != null)
            {
                Queue(
                    Path.Combine(folder, "objectives.json"),
                    () => NarrativeContentJsonExporter.BuildObjectivesJson(objectiveDatabase));
            }
            if (readableAssets.Length > 0)
            {
                Queue(
                    Path.Combine(folder, "readables.json"),
                    () => NarrativeContentJsonExporter.BuildReadablesJson(readableAssets, "readables"));
            }

            string flagCatalog = File.Exists(flagsPath) ||
                                 pending.Any(item => PathsEqual(item.Path, flagsPath))
                ? "../../flags.json"
                : null;
            string dialogueFolder = Path.Combine(folder, "dialogues", "legacy");
            var dialogueNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DialogueTree dialogue in dialogueAssets)
            {
                string treeId = NarrativeJsonPathUtility.ResolveAssetIdentity(dialogue, null);
                string safeName = MakeSafeFileName(treeId);
                if (!dialogueNames.Add(safeName))
                    throw new ArgumentException(
                        $"Dialogue tree ID '{treeId}' maps to a duplicate snapshot filename.");
                Queue(
                    Path.Combine(dialogueFolder, safeName + ".json"),
                    () => DialogueJsonExporter.BuildJson(dialogue, treeId, flagCatalog));
            }

            var written = new List<string>(pending.Count);
            foreach (PendingJson item in pending)
                written.Add(NarrativeJsonPathUtility.WriteJson(item.Path, item.Json));
            return new SnapshotResult(written.AsReadOnly(), preserved.AsReadOnly());
        }

        /// <summary>Command-line entry point using -narrativeSnapshotOutput &lt;folder&gt;.</summary>
        public static void ExportProjectSnapshotFromCommandLine()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int outputIndex = Array.FindIndex(
                arguments,
                value => string.Equals(
                    value,
                    "-narrativeSnapshotOutput",
                    StringComparison.OrdinalIgnoreCase));
            if (outputIndex < 0 || outputIndex + 1 >= arguments.Length)
                throw new ArgumentException(
                    "Command line must include -narrativeSnapshotOutput <folder>.");
            bool overwriteExisting = arguments.Any(value => string.Equals(
                value,
                "-overwriteNarrativeExisting",
                StringComparison.OrdinalIgnoreCase));
            SnapshotResult result = ExportProjectSnapshot(
                arguments[outputIndex + 1],
                overwriteExisting: overwriteExisting);
            GameLogger.Log(
                nameof(NarrativeAuthoringJsonExporter),
                null,
                $"Wrote {result.WrittenPaths.Count} narrative authoring JSON file(s); " +
                $"preserved {result.PreservedPaths.Count} existing file(s).");
        }

        [MenuItem("Tools/Quiet Static/Exporters/Export Project Narrative Authoring Snapshot...")]
        private static void ExportProjectSnapshotFromMenu()
        {
            string folder = EditorUtility.OpenFolderPanel(
                "Export Project Narrative Authoring Snapshot",
                Directory.GetCurrentDirectory(),
                string.Empty);
            if (string.IsNullOrEmpty(folder))
                return;

            try
            {
                SnapshotResult result = ExportProjectSnapshot(folder);
                EditorUtility.DisplayDialog(
                    "Narrative Snapshot Exported",
                    $"Wrote {result.WrittenPaths.Count} JSON file(s). " +
                    $"Preserved {result.PreservedPaths.Count} existing file(s).",
                    "OK");
            }
            catch (Exception exception)
            {
                GameLogger.Error(
                    nameof(NarrativeAuthoringJsonExporter),
                    null,
                    $"Narrative snapshot export failed: {exception.Message}");
                EditorUtility.DisplayDialog(
                    "Narrative Snapshot Export Failed",
                    exception.Message,
                    "OK");
            }
        }

        private static T FindSingleOrNull<T>() where T : UnityEngine.Object
        {
            T[] assets = FindAssets<T>();
            if (assets.Length > 1)
                throw new InvalidOperationException(
                    $"Found multiple {typeof(T).Name} assets. Supply the intended asset explicitly.");
            return assets.FirstOrDefault();
        }

        private static T[] FindAssets<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToArray();

        private static string ResolveOutputFolder(string outputFolder)
        {
            if (string.IsNullOrWhiteSpace(outputFolder))
                throw new ArgumentException("Output folder must be non-empty.", nameof(outputFolder));
            try
            {
                return Path.GetFullPath(outputFolder)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                throw new ArgumentException("Output folder is invalid.", nameof(outputFolder), exception);
            }
        }

        private static string MakeSafeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value.Trim();
        }

        private static bool PathsEqual(string left, string right) =>
            string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                Path.DirectorySeparatorChar == '\\'
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
    }
}
