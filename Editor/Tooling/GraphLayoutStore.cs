using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Tooling
{
    /// <summary>
    /// Stores graph presentation state in project editor metadata, keyed by asset GUID and stable
    /// node ID. Runtime assets never receive layout fields or dirty-state changes.
    /// </summary>
    [FilePath("ProjectSettings/QuietStaticGraphLayouts.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class GraphLayoutStore : ScriptableSingleton<GraphLayoutStore>
    {
        [Serializable]
        private sealed class Entry
        {
            public string assetGuid;
            public string nodeId;
            public Vector2 position;
        }

        [SerializeField] private List<Entry> entries = new();

        /// <summary>Gets a saved position for a node in a specific asset document.</summary>
        public bool TryGetPosition(UnityEngine.Object asset, string nodeId, out Vector2 position)
        {
            return TryGetPosition(GetAssetGuid(asset), nodeId, out position);
        }

        /// <summary>Sets a node position and records the metadata change with Unity Undo.</summary>
        public void SetPosition(
            UnityEngine.Object asset,
            string nodeId,
            Vector2 position,
            string undoName = "Move Graph Node")
        {
            SetPosition(GetAssetGuid(asset), nodeId, position, undoName);
        }

        /// <summary>Removes all presentation metadata belonging to one asset document.</summary>
        public void RemoveDocument(UnityEngine.Object asset, string undoName = "Clear Graph Layout")
        {
            string guid = GetAssetGuid(asset);
            if (guid.Length == 0 || !entries.Exists(entry => entry.assetGuid == guid)) return;

            Undo.RegisterCompleteObjectUndo(this, undoName);
            entries.RemoveAll(entry => entry.assetGuid == guid);
            Save(true);
        }

        internal bool TryGetPosition(string assetGuid, string nodeId, out Vector2 position)
        {
            string guid = Normalize(assetGuid);
            string id = Normalize(nodeId);
            Entry entry = entries.Find(candidate =>
                candidate.assetGuid == guid && candidate.nodeId == id);
            position = entry?.position ?? default;
            return entry != null;
        }

        internal void SetPosition(
            string assetGuid,
            string nodeId,
            Vector2 position,
            string undoName)
        {
            string guid = Normalize(assetGuid);
            string id = Normalize(nodeId);
            if (guid.Length == 0) throw new ArgumentException("Asset GUID must be non-empty.", nameof(assetGuid));
            if (id.Length == 0) throw new ArgumentException("Node ID must be non-empty.", nameof(nodeId));

            Entry entry = entries.Find(candidate =>
                candidate.assetGuid == guid && candidate.nodeId == id);
            if (entry != null && entry.position == position) return;

            Undo.RegisterCompleteObjectUndo(this, undoName);
            if (entry == null)
            {
                entry = new Entry { assetGuid = guid, nodeId = id };
                entries.Add(entry);
            }
            entry.position = position;
            Save(true);
        }

        private static string GetAssetGuid(UnityEngine.Object asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            string path = AssetDatabase.GetAssetPath(asset);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (guid.Length == 0)
            {
                throw new ArgumentException("Graph layout requires a persistent project asset.", nameof(asset));
            }
            return guid;
        }

        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
    }
}
