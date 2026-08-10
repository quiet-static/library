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
    }
}
