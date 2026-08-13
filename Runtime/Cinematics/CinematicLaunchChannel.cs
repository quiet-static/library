using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>Carries one requested location/cinematic pair across a scene transition.</summary>
    [CreateAssetMenu(menuName = "Quiet Static Toolkit/Cinematics/Cinematic Launch Channel")]
    public sealed class CinematicLaunchChannel : ScriptableObject
    {
        [NonSerialized] private string pendingLocationId;
        [NonSerialized] private string pendingCinematicId;

        /// <summary>Gets whether a launch is waiting for a matching scene player.</summary>
        public bool HasPendingRequest => !string.IsNullOrEmpty(pendingLocationId);

        /// <summary>Replaces any pending launch with a new destination selection.</summary>
        public bool Request(string locationId, string cinematicId)
        {
            if (string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(cinematicId))
                return false;
            pendingLocationId = locationId.Trim();
            pendingCinematicId = cinematicId.Trim();
            return true;
        }

        /// <summary>Consumes the request only when the destination location matches.</summary>
        public bool TryConsume(string locationId, out string cinematicId)
        {
            cinematicId = string.Empty;
            if (!HasPendingRequest || !string.Equals(pendingLocationId, locationId, StringComparison.Ordinal))
                return false;
            cinematicId = pendingCinematicId;
            Clear();
            return true;
        }

        /// <summary>Discards a pending request after a rejected transition.</summary>
        public void Clear()
        {
            pendingLocationId = string.Empty;
            pendingCinematicId = string.Empty;
        }
    }
}
