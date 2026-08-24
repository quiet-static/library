using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuietStatic.Toolkit.Editor.Dialogue;
using QuietStatic.Toolkit.Dialogue;
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

        /// <summary>The effect a reviewed batch import will have on a Unity asset.</summary>
        public enum AssetChangeKind
        {
            /// <summary>No asset currently exists at the target path.</summary>
            Create,

            /// <summary>The existing asset will be updated in place and retain its GUID.</summary>
            Update,

            /// <summary>The existing asset will be replaced with a newly generated asset.</summary>
            Regenerate,

            /// <summary>The existing asset will be removed by a replacement import.</summary>
            Delete,
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

        /// <summary>One concrete Unity asset change resolved during batch preflight.</summary>
        public sealed class AssetChange
        {
            internal AssetChange(
                Document sourceDocument,
                string contentId,
                string assetPath,
                Type assetType,
                AssetChangeKind kind,
                string existingAssetGuid,
                Hash128 existingAssetDependencyHash,
                int existingAssetDirtyCount)
            {
                SourceDocument = sourceDocument;
                ContentId = contentId;
                AssetPath = assetPath;
                AssetType = assetType;
                Kind = kind;
                ExistingAssetGuid = existingAssetGuid;
                ExistingAssetDependencyHash = existingAssetDependencyHash;
                ExistingAssetDirtyCount = existingAssetDirtyCount;
            }

            /// <summary>The source document responsible for this change.</summary>
            public Document SourceDocument { get; }

            /// <summary>The catalog, tree, or item ID represented by the target asset.</summary>
            public string ContentId { get; }

            /// <summary>Canonical project-relative path of the affected Unity asset.</summary>
            public string AssetPath { get; }

            /// <summary>Expected Unity object type at <see cref="AssetPath"/>.</summary>
            public Type AssetType { get; }

            /// <summary>The preflighted effect on the asset.</summary>
            public AssetChangeKind Kind { get; }

            internal string ExistingAssetGuid { get; }
            internal Hash128 ExistingAssetDependencyHash { get; }
            internal int ExistingAssetDirtyCount { get; }
        }

        /// <summary>An immutable, fully preflighted import plan.</summary>
        public sealed class Plan
        {
            internal Plan(
                string manifestPath,
                string narrativeOutputFolder,
                string dialogueOutputFolder,
                IReadOnlyList<Document> documents,
                IReadOnlyList<AssetChange> assetChanges)
            {
                ManifestPath = manifestPath;
                NarrativeOutputFolder = narrativeOutputFolder;
                DialogueOutputFolder = dialogueOutputFolder;
                Documents = documents;
                AssetChanges = assetChanges;
            }

            /// <summary>Absolute path to the validated manifest.</summary>
            public string ManifestPath { get; }

            /// <summary>Normalized output folder used for narrative content without explicit targets.</summary>
            public string NarrativeOutputFolder { get; }

            /// <summary>Normalized output folder used for dialogue without explicit targets.</summary>
            public string DialogueOutputFolder { get; }

            /// <summary>Documents in flags, objectives, readables, then dialogue order.</summary>
            public IReadOnlyList<Document> Documents { get; }

            /// <summary>Every created, updated, regenerated, or deleted Unity asset.</summary>
            public IReadOnlyList<AssetChange> AssetChanges { get; }
        }

        /// <summary>
        /// Thrown when a source file or affected Unity asset changed after a plan was reviewed.
        /// </summary>
        public sealed class PreviewOutOfDateException : InvalidOperationException
        {
            internal PreviewOutOfDateException()
                : base(
                    "The narrative sources or affected Unity assets changed after this " +
                    "preview was created. Refresh and review the import before confirming again.")
            {
            }
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
        private sealed class DialogueTargetProbe
        {
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
            return ImportPreflightedPlan(plan);
        }

        /// <summary>
        /// Imports a previously reviewed plan only when a fresh preflight produces the same
        /// sources, targets, and asset operations.
        /// </summary>
        /// <param name="reviewedPlan">The exact plan shown to the user for confirmation.</param>
        /// <returns>The freshly validated plan and its created or updated Unity assets.</returns>
        /// <exception cref="PreviewOutOfDateException">
        /// Thrown when files or affected Unity assets changed after the preview was built.
        /// </exception>
        public static Result ImportReviewedPlan(Plan reviewedPlan)
        {
            if (reviewedPlan == null)
                throw new ArgumentNullException(nameof(reviewedPlan));

            Plan currentPlan = Preflight(
                reviewedPlan.ManifestPath,
                reviewedPlan.NarrativeOutputFolder,
                reviewedPlan.DialogueOutputFolder);
            if (!PlansMatch(reviewedPlan, currentPlan))
                throw new PreviewOutOfDateException();
            return ImportPreflightedPlan(currentPlan);
        }

        private static Result ImportPreflightedPlan(Plan plan)
        {
            List<SourceAsset> sources = PrepareSources(plan.Documents);
            var importedAssets = new List<UnityEngine.Object>(sources.Count);

            try
            {
                for (int index = 0; index < plan.Documents.Count; index++)
                {
                    Document document = plan.Documents[index];
                    TextAsset source = sources[index].Asset;
                    UnityEngine.Object imported = document.Kind == DocumentKind.Dialogue
                        ? DialogueJsonImporter.Import(source, plan.DialogueOutputFolder)
                        : NarrativeContentJsonImporter.Import(source, plan.NarrativeOutputFolder);
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
            var assetChanges = new List<AssetChange>();

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
                IReadOnlyList<NarrativeContentJsonImporter.ImportTarget> contentTargets = null;
                if (kind == DocumentKind.Dialogue)
                    DialogueJsonImporter.ValidateJson(json, documentPath);
                else
                    contentTargets = NarrativeContentJsonImporter.GetImportTargets(
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
                var document = new Document(
                    entry.path,
                    documentPath,
                    kind,
                    identity,
                    json);
                RegisterEffectiveUnityTargets(
                    json,
                    document,
                    contentTargets,
                    dialogueOutputFolder,
                    unityTargets,
                    assetChanges,
                    nameof(manifestOrFolderPath));
                documents.Add(document);
            }

            var ordered = new List<Document>(documents.Count);
            foreach (DocumentKind kind in ImportOrder)
                ordered.AddRange(documents.Where(document => document.Kind == kind));
            var orderedChanges = new List<AssetChange>(assetChanges.Count);
            foreach (Document document in ordered)
            {
                orderedChanges.AddRange(assetChanges.Where(
                    change => ReferenceEquals(change.SourceDocument, document)));
            }
            return new Plan(
                manifestPath,
                narrativeOutputFolder,
                dialogueOutputFolder,
                ordered.AsReadOnly(),
                orderedChanges.AsReadOnly());
        }

        public static void OpenImportPreview()
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
                NarrativeBatchImportWindow.Open(sourcePath);
            }
            catch (Exception exception)
            {
                GameLogger.Error(
                    nameof(NarrativeBatchJsonImporter),
                    Selection.activeObject,
                    $"Narrative batch preview failed: {exception.Message}");
                EditorUtility.DisplayDialog(
                    "Narrative Batch Preview Failed",
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
            Document document,
            IReadOnlyList<NarrativeContentJsonImporter.ImportTarget> contentTargets,
            string dialogueOutputFolder,
            IDictionary<string, string> targets,
            ICollection<AssetChange> assetChanges,
            string parameterName)
        {
            if (document.Kind == DocumentKind.Dialogue)
            {
                DialogueTargetProbe probe = ParseJson<DialogueTargetProbe>(
                    json,
                    document.SourcePath,
                    "Unity target probe");
                RegisterUnityTarget(
                    probe.unityAssetPath ??
                    $"{dialogueOutputFolder}/{MakeSafeFileName(document.Identity)}.asset",
                    typeof(DialogueTree),
                    document,
                    document.Identity,
                    AssetChangeKind.Create,
                    document.SourcePath + ": dialogue target",
                    targets,
                    assetChanges,
                    parameterName);
                return;
            }

            foreach (NarrativeContentJsonImporter.ImportTarget target in contentTargets)
            {
                AssetChangeKind changeKind = target.Intent switch
                {
                    NarrativeContentJsonImporter.ImportTargetIntent.Regenerate =>
                        AssetChangeKind.Regenerate,
                    NarrativeContentJsonImporter.ImportTargetIntent.Delete =>
                        AssetChangeKind.Delete,
                    _ => AssetChangeKind.Create,
                };
                RegisterUnityTarget(
                    target.AssetPath,
                    target.AssetType,
                    document,
                    target.ContentId,
                    changeKind,
                    $"{document.SourcePath}: {target.ContentId} target",
                    targets,
                    assetChanges,
                    parameterName);
            }
        }

        private static void RegisterUnityTarget(
            string path,
            Type expectedType,
            Document document,
            string contentId,
            AssetChangeKind proposedKind,
            string location,
            IDictionary<string, string> targets,
            ICollection<AssetChange> assetChanges,
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
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            AssetChangeKind actualKind = proposedKind == AssetChangeKind.Create &&
                                         existing != null
                ? AssetChangeKind.Update
                : proposedKind;
            assetChanges.Add(new AssetChange(
                document,
                contentId,
                path,
                expectedType,
                actualKind,
                existing == null ? string.Empty : AssetDatabase.AssetPathToGUID(path),
                existing == null ? default : AssetDatabase.GetAssetDependencyHash(path),
                existing == null ? 0 : EditorUtility.GetDirtyCount(existing)));
        }

        private static bool PlansMatch(Plan reviewed, Plan current)
        {
            if (!string.Equals(reviewed.ManifestPath, current.ManifestPath, StringComparison.Ordinal) ||
                !string.Equals(
                    reviewed.NarrativeOutputFolder,
                    current.NarrativeOutputFolder,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    reviewed.DialogueOutputFolder,
                    current.DialogueOutputFolder,
                    StringComparison.Ordinal) ||
                reviewed.Documents.Count != current.Documents.Count ||
                reviewed.AssetChanges.Count != current.AssetChanges.Count)
            {
                return false;
            }

            for (int index = 0; index < reviewed.Documents.Count; index++)
            {
                Document left = reviewed.Documents[index];
                Document right = current.Documents[index];
                if (!string.Equals(left.RelativePath, right.RelativePath, StringComparison.Ordinal) ||
                    !string.Equals(left.SourcePath, right.SourcePath, StringComparison.Ordinal) ||
                    left.Kind != right.Kind ||
                    !string.Equals(left.Identity, right.Identity, StringComparison.Ordinal) ||
                    !string.Equals(left.Json, right.Json, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            for (int index = 0; index < reviewed.AssetChanges.Count; index++)
            {
                AssetChange left = reviewed.AssetChanges[index];
                AssetChange right = current.AssetChanges[index];
                if (!string.Equals(left.ContentId, right.ContentId, StringComparison.Ordinal) ||
                    !string.Equals(left.AssetPath, right.AssetPath, StringComparison.Ordinal) ||
                    left.AssetType != right.AssetType ||
                    left.Kind != right.Kind ||
                    !string.Equals(
                        left.ExistingAssetGuid,
                        right.ExistingAssetGuid,
                        StringComparison.OrdinalIgnoreCase) ||
                    left.ExistingAssetDependencyHash != right.ExistingAssetDependencyHash ||
                    left.ExistingAssetDirtyCount != right.ExistingAssetDirtyCount ||
                    !string.Equals(
                        left.SourceDocument.RelativePath,
                        right.SourceDocument.RelativePath,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
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
