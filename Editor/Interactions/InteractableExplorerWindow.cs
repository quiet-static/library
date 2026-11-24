using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Flags;
using QuietStatic.Toolkit.Interactions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Editor.Interactions
{
    /// <summary>
    /// Editor-only index of interaction targets in loaded scenes and project prefabs.
    /// Scene and prefab objects remain the source of truth; no runtime database is created.
    /// </summary>
    public sealed class InteractableExplorerWindow : EditorWindow
    {
        private enum ExplorerScope
        {
            OpenScenes,
            ProjectPrefabs,
            OpenScenesAndPrefabs
        }

        private sealed class Entry
        {
            public Component Target;
            public GameObject Owner;
            public string Kind;
            public string Location;
            public string HierarchyPath;
            public string Prompt;
            public string Requirement;
            public string[] CompletionFlags;
            public bool RequiresCrosshairTarget;
            public bool HasCollider;
            public bool HasHighlighter;
            public ConditionalInteractionMessage[] ConditionalMessages;
        }

        private readonly List<Entry> entries = new();
        private Vector2 scroll;
        private string search = string.Empty;
        private ExplorerScope scope = ExplorerScope.OpenScenes;
        private bool issuesOnly;

        private void OnDisable() => Undo.undoRedoPerformed -= HandleUndo;

        private void HandleUndo()
        {
            RefreshEntries();
        }

        public static void Open()
        {
            GetWindow<InteractableExplorerWindow>("Interactables");
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += HandleUndo;
            RefreshEntries();
        }

        private void OnGUI()
        {
            DrawToolbar();

            List<Entry> visible = entries
                .Where(entry => entry.Target != null && entry.Owner != null)
                .Where(MatchesSearch)
                .Where(entry => !issuesOnly || HasIssue(entry))
                .ToList();

            int basic = entries.Count(entry => entry.Target is Interactable);
            int hold = entries.Count(entry => entry.Target is HoldInteractable);
            int progress = entries.Count(
                entry => entry.Target is ActivatedProgressInteractable);
            int issues = entries.Count(HasIssue);
            EditorGUILayout.HelpBox(
                $"{entries.Count} target(s) • {basic} one-shot • {hold} hold • " +
                $"{progress} autonomous progress • {issues} setup issue(s)",
                issues > 0 ? MessageType.Warning : MessageType.Info);
            EditorGUILayout.HelpBox(
                "Edit prompts, flag requirements, and outcome messages below. Changes support Undo. Save scenes after editing; " +
                "use Save Prefab Changes for project prefab edits.", MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (Entry entry in visible)
            {
                DrawEntry(entry);
            }

            if (visible.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No interaction targets match the current scope and search.",
                    MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                search = GUILayout.TextField(
                    search,
                    GUI.skin.FindStyle("ToolbarSearchTextField"));
                ExplorerScope selectedScope = (ExplorerScope)EditorGUILayout.EnumPopup(
                    scope,
                    EditorStyles.toolbarPopup,
                    GUILayout.Width(165f));
                if (selectedScope != scope)
                {
                    scope = selectedScope;
                    RefreshEntries();
                }
                issuesOnly = GUILayout.Toggle(
                    issuesOnly,
                    "Issues only",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(80f));
                if (GUILayout.Button(
                        "Refresh",
                        EditorStyles.toolbarButton,
                        GUILayout.Width(65f)))
                {
                    RefreshEntries();
                }
                if (scope != ExplorerScope.OpenScenes && GUILayout.Button(
                    "Save Prefab Changes", EditorStyles.toolbarButton, GUILayout.Width(135f)))
                {
                    foreach (string path in entries.Where(entry => entry.Owner != null && EditorUtility.IsPersistent(entry.Owner))
                                 .Select(entry => AssetDatabase.GetAssetPath(entry.Owner)).Distinct())
                        AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(path));
                }
            }
        }

        private static void DrawEntry(Entry entry)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        $"{entry.Owner.name}  [{entry.Kind}]",
                        EditorStyles.boldLabel);
                    if (GUILayout.Button("Select", GUILayout.Width(55f)))
                    {
                        Selection.activeObject = entry.Owner;
                        EditorGUIUtility.PingObject(entry.Owner);
                    }
                }

                EditorGUILayout.LabelField("Location", entry.Location);
                EditorGUILayout.LabelField("Hierarchy", entry.HierarchyPath);
                using (var data = new SerializedObject(entry.Target))
                {
                    data.Update();
                    SerializedProperty prompt = InteractionTargetAuthoring.DrawPrompt(data);
                    InteractionOutcomeEditor.DrawInteractionDetails(data);
                    if (data.ApplyModifiedProperties())
                    {
                        entry.Prompt = prompt.stringValue;
                        entry.Requirement = DescribeRequirement(data);
                    }
                }

                if (entry.CompletionFlags.Length > 0)
                {
                    EditorGUILayout.LabelField(
                        "Sets Flags",
                        string.Join(", ", entry.CompletionFlags));
                }

                if (entry.RequiresCrosshairTarget && !entry.HasCollider)
                {
                    EditorGUILayout.HelpBox(
                        "No 3D Collider exists on this target or an interaction-owned child. Trigger and solid Colliders are both supported.",
                        MessageType.Error);
                }

                if (entry.RequiresCrosshairTarget && !entry.HasHighlighter)
                {
                    EditorGUILayout.HelpBox(
                        "No InteractionHighlighter is present. This is optional but used by most project interactables.",
                        MessageType.Warning);
                }

                foreach (FlavorTextInteraction flavor in entry.Owner.GetComponents<FlavorTextInteraction>())
                {
                    using var data = new SerializedObject(flavor);
                    data.Update();
                    EditorGUILayout.PropertyField(data.FindProperty("flavorText"), new GUIContent("Flavor Text"));
                    data.ApplyModifiedProperties();
                }
            }
        }

        private void RefreshEntries()
        {
            entries.Clear();

            if (scope == ExplorerScope.OpenScenes ||
                scope == ExplorerScope.OpenScenesAndPrefabs)
            {
                ScanOpenScenes();
            }

            if (scope == ExplorerScope.ProjectPrefabs ||
                scope == ExplorerScope.OpenScenesAndPrefabs)
            {
                ScanProjectPrefabs();
            }

            entries.Sort((left, right) =>
            {
                int locationComparison = string.Compare(
                    left.Location,
                    right.Location,
                    StringComparison.OrdinalIgnoreCase);
                return locationComparison != 0
                    ? locationComparison
                    : string.Compare(
                        left.HierarchyPath,
                        right.HierarchyPath,
                        StringComparison.OrdinalIgnoreCase);
            });
            Repaint();
        }

        private void ScanOpenScenes()
        {
            for (int sceneIndex = 0;
                 sceneIndex < SceneManager.sceneCount;
                 sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    AddTargets(
                        root,
                        string.IsNullOrWhiteSpace(scene.path)
                            ? scene.name
                            : scene.path);
                }
            }
        }

        private void ScanProjectPrefabs()
        {
            string[] prefabGuids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets" });
            Array.Sort(prefabGuids, StringComparer.Ordinal);

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root != null)
                {
                    AddTargets(root, path);
                }
            }
        }

        private void AddTargets(GameObject root, string location)
        {
            foreach (Component target in InteractionTargetAuthoring.FindTargets(root))
                entries.Add(CreateEntry(target, location));
        }

        private static Entry CreateEntry(Component target, string location)
        {
            using var serializedTarget = new SerializedObject(target);
            InteractionTargetAuthoring targetType = InteractionTargetAuthoring.For(target);

            GameObject owner = target.gameObject;
            HoldActivitySequence activitySequence =
                target is HoldInteractable
                    ? owner.GetComponent<HoldActivitySequence>()
                    : null;
            bool requiresCrosshairTarget = activitySequence == null ||
                activitySequence.RequiresColliderFocus;
            return new Entry
            {
                Target = target,
                Owner = owner,
                Kind = targetType.Kind,
                Location = location,
                HierarchyPath = GetHierarchyPath(owner.transform),
                Prompt = ReadString(serializedTarget, targetType.PromptProperty, targetType.DefaultPrompt),
                Requirement = DescribeRequirement(serializedTarget),
                CompletionFlags = ReadStringArray(serializedTarget, targetType.CompletionFlags),
                RequiresCrosshairTarget = requiresCrosshairTarget,
                HasCollider =
                    InteractionTargetColliderUtility.HasRaycastCollider(target),
                HasHighlighter = owner.GetComponentInChildren<InteractionHighlighter>(true) != null,
                ConditionalMessages = owner.GetComponents<ConditionalInteractionMessage>()
            };
        }

        private bool MatchesSearch(Entry entry)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            return ContainsIgnoreCase(entry.Owner.name, search) ||
                   ContainsIgnoreCase(entry.Location, search) ||
                   ContainsIgnoreCase(entry.HierarchyPath, search) ||
                   ContainsIgnoreCase(entry.Prompt, search) ||
                   ContainsIgnoreCase(entry.Requirement, search) ||
                   entry.CompletionFlags.Any(flag =>
                       ContainsIgnoreCase(flag, search)) ||
                   entry.ConditionalMessages.Any(MatchesConditionalMessage) ||
                   entry.Owner.GetComponents<FlavorTextInteraction>().Any(flavor => ContainsIgnoreCase(flavor.FlavorText, search)) ||
                   MatchesEventText(entry.Target, search);
        }

        private bool MatchesConditionalMessage(ConditionalInteractionMessage message) =>
            message != null && (ContainsIgnoreCase(message.DefaultMessage, search) ||
                message.Rules.Any(rule => rule != null &&
                    (ContainsIgnoreCase(rule.Label, search) || ContainsIgnoreCase(rule.Message, search) ||
                     (rule.Requirement != null && (rule.Requirement.Flags.Any(flag => ContainsIgnoreCase(flag, search)) || rule.Requirement.Comparisons.Any(condition => condition != null && ContainsIgnoreCase(condition.flagId, search)))))));

        private static bool MatchesEventText(Component target, string query)
        {
            using var data = new SerializedObject(target);
            return InteractionOutcomeEditor.EventMessages(data)
                .Any(message => ContainsIgnoreCase(message.Text.stringValue, query));
        }

        private static bool HasIssue(Entry entry) =>
            entry.RequiresCrosshairTarget &&
            (!entry.HasCollider || !entry.HasHighlighter);

        private static string DescribeRequirement(SerializedObject serializedTarget)
        {
            SerializedProperty requirement =
                serializedTarget.FindProperty("requirement");
            if (requirement == null)
            {
                return "None";
            }

            SerializedProperty mode = requirement.FindPropertyRelative("mode");
            SerializedProperty flags = requirement.FindPropertyRelative("flags");
            if (mode == null || mode.enumValueIndex == 0)
            {
                return "None";
            }

            string[] ids = ReadStringArray(flags);
            return ids.Length == 0
                ? $"{mode.enumDisplayNames[mode.enumValueIndex]} (no flags)"
                : $"{mode.enumDisplayNames[mode.enumValueIndex]}: " +
                  string.Join(", ", ids);
        }

        private static string DescribeRequirement(FlagRequirement requirement)
        {
            if (requirement == null)
            {
                return "Missing requirement";
            }

            if (requirement.Mode == FlagRequirementMode.None && requirement.Comparisons.Count == 0)
            {
                return "None";
            }

            string[] flags = requirement.Flags
                .Where(flag => !string.IsNullOrWhiteSpace(flag))
                .Select(flag => flag.Trim())
                .ToArray();
            string typed = string.Join(", ", requirement.Comparisons.Where(condition => condition != null).Select(condition => condition.flagId + " " + condition.comparison));
            if (typed.Length > 0) return $"{requirement.Mode}: {string.Join(", ", flags)}; comparisons: {typed}";
            return flags.Length == 0
                ? $"{requirement.Mode} (no flags)"
                : $"{requirement.Mode}: {string.Join(", ", flags)}";
        }

        private static string ReadString(
            SerializedObject serializedObject,
            string propertyName,
            string fallback)
        {
            SerializedProperty property =
                serializedObject.FindProperty(propertyName);
            return property == null || string.IsNullOrWhiteSpace(property.stringValue)
                ? fallback
                : property.stringValue;
        }

        private static string[] ReadStringArray(
            SerializedObject serializedObject,
            string propertyName)
        {
            return ReadStringArray(serializedObject.FindProperty(propertyName));
        }

        private static string[] ReadStringArray(SerializedProperty property)
        {
            if (property == null || !property.isArray)
            {
                return Array.Empty<string>();
            }

            var values = new List<string>();
            for (int index = 0; index < property.arraySize; index++)
            {
                string value = property.GetArrayElementAtIndex(index).stringValue;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value.Trim());
                }
            }

            return values.ToArray();
        }

        private static string GetHierarchyPath(Transform target)
        {
            var names = new Stack<string>();
            Transform current = target;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        private static bool ContainsIgnoreCase(string value, string query)
        {
            return (value ?? string.Empty).IndexOf(
                query,
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

    }
}
