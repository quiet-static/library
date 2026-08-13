using System.Collections.Generic;
using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>Project-level catalog used to browse and resolve cinematic definitions.</summary>
    [CreateAssetMenu(menuName = "Quiet Static Toolkit/Cinematics/Cinematic Database")]
    public sealed class CinematicDatabase : ScriptableObject
    {
        [Tooltip("Reusable cinematic definitions known to this project.")]
        [SerializeField] private List<CinematicDefinition> cinematics = new();

        public IReadOnlyList<CinematicDefinition> Cinematics => cinematics;

        /// <summary>Finds a cinematic by its stable ID.</summary>
        public CinematicDefinition Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            for (int i = 0; i < cinematics.Count; i++)
            {
                CinematicDefinition item = cinematics[i];
                if (item != null && string.Equals(item.Id, id, System.StringComparison.Ordinal))
                    return item;
            }
            return null;
        }
    }
}
