using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuietStatic.Toolkit.Dialogue;
using QuietStatic.Toolkit.Flags;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Dialogue
{
    /// <summary>Imports validated, ID-linked JSON into the existing index-linked DialogueTree asset.</summary>
    public static class DialogueJsonImporter
    {
        private const int SupportedSchemaVersion = 1;
        private const string DefaultOutputFolder = "Assets/Generated/Dialogue";
        private const string MissingNext = "\u0001missing\u0001";

        [Serializable]
        private sealed class Document
        {
            public int schemaVersion;
            public string treeId;
            public string startNode;
            public Node[] nodes;
        }

        [Serializable]
        private sealed class Node
        {
            public string id;
            public string speaker;
            public string text;
            public string[] flagsToSetOnEnter;
            public Choice[] choices;
        }

        [Serializable]
        private sealed class Choice
        {
            public string text;
            public string next = MissingNext;
            public string[] flagsToSet;
            public Condition condition;
        }

        [Serializable]
        private sealed class Condition
        {
            public string mode;
            public string[] flags;
        }

        /// <summary>Returns whether the current selection is an importable JSON text asset.</summary>
        [MenuItem("Tools/Quiet Static/Dialogue/Import Selected Dialogue JSON", true)]
        private static bool CanImportSelected() =>
            Selection.activeObject is TextAsset && AssetDatabase.GetAssetPath(Selection.activeObject)
                .EndsWith(".json", StringComparison.OrdinalIgnoreCase);

        /// <summary>Imports the selected JSON into the default generated asset folder.</summary>
        [MenuItem("Tools/Quiet Static/Dialogue/Import Selected Dialogue JSON")]
        private static void ImportSelected()
        {
            try
            {
                DialogueTree tree = Import((TextAsset)Selection.activeObject, DefaultOutputFolder);
                Selection.activeObject = tree;
                EditorGUIUtility.PingObject(tree);
                GameLogger.Log(nameof(DialogueJsonImporter), tree,
                    $"Imported dialogue JSON to {AssetDatabase.GetAssetPath(tree)}.");
            }
            catch (Exception exception)
            {
                GameLogger.Error(nameof(DialogueJsonImporter), Selection.activeObject,
                    $"Dialogue import failed: {exception.Message}");
            }
        }

        /// <summary>Validates and imports a JSON TextAsset, updating an existing generated asset in place.</summary>
        /// <param name="source">JSON source asset.</param>
        /// <param name="outputFolder">Project-relative Assets folder for generated DialogueTree assets.</param>
        /// <returns>The created or updated DialogueTree.</returns>
        /// <exception cref="ArgumentException">Thrown when input, output, or dialogue data is invalid.</exception>
        public static DialogueTree Import(TextAsset source, string outputFolder = DefaultOutputFolder)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(outputFolder) || !outputFolder.StartsWith("Assets", StringComparison.Ordinal))
                throw new ArgumentException("Output folder must be a project-relative path under Assets.", nameof(outputFolder));

            string sourcePath = AssetDatabase.GetAssetPath(source);
            Document document;
            try
            {
                document = JsonUtility.FromJson<Document>(source.text);
            }
            catch (Exception exception)
            {
                throw new ArgumentException($"{sourcePath}: invalid JSON: {exception.Message}", exception);
            }

            List<string> errors = Validate(document, sourcePath);
            if (errors.Count > 0)
                throw new ArgumentException(string.Join(Environment.NewLine, errors));

            Dictionary<string, int> indexes = document.nodes
                .Select((node, index) => new { node.id, index })
                .ToDictionary(pair => pair.id, pair => pair.index, StringComparer.Ordinal);
            DialogueTree.Node[] nodes = document.nodes.Select(node => new DialogueTree.Node
            {
                id = node.id,
                speaker = node.speaker,
                line = node.text,
                nextNodeIndex = -1,
                flagsToSetOnEnter = node.flagsToSetOnEnter ?? Array.Empty<string>(),
                choices = node.choices.Select(choice => new DialogueTree.Choice
                {
                    text = choice.text,
                    nextNodeIndex = string.IsNullOrEmpty(choice.next) ? -1 : indexes[choice.next],
                    flagsToSet = choice.flagsToSet ?? Array.Empty<string>(),
                    availabilityRequirement = CreateRequirement(choice.condition)
                }).ToArray()
            }).ToArray();

            EnsureFolder(outputFolder);
            string safeName = MakeSafeFileName(document.treeId);
            string assetPath = $"{outputFolder.TrimEnd('/')}/{safeName}.asset";
            DialogueTree tree = AssetDatabase.LoadAssetAtPath<DialogueTree>(assetPath);
            bool created = tree == null;
            if (created)
            {
                tree = ScriptableObject.CreateInstance<DialogueTree>();
                AssetDatabase.CreateAsset(tree, assetPath);
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(tree, "Import Dialogue JSON");
            }

            try
            {
                SerializedObject serialized = new SerializedObject(tree);
                serialized.FindProperty("nodes").arraySize = nodes.Length;
                for (int i = 0; i < nodes.Length; i++)
                    WriteNode(serialized.FindProperty("nodes").GetArrayElementAtIndex(i), nodes[i]);
                serialized.FindProperty("startNodeIndex").intValue = indexes[document.startNode];
                serialized.FindProperty("generatedFromJson").boolValue = true;
                serialized.FindProperty("sourceJsonPath").stringValue = sourcePath;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(tree);
                AssetDatabase.SaveAssets();
                return tree;
            }
            catch
            {
                if (created)
                    AssetDatabase.DeleteAsset(assetPath);
                throw;
            }
        }

        private static List<string> Validate(Document document, string sourcePath)
        {
            var errors = new List<string>();
            if (document == null)
            {
                errors.Add($"{sourcePath}: top-level value must be an object.");
                return errors;
            }
            if (document.schemaVersion != SupportedSchemaVersion)
                errors.Add($"{sourcePath}: schemaVersion must be {SupportedSchemaVersion}.");
            if (string.IsNullOrWhiteSpace(document.treeId))
                errors.Add($"{sourcePath}: treeId must be a non-empty string.");
            if (string.IsNullOrWhiteSpace(document.startNode))
                errors.Add($"{sourcePath}: startNode must be a non-empty string.");
            if (document.nodes == null || document.nodes.Length == 0)
            {
                errors.Add($"{sourcePath}: nodes must be a non-empty array.");
                return errors;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int nodeIndex = 0; nodeIndex < document.nodes.Length; nodeIndex++)
            {
                Node node = document.nodes[nodeIndex];
                string at = $"{sourcePath}: node[{nodeIndex}]";
                if (node == null) { errors.Add($"{at} must be an object."); continue; }
                if (string.IsNullOrWhiteSpace(node.id)) errors.Add($"{at}.id must be non-empty.");
                else if (!ids.Add(node.id)) errors.Add($"{at}.id duplicates '{node.id}'.");
                if (node.speaker == null) errors.Add($"{at}.speaker is required.");
                if (node.text == null) errors.Add($"{at}.text is required.");
                ValidateStrings(node.flagsToSetOnEnter, $"{at}.flagsToSetOnEnter", errors);
                if (node.choices == null || node.choices.Length == 0)
                {
                    errors.Add($"{at}.choices must contain at least one choice.");
                    continue;
                }
                for (int choiceIndex = 0; choiceIndex < node.choices.Length; choiceIndex++)
                {
                    Choice choice = node.choices[choiceIndex];
                    string choiceAt = $"{at}.choice[{choiceIndex}]";
                    if (choice == null) { errors.Add($"{choiceAt} must be an object."); continue; }
                    if (string.IsNullOrWhiteSpace(choice.text)) errors.Add($"{choiceAt}.text must be non-empty.");
                    if (choice.next == MissingNext) errors.Add($"{choiceAt}.next is required; use null to end.");
                    ValidateStrings(choice.flagsToSet, $"{choiceAt}.flagsToSet", errors);
                    ValidateCondition(choice.condition, $"{choiceAt}.condition", errors);
                }
            }
            if (!string.IsNullOrWhiteSpace(document.startNode) && !ids.Contains(document.startNode))
                errors.Add($"{sourcePath}: startNode references nonexistent node '{document.startNode}'.");
            for (int nodeIndex = 0; nodeIndex < document.nodes.Length; nodeIndex++)
            {
                Node node = document.nodes[nodeIndex];
                if (node?.choices == null) continue;
                for (int choiceIndex = 0; choiceIndex < node.choices.Length; choiceIndex++)
                {
                    string next = node.choices[choiceIndex]?.next;
                    if (!string.IsNullOrEmpty(next) && next != MissingNext && !ids.Contains(next))
                        errors.Add($"{sourcePath}: node[{nodeIndex}].choice[{choiceIndex}].next references nonexistent node '{next}'.");
                }
            }
            return errors;
        }

        private static void ValidateStrings(string[] values, string at, ICollection<string> errors)
        {
            if (values != null && values.Any(string.IsNullOrWhiteSpace))
                errors.Add($"{at} must contain only non-empty strings.");
        }

        private static void WriteNode(SerializedProperty target, DialogueTree.Node node)
        {
            target.FindPropertyRelative("id").stringValue = node.id;
            target.FindPropertyRelative("speaker").stringValue = node.speaker;
            target.FindPropertyRelative("line").stringValue = node.line;
            target.FindPropertyRelative("nextNodeIndex").intValue = -1;
            WriteStrings(target.FindPropertyRelative("flagsToSetOnEnter"), node.flagsToSetOnEnter);
            SerializedProperty choices = target.FindPropertyRelative("choices");
            choices.arraySize = node.choices.Length;
            for (int index = 0; index < node.choices.Length; index++)
            {
                SerializedProperty choice = choices.GetArrayElementAtIndex(index);
                choice.FindPropertyRelative("text").stringValue = node.choices[index].text;
                choice.FindPropertyRelative("nextNodeIndex").intValue = node.choices[index].nextNodeIndex;
                WriteStrings(choice.FindPropertyRelative("flagsToSet"), node.choices[index].flagsToSet);
                WriteRequirement(
                    choice.FindPropertyRelative("availabilityRequirement"),
                    node.choices[index].availabilityRequirement);
            }
        }

        private static void WriteStrings(SerializedProperty target, string[] values)
        {
            target.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
                target.GetArrayElementAtIndex(index).stringValue = values[index];
        }

        private static FlagRequirement CreateRequirement(Condition condition)
        {
            if (condition == null || string.IsNullOrWhiteSpace(condition.mode))
                return new FlagRequirement();
            Enum.TryParse(condition.mode, true, out FlagRequirementMode mode);
            return new FlagRequirement(mode, condition.flags);
        }

        private static void ValidateCondition(
            Condition condition,
            string at,
            ICollection<string> errors)
        {
            if (condition == null ||
                (string.IsNullOrWhiteSpace(condition.mode) &&
                 (condition.flags == null || condition.flags.Length == 0)))
                return;
            if (string.IsNullOrWhiteSpace(condition.mode) ||
                !Enum.TryParse(condition.mode, true, out FlagRequirementMode mode))
            {
                errors.Add($"{at}.mode must be None, All, Any, NotAll, or NotAny.");
                return;
            }
            ValidateStrings(condition.flags, $"{at}.flags", errors);
            if (mode != FlagRequirementMode.None &&
                (condition.flags == null || condition.flags.Length == 0))
                errors.Add($"{at}.flags must contain at least one flag for mode {mode}.");
        }

        private static void WriteRequirement(
            SerializedProperty target,
            FlagRequirement requirement)
        {
            SerializedObjectBox box = new(requirement);
            target.FindPropertyRelative("mode").enumValueIndex = (int)box.Mode;
            WriteStrings(target.FindPropertyRelative("flags"), box.Flags);
        }

        private readonly struct SerializedObjectBox
        {
            public SerializedObjectBox(FlagRequirement requirement)
            {
                Mode = requirement?.Mode ?? FlagRequirementMode.None;
                Flags = requirement?.Flags.ToArray() ?? Array.Empty<string>();
            }
            public FlagRequirementMode Mode { get; }
            public string[] Flags { get; }
        }

        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string segment in path.Replace('\\', '/').Split('/').Skip(1))
            {
                if (string.IsNullOrWhiteSpace(segment)) continue;
                string child = $"{current}/{segment}";
                if (!AssetDatabase.IsValidFolder(child))
                    AssetDatabase.CreateFolder(current, segment);
                current = child;
            }
        }

        private static string MakeSafeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value.Trim();
        }
    }
}
