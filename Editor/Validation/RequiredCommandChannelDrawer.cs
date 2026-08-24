using UnityEditor;
using UnityEngine;

namespace QuietStatic.Toolkit.Editor.Validation
{
    /// <summary>
    /// Shows the command-channel role and required-assignment state where the field is authored.
    /// Project-wide sender/receiver composition remains the responsibility of
    /// <see cref="ArchitectureValidation"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(RequiredCommandChannelAttribute))]
    public sealed class RequiredCommandChannelDrawer : PropertyDrawer
    {
        private const float InspectorHorizontalPadding = 40f;

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            float fieldHeight = EditorGUI.GetPropertyHeight(property, label, true);
            return RequiresMessage(property)
                ? fieldHeight + EditorGUIUtility.standardVerticalSpacing +
                  GetMessageHeight(
                      property,
                      Mathf.Max(
                          1f,
                          EditorGUIUtility.currentViewWidth -
                          InspectorHorizontalPadding))
                : fieldHeight;
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            RequiredCommandChannelAttribute marker =
                (RequiredCommandChannelAttribute)attribute;
            string role = marker.IsReceiver ? "Receiver" : "Sender";
            GUIContent roleLabel = new(
                $"{label.text} ({role})",
                BuildTooltip(label.tooltip, role));

            float fieldHeight = EditorGUI.GetPropertyHeight(property, roleLabel, true);
            Rect fieldRect = new(position.x, position.y, position.width, fieldHeight);

            EditorGUI.BeginProperty(position, roleLabel, property);
            EditorGUI.PropertyField(fieldRect, property, roleLabel, true);

            if (RequiresMessage(property))
            {
                string message = GetMessage(property, role);
                Rect messageRect = new(
                    position.x,
                    fieldRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                    position.width,
                    GetMessageHeight(property, position.width));

                EditorGUI.HelpBox(
                    messageRect,
                    message,
                    MessageType.Error);
            }

            EditorGUI.EndProperty();
        }

        private bool SupportsCommandChannels(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.ObjectReference &&
                   fieldInfo != null &&
                   typeof(CrossSceneCommandChannel).IsAssignableFrom(fieldInfo.FieldType);
        }

        private bool RequiresMessage(SerializedProperty property)
        {
            if (!SupportsCommandChannels(property))
            {
                return true;
            }

            return !property.hasMultipleDifferentValues &&
                   property.objectReferenceValue == null;
        }

        private string GetMessage(
            SerializedProperty property,
            string role)
        {
            return !SupportsCommandChannels(property)
                ? "RequiredCommandChannel supports only CrossSceneCommandChannel fields."
                : $"Assign the required {role.ToLowerInvariant()} command channel.";
        }

        private float GetMessageHeight(
            SerializedProperty property,
            float width)
        {
            RequiredCommandChannelAttribute marker =
                (RequiredCommandChannelAttribute)attribute;
            string role = marker.IsReceiver ? "Receiver" : "Sender";
            return Mathf.Max(
                EditorGUIUtility.singleLineHeight,
                EditorStyles.helpBox.CalcHeight(
                    new GUIContent(GetMessage(property, role)),
                    Mathf.Max(1f, width)));
        }

        private static string BuildTooltip(string tooltip, string role)
        {
            string roleDescription = role == "Receiver"
                ? "Required receiver for commands raised through this channel."
                : "Required sender that raises commands through this channel.";
            return string.IsNullOrWhiteSpace(tooltip)
                ? roleDescription
                : $"{tooltip}\n\n{roleDescription}";
        }
    }
}
