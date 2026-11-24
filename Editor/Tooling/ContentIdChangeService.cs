using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Objectives;
using QuietStatic.Toolkit.State;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Editor.Tooling
{
    public enum ContentIdKind { Flag, Objective, GameState, Cinematic }

    public sealed class ContentIdReference
    {
        internal ContentIdReference(string assetPath, string objectId, string objectName, string propertyPath, string originalValue, bool canRename)
        {
            AssetPath = assetPath;
            ObjectId = objectId;
            ObjectName = objectName;
            PropertyPath = propertyPath;
            OriginalValue = originalValue;
            CanRename = canRename;
        }

        public string AssetPath { get; }
        public string ObjectName { get; }
        public string PropertyPath { get; }
        public string Label => $"{AssetPath} • {ObjectName} • {PropertyPath}";
        internal string ObjectId { get; }
        internal string OriginalValue { get; }
        internal bool CanRename { get; }

        internal bool Matches(ContentIdReference other) =>
            AssetPath == other.AssetPath && ObjectId == other.ObjectId &&
            ObjectName == other.ObjectName && PropertyPath == other.PropertyPath &&
            OriginalValue == other.OriginalValue && CanRename == other.CanRename;
    }

    public sealed class ContentIdChangePreview
    {
        internal ContentIdChangePreview(ContentIdKind kind, string sourcePath, string sourceProperty, GlobalObjectId sourceId,
            string oldId, string newId, string version, IReadOnlyList<ContentIdReference> matches)
        {
            Kind = kind;
            SourcePath = sourcePath;
            SourceProperty = sourceProperty;
            SourceId = sourceId;
            OldId = oldId;
            NewId = newId;
            Version = version;
            Matches = matches;
            References = Array.AsReadOnly(matches.Where(reference => reference.CanRename).ToArray());
            ManualReview = Array.AsReadOnly(matches.Where(reference => !reference.CanRename).ToArray());
        }

        public ContentIdKind Kind { get; }
        internal string SourcePath { get; }
        internal string SourceProperty { get; }
        internal GlobalObjectId SourceId { get; }
        internal string Version { get; }
        public string OldId { get; }
        public string NewId { get; }
        public IReadOnlyList<ContentIdReference> References { get; }
        /// <summary>Possible references that must be reviewed and updated by hand.</summary>
        public IReadOnlyList<ContentIdReference> ManualReview { get; }
        internal IReadOnlyList<ContentIdReference> Matches { get; }
    }

    /// <summary>Previews and applies serialized ID renames in project assets.</summary>
    public static class ContentIdChangeService
    {
        public static ContentIdChangePreview PreviewRename(UnityEngine.Object source, ContentIdKind kind, string oldId, string newId)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            string oldValue = Normalize(oldId);
            string newValue = Normalize(newId);
            if (oldValue.Length == 0 || newValue.Length == 0) throw new ArgumentException("Both old and new IDs are required.");
            string sourceProperty = FindSourceProperty(source, kind, oldValue);
            if (sourceProperty == null) throw new InvalidOperationException($"ID '{oldValue}' was not found in '{source.name}'.");
            if (FindSourceProperty(source, kind, newValue) != null) throw new InvalidOperationException($"ID '{newValue}' already exists in '{source.name}'.");
            string path = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsMainAsset(source))
                throw new InvalidOperationException("Save the source as a standalone asset before renaming its ID.");
            EnsureFlagIdAvailable(kind, newValue, source);
            return new ContentIdChangePreview(kind, path, sourceProperty, GlobalObjectId.GetGlobalObjectIdSlow(source),
                oldValue, newValue, EditorJsonUtility.ToJson(source), FindReferenceMatches(kind, new[] { oldValue }, path));
        }

        public static void Apply(ContentIdChangePreview preview) => Apply(preview, AssetDatabase.SaveAssetIfDirty);

        internal static void Apply(ContentIdChangePreview preview, Action<UnityEngine.Object> saveSource)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            UnityEngine.Object source = AssetDatabase.LoadMainAssetAtPath(preview.SourcePath);
            if (source == null || !GlobalObjectId.GetGlobalObjectIdSlow(source).Equals(preview.SourceId) ||
                EditorJsonUtility.ToJson(source) != preview.Version)
                throw new InvalidOperationException("The source asset changed after preview. Refresh the preview before applying.");
            EnsureFlagIdAvailable(preview.Kind, preview.NewId, source);

            var current = FindReferenceMatches(preview.Kind, new[] { preview.OldId }, preview.SourcePath);
            if (current.Count != preview.Matches.Count ||
                !current.Zip(preview.Matches, (left, right) => left.Matches(right)).All(matches => matches))
                throw new InvalidOperationException("Consumer references changed after preview. Run Preview and Rename again to review the current edits.");

            var files = preview.References.GroupBy(reference => reference.AssetPath).ToArray();
            string[] affectedPaths = files.Select(file => file.Key).Append(preview.SourcePath).ToArray();
            foreach (string path in affectedPaths) VerifySaved(path);

            using var serialized = new SerializedObject(source);
            SerializedProperty property = serialized.FindProperty(preview.SourceProperty);
            if (property == null || Normalize(property.stringValue) != preview.OldId)
                throw new InvalidOperationException("The source ID moved after preview.");

            using var backups = new RenameBackups();
            foreach (string path in affectedPaths) backups.Capture(path);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            string undoName = $"Rename {preview.Kind} ID";
            Undo.SetCurrentGroupName(undoName);
            try
            {
                foreach (var file in files)
                {
                    backups.BeginWrite(file.Key);
                    RewriteFile(file.Key, file, preview.NewId, undoName);
                }

                backups.BeginWrite(preview.SourcePath);
                Undo.RegisterCompleteObjectUndo(source, undoName);
                property.stringValue = preview.NewId;
                serialized.ApplyModifiedProperties();
                saveSource(source);
                if (EditorUtility.IsDirty(source))
                    throw new IOException($"Could not save the source asset '{preview.SourcePath}'.");

                Undo.CollapseUndoOperations(undoGroup);
            }
            catch (Exception failure)
            {
                IReadOnlyList<string> rollbackErrors = backups.RollBack(undoGroup);
                if (rollbackErrors.Count > 0)
                    throw new InvalidOperationException(
                        $"Rename failed and could not be fully restored. Backups are in '{backups.DirectoryPath}'.\n" +
                        $"{failure.Message}\n{string.Join("\n", rollbackErrors)}", failure);

                throw new InvalidOperationException(
                    $"Rename failed. The original IDs and saved files were restored.\n{failure.Message}", failure);
            }
        }

        private static void VerifySaved(string path)
        {
            if (IsScenePath(path))
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                    throw new InvalidOperationException($"Save the affected scene '{path}', then run Preview and Rename again.");
                return;
            }

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            UnityEngine.Object[] roots = LoadAssetObjects(path);
            if ((stage != null && stage.assetPath == path && stage.scene.isDirty) ||
                roots.Any(EditorUtility.IsDirty) || EnumerateObjects(roots).Any(EditorUtility.IsDirty))
                throw new InvalidOperationException($"Save the affected asset '{path}', then run Preview and Rename again.");
        }

        /// <summary>Finds confirmed serialized references that can be updated automatically.</summary>
        public static IReadOnlyList<ContentIdReference> FindReferences(ContentIdKind kind, string id, string excludedPath = null) =>
            Array.AsReadOnly(FindReferenceMatches(kind, new[] { id }, excludedPath)
                .Where(reference => reference.CanRename).ToArray());

        internal static IReadOnlyList<ContentIdReference> FindReferenceMatches(ContentIdKind kind, IEnumerable<string> ids,
            string excludedPath = null, bool includeClosedScenes = true)
        {
            var values = new HashSet<string>(ids.Select(Normalize).Where(id => id.Length > 0), StringComparer.Ordinal);
            if (values.Count == 0) return Array.Empty<ContentIdReference>();

            var results = new List<ContentIdReference>();
            foreach (string path in CandidatePaths().Where(path => path != excludedPath))
                ScanPath(path, kind, values, includeClosedScenes, results);

            return Array.AsReadOnly(results.OrderBy(item => item.AssetPath, StringComparer.Ordinal)
                .ThenBy(item => item.ObjectName, StringComparer.Ordinal)
                .ThenBy(item => item.ObjectId, StringComparer.Ordinal)
                .ThenBy(item => item.PropertyPath, StringComparer.Ordinal).ToArray());
        }

        private static void ScanPath(string path, ContentIdKind kind, HashSet<string> ids,
            bool includeClosedScenes, ICollection<ContentIdReference> results)
        {
            if (!IsScenePath(path))
            {
                ScanObjects(EnumerateObjects(LoadAssetObjects(path)), path, kind, ids, results);
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isLoaded)
            {
                ScanObjects(EnumerateObjects(scene.GetRootGameObjects()), path, kind, ids, results);
                return;
            }

            if (!includeClosedScenes) return;
            Scene preview = EditorSceneManager.OpenPreviewScene(path);
            try { ScanObjects(EnumerateObjects(preview.GetRootGameObjects()), path, kind, ids, results); }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static void ScanObjects(IEnumerable<UnityEngine.Object> objects, string path, ContentIdKind kind, HashSet<string> ids,
            ICollection<ContentIdReference> results)
        {
            Type marker = kind switch
            {
                ContentIdKind.Flag => typeof(FlagIdAttribute),
                ContentIdKind.Objective => typeof(ObjectiveIdAttribute),
                ContentIdKind.GameState => typeof(GameStateIdAttribute),
                ContentIdKind.Cinematic => typeof(CinematicIdAttribute),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            foreach (UnityEngine.Object value in objects)
            {
                SerializedObject serialized;
                try { serialized = new SerializedObject(value); } catch (ArgumentException) { continue; }
                using (serialized)
                {
                    SerializedProperty property = serialized.GetIterator();
                    string objectId = null;
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.String || !ids.Contains(Normalize(property.stringValue)))
                            continue;

                        bool canRename = ResolveField(value.GetType(), property.propertyPath)?.IsDefined(marker, inherit: true) == true;
                        if (!canRename && !IsPossibleReference(kind, property.name, property.propertyPath)) continue;

                        objectId ??= GetObjectId(value);
                        results.Add(new ContentIdReference(path, objectId, value.name, property.propertyPath, property.stringValue, canRename));
                    }
                }
            }
        }

        private static void RewriteFile(string path, IEnumerable<ContentIdReference> edits, string replacement, string undoName)
        {
            if (!IsScenePath(path))
            {
                UnityEngine.Object[] assets = LoadAssetObjects(path);
                if (!RewriteObjects(EnumerateObjects(assets), edits, replacement, undoName)) return;
                if (assets.FirstOrDefault() is GameObject prefab)
                {
                    PrefabUtility.SavePrefabAsset(prefab, out bool saved);
                    if (!saved) throw new InvalidOperationException($"Could not save the prefab '{path}'.");
                }
                else
                {
                    AssetDatabase.SaveAssetIfDirty(AssetDatabase.GUIDFromAssetPath(path));
                    if (assets.Any(EditorUtility.IsDirty))
                        throw new IOException($"Could not save the asset '{path}'.");
                }
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            bool wasOpen = wasLoaded || EditorSceneManager.GetSceneManagerSetup().Any(setup => setup.path == path);
            Scene previousActive = SceneManager.GetActiveScene();
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                if (RewriteObjects(EnumerateObjects(scene.GetRootGameObjects()), edits, replacement, undoName) &&
                    (!EditorSceneManager.SaveScene(scene) || scene.isDirty))
                    throw new InvalidOperationException($"Could not save the scene '{path}'.");
            }
            finally
            {
                if (!wasLoaded)
                {
                    if (previousActive.IsValid() && previousActive.isLoaded && SceneManager.GetActiveScene() != previousActive)
                        SceneManager.SetActiveScene(previousActive);
                    EditorSceneManager.CloseScene(scene, removeScene: !wasOpen);
                }
            }
        }

        private static bool RewriteObjects(IEnumerable<UnityEngine.Object> objects, IEnumerable<ContentIdReference> edits,
            string replacement, string undoName)
        {
            var editsByObject = edits.ToLookup(edit => edit.ObjectId);
            int appliedObjects = 0;
            bool changed = false;
            foreach (UnityEngine.Object value in objects)
            {
                string objectId = GetObjectId(value);
                if (!editsByObject.Contains(objectId)) continue;

                using var serialized = new SerializedObject(value);
                foreach (ContentIdReference edit in editsByObject[objectId])
                {
                    SerializedProperty property = serialized.FindProperty(edit.PropertyPath);
                    if (property == null || property.propertyType != SerializedPropertyType.String)
                        throw new InvalidOperationException($"The reviewed field '{edit.Label}' could not be located.");
                    // A saved prefab edit can already have supplied this value to an instance.
                    if (property.stringValue == replacement) continue;
                    if (property.stringValue != edit.OriginalValue)
                        throw new InvalidOperationException($"The reviewed field '{edit.Label}' changed while applying the rename.");
                    property.stringValue = replacement;
                }

                if (serialized.hasModifiedProperties)
                {
                    Undo.RegisterCompleteObjectUndo(value, undoName);
                    changed |= serialized.ApplyModifiedProperties();
                }
                appliedObjects++;
            }

            if (appliedObjects != editsByObject.Count)
                throw new InvalidOperationException("A reviewed consumer could not be located while applying the rename.");
            return changed;
        }

        private static UnityEngine.Object[] LoadAssetObjects(string path) =>
            path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                ? new[] { AssetDatabase.LoadMainAssetAtPath(path) }
                : AssetDatabase.LoadAllAssetsAtPath(path);

        private static IEnumerable<UnityEngine.Object> EnumerateObjects(IEnumerable<UnityEngine.Object> roots)
        {
            foreach (UnityEngine.Object root in roots)
            {
                if (root is GameObject gameObject)
                {
                    foreach (Component component in gameObject.GetComponentsInChildren<Component>(true))
                        if (component != null) yield return component;
                }
                else if (root != null) yield return root;
            }
        }

        private static string GetObjectId(UnityEngine.Object value)
        {
            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(value);
            // Unsaved objects are visible in a read-only preview. Their scene must be saved before apply.
            return id.targetObjectId != 0 ? id.ToString() : $"unsaved:{value.GetInstanceID()}";
        }

        private sealed class RenameBackups : IDisposable
        {
            private readonly Dictionary<string, string> originals = new(StringComparer.Ordinal);
            private readonly List<string> attempted = new();
            private bool retainBackups;

            internal string DirectoryPath { get; } = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "Library", "QuietStaticRenameBackups", Guid.NewGuid().ToString("N")));

            internal void Capture(string assetPath)
            {
                string filePath = Path.GetFullPath(FileUtil.GetPhysicalPath(assetPath));
                string backupPath = Path.Combine(DirectoryPath, assetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                File.Copy(filePath, backupPath);
                originals.Add(assetPath, filePath);
            }

            internal void BeginWrite(string assetPath) => attempted.Add(assetPath);

            internal IReadOnlyList<string> RollBack(int undoGroup)
            {
                var errors = new List<string>();
                try
                {
                    Undo.FlushUndoRecordObjects();
                    Undo.RevertAllDownToGroup(undoGroup);
                }
                catch (Exception exception) { errors.Add($"Unity Undo failed: {exception.Message}"); }

                var restoredPaths = new List<string>();
                foreach (string assetPath in attempted)
                {
                    try
                    {
                        string backupPath = Path.Combine(DirectoryPath, assetPath);
                        string filePath = originals[assetPath];
                        if (FilesMatch(backupPath, filePath)) continue;
                        RestoreFile(backupPath, filePath);
                        restoredPaths.Add(assetPath);
                    }
                    catch (Exception exception) { errors.Add($"Could not restore '{assetPath}': {exception.Message}"); }
                }

                foreach (string assetPath in restoredPaths)
                {
                    try { AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport); }
                    catch (Exception exception) { errors.Add($"Could not reload '{assetPath}': {exception.Message}"); }
                }

                retainBackups = errors.Count > 0;
                return errors;
            }

            private static bool FilesMatch(string backup, string current)
            {
                if (!File.Exists(current) || new FileInfo(backup).Length != new FileInfo(current).Length) return false;
                using var hash = SHA256.Create();
                using FileStream original = File.OpenRead(backup);
                using FileStream modified = File.OpenRead(current);
                return hash.ComputeHash(original).SequenceEqual(hash.ComputeHash(modified));
            }

            private static void RestoreFile(string backup, string destination)
            {
                // Stage the replacement beside the target, including packages on another volume.
                string temporary = Path.Combine(Path.GetDirectoryName(destination), $".quietstatic-restore-{Guid.NewGuid():N}.tmp");
                try
                {
                    File.Copy(backup, temporary);
                    if (File.Exists(destination)) File.Replace(temporary, destination, null);
                    else File.Move(temporary, destination);
                }
                finally { File.Delete(temporary); }
            }

            public void Dispose()
            {
                if (retainBackups || !Directory.Exists(DirectoryPath)) return;
                try { Directory.Delete(DirectoryPath, recursive: true); }
                catch (IOException exception) { Debug.LogWarning($"Could not remove rename backups at '{DirectoryPath}': {exception.Message}"); }
                catch (UnauthorizedAccessException exception) { Debug.LogWarning($"Could not remove rename backups at '{DirectoryPath}': {exception.Message}"); }
            }
        }

        private static string FindSourceProperty(UnityEngine.Object source, ContentIdKind kind, string id)
        {
            using var serialized = new SerializedObject(source);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.String && Normalize(property.stringValue) == id &&
                    IsSourceField(kind, source, property.propertyPath))
                    return property.propertyPath;
            return null;
        }

        private static bool IsSourceField(ContentIdKind kind, UnityEngine.Object source, string path) => kind switch
        {
            ContentIdKind.Flag => source is FlagDatabase && path.StartsWith("flags.Array.data[", StringComparison.Ordinal) &&
                                  path.EndsWith("].id", StringComparison.Ordinal),
            ContentIdKind.Objective => source is ObjectiveDefinition && path == "id",
            ContentIdKind.GameState => source is QuietStatic.Toolkit.Core.GameStateDatabase &&
                                       path.StartsWith("states.Array.data[", StringComparison.Ordinal) &&
                                       path.EndsWith("].state", StringComparison.Ordinal),
            ContentIdKind.Cinematic => source is CinematicDefinition && path == "id",
            _ => false
        };

        private static void EnsureFlagIdAvailable(ContentIdKind kind, string id, UnityEngine.Object source)
        {
            if (kind != ContentIdKind.Flag) return;
            foreach (string guid in AssetDatabase.FindAssets("t:FlagDatabase"))
            {
                var database = AssetDatabase.LoadAssetAtPath<QuietStatic.Toolkit.Flags.FlagDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                if (database != null && database != source && FindSourceProperty(database, kind, id) != null)
                    throw new InvalidOperationException($"Flag ID '{id}' already exists in catalog '{database.name}'.");
            }
        }

        private static FieldInfo ResolveField(Type type, string propertyPath)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            string[] segments = propertyPath.Split('.');
            FieldInfo field = null;
            for (int index = 0; index < segments.Length; index++)
            {
                if (segments[index] == "Array" && index + 1 < segments.Length &&
                    segments[index + 1].StartsWith("data[", StringComparison.Ordinal))
                {
                    if (type.IsArray) type = type.GetElementType();
                    else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                        type = type.GetGenericArguments()[0];
                    else return null;
                    index++;
                    continue;
                }

                field = null;
                for (Type parent = type; parent != null && field == null; parent = parent.BaseType)
                    field = parent.GetField(segments[index], flags);
                if (field == null) return null;
                type = field.FieldType;
            }
            return field;
        }

        private static bool IsPossibleReference(ContentIdKind kind, string name, string propertyPath)
        {
            // A string passed to a persistent UnityEvent may refer to an ID, depending on the method.
            if (name == "m_StringArgument") return true;
            string lower = name.ToLowerInvariant();
            string path = propertyPath.ToLowerInvariant();
            return kind switch
            {
                ContentIdKind.Flag => !path.EndsWith(".value.text") && !path.EndsWith(".expected.text") && !path.EndsWith(".defaultstring") && (lower.Contains("flag") || path.Contains("flag")),
                ContentIdKind.Objective => lower.Contains("objectiveid") || path.Contains("objectiveid"),
                ContentIdKind.GameState => lower == "state" || lower.Contains("stateid") || lower.EndsWith("state") || path.Contains("stateid"),
                ContentIdKind.Cinematic => lower.Contains("cinematicid") || path.Contains("cinematicid"),
                _ => false
            };
        }

        private static IEnumerable<string> CandidatePaths() => AssetDatabase.GetAllAssetPaths().Where(path =>
            path.StartsWith("Assets/", StringComparison.Ordinal) &&
            (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)));

        private static bool IsScenePath(string path) => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
    }
}
