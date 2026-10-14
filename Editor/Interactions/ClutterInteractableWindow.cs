using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietStatic.Toolkit.Editor.Interactions
{
    /// <summary>Finds and authors lightweight flavor-text interactions in loaded scenes.</summary>
    public sealed class ClutterInteractableWindow : EditorWindow
    {
        private readonly List<FlavorTextInteraction> clutter = new();
        private readonly List<Interactable> otherInteractions = new();
        private Vector2 scroll;
        private string search = string.Empty;

        public static void Open() => GetWindow<ClutterInteractableWindow>("Clutter Interactables");

        private void OnEnable() => Refresh();

        private void OnHierarchyChange() => Refresh();

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Clutter interactables show optional flavor text through the interaction UI. They do not use dialogue trees, flags, or story progression.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                search = GUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField"));
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(65f))) Refresh();
            }

            using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0))
            {
                if (GUILayout.Button("Set Up Selected Objects as Clutter Interactables"))
                {
                    foreach (GameObject selected in Selection.gameObjects) SetUp(selected);
                    Refresh();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Flavor Interactables ({clutter.Count})", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (FlavorTextInteraction item in clutter.Where(Matches)) DrawFlavorItem(item);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Other Interaction Targets ({otherInteractions.Count})", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "These may drive story, objectives, doors, or other gameplay. They are listed for detection only and are never changed automatically.",
                MessageType.None);
            foreach (Interactable item in otherInteractions.Where(Matches)) DrawOtherItem(item);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawFlavorItem(FlavorTextInteraction item)
        {
            if (item == null) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(item.name, EditorStyles.boldLabel);
                    if (GUILayout.Button("Select", GUILayout.Width(55f))) Selection.activeGameObject = item.gameObject;
                    if (GUILayout.Button("Repair", GUILayout.Width(55f))) SetUp(item.gameObject);
                }

                var interactionObject = new SerializedObject(item.GetComponent<Interactable>());
                var flavorObject = new SerializedObject(item);
                interactionObject.Update();
                flavorObject.Update();
                EditorGUILayout.PropertyField(interactionObject.FindProperty("displayName"), new GUIContent("Prompt"));
                EditorGUILayout.PropertyField(flavorObject.FindProperty("flavorText"), new GUIContent("Flavor Text"));
                EditorGUILayout.PropertyField(flavorObject.FindProperty("channel"));
                SerializedProperty custom = flavorObject.FindProperty("useCustomDuration");
                EditorGUILayout.PropertyField(custom);
                if (custom.boolValue) EditorGUILayout.PropertyField(flavorObject.FindProperty("messageDuration"));
                if (interactionObject.ApplyModifiedProperties() | flavorObject.ApplyModifiedProperties())
                    EditorSceneManager.MarkSceneDirty(item.gameObject.scene);
            }
        }

        private static void DrawOtherItem(Interactable item)
        {
            if (item == null) return;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{item.name} — {item.DisplayName}");
                if (GUILayout.Button("Select", GUILayout.Width(55f))) Selection.activeGameObject = item.gameObject;
                if (GUILayout.Button("Convert", GUILayout.Width(60f))) SetUp(item.gameObject);
            }
        }

        private bool Matches(Component item) => string.IsNullOrWhiteSpace(search) ||
            item.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
            GetPath(item.transform).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private void Refresh()
        {
            clutter.Clear();
            otherInteractions.Clear();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Interactable target in root.GetComponentsInChildren<Interactable>(true))
                {
                    FlavorTextInteraction flavor = target.GetComponent<FlavorTextInteraction>();
                    if (flavor != null) clutter.Add(flavor);
                    else otherInteractions.Add(target);
                }
            }
            Repaint();
        }

        internal static FlavorTextInteraction SetUp(GameObject target)
        {
            if (target == null || EditorUtility.IsPersistent(target)) return null;
            Undo.RegisterCompleteObjectUndo(target, "Set Up Clutter Interactable");
            Interactable interaction = target.GetComponent<Interactable>() ?? Undo.AddComponent<Interactable>(target);
            FlavorTextInteraction flavor = target.GetComponent<FlavorTextInteraction>() ?? Undo.AddComponent<FlavorTextInteraction>(target);
            if (target.GetComponentInChildren<InteractionHighlighter>(true) == null) Undo.AddComponent<InteractionHighlighter>(target);
            if (!InteractionTargetColliderUtility.HasRaycastCollider(interaction)) AddBoundsCollider(target);
            int layer = LayerMask.NameToLayer("Interactables");
            if (layer >= 0) target.layer = layer;
            AssignChannel(flavor);
            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkSceneDirty(target.scene);
            return flavor;
        }

        private static void AddBoundsCollider(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            BoxCollider collider = Undo.AddComponent<BoxCollider>(target);
            collider.isTrigger = true;
            collider.center = target.transform.InverseTransformPoint(bounds.center);
            Vector3 scale = target.transform.lossyScale;
            collider.size = new Vector3(
                bounds.size.x / Mathf.Max(Mathf.Abs(scale.x), .0001f),
                bounds.size.y / Mathf.Max(Mathf.Abs(scale.y), .0001f),
                bounds.size.z / Mathf.Max(Mathf.Abs(scale.z), .0001f));
        }

        private static void AssignChannel(FlavorTextInteraction flavor)
        {
            var serialized = new SerializedObject(flavor);
            SerializedProperty channel = serialized.FindProperty("channel");
            if (channel.objectReferenceValue != null) return;
            string guid = AssetDatabase.FindAssets("t:InteractionUIChannel").FirstOrDefault();
            if (string.IsNullOrEmpty(guid)) return;
            channel.objectReferenceValue = AssetDatabase.LoadAssetAtPath<InteractionUIChannel>(AssetDatabase.GUIDToAssetPath(guid));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string GetPath(Transform target) => target.parent == null
            ? target.name
            : GetPath(target.parent) + "/" + target.name;
    }
}
