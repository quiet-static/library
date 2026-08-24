using System;
using UnityEngine;

namespace QuietStatic.Toolkit.Cinematics
{
    /// <summary>
    /// Draws a serialized shot-ID string as a selector sourced from a sibling camera director.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CinematicShotIdAttribute : PropertyAttribute
    {
        /// <summary>Creates a director-backed cinematic shot selector.</summary>
        /// <param name="directorFieldName">
        /// Name of the sibling serialized field containing the camera director.
        /// </param>
        public CinematicShotIdAttribute(string directorFieldName)
        {
            DirectorFieldName = directorFieldName;
        }

        /// <summary>Gets the sibling camera-director field name.</summary>
        public string DirectorFieldName { get; }

    }
}
