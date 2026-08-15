using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuietStatic.Toolkit.Editor.Dialogue;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Interactions;
using QuietStatic.Toolkit.Objectives;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor
{
    /// <summary>
    /// Explicitly imports a complete dialogue-authoring export after validating its manifest.
    /// </summary>
    public static class NarrativeBatchJsonImporter
    {
        /// <summary>Required filename for a dialogue-authoring export manifest.</summary>
        public const string ManifestFileName = "quiet-static.narrative-manifest.json";

        /// <summary>Default output folder used by <see cref="NarrativeContentJsonImporter"/>.</summary>
        public const string DefaultNarrativeOutputFolder = "Assets/Generated/Narrative";

        /// <summary>Default output folder used by <see cref="DialogueJsonImporter"/>.</summary>
        public const string DefaultDialogueOutputFolder = "Assets/Generated/Dialogue";

        private const int SupportedSchemaVersion = 1;

        private static readonly DocumentKind[] ImportOrder =
        {
            DocumentKind.Flags,
            DocumentKind.Objectives,
            DocumentKind.Readables,
            DocumentKind.Dialogue,
        };

        private static readonly HashSet<string> WindowsReservedAssetNames = new(
            new[]
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
            },
            StringComparer.OrdinalIgnoreCase);

        /// <summary>Supported document kinds in a narrative batch.</summary>
        public enum DocumentKind
        {
            Flags,
            Objectives,
            Readables,
            Dialogue,
        }

        /// <summary>A preflighted source document, ordered for safe import.</summary>
        public sealed class Document
        {
            internal Document(
                string relativePath,
                string sourcePath,
                DocumentKind kind,
                string identity,
                string json)
            {
                RelativePath = relativePath;
                SourcePath = sourcePath;
                Kind = kind;
                Identity = identity;
                Json = json;
            }

            /// <summary>Forward-slash path relative to the manifest.</summary>
            public string RelativePath { get; }

            /// <summary>Absolute path to the source JSON file.</summary>
            public string SourcePath { get; }

            /// <summary>Declared and verified document kind.</summary>
            public DocumentKind Kind { get; }

            /// <summary>Stable catalog or tree identifier read from the document.</summary>
            public string Identity { get; }

            internal string Json { get; }
        }

        /// <summary>An immutable, fully preflighted import plan.</summary>
        public sealed class Plan
        {
            internal Plan(string manifestPath, IReadOnlyList<Document> documents)
            {
                ManifestPath = manifestPath;
                Documents = documents;
            }

            /// <summary>Absolute path to the validated manifest.</summary>
            public string ManifestPath { get; }

            /// <summary>Documents in flags, objectives, readables, then dialogue order.</summary>
            public IReadOnlyList<Document> Documents { get; }
        }

        /// <summary>Imported assets paired with the plan that produced them.</summary>
        public sealed class Result
        {
            internal Result(Plan plan, IReadOnlyList<UnityEngine.Object> importedAssets)
            {
                Plan = plan;
                ImportedAssets = importedAssets;
            }

            /// <summary>The plan validated before any generated asset was changed.</summary>
            public Plan Plan { get; }

            /// <summary>Created or updated assets in the same order as <see cref="Plan.Documents"/>.</summary>
            public IReadOnlyList<UnityEngine.Object> ImportedAssets { get; }
        }

        [Serializable]
        private sealed class Manifest
        {
            public int schemaVersion;
            public ManifestEntry[] documents;
        }

        [Serializable]
        private sealed class ManifestEntry
        {
            public string path;
            public string kind;
        }

        [Serializable]
        private sealed class DocumentProbe
        {
            public int schemaVersion;
            public string contentType;
            public string catalogId;
            public string treeId;
        }

        [Serializable]
        private sealed class UnityTargetProbe
        {
            public string contentType;
            public string unityAssetPath;
            public string unityDatabasePath;
            public string unityObjectiveImportMode;
            public UnityTargetItemProbe[] items;
        }

        [Serializable]
        private sealed class UnityTargetItemProbe
        {
            public string id;
            public string unityAssetPath;
        }

        private sealed class SourceAsset
        {
            public SourceAsset(TextAsset asset, bool temporary)
            {
                Asset = asset;
                Temporary = temporary;
            }

            public TextAsset Asset { get; }
            public bool Temporary { get; }
        }

        /// <summary>
        /// Imports every document in a manifest, or in the supplied manifest-containing folder.
        /// </summary>
        /// <remarks>
        /// All files are read and their manifest kind, JSON, schema, and top-level identity are
        /// checked before generated assets are changed. Flags are always imported before content
        /// that can refer to them, and dialogue is imported last.
        /// </remarks>
        /// <param name="manifestOrFolderPath">
        /// Absolute or project-relative path to the manifest or its containing folder.
        /// </param>
        /// <param name="narrativeOutputFolder">Assets folder for flags, objectives, and readables.</param>
        /// <param name="dialogueOutputFolder">Assets folder for dialogue trees.</param>
        /// <returns>The validated plan and its created or updated Unity assets.</returns>
        public static Result Import(
            string manifestOrFolderPath,
            string narrativeOutputFolder = DefaultNarrativeOutputFolder,
            string dialogueOutputFolder = DefaultDialogueOutputFolder)
        {
            narrativeOutputFolder = NormalizeOutputFolder(
                narrativeOutputFolder,
                nameof(narrativeOutputFolder));
            dialogueOutputFolder = NormalizeOutputFolder(
                dialogueOutputFolder,
                nameof(dialogueOutputFolder));

            Plan plan = Preflight(
                manifestOrFolderPath,
                narrativeOutputFolder,
                dialogueOutputFolder);
            List<SourceAsset> sources = PrepareSources(plan.Documents);
            var importedAssets = new List<UnityEngine.Object>(sources.Count);

            try
            {
                for (int index = 0; index < plan.Documents.Count; index++)
                {
                    Document document = plan.Documents[index];
                    TextAsset source = sources[index].Asset;
                    UnityEngine.Object imported = document.Kind == DocumentKind.Dialogue
                        ? DialogueJsonImporter.Import(source, dialogueOutputFolder)
                        : NarrativeContentJsonImporter.Import(source, narrativeOutputFolder);
                    importedAssets.Add(imported);
                }

                return new Result(plan, importedAssets.AsReadOnly());
            }
            finally
            {
                foreach (SourceAsset source in sources)
                {
                    if (source.Temporary && source.Asset != null)
                        UnityEngine.Object.DestroyImmediate(source.Asset);
                }
            }
        }

        /// <summary>
        /// Reads and validates a narrative batch without creating or changing generated assets.
        /// </summary>
        /// <param name="manifestOrFolderPath">
        /// Absolute or project-relative path to the manifest or its containing folder.
        /// </param>
        /// <returns>An immutable plan in deterministic import order.</returns>
        public static Plan Preflight(
            string manifestOrFolderPath,
            string narrativeOutputFolder = DefaultNarrativeOutputFolder,
            string dialogueOutputFolder = DefaultDialogueOutputFolder)
        {
            narrativeOutputFolder = NormalizeOutputFolder(
                narrativeOutputFolder,
                nameof(narrativeOutputFolder));
            dialogueOutputFolder = NormalizeOutputFolder(
                dialogueOutputFolder,
                nameof(dialogueOutputFolder));
            string manifestPath = ResolveManifestPath(manifestOrFolderPath);
            Manifest manifest = ParseJson<Manifest>(
                File.ReadAllText(manifestPath),
                manifestPath,
                "manifest");

            if (manifest.schemaVersion != SupportedSchemaVersion)
            {
                throw new ArgumentException(
                    $"{manifestPath}: schemaVersion must be {SupportedSchemaVersion}.",
                    nameof(manifestOrFolderPath));
            }
            if (manifest.documents == null || manifest.documents.Length == 0)
            {
                throw new ArgumentException(
                    $"{manifestPath}: documents must contain at least one entry.",
                    nameof(manifestOrFolderPath));
            }

            string manifestFolder = Path.GetDirectoryName(manifestPath);
            StringComparer pathComparer = Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var seenPaths = new HashSet<string>(pathComparer);
            var dialogueIdentities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var catalogIdentities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var unityTargets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var documents = new List<Document>(manifest.documents.Length);

            for (int index = 0; index < manifest.documents.Length; index++)
            {
                ManifestEntry entry = manifest.documents[index];
                string at = $"{manifestPath}: documents[{index}]";
                if (entry == null)
                    throw new ArgumentException($"{at} must be an object.", nameof(manifestOrFolderPath));

                DocumentKind kind = ParseKind(
                    entry.kind,
                    at,
                    nameof(manifestOrFolderPath));
                string documentPath = ResolveDocumentPath(
                    manifestFolder,
                    entry.path,
                    at,
                    nameof(manifestOrFolderPath));
                if (!seenPaths.Add(documentPath))
                {
                    throw new ArgumentException(
                        $"{at}.path duplicates '{entry.path}'.",
                        nameof(manifestOrFolderPath));
                }
                if (!File.Exists(documentPath))
                {
                    throw new FileNotFoundException(
                        $"{at}.path does not exist: {entry.path}",
                        documentPath);
                }

                string json = File.ReadAllText(documentPath);
                DocumentProbe probe = ParseJson<DocumentProbe>(json, documentPath, "document");
                string identity = ValidateDocumentIdentity(
                    probe,
                    kind,
                    documentPath,
                    nameof(manifestOrFolderPath));
                if (kind == DocumentKind.Dialogue)
                    DialogueJsonImporter.ValidateJson(json, documentPath);
                else
                    NarrativeContentJsonImporter.PreflightImport(
                        json,
                        narrativeOutputFolder,
                        documentPath);
                Dictionary<string, string> identities = kind == DocumentKind.Dialogue
                    ? dialogueIdentities
                    : catalogIdentities;
                string assetName = MakeSafeFileName(identity);
                if (IsWindowsReservedAssetName(assetName))
                {
                    throw new ArgumentException(
                        $"{documentPath}: identity '{identity}' maps to a Windows-reserved " +
                        "generated asset name.",
                        nameof(manifestOrFolderPath));
                }
                if (identities.TryGetValue(assetName, out string existingIdentityPath))
                {
                    string label = kind == DocumentKind.Dialogue ? "treeId" : "catalogId";
                    throw new ArgumentException(
                        $"{documentPath}: {label} '{identity}' maps to the same generated " +
                        $"asset name as {existingIdentityPath}.",
                        nameof(manifestOrFolderPath));
                }
                identities.Add(assetName, documentPath);
                RegisterEffectiveUnityTargets(
                    json,
                    kind,
                    identity,
                    documentPath,
                    narrativeOutputFolder,
                    dialogueOutputFolder,
                    unityTargets,
                    nameof(manifestOrFolderPath));
                documents.Add(new Document(entry.path, documentPath, kind, identity, json));
            }

            var ordered = new List<Document>(documents.Count);
            foreach (DocumentKind kind in ImportOrder)
                ordered.AddRange(documents.Where(document => document.Kind == kind));
            return new Plan(manifestPath, ordered.AsReadOnly());
        }

        [MenuItem("Tools/Quiet Static/Importers/Import Narrative Authorer Batch...")]
        private static void ImportFromMenu()
        {
            string sourcePath = GetSelectedSourcePath();
            if (string.IsNullOrEmpty(sourcePath))
            {
                sourcePath = EditorUtility.OpenFilePanel(
                    "Import Narrative Authorer Batch",
                    Directory.GetCurrentDirectory(),
                    "json");
            }
            if (string.IsNullOrEmpty(sourcePath))
                return;

            try
            {
                Result result = Import(sourcePath);
                UnityEngine.Object selected = result.ImportedAssets.LastOrDefault();
                if (selected != null)
                {
                    Selection.activeObject = selected;
                    EditorGUIUtility.PingObject(selected);
                }
                GameLogger.Log(
                    nameof(NarrativeBatchJsonImporter),
                    selected,
                    $"Imported {result.ImportedAssets.Count} narrative assets from {result.Plan.ManifestPath}.");
            }
            catch (Exception exception)
            {
                GameLogger.Error(
                    nameof(NarrativeBatchJsonImporter),
                    Selection.activeObject,
                    $"Narrative batch import failed: {exception.Message}");
                EditorUtility.DisplayDialog(
                    "Narrative Batch Import Failed",
                    exception.Message,
                    "OK");
            }
        }

        private static string GetSelectedSourcePath()
        {
            string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrWhiteSpace(selectedPath))
                return null;

            string fullPath = Path.GetFullPath(selectedPath);
            if (Directory.Exists(fullPath) &&
                File.Exists(Path.Combine(fullPath, ManifestFileName)))
                return fullPath;
            if (File.Exists(fullPath) &&
                string.Equals(Path.GetFileName(fullPath), ManifestFileName, StringComparison.Ordinal))
                return fullPath;
            return null;
        }

        private static List<SourceAsset> PrepareSources(IReadOnlyList<Document> documents)
        {
            var sources = new List<SourceAsset>(documents.Count);
            try
            {
                foreach (Document document in documents)
                {
                    string assetPath = TryGetAssetPath(document.SourcePath);
                    if (assetPath != null)
                    {
                        AssetDatabase.ImportAsset(
                            assetPath,
                            ImportAssetOptions.ForceSynchronousImport);
                        TextAsset sourceAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                        if (sourceAsset == null)
                            throw new IOException($"Unity could not import JSON source '{assetPath}'.");
                        if (!string.Equals(sourceAsset.text, document.Json, StringComparison.Ordinal))
                        {
                            throw new IOException(
                                $"JSON source changed after preflight: {document.SourcePath}");
                        }
                        sources.Add(new SourceAsset(sourceAsset, false));
                        continue;
                    }

                    var temporary = new TextAsset(document.Json)
                    {
                        name = Path.GetFileNameWithoutExtension(document.SourcePath),
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    sources.Add(new SourceAsset(temporary, true));
                }
                return sources;
            }
            catch
            {
                foreach (SourceAsset source in sources)
                {
                    if (source.Temporary && source.Asset != null)
                        UnityEngine.Object.DestroyImmediate(source.Asset);
                }
                throw;
            }
        }

        private static string TryGetAssetPath(string sourcePath)
        {
            string assetsFolder = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string prefix = assetsFolder + Path.DirectorySeparatorChar;
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!sourcePath.StartsWith(prefix, comparison))
                return null;
            return "Assets/" + sourcePath.Substring(prefix.Length).Replace('\\', '/');
        }

        private static string ResolveManifestPath(string manifestOrFolderPath)
        {
            if (string.IsNullOrWhiteSpace(manifestOrFolderPath))
                throw new ArgumentException("A manifest or folder path is required.", nameof(manifestOrFolderPath));

            string fullPath = Path.GetFullPath(manifestOrFolderPath);
            if (Directory.Exists(fullPath))
                fullPath = Path.Combine(fullPath, ManifestFileName);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Narrative batch manifest was not found.", fullPath);
            if (!string.Equals(Path.GetFileName(fullPath), ManifestFileName, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Manifest filename must be '{ManifestFileName}'.",
                    nameof(manifestOrFolderPath));
            }
            return Path.GetFullPath(fullPath);
        }

        private static string ResolveDocumentPath(
            string manifestFolder,
            string relativePath,
            string at,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || relativePath != relativePath.Trim())
                throw new ArgumentException($"{at}.path must be non-empty and normalized.", parameterName);
            if (relativePath.IndexOf('\\') >= 0 || Path.IsPathRooted(relativePath))
                throw new ArgumentException($"{at}.path must be a forward-slash relative path.", parameterName);
            if (!relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"{at}.path must reference a JSON file.", parameterName);

            string[] segments = relativePath.Split('/');
            if (segments.Any(segment =>
                    string.IsNullOrWhiteSpace(segment) ||
                    segment == "." ||
                    segment == ".."))
            {
                throw new ArgumentException(
                    $"{at}.path must not contain empty, '.', or '..' segments.",
                    parameterName);
            }

            string root = Path.GetFullPath(manifestFolder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string combined = Path.GetFullPath(Path.Combine(manifestFolder, Path.Combine(segments)));
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!combined.StartsWith(root, comparison))
                throw new ArgumentException($"{at}.path must remain inside the export folder.", parameterName);
            return combined;
        }

        private static DocumentKind ParseKind(string value, string at, string parameterName)
        {
            return value switch
            {
                "flags" => DocumentKind.Flags,
                "objectives" => DocumentKind.Objectives,
                "readables" => DocumentKind.Readables,
                "dialogue" => DocumentKind.Dialogue,
                _ => throw new ArgumentException(
                    $"{at}.kind must be flags, objectives, readables, or dialogue.",
                    parameterName),
            };
        }

        private static string ValidateDocumentIdentity(
            DocumentProbe probe,
            DocumentKind kind,
            string sourcePath,
            string parameterName)
        {
            if (probe.schemaVersion != SupportedSchemaVersion)
            {
                throw new ArgumentException(
                    $"{sourcePath}: schemaVersion must be {SupportedSchemaVersion}.",
                    parameterName);
            }

            if (kind == DocumentKind.Dialogue)
            {
                if (!string.IsNullOrEmpty(probe.contentType) || string.IsNullOrWhiteSpace(probe.treeId))
                {
                    throw new ArgumentException(
                        $"{sourcePath}: kind dialogue requires a non-empty treeId and no contentType.",
                        parameterName);
                }
                return probe.treeId;
            }

            string expectedContentType = KindToManifestValue(kind);
            if (!string.Equals(probe.contentType, expectedContentType, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(probe.catalogId) ||
                !string.IsNullOrEmpty(probe.treeId))
            {
                throw new ArgumentException(
                    $"{sourcePath}: kind {expectedContentType} requires contentType " +
                    $"'{expectedContentType}', a non-empty catalogId, and no treeId.",
                    parameterName);
            }
            return probe.catalogId;
        }

        private static string KindToManifestValue(DocumentKind kind)
        {
            return kind switch
            {
                DocumentKind.Flags => "flags",
                DocumentKind.Objectives => "objectives",
                DocumentKind.Readables => "readables",
                DocumentKind.Dialogue => "dialogue",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };
        }

        private static void RegisterEffectiveUnityTargets(
            string json,
            DocumentKind kind,
            string identity,
            string sourcePath,
            string narrativeOutputFolder,
            string dialogueOutputFolder,
            IDictionary<string, string> targets,
            string parameterName)
        {
            UnityTargetProbe probe = ParseJson<UnityTargetProbe>(
                json,
                sourcePath,
                "Unity target probe");
            if (kind == DocumentKind.Dialogue)
            {
                RegisterUnityTarget(
                    probe.unityAssetPath ??
                    $"{dialogueOutputFolder}/{MakeSafeFileName(identity)}.asset",
                    typeof(DialogueTree),
                    sourcePath + ": dialogue target",
                    targets,
                    parameterName);
                return;
            }

            string catalogFolder =
                $"{narrativeOutputFolder}/{MakeSafeFileName(identity)}";
            if (kind == DocumentKind.Flags || kind == DocumentKind.Objectives)
            {
                RegisterUnityTarget(
                    probe.unityDatabasePath ??
                    $"{catalogFolder}/{MakeSafeFileName(identity)}.asset",
                    kind == DocumentKind.Flags
                        ? typeof(FlagDatabase)
                        : typeof(ObjectiveDatabase),
                    sourcePath + ": catalog database target",
                    targets,
                    parameterName);
            }
            if (kind == DocumentKind.Flags)
                return;

            Type itemType = kind == DocumentKind.Objectives
                ? typeof(ObjectiveDefinition)
                : typeof(ReadableContentDefinition);
            bool replaceObjectives =
                kind == DocumentKind.Objectives &&
                probe.unityObjectiveImportMode == "Replace";
            for (int index = 0; index < (probe.items?.Length ?? 0); index++)
            {
                UnityTargetItemProbe item = probe.items[index];
                RegisterUnityTarget(
                    replaceObjectives
                        ? $"{catalogFolder}/Definitions/{MakeSafeFileName(item?.id)}.asset"
                        : item?.unityAssetPath ??
                          $"{catalogFolder}/{MakeSafeFileName(item?.id)}.asset",
                    itemType,
                    $"{sourcePath}: items[{index}] target",
                    targets,
                    parameterName);
            }
        }

        private static void RegisterUnityTarget(
            string path,
            Type expectedType,
            string location,
            IDictionary<string, string> targets,
            string parameterName)
        {
            if (string.IsNullOrEmpty(path))
                return;
            var pathErrors = new List<string>();
            NarrativeJsonPathUtility.ValidateUnityAssetPath(
                path,
                expectedType,
                location,
                pathErrors);
            if (pathErrors.Count > 0)
                throw new ArgumentException(
                    string.Join(Environment.NewLine, pathErrors),
                    parameterName);
            if (targets.TryGetValue(path, out string existingLocation))
            {
                throw new ArgumentException(
                    $"{location} targets the same Unity asset as {existingLocation}: {path}",
                    parameterName);
            }
            targets.Add(path, location);
        }

        private static string MakeSafeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value.Trim();
        }

        private static bool IsWindowsReservedAssetName(string value)
        {
            string basename = value.Split('.')[0];
            return WindowsReservedAssetNames.Contains(basename);
        }

        private static T ParseJson<T>(string json, string path, string label)
        {
            try
            {
                T value = JsonUtility.FromJson<T>(json);
                if (value == null)
                    throw new ArgumentException("top-level value must be an object.");
                return value;
            }
            catch (Exception exception)
            {
                throw new ArgumentException(
                    $"{path}: invalid {label} JSON: {exception.Message}",
                    exception);
            }
        }

        private static string NormalizeOutputFolder(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Output folder must be under Assets.", parameterName);
            string normalized = value.Replace('\\', '/').TrimEnd('/');
            if (normalized != "Assets" &&
                !normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException("Output folder must be under Assets.", parameterName);
            }
            string[] segments = normalized.Split('/');
            if (segments.Any(segment =>
                    string.IsNullOrWhiteSpace(segment) ||
                    segment == "." ||
                    segment == ".."))
            {
                throw new ArgumentException("Output folder must be a normalized Assets path.", parameterName);
            }
            return normalized;
        }
    }
}
