using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Flags
{
    /// <summary>Typed comparison operations. Ordering is supported only for numeric values.</summary>
    public enum FlagComparisonOperator { Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual }

    /// <summary>One condition on a typed flag, usable by any FlagRequirement.</summary>
    [Serializable]
    public sealed class FlagComparison
    {
        [FlagId, Tooltip("Flag to compare.")] public string flagId;
        [Tooltip("Comparison operation; ordering requires Integer or Float.")] public FlagComparisonOperator comparison;
        [Tooltip("Expected value. Its type must match the catalog.")] public FlagValue expected;
        /// <summary>Evaluates without coercing kinds; unknown IDs and invalid comparisons fail closed.</summary>
        public bool IsMet(FlagManager manager)
        {
            return manager != null && manager.TryGetValue(flagId, out var actual) && IsMet(actual);
        }

        /// <summary>Compares an explicitly supplied value, also used by editor-only path simulation.</summary>
        public bool IsMet(FlagValue actual)
        {
            if (!expected.IsValid || !actual.IsValid || actual.Type != expected.Type) return false;
            if (comparison == FlagComparisonOperator.Equal) return actual.Equals(expected);
            if (comparison == FlagComparisonOperator.NotEqual) return !actual.Equals(expected);
            int order;
            if (actual.Type == FlagValueType.Integer) order = actual.Integer.CompareTo(expected.Integer);
            else if (actual.Type == FlagValueType.Float) order = actual.Float.CompareTo(expected.Float);
            else return false;
            return comparison switch
            {
                FlagComparisonOperator.Greater => order > 0,
                FlagComparisonOperator.GreaterOrEqual => order >= 0,
                FlagComparisonOperator.Less => order < 0,
                FlagComparisonOperator.LessOrEqual => order <= 0,
                _ => false
            };
        }
    }
}
