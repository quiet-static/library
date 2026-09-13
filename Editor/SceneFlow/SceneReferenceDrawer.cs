using System;
using System.Collections.Generic;
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
        private readonly Func<EditorBuildSettingsScene[]> getBuildSettingsScenes;
        private readonly Func<Rect, GUIContent, int, string[], int> drawPopup;

        /// <summary>Creates a drawer backed by the project's Build Settings.</summary>
        public SceneReferenceDrawer()
            : this(
                () => EditorBuildSettings.scenes,
                (position, label, selectedIndex, displayedOptions) =>
                    EditorGUI.Popup(
                        position,
                        label.text,
                        selectedIndex,
                        displayedOptions))
        {
        }

        internal SceneReferenceDrawer(
            Func<EditorBuildSettingsScene[]> getBuildSettingsScenes,
            Func<Rect, GUIContent, int, string[], int> drawPopup)
        {
            this.getBuildSettingsScenes = getBuildSettingsScenes
                ?? throw new ArgumentNullException(nameof(getBuildSettingsScenes));
            this.drawPopup = drawPopup
                ?? throw new ArgumentNullException(nameof(drawPopup));
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            SerializedProperty sceneName =
                property.FindPropertyRelative("sceneName");
            string[] names = getBuildSettingsScenes()
                .Where(scene => scene.enabled)
                .Select(scene => System.IO.Path.GetFileNameWithoutExtension(scene.path))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            int current = Array.IndexOf(names, sceneName.stringValue);
            List<string> choices = new() { "<None>" };
            List<string> values = new() { string.Empty };
            int selectedIndex;

            if (current >= 0)
            {
                choices.AddRange(names);
                values.AddRange(names);
                selectedIndex = current + 1;
            }
            else if (!string.IsNullOrEmpty(sceneName.stringValue))
            {
                choices.Add($"<Missing/disabled: {sceneName.stringValue}>");
                values.Add(sceneName.stringValue);
                choices.AddRange(names);
                values.AddRange(names);
                selectedIndex = 1;
            }
            else
            {
                choices.AddRange(names);
                values.AddRange(names);
                selectedIndex = 0;
            }

            EditorGUI.BeginChangeCheck();
            int selected = drawPopup(
                position,
                label,
                selectedIndex,
                choices.ToArray());
            if (EditorGUI.EndChangeCheck()
                && selected >= 0
                && selected < values.Count)
            {
                sceneName.stringValue = values[selected];
            }
        }
    }
}
