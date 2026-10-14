using System;
using System.IO;
using System.Linq;
using QuietStatic.Toolkit.Cinematics;
using QuietStatic.Toolkit.Editor.Tooling;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Cinematics
{
    /// <summary>Searchable cinematic-tree browser, linear graph, beat editor and typewriter preview.</summary>
    public sealed class CinematicTreeWorkspaceWindow : EditorWindow
    {
        [SerializeField] private CinematicTree tree;
        private string search = "";
        private int selected;
        private Vector2 browserScroll, beatScroll, detailScroll, graphScroll;
        private bool graph = true;
        private bool preview;
        private double previewStart;

        [MenuItem("Tools/Quiet Static/Cinematics/Cinematic Trees")]
        public static void Open() => Open(Selection.activeObject as CinematicTree);
        public static void Open(CinematicTree value)
        {
            var window = GetWindow<CinematicTreeWorkspaceWindow>("Cinematic Trees");
            window.minSize = new Vector2(900, 450);
            if (value != null) { window.tree = value; window.selected = 0; }
        }
        [OnOpenAsset] private static bool OpenAsset(int id, int line)
        {
            if (EditorUtility.InstanceIDToObject(id) is not CinematicTree selectedTree) return false;
            Open(selectedTree);
            return true;
        }
        private void OnEnable() => Undo.undoRedoPerformed += Repaint;
        private void OnDisable() => Undo.undoRedoPerformed -= Repaint;
        private void Update() { if (preview) Repaint(); }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Create", EditorStyles.toolbarButton)) Create();
                if (GUILayout.Button("Import JSON", EditorStyles.toolbarButton)) Import();
                using (new EditorGUI.DisabledScope(tree == null))
                {
                    if (GUILayout.Button("Export JSON", EditorStyles.toolbarButton)) Export();
                    if (GUILayout.Button("Save", EditorStyles.toolbarButton)) AssetDatabase.SaveAssetIfDirty(tree);
                    if (GUILayout.Button("Validate", EditorStyles.toolbarButton))
                        EditorUtility.DisplayDialog("Cinematic Tree", tree.TryValidate(out string error) ? "All text beats and IDs are valid." : error, "OK");
                }
                graph = GUILayout.Toggle(graph, "Linear Graph", EditorStyles.toolbarButton);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(210)))
                {
                    EditorGUILayout.LabelField("Cinematic Trees", EditorStyles.boldLabel);
                    search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
                    browserScroll = EditorGUILayout.BeginScrollView(browserScroll);
                    foreach (string guid in AssetDatabase.FindAssets("t:CinematicTree"))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        var asset = AssetDatabase.LoadAssetAtPath<CinematicTree>(path);
                        if (!path.Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
                        if (GUILayout.Toggle(tree == asset, new GUIContent(asset.name, path), "Button"))
                        { if (tree != asset) { tree = asset; selected = 0; preview = false; } }
                    }
                    EditorGUILayout.EndScrollView();
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    var picked = (CinematicTree)EditorGUILayout.ObjectField("Tree", tree, typeof(CinematicTree), false);
                    if (picked != tree) { tree = picked; selected = 0; preview = false; }
                    if (tree == null) { EditorGUILayout.HelpBox("Create or select a cinematic tree. Beats always play left to right in list order.", MessageType.Info); return; }
                    if (!tree.TryValidate(out string issue)) EditorGUILayout.HelpBox(issue, MessageType.Warning);
                    if (graph) DrawGraph();
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUILayout.VerticalScope(GUILayout.Width(270))) DrawBeatList();
                        using (new EditorGUILayout.VerticalScope()) DrawDetails();
                    }
                }
            }
        }

        private void DrawGraph()
        {
            graphScroll = EditorGUILayout.BeginScrollView(graphScroll, GUILayout.Height(125));
            using (new EditorGUILayout.HorizontalScope())
                for (int i = 0; i < tree.Beats.Count; i++)
                {
                    var beat = tree.Beats[i];
                    string text = beat?.text ?? "";
                    if (text.Length > 68) text = text.Substring(0, 68) + "…";
                    if (GUILayout.Toggle(selected == i, $"{i + 1}. {beat?.id}\n\n{text}", "Button", GUILayout.Width(210), GUILayout.Height(90)))
                    { if (selected != i) { selected = i; preview = false; } }
                    if (i < tree.Beats.Count - 1) GUILayout.Label("→", GUILayout.Width(24));
                }
            EditorGUILayout.EndScrollView();
        }

        private void DrawBeatList()
        {
            EditorGUILayout.LabelField("Playback Order", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Beat")) { CinematicTreeCommands.Add(tree); selected = tree.Beats.Count - 1; preview = false; }
            beatScroll = EditorGUILayout.BeginScrollView(beatScroll);
            for (int i = 0; i < tree.Beats.Count; i++)
                if (GUILayout.Toggle(selected == i, $"{i + 1:00}  {tree.Beats[i]?.id}", "Button"))
                { if (selected != i) { selected = i; preview = false; } }
            EditorGUILayout.EndScrollView();
            using (new EditorGUI.DisabledScope(tree.Beats.Count == 0))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("↑") && selected > 0) { CinematicTreeCommands.Move(tree, selected, selected - 1); selected--; }
                if (GUILayout.Button("↓") && selected < tree.Beats.Count - 1) { CinematicTreeCommands.Move(tree, selected, selected + 1); selected++; }
                if (GUILayout.Button("Duplicate")) CinematicTreeCommands.Duplicate(tree, selected);
                if (GUILayout.Button("Delete")) { CinematicTreeCommands.Delete(tree, selected); selected = Mathf.Max(0, selected - 1); preview = false; }
            }
        }

        private void DrawDetails()
        {
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            var serialized = new SerializedObject(tree);
            EditorGUILayout.PropertyField(serialized.FindProperty("description"));
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, tree.Beats.Count - 1));
            if (tree.Beats.Count > 0)
            {
                var beat = serialized.FindProperty("beats").GetArrayElementAtIndex(selected);
                EditorGUILayout.LabelField($"Beat {selected + 1}", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Keep the ID stable after creating shot cues. Reordering preserves IDs; duplication creates a new one.", MessageType.Info);
                EditorGUILayout.PropertyField(beat.FindPropertyRelative("id"));
                EditorGUILayout.PropertyField(beat.FindPropertyRelative("text"));
                EditorGUILayout.PropertyField(beat.FindPropertyRelative("charactersPerSecond"));
                EditorGUILayout.PropertyField(beat.FindPropertyRelative("holdSeconds"));
                if (GUILayout.Button(preview ? "Restart Typewriter Preview" : "Preview Text")) { preview = true; previewStart = EditorApplication.timeSinceStartup; }
                var source = tree.Beats[selected];
                if (preview && source != null)
                {
                    string plain = System.Text.RegularExpressions.Regex.Replace(source.text ?? "", "<[^>]*>", "");
                    int visible = source.charactersPerSecond <= 0 ? plain.Length : Mathf.Min(plain.Length, (int)((EditorApplication.timeSinceStartup - previewStart) * source.charactersPerSecond));
                    EditorGUILayout.LabelField(plain.Substring(0, visible), EditorStyles.wordWrappedLabel, GUILayout.MinHeight(70));
                }
            }
            serialized.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();
        }

        private void Create()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Cinematic Tree", "NewCinematicTree", "asset", "Choose an asset location.");
            if (string.IsNullOrEmpty(path)) return;
            tree = CreateInstance<CinematicTree>();
            AssetDatabase.CreateAsset(tree, path);
            CinematicTreeCommands.Add(tree);
            AssetDatabase.SaveAssetIfDirty(tree);
            selected = 0;
        }
        private void Export()
        {
            string path = EditorUtility.SaveFilePanel("Export Cinematic Tree", "", tree.name + ".json", "json");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, CinematicTreeCommands.ExportJson(tree));
        }
        private void Import()
        {
            string source = EditorUtility.OpenFilePanel("Import Cinematic Tree", "", "json");
            if (string.IsNullOrEmpty(source)) return;
            var imported = CreateInstance<CinematicTree>();
            try
            {
                CinematicTreeCommands.ImportJson(imported, File.ReadAllText(source));
                string path = EditorUtility.SaveFilePanelInProject("Save Imported Tree", Path.GetFileNameWithoutExtension(source), "asset", "Creates a new tree.");
                if (string.IsNullOrEmpty(path)) return;
                AssetDatabase.CreateAsset(imported, path);
                tree = imported;
                selected = 0;
                AssetDatabase.SaveAssets();
            }
            catch (Exception error) { EditorUtility.DisplayDialog("Cinematic Import", error.Message, "OK"); }
            finally { if (!AssetDatabase.Contains(imported)) DestroyImmediate(imported); }
        }
    }

    [CustomEditor(typeof(CinematicTree))]
    public sealed class CinematicTreeInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open Cinematic Tree Workspace")) CinematicTreeWorkspaceWindow.Open((CinematicTree)target);
            DrawDefaultInspector();
        }
    }

    [ContentWorkspaceTab]
    public sealed class CinematicTreesWorkspaceTab : AssetCatalogWorkspaceTab
    {
        public override string Id => "cinematic-trees";
        public override string DisplayName => "Cinematic Trees";
        public override int Order => 41;
        protected override System.Collections.Generic.IReadOnlyList<Type> AssetTypes => new[] { typeof(CinematicTree) };
        protected override void DrawActions(UnityEngine.Object asset)
        {
            if (GUILayout.Button("Open Linear Graph")) CinematicTreeWorkspaceWindow.Open(asset as CinematicTree);
        }
    }
}
