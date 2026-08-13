using System;
using System.Collections.Generic;
using QuietStatic.Toolkit.Cinematics;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Cinematics
{
    /// <summary>
    /// Draws cinematic shot IDs as director-backed dropdowns with an edit-mode move action.
    /// </summary>
    [CustomPropertyDrawer(typeof(CinematicShotIdAttribute))]
    public sealed class CinematicShotIdDrawer : PropertyDrawer
    {
        private const float MoveButtonWidth = 54f;
        private const float MigrateButtonWidth = 62f;
        private const float ButtonSpacing = 4f;

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(
                    position,
                    "CinematicShotId supports only string fields.",
                    MessageType.Error);
                return;
            }

            var settings = (CinematicShotIdAttribute)attribute;
            SerializedProperty directorProperty = FindSibling(
                property,
                settings.DirectorFieldName);
            SerializedProperty legacyIndexProperty = FindSibling(
                property,
                settings.LegacyIndexFieldName);
            var director = directorProperty?.objectReferenceValue
                as CinematicCutsceneCameraDirector;
            bool hasLegacyReference = HasLegacyReference(
                property,
                legacyIndexProperty);
            int legacyIndex = hasLegacyReference
                ? legacyIndexProperty.intValue
                : -1;
            string selectedId = GetSelectedId(
                property,
                legacyIndexProperty,
                director);
            bool mixedDirector = directorProperty != null &&
                                 directorProperty.hasMultipleDifferentValues;

            EditorGUI.BeginProperty(position, label, property);
            Rect contentRect = EditorGUI.PrefixLabel(position, label);
            float buttonWidth = MoveButtonWidth;
            if (hasLegacyReference)
            {
                buttonWidth += ButtonSpacing + MigrateButtonWidth;
            }
            Rect popupRect = new(
                contentRect.x,
                contentRect.y,
                Mathf.Max(0f, contentRect.width - buttonWidth - ButtonSpacing),
                contentRect.height);
            Rect migrateRect = new(
                popupRect.xMax + ButtonSpacing,
                contentRect.y,
                MigrateButtonWidth,
                contentRect.height);
            Rect moveRect = new(
                hasLegacyReference
                    ? migrateRect.xMax + ButtonSpacing
                    : popupRect.xMax + ButtonSpacing,
                contentRect.y,
                MoveButtonWidth,
                contentRect.height);

            if (director == null || mixedDirector)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.TextField(
                        popupRect,
                        mixedDirector ? "—" : "<Assign Camera Director>");
                }
            }
            else
            {
                DrawDropdown(
                    popupRect,
                    property,
                    legacyIndexProperty,
                    director,
                    selectedId,
                    hasLegacyReference,
                    legacyIndex);
            }

            if (hasLegacyReference)
            {
                bool canMigrate = !property.hasMultipleDifferentValues &&
                                  !mixedDirector &&
                                  director != null &&
                                  !string.IsNullOrEmpty(selectedId) &&
                                  director.TryGetShotIndex(
                                      selectedId,
                                      out int resolvedIndex) &&
                                  resolvedIndex == legacyIndex;
                using (new EditorGUI.DisabledScope(!canMigrate))
                {
                    GUIContent migrateLabel = new(
                        "Migrate",
                        "Replace the legacy list index with this shot's stable ID.");
                    if (GUI.Button(migrateRect, migrateLabel))
                    {
                        Undo.RecordObjects(
                            property.serializedObject.targetObjects,
                            "Migrate Cinematic Shot Reference");
                        property.stringValue = selectedId;
                        legacyIndexProperty.intValue = -1;
                        property.serializedObject.ApplyModifiedProperties();
                    }
                }
            }

            bool canMove = !property.hasMultipleDifferentValues &&
                           !mixedDirector &&
                           director != null &&
                           (hasLegacyReference
                               ? director.IsShotUsable(legacyIndex)
                               : director.TryGetShotIndex(
                                     selectedId,
                                     out int selectedIndex) &&
                                 director.IsShotUsable(selectedIndex));
            using (new EditorGUI.DisabledScope(!canMove))
            {
                GUIContent moveLabel = new(
                    "Move",
                    "Move the cutscene camera to this shot now. In Edit Mode this is undoable.");
                if (GUI.Button(moveRect, moveLabel))
                {
                    property.serializedObject.ApplyModifiedProperties();
                    if (hasLegacyReference)
                    {
                        CinematicShotPreviewUtility.MoveCameraToShot(
                            director,
                            legacyIndex);
                    }
                    else
                    {
                        CinematicShotPreviewUtility.MoveCameraToShot(
                            director,
                            selectedId);
                    }
                }
            }

            EditorGUI.EndProperty();
        }

        private static void DrawDropdown(
            Rect position,
            SerializedProperty property,
            SerializedProperty legacyIndexProperty,
            CinematicCutsceneCameraDirector director,
            string selectedId,
            bool hasLegacyReference,
            int legacyIndex)
        {
            var ids = new List<string> { string.Empty };
            var labels = new List<GUIContent>
            {
                new("<None>", "Leave the camera on its current shot."),
            };

            for (int index = 0; index < director.ShotCount; index++)
            {
                string id = director.GetExplicitShotId(index);
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                string name = director.GetShotDisplayName(index);
                string display = string.Equals(name, id, StringComparison.Ordinal)
                    ? name
                    : $"{name} — {id}";
                ids.Add(id);
                labels.Add(new GUIContent(display, $"Camera shot ID: {id}"));
            }

            int selectedIndex;
            if (hasLegacyReference)
            {
                selectedIndex = ids.FindIndex(
                    id => string.Equals(id, selectedId, StringComparison.Ordinal));
                if (selectedIndex > 0)
                {
                    labels[selectedIndex] = new GUIContent(
                        $"<Legacy index {legacyIndex}> {labels[selectedIndex].text}",
                        "This cue still runs by list index. Click Migrate to store the stable Shot ID.");
                }
                else
                {
                    string legacyName = legacyIndex >= 0 &&
                                        legacyIndex < director.ShotCount
                        ? director.GetShotDisplayName(legacyIndex)
                        : "<Missing shot>";
                    ids.Add(string.Empty);
                    labels.Add(new GUIContent(
                        $"<Legacy index {legacyIndex}> {legacyName} — assign Shot ID",
                        "Assign an explicit Shot ID on this director before migrating the reference."));
                    selectedIndex = ids.Count - 1;
                }
            }
            else
            {
                selectedIndex = ids.FindIndex(
                    id => string.Equals(id, selectedId, StringComparison.Ordinal));
                if (selectedIndex < 0 && !string.IsNullOrWhiteSpace(selectedId))
                {
                    ids.Add(selectedId);
                    labels.Add(new GUIContent(
                        $"<Missing> {selectedId}",
                        "The assigned director does not contain this shot ID."));
                    selectedIndex = ids.Count - 1;
                }
            }

            selectedIndex = Mathf.Max(0, selectedIndex);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUI.Popup(
                position,
                selectedIndex,
                labels.ToArray());
                if (EditorGUI.EndChangeCheck())
                {
                    property.stringValue = ids[nextIndex];
                if (legacyIndexProperty != null &&
                    legacyIndexProperty.propertyType == SerializedPropertyType.Integer)
                    {
                        legacyIndexProperty.intValue = -1;
                    }
                    selectedId = property.stringValue;
                    hasLegacyReference = false;
                    legacyIndex = -1;
                }
            EditorGUI.showMixedValue = false;
        }

        private static string GetSelectedId(
            SerializedProperty property,
            SerializedProperty legacyIndexProperty,
            CinematicCutsceneCameraDirector director)
        {
            if (!string.IsNullOrWhiteSpace(property.stringValue))
            {
                return property.stringValue.Trim();
            }

            if (director != null &&
                legacyIndexProperty != null &&
                legacyIndexProperty.propertyType == SerializedPropertyType.Integer &&
                legacyIndexProperty.intValue >= 0)
            {
                return director.GetExplicitShotId(
                    legacyIndexProperty.intValue);
            }

            return string.Empty;
        }

        private static bool HasLegacyReference(
            SerializedProperty property,
            SerializedProperty legacyIndexProperty)
        {
            return !property.hasMultipleDifferentValues &&
                   string.IsNullOrWhiteSpace(property.stringValue) &&
                   legacyIndexProperty != null &&
                   !legacyIndexProperty.hasMultipleDifferentValues &&
                   legacyIndexProperty.propertyType == SerializedPropertyType.Integer &&
                   legacyIndexProperty.intValue >= 0;
        }

        /// <summary>Finds a field beside another serialized field, including nested array data.</summary>
        public static SerializedProperty FindSibling(
            SerializedProperty property,
            string siblingFieldName)
        {
            if (string.IsNullOrWhiteSpace(siblingFieldName))
            {
                return null;
            }

            string path = property.propertyPath;
            int separator = path.LastIndexOf('.');
            string siblingPath = separator >= 0
                ? path.Substring(0, separator + 1) + siblingFieldName
                : siblingFieldName;
            return property.serializedObject.FindProperty(siblingPath);
        }
    }
}
