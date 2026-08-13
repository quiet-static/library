using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuietStatic.Toolkit.Objectives;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Objectives
{
    /// <summary>
    /// Project-wide authoring and reference explorer for an
    /// <see cref="ObjectiveDatabase"/> and its definition assets.
    /// </summary>
    public sealed class ObjectiveDatabaseEditorWindow : EditorWindow
    {
        private static readonly string[] ReferenceExtensions =
        {
            ".asset", ".prefab", ".unity"
        };

        private ObjectiveDatabase database;
        private ObjectiveDefinition objectiveToAdd;
        private SerializedObject serializedDatabase;
        private SerializedProperty objectives;
        private Vector2 scroll;
        private string search = string.Empty;
        private bool referenceScanCompleted;

        private readonly Dictionary<ObjectiveDefinition, IReadOnlyList<string>>
            references = new();

        [MenuItem("Tools/Quiet Static/Objectives/Objective Database")]
        public static void Open()
        {
            GetWindow<ObjectiveDatabaseEditorWindow>("Objective Database");
        }

        /// <summary>Opens the explorer with a specific database selected.</summary>
        public static void Open(ObjectiveDatabase selectedDatabase)
        {
            ObjectiveDatabaseEditorWindow window =
                GetWindow<ObjectiveDatabaseEditorWindow>("Objective Database");
            window.database = selectedDatabase;
            window.Bind();
            window.Repaint();
        }

        private void OnEnable()
        {
            if (database == null)
            {
                string guid = AssetDatabase.FindAssets("t:ObjectiveDatabase")
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .FirstOrDefault();
                database = string.IsNullOrEmpty(guid)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<ObjectiveDatabase>(
                        AssetDatabase.GUIDToAssetPath(guid));
            }

            Bind();
        }

        private void OnGUI()
        {
            DrawDatabasePicker();

            if (database == null || serializedDatabase == null || objectives == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign or create an Objective Database asset to begin authoring objectives.",
                    MessageType.Info);
                if (GUILayout.Button("Create Objective Database"))
                {
                    CreateDatabase();
                }
                return;
            }

            serializedDatabase.Update();
            DrawToolbar();
            DrawAddExisting();
            DrawSummary();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int index = 0; index < objectives.arraySize; index++)
            {
                SerializedProperty element = objectives.GetArrayElementAtIndex(index);
                ObjectiveDefinition objective =
                    element.objectReferenceValue as ObjectiveDefinition;

                if (objective != null && !Matches(objective))
                {
                    continue;
                }

                DrawObjective(index, element, objective);
            }
            EditorGUILayout.EndScrollView();

            if (serializedDatabase.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(database);
            }
        }

        private void DrawDatabasePicker()
        {
            EditorGUI.BeginChangeCheck();
            database = (ObjectiveDatabase)EditorGUILayout.ObjectField(
                new GUIContent("Database", "Objective Database asset to explore."),
                database,
                typeof(ObjectiveDatabase),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                Bind();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                search = GUILayout.TextField(
                    search,
                    GUI.skin.FindStyle("ToolbarSearchTextField"));

                if (GUILayout.Button(
                        "Refresh References",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(125f)))
                {
                    RefreshReferences();
                }

                if (GUILayout.Button(
                        "Create Objective",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(105f)))
                {
                    CreateObjective();
                }
            }
        }

        private void DrawAddExisting()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                objectiveToAdd = (ObjectiveDefinition)EditorGUILayout.ObjectField(
                    new GUIContent(
                        "Add Existing",
                        "Definition asset to append without creating or duplicating it."),
                    objectiveToAdd,
                    typeof(ObjectiveDefinition),
                    false);

                using (new EditorGUI.DisabledScope(
                           objectiveToAdd == null || Contains(objectiveToAdd)))
                {
                    if (GUILayout.Button("Add", GUILayout.Width(55f)))
                    {
                        AddObjective(objectiveToAdd);
                        objectiveToAdd = null;
                    }
                }
            }
        }

        private void DrawSummary()
        {
            var ids = new List<string>();
            int missingDefinitions = 0;
            int emptyText = 0;

            for (int index = 0; index < objectives.arraySize; index++)
            {
                ObjectiveDefinition objective = objectives
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue as ObjectiveDefinition;
                if (objective == null)
                {
                    missingDefinitions++;
                    continue;
                }

                ids.Add(objective.Id);
                if (string.IsNullOrWhiteSpace(objective.DisplayText))
                {
                    emptyText++;
                }
            }

            int emptyIds = ids.Count(string.IsNullOrWhiteSpace);
            int duplicateIds = ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .GroupBy(id => id, StringComparer.Ordinal)
                .Count(group => group.Count() > 1);
            int unreferenced = references.Count(pair => pair.Value.Count == 0);
            string referenceSummary = referenceScanCompleted
                ? $"{unreferenced} unreferenced"
                : "references not scanned";

            MessageType severity = missingDefinitions > 0 ||
                                   emptyIds > 0 ||
                                   duplicateIds > 0
                ? MessageType.Error
                : emptyText > 0
                    ? MessageType.Warning
                    : MessageType.Info;

            EditorGUILayout.HelpBox(
                $"{objectives.arraySize} objective(s) • {missingDefinitions} missing • " +
                $"{emptyIds} empty ID(s) • {duplicateIds} duplicate ID group(s) • " +
                $"{emptyText} without display text • {referenceSummary}",
                severity);
        }

        private void DrawObjective(
            int index,
            SerializedProperty element,
            ObjectiveDefinition objective)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(
                        element,
                        new GUIContent($"Objective {index}"));

                    if (objective != null)
                    {
                        int referenceCount = references.TryGetValue(
                            objective,
                            out IReadOnlyList<string> paths)
                            ? paths.Count
                            : -1;
                        string label = referenceCount < 0
                            ? "Not scanned"
                            : $"{referenceCount} ref";
                        GUILayout.Label(
                            label,
                            EditorStyles.miniLabel,
                            GUILayout.Width(70f));

                        if (GUILayout.Button("Ping", GUILayout.Width(45f)))
                        {
                            Selection.activeObject = objective;
                            EditorGUIUtility.PingObject(objective);
                        }

                        using (new EditorGUI.DisabledScope(referenceCount <= 0))
                        {
                            if (GUILayout.Button("Refs", GUILayout.Width(45f)))
                            {
                                ObjectiveReferenceResultsWindow.Open(
                                    objective,
                                    paths);
                            }
                        }
                    }

                    if (GUILayout.Button("Remove", GUILayout.Width(60f)) &&
                        EditorUtility.DisplayDialog(
                            "Remove Objective From Database?",
                            objective == null
                                ? "Remove this missing entry?"
                                : $"Remove '{objective.Id}' from the database?\n\n" +
                                  "The Objective Definition asset will not be deleted.",
                            "Remove",
                            "Cancel"))
                    {
                        RemoveObjective(index);
                        GUIUtility.ExitGUI();
                    }
                }

                if (objective == null)
                {
                    EditorGUILayout.HelpBox(
                        "This database entry has no Objective Definition.",
                        MessageType.Error);
                    return;
                }

                DrawDefinitionFields(objective);
            }
        }

        private void DrawDefinitionFields(ObjectiveDefinition objective)
        {
            var serializedObjective = new SerializedObject(objective);
            serializedObjective.Update();

            SerializedProperty id = serializedObjective.FindProperty("id");
            SerializedProperty title = serializedObjective.FindProperty("title");
            SerializedProperty description =
                serializedObjective.FindProperty("description");
            SerializedProperty activationRequirement =
                serializedObjective.FindProperty("activationRequirement");
            SerializedProperty completionRequirement =
                serializedObjective.FindProperty("completionRequirement");

            EditorGUILayout.PropertyField(
                id,
                new GUIContent(
                    "Stable ID",
                    "Save-game identifier. Renaming does not rewrite saved data."));
            EditorGUILayout.PropertyField(title);
            EditorGUILayout.PropertyField(description);
            EditorGUILayout.PropertyField(activationRequirement, true);
            EditorGUILayout.PropertyField(completionRequirement, true);

            string normalizedId = id.stringValue?.Trim();
            if (string.IsNullOrEmpty(normalizedId))
            {
                EditorGUILayout.HelpBox(
                    "Stable ID cannot be empty.",
                    MessageType.Error);
            }
            else if (IsDuplicateId(normalizedId))
            {
                EditorGUILayout.HelpBox(
                    $"'{normalizedId}' is duplicated in this database.",
                    MessageType.Error);
            }

            if (string.IsNullOrWhiteSpace(title.stringValue) &&
                string.IsNullOrWhiteSpace(description.stringValue))
            {
                EditorGUILayout.HelpBox(
                    "Add a title or description so this objective has player-facing text.",
                    MessageType.Warning);
            }

            if (serializedObjective.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(objective);
            }
        }

        private bool Matches(ObjectiveDefinition objective)
        {
            return string.IsNullOrWhiteSpace(search) ||
                   ContainsIgnoreCase(objective.name, search) ||
                   ContainsIgnoreCase(objective.Id, search) ||
                   ContainsIgnoreCase(objective.Title, search) ||
                   ContainsIgnoreCase(objective.Description, search);
        }

        private bool IsDuplicateId(string objectiveId)
        {
            int count = 0;
            for (int index = 0; index < objectives.arraySize; index++)
            {
                ObjectiveDefinition objective = objectives
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue as ObjectiveDefinition;
                if (objective != null &&
                    string.Equals(
                        objective.Id,
                        objectiveId,
                        StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count > 1;
        }

        private bool Contains(ObjectiveDefinition objective)
        {
            for (int index = 0; index < objectives.arraySize; index++)
            {
                if (objectives.GetArrayElementAtIndex(index).objectReferenceValue ==
                    objective)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddObjective(ObjectiveDefinition objective)
        {
            if (objective == null || Contains(objective))
            {
                return;
            }

            Undo.RecordObject(database, "Add Objective Definition");
            int index = objectives.arraySize;
            objectives.InsertArrayElementAtIndex(index);
            objectives.GetArrayElementAtIndex(index).objectReferenceValue = objective;
            serializedDatabase.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
        }

        private void RemoveObjective(int index)
        {
            Undo.RecordObject(database, "Remove Objective Definition");
            objectives.DeleteArrayElementAtIndex(index);
            if (index < objectives.arraySize &&
                objectives.GetArrayElementAtIndex(index).objectReferenceValue == null)
            {
                objectives.DeleteArrayElementAtIndex(index);
            }
            serializedDatabase.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
        }

        private void CreateDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Objective Database",
                "ObjectiveDatabase",
                "asset",
                "Choose a location for the new objective database.");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            database = CreateInstance<ObjectiveDatabase>();
            AssetDatabase.CreateAsset(database, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = database;
            Bind();
        }

        private void CreateObjective()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Objective Definition",
                "NewObjective",
                "asset",
                "Choose a location for the new objective definition.");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var objective = CreateInstance<ObjectiveDefinition>();
            var serializedObjective = new SerializedObject(objective);
            string defaultName = Path.GetFileNameWithoutExtension(path);
            serializedObjective.FindProperty("id").stringValue = defaultName;
            serializedObjective.FindProperty("title").stringValue = defaultName;
            serializedObjective.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(objective, path);
            AssetDatabase.SaveAssets();
            AddObjective(objective);
            Selection.activeObject = objective;
            EditorGUIUtility.PingObject(objective);
        }

        private void Bind()
        {
            serializedDatabase = database == null
                ? null
                : new SerializedObject(database);
            objectives = serializedDatabase?.FindProperty("objectives");
            references.Clear();
            referenceScanCompleted = false;
        }

        private void RefreshReferences()
        {
            references.Clear();
            if (database == null)
            {
                return;
            }

            string databasePath = AssetDatabase.GetAssetPath(database);
            string[] candidatePaths = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
                .Where(path => ReferenceExtensions.Contains(
                    Path.GetExtension(path),
                    StringComparer.OrdinalIgnoreCase))
                .Where(path => !string.Equals(
                    path,
                    databasePath,
                    StringComparison.Ordinal))
                .ToArray();

            foreach (ObjectiveDefinition objective in database.Objectives)
            {
                if (objective == null || references.ContainsKey(objective))
                {
                    continue;
                }

                string objectivePath = AssetDatabase.GetAssetPath(objective);
                var found = new List<string>();
                foreach (string candidatePath in candidatePaths)
                {
                    if (string.Equals(
                            candidatePath,
                            objectivePath,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string[] dependencies = AssetDatabase.GetDependencies(
                        candidatePath,
                        false);
                    if (dependencies.Contains(
                            objectivePath,
                            StringComparer.Ordinal))
                    {
                        found.Add(candidatePath);
                    }
                }

                references[objective] = found;
            }

            referenceScanCompleted = true;
            Repaint();
        }

        private static bool ContainsIgnoreCase(string value, string query)
        {
            return (value ?? string.Empty).IndexOf(
                query,
                StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>Read-only asset references for one objective definition.</summary>
    public sealed class ObjectiveReferenceResultsWindow : EditorWindow
    {
        private ObjectiveDefinition objective;
        private IReadOnlyList<string> paths = Array.Empty<string>();
        private Vector2 scroll;

        public static void Open(
            ObjectiveDefinition selectedObjective,
            IReadOnlyList<string> referencePaths)
        {
            ObjectiveReferenceResultsWindow window =
                GetWindow<ObjectiveReferenceResultsWindow>("Objective References");
            window.objective = selectedObjective;
            window.paths = referencePaths ?? Array.Empty<string>();
            window.Repaint();
        }

        private void OnGUI()
        {
            string label = objective == null
                ? "Missing objective"
                : $"References to '{objective.Id}'";
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "These are direct serialized asset dependencies. Runtime-created references are not detectable.",
                MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string path in paths)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.SelectableLabel(
                        path,
                        GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    if (GUILayout.Button("Select", GUILayout.Width(60f)))
                    {
                        UnityEngine.Object asset =
                            AssetDatabase.LoadMainAssetAtPath(path);
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }
                }
            }

            if (paths.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No serialized asset references found outside the selected database.",
                    MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    /// <summary>Adds an explorer shortcut to the normal database Inspector.</summary>
    [CustomEditor(typeof(ObjectiveDatabase))]
    public sealed class ObjectiveDatabaseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (GUILayout.Button("Open Objective Database Explorer"))
            {
                ObjectiveDatabaseEditorWindow.Open(
                    target as ObjectiveDatabase);
            }
        }
    }
}
