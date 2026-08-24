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

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            DrawLayerWarning();
            DrawColliderWarning();
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
