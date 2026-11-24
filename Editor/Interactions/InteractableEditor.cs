using QuietStatic.Toolkit.Interactions;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Interactions
{
    /// <summary>
    /// Draws authoring-time guidance for interaction targets.
    /// </summary>
    [CustomEditor(typeof(Interactable))]
    [CanEditMultipleObjects]
    public sealed class InteractableEditor : UnityEditor.Editor
    {
        private const string InteractablesLayerName = "Interactables";
        private const string GrabbablePropertyName = "isGrabbable";

        public override void OnInspectorGUI()
        {
            DrawInteractionProperties();
            DrawLayerWarning();
            DrawColliderWarning();
            DrawGrabbableWarnings();
        }

        private void DrawInteractionProperties()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }

            DrawPropertiesExcluding(serializedObject, "m_Script", GrabbablePropertyName);

            // Add companion components through scene objects or editable Prefab Mode contents.
            bool editingPrefabAsset = System.Array.Exists(targets, EditorUtility.IsPersistent);
            bool grabbableChanged;
            using (var change = new EditorGUI.ChangeCheckScope())
            using (new EditorGUI.DisabledScope(editingPrefabAsset))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(GrabbablePropertyName));
                grabbableChanged = change.changed;
            }

            if (editingPrefabAsset)
            {
                EditorGUILayout.HelpBox("Open the prefab in Prefab Mode to change Is Grabbable.", MessageType.Info);
            }

            if (grabbableChanged)
            {
                ApplyGrabbableModification(serializedObject);
            }
            else
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        internal static void ApplyGrabbableModification(SerializedObject interactionProperties)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Change Grabbable Setting");
            interactionProperties.ApplyModifiedProperties();
            foreach (Object selectedTarget in interactionProperties.targetObjects)
            {
                SynchronizeGrabbable((Interactable)selectedTarget);
            }

            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(undoGroup);
        }

        internal static void SynchronizeGrabbable(Interactable interactable)
        {
            if (Application.IsPlaying(interactable))
            {
                // SerializedProperty writes bypass the runtime setter.
                interactable.IsGrabbable = interactable.IsGrabbable;
                return;
            }

            if (interactable.IsGrabbable && !EditorUtility.IsPersistent(interactable) &&
                !interactable.TryGetComponent<Grabbable>(out _))
            {
                Undo.AddComponent<Grabbable>(interactable.gameObject);
            }
        }

        private void DrawGrabbableWarnings()
        {
            foreach (Object selectedTarget in targets)
            {
                if (selectedTarget is not Interactable interactable || !interactable.IsGrabbable)
                {
                    continue;
                }

                if (interactable.DialogueAction != null)
                {
                    EditorGUILayout.HelpBox(
                        $"'{interactable.name}' uses grabbing while Is Grabbable is enabled. Turn it off to use the assigned Dialogue Action.",
                        MessageType.Warning);
                }

                if (!interactable.TryGetComponent(out Grabbable grabbable))
                {
                    EditorGUILayout.HelpBox($"'{interactable.name}' needs a Grabbable component.", MessageType.Warning);
                    if (!EditorUtility.IsPersistent(interactable) && GUILayout.Button("Add Grabbable"))
                    {
                        SynchronizeGrabbable(interactable);
                    }

                    continue;
                }

                if (!grabbable.IsHeld && (!interactable.TryGetComponent(out Rigidbody body) ||
                    body.isKinematic || !body.detectCollisions))
                {
                    EditorGUILayout.HelpBox(
                        $"'{interactable.name}' needs a Rigidbody on the same GameObject with Is Kinematic off and collision detection enabled.",
                        MessageType.Warning);
                }
            }
        }

        private void DrawLayerWarning()
        {
            int interactablesLayer = LayerMask.NameToLayer(InteractablesLayerName);
            if (interactablesLayer < 0)
            {
                EditorGUILayout.HelpBox(
                    $"The '{InteractablesLayerName}' layer does not exist. Add it in Project Settings > Tags and Layers, then include it in the player's Interactor mask.",
                    MessageType.Warning);
                return;
            }

            foreach (Object selectedTarget in targets)
            {
                if (selectedTarget is Interactable interactable &&
                    interactable.gameObject.layer != interactablesLayer)
                {
                    EditorGUILayout.HelpBox(
                        $"'{interactable.gameObject.name}' is not on the '{InteractablesLayerName}' layer, so an Interactor configured for that layer will not detect it. Set this GameObject and any collider children used for interaction to '{InteractablesLayerName}'.",
                        MessageType.Warning);
                    return;
                }
            }
        }

        private void DrawColliderWarning()
        {
            foreach (Object selectedTarget in targets)
            {
                if (selectedTarget is not Interactable interactable ||
                    InteractionTargetColliderUtility.HasRaycastCollider(interactable))
                {
                    continue;
                }

                EditorGUILayout.HelpBox(
                    $"'{interactable.gameObject.name}' has no 3D Collider on this GameObject or an interaction-owned child, so the player's Interactor raycast cannot detect it. Add a Collider here or to a child; trigger and solid Colliders are both supported.",
                    MessageType.Warning);
                return;
            }
        }
    }
}
