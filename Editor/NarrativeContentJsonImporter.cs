using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Interactions;
using QuietStatic.Toolkit.Objectives;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor
{
    /// <summary>Imports versioned narrative content catalogs into the existing runtime ScriptableObjects.</summary>
    public static class NarrativeContentJsonImporter
    {
        private const string DefaultOutputFolder = "Assets/Generated/Narrative";

        [Serializable] private sealed class Document { public int schemaVersion; public string contentType; public string catalogId; public Item[] items; }
        [Serializable] private sealed class Item
        {
            public string id; public string title; public string description; public string body;
            public string closeLabel = "Close"; public Requirement activationRequirement; public Requirement completionRequirement;
        }
        [Serializable] private sealed class Requirement { public string mode = "None"; public string[] flags; }

        /// <summary>Imports the selected content-catalog JSON.</summary>
        [MenuItem("Tools/Quiet Static/Importers/Import Selected Content JSON")]
        private static void ImportSelected()
        {
            try
            {
                UnityEngine.Object result = Import((TextAsset)Selection.activeObject);
                Selection.activeObject = result;
                EditorGUIUtility.PingObject(result);
            }
            catch (Exception exception)
            {
                GameLogger.Error(nameof(NarrativeContentJsonImporter), Selection.activeObject,
                    $"Narrative content import failed: {exception.Message}");
            }
        }

        [MenuItem("Tools/Quiet Static/Importers/Import Selected Content JSON", true)]
        private static bool CanImportSelected() => Selection.activeObject is TextAsset asset &&
            AssetDatabase.GetAssetPath(asset).EndsWith(".json", StringComparison.OrdinalIgnoreCase);

        /// <summary>Validates and imports a flag, objective, or readable content catalog.</summary>
        public static UnityEngine.Object Import(TextAsset source, string outputFolder = DefaultOutputFolder)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(outputFolder) || !outputFolder.StartsWith("Assets", StringComparison.Ordinal))
                throw new ArgumentException("Output folder must be under Assets.", nameof(outputFolder));
            Document document;
            try { document = JsonUtility.FromJson<Document>(source.text); }
            catch (Exception exception) { throw new ArgumentException($"Invalid JSON: {exception.Message}", exception); }
            Validate(document, AssetDatabase.GetAssetPath(source));
            EnsureFolder(outputFolder);
            string folder = $"{outputFolder.TrimEnd('/')}/{Safe(document.catalogId)}";
            EnsureFolder(folder);
            UnityEngine.Object result = document.contentType switch
            {
                "flags" => ImportFlags(document, folder),
                "objectives" => ImportObjectives(document, folder),
                "readables" => ImportReadables(document, folder),
                _ => throw new ArgumentException($"Unsupported contentType '{document.contentType}'.")
            };
            AssetDatabase.SaveAssets();
            return result;
        }

        private static FlagDatabase ImportFlags(Document document, string folder)
        {
            FlagDatabase database = LoadOrCreate<FlagDatabase>($"{folder}/{Safe(document.catalogId)}.asset");
            SerializedObject serialized = new(database);
            SerializedProperty flags = serialized.FindProperty("flags");
            flags.arraySize = document.items.Length;
            for (int index = 0; index < document.items.Length; index++)
            {
                SerializedProperty flag = flags.GetArrayElementAtIndex(index);
                flag.FindPropertyRelative("id").stringValue = document.items[index].id.Trim();
                flag.FindPropertyRelative("description").stringValue = document.items[index].description ?? string.Empty;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(database); return database;
        }

        private static ObjectiveDatabase ImportObjectives(Document document, string folder)
        {
            var definitions = new ObjectiveDefinition[document.items.Length];
            for (int index = 0; index < document.items.Length; index++)
            {
                Item item = document.items[index];
                ObjectiveDefinition definition = LoadOrCreate<ObjectiveDefinition>($"{folder}/{Safe(item.id)}.asset");
                SerializedObject serialized = new(definition);
                serialized.FindProperty("id").stringValue = item.id.Trim();
                serialized.FindProperty("title").stringValue = item.title ?? string.Empty;
                serialized.FindProperty("description").stringValue = item.description ?? string.Empty;
                WriteRequirement(serialized.FindProperty("activationRequirement"), item.activationRequirement);
                WriteRequirement(serialized.FindProperty("completionRequirement"), item.completionRequirement);
                serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(definition); definitions[index] = definition;
            }
            ObjectiveDatabase database = LoadOrCreate<ObjectiveDatabase>($"{folder}/{Safe(document.catalogId)}.asset");
            SerializedObject databaseObject = new(database); SerializedProperty objectives = databaseObject.FindProperty("objectives");
            objectives.arraySize = definitions.Length;
            for (int index = 0; index < definitions.Length; index++) objectives.GetArrayElementAtIndex(index).objectReferenceValue = definitions[index];
            databaseObject.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(database); return database;
        }

        private static ReadableContentDefinition ImportReadables(Document document, string folder)
        {
            ReadableContentDefinition first = null;
            foreach (Item item in document.items)
            {
                ReadableContentDefinition definition = LoadOrCreate<ReadableContentDefinition>($"{folder}/{Safe(item.id)}.asset");
                SerializedObject serialized = new(definition);
                serialized.FindProperty("title").stringValue = item.title ?? string.Empty;
                serialized.FindProperty("body").stringValue = item.body ?? string.Empty;
                serialized.FindProperty("closeLabel").stringValue = item.closeLabel ?? "Close";
                serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(definition); first ??= definition;
            }
            return first;
        }

        private static void Validate(Document document, string source)
        {
            var errors = new List<string>();
            if (document == null) throw new ArgumentException($"{source}: top-level value must be an object.");
            if (document.schemaVersion != 1) errors.Add("schemaVersion must be 1.");
            if (!new[] { "flags", "objectives", "readables" }.Contains(document.contentType)) errors.Add("contentType must be flags, objectives, or readables.");
            if (string.IsNullOrWhiteSpace(document.catalogId)) errors.Add("catalogId must be non-empty.");
            if (document.items == null || document.items.Length == 0) errors.Add("items must contain at least one item.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Item item in document.items ?? Array.Empty<Item>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id)) errors.Add("Every item must have a non-empty id.");
                else if (!ids.Add(item.id.Trim())) errors.Add($"Item id '{item.id}' is duplicated.");
                if (document.contentType == "readables" && string.IsNullOrWhiteSpace(item?.body)) errors.Add($"Readable '{item?.id}' must have a body.");
                if (document.contentType == "objectives") ValidateRequirement(item?.completionRequirement, item?.id, errors);
            }
            if (errors.Count > 0) throw new ArgumentException($"{source}: {string.Join(Environment.NewLine, errors)}");
        }

        private static void ValidateRequirement(Requirement requirement, string id, ICollection<string> errors)
        {
            if (requirement == null) return;
            if (!Enum.TryParse(requirement.mode, true, out FlagRequirementMode mode)) { errors.Add($"Objective '{id}' has an invalid requirement mode."); return; }
            if (mode != FlagRequirementMode.None && (requirement.flags == null || requirement.flags.Length == 0)) errors.Add($"Objective '{id}' requirement needs flags.");
            if (requirement.flags != null && requirement.flags.Any(string.IsNullOrWhiteSpace)) errors.Add($"Objective '{id}' requirement contains an empty flag.");
        }

        private static void WriteRequirement(SerializedProperty property, Requirement requirement)
        {
            requirement ??= new Requirement(); Enum.TryParse(requirement.mode, true, out FlagRequirementMode mode);
            property.FindPropertyRelative("mode").enumValueIndex = (int)mode;
            SerializedProperty flags = property.FindPropertyRelative("flags"); string[] values = requirement.flags ?? Array.Empty<string>(); flags.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++) flags.GetArrayElementAtIndex(index).stringValue = values[index].Trim();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) { Undo.RegisterCompleteObjectUndo(asset, "Import Narrative Content JSON"); return asset; }
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }

        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string segment in path.Replace('\\', '/').Split('/').Skip(1))
            { if (string.IsNullOrWhiteSpace(segment)) continue; string next = $"{current}/{segment}"; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment); current = next; }
        }

        private static string Safe(string value)
        { foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_'); return value.Trim(); }
    }
}
