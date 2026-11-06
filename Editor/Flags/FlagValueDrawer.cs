using QuietStatic.Toolkit.Flags;
using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Flags.Editor
{
    /// <summary>Shows the selected value kind and only its matching payload.</summary>
    [CustomPropertyDrawer(typeof(FlagValue))]
    public sealed class FlagValueDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            2 * EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var kind = property.FindPropertyRelative("type");
            EditorGUI.PropertyField(row, kind, label);
            row.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            string payload = (FlagValueType)kind.intValue switch
            {
                FlagValueType.Boolean => "boolean", FlagValueType.Integer => "integer",
                FlagValueType.Float => "number", _ => "text"
            };
            EditorGUI.PropertyField(row, property.FindPropertyRelative(payload), new GUIContent("Value"));
            EditorGUI.EndProperty();
        }
    }
}
