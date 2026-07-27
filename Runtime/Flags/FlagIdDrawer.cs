#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Flags.Editor
{
    /// <summary>
    /// Draws strings marked with FlagIdAttribute as dropdowns populated from
    /// the first FlagDatabase asset found in the project.
    /// Supports both a single string and string arrays.
    /// </summary>
    [CustomPropertyDrawer(typeof(FlagIdAttribute))]
    public sealed class FlagIdDrawer : PropertyDrawer
    {
        private const string NoneOption = "<None>";
        private const string MissingPrefix = "<Missing> ";

        private static FlagDatabase cachedDatabase;
        private static string[] cachedIds;
        private static Dictionary<string, string> cachedDescriptions;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isArray || property.propertyType == SerializedPropertyType.String)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            int visibleLines = 2 + property.arraySize;
            return visibleLines * EditorGUIUtility.singleLineHeight
                   + (visibleLines - 1) * EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EnsureCache();

            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                DrawArray(position, property, label);
                return;
            }

            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(position, "FlagId supports only strings and string arrays.", MessageType.Error);
                return;
            }

            DrawStringDropdown(position, property, label);
        }

        private static void DrawArray(Rect position, SerializedProperty property, GUIContent label)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            Rect line = new Rect(position.x, position.y, position.width, lineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);

            if (!property.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;

            line.y += lineHeight + spacing;
            int newSize = Mathf.Max(0, EditorGUI.IntField(line, "Size", property.arraySize));
            if (newSize != property.arraySize)
            {
                property.arraySize = newSize;
            }

            for (int i = 0; i < property.arraySize; i++)
            {
                line.y += lineHeight + spacing;
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                DrawStringDropdown(line, element, new GUIContent($"Element {i}"));
            }

            EditorGUI.indentLevel--;
        }

        private static void DrawStringDropdown(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            if (cachedDatabase == null || cachedIds.Length == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            string currentValue = property.stringValue;
            bool valueExists = string.IsNullOrEmpty(currentValue) || cachedIds.Contains(currentValue);

            GUIContent[] options;
            int selectedIndex;

            GUIContent[] databaseOptions = cachedIds
                .Select(id => new GUIContent(id, GetDescription(id)))
                .ToArray();

            if (valueExists)
            {
                options = new[] { new GUIContent(NoneOption, "No flag selected.") }
                    .Concat(databaseOptions)
                    .ToArray();
                selectedIndex = string.IsNullOrEmpty(currentValue)
                    ? 0
                    : Array.IndexOf(cachedIds, currentValue) + 1;
            }
            else
            {
                options = new[]
                    {
                        new GUIContent(NoneOption, "No flag selected."),
                        new GUIContent(MissingPrefix + currentValue, "This flag is not present in the active Flag Database.")
                    }
                    .Concat(databaseOptions)
                    .ToArray();
                selectedIndex = 1;
            }

            GUIContent tooltipLabel = new GUIContent(label);
            if (!string.IsNullOrEmpty(currentValue))
            {
                string description = GetDescription(currentValue);
                tooltipLabel.tooltip = string.IsNullOrWhiteSpace(description)
                    ? $"Flag: {currentValue}"
                    : description;
            }

            EditorGUI.BeginProperty(position, tooltipLabel, property);
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(position, tooltipLabel, selectedIndex, options);

            if (EditorGUI.EndChangeCheck())
            {
                if (newIndex == 0)
                {
                    property.stringValue = string.Empty;
                }
                else if (!valueExists && newIndex == 1)
                {
                    // Keep the legacy/missing value until another option is chosen.
                }
                else
                {
                    int idIndex = valueExists ? newIndex - 1 : newIndex - 2;
                    property.stringValue = cachedIds[idIndex];
                }
            }

            EditorGUI.EndProperty();
        }

        private static void EnsureCache()
        {
            if (cachedDatabase != null && cachedIds != null && cachedDescriptions != null)
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:FlagDatabase");
            if (guids.Length == 0)
            {
                cachedDatabase = null;
                cachedIds = Array.Empty<string>();
                cachedDescriptions = new Dictionary<string, string>();
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            cachedDatabase = AssetDatabase.LoadAssetAtPath<FlagDatabase>(path);
            if (cachedDatabase == null)
            {
                cachedIds = Array.Empty<string>();
                cachedDescriptions = new Dictionary<string, string>();
                return;
            }

            var validDefinitions = cachedDatabase.Flags
                .Where(definition => definition != null)
                .Where(definition => !string.IsNullOrWhiteSpace(definition.id))
                .GroupBy(definition => definition.id.Trim())
                .Select(group => group.First())
                .OrderBy(definition => definition.id.Trim())
                .ToArray();

            cachedIds = validDefinitions
                .Select(definition => definition.id.Trim())
                .ToArray();

            cachedDescriptions = validDefinitions.ToDictionary(
                definition => definition.id.Trim(),
                definition => definition.description?.Trim() ?? string.Empty);
        }

        private static string GetDescription(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || cachedDescriptions == null)
            {
                return string.Empty;
            }

            return cachedDescriptions.TryGetValue(id, out string description)
                ? description
                : string.Empty;
        }

        public static void ClearCache()
        {
            cachedDatabase = null;
            cachedIds = null;
            cachedDescriptions = null;
        }
    }
}
#endif
