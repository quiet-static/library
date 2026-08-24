using System;
using System.Linq;
using QuietStatic.Toolkit.SceneFlow;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.SceneFlow
{
    /// <summary>Serialized, Undo-aware mutations used by every scene-flow view.</summary>
    public static class SceneFlowGraphCommands
    {
        public static string GenerateUniqueConnectionId(SceneFlowMap map, string source, string target)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            string prefix = $"{Slug(source)}.to.{Slug(target)}";
            if (prefix == ".to.") prefix = "connection";
            var ids = map.Connections.Where(value => value != null)
                .Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
            if (!ids.Contains(prefix)) return prefix;
            for (int suffix = 2; ; suffix++)
            {
                string candidate = $"{prefix}.{suffix}";
                if (!ids.Contains(candidate)) return candidate;
            }
        }

        public static void Add(SceneFlowMap map, string id, string source, string target)
        {
            Validate(map, id, source, target);
            if (map.Connections.Any(value => value != null && value.Id == id.Trim()))
                throw new InvalidOperationException($"Connection ID '{id.Trim()}' already exists.");
            Undo.RegisterCompleteObjectUndo(map, "Add Scene Connection");
            SerializedObject serialized = new(map);
            SerializedProperty connections = serialized.FindProperty("connections");
            int index = connections.arraySize;
            connections.InsertArrayElementAtIndex(index);
            SerializedProperty connection = connections.GetArrayElementAtIndex(index);
            connection.FindPropertyRelative("id").stringValue = id.Trim();
            SetScene(connection, "fromScene", source);
            SetScene(connection, "toScene", target);
            connection.FindPropertyRelative("additionalScenesToLoad").arraySize = 0;
            connection.FindPropertyRelative("additionalScenesToKeep").arraySize = 0;
            connection.FindPropertyRelative("unloadOtherScenes").boolValue = true;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(map);
        }

        public static void Reconnect(SceneFlowMap map, string id, string source, string target)
        {
            Validate(map, id, source, target);
            SerializedObject serialized = new(map);
            SerializedProperty connection = FindUnique(serialized.FindProperty("connections"), id);
            Undo.RegisterCompleteObjectUndo(map, "Reconnect Scene Connection");
            SetScene(connection, "fromScene", source);
            SetScene(connection, "toScene", target);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(map);
        }

        public static void Rename(SceneFlowMap map, string oldId, string newId)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            string replacement = newId?.Trim() ?? string.Empty;
            if (replacement.Length == 0) throw new ArgumentException("New connection ID is required.", nameof(newId));
            if (map.Connections.Any(value => value != null && value.Id == replacement))
                throw new InvalidOperationException($"Connection ID '{replacement}' already exists.");
            SerializedObject serialized = new(map);
            SerializedProperty connection = FindUnique(serialized.FindProperty("connections"), oldId);
            Undo.RegisterCompleteObjectUndo(map, "Rename Scene Connection");
            connection.FindPropertyRelative("id").stringValue = replacement;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(map);
        }

        public static void Remove(SceneFlowMap map, string id)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            SerializedObject serialized = new(map);
            SerializedProperty connections = serialized.FindProperty("connections");
            SerializedProperty match = FindUnique(connections, id);
            int index = match.propertyPath.EndsWith("]", StringComparison.Ordinal)
                ? int.Parse(match.propertyPath.Split('[', ']')[1])
                : -1;
            Undo.RegisterCompleteObjectUndo(map, "Remove Scene Connection");
            connections.DeleteArrayElementAtIndex(index);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(map);
        }

        private static SerializedProperty FindUnique(SerializedProperty connections, string id)
        {
            string normalized = id?.Trim() ?? string.Empty;
            int found = -1;
            for (int index = 0; index < connections.arraySize; index++)
            {
                if (connections.GetArrayElementAtIndex(index).FindPropertyRelative("id").stringValue.Trim() != normalized)
                    continue;
                if (found >= 0) throw new InvalidOperationException(
                    $"Connection ID '{normalized}' is duplicated and cannot be mutated safely.");
                found = index;
            }
            if (found < 0) throw new InvalidOperationException($"Connection ID '{normalized}' was not found.");
            return connections.GetArrayElementAtIndex(found);
        }

        private static void SetScene(SerializedProperty connection, string field, string value) =>
            connection.FindPropertyRelative(field).FindPropertyRelative("sceneName").stringValue = value.Trim();

        private static void Validate(SceneFlowMap map, string id, string source, string target)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Connection ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Source scene is required.", nameof(source));
            if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("Target scene is required.", nameof(target));
        }

        private static string Slug(string value) => new string((value ?? string.Empty).Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '.')
            .ToArray()).Trim('.');
    }
}
