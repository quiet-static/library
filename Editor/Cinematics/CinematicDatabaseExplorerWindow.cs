using System;
using System.Linq;
using QuietStatic.Toolkit.Cinematics;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Cinematics
{
    /// <summary>Searchable catalog and scene-setup browser for cinematic assets.</summary>
    public sealed class CinematicDatabaseExplorerWindow : EditorWindow
    {
        private CinematicDatabase database;
        private SerializedObject serializedDatabase;
        private SerializedProperty cinematics;
        private string search = string.Empty;
        private Vector2 scroll;

        [MenuItem("Tools/Narrative/Cinematic Database")]
        public static void Open() => GetWindow<CinematicDatabaseExplorerWindow>("Cinematic Database");

        private void OnEnable()
        {
            if (database == null)
            {
                string guid = AssetDatabase.FindAssets("t:CinematicDatabase").OrderBy(value => value).FirstOrDefault();
                if (!string.IsNullOrEmpty(guid))
                    database = AssetDatabase.LoadAssetAtPath<CinematicDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            }
            Bind();
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            database = (CinematicDatabase)EditorGUILayout.ObjectField(
                new GUIContent("Database", "Project cinematic catalog to explore."),
                database, typeof(CinematicDatabase), false);
            if (EditorGUI.EndChangeCheck()) Bind();

            if (database == null || cinematics == null)
            {
                EditorGUILayout.HelpBox("Assign or create a Cinematic Database to browse scene definitions.", MessageType.Info);
                if (GUILayout.Button("Create Cinematic Database")) CreateDatabase();
                return;
            }

            serializedDatabase.Update();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                search = GUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField"));
                if (GUILayout.Button("New Definition", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                    CreateDefinition();
            }

            DrawValidationSummary();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < cinematics.arraySize; i++)
                DrawDefinition(i, cinematics.GetArrayElementAtIndex(i));
            DrawScenePlayers();
            EditorGUILayout.EndScrollView();

            if (serializedDatabase.ApplyModifiedProperties()) EditorUtility.SetDirty(database);
        }

        private void DrawValidationSummary()
        {
            CinematicDefinition[] definitions = Enumerable.Range(0, cinematics.arraySize)
                .Select(i => cinematics.GetArrayElementAtIndex(i).objectReferenceValue as CinematicDefinition)
                .Where(item => item != null).ToArray();
            int emptyIds = definitions.Count(item => string.IsNullOrWhiteSpace(item.Id));
            int duplicateIds = definitions.Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id, StringComparer.Ordinal).Count(group => group.Count() > 1);
            int duplicateBeats = definitions.Sum(item => item.Beats.Where(beat => beat != null && !string.IsNullOrWhiteSpace(beat.id))
                .GroupBy(beat => beat.id, StringComparer.Ordinal).Count(group => group.Count() > 1));
            EditorGUILayout.HelpBox(
                $"{definitions.Length} cinematic(s) • {emptyIds} empty ID(s) • {duplicateIds} duplicate cinematic ID group(s) • {duplicateBeats} duplicate beat ID group(s)",
                emptyIds > 0 || duplicateIds > 0 || duplicateBeats > 0 ? MessageType.Error : MessageType.Info);
        }

        private void DrawDefinition(int index, SerializedProperty property)
        {
            CinematicDefinition definition = property.objectReferenceValue as CinematicDefinition;
            string label = definition == null ? "Missing Definition" : definition.Id;
            string searchable = $"{label} {definition?.Description}";
            if (!string.IsNullOrWhiteSpace(search) && searchable.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                return;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.ObjectField(definition, typeof(CinematicDefinition), false);
                GUILayout.Label(definition == null ? "—" : $"{definition.Beats.Count} beat(s)", EditorStyles.miniLabel, GUILayout.Width(65f));
                if (definition != null && GUILayout.Button("Select", GUILayout.Width(55f)))
                {
                    Selection.activeObject = definition;
                    EditorGUIUtility.PingObject(definition);
                }
                if (GUILayout.Button("Remove", GUILayout.Width(60f)))
                {
                    Undo.RecordObject(database, "Remove Cinematic Definition");
                    cinematics.DeleteArrayElementAtIndex(index);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawScenePlayers()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Loaded Scene Setups", EditorStyles.boldLabel);
            CinematicScenePlayer[] players = FindObjectsByType<CinematicScenePlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (players.Length == 0)
                EditorGUILayout.HelpBox("No Cinematic Scene Player exists in a loaded scene.", MessageType.None);
            foreach (CinematicScenePlayer player in players.OrderBy(item => item.DisplayName))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(player.DisplayName);
                    if (GUILayout.Button("Select", GUILayout.Width(55f))) Selection.activeGameObject = player.gameObject;
                    EditorGUI.BeginDisabledGroup(!Application.isPlaying || player.IsRunning);
                    if (GUILayout.Button("Play", GUILayout.Width(45f))) player.Play();
                    EditorGUI.EndDisabledGroup();
                }
            }
        }

        private void Bind()
        {
            serializedDatabase = database == null ? null : new SerializedObject(database);
            cinematics = serializedDatabase?.FindProperty("cinematics");
        }

        private void CreateDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Cinematic Database", "CinematicDatabase", "asset", "Choose a database location.");
            if (string.IsNullOrEmpty(path)) return;
            database = CreateInstance<CinematicDatabase>();
            AssetDatabase.CreateAsset(database, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = database;
            Bind();
        }

        private void CreateDefinition()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Cinematic Definition", "NewCinematic", "asset", "Choose a definition location.");
            if (string.IsNullOrEmpty(path)) return;
            CinematicDefinition definition = CreateInstance<CinematicDefinition>();
            AssetDatabase.CreateAsset(definition, path);
            Undo.RecordObject(database, "Add Cinematic Definition");
            int index = cinematics.arraySize;
            cinematics.InsertArrayElementAtIndex(index);
            cinematics.GetArrayElementAtIndex(index).objectReferenceValue = definition;
            serializedDatabase.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Selection.activeObject = definition;
        }
    }
}
