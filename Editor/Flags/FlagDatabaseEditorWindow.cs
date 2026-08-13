using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Editor.Validation;
using QuietStatic.Toolkit.Flags;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Flags
{
    /// <summary>
    /// Focused authoring window for the existing <see cref="FlagDatabase"/> data model.
    /// All changes use serialized properties and Unity Undo.
    /// </summary>
    public sealed class FlagDatabaseEditorWindow : EditorWindow
    {
        private FlagDatabase database;
        private SerializedObject serializedDatabase;
        private SerializedProperty flags;
        private Vector2 scroll;
        private string search = string.Empty;
        private readonly Dictionary<string, int> usageCounts = new(StringComparer.Ordinal);

        [MenuItem("Tools/Quiet Static/Flags/Flag Database")]
        public static void Open()
        {
            GetWindow<FlagDatabaseEditorWindow>("Flag Database");
        }

        private void OnEnable()
        {
            if (database == null)
            {
                string guid = AssetDatabase.FindAssets("t:FlagDatabase").OrderBy(value => value).FirstOrDefault();
                database = string.IsNullOrEmpty(guid)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<FlagDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            }
            Bind();
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            database = (FlagDatabase)EditorGUILayout.ObjectField(
                new GUIContent("Database", "Flag Database asset to edit."),
                database, typeof(FlagDatabase), false);
            if (EditorGUI.EndChangeCheck())
            {
                Bind();
            }

            if (database == null || serializedDatabase == null || flags == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign or create a Flag Database asset to begin authoring flags.",
                    MessageType.Info);
                if (GUILayout.Button("Create Flag Database"))
                {
                    CreateDatabase();
                }
                return;
            }

            serializedDatabase.Update();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                search = GUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField"));
                if (GUILayout.Button("Refresh Usage", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                {
                    RefreshUsage();
                }
                if (GUILayout.Button("Add Flag", EditorStyles.toolbarButton, GUILayout.Width(75f)))
                {
                    AddFlag();
                }
            }

            DrawSummary();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int index = 0; index < flags.arraySize; index++)
            {
                SerializedProperty definition = flags.GetArrayElementAtIndex(index);
                SerializedProperty id = definition.FindPropertyRelative("id");
                SerializedProperty description = definition.FindPropertyRelative("description");
                if (!Matches(id.stringValue, description.stringValue))
                {
                    continue;
                }

                DrawDefinition(index, id, description);
            }
            EditorGUILayout.EndScrollView();

            if (serializedDatabase.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(database);
                QuietStatic.Toolkit.Flags.Editor.FlagIdDrawer.ClearCache();
            }
        }

        private void DrawSummary()
        {
            var ids = new List<string>();
            for (int index = 0; index < flags.arraySize; index++)
            {
                ids.Add(flags.GetArrayElementAtIndex(index).FindPropertyRelative("id").stringValue?.Trim());
            }

            int empty = ids.Count(string.IsNullOrEmpty);
            int duplicates = ids.Where(id => !string.IsNullOrEmpty(id))
                .GroupBy(id => id, StringComparer.Ordinal)
                .Count(group => group.Count() > 1);
            int unused = ids.Count(id => !string.IsNullOrEmpty(id) &&
                                         usageCounts.TryGetValue(id, out int count) && count == 0);
            EditorGUILayout.HelpBox(
                $"{flags.arraySize} flag(s) • {empty} empty • {duplicates} duplicate group(s) • {unused} unused after usage scan",
                empty > 0 || duplicates > 0 ? MessageType.Error : MessageType.Info);
        }

        private void DrawDefinition(
            int index,
            SerializedProperty id,
            SerializedProperty description)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(id, new GUIContent("Stable ID",
                        "Exact string stored by runtime systems. Renaming does not rewrite references."));
                    string normalized = id.stringValue?.Trim();
                    if (usageCounts.TryGetValue(normalized ?? string.Empty, out int count))
                    {
                        GUILayout.Label($"{count} ref", EditorStyles.miniLabel, GUILayout.Width(45f));
                        if (count > 0 && GUILayout.Button("Find", GUILayout.Width(45f)))
                        {
                            FlagReferenceResultsWindow.Open(normalized);
                        }
                    }
                    if (GUILayout.Button("Delete", GUILayout.Width(55f)) &&
                        EditorUtility.DisplayDialog(
                            "Delete Flag Definition?",
                            $"Delete '{id.stringValue}' from the database?\n\nReferences will not be changed.",
                            "Delete", "Cancel"))
                    {
                        Undo.RecordObject(database, "Delete Flag Definition");
                        flags.DeleteArrayElementAtIndex(index);
                        GUIUtility.ExitGUI();
                    }
                }

                EditorGUILayout.PropertyField(description, new GUIContent("Description"));
                string value = id.stringValue?.Trim();
                if (string.IsNullOrEmpty(value))
                {
                    EditorGUILayout.HelpBox("Stable ID cannot be empty.", MessageType.Error);
                }
                else if (IsDuplicate(value))
                {
                    EditorGUILayout.HelpBox($"'{value}' is duplicated.", MessageType.Error);
                }
                else if (usageCounts.TryGetValue(value, out int count) && count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No serialized references were found. This is advisory; runtime-created references are not detectable.",
                        MessageType.Warning);
                }
            }
        }

        private bool IsDuplicate(string id)
        {
            int count = 0;
            for (int index = 0; index < flags.arraySize; index++)
            {
                if (string.Equals(
                        flags.GetArrayElementAtIndex(index).FindPropertyRelative("id").stringValue?.Trim(),
                        id, StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count > 1;
        }

        private bool Matches(string id, string description)
        {
            return string.IsNullOrWhiteSpace(search) ||
                   (id ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (description ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AddFlag()
        {
            Undo.RecordObject(database, "Add Flag Definition");
            int index = flags.arraySize;
            flags.InsertArrayElementAtIndex(index);
            SerializedProperty definition = flags.GetArrayElementAtIndex(index);
            definition.FindPropertyRelative("id").stringValue = string.Empty;
            definition.FindPropertyRelative("description").stringValue = string.Empty;
            serializedDatabase.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
        }

        private void Bind()
        {
            serializedDatabase = database == null ? null : new SerializedObject(database);
            flags = serializedDatabase?.FindProperty("flags");
            usageCounts.Clear();
            RefreshUsage();
        }

        private void RefreshUsage()
        {
            usageCounts.Clear();
            if (database?.Flags == null)
            {
                return;
            }

            foreach (FlagDatabase.FlagDefinition definition in database.Flags)
            {
                string id = definition?.id?.Trim();
                if (!string.IsNullOrEmpty(id) && !usageCounts.ContainsKey(id))
                {
                    usageCounts[id] = ToolkitValidation.FindFlagReferences(id).Count;
                }
            }
            Repaint();
        }

        private void CreateDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Flag Database", "FlagDatabase", "asset",
                "Choose a location for the new database.");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            database = CreateInstance<FlagDatabase>();
            AssetDatabase.CreateAsset(database, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = database;
            Bind();
        }
    }

    /// <summary>Read-only references for one selected flag ID.</summary>
    public sealed class FlagReferenceResultsWindow : EditorWindow
    {
        private string flagId;
        private IReadOnlyList<ValidationIssue> results = Array.Empty<ValidationIssue>();
        private Vector2 scroll;

        public static void Open(string id)
        {
            var window = GetWindow<FlagReferenceResultsWindow>("Flag References");
            window.flagId = id;
            window.results = ToolkitValidation.FindFlagReferences(id);
            window.Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField($"References to '{flagId}'", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Results are definite serialized string matches in assets and open scenes. Dynamic and closed-scene references are not included.",
                MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (ValidationIssue result in results)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(result.Message);
                    if (GUILayout.Button("Select", GUILayout.Width(60f)) && result.Context != null)
                    {
                        Selection.activeObject = result.Context;
                        EditorGUIUtility.PingObject(result.Context);
                    }
                }
            }
            if (results.Count == 0)
            {
                EditorGUILayout.HelpBox("No serialized references found.", MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
