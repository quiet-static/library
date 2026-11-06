using System;
using System.Collections.Generic;
using System.Linq;
using QuietStatic.Toolkit.Flags;
using UnityEditor;

namespace QuietStatic.Toolkit.Editor
{
    /// <summary>Shared lossless serialization and preflight for typed narrative conditions.</summary>
    internal static class FlagComparisonSerialization
    {
        internal static void Validate(IEnumerable<FlagComparison> conditions, string label, ICollection<string> errors)
        {
            foreach (var condition in conditions ?? Array.Empty<FlagComparison>())
                if (condition == null || string.IsNullOrWhiteSpace(condition.flagId) ||
                    condition.flagId != condition.flagId.Trim() || !condition.expected.IsValid ||
                    !Enum.IsDefined(typeof(FlagComparisonOperator), condition.comparison) ||
                    (condition.comparison != FlagComparisonOperator.Equal && condition.comparison != FlagComparisonOperator.NotEqual &&
                     condition.expected.Type != FlagValueType.Integer && condition.expected.Type != FlagValueType.Float))
                    errors.Add($"{label} has an invalid typed comparison.");
        }

        internal static void Write(SerializedProperty property, IEnumerable<FlagComparison> conditions)
        {
            var values = conditions?.ToArray() ?? Array.Empty<FlagComparison>();
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                var target = property.GetArrayElementAtIndex(index);
                var condition = values[index];
                target.FindPropertyRelative("flagId").stringValue = condition.flagId.Trim();
                target.FindPropertyRelative("comparison").intValue = (int)condition.comparison;
                var expected = target.FindPropertyRelative("expected");
                expected.FindPropertyRelative("type").intValue = (int)condition.expected.Type;
                expected.FindPropertyRelative("boolean").boolValue = condition.expected.Boolean;
                expected.FindPropertyRelative("integer").intValue = condition.expected.Integer;
                expected.FindPropertyRelative("number").floatValue = condition.expected.Float;
                expected.FindPropertyRelative("text").stringValue = condition.expected.String;
            }
        }
    }
}
