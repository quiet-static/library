using System;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Tooling
{
    /// <summary>
    /// Owns the editor lifecycle shared by graph documents without depending on a canvas API.
    /// </summary>
    public sealed class GraphDocumentController : IDisposable
    {
        private UnityEngine.Object document;
        private SerializedObject serializedDocument;
        private string selectedNodeId = string.Empty;
        private float zoom = 1f;

        public GraphDocumentController()
        {
            Undo.undoRedoPerformed += HandleUndoRedo;
        }

        public UnityEngine.Object Document => document;
        public SerializedObject SerializedDocument => serializedDocument;
        public string SelectedNodeId => selectedNodeId;
        public float Zoom => zoom;
        public bool HasDocument => document != null;
        public event Action Changed;
        public event Action FrameAllRequested;
        public event Action<string> FrameSelectionRequested;

        /// <summary>Changes the active asset and safely clears document-local selection.</summary>
        public void SetDocument(UnityEngine.Object value)
        {
            if (document == value) return;
            document = value;
            serializedDocument = value == null ? null : new SerializedObject(value);
            selectedNodeId = string.Empty;
            zoom = 1f;
            Changed?.Invoke();
        }

        /// <summary>Retains selection only when the rebuilt model still contains its stable ID.</summary>
        public void Refresh(Func<string, bool> containsNodeId)
        {
            if (document == null) return;
            serializedDocument = new SerializedObject(document);
            if (selectedNodeId.Length > 0 &&
                (containsNodeId == null || !containsNodeId(selectedNodeId)))
            {
                selectedNodeId = string.Empty;
            }
            Changed?.Invoke();
        }

        public void SelectNode(string stableId)
        {
            selectedNodeId = stableId?.Trim() ?? string.Empty;
            Changed?.Invoke();
        }

        public void SetZoom(float value)
        {
            zoom = Mathf.Clamp(value, 0.2f, 2.5f);
            Changed?.Invoke();
        }

        public void RequestFrameAll() => FrameAllRequested?.Invoke();

        public void RequestFrameSelection()
        {
            if (selectedNodeId.Length > 0) FrameSelectionRequested?.Invoke(selectedNodeId);
        }

        /// <summary>Runs one serialized mutation with Undo and marks the asset dirty.</summary>
        public void Mutate(string undoName, Action<SerializedObject> mutation)
        {
            if (document == null) throw new InvalidOperationException("No graph document is selected.");
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));

            Undo.RegisterCompleteObjectUndo(document, undoName);
            serializedDocument.Update();
            mutation(serializedDocument);
            serializedDocument.ApplyModifiedProperties();
            EditorUtility.SetDirty(document);
            Changed?.Invoke();
        }

        public void Save()
        {
            if (document != null) AssetDatabase.SaveAssetIfDirty(document);
        }

        /// <summary>Discards unsaved asset changes by synchronously reimporting its source file.</summary>
        public void Revert()
        {
            if (document == null) return;
            string path = AssetDatabase.GetAssetPath(document);
            if (path.Length == 0) return;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport |
                                            ImportAssetOptions.ForceUpdate);
            serializedDocument = new SerializedObject(document);
            Changed?.Invoke();
        }

        public void Dispose()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
        }

        private void HandleUndoRedo()
        {
            if (document == null) return;
            serializedDocument = new SerializedObject(document);
            Changed?.Invoke();
        }
    }
}
