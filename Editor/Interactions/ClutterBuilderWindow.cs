using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuietStatic.Toolkit.Interactions;
using UnityEditor;
using UnityEngine;
using Batch = QuietStatic.Toolkit.Editor.Interactions.ClutterBatchInteractionAuthoring;

namespace QuietStatic.Toolkit.Editor.Interactions
{
    /// <summary>Assembles reusable clutter, places prefabs, and previews batch interaction setup.</summary>
    public sealed class ClutterBuilderWindow : EditorWindow
    {
        private static readonly string[] Tabs = { "Build Prefab", "Place and Snap", "Batch Interactions" };
        private static readonly string[] PresetLabels = { "Keep Existing", "Inspect (Flavor Text)", "Pick Up" };

        [SerializeField] private int tab;
        [SerializeField] private string assemblyName = "Clutter Group";
        [SerializeField] private GameObject assembly;
        [SerializeField] private List<GameObject> buildSources = new();
        [SerializeField] private GameObject placementPrefab;
        [SerializeField] private Transform placementParent;
        [SerializeField] private GameObject meshSurface;
        [SerializeField] private bool alignToSurface;
        [SerializeField] private float surfaceOffset;
        [SerializeField] private GameObject batchRoot;
        [SerializeField] private Batch.Preset preset = Batch.Preset.Flavor;
        [SerializeField] private InteractionUIChannel channel;
        [SerializeField] private string sharedPrompt = string.Empty;
        [SerializeField] private string sharedFlavorText = string.Empty;

        private readonly List<Batch.Target> batchTargets = new();
        private IReadOnlyList<Batch.Row> batchResults;
        private bool readyToApply;
        private Transform batchChild;
        private Vector2 scroll;
        private string message;
        private MessageType messageType;

        /// <summary>Opens the authoring window from the Content Workspace.</summary>
        public static void Open() => GetWindow<ClutterBuilderWindow>("Clutter Builder");

        private void OnEnable() => minSize = new Vector2(450f, 340f);

        private void OnDisable() => ClutterSurfacePlacement.Cancel();

        private void OnSelectionChange() => Repaint();

        private void OnInspectorUpdate()
        {
            if (tab == 1) Repaint();
        }

        private void OnGUI()
        {
            int selectedTab = GUILayout.Toolbar(tab, Tabs);
            if (selectedTab != tab) OpenTab(selectedTab);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("Exit Play Mode to author clutter.", MessageType.Info);
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, messageType);

            using var scroller = new EditorGUILayout.ScrollViewScope(scroll);
            scroll = scroller.scrollPosition;
            using var disabled = new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode);
            switch (tab)
            {
                case 0: DrawBuild(); break;
                case 1: DrawPlace(); break;
                case 2: DrawBatch(); break;
            }
        }

        private void DrawBuild()
        {
            EditorGUILayout.HelpBox(
                "Queue Hierarchy props and Project prefab or model assets to build an assembly. " +
                "Hierarchy objects are grouped in place. Linked Project instances start side by side.", MessageType.Info);

            DrawBuildSources();
            assemblyName = EditorGUILayout.TextField("Assembly Name", assemblyName);
            GameObject[] sources = buildSources.Where(IsAssemblySource).ToArray();
            using (new EditorGUI.DisabledScope(sources.Length == 0 || string.IsNullOrWhiteSpace(assemblyName)))
            {
                if (GUILayout.Button("Create Assembly"))
                {
                    Run(() =>
                    {
                        assembly = ClutterPrefabAuthoring.CreateAssembly(sources, assemblyName);
                        buildSources.Clear();
                        Selection.activeGameObject = assembly;
                    });
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Staging Assembly", EditorStyles.boldLabel);
            assembly = (GameObject)EditorGUILayout.ObjectField("Assembly Root", assembly, typeof(GameObject), true);
            EditorGUILayout.LabelField("Use Unity’s Move, Rotate, and Scale tools to arrange the children.", EditorStyles.wordWrappedLabel);
            GameObject[] additional = IsSceneObject(assembly)
                ? sources.Where(source => source != assembly && !source.transform.IsChildOf(assembly.transform)).ToArray()
                : Array.Empty<GameObject>();

            using (new EditorGUI.DisabledScope(!IsSceneObject(assembly)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select Assembly")) Selection.activeGameObject = assembly;
                    using (new EditorGUI.DisabledScope(additional.Length == 0))
                    {
                        if (GUILayout.Button($"Add Queued Props ({additional.Length})"))
                        {
                            Run(() =>
                            {
                                ClutterPrefabAuthoring.AddProps(assembly, additional);
                                buildSources.Clear();
                            });
                        }
                    }
                }

                if (GUILayout.Button("Save as Prefab…")) SaveAssembly();
            }

            using (new EditorGUI.DisabledScope(placementPrefab == null))
            {
                if (GUILayout.Button("Open Placement")) OpenTab(1);
            }
        }

        private void DrawBuildSources()
        {
            Rect dropArea = GUILayoutUtility.GetRect(0f, 42f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, GUIContent.none, EditorStyles.helpBox);
            GUI.Label(dropArea, "Drop Hierarchy props, prefabs, or models here", EditorStyles.centeredGreyMiniLabel);

            Event current = Event.current;
            if (GUI.enabled && (current.type == EventType.DragUpdated || current.type == EventType.DragPerform) &&
                dropArea.Contains(current.mousePosition))
            {
                GameObject[] dragged = DragAndDrop.objectReferences.OfType<GameObject>().Where(IsAssemblySource).ToArray();
                DragAndDrop.visualMode = dragged.Length > 0 ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                if (current.type == EventType.DragPerform && dragged.Length > 0)
                {
                    DragAndDrop.AcceptDrag();
                    QueueSources(dragged);
                }
                current.Use();
            }

            GameObject[] selected = Selection.gameObjects.Where(IsAssemblySource).ToArray();
            using (new EditorGUI.DisabledScope(selected.Length == 0))
            {
                if (GUILayout.Button("Queue Selection")) QueueSources(selected);
            }

            if (buildSources.Count == 0) return;
            for (int index = 0; index < buildSources.Count; index++)
            {
                GameObject source = buildSources[index];
                using var row = new EditorGUILayout.HorizontalScope();
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(source, typeof(GameObject), true);
                if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                {
                    buildSources.RemoveAt(index);
                    break;
                }
            }
            if (GUILayout.Button("Clear Queue")) buildSources.Clear();
        }

        private void QueueSources(IEnumerable<GameObject> sources)
        {
            foreach (GameObject source in sources)
                if (!buildSources.Contains(source)) buildSources.Add(source);
            Repaint();
        }

        private void SaveAssembly()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Clutter Prefab", assembly.name, "prefab",
                "Choose a new prefab name and folder under Assets.");
            if (string.IsNullOrEmpty(path)) return;

            Run(() =>
            {
                placementPrefab = ClutterPrefabAuthoring.SavePrefab(assembly, path);
                EditorGUIUtility.PingObject(placementPrefab);
                SetMessage($"Saved {path}. Open Placement to position an instance.", MessageType.Info);
            });
        }

        private void DrawPlace()
        {
            EditorGUILayout.HelpBox(
                "Place a prefab with the Scene view preview, or snap selected Hierarchy objects down onto a surface. " +
                "Assign a Mesh Surface to use that object’s mesh for placement.", MessageType.Info);

            bool placing = ClutterSurfacePlacement.IsPlacing;
            using (new EditorGUI.DisabledScope(placing))
            {
                placementPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", placementPrefab, typeof(GameObject), false);
                placementParent = (Transform)EditorGUILayout.ObjectField("Placement Parent", placementParent, typeof(Transform), true);
                meshSurface = (GameObject)EditorGUILayout.ObjectField("Mesh Surface", meshSurface, typeof(GameObject), true);
                alignToSurface = EditorGUILayout.Toggle("Align to Surface", alignToSurface);
                surfaceOffset = EditorGUILayout.FloatField("Surface Offset", surfaceOffset);
            }

            if (placing)
            {
                EditorGUILayout.LabelField("Click in the Scene view to place. Press Escape to stop.", EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("Stop Placement")) ClutterSurfacePlacement.Cancel();
                return;
            }

            using (new EditorGUI.DisabledScope(!IsPrefabAsset(placementPrefab)))
            {
                if (GUILayout.Button("Start Placement"))
                    Run(() => ClutterSurfacePlacement.Begin(placementPrefab, alignToSurface, surfaceOffset,
                        placementParent, meshSurface));
            }

            using (new EditorGUI.DisabledScope(!Selection.gameObjects.Any(IsSceneObject)))
            {
                if (GUILayout.Button("Snap Hierarchy Selection"))
                    Run(() => ClutterSurfacePlacement.SnapSelection(alignToSurface, surfaceOffset, meshSurface));
            }
        }

        private void DrawBatch()
        {
            EditorGUILayout.HelpBox(
                "Add specific props from the Hierarchy or a prefab asset. Choose the interaction setup, " +
                "edit prompts or inspection text per prop, then preview the changes before applying. " +
                "Use Browse Children to choose props inside an assembly or prefab.", MessageType.Info);

            GameObject[] selected = Selection.gameObjects;
            using (new EditorGUI.DisabledScope(selected.Length == 0))
            {
                if (GUILayout.Button($"Add Selection ({selected.Length})"))
                    foreach (GameObject item in selected) AddTarget(item);
            }

            DrawBatchChildPicker();

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                int choice = EditorGUILayout.Popup("Interaction", (int)preset, PresetLabels);
                preset = (Batch.Preset)choice;
                if (preset == Batch.Preset.Flavor)
                {
                    channel = (InteractionUIChannel)EditorGUILayout.ObjectField("Default Message Channel", channel,
                        typeof(InteractionUIChannel), false);
                    EditorGUILayout.LabelField("New inspections need a message channel. Existing channels are kept.",
                        EditorStyles.wordWrappedLabel);
                }
                if (check.changed) InvalidatePreview();
            }
            DrawSharedText();

            EditorGUILayout.Space();
            Batch.Target remove = null;
            foreach (Batch.Target target in batchTargets)
            {
                if (DrawBatchTarget(target)) remove = target;
            }
            if (remove != null)
            {
                batchTargets.Remove(remove);
                InvalidatePreview();
                GUI.FocusControl(null);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(batchTargets.Count == 0))
                {
                    if (GUILayout.Button("Clear Targets"))
                    {
                        batchTargets.Clear();
                        InvalidatePreview();
                    }

                    if (GUILayout.Button("Preview Setup")) Run(() =>
                    {
                        batchResults = Batch.Preview(batchTargets, preset, channel);
                        readyToApply = batchResults.Any(row => row.CanApply);
                    });
                }

                using (new EditorGUI.DisabledScope(!readyToApply))
                {
                    if (GUILayout.Button("Apply Setup")) Run(() =>
                    {
                        batchResults = Batch.Apply(batchTargets, preset, channel);
                        readyToApply = false;
                    });
                }
            }
        }

        private void DrawSharedText()
        {
            if (preset == Batch.Preset.Keep) return;
            sharedPrompt = EditorGUILayout.TextField("Shared Prompt", sharedPrompt);
            if (preset == Batch.Preset.Flavor)
                sharedFlavorText = EditorGUILayout.TextField("Shared Flavor Text", sharedFlavorText);

            bool fillPrompt = !string.IsNullOrWhiteSpace(sharedPrompt);
            bool fillFlavor = preset == Batch.Preset.Flavor && !string.IsNullOrWhiteSpace(sharedFlavorText);
            using (new EditorGUI.DisabledScope(batchTargets.Count == 0 || (!fillPrompt && !fillFlavor)))
            {
                if (GUILayout.Button("Fill Blank Fields"))
                {
                    foreach (Batch.Target target in batchTargets)
                    {
                        if (fillPrompt && string.IsNullOrWhiteSpace(target.Prompt)) target.Prompt = sharedPrompt;
                        if (fillFlavor && string.IsNullOrWhiteSpace(target.FlavorText)) target.FlavorText = sharedFlavorText;
                    }
                    InvalidatePreview();
                    GUI.FocusControl(null);
                }
            }
        }

        private void DrawBatchChildPicker()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GameObject root = (GameObject)EditorGUILayout.ObjectField("Browse Children", batchRoot, typeof(GameObject), true);
                using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
                {
                    if (GUILayout.Button("Use Selection", GUILayout.Width(100f))) root = Selection.activeGameObject;
                }
                if (root != batchRoot)
                {
                    batchRoot = root;
                    batchChild = null;
                }
            }

            if (batchRoot == null) return;
            Transform[] children = batchRoot.GetComponentsInChildren<Transform>(true)
                .Where(child => child != batchRoot.transform).ToArray();
            if (children.Length == 0)
            {
                EditorGUILayout.LabelField("The selected object has no children.", EditorStyles.wordWrappedLabel);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                int current = Array.IndexOf(children, batchChild);
                string[] names = children.Select(child => AnimationUtility.CalculateTransformPath(child, batchRoot.transform)).ToArray();
                int picked = EditorGUILayout.Popup("Child", Mathf.Max(0, current), names);
                batchChild = children[picked];
                using (new EditorGUI.DisabledScope(batchTargets.Any(target => target.Source == batchChild.gameObject)))
                {
                    if (GUILayout.Button("Add Child", GUILayout.Width(100f))) AddTarget(batchChild.gameObject);
                }
            }
        }

        private bool DrawBatchTarget(Batch.Target target)
        {
            Batch.Row result = batchResults?.FirstOrDefault(row => row.Target == target);
            using var panel = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            bool remove;
            using (new EditorGUILayout.HorizontalScope())
            {
                string label = target.Source != null ? target.Source.name : "Missing target";
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                remove = GUILayout.Button("Remove", GUILayout.Width(70f));
            }
            if (result != null)
                EditorGUILayout.LabelField(result.Location, EditorStyles.wordWrappedMiniLabel);

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                using var disabled = new EditorGUI.DisabledScope(preset == Batch.Preset.Keep || target.Source == null);
                if (preset != Batch.Preset.Keep) target.Prompt = EditorGUILayout.TextField("Prompt", target.Prompt);
                if (preset == Batch.Preset.Flavor)
                {
                    EditorGUILayout.LabelField("Flavor Text");
                    target.FlavorText = EditorGUILayout.TextArea(target.FlavorText, GUILayout.MinHeight(36f));
                }
                if (check.changed) InvalidatePreview();
            }

            if (result != null)
            {
                MessageType type = result.Status switch
                {
                    Batch.Status.Failed => MessageType.Error,
                    Batch.Status.Skipped => MessageType.Warning,
                    _ => MessageType.None
                };
                EditorGUILayout.HelpBox(string.IsNullOrEmpty(result.Detail)
                    ? result.Status.ToString() : $"{result.Status} · {result.Detail}", type);
            }
            return remove;
        }

        private void AddTarget(GameObject source)
        {
            if (source == null || batchTargets.Any(target => target.Source == source)) return;
            batchTargets.Add(new Batch.Target(source));
            InvalidatePreview();
        }

        private void InvalidatePreview()
        {
            batchResults = null;
            readyToApply = false;
        }

        private void OpenTab(int index)
        {
            if (tab == 1 && index != 1) ClutterSurfacePlacement.Cancel();
            tab = index;
            scroll = Vector2.zero;
            message = null;
            GUI.FocusControl(null);
        }

        private void Run(Action operation)
        {
            message = null;
            try { operation(); }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException ||
                exception is IOException || exception is UnityException)
            {
                SetMessage(exception.Message, MessageType.Error);
            }
        }

        private void SetMessage(string value, MessageType type)
        {
            message = value;
            messageType = type;
            Repaint();
        }

        private static bool IsAssemblySource(GameObject item) => IsSceneObject(item) || IsPrefabAsset(item);
        private static bool IsSceneObject(GameObject item) => item != null && !EditorUtility.IsPersistent(item);
        private static bool IsPrefabAsset(GameObject item) => item != null && PrefabUtility.IsPartOfPrefabAsset(item);
    }
}
