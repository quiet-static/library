using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Cinematics;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Cinematics
{
    /// <summary>Draws stable cinematic shot IDs with an edit-mode preview action.</summary>
    [CustomPropertyDrawer(typeof(CinematicShotIdAttribute))]
    public sealed class CinematicShotIdDrawer : PropertyDrawer
    {
        private const float MoveButtonWidth = 54f;
        private const float ButtonSpacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(position, "CinematicShotId supports only string fields.", MessageType.Error);
                return;
            }

            var settings = (CinematicShotIdAttribute)attribute;
            SerializedProperty directorProperty = FindSibling(property, settings.DirectorFieldName);
            var director = directorProperty?.objectReferenceValue as CinematicCutsceneCameraDirector;
            bool mixedDirector = directorProperty != null && directorProperty.hasMultipleDifferentValues;

            EditorGUI.BeginProperty(position, label, property);
            Rect content = EditorGUI.PrefixLabel(position, label);
            Rect popup = new(content.x, content.y,
                Mathf.Max(0f, content.width - MoveButtonWidth - ButtonSpacing), content.height);
            Rect move = new(popup.xMax + ButtonSpacing, content.y, MoveButtonWidth, content.height);

            if (director == null || mixedDirector)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.TextField(popup, mixedDirector ? "—" : "<Assign Camera Director>");
                }
            }
            else
            {
                DrawDropdown(popup, property, director);
            }

            string selectedId = property.stringValue?.Trim() ?? string.Empty;
            bool canMove = !property.hasMultipleDifferentValues && !mixedDirector && director != null &&
                           director.TryGetShotIndex(selectedId, out int index) && director.IsShotUsable(index);
            using (new EditorGUI.DisabledScope(!canMove))
            {
                if (GUI.Button(move, new GUIContent("Move", "Move the camera to this shot now.")))
                {
                    property.serializedObject.ApplyModifiedProperties();
                    CinematicShotPreviewUtility.MoveCameraToShot(director, selectedId);
                }
            }

            EditorGUI.EndProperty();
        }

        private static void DrawDropdown(Rect position, SerializedProperty property,
            CinematicCutsceneCameraDirector director)
        {
            var ids = new List<string> { string.Empty };
            var labels = new List<GUIContent> { new("<None>") };
            for (int index = 0; index < director.ShotCount; index++)
            {
                string id = director.GetExplicitShotId(index);
                if (string.IsNullOrEmpty(id)) continue;
                string name = director.GetShotDisplayName(index);
                ids.Add(id);
                labels.Add(new GUIContent(string.Equals(name, id, StringComparison.Ordinal)
                    ? name
                    : $"{name} — {id}"));
            }

            string selectedId = property.stringValue?.Trim() ?? string.Empty;
            int selected = ids.FindIndex(id => string.Equals(id, selectedId, StringComparison.Ordinal));
            if (selected < 0 && !string.IsNullOrEmpty(selectedId))
            {
                ids.Add(selectedId);
                labels.Add(new GUIContent($"<Missing> {selectedId}"));
                selected = ids.Count - 1;
            }

            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int next = EditorGUI.Popup(position, Mathf.Max(0, selected), labels.ToArray());
            if (EditorGUI.EndChangeCheck()) property.stringValue = ids[next];
            EditorGUI.showMixedValue = false;
        }

        /// <summary>Finds a field beside another serialized field, including nested array data.</summary>
        public static SerializedProperty FindSibling(SerializedProperty property, string siblingFieldName)
        {
            if (string.IsNullOrWhiteSpace(siblingFieldName)) return null;
            string path = property.propertyPath;
            int separator = path.LastIndexOf('.');
            string siblingPath = separator >= 0
                ? path.Substring(0, separator + 1) + siblingFieldName
                : siblingFieldName;
            return property.serializedObject.FindProperty(siblingPath);
        }
    }
}
