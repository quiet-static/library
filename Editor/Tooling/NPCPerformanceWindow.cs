using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Characters.NPC;
using QuietStatic.Toolkit.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Batch = QuietStatic.Toolkit.Editor.Characters.NPC.NPCPerformanceAuthoring;

namespace QuietStatic.Toolkit.Editor.Characters.NPC
{
    /// <summary>Reviews and batch edits animation and behavior settings on authored NPCs.</summary>
    public sealed class NPCPerformanceWindow : EditorWindow
    {
        private enum Scope { AllScenes, OneScene, Subtree }

        private sealed class Entry
        {
            internal NPCController NPC;
            internal string Path;
            internal readonly List<DialogueNPCReaction> Reactions = new();
        }

        private static readonly string[] ScopeLabels = { "All Loaded Scenes", "One Loaded Scene", "Hierarchy Subtree" };
        private static readonly string[] PrefabScopeLabels = { "Current Prefab", "Hierarchy Subtree" };
        private static readonly string[] BehaviorLabels = { "Look At", "Follow", "Wander", "Waypoint Route" };
        private static readonly string[] BatchLabels =
        {
            "Configured Animation Trigger", "Moving and Stationary Triggers", "Movement Speed Parameter",
            "Stop Movement During Dialogue", "Face Target During Dialogue", "Reaction Looks at Player",
            "Reaction Start Trigger", "Reaction End Trigger", "Reaction Start Mode",
            "Reaction End Mode", "Starting NPC Mode", "Behavior Active on Start"
        };

        [SerializeField] private Scope scope;
        [SerializeField] private GameObject subtree;
        [SerializeField] private string search = string.Empty;
        [SerializeField] private Batch.Field field;
        [SerializeField] private Batch.Behavior behavior;

        private readonly List<Entry> entries = new();
        private readonly HashSet<NPCController> selected = new();
        private readonly HashSet<int> expanded = new();
        private readonly List<Scene> scenes = new();
        private Scene selectedScene;
        private PrefabStage prefabStage;
        private Vector2 scroll;
        private string value = string.Empty;
        private string secondaryValue = string.Empty;
        private bool enabledValue;
        private bool batchExpanded = true;
        private bool resultsExpanded;
        private IReadOnlyList<Batch.Row> results;

        /// <summary>Opens the NPC editor from the Content Workspace.</summary>
        public static void Open() => GetWindow<NPCPerformanceWindow>("NPC Animation & Behaviors");

        private void OnEnable()
        {
            minSize = new Vector2(525f, 345f);
            Undo.undoRedoPerformed += Refresh;
            Refresh();
        }

        private void OnDisable() => Undo.undoRedoPerformed -= Refresh;

        private void OnHierarchyChange() => Refresh();

        private void OnSelectionChange() => Repaint();

        private void OnInspectorUpdate()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != prefabStage) Refresh();
        }

        private void OnGUI()
        {
            DrawScope();
            Entry[] shown = entries.Where(Matches).ToArray();
            DrawSelection(shown);

            bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
            bool readOnlyPrefab = prefabStage != null && !AssetDatabase.IsOpenForEdit(prefabStage.assetPath);
            if (playing) EditorGUILayout.HelpBox("Exit Play Mode to edit NPC settings.", MessageType.Info);
            else if (readOnlyPrefab) EditorGUILayout.HelpBox("This prefab asset is read-only.", MessageType.Info);
            using var scroller = new EditorGUILayout.ScrollViewScope(scroll);
            scroll = scroller.scrollPosition;
            using var disabled = new EditorGUI.DisabledScope(playing || readOnlyPrefab);
            DrawBatch();
            if (shown.Length == 0)
                EditorGUILayout.HelpBox(scope == Scope.Subtree && !IsInScope(subtree)
                    ? "Choose a Hierarchy object to inspect its NPCs."
                    : "No NPC controllers match this scope and search.", MessageType.Info);
            foreach (Entry entry in shown) DrawNpc(entry);
        }

        private void DrawScope()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (prefabStage == null)
                {
                    int picked = EditorGUILayout.Popup((int)scope, ScopeLabels, EditorStyles.toolbarPopup, GUILayout.Width(150));
                    if ((Scope)picked != scope) ChangeScope((Scope)picked);
                }
                else
                {
                    int current = scope == Scope.Subtree ? 1 : 0;
                    int picked = EditorGUILayout.Popup(current, PrefabScopeLabels, EditorStyles.toolbarPopup, GUILayout.Width(150));
                    if (picked != current) ChangeScope(picked == 1 ? Scope.Subtree : Scope.AllScenes);
                    GUILayout.Label(prefabStage.prefabContentsRoot != null ? prefabStage.prefabContentsRoot.name : string.Empty,
                        EditorStyles.miniLabel);
                }

                search = GUILayout.TextField(search, EditorStyles.toolbarSearchField);
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(65))) Refresh();
            }

            if (scope == Scope.OneScene && prefabStage == null && scenes.Count > 0)
            {
                int current = Mathf.Max(0, scenes.IndexOf(selectedScene));
                int picked = EditorGUILayout.Popup("Scene", current, scenes.Select(SceneLabel).ToArray());
                if (picked != current)
                {
                    selectedScene = scenes[picked];
                    Refresh();
                }
            }

            if (scope == Scope.Subtree)
            {
                using var row = new EditorGUILayout.HorizontalScope();
                GameObject target = (GameObject)EditorGUILayout.ObjectField("Hierarchy Root", subtree, typeof(GameObject), true);
                using (new EditorGUI.DisabledScope(!IsInScope(Selection.activeGameObject)))
                {
                    if (GUILayout.Button("Use Selection", GUILayout.Width(100))) target = Selection.activeGameObject;
                }
                if (target != subtree && (target == null || IsInScope(target)))
                {
                    subtree = target;
                    Refresh();
                }
            }
        }

        private void DrawSelection(IReadOnlyList<Entry> shown)
        {
            using var row = new EditorGUILayout.HorizontalScope();
            int hidden = selected.Count - shown.Count(entry => selected.Contains(entry.NPC));
            string hiddenLabel = hidden > 0 ? $" ({hidden} hidden by search)" : string.Empty;
            EditorGUILayout.LabelField($"{shown.Count} shown · {selected.Count} selected{hiddenLabel}");
            using (new EditorGUI.DisabledScope(shown.Count == 0))
            {
                if (GUILayout.Button("Select Shown", GUILayout.Width(90)))
                {
                    selected.UnionWith(shown.Select(entry => entry.NPC));
                    results = null;
                }
            }
            using (new EditorGUI.DisabledScope(selected.Count == 0))
            {
                if (GUILayout.Button("Clear Selection", GUILayout.Width(100)))
                {
                    selected.Clear();
                    results = null;
                }
            }
        }

        private void DrawBatch()
        {
            using var panel = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            batchExpanded = EditorGUILayout.Foldout(batchExpanded, "Batch Edit", true);
            if (!batchExpanded) return;

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var next = (Batch.Field)EditorGUILayout.Popup("Field", (int)field, BatchLabels);
                if (next != field)
                {
                    field = next;
                    value = secondaryValue = string.Empty;
                    enabledValue = false;
                }
                DrawBatchInput();
                if (check.changed) results = null;
            }
            if (IsReactionField(field))
                EditorGUILayout.LabelField("This field applies to the authored dialogue reactions on the selected NPCs.",
                    EditorStyles.wordWrappedLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(selected.Count == 0))
                {
                    if (GUILayout.Button("Preview Changes"))
                    {
                        results = Batch.Preview(BatchTargets(), CurrentPatch());
                        resultsExpanded = true;
                    }
                }
                using (new EditorGUI.DisabledScope(results == null || !results.Any(row => row.CanApply)))
                {
                    if (GUILayout.Button("Apply Compatible Changes"))
                        results = Batch.Apply(BatchTargets(), CurrentPatch());
                }
            }

            DrawResults();
        }

        private void DrawBatchInput()
        {
            if (field == Batch.Field.BehaviorActiveOnStart)
                behavior = (Batch.Behavior)EditorGUILayout.Popup("Behavior", (int)behavior, BehaviorLabels);

            switch (field)
            {
                case Batch.Field.BehaviorActiveOnStart:
                case Batch.Field.PauseDuringDialogue:
                case Batch.Field.FaceDuringDialogue:
                case Batch.Field.ReactionLookAtPlayer:
                    enabledValue = EditorGUILayout.Toggle("Enabled", enabledValue);
                    break;
                case Batch.Field.LocomotionTriggers:
                    value = EditorGUILayout.TextField("Moving Trigger", value);
                    secondaryValue = EditorGUILayout.TextField("Stationary Trigger", secondaryValue);
                    break;
                case Batch.Field.LocomotionSpeedParameter:
                    value = EditorGUILayout.TextField("Float Parameter", value);
                    EditorGUILayout.LabelField("Uses speed-driven animation and clears the driver’s two trigger settings.",
                        EditorStyles.wordWrappedLabel);
                    break;
                case Batch.Field.ReactionStartMode:
                case Batch.Field.ReactionEndMode:
                case Batch.Field.StartupMode:
                    value = EditorGUILayout.TextField("Mode Name", value);
                    break;
                default:
                    value = EditorGUILayout.TextField("Trigger Name", value);
                    break;
            }
        }

        private Batch.Patch CurrentPatch() => new(field, value, secondaryValue, enabledValue, behavior);

        private Component[] BatchTargets()
        {
            IEnumerable<Entry> chosen = entries.Where(entry => selected.Contains(entry.NPC));
            return IsReactionField(field)
                ? chosen.SelectMany(entry => entry.Reactions).Cast<Component>().ToArray()
                : chosen.Select(entry => (Component)entry.NPC).ToArray();
        }

        private static bool IsReactionField(Batch.Field item) => item is Batch.Field.ReactionLookAtPlayer or
            Batch.Field.ReactionStartAnimation or Batch.Field.ReactionEndAnimation or Batch.Field.ReactionStartMode or
            Batch.Field.ReactionEndMode;

        private void DrawResults()
        {
            if (results == null) return;
            if (results.Count == 0)
            {
                EditorGUILayout.HelpBox("The selected NPCs have no authored dialogue reactions.", MessageType.Info);
                return;
            }

            string summary = string.Join(" · ", results.GroupBy(row => row.Status)
                .Select(group => $"{group.Count()} {group.Key.ToString().ToLowerInvariant()}"));
            resultsExpanded = EditorGUILayout.Foldout(resultsExpanded, summary, true);
            if (!resultsExpanded) return;
            foreach (Batch.Row result in results)
            {
                using var panel = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
                if (result.Target != null) DrawObjectLink(result.Target.gameObject, ObjectPath(result.Target.gameObject));
                else EditorGUILayout.LabelField("Missing target", EditorStyles.boldLabel);
                if (!string.IsNullOrEmpty(result.Current) || !string.IsNullOrEmpty(result.Proposed))
                    EditorGUILayout.LabelField($"{result.Current} → {result.Proposed}", EditorStyles.wordWrappedMiniLabel);
                string detail = string.IsNullOrEmpty(result.Detail) ? result.Status.ToString() : $"{result.Status} · {result.Detail}";
                EditorGUILayout.LabelField(detail, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawNpc(Entry entry)
        {
            NPCController npc = entry.NPC;
            if (npc == null) return;
            using var panel = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            int id = npc.GetInstanceID();
            using (new EditorGUILayout.HorizontalScope())
            {
                bool included = selected.Contains(npc);
                bool include = EditorGUILayout.Toggle(included, GUILayout.Width(16));
                if (include != included)
                {
                    if (include) selected.Add(npc);
                    else selected.Remove(npc);
                    results = null;
                }

                string name = npc.gameObject.activeInHierarchy ? npc.DisplayName : $"{npc.DisplayName} (inactive)";
                SetExpanded(id, EditorGUILayout.Foldout(expanded.Contains(id), name, true, EditorStyles.foldoutHeader));
                if (GUILayout.Button("Select", GUILayout.Width(55))) Select(npc.gameObject);
            }
            EditorGUILayout.LabelField(entry.Path, EditorStyles.wordWrappedMiniLabel);
            if (!expanded.Contains(id)) return;

            foreach (Animator animator in npc.GetComponentsInChildren<Animator>(true))
            {
                if (animator.GetComponentInParent<NPCController>(true) == npc) DrawComponent(animator);
            }
            foreach (Component component in npc.GetComponents<Component>())
            {
                if (component is NPCAnimatorDriver or NPCAnimationTrigger or NPCStartupBehaviour or NPCModeController or
                    NPCDialogueBehaviour or NPCBehaviour or DialogueTreeResolver) DrawComponent(component);
            }
            foreach (DialogueNPCReaction reaction in entry.Reactions) DrawComponent(reaction);
        }

        private void DrawComponent(Component component)
        {
            if (component == null) return;
            int id = component.GetInstanceID();
            string label = ObjectNames.NicifyVariableName(component.GetType().Name);
            using var panel = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                SetExpanded(id, EditorGUILayout.Foldout(expanded.Contains(id), label, true));
                if (GUILayout.Button("Select", GUILayout.Width(55))) Select(component.gameObject);
            }
            if (component is Animator or DialogueNPCReaction)
                EditorGUILayout.LabelField(ObjectPath(component.gameObject), EditorStyles.wordWrappedMiniLabel);
            if (!expanded.Contains(id)) return;

            using var data = new SerializedObject(component);
            data.Update();
            using SerializedProperty property = data.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath != "m_Script") EditorGUILayout.PropertyField(property, true);
            }
            if (!data.ApplyModifiedProperties()) return;
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            results = null;
            if (component is DialogueNPCReaction)
                EditorApplication.delayCall += RefreshAfterReactionEdit;
        }

        private void RefreshAfterReactionEdit()
        {
            if (this != null) Refresh();
        }

        private void SetExpanded(int id, bool open)
        {
            if (open) expanded.Add(id);
            else expanded.Remove(id);
        }

        private static void DrawObjectLink(GameObject target, string title)
        {
            using var row = new EditorGUILayout.HorizontalScope();
            EditorGUILayout.LabelField(title, EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Select", GUILayout.Width(55))) Select(target);
        }

        private static void Select(GameObject target)
        {
            Selection.activeGameObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private void ChangeScope(Scope next)
        {
            scope = next;
            if (scope == Scope.Subtree && !IsInScope(subtree) && IsInScope(Selection.activeGameObject))
                subtree = Selection.activeGameObject;
            scroll = Vector2.zero;
            Refresh();
        }

        private void Refresh()
        {
            prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            scenes.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded && !EditorSceneManager.IsPreviewScene(scene)) scenes.Add(scene);
            }
            if (!scenes.Contains(selectedScene))
                selectedScene = scenes.Contains(SceneManager.GetActiveScene()) ? SceneManager.GetActiveScene() : scenes.FirstOrDefault();

            GameObject[] contextRoots = prefabStage == null
                ? scenes.SelectMany(scene => scene.GetRootGameObjects()).ToArray()
                : prefabStage.prefabContentsRoot != null ? new[] { prefabStage.prefabContentsRoot } : Array.Empty<GameObject>();
            GameObject[] scopedRoots = scope == Scope.Subtree
                ? IsInScope(subtree) ? new[] { subtree } : Array.Empty<GameObject>()
                : scope == Scope.OneScene && prefabStage == null
                    ? contextRoots.Where(root => root.scene == selectedScene).ToArray()
                    : contextRoots;

            var map = new Dictionary<NPCController, Entry>();
            foreach (GameObject root in scopedRoots)
            foreach (NPCController npc in root.GetComponentsInChildren<NPCController>(true))
                map.Add(npc, new Entry { NPC = npc, Path = ObjectPath(npc.gameObject) });

            if (map.Count > 0)
            {
                foreach (GameObject root in contextRoots)
                foreach (DialogueNPCReaction reaction in root.GetComponentsInChildren<DialogueNPCReaction>(true))
                {
                    using var data = new SerializedObject(reaction);
                    if (data.FindProperty("npc").objectReferenceValue is NPCController npc && npc != null &&
                        map.TryGetValue(npc, out Entry entry)) entry.Reactions.Add(reaction);
                }
            }

            entries.Clear();
            entries.AddRange(map.Values.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase));
            selected.IntersectWith(map.Keys);
            results = null;
            Repaint();
        }

        private bool IsInScope(GameObject target) => target != null &&
            (prefabStage != null ? target.scene == prefabStage.scene : scenes.Contains(target.scene));

        private bool Matches(Entry entry)
        {
            if (entry.NPC == null) return false;
            string query = search.Trim();
            return query.Length == 0 || Contains(entry.Path, query) || Contains(entry.NPC.DisplayName, query) ||
                Contains(entry.NPC.NpcId, query);
        }

        private static bool Contains(string content, string query) =>
            content?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string SceneLabel(Scene scene) => string.IsNullOrEmpty(scene.name) ? "Untitled Scene" : scene.name;

        private static string ObjectPath(GameObject target) =>
            $"{SceneLabel(target.scene)} / {AnimationUtility.CalculateTransformPath(target.transform, null)}";
    }
}
