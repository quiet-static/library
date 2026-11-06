using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Flags
{
    /// <summary>Stable serialized kinds; Boolean is zero for existing assets.</summary>
    public enum FlagValueType { Boolean = 0, Integer = 1, Float = 2, String = 3 }

    /// <summary>A serializable, explicitly typed progression value. No implicit conversions.</summary>
    [Serializable]
    public struct FlagValue : IEquatable<FlagValue>
    {
        [SerializeField, Tooltip("The stored value kind.")] private FlagValueType type;
        [SerializeField, Tooltip("Boolean payload.")] private bool boolean;
        [SerializeField, Tooltip("Integer payload.")] private int integer;
        [SerializeField, Tooltip("Finite float payload.")] private float number;
        [SerializeField, Tooltip("String payload; null is normalized to empty.")] private string text;
        /// <summary>Gets the stored kind.</summary>
        public FlagValueType Type => type;
        /// <summary>Gets the Boolean payload. Check Type before reading.</summary>
        public bool Boolean => boolean;
        /// <summary>Gets the Integer payload. Check Type before reading.</summary>
        public int Integer => integer;
        /// <summary>Gets the Float payload. Check Type before reading.</summary>
        public float Float => number;
        /// <summary>Gets the String payload. Check Type before reading.</summary>
        public string String => text ?? string.Empty;
        /// <summary>Whether the kind and its payload are supported.</summary>
        public bool IsValid => Enum.IsDefined(typeof(FlagValueType), type) &&
            (type != FlagValueType.Float || (!float.IsNaN(number) && !float.IsInfinity(number)));
        /// <summary>Creates an explicitly typed Boolean value.</summary>
        public static FlagValue From(bool value) => new() { type = FlagValueType.Boolean, boolean = value };
        /// <summary>Creates an explicitly typed Integer value.</summary>
        public static FlagValue From(int value) => new() { type = FlagValueType.Integer, integer = value };
        /// <summary>Creates an explicitly typed Float value.</summary>
        public static FlagValue From(float value) => new() { type = FlagValueType.Float, number = value };
        /// <summary>Creates an explicitly typed String value.</summary>
        public static FlagValue From(string value) => new() { type = FlagValueType.String, text = value ?? string.Empty };
        public bool Equals(FlagValue other) => type == other.type && (type switch
        {
            FlagValueType.Boolean => boolean == other.boolean,
            FlagValueType.Integer => integer == other.integer,
            FlagValueType.Float => number.Equals(other.number),
            FlagValueType.String => string.Equals(String, other.String, StringComparison.Ordinal),
            _ => false
        });
        public override bool Equals(object obj) => obj is FlagValue other && Equals(other);
        public override int GetHashCode() => type switch
        {
            FlagValueType.Boolean => HashCode.Combine(type, boolean),
            FlagValueType.Integer => HashCode.Combine(type, integer),
            FlagValueType.Float => HashCode.Combine(type, number),
            _ => HashCode.Combine(type, String)
        };
    }

    /// <summary>One stable ID and its value in an Inspector operation or save snapshot.</summary>
    [Serializable]
    public sealed class FlagValueEntry
    {
        [FlagId, Tooltip("Globally unique flag ID.")] public string flagId;
        [Tooltip("Typed value; must match the catalog definition.")] public FlagValue value;
        /// <summary>Creates an empty serializable entry.</summary>
        public FlagValueEntry() { }
        /// <summary>Creates a snapshot or assignment for a stable ID.</summary>
        public FlagValueEntry(string flagId, FlagValue value) { this.flagId = flagId; this.value = value; }
    }
}
