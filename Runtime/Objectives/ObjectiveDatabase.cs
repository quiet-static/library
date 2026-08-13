using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuietStatic.Toolkit.Objectives
{
    /// <summary>
    /// Project-level catalog used to resolve stable objective IDs.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ObjectiveDatabase",
        menuName = "Quiet Static Toolkit/Objectives/Objective Database")]
    public sealed class ObjectiveDatabase : ScriptableObject
    {
        [Tooltip("All reusable objectives known to this project, ordered from lowest to highest automatic-activation priority.")]
        [SerializeField] private ObjectiveDefinition[] objectives;

        /// <summary>Gets the configured objective definitions.</summary>
        public IReadOnlyList<ObjectiveDefinition> Objectives =>
            objectives ?? Array.Empty<ObjectiveDefinition>();

        /// <summary>Finds an objective by its stable ID.</summary>
        public ObjectiveDefinition FindById(string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(objectiveId) || objectives == null)
            {
                return null;
            }

            string normalizedId = objectiveId.Trim();

            foreach (ObjectiveDefinition objective in objectives)
            {
                if (objective != null &&
                    string.Equals(
                        objective.Id,
                        normalizedId,
                        StringComparison.Ordinal))
                {
                    return objective;
                }
            }

            return null;
        }

        /// <summary>Returns whether the database contains the supplied definition.</summary>
        public bool Contains(ObjectiveDefinition objective)
        {
            return objective != null && FindById(objective.Id) == objective;
        }
    }
}
