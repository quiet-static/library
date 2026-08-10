using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Core;
using QuietStatic.Toolkit.State;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.State
{
    /// <summary>Draws game-state strings as searchable database-backed selectors.</summary>
    [CustomPropertyDrawer(typeof(GameStateIdAttribute))]
    public sealed class GameStateIdDrawer : PropertyDrawer
    {
        private const string MissingPrefix = "<Missing> ";
        private static GameStateDatabase cachedDatabase;
        private static string[] cachedIds;
        private static Dictionary<string, string> cachedDescriptions;

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            if (property.isArray &&
                property.propertyType != SerializedPropertyType.String)
            {
                if (!property.isExpanded)
                {
                    return EditorGUIUtility.singleLineHeight;
                }

                int lines = 2 + property.arraySize * 2;
                return lines * EditorGUIUtility.singleLineHeight +
                       (lines - 1) * EditorGUIUtility.standardVerticalSpacing;
            }

            EnsureCache();
            string value = property.propertyType == SerializedPropertyType.String
                ? property.stringValue
                : string.Empty;
            bool missing = !string.IsNullOrWhiteSpace(value) &&
                           (cachedIds == null || !cachedIds.Contains(value));
            bool described = !string.IsNullOrWhiteSpace(GetDescription(value));
            return missing || described
                ? EditorGUIUtility.singleLineHeight * 2f +
                  EditorGUIUtility.standardVerticalSpacing
                : EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            EnsureCache();

            if (property.isArray &&
                property.propertyType != SerializedPropertyType.String)
            {
                DrawArray(position, property, label);
                return;
            }

            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(
                    position,
                    "GameStateId supports only strings and string arrays.",
                    MessageType.Error);
                return;
            }

            DrawStringDropdown(position, property, label);
        }

        private static void DrawArray(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Rect line = new Rect(position.x, position.y, position.width, height);
            property.isExpanded =
                EditorGUI.Foldout(line, property.isExpanded, label, true);

            if (!property.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            line.y += height + spacing;
            int size = Mathf.Max(
                0,
                EditorGUI.IntField(line, "Size", property.arraySize));
            if (size != property.arraySize)
            {
                property.arraySize = size;
            }

            for (int index = 0; index < property.arraySize; index++)
            {
                line.y += height + spacing;
                DrawStringDropdown(
                    line,
                    property.GetArrayElementAtIndex(index),
                    new GUIContent($"Element {index}"));
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

            string value = property.stringValue;
            bool exists = string.IsNullOrEmpty(value) || cachedIds.Contains(value);
            var tooltipLabel = new GUIContent(label)
            {
                tooltip = string.IsNullOrEmpty(value)
                    ? label.tooltip
                    : GetDescription(value)
            };

            EditorGUI.BeginProperty(position, tooltipLabel, property);
            Rect fieldRect = new Rect(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);
            Rect buttonRect = EditorGUI.PrefixLabel(fieldRect, tooltipLabel);
            string display = property.hasMultipleDifferentValues
                ? "—"
                : string.IsNullOrEmpty(value)
                    ? "<None>"
                    : exists ? value : MissingPrefix + value;

            if (EditorGUI.DropdownButton(
                    buttonRect,
                    new GUIContent(display),
                    FocusType.Keyboard))
            {
                var dropdown = new StateDropdown(
                    new AdvancedDropdownState(),
                    cachedIds,
                    cachedDescriptions,
                    selected => ApplySelection(property, selected));
                dropdown.Show(buttonRect);
            }

            Rect messageRect = fieldRect;
            messageRect.y +=
                EditorGUIUtility.singleLineHeight +
                EditorGUIUtility.standardVerticalSpacing;
            if (!exists)
            {
                EditorGUI.HelpBox(
                    messageRect,
                    $"Unknown game-state ID: {value}",
                    MessageType.Warning);
            }
            else
            {
                string description = GetDescription(value);
                if (!string.IsNullOrWhiteSpace(description))
                {
                    EditorGUI.LabelField(
                        messageRect,
                        description,
                        EditorStyles.miniLabel);
                }
            }

            EditorGUI.EndProperty();
        }

        private static void ApplySelection(
            SerializedProperty property,
            string selected)
        {
            property.serializedObject.Update();
            property.stringValue = selected;
            property.serializedObject.ApplyModifiedProperties();
        }

        private static void EnsureCache()
        {
            if (cachedDatabase != null &&
                cachedIds != null &&
                cachedDescriptions != null)
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:GameStateDatabase");
            if (guids.Length == 0)
            {
                cachedDatabase = null;
                cachedIds = Array.Empty<string>();
                cachedDescriptions = new Dictionary<string, string>();
                return;
            }

            Array.Sort(guids, StringComparer.Ordinal);
            cachedDatabase = AssetDatabase.LoadAssetAtPath<GameStateDatabase>(
                AssetDatabase.GUIDToAssetPath(guids[0]));

            GameStateDatabase.StateDefinition[] definitions =
                cachedDatabase != null ? cachedDatabase.States : null;
            var valid = (definitions ??
                         Array.Empty<GameStateDatabase.StateDefinition>())
                .Where(definition => definition != null)
                .Where(definition =>
                    !string.IsNullOrWhiteSpace(definition.state))
                .GroupBy(definition => definition.state.Trim())
                .Select(group => group.First())
                .OrderBy(definition => definition.state.Trim())
                .ToArray();

            cachedIds = valid
                .Select(definition => definition.state.Trim())
                .ToArray();
            cachedDescriptions = valid.ToDictionary(
                definition => definition.state.Trim(),
                definition => definition.description?.Trim() ?? string.Empty);
        }

        private static string GetDescription(string stateId)
        {
            return !string.IsNullOrWhiteSpace(stateId) &&
                   cachedDescriptions != null &&
                   cachedDescriptions.TryGetValue(stateId, out string description)
                ? description
                : string.Empty;
        }

        public static void ClearCache()
        {
            cachedDatabase = null;
            cachedIds = null;
            cachedDescriptions = null;
        }

        private sealed class StateDropdown : AdvancedDropdown
        {
            private readonly string[] ids;
            private readonly Dictionary<string, string> descriptions;
            private readonly Action<string> onSelected;

            public StateDropdown(
                AdvancedDropdownState state,
                string[] ids,
                Dictionary<string, string> descriptions,
                Action<string> onSelected) : base(state)
            {
                this.ids = ids;
                this.descriptions = descriptions;
                this.onSelected = onSelected;
                minimumSize = new Vector2(340f, 280f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Select Game State");
                root.AddChild(new StateItem("<None>", string.Empty));
                foreach (string id in ids)
                {
                    descriptions.TryGetValue(id, out string description);
                    string label = string.IsNullOrWhiteSpace(description)
                        ? id
                        : $"{id} — {description}";
                    root.AddChild(new StateItem(label, id));
                }

                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is StateItem state)
                {
                    onSelected(state.Id);
                }
            }

            private sealed class StateItem : AdvancedDropdownItem
            {
                public StateItem(string name, string id) : base(name)
                {
                    Id = id;
                }

                public string Id { get; }
            }
        }
    }
}
