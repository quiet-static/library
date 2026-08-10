using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace QuietStatic.Toolkit.Flags.Editor
{
    /// <summary>
    /// Draws strings marked with <see cref="FlagIdAttribute"/> as searchable,
    /// database-backed selectors while preserving the serialized string value.
    /// </summary>
    [CustomPropertyDrawer(typeof(FlagIdAttribute))]
    public sealed class FlagIdDrawer : PropertyDrawer
    {
        private const string MissingPrefix = "<Missing> ";

        private static FlagDatabase cachedDatabase;
        private static string[] cachedIds;
        private static Dictionary<string, string> cachedDescriptions;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isArray || property.propertyType == SerializedPropertyType.String)
            {
                EnsureCache();
                string value = property.propertyType == SerializedPropertyType.String
                    ? property.stringValue
                    : string.Empty;
                bool missing = !string.IsNullOrWhiteSpace(value) &&
                               (cachedIds == null || !cachedIds.Contains(value));
                bool hasDescription = !string.IsNullOrWhiteSpace(GetDescription(value));
                return missing || hasDescription
                    ? EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing
                    : EditorGUIUtility.singleLineHeight;
            }

            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            int visibleLines = 2 + property.arraySize * 2;
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

            GUIContent tooltipLabel = new GUIContent(label);
            if (!string.IsNullOrEmpty(currentValue))
            {
                string description = GetDescription(currentValue);
                tooltipLabel.tooltip = string.IsNullOrWhiteSpace(description)
                    ? $"Flag: {currentValue}"
                    : description;
            }

            EditorGUI.BeginProperty(position, tooltipLabel, property);
            Rect fieldRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect buttonRect = EditorGUI.PrefixLabel(fieldRect, tooltipLabel);
            string display = property.hasMultipleDifferentValues
                ? "—"
                : string.IsNullOrEmpty(currentValue)
                    ? "<None>"
                    : valueExists ? currentValue : MissingPrefix + currentValue;

            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(display), FocusType.Keyboard))
            {
                var dropdown = new FlagDropdown(
                    new AdvancedDropdownState(),
                    cachedIds,
                    cachedDescriptions,
                    selected => ApplySelection(property, selected));
                dropdown.Show(buttonRect);
            }

            Rect messageRect = fieldRect;
            messageRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            if (!valueExists)
            {
                EditorGUI.HelpBox(messageRect, $"Unknown flag ID: {currentValue}", MessageType.Warning);
            }
            else
            {
                string description = GetDescription(currentValue);
                if (!string.IsNullOrWhiteSpace(description))
                {
                    EditorGUI.LabelField(messageRect, description, EditorStyles.miniLabel);
                }
            }
            EditorGUI.EndProperty();
        }

        private static void ApplySelection(SerializedProperty property, string selected)
        {
            property.serializedObject.Update();
            property.stringValue = selected;
            property.serializedObject.ApplyModifiedProperties();
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

            Array.Sort(guids, StringComparer.Ordinal);
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

        private sealed class FlagDropdown : AdvancedDropdown
        {
            private readonly string[] ids;
            private readonly Dictionary<string, string> descriptions;
            private readonly Action<string> onSelected;

            public FlagDropdown(
                AdvancedDropdownState state,
                string[] ids,
                Dictionary<string, string> descriptions,
                Action<string> onSelected) : base(state)
            {
                this.ids = ids;
                this.descriptions = descriptions;
                this.onSelected = onSelected;
                minimumSize = new Vector2(320f, 280f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Select Flag");
                root.AddChild(new FlagItem("<None>", string.Empty, "Clear the selected flag."));
                foreach (string id in ids)
                {
                    descriptions.TryGetValue(id, out string description);
                    root.AddChild(new FlagItem(id, id, description));
                }
                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is FlagItem flag)
                {
                    onSelected(flag.Id);
                }
            }

            private sealed class FlagItem : AdvancedDropdownItem
            {
                public FlagItem(string name, string id, string tooltip) : base(
                    string.IsNullOrWhiteSpace(tooltip) ? name : $"{name} — {tooltip}")
                {
                    Id = id;
                }

                public string Id { get; }
            }
        }
    }
}
