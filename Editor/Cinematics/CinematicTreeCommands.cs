using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Cinematics;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Cinematics
{
    /// <summary>Undo-aware linear authoring and validated cinematic-tree JSON interchange.</summary>
    public static class CinematicTreeCommands
    {
        [Serializable] private sealed class Document
        {
            public int schemaVersion;
            public string description;
            public List<CinematicTree.Beat> beats = new();
        }

        public static void Add(CinematicTree tree)
        {
            Edit(tree, "Add cinematic beat", beats =>
            {
                int index = beats.arraySize++;
                var beat = beats.GetArrayElementAtIndex(index);
                beat.FindPropertyRelative("id").stringValue = NewId();
                beat.FindPropertyRelative("text").stringValue = "New cinematic text";
                beat.FindPropertyRelative("charactersPerSecond").floatValue = 28;
                beat.FindPropertyRelative("holdSeconds").floatValue = 2;
            });
        }

        public static void Duplicate(CinematicTree tree, int index)
        {
            Edit(tree, "Duplicate cinematic beat", beats =>
            {
                if (index < 0 || index >= beats.arraySize) return;
                beats.InsertArrayElementAtIndex(index);
                beats.GetArrayElementAtIndex(index).FindPropertyRelative("id").stringValue = NewId();
            });
        }

        public static void Move(CinematicTree tree, int from, int to)
        {
            Edit(tree, "Reorder cinematic beat", beats =>
            {
                if (from >= 0 && from < beats.arraySize && to >= 0 && to < beats.arraySize) beats.MoveArrayElement(from, to);
            });
        }

        public static void Delete(CinematicTree tree, int index)
        {
            Edit(tree, "Delete cinematic beat", beats => { if (index >= 0 && index < beats.arraySize) beats.DeleteArrayElementAtIndex(index); });
        }

        private static string NewId() => "beat." + Guid.NewGuid().ToString("N").Substring(0, 10);
        private static void Edit(CinematicTree tree, string label, Action<SerializedProperty> operation)
        {
            if (tree == null) return;
            Undo.RecordObject(tree, label);
            var serialized = new SerializedObject(tree);
            operation(serialized.FindProperty("beats"));
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(tree);
        }

        public static string ExportJson(CinematicTree tree) => JsonUtility.ToJson(new Document
        { schemaVersion = 1, description = tree.Description, beats = tree.Beats.ToList() }, true);

        /// <summary>Validates the entire import before changing the target. IDs and order survive round trips.</summary>
        public static void ImportJson(CinematicTree tree, string json)
        {
            var document = JsonUtility.FromJson<Document>(json);
            if (document == null || document.schemaVersion != 1 || document.beats == null)
                throw new ArgumentException("Expected cinematic-tree JSON with schemaVersion 1 and a beats array.");
            var temporary = ScriptableObject.CreateInstance<CinematicTree>();
            try
            {
                ApplyDocument(temporary, document);
                if (!temporary.TryValidate(out string error)) throw new ArgumentException(error);
                Undo.RecordObject(tree, "Import cinematic tree");
                ApplyDocument(tree, document);
                EditorUtility.SetDirty(tree);
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }

        private static void ApplyDocument(CinematicTree tree, Document document)
        {
            var serialized = new SerializedObject(tree);
            serialized.FindProperty("description").stringValue = document.description ?? string.Empty;
            var beats = serialized.FindProperty("beats");
            beats.arraySize = document.beats.Count;
            for (int i = 0; i < document.beats.Count; i++)
            {
                var source = document.beats[i];
                if (source == null) throw new ArgumentException("Null text beats are not supported.");
                var beat = beats.GetArrayElementAtIndex(i);
                beat.FindPropertyRelative("id").stringValue = source.id ?? string.Empty;
                beat.FindPropertyRelative("text").stringValue = source.text ?? string.Empty;
                beat.FindPropertyRelative("charactersPerSecond").floatValue = source.charactersPerSecond;
                beat.FindPropertyRelative("holdSeconds").floatValue = source.holdSeconds;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
