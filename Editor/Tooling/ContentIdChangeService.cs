using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Editor.Tooling
{
    public enum ContentIdKind { Flag, Objective, GameState, Cinematic }

    public sealed class ContentIdReference
    {
        internal ContentIdReference(string assetPath, string objectName, string propertyPath)
        { AssetPath = assetPath; ObjectName = objectName; PropertyPath = propertyPath; }
        public string AssetPath { get; }
        public string ObjectName { get; }
        public string PropertyPath { get; }
        public string Label => $"{AssetPath} • {ObjectName} • {PropertyPath}";
    }

    public sealed class ContentIdChangePreview
    {
        internal ContentIdChangePreview(ContentIdKind kind, string sourcePath, string sourceProperty,
            string oldId, string newId, int version, IReadOnlyList<ContentIdReference> references)
        { Kind = kind; SourcePath = sourcePath; SourceProperty = sourceProperty; OldId = oldId; NewId = newId; Version = version; References = references; }
        public ContentIdKind Kind { get; }
        internal string SourcePath { get; }
        internal string SourceProperty { get; }
        internal int Version { get; }
        public string OldId { get; }
        public string NewId { get; }
        public IReadOnlyList<ContentIdReference> References { get; }
    }

    /// <summary>Previewed project-wide serialized ID rename transaction for content catalogs.</summary>
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
            return new ContentIdChangePreview(kind, path, sourceProperty, oldValue, newValue,
                ComputeVersion(source), FindReferences(kind, oldValue, path));
        }

        public static void Apply(ContentIdChangePreview preview)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            UnityEngine.Object source = AssetDatabase.LoadMainAssetAtPath(preview.SourcePath);
            if (source == null || ComputeVersion(source) != preview.Version)
                throw new InvalidOperationException("The source asset changed after preview. Refresh the preview before applying.");
            RewriteConsumers(preview.Kind, preview.OldId, preview.NewId, preview.SourcePath);
            source = AssetDatabase.LoadMainAssetAtPath(preview.SourcePath);
            var serialized = new SerializedObject(source);
            SerializedProperty property = serialized.FindProperty(preview.SourceProperty);
            if (property == null || Normalize(property.stringValue) != preview.OldId)
                throw new InvalidOperationException("The source ID moved after preview.");
            Undo.RegisterCompleteObjectUndo(source, $"Rename {preview.Kind} ID");
            property.stringValue = preview.NewId;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssets();
        }

        public static IReadOnlyList<ContentIdReference> FindReferences(ContentIdKind kind, string id, string excludedPath = null)
        {
            var results = new List<ContentIdReference>();
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in CandidatePaths().Where(path => path != excludedPath))
                    ScanPath(path, kind, Normalize(id), results, null);
            }
            finally { RestoreScenes(setup); }
            return results.OrderBy(item => item.AssetPath, StringComparer.Ordinal)
                .ThenBy(item => item.ObjectName, StringComparer.Ordinal)
                .ThenBy(item => item.PropertyPath, StringComparer.Ordinal).ToArray();
        }

        private static void RewriteConsumers(ContentIdKind kind, string oldId, string replacement, string excludedPath)
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in CandidatePaths().Where(path => path != excludedPath))
                    ScanPath(path, kind, oldId, null, replacement);
            }
            finally { RestoreScenes(setup); }
        }

        private static bool ScanPath(string path, ContentIdKind kind, string id,
            ICollection<ContentIdReference> results, string replacement)
        {
            bool changed = false;
            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects()) changed |= ScanHierarchy(root, path, kind, id, results, replacement);
                if (changed) EditorSceneManager.SaveScene(scene);
                return changed;
            }
            UnityEngine.Object[] assets = path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                ? new[] { AssetDatabase.LoadMainAssetAtPath(path) } : AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (UnityEngine.Object asset in assets)
                changed |= asset is GameObject root ? ScanHierarchy(root, path, kind, id, results, replacement) : ScanObject(asset, path, kind, id, results, replacement);
            if (changed && assets.FirstOrDefault() is GameObject prefab) PrefabUtility.SavePrefabAsset(prefab);
            return changed;
        }

        private static bool ScanHierarchy(GameObject root, string path, ContentIdKind kind, string id,
            ICollection<ContentIdReference> results, string replacement)
        {
            bool changed = false;
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                if (component != null) changed |= ScanObject(component, path, kind, id, results, replacement);
            return changed;
        }

        private static bool ScanObject(UnityEngine.Object value, string path, ContentIdKind kind, string id,
            ICollection<ContentIdReference> results, string replacement)
        {
            if (value == null) return false;
            SerializedObject serialized;
            try { serialized = new SerializedObject(value); } catch (ArgumentException) { return false; }
            SerializedProperty property = serialized.GetIterator();
            bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.String || Normalize(property.stringValue) != id ||
                    !IsConsumerField(kind, property.name, property.propertyPath)) continue;
                results?.Add(new ContentIdReference(path, value.name, property.propertyPath));
                if (replacement == null) continue;
                property.stringValue = replacement;
                changed = true;
            }
            if (changed)
            {
                Undo.RegisterCompleteObjectUndo(value, $"Rename {kind} References");
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(value);
            }
            return changed;
        }

        private static string FindSourceProperty(UnityEngine.Object source, ContentIdKind kind, string id)
        {
            var serialized = new SerializedObject(source);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.String && Normalize(property.stringValue) == id && IsSourceField(kind, property.name)) return property.propertyPath;
            return null;
        }

        private static bool IsSourceField(ContentIdKind kind, string name) => kind switch
        {
            ContentIdKind.Flag => name == "id",
            ContentIdKind.Objective => name == "id",
            ContentIdKind.GameState => name == "state",
            ContentIdKind.Cinematic => name == "id",
            _ => false
        };

        private static bool IsConsumerField(ContentIdKind kind, string name, string propertyPath)
        {
            string lower = name.ToLowerInvariant();
            string path = propertyPath.ToLowerInvariant();
            return kind switch
            {
                ContentIdKind.Flag => lower.Contains("flag") || path.Contains("flag"),
                ContentIdKind.Objective => lower.Contains("objectiveid") || path.Contains("objectiveid"),
                ContentIdKind.GameState => lower == "state" || lower.Contains("stateid") || lower.EndsWith("state") || path.Contains("stateid"),
                ContentIdKind.Cinematic => lower.Contains("cinematicid") || path.Contains("cinematicid"),
                _ => false
            };
        }

        private static IEnumerable<string> CandidatePaths() => AssetDatabase.GetAllAssetPaths().Where(path =>
            path.StartsWith("Assets/", StringComparison.Ordinal) &&
            (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)));

        private static int ComputeVersion(UnityEngine.Object source) => EditorJsonUtility.ToJson(source).GetHashCode();
        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
        private static void RestoreScenes(SceneSetup[] setup)
        {
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
