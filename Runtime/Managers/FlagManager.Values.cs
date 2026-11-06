using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuietStatic.Toolkit.Flags
{
    public partial class FlagManager
    {
        private readonly Dictionary<string, FlagDatabase.FlagDefinition> definitions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FlagValue> typedValues = new(StringComparer.Ordinal);
        /// <summary>All explicitly assigned catalogs, including the legacy database, without repeats.</summary>
        public IEnumerable<FlagDatabase> Databases => new[] { flagDatabase }
            .Concat(additionalFlagDatabases ?? Array.Empty<FlagDatabase>()).Where(db => db != null).Distinct();
        /// <summary>Raised after a typed value changes; Boolean changes also retain their legacy events.</summary>
        public event Action<string, FlagValue> ValueChanged;

        private bool IsBooleanFlag(string id) => !definitions.TryGetValue(id, out var definition) ||
            definition.valueType == FlagValueType.Boolean;

        /// <summary>Reads the current value, or its catalog default. Uncatalogued IDs are Boolean only.</summary>
        public bool TryGetValue(string id, out FlagValue value)
        {
            value = default;
            if (!IsKnownFlag(id)) return false;
            id = id.Trim();
            if (IsBooleanFlag(id)) { value = FlagValue.From(activeFlags.Contains(id)); return true; }
            value = typedValues.TryGetValue(id, out var current) ? current : definitions[id].DefaultValue;
            return true;
        }

        /// <summary>Sets a value of the declared type. Unknown IDs, type mismatches and nonfinite floats fail.</summary>
        public bool SetValue(string id, FlagValue value)
        {
            if (!value.IsValid || !TryGetValue(id, out var previous) || previous.Type != value.Type) return false;
            id = id.Trim();
            if (previous.Equals(value)) return true;
            if (value.Type == FlagValueType.Boolean)
            {
                if (value.Boolean) SetFlag(id); else ClearFlag(id);
                return true;
            }
            typedValues[id] = value;
            ValueChanged?.Invoke(id, value);
            FlagsChanged?.Invoke();
            return true;
        }
        /// <summary>Sets a declared Boolean flag; returns false for invalid IDs, values, or kinds.</summary>
        public bool SetBoolean(string id, bool value) => SetValue(id, FlagValue.From(value));
        /// <summary>Sets a declared Integer flag; returns false for invalid IDs, values, or kinds.</summary>
        public bool SetInteger(string id, int value) => SetValue(id, FlagValue.From(value));
        /// <summary>Sets a declared Float flag; returns false for invalid IDs, values, or kinds.</summary>
        public bool SetFloat(string id, float value) => SetValue(id, FlagValue.From(value));
        /// <summary>Sets a declared String flag; returns false for invalid IDs, values, or kinds.</summary>
        public bool SetString(string id, string value) => SetValue(id, FlagValue.From(value));
        /// <summary>Adds an integer delta without overflow or type conversion.</summary>
        public bool AddInteger(string id, int delta)
        {
            if (!TryGetValue(id, out var value) || value.Type != FlagValueType.Integer) return false;
            long next = (long)value.Integer + delta;
            return next >= int.MinValue && next <= int.MaxValue && SetInteger(id, (int)next);
        }
        /// <summary>Adds a float delta, rejecting nonfinite results.</summary>
        public bool AddFloat(string id, float delta) => TryGetValue(id, out var value) &&
            value.Type == FlagValueType.Float && SetFloat(id, value.Float + delta);

        /// <summary>Captures detached typed values, including defaults and Boolean false values.</summary>
        public List<FlagValueEntry> CaptureValues() => definitions.Keys.Concat(activeFlags)
            .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => { TryGetValue(id, out var value); return new FlagValueEntry(id, value); }).ToList();

        /// <summary>Restores Boolean and typed values together before dependency evaluation and notification.
        /// Missing typed payloads in legacy saves use catalog defaults. Invalid entries are rejected.</summary>
        public void RestoreValues(IEnumerable<string> booleanFlags, IEnumerable<FlagValueEntry> values)
        {
            var nextFlags = new HashSet<string>(StringComparer.Ordinal);
            var nextValues = new Dictionary<string, FlagValue>(StringComparer.Ordinal);
            foreach (string id in booleanFlags ?? Array.Empty<string>())
                if (IsKnownFlag(id) && IsBooleanFlag(id.Trim())) nextFlags.Add(id.Trim());
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in values ?? Array.Empty<FlagValueEntry>())
            {
                if (entry == null || !TryGetValue(entry.flagId, out var expected) ||
                    !entry.value.IsValid || entry.value.Type != expected.Type || !seen.Add(entry.flagId.Trim()))
                    throw new ArgumentException("Invalid, duplicate, unknown or mistyped saved flag value.", nameof(values));
                string id = entry.flagId.Trim();
                if (entry.value.Type == FlagValueType.Boolean)
                { if (entry.value.Boolean) nextFlags.Add(id); else nextFlags.Remove(id); }
                else nextValues.Add(id, entry.value);
            }
            activeFlags.Clear(); activeFlags.UnionWith(nextFlags);
            typedValues.Clear(); foreach (var entry in nextValues) typedValues.Add(entry.Key, entry.Value);
            ApplyDependencies();
            FlagsChanged?.Invoke();
        }
    }
}
