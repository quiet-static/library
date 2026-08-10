using System;
using System.Linq;
using QuietStatic.Toolkit.SceneFlow;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.SceneFlow
{
    /// <summary>Build Settings-backed selector for runtime scene references.</summary>
    [CustomPropertyDrawer(typeof(SceneReference))]
    public sealed class SceneReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            SerializedProperty sceneName =
                property.FindPropertyRelative("sceneName");
            string[] names = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => System.IO.Path.GetFileNameWithoutExtension(scene.path))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            string[] choices = new[] { "<None>" }.Concat(names).ToArray();
            int current = Array.IndexOf(names, sceneName.stringValue);
            int selected = EditorGUI.Popup(position, label.text, current + 1, choices);
            sceneName.stringValue = selected <= 0 ? string.Empty : names[selected - 1];
        }
    }
}
