using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Core
{
    /// <summary>Defines the valid string identifiers used by global game state.</summary>
    [CreateAssetMenu(menuName = "Quiet Static Toolkit/State/Game State Database")]
    public class GameStateDatabase : ScriptableObject
    {
        /// <summary>One valid game-state identifier and its editor documentation.</summary>
        [Serializable]
        public class StateDefinition
        {
            [Tooltip("Unique string ID used by runtime game-state systems.")]
            /// <summary>Runtime game-state identifier.</summary>
            public string state;

            [Tooltip("Editor-facing explanation of what this state represents.")]
            [TextArea(2, 5)]
            /// <summary>Editor-facing documentation for this state.</summary>
            public string description;
        }

        [Tooltip("Known game-state identifiers and their editor-facing descriptions.")]
        [SerializeField] private StateDefinition[] states;

        /// <summary>Gets the known game-state definitions.</summary>
        public StateDefinition[] States => states;

        /// <summary>Returns whether this database contains the supplied state ID.</summary>
        public bool Contains(string stateId)
        {
            if (string.IsNullOrWhiteSpace(stateId) || states == null)
            {
                return false;
            }

            string normalizedId = stateId.Trim();
            foreach (StateDefinition definition in states)
            {
                if (definition != null &&
                    string.Equals(
                        definition.state?.Trim(),
                        normalizedId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns editor-facing documentation for a state ID.</summary>
        public string GetDescription(string stateId)
        {
            if (string.IsNullOrWhiteSpace(stateId) || states == null)
            {
                return string.Empty;
            }

            string normalizedId = stateId.Trim();
            foreach (StateDefinition definition in states)
            {
                if (definition != null &&
                    string.Equals(
                        definition.state?.Trim(),
                        normalizedId,
                        StringComparison.Ordinal))
                {
                    return definition.description?.Trim() ?? string.Empty;
                }
            }

            return string.Empty;
        }
    }
}
