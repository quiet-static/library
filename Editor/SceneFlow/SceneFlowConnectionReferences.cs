using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.SceneFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Editor.SceneFlow
{
    public enum SceneFlowConnectionReferenceKind
    {
        Request,
        DestinationResponse
    }

    /// <summary>One definite serialized consumer of a scene-flow connection ID.</summary>
    public sealed class SceneFlowConnectionReference
    {
        internal SceneFlowConnectionReference(
            SceneFlowConnectionReferenceKind kind,
            string assetPath,
            string objectName,
            string propertyPath)
        {
            Kind = kind;
            AssetPath = assetPath ?? string.Empty;
            ObjectName = objectName ?? string.Empty;
            PropertyPath = propertyPath ?? string.Empty;
        }

        public SceneFlowConnectionReferenceKind Kind { get; }
        public string AssetPath { get; }
        public string ObjectName { get; }
        public string PropertyPath { get; }
        public string Label => $"{AssetPath} • {ObjectName} • {PropertyPath}";
    }

    /// <summary>Immutable rename/delete preview. Apply verifies its source map has not changed.</summary>
    public sealed class SceneFlowConnectionChangePreview
    {
        internal SceneFlowConnectionChangePreview(
            string mapPath,
            string oldId,
            string newId,
            bool delete,
            int mapVersion,
            IReadOnlyList<SceneFlowConnectionReference> references)
        {
            MapPath = mapPath;
            OldId = oldId;
            NewId = newId;
            IsDelete = delete;
            MapVersion = mapVersion;
            References = references;
        }

        internal string MapPath { get; }
        internal int MapVersion { get; }
        public string OldId { get; }
        public string NewId { get; }
        public bool IsDelete { get; }
        public IReadOnlyList<SceneFlowConnectionReference> References { get; }
    }

    /// <summary>Reference-aware connection ID changes shared by graph and list views.</summary>
    public static class SceneFlowConnectionChangeService
    {
        private static readonly HashSet<string> RequestFields = new(StringComparer.Ordinal)
        {
            "connectionId"
        };

        private static readonly HashSet<string> ResponseFields = new(StringComparer.Ordinal)
        {
            "conditionId"
        };

        public static SceneFlowConnectionChangePreview PreviewRename(
            SceneFlowMap map, string oldId, string newId) =>
            Preview(map, oldId, newId, delete: false);

        public static SceneFlowConnectionChangePreview PreviewDelete(SceneFlowMap map, string id) =>
            Preview(map, id, string.Empty, delete: true);

        public static void Apply(SceneFlowConnectionChangePreview preview)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            SceneFlowMap map = AssetDatabase.LoadAssetAtPath<SceneFlowMap>(preview.MapPath);
            if (map == null) throw new InvalidOperationException("The preview map no longer exists.");
            if (ComputeMapVersion(map) != preview.MapVersion)
                throw new InvalidOperationException("The scene-flow map changed after preview. Refresh the preview before applying.");

            string replacement = preview.IsDelete ? string.Empty : preview.NewId;
            RewriteSerializedConsumers(map, preview.OldId, replacement);
            map = AssetDatabase.LoadAssetAtPath<SceneFlowMap>(preview.MapPath);
            if (map == null) throw new InvalidOperationException("The preview map was unloaded during reference updates.");
            if (preview.IsDelete)
                SceneFlowGraphCommands.Remove(map, preview.OldId);
            else
                SceneFlowGraphCommands.Rename(map, preview.OldId, preview.NewId);
            AssetDatabase.SaveAssets();
        }

        public static IReadOnlyList<SceneFlowConnectionReference> FindReferences(
            string connectionId, SceneFlowMap map = null)
        {
            string id = Normalize(connectionId);
            if (id.Length == 0) return Array.Empty<SceneFlowConnectionReference>();

            var results = new List<SceneFlowConnectionReference>();
            SceneSetup[] prior = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in CandidatePaths(map))
                {
                    if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    {
                        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                        foreach (GameObject root in scene.GetRootGameObjects())
                            ScanObjectHierarchy(root, path, id, results, rewrite: null);
                    }
                    else
                    {
                        UnityEngine.Object[] assets = path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                            ? new[] { AssetDatabase.LoadMainAssetAtPath(path) }
                            : AssetDatabase.LoadAllAssetsAtPath(path);
                        foreach (UnityEngine.Object asset in assets)
                        {
                            if (asset is GameObject root) ScanObjectHierarchy(root, path, id, results, rewrite: null);
                            else ScanObject(asset, path, id, results, rewrite: null);
                        }
                    }
                }
            }
            finally
            {
                RestoreScenes(prior);
            }
            return results.OrderBy(result => result.AssetPath, StringComparer.Ordinal)
                .ThenBy(result => result.ObjectName, StringComparer.Ordinal)
                .ThenBy(result => result.PropertyPath, StringComparer.Ordinal).ToArray();
        }

        private static SceneFlowConnectionChangePreview Preview(
            SceneFlowMap map, string oldId, string newId, bool delete)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            string oldValue = Normalize(oldId);
            string newValue = Normalize(newId);
            if (!map.TryGetConnection(oldValue, out _))
                throw new InvalidOperationException($"Connection ID '{oldValue}' was not found.");
            if (!delete)
            {
                if (newValue.Length == 0) throw new ArgumentException("New connection ID is required.", nameof(newId));
                if (newValue != oldValue && map.TryGetConnection(newValue, out _))
                    throw new InvalidOperationException($"Connection ID '{newValue}' already exists.");
            }
            string mapPath = AssetDatabase.GetAssetPath(map);
            int mapVersion = ComputeMapVersion(map);
            IReadOnlyList<SceneFlowConnectionReference> references = FindReferences(oldValue, map);
            return new SceneFlowConnectionChangePreview(
                mapPath, oldValue, newValue, delete, mapVersion, references);
        }

        private static void RewriteSerializedConsumers(SceneFlowMap map, string oldId, string replacement)
        {
            SceneSetup[] prior = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in CandidatePaths(map))
                {
                    bool changed = false;
                    if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    {
                        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                        foreach (GameObject root in scene.GetRootGameObjects())
                            changed |= ScanObjectHierarchy(root, path, oldId, null, replacement);
                        if (changed) EditorSceneManager.SaveScene(scene);
                    }
                    else
                    {
                        UnityEngine.Object[] assets = path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                            ? new[] { AssetDatabase.LoadMainAssetAtPath(path) }
                            : AssetDatabase.LoadAllAssetsAtPath(path);
                        foreach (UnityEngine.Object asset in assets)
                        {
                            if (asset is GameObject root)
                                changed |= ScanObjectHierarchy(root, path, oldId, null, replacement);
                            else
                                changed |= ScanObject(asset, path, oldId, null, replacement);
                        }
                        if (changed && assets.FirstOrDefault() is GameObject prefabRoot)
                            PrefabUtility.SavePrefabAsset(prefabRoot);
                    }
                }
            }
            finally
            {
                RestoreScenes(prior);
            }
        }

        private static bool ScanObjectHierarchy(
            GameObject root, string path, string id,
            ICollection<SceneFlowConnectionReference> results, string rewrite)
        {
            bool changed = false;
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                if (component != null) changed |= ScanObject(component, path, id, results, rewrite);
            return changed;
        }

        private static bool ScanObject(
            UnityEngine.Object value, string path, string id,
            ICollection<SceneFlowConnectionReference> results, string rewrite)
        {
            if (value == null) return false;
            SerializedObject serialized;
            try { serialized = new SerializedObject(value); }
            catch (ArgumentException) { return false; }
            SerializedProperty property = serialized.GetIterator();
            bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.String ||
                    property.stringValue.Trim() != id) continue;
                SceneFlowConnectionReferenceKind? kind = RequestFields.Contains(property.name)
                    ? SceneFlowConnectionReferenceKind.Request
                    : ResponseFields.Contains(property.name)
                        ? SceneFlowConnectionReferenceKind.DestinationResponse
                        : null;
                if (!kind.HasValue) continue;
                results?.Add(new SceneFlowConnectionReference(
                    kind.Value, path, value.name, property.propertyPath));
                if (rewrite != null)
                {
                    property.stringValue = rewrite;
                    changed = true;
                }
            }
            if (changed)
            {
                Undo.RegisterCompleteObjectUndo(value, "Update Scene Connection References");
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(value);
            }
            return changed;
        }

        private static IEnumerable<string> CandidatePaths(SceneFlowMap map)
        {
            HashSet<string> sceneNames = map == null
                ? null
                : map.Connections.Where(connection => connection != null)
                    .SelectMany(connection => new[] { connection.FromSceneName, connection.ToSceneName })
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToHashSet(StringComparer.Ordinal);
            return AssetDatabase.GetAllAssetPaths().Where(path =>
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)) return false;
                if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) return true;
                return path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) &&
                       (sceneNames == null || sceneNames.Contains(System.IO.Path.GetFileNameWithoutExtension(path)));
            });
        }

        private static int ComputeMapVersion(SceneFlowMap map)
        {
            unchecked
            {
                int hash = 17;
                foreach (SceneFlowMap.Connection connection in map.Connections)
                {
                    hash = hash * 31 + (connection?.Id?.GetHashCode() ?? 0);
                    hash = hash * 31 + (connection?.FromSceneName?.GetHashCode() ?? 0);
                    hash = hash * 31 + (connection?.ToSceneName?.GetHashCode() ?? 0);
                }
                return hash;
            }
        }

        private static void RestoreScenes(SceneSetup[] setup)
        {
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
    }
}
